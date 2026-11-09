using System.Globalization;
using System.Runtime.Versioning;
using RaidBridge.Game.Linux.Memory;

namespace RaidBridge.Game.Linux.Tests.Memory;

/// <summary>
/// A temporary folder laid out like the kernel's process file system: raid-bridge's own process, the boot time, Yama's
/// ptrace_scope, and the processes a test adds. Every id, path and namespace is synthetic.
/// </summary>
internal sealed class FakeProcFileSystem : IDisposable
{
	public const uint PlayerId = 1000;
	public const int GamePid = 4242;
	public const ulong ImageBase = 0x6FFF_EE53_0000;
	public const string HostNamespace = "user:[4026531837]";
	public const string ContainerNamespace = "user:[4026534213]";

	/// <summary>The game started an hour after boot: 360,000 ticks of 1/100 s.</summary>
	public const ulong GameStartTicks = 360_000;

	public static readonly DateTimeOffset BootedAt = new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);

	private readonly string _root = Directory.CreateTempSubdirectory("raid-bridge-procfs-").FullName;

	public FakeProcFileSystem()
	{
		Write("stat", $"cpu  1 2 3 4\nbtime {BootedAt.ToUnixTimeSeconds()}\nprocesses 99\n");
		Write(Path.Combine("self", "status"), Status(PlayerId, PlayerId, PlayerId));
		Link(Path.Combine("self", "ns", "user"), HostNamespace);
		PtraceScope(1);
	}

	/// <summary>The player, as raid-bridge should run: their own user, no capabilities.</summary>
	public static LinuxUser Player { get; } = new(PlayerId, PlayerId, 0);

	/// <summary>Where the fake Game client loaded GameAssembly.dll from. Nothing is there.</summary>
	public string GameAssemblyPath => Path.Combine(_root, "games", "raid", "GameAssembly.dll");

	public ProcFileSystem FileSystem => new(_root);

	/// <summary>The Game client as Proton runs it: Raid.exe in Steam's container, GameAssembly.dll's header mapped from offset 0.</summary>
	public void AddGameClient(uint userId = PlayerId, string userNamespace = ContainerNamespace, string? maps = null) =>
		AddProcess(GamePid, "Raid.exe", userId, userNamespace, maps ?? GameClientMaps(GameAssemblyPath));

	public void AddProcess(int pid, string comm, uint userId = PlayerId, string userNamespace = ContainerNamespace, string maps = "")
	{
		var process = pid.ToString(CultureInfo.InvariantCulture);
		Write(Path.Combine(process, "comm"), $"{comm}\n");
		Write(Path.Combine(process, "stat"), $"{pid} ({comm}) S {string.Join(' ', Enumerable.Repeat("0", 18))} {GameStartTicks} 0 0\n");
		Write(Path.Combine(process, "status"), Status(userId, userId, userId));
		Write(Path.Combine(process, "maps"), maps);
		Link(Path.Combine(process, "ns", "user"), userNamespace);
	}

	/// <summary>A process directory with nothing in it, as one that exits while raid-bridge looks.</summary>
	public void AddExitedProcess(int pid) => Directory.CreateDirectory(Path.Combine(_root, pid.ToString(CultureInfo.InvariantCulture)));

	/// <summary>The process's real, effective and saved user ids differ.</summary>
	public void UserIds(int pid, uint real, uint effective, uint saved) =>
		Write(Path.Combine(pid.ToString(CultureInfo.InvariantCulture), "status"), Status(real, effective, saved));

	/// <summary>Replaces one of the process's files, as a kernel raid-bridge doesn't know might write it.</summary>
	public void Garble(int pid, string file, string text) => Write(Path.Combine(pid.ToString(CultureInfo.InvariantCulture), file), text);

	/// <summary>Linux won't show the process's comm, as /proc mounted with hidepid does for another user's process.</summary>
	[UnsupportedOSPlatform("windows")]
	public void HideComm(int pid) =>
		File.SetUnixFileMode(Path.Combine(_root, pid.ToString(CultureInfo.InvariantCulture), "comm"), UnixFileMode.None);

	/// <summary>Linux won't show the process's maps, as for another user's process.</summary>
	[UnsupportedOSPlatform("windows")]
	public void HideMaps(int pid) =>
		File.SetUnixFileMode(Path.Combine(_root, pid.ToString(CultureInfo.InvariantCulture), "maps"), UnixFileMode.None);

	public void PtraceScope(int scope) => Write(Path.Combine("sys", "kernel", "yama", "ptrace_scope"), $"{scope}\n");

	public void RemoveYama() => File.Delete(Path.Combine(_root, "sys", "kernel", "yama", "ptrace_scope"));

	/// <summary>Maps as Wine lays out a loaded GameAssembly.dll: the header page from the file, and the rest at other offsets or anonymous.</summary>
	public static string GameClientMaps(string gameAssemblyPath) =>
		$"""
		140000000-140001000 r--p 00000000 00:24 7610001                    /games/raid/Raid.exe
		{ImageBase:x}-{ImageBase + 0x1000:x} r--p 00000000 00:24 7610756                    {gameAssemblyPath}
		{ImageBase + 0x1000:x}-{ImageBase + 0x5000000:x} r-xp 00001000 00:24 7610756                    {gameAssemblyPath}
		{ImageBase + 0x5000000:x}-{ImageBase + 0x5200000:x} rw-p 00000000 00:00 0
		7ffc8a1d2000-7ffc8a1f3000 rw-p 00000000 00:00 0                          [stack]

		""";

	public void Dispose() => Directory.Delete(_root, recursive: true);

	private static string Status(uint real, uint effective, uint saved) =>
		$"Name:\tsome-process\nUid:\t{real}\t{effective}\t{saved}\t{effective}\nGid:\t{real}\t{effective}\t{saved}\t{effective}\nCapEff:\t0000000000000000\n";

	private void Write(string relativePath, string text)
	{
		var path = Path.Combine(_root, relativePath);
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		File.WriteAllText(path, text);
	}

	/// <summary>A namespace link points at no file, like the kernel's, and only its text matters.</summary>
	private void Link(string relativePath, string target)
	{
		var path = Path.Combine(_root, relativePath);
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		File.CreateSymbolicLink(path, target);
	}
}
