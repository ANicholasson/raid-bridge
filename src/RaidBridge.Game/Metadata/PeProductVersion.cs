using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Text;

namespace RaidBridge.Game.Metadata;

/// <summary>
/// The ProductVersion string in a Windows PE file's version resource, such as UnityPlayer.dll's
/// "6000.3.18f1 (5ebeb53e4c07)". .NET reads it only on Windows, so the BCL's PEReader maps the file, and this walks
/// the resource directory to RT_VERSION and the VS_VERSIONINFO block tree to the string.
/// </summary>
internal sealed class PeProductVersion
{
	private const int ResourceTypeVersion = 16;

	private readonly byte[] _bytes;
	private readonly string _path;

	private PeProductVersion(byte[] bytes, string path)
	{
		_bytes = bytes;
		_path = path;
	}

	public static string Read(Stream file, string path)
	{
		try
		{
			using var pe = new PEReader(file, PEStreamOptions.LeaveOpen);
			var directory = pe.PEHeaders.PEHeader?.ResourceTableDirectory ?? default;
			if (directory.Size == 0)
			{
				throw Unreadable(path, "it has no resource directory");
			}

			var (rva, size) = new PeProductVersion(Section(pe, directory.RelativeVirtualAddress, directory.Size, path), path).VersionResource();
			return new PeProductVersion(Section(pe, rva, size, path), path).ProductVersion();
		}
		catch (BadImageFormatException exception)
		{
			throw new GameMetadataException($"Can't read the product version of {path}: it isn't a valid PE file ({exception.Message}).", exception);
		}
	}

	private static byte[] Section(PEReader pe, int rva, int size, string path)
	{
		var block = pe.GetSectionData(rva);
		if (size < 0 || block.Length < size)
		{
			throw Unreadable(path, $"no section holds the {size} bytes at address 0x{rva:X}");
		}

		return [.. block.GetContent(0, size)];
	}

	/// <summary>
	/// The address and size of the version resource: the RT_VERSION entry of the resource directory's type level,
	/// then the first name and the first language under it. Offsets inside the directory count from its start.
	/// </summary>
	private (int Rva, int Size) VersionResource()
	{
		var names = Subdirectory(Entry(0, ResourceTypeVersion));
		var languages = Subdirectory(FirstEntry(names));
		var dataEntry = FirstEntry(languages);
		if ((dataEntry & 0x8000_0000) != 0)
		{
			throw Unreadable(_path, "its version resource is a directory, not data");
		}

		return (Int32((int)dataEntry), Int32((int)dataEntry + 4));
	}

	/// <summary>The entry with this id in an IMAGE_RESOURCE_DIRECTORY, whose named entries come before its id entries.</summary>
	private uint Entry(int directory, int id)
	{
		int named = UInt16(directory + 12);
		int ids = UInt16(directory + 14);
		for (var index = named; index < named + ids; index++)
		{
			var entry = directory + 16 + (index * 8);
			if (UInt32(entry) == id)
			{
				return UInt32(entry + 4);
			}
		}

		throw Unreadable(_path, "it has no RT_VERSION resource");
	}

	private uint FirstEntry(int directory)
	{
		if (UInt16(directory + 12) + UInt16(directory + 14) == 0)
		{
			throw Unreadable(_path, $"its resource directory at 0x{directory:X} is empty");
		}

		return UInt32(directory + 16 + 4);
	}

	private int Subdirectory(uint entry) =>
		(entry & 0x8000_0000) != 0
			? (int)(entry & 0x7FFF_FFFF)
			: throw Unreadable(_path, "its version resource is data where a directory belongs");

	private string ProductVersion()
	{
		var root = Block(0);
		if (root.Key != "VS_VERSION_INFO")
		{
			throw Unreadable(_path, $"its version resource starts with \"{root.Key}\", not VS_VERSION_INFO");
		}

		var productVersion = Children(root)
			.Where(child => child.Key == "StringFileInfo")
			.SelectMany(Children)
			.SelectMany(Children)
			.FirstOrDefault(entry => entry.Key == "ProductVersion")
			?? throw Unreadable(_path, "its version resource has no ProductVersion");

		return Utf16(productVersion.ValueStart, productVersion.ValueStart + productVersion.ValueLength).TrimEnd('\0');
	}

	private IEnumerable<VersionBlock> Children(VersionBlock parent)
	{
		for (var child = parent.ChildrenStart; child < parent.End; child = Align(Block(child).End))
		{
			yield return Block(child);
		}
	}

	/// <summary>A VS_VERSIONINFO block: length, value length, type (1 for text, counted in UTF-16 units), key, value, children.</summary>
	private VersionBlock Block(int start)
	{
		var length = UInt16(start);
		var valueLength = UInt16(start + 2);
		var isText = UInt16(start + 4) == 1;
		var keyStart = start + 6;
		var keyEnd = keyStart;
		while (UInt16(keyEnd) != 0)
		{
			keyEnd += 2;
		}

		var valueStart = Align(keyEnd + 2);
		var valueBytes = isText ? valueLength * 2 : valueLength;
		var end = start + length;
		if (length < 6 || valueStart + valueBytes > end || end > _bytes.Length)
		{
			throw Unreadable(_path, $"its version resource has a malformed block at 0x{start:X}");
		}

		return new(Utf16(keyStart, keyEnd), valueStart, valueBytes, Align(valueStart + valueBytes), end);
	}

	private sealed record VersionBlock(string Key, int ValueStart, int ValueLength, int ChildrenStart, int End);

	private static int Align(int offset) => (offset + 3) & ~3;

	private ushort UInt16(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(Bytes(offset, sizeof(ushort)));

	private uint UInt32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(Bytes(offset, sizeof(uint)));

	private int Int32(int offset) => BinaryPrimitives.ReadInt32LittleEndian(Bytes(offset, sizeof(int)));

	private string Utf16(int start, int end) => Encoding.Unicode.GetString(Bytes(start, end - start));

	private ReadOnlySpan<byte> Bytes(int offset, int length) =>
		offset >= 0 && length >= 0 && offset <= _bytes.Length - length
			? _bytes.AsSpan(offset, length)
			: throw Unreadable(_path, $"its version resource ends before byte 0x{offset:X}");

	private static GameMetadataException Unreadable(string path, string why) =>
		new($"Can't read the product version of {path}: {why}.");
}
