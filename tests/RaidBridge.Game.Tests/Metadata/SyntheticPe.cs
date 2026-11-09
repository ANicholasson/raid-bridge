using System.Buffers.Binary;
using System.Text;

namespace RaidBridge.Game.Tests.Metadata;

/// <summary>
/// A minimal 64-bit PE file with one .rsrc section holding a version resource, laid out as Windows does: a resource
/// directory three levels deep (type, name, language) over a VS_VERSIONINFO block tree.
/// </summary>
internal static class SyntheticPe
{
	private const int ResourceFileOffset = 0x200;
	private const uint ResourceRva = 0x1000;
	private const int VersionInfoOffset = 0x58;

	public static byte[] WithProductVersion(string productVersion) =>
		WithStrings(new() { ["CompanyName"] = "Unity Technologies", ["ProductVersion"] = productVersion });

	public static byte[] WithStrings(Dictionary<string, string> strings, int resourceType = 16)
	{
		var versionInfo = Block("VS_VERSION_INFO", FixedFileInfo(), isText: false,
			Block("StringFileInfo", [], isText: true,
				Block("040904b0", [], isText: true, [.. strings.Select(pair => Block(pair.Key, Utf16(pair.Value + "\0"), isText: true))])));

		var resources = new byte[VersionInfoOffset + versionInfo.Length];
		Directory(resources, 0x00, id: (uint)resourceType, entry: 0x8000_0000 | 0x18);
		Directory(resources, 0x18, id: 1, entry: 0x8000_0000 | 0x30);
		Directory(resources, 0x30, id: 0x409, entry: 0x48);
		Write32(resources, 0x48, ResourceRva + VersionInfoOffset);
		Write32(resources, 0x4C, (uint)versionInfo.Length);
		versionInfo.CopyTo(resources, VersionInfoOffset);

		var file = new byte[ResourceFileOffset + resources.Length];
		Write16(file, 0x00, 0x5A4D);
		Write32(file, 0x3C, 0x40);
		Write32(file, 0x40, 0x0000_4550);
		Write16(file, 0x44, 0x8664);
		Write16(file, 0x46, 1);
		Write16(file, 0x54, 240);
		const int optionalHeader = 0x58;
		Write16(file, optionalHeader, 0x20B);
		Write32(file, optionalHeader + 32, 0x1000);
		Write32(file, optionalHeader + 36, 0x200);
		Write32(file, optionalHeader + 56, ResourceRva + 0x1000);
		Write32(file, optionalHeader + 60, ResourceFileOffset);
		Write32(file, optionalHeader + 108, 16);
		Write32(file, optionalHeader + 112 + 16, ResourceRva);
		Write32(file, optionalHeader + 112 + 20, (uint)resources.Length);
		const int section = optionalHeader + 240;
		Encoding.ASCII.GetBytes(".rsrc").CopyTo(file, section);
		Write32(file, section + 8, (uint)resources.Length);
		Write32(file, section + 12, ResourceRva);
		Write32(file, section + 16, (uint)resources.Length);
		Write32(file, section + 20, ResourceFileOffset);
		Write32(file, section + 36, 0x4000_0040);
		resources.CopyTo(file, ResourceFileOffset);
		return file;
	}

	private static void Directory(byte[] resources, int offset, uint id, uint entry)
	{
		Write16(resources, offset + 14, 1);
		Write32(resources, offset + 16, id);
		Write32(resources, offset + 20, entry);
	}

	/// <summary>A version block padded to four bytes, so it can sit directly before a sibling.</summary>
	private static byte[] Block(string key, byte[] value, bool isText, params byte[][] children)
	{
		var bytes = new List<byte>(new byte[6]);
		bytes.AddRange(Utf16(key + "\0"));
		Pad(bytes);
		bytes.AddRange(value);
		Pad(bytes);
		foreach (var child in children)
		{
			bytes.AddRange(child);
		}

		var block = bytes.ToArray();
		Write16(block, 0, (ushort)block.Length);
		Write16(block, 2, (ushort)(isText ? value.Length / 2 : value.Length));
		Write16(block, 4, (ushort)(isText ? 1 : 0));
		return block;
	}

	private static byte[] FixedFileInfo()
	{
		var info = new byte[52];
		Write32(info, 0, 0xFEEF04BD);
		return info;
	}

	private static void Pad(List<byte> bytes)
	{
		while (bytes.Count % 4 != 0)
		{
			bytes.Add(0);
		}
	}

	private static byte[] Utf16(string text) => Encoding.Unicode.GetBytes(text);

	private static void Write16(byte[] bytes, int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);

	private static void Write32(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
}
