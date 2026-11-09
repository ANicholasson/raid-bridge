using RaidBridge.Game.Memory;

namespace RaidBridge.Game.Linux.Memory;

/// <summary>
/// The Game client's process under Proton: the one whose comm is Raid.exe, with GameAssembly.dll's image base and file
/// taken from the line of its maps that maps the file from offset 0, where the PE header is.
/// </summary>
internal sealed record GameProcess(int Id, DateTimeOffset StartedAt, GameModule GameAssembly)
{
	private const string Comm = "Raid.exe";
	private const string GameAssemblyFile = "GameAssembly.dll";

	/// <summary>The suffix the kernel puts on a mapped file's path once the file is deleted or replaced.</summary>
	private const string Deleted = " (deleted)";

	public static GameProcess Find(ProcFileSystem proc, LinuxUser user)
	{
		var pid = proc.ProcessesNamed(Comm) switch
		{
			[] => throw new GameMemoryException($"The Game client isn't running: no process is named {Comm}. Start RAID from Steam, then try again."),
			[var only] => only,
			var several => throw new GameMemoryException($"{several.Count} processes are named {Comm} ({string.Join(", ", several)}), so raid-bridge can't tell which is the Game client. Close all but one, then try again."),
		};

		try
		{
			return new(pid, proc.StartedAt(pid), GameAssemblyIn(proc.Mappings(pid), pid));
		}
		catch (UnauthorizedAccessException)
		{
			throw ReadDenial.Explain(proc, pid, user);
		}
		catch (IOException)
		{
			throw new GameMemoryException($"The Game client (pid {pid}) exited while raid-bridge looked for it.");
		}
	}

	private static GameModule GameAssemblyIn(IReadOnlyList<Mapping> mappings, int pid)
	{
		// Distinct, because the kernel can repeat a line when the mappings change while raid-bridge reads them (code review, #24).
		var images = mappings
			.Where(mapping => mapping.FileOffset == 0 && Path.GetFileName(WithoutDeleted(mapping.Path)) == GameAssemblyFile)
			.Distinct()
			.ToList();

		return images switch
		{
			[] => throw new GameMemoryException($"The Game client (pid {pid}) hasn't loaded {GameAssemblyFile} yet. Wait until RAID shows its main screen, then try again."),
			[var image] when image.Path.EndsWith(Deleted, StringComparison.Ordinal) => throw new GameMemoryException(
				$"{WithoutDeleted(image.Path)} was replaced after the Game client loaded it: the game updated under the running Game client. Restart the Game client and try again."),
			[var image] => new(image.Start, image.Path),
			_ => throw new GameMemoryException(
				$"The Game client (pid {pid}) maps {GameAssemblyFile} from offset 0 more than once ({string.Join(", ", images.Select(image => $"0x{image.Start:X}"))}), so raid-bridge can't tell which is its image."),
		};
	}

	private static string WithoutDeleted(string path) => path.EndsWith(Deleted, StringComparison.Ordinal) ? path[..^Deleted.Length] : path;
}
