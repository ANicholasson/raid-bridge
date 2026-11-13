using RaidBridge.Game.Il2Cpp;

namespace RaidBridge.Game.Localization;

/// <summary>
/// Names as the Game client shows them. A name is a SharedLTextKey: its key is looked up in the client's loaded
/// localization tables, ClientStaticData's StaticDataLocalization and then its ClientLocalization, and its default
/// text stands in when neither holds the key. A name with neither is refused, never made up (spec #20).
/// </summary>
internal sealed class LocalizedNames(ManagedObject staticData)
{
	private const string TextKey = "SharedModel.Common.Localization.SharedLTextKey";
	private const string Table = "System.Collections.Generic.Dictionary`2<System.String, System.String>";
	private static readonly string[] TableFields = ["StaticDataLocalization", "ClientLocalization"];

	private readonly List<IReadOnlyDictionary<string, ManagedDictionary.Entry>> _tables = [];

	/// <summary>The name a SharedLTextKey field of <paramref name="owner"/> gives, for the <paramref name="named"/> thing, such as "Champion type 5240".</summary>
	public string Of(ManagedObject owner, string fieldName, string named)
	{
		var name = owner.Object(fieldName, TextKey);
		var key = name.String("Key").Value;
		if (key is { Length: > 0 } && Localized(key) is { } localized)
		{
			return localized;
		}

		return name.String("DefaultValue").Value is { Length: > 0 } defaultText
			? defaultText
			: throw new GameDataException(
				$"{named} has no name: its {TextKey} at 0x{name.Address:X} has the key \"{key}\", which isn't in the Game client's localization tables, and no default text.");
	}

	/// <summary>
	/// The text a SharedLTextKey field of <paramref name="owner"/> gives, for the <paramref name="named"/> thing, from the
	/// localization tables only. Its default text is refused, not stood in: for a Mission it is a developer's note, such
	/// as "Have 5 heroes level 60", not the text the screen shows
	/// (https://github.com/ANicholasson/raid-bridge/issues/33).
	/// </summary>
	public string InTables(ManagedObject owner, string fieldName, string named)
	{
		var name = owner.Object(fieldName, TextKey);
		var key = name.String("Key").Value;
		return key is { Length: > 0 } && Localized(key) is { } localized
			? localized
			: throw new GameDataException(
				$"{named} has no text: its {TextKey} at 0x{name.Address:X} has the key \"{key}\", which isn't in the Game client's localization tables.");
	}

	/// <summary>
	/// The text the first table holding the key gives it, passing over a table that holds the key with no text (code
	/// review, #25). Each table is read when a key isn't in the ones before it.
	/// </summary>
	private string? Localized(string key)
	{
		for (var index = 0; index < TableFields.Length; index++)
		{
			if (index == _tables.Count)
			{
				_tables.Add(staticData.Dictionary(TableFields[index], Table).ByStringKey());
			}

			if (_tables[index].TryGetValue(key, out var entry) && entry.String() is { Length: > 0 } text)
			{
				return text;
			}
		}

		return null;
	}
}
