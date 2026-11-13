using System.Buffers.Binary;
using System.ComponentModel;
using System.Text;
using RaidBridge.Game.Windows.Memory;

namespace RaidBridge.Game.Windows.Tests.Memory;

/// <summary>Made-up Windows processes for the Windows adapter, with each Win32 call's answer set by the test.</summary>
internal sealed class FakeWin32Processes : IWin32Processes
{
	public const int GameId = 4242;
	public const ulong ExeBase = 0x7FF6_0000_0000;
	public const ulong ImageBase = 0x7FF8_0000_0000;
	public const ulong NtdllBase = 0x7FFC_0000_0000;
	public const string GameDirectory = @"C:\Users\player\AppData\Local\PlariumPlay\StandAloneApps\raid-shadow-legends\build";
	public const string ExePath = GameDirectory + @"\Raid.exe";
	public const string GameAssemblyPath = GameDirectory + @"\GameAssembly.dll";

	public static readonly DateTimeOffset StartedAt = new(2026, 11, 13, 20, 0, 0, TimeSpan.Zero);

	private readonly Dictionary<string, List<int>> _named = [];
	private readonly Dictionary<int, FakeWin32Process> _processes = [];
	private readonly Dictionary<int, int> _openErrors = [];

	public List<FakeWin32Process> Opened { get; } = [];

	/// <summary>Raid.exe, which exports nothing, GameAssembly.dll and ntdll.dll, as Windows loads them.</summary>
	public FakeWin32Process AddGameClient(int id = GameId) =>
		AddProcess(id, "Raid")
			.Load(ExeBase, Image(exportedAs: null))
			.Load(ImageBase, Image("GameAssembly.dll"))
			.Load(NtdllBase, Image("ntdll.dll"));

	public FakeWin32Process AddProcess(int id, string name)
	{
		if (!_named.TryGetValue(name, out var ids))
		{
			_named[name] = ids = [];
		}

		ids.Add(id);
		return _processes[id] = new FakeWin32Process(id);
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

	/// <summary>
	/// The headers of a 64-bit PE image as a loader maps them: the DOS header pointing at the PE header, an optional
	/// header whose first data directory points at an export directory naming the image, or no exports.
	/// </summary>
	public static byte[] Image(string? exportedAs, ushort magic = 0x20B)
	{
		const int ntOffset = 0x80;
		const int exportsAt = 0x400;
		const int nameAt = 0x440;
		var image = new byte[0x1000];
		"MZ"u8.CopyTo(image);
		BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(0x3C), ntOffset);
		"PE\0\0"u8.CopyTo(image.AsSpan(ntOffset));
		var optional = image.AsSpan(ntOffset + 24);
		BinaryPrimitives.WriteUInt16LittleEndian(optional, magic);
		var (directoryCount, exports) = magic == 0x20B ? (108, 112) : (92, 96);
		BinaryPrimitives.WriteUInt32LittleEndian(optional[directoryCount..], 16);
		if (exportedAs is not null)
		{
			BinaryPrimitives.WriteUInt32LittleEndian(optional[exports..], exportsAt);
			BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(exportsAt + 12), nameAt);
			Encoding.ASCII.GetBytes(exportedAs).CopyTo(image.AsSpan(nameAt));
		}

		return image;
	}
}

/// <summary>A made-up process opened for reading, its memory the images loaded into it.</summary>
internal sealed class FakeWin32Process(int id) : IWin32Process
{
	private readonly SortedDictionary<ulong, byte[]> _images = [];

	public int Id => id;

	public string Path { get; set; } = FakeWin32Processes.ExePath;

	public int? ModulesError { get; set; }

	public int? PathError { get; set; }

	public bool Exited { get; set; }

	public bool Disposed { get; private set; }

	public int Reads { get; private set; }

	/// <summary>Answers each read as ReadProcessMemory would, from the images loaded unless a test sets otherwise.</summary>
	public ReadAnswer? Answer { get; set; }

	public FakeWin32Process Load(ulong imageBase, byte[] image)
	{
		_images[imageBase] = image;
		return this;
	}

	public DateTimeOffset StartedAt() => FakeWin32Processes.StartedAt;

	public bool HasExited() => Exited;

	public string ImagePath() => PathError is { } error ? throw new Win32Exception(error) : Path;

	public IReadOnlyList<ulong> ModuleBases() => ModulesError is { } error ? throw new Win32Exception(error) : [.. _images.Keys];

	public (int Read, int Error) Read(ulong address, Span<byte> buffer)
	{
		Reads++;
		if (Answer is { } answer)
		{
			return answer(address, buffer);
		}

		foreach (var (imageBase, image) in _images)
		{
			if (address >= imageBase && address < imageBase + (ulong)image.Length)
			{
				var available = image.AsSpan((int)(address - imageBase));
				var copied = Math.Min(available.Length, buffer.Length);
				available[..copied].CopyTo(buffer);
				return copied == buffer.Length ? (copied, 0) : (copied, 299);
			}
		}

		return (0, 299);
	}

	public void Dispose() => Disposed = true;
}

internal delegate (int Read, int Error) ReadAnswer(ulong address, Span<byte> buffer);
