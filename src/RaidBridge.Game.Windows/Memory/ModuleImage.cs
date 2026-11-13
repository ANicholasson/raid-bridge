using System.Buffers.Binary;
using System.Text;
using RaidBridge.Game.Memory;

namespace RaidBridge.Game.Windows.Memory;

/// <summary>Fills the buffer from the Game client's memory at the address, or throws <see cref="GameMemoryException"/>.</summary>
internal delegate void ReadMemory(ulong address, Span<byte> buffer);

/// <summary>
/// A module's PE image as the Game client loaded it, read from its headers in memory. A module is told by the name its
/// export directory gives it, as the linker wrote it, because asking Windows for a module's file needs
/// PROCESS_QUERY_INFORMATION, a right raid-bridge doesn't ask for (https://github.com/ANicholasson/raid-bridge/pull/49).
/// </summary>
internal static class ModuleImage
{
	private const ushort Pe32 = 0x10B;
	private const ushort Pe32Plus = 0x20B;

	/// <summary>The furthest into the image the PE header may start, as the Windows loader allows it.</summary>
	private const int MaxPeHeaderOffset = 0x1000;

	/// <summary>The PE signature, then IMAGE_FILE_HEADER, then the optional header up to and including its first data directory, the exports.</summary>
	private const int NtHeadersRead = 4 + 20 + 120;

	/// <summary>Whether the image at the address exports under the name, ignoring case as Windows does.</summary>
	public static bool ExportsAs(ReadMemory read, ulong imageBase, string name)
	{
		Span<byte> dos = stackalloc byte[0x40];
		read(imageBase, dos);
		if (dos[0] != 'M' || dos[1] != 'Z')
		{
			throw NotAnImage(imageBase, "it doesn't start with MZ");
		}

		var ntOffset = BinaryPrimitives.ReadInt32LittleEndian(dos[0x3C..]);
		if (ntOffset is < 0x40 or > MaxPeHeaderOffset)
		{
			throw NotAnImage(imageBase, $"its PE header would start at 0x{ntOffset:X}");
		}

		Span<byte> nt = stackalloc byte[NtHeadersRead];
		read(imageBase + (ulong)ntOffset, nt);
		if (!nt[..4].SequenceEqual("PE\0\0"u8))
		{
			throw NotAnImage(imageBase, $"it has no PE signature at 0x{ntOffset:X}");
		}

		var optional = nt[24..];
		var (directoryCount, exports) = BinaryPrimitives.ReadUInt16LittleEndian(optional) switch
		{
			Pe32Plus => (108, 112),
			Pe32 => (92, 96),
			var magic => throw NotAnImage(imageBase, $"its optional header's magic is 0x{magic:X}"),
		};
		var exportDirectory = BinaryPrimitives.ReadUInt32LittleEndian(optional[exports..]);
		if (BinaryPrimitives.ReadUInt32LittleEndian(optional[directoryCount..]) == 0 || exportDirectory == 0)
		{
			return false;
		}

		// IMAGE_EXPORT_DIRECTORY keeps the RVA of the image's name 12 bytes in.
		Span<byte> directory = stackalloc byte[16];
		read(imageBase + exportDirectory, directory);
		var nameAt = BinaryPrimitives.ReadUInt32LittleEndian(directory[12..]);
		if (nameAt == 0)
		{
			return false;
		}

		// The name and its terminating NUL, so a longer name that starts the same doesn't match.
		Span<byte> exported = stackalloc byte[name.Length + 1];
		read(imageBase + nameAt, exported);
		return exported[^1] == 0 && Ascii.EqualsIgnoreCase(exported[..^1], name);
	}

	private static GameMemoryException NotAnImage(ulong imageBase, string why) =>
		new($"The module the Game client loaded at 0x{imageBase:X} isn't a PE image: {why}.");
}
