using System.ComponentModel;
using System.Runtime.Versioning;
using RaidBridge.Game.Memory;

namespace RaidBridge.Game.Windows.Memory;

/// <summary>
/// The Windows memory adapter (ADR-0002): the Game client as Plarium Play runs it, opened with PROCESS_VM_READ and
/// PROCESS_QUERY_LIMITED_INFORMATION and nothing more (ADR-0001). It needs no setup: the player's own user may read a
/// process it started.
/// </summary>
public sealed class WindowsGameMemory : IGameMemory
{
	private const string ImageName = "Raid";
	private const string GameAssemblyFile = "GameAssembly.dll";

	private const int ErrorAccessDenied = 5;
	private const int ErrorInvalidParameter = 87;
	private const int ErrorPartialCopy = 299;
	private const int ErrorNoAccess = 998;

	private readonly IWin32Process _process;

	private WindowsGameMemory(IWin32Process process, DateTimeOffset startedAt, GameModule gameAssembly)
	{
		_process = process;
		StartedAt = startedAt;
		GameAssembly = gameAssembly;
	}

	public DateTimeOffset StartedAt { get; }

	public GameModule GameAssembly { get; }

	/// <summary>Finds the running Game client, or throws <see cref="GameMemoryException"/> naming why it can't.</summary>
	[SupportedOSPlatform("windows")]
	public static WindowsGameMemory Open() => Open(Kernel32Processes.Host);

	internal static WindowsGameMemory Open(IWin32Processes processes)
	{
		var id = processes.IdsNamed(ImageName) switch
		{
			[] => throw new GameMemoryException($"The Game client isn't running: no process is named {ImageName}.exe. Start RAID from Plarium Play, then try again."),
			[var only] => only,
			var several => throw new GameMemoryException($"{several.Count} processes are named {ImageName}.exe ({string.Join(", ", several)}), so raid-bridge can't tell which is the Game client. Close all but one, then try again."),
		};

		var process = OpenGameClient(processes, id);
		try
		{
			return new(process, StartedAtOf(process), GameAssemblyIn(process));
		}
		catch
		{
			process.Dispose();
			throw;
		}
	}

	public void Read(ulong address, Span<byte> buffer) => ReadFrom(_process, address, buffer);

	private static void ReadFrom(IWin32Process process, ulong address, Span<byte> buffer)
	{
		var (read, error) = process.Read(address, buffer);
		if (read == buffer.Length && error == 0)
		{
			return;
		}

		throw error switch
		{
			ErrorPartialCopy or ErrorNoAccess when read > 0 => new GameMemoryException($"Only {read} of {buffer.Length} bytes at 0x{address:X} are mapped in the Game client."),
			ErrorPartialCopy or ErrorNoAccess when HasExited(process) => new GameMemoryException($"The Game client (pid {process.Id}) has exited."),
			ErrorPartialCopy or ErrorNoAccess => new GameMemoryException($"Nothing is mapped at 0x{address:X} for {buffer.Length} bytes in the Game client."),
			_ => new GameMemoryException($"Reading {buffer.Length} bytes at 0x{address:X} from the Game client failed with Windows error {error}."),
		};
	}

	private static IWin32Process OpenGameClient(IWin32Processes processes, int id)
	{
		try
		{
			return processes.Open(id);
		}
		catch (Win32Exception error) when (error.NativeErrorCode == ErrorAccessDenied)
		{
			throw new GameMemoryException(
				$"Windows doesn't let raid-bridge read the Game client (pid {id}). raid-bridge asks only to read it, with your own user's rights: run raid-bridge as the user who plays RAID, and start RAID as that user, not as administrator.",
				error);
		}
		catch (Win32Exception error) when (error.NativeErrorCode == ErrorInvalidParameter)
		{
			throw new GameMemoryException($"The Game client (pid {id}) exited while raid-bridge looked for it.", error);
		}
		catch (Win32Exception error)
		{
			throw new GameMemoryException($"Opening the Game client (pid {id}) to read it failed with Windows error {error.NativeErrorCode}: {error.Message}", error);
		}
	}

	private static DateTimeOffset StartedAtOf(IWin32Process process)
	{
		try
		{
			return process.StartedAt();
		}
		catch (Win32Exception error)
		{
			throw new GameMemoryException($"Asking when the Game client (pid {process.Id}) started failed with Windows error {error.NativeErrorCode}: {error.Message}", error);
		}
	}

	private static IReadOnlyList<ulong> ModuleBases(IWin32Process process)
	{
		try
		{
			return process.ModuleBases();
		}
		catch (Win32Exception error) when (error.NativeErrorCode == ErrorPartialCopy)
		{
			throw new GameMemoryException($"The Game client (pid {process.Id}) is still starting: Windows can't list its modules yet. Wait until RAID shows its main screen, then try again.", error);
		}
		catch (Win32Exception error)
		{
			throw new GameMemoryException($"Listing the modules of the Game client (pid {process.Id}) failed with Windows error {error.NativeErrorCode}: {error.Message}", error);
		}
	}

	/// <summary>
	/// GameAssembly.dll: the one module whose image exports under that name, and the file of that name beside Raid.exe,
	/// where Unity's player loads it from. GameSnapshotSource checks that file's headers against the image's.
	/// </summary>
	private static GameModule GameAssemblyIn(IWin32Process process)
	{
		var id = process.Id;
		var images = ModuleBases(process).Where(imageBase => ModuleImage.ExportsAs((address, buffer) => ReadFrom(process, address, buffer), imageBase, GameAssemblyFile)).ToList();

		return images switch
		{
			[] => throw new GameMemoryException($"The Game client (pid {id}) hasn't loaded {GameAssemblyFile} yet. Wait until RAID shows its main screen, then try again."),
			[var image] => new(image, Beside(ImagePathOf(process), GameAssemblyFile)),
			_ => throw new GameMemoryException(
				$"The Game client (pid {id}) has loaded {GameAssemblyFile} more than once ({string.Join(", ", images.Select(image => $"0x{image:X}"))}), so raid-bridge can't tell which is its image."),
		};
	}

	private static string ImagePathOf(IWin32Process process)
	{
		try
		{
			return process.ImagePath();
		}
		catch (Win32Exception error)
		{
			throw new GameMemoryException($"Asking for the file of the Game client (pid {process.Id}) failed with Windows error {error.NativeErrorCode}: {error.Message}", error);
		}
	}

	/// <summary>The file of that name in the folder of a Windows path, whichever platform the tests run on.</summary>
	private static string Beside(string path, string fileName) => path[..(path.LastIndexOfAny(['\\', '/']) + 1)] + fileName;

	private static bool HasExited(IWin32Process process)
	{
		try
		{
			return process.HasExited();
		}
		catch (Win32Exception)
		{
			// Whether it exited can't be told, so the read's own failure is named instead.
			return false;
		}
	}
}
