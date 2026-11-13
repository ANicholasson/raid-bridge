namespace RaidBridge.Game.Windows.Memory;

/// <summary>
/// The processes Windows runs, as the Windows adapter sees them: one seam over its Win32 calls, so the tests can stand
/// in for Windows. A failed call throws <see cref="System.ComponentModel.Win32Exception"/> with Windows' error code.
/// </summary>
internal interface IWin32Processes
{
	/// <summary>The ids of the running processes whose image is named so, without its .exe.</summary>
	IReadOnlyList<int> IdsNamed(string name);

	/// <summary>Opens the process for reading and nothing more.</summary>
	IWin32Process Open(int id);
}

/// <summary>A process opened for reading. Disposing it closes its handle.</summary>
internal interface IWin32Process : IDisposable
{
	int Id { get; }

	DateTimeOffset StartedAt();

	bool HasExited();

	/// <summary>The modules the process has loaded, each with its image base and the path of its file.</summary>
	IReadOnlyList<LoadedModule> Modules();

	/// <summary>Copies from the process's memory at the address into the buffer: the bytes copied, and Windows' error code if it copied fewer than asked, else 0.</summary>
	(int Read, int Error) Read(ulong address, Span<byte> buffer);
}

/// <summary>A module a process loaded: its image base and the path of its file.</summary>
internal sealed record LoadedModule(ulong ImageBase, string FilePath);
