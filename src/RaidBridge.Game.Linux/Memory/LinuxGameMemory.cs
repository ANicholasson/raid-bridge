using System.Runtime.Versioning;
using RaidBridge.Game.Memory;

namespace RaidBridge.Game.Linux.Memory;

/// <summary>
/// The Linux memory adapter (ADR-0002): the Game client running under Steam's Proton, read with process_vm_readv and
/// nothing else (ADR-0001). It needs no setup: Steam's container already lets the player's own user read the game (#13).
/// </summary>
public sealed class LinuxGameMemory : IGameMemory
{
	private const int Eperm = 1;
	private const int Esrch = 3;
	private const int Efault = 14;

	private readonly GameProcess _process;
	private readonly ProcFileSystem _proc;
	private readonly LinuxUser _user;
	private readonly ProcessVmRead _read;

	private LinuxGameMemory(GameProcess process, ProcFileSystem proc, LinuxUser user, ProcessVmRead read)
	{
		_process = process;
		_proc = proc;
		_user = user;
		_read = read;
	}

	public DateTimeOffset StartedAt => _process.StartedAt;

	public GameModule GameAssembly => _process.GameAssembly;

	/// <summary>Finds the running Game client, or throws <see cref="GameMemoryException"/> naming why it can't.</summary>
	[SupportedOSPlatform("linux")]
	public static LinuxGameMemory Open() => Open(ProcFileSystem.Host, LinuxUser.Current(ProcFileSystem.Host), Libc.ReadProcessVm);

	internal static LinuxGameMemory Open(ProcFileSystem proc, LinuxUser user, ProcessVmRead read)
	{
		user.RefuseMoreThanThePlayersOwnRights();
		return new(GameProcess.Find(proc, user), proc, user, read);
	}

	public void Read(ulong address, Span<byte> buffer)
	{
		var (read, error) = _read(_process.Id, address, buffer);
		if (read == buffer.Length)
		{
			return;
		}

		throw error switch
		{
			0 => new GameMemoryException($"Only {read} of {buffer.Length} bytes at 0x{address:X} are mapped in the Game client."),
			Efault => new GameMemoryException($"Nothing is mapped at 0x{address:X} for {buffer.Length} bytes in the Game client."),
			Esrch => new GameMemoryException($"The Game client (pid {_process.Id}) has exited."),
			Eperm => ReadDenial.Explain(_proc, _process.Id, _user),
			_ => new GameMemoryException($"Reading {buffer.Length} bytes at 0x{address:X} from the Game client failed with errno {error}."),
		};
	}
}

/// <summary>Copies from the process's memory at the address into the buffer: the bytes copied, or -1 and the errno.</summary>
internal delegate (int Read, int Error) ProcessVmRead(int pid, ulong address, Span<byte> buffer);
