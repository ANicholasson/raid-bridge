using System.ComponentModel;
using RaidBridge.Game.Windows.Memory;

namespace RaidBridge.Game.Windows.Tests.Memory;

/// <summary>Made-up Windows processes for the Windows adapter, with each Win32 call's answer set by the test.</summary>
internal sealed class FakeWin32Processes : IWin32Processes
{
	public const int GameId = 4242;
	public const ulong ImageBase = 0x7FF8_0000_0000;
	public const string GameDirectory = @"C:\Users\player\AppData\Local\PlariumPlay\StandAloneApps\raid-shadow-legends\build\9.30";
	public const string GameAssemblyPath = GameDirectory + @"\GameAssembly.dll";

	public static readonly DateTimeOffset StartedAt = new(2026, 11, 13, 20, 0, 0, TimeSpan.Zero);

	private readonly Dictionary<string, List<int>> _named = [];
	private readonly Dictionary<int, FakeWin32Process> _processes = [];
	private readonly Dictionary<int, int> _openErrors = [];

	public List<FakeWin32Process> Opened { get; } = [];

	public FakeWin32Process AddGameClient(int id = GameId) =>
		AddProcess(id, "Raid", new LoadedModule(0x7FF6_0000_0000, GameDirectory + @"\Raid.exe"), new LoadedModule(ImageBase, GameAssemblyPath), new LoadedModule(0x7FFC_0000_0000, @"C:\Windows\System32\ntdll.dll"));

	public FakeWin32Process AddProcess(int id, string name, params LoadedModule[] modules)
	{
		if (!_named.TryGetValue(name, out var ids))
		{
			_named[name] = ids = [];
		}

		ids.Add(id);
		return _processes[id] = new FakeWin32Process(id, [.. modules]);
	}

	/// <summary>OpenProcess fails on the process with Windows' error code.</summary>
	public void RefuseToOpen(int id, int error) => _openErrors[id] = error;

	public IReadOnlyList<int> IdsNamed(string name) => _named.TryGetValue(name, out var ids) ? ids : [];

	public IWin32Process Open(int id)
	{
		if (_openErrors.TryGetValue(id, out var error))
		{
			throw new Win32Exception(error);
		}

		var process = _processes[id];
		Opened.Add(process);
		return process;
	}
}

/// <summary>A made-up process opened for reading.</summary>
internal sealed class FakeWin32Process(int id, List<LoadedModule> modules) : IWin32Process
{
	public int Id => id;

	public int? ModulesError { get; set; }

	public bool Exited { get; set; }

	public bool Disposed { get; private set; }

	public int Reads { get; private set; }

	/// <summary>Answers each read as ReadProcessMemory would: the bytes copied, and the error code if fewer than asked.</summary>
	public ReadAnswer Answer { get; set; } = (_, buffer) => (buffer.Length, 0);

	public DateTimeOffset StartedAt() => FakeWin32Processes.StartedAt;

	public bool HasExited() => Exited;

	public IReadOnlyList<LoadedModule> Modules() => ModulesError is { } error ? throw new Win32Exception(error) : modules;

	public (int Read, int Error) Read(ulong address, Span<byte> buffer)
	{
		Reads++;
		return Answer(address, buffer);
	}

	public void Dispose() => Disposed = true;
}

internal delegate (int Read, int Error) ReadAnswer(ulong address, Span<byte> buffer);
