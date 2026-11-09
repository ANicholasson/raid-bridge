using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace RaidBridge.Game.Linux.Memory;

/// <summary>
/// The only native functions raid-bridge calls (spec #20): process_vm_readv to read the Game client, and getuid and
/// geteuid to know who it runs as. The guardrails fail on any other import.
/// </summary>
[SupportedOSPlatform("linux")]
internal static unsafe partial class Libc
{
	/// <summary>Copies from the process's memory at the address into the buffer: the bytes copied, or -1 and the errno.</summary>
	public static (int Read, int Error) ReadProcessVm(int pid, ulong address, Span<byte> buffer)
	{
		fixed (byte* local = buffer)
		{
			var localVector = new IoVec((nint)local, (nuint)buffer.Length);
			var remoteVector = new IoVec(unchecked((nint)address), (nuint)buffer.Length);
			var read = ProcessVmReadv(pid, &localVector, 1, &remoteVector, 1, 0);
			return read < 0 ? (-1, Marshal.GetLastPInvokeError()) : ((int)read, 0);
		}
	}

	[LibraryImport("libc", EntryPoint = "process_vm_readv", SetLastError = true)]
	private static partial nint ProcessVmReadv(int pid, IoVec* localVectors, nuint localCount, IoVec* remoteVectors, nuint remoteCount, nuint flags);

	[LibraryImport("libc", EntryPoint = "getuid")]
	public static partial uint GetUserId();

	[LibraryImport("libc", EntryPoint = "geteuid")]
	public static partial uint GetEffectiveUserId();

	/// <summary>The kernel's struct iovec: where a buffer starts and how long it is.</summary>
	private readonly record struct IoVec(nint Base, nuint Length);
}
