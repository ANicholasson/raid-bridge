using System.Text;
using System.Text.RegularExpressions;

namespace RaidBridge.Game.Metadata;

/// <summary>
/// The Unity version in data.unity3d's UnityFS header: the signature "UnityFS", a four-byte format version, the
/// player version ("5.x.x") and the engine version, each string null-terminated.
/// </summary>
internal static partial class UnityBundleHeader
{
	/// <summary>More than the header needs; the bundle itself is large and only its start is read.</summary>
	public const int MaxLength = 128;

	private const string Signature = "UnityFS";

	public static string UnityVersion(ReadOnlySpan<byte> header, string path)
	{
		var position = 0;
		if (NullTerminated(header, ref position, path) != Signature)
		{
			throw new GameMetadataException($"{path} isn't a Unity bundle: it doesn't start with {Signature}.");
		}

		position += sizeof(uint);
		_ = NullTerminated(header, ref position, path);
		var engineVersion = NullTerminated(header, ref position, path);
		if (!UnityVersionPattern().IsMatch(engineVersion))
		{
			throw new GameMetadataException($"{path} names Unity version \"{engineVersion}\", which isn't a Unity version.");
		}

		return engineVersion;
	}

	private static string NullTerminated(ReadOnlySpan<byte> header, ref int position, string path)
	{
		var length = position < header.Length ? header[position..].IndexOf((byte)0) : -1;
		if (length < 0)
		{
			throw new GameMetadataException($"{path}'s Unity bundle header ends early, at byte {position}.");
		}

		var value = Encoding.ASCII.GetString(header.Slice(position, length));
		position += length + 1;
		return value;
	}

	[GeneratedRegex(@"^\d+\.\d+\.\d+[abfpx]\d+$", RegexOptions.CultureInvariant)]
	private static partial Regex UnityVersionPattern();
}
