using System.Globalization;
using System.Text.RegularExpressions;
using RaidBridge.Game.Memory;

namespace RaidBridge.Game.Linux.Memory;

/// <summary>
/// The kernel's process file system, read and never written: what raid-bridge needs to know about the Game client's
/// process and its own. Its root is the real one, or a folder laid out like it for tests. Only this class knows the
/// formats of the files under it.
/// </summary>
internal sealed partial class ProcFileSystem(string root)
{
	/// <summary>
	/// USER_HZ, the unit of a process's start time in its stat file. The kernel fixes it at 100 for user space on every
	/// architecture .NET runs on; sysconf(_SC_CLK_TCK) would say the same, but the import allowlist keeps sysconf out.
	/// </summary>
	private const int TicksPerSecond = 100;

	public static ProcFileSystem Host { get; } = new("/proc");

	/// <summary>The ids of the processes whose comm, the name the kernel shows for them, is this one.</summary>
	public IReadOnlyList<int> ProcessesNamed(string comm) =>
	[
		.. Directory.EnumerateDirectories(root)
			.Select(directory => int.TryParse(Path.GetFileName(directory), NumberStyles.None, CultureInfo.InvariantCulture, out var pid) ? pid : 0)
			.Where(pid => pid > 0 && Comm(pid) == comm)
			.Order(),
	];

	/// <summary>The process's user ids and effective capabilities, from its status file.</summary>
	public ProcessStatus Status(int pid) => ReadStatus(Process(pid));

	/// <summary>raid-bridge's own user ids and effective capabilities.</summary>
	public ProcessStatus OwnStatus() => ReadStatus(Self);

	/// <summary>
	/// The latest the process can have started: the boot time plus its start time in ticks since boot, plus a second,
	/// because the boot time is whole seconds rounded down. A file written just before the launch then can't be taken
	/// for an update under the running game (code review, #24).
	/// </summary>
	public DateTimeOffset StartedAt(int pid)
	{
		var path = Path.Combine(Process(pid), "stat");
		var startTicks = Parse(path, stat =>
		{
			// The comm in parentheses can hold spaces and parentheses itself, so fields are counted after the last ')'.
			var fields = stat[(stat.LastIndexOf(')') + 2)..].Split(' ');
			// Field 22 of the stat file, counting the pid as field 1 and the comm as field 2.
			return ulong.Parse(fields[19], NumberStyles.None, CultureInfo.InvariantCulture);
		});
		return DateTimeOffset.FromUnixTimeSeconds(BootTime()).AddSeconds(((double)startTicks / TicksPerSecond) + 1);
	}

	/// <summary>The process's memory mappings, from its maps file. Throws <see cref="UnauthorizedAccessException"/> when Linux won't show them.</summary>
	public IReadOnlyList<Mapping> Mappings(int pid) =>
	[
		.. Read(Path.Combine(Process(pid), "maps"))
			.Split('\n', StringSplitOptions.RemoveEmptyEntries)
			.Select(line => MappingLine().Match(line) is { Success: true } match
				? new Mapping(
					ulong.Parse(match.Groups["start"].ValueSpan, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture),
					ulong.Parse(match.Groups["offset"].ValueSpan, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture),
					match.Groups["path"].Value)
				: throw new GameMemoryException($"Line \"{line}\" of process {pid}'s maps isn't a mapping.")),
	];

	/// <summary>The process's user namespace, such as "user:[4026531837]", or null when Linux won't show it.</summary>
	public string? UserNamespace(int pid) => ReadUserNamespace(Process(pid));

	/// <summary>raid-bridge's own user namespace.</summary>
	public string? OwnUserNamespace() => ReadUserNamespace(Self);

	/// <summary>Yama's kernel.yama.ptrace_scope, or null when the kernel has no Yama.</summary>
	public int? PtraceScope()
	{
		var path = Path.Combine(root, "sys", "kernel", "yama", "ptrace_scope");
		return File.Exists(path) ? Parse(path, scope => int.Parse(scope, NumberStyles.AllowTrailingWhite, CultureInfo.InvariantCulture)) : null;
	}

	private string Process(int pid) => Path.Combine(root, pid.ToString(CultureInfo.InvariantCulture));

	private string Self => Path.Combine(root, "self");

	/// <summary>
	/// The process's comm, or null when it exited while raid-bridge looked, or when Linux hides it, as /proc mounted
	/// with hidepid does for other users' processes (code review, #24).
	/// </summary>
	private string? Comm(int pid)
	{
		try
		{
			return Read(Path.Combine(Process(pid), "comm")).TrimEnd('\n');
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
		{
			return null;
		}
	}

	private static ProcessStatus ReadStatus(string process) =>
		Parse(Path.Combine(process, "status"), status =>
		{
			var lines = status.Split('\n')
				.Select(line => line.Split(':', 2))
				.Where(parts => parts.Length == 2)
				.ToDictionary(parts => parts[0], parts => parts[1].Trim(), StringComparer.Ordinal);
			var ids = lines["Uid"].Split('\t').Select(id => uint.Parse(id, NumberStyles.None, CultureInfo.InvariantCulture)).ToList();
			return new ProcessStatus(ids[0], ids[1], ids[2], ulong.Parse(lines["CapEff"], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture));
		});

	private static string? ReadUserNamespace(string process)
	{
		try
		{
			return new FileInfo(Path.Combine(process, "ns", "user")).LinkTarget;
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
		{
			return null;
		}
	}

	private long BootTime() =>
		Parse(Path.Combine(root, "stat"), stat =>
			long.Parse(stat.Split('\n').First(line => line.StartsWith("btime ", StringComparison.Ordinal)).AsSpan(6), NumberStyles.None, CultureInfo.InvariantCulture));

	private static string Read(string path) => File.ReadAllText(path);

	/// <summary>Reads and parses the file, and names it when it isn't what raid-bridge expects (code review, #24).</summary>
	private static T Parse<T>(string path, Func<string, T> parse)
	{
		var text = Read(path);
		try
		{
			return parse(text);
		}
		catch (Exception exception) when (exception is FormatException or OverflowException or IndexOutOfRangeException or ArgumentException or KeyNotFoundException or InvalidOperationException)
		{
			throw new GameMemoryException($"{path} isn't in the format raid-bridge reads.", exception);
		}
	}

	[GeneratedRegex(@"^(?<start>[0-9a-f]+)-[0-9a-f]+ \S+ (?<offset>[0-9a-f]+) \S+ \d+ *(?<path>.*)$", RegexOptions.CultureInvariant)]
	private static partial Regex MappingLine();
}

/// <summary>A process's real, effective and saved user ids, and the capabilities it can use now.</summary>
internal sealed record ProcessStatus(uint UserId, uint EffectiveUserId, uint SavedUserId, ulong EffectiveCapabilities);

/// <summary>One line of a process's maps: where the mapping starts, the offset in its file, and the file's path, empty when it has none.</summary>
internal sealed record Mapping(ulong Start, ulong FileOffset, string Path);
