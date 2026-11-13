using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace RaidBridge.Game.Windows.Memory;

/// <summary>
/// The only Win32 functions raid-bridge calls (ADR-0001): it opens the Game client with PROCESS_VM_READ and
/// PROCESS_QUERY_LIMITED_INFORMATION and nothing more, lists where its modules start, reads its memory, and asks for
/// its executable's path, when it started and whether it has exited. Microsoft documents each of these calls as
/// needing no more than those two rights, except K32EnumProcessModulesEx, whose page names none. Nothing asks Windows
/// for a module's file, which is documented to need PROCESS_QUERY_INFORMATION
/// (https://github.com/ANicholasson/raid-bridge/pull/49). The guardrails fail on any other import or access right.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class Kernel32Processes : IWin32Processes
{
	public static readonly Kernel32Processes Host = new();

	private Kernel32Processes()
	{
	}

	/// <summary>The ids of the processes whose image is named so, without its .exe, read from the system's process list without opening any.</summary>
	public IReadOnlyList<int> IdsNamed(string name)
	{
		var processes = Process.GetProcessesByName(name);
		try
		{
			return [.. processes.Select(process => process.Id).Order()];
		}
		finally
		{
			foreach (var process in processes)
			{
				process.Dispose();
			}
		}
	}

	public IWin32Process Open(int id) => Kernel32Process.Open(id);
}

/// <summary>A process opened for reading: its memory, its modules, its executable, when it started and whether it has exited.</summary>
[SupportedOSPlatform("windows")]
internal sealed unsafe partial class Kernel32Process : IWin32Process
{
	// Named as Win32 names them, so the guardrail can check the rights an OpenProcess call asks for.
#pragma warning disable IDE1006
	private const uint PROCESS_VM_READ = 0x0010;
	private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
#pragma warning restore IDE1006

	/// <summary>EnumProcessModulesEx's LIST_MODULES_ALL: 32-bit and 64-bit modules alike.</summary>
	private const uint ListModulesAll = 0x03;

	/// <summary>The exit code GetExitCodeProcess gives a process that is still running.</summary>
	private const uint StillActive = 259;

	/// <summary>The longest path Windows gives, in characters.</summary>
	private const int LongestPath = 32_767;

	private readonly SafeProcessHandle _process;

	private Kernel32Process(int id, SafeProcessHandle process)
	{
		Id = id;
		_process = process;
	}

	public int Id { get; }

	public static Kernel32Process Open(int id)
	{
		var process = OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_LIMITED_INFORMATION, inheritHandle: false, (uint)id);
		if (process.IsInvalid)
		{
			var error = Marshal.GetLastPInvokeError();
			process.Dispose();
			throw new Win32Exception(error);
		}

		return new(id, process);
	}

	public DateTimeOffset StartedAt() =>
		GetProcessTimes(_process, out var created, out _, out _, out _)
			? DateTimeOffset.FromFileTime(created)
			: throw new Win32Exception(Marshal.GetLastPInvokeError());

	public bool HasExited() =>
		GetExitCodeProcess(_process, out var exitCode)
			? exitCode != StillActive
			: throw new Win32Exception(Marshal.GetLastPInvokeError());

	public string ImagePath()
	{
		var path = new char[LongestPath];
		var length = (uint)path.Length;
		fixed (char* first = path)
		{
			return QueryFullProcessImageNameW(_process, 0, first, ref length)
				? new string(path, 0, (int)length)
				: throw new Win32Exception(Marshal.GetLastPInvokeError());
		}
	}

	public IReadOnlyList<ulong> ModuleBases()
	{
		var bases = new nint[256];
		while (true)
		{
			uint needed;
			fixed (nint* first = bases)
			{
				if (!EnumProcessModulesEx(_process, first, (uint)(bases.Length * sizeof(nint)), out needed, ListModulesAll))
				{
					throw new Win32Exception(Marshal.GetLastPInvokeError());
				}
			}

			var count = (int)(needed / sizeof(nint));
			if (count <= bases.Length)
			{
				return [.. bases.Take(count).Select(module => (ulong)module)];
			}

			bases = new nint[count];
		}
	}

	public (int Read, int Error) Read(ulong address, Span<byte> buffer)
	{
		fixed (byte* local = buffer)
		{
			return ReadProcessMemory(_process, (nuint)address, local, (nuint)buffer.Length, out var read)
				? ((int)read, 0)
				: ((int)read, Marshal.GetLastPInvokeError());
		}
	}

	public void Dispose() => _process.Dispose();

	[LibraryImport("kernel32", EntryPoint = "OpenProcess", SetLastError = true)]
	private static partial SafeProcessHandle OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

	[LibraryImport("kernel32", EntryPoint = "ReadProcessMemory", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static partial bool ReadProcessMemory(SafeProcessHandle process, nuint address, byte* buffer, nuint size, out nuint read);

	[LibraryImport("kernel32", EntryPoint = "GetProcessTimes", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static partial bool GetProcessTimes(SafeProcessHandle process, out long created, out long exited, out long kernel, out long user);

	[LibraryImport("kernel32", EntryPoint = "GetExitCodeProcess", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static partial bool GetExitCodeProcess(SafeProcessHandle process, out uint exitCode);

	[LibraryImport("kernel32", EntryPoint = "K32EnumProcessModulesEx", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static partial bool EnumProcessModulesEx(SafeProcessHandle process, nint* modules, uint size, out uint needed, uint filter);

	[LibraryImport("kernel32", EntryPoint = "QueryFullProcessImageNameW", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static partial bool QueryFullProcessImageNameW(SafeProcessHandle process, uint flags, char* name, ref uint size);
}
