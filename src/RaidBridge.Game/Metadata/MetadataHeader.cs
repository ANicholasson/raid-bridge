using System.Buffers.Binary;

namespace RaidBridge.Game.Metadata;

/// <summary>The version global-metadata.dat declares in its header, read before LibCpp2IL parses the rest.</summary>
internal static class MetadataHeader
{
	public const int Length = 8;

	private const uint Magic = 0xFAB11BAF;

	public static int Version(ReadOnlySpan<byte> header, string path)
	{
		if (header.Length < Length || BinaryPrimitives.ReadUInt32LittleEndian(header) != Magic)
		{
			throw new GameMetadataException($"{path} isn't IL2CPP metadata: it doesn't start with 0x{Magic:X8}.");
		}

		return BinaryPrimitives.ReadInt32LittleEndian(header[4..]);
	}
}
