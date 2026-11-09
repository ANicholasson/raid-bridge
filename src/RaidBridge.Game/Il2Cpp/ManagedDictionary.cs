using RaidBridge.Game.Metadata;

namespace RaidBridge.Game.Il2Cpp;

/// <summary>
/// A Dictionary&lt;TKey, TValue&gt; in the Game client's memory: the first <c>_count</c> slots of its <c>_entries</c>
/// array, less the <c>_freeCount</c> slots a removal freed, which hold a negative <c>hashCode</c>. The entries are
/// read in slot order, and only their keys until a value is asked for.
/// </summary>
/// <remarks>
/// That is the class library's corefx 2.x Dictionary, whose Entry declares an int <c>hashCode</c> and whose removal
/// sets it to -1. The later one marks a freed slot in <c>next</c> instead, and declares a uint <c>hashCode</c>, which
/// the declared-type check refuses by name; any other disagreement fails the free count check (code review, #25).
/// </remarks>
internal sealed class ManagedDictionary
{
	private const string EntryClass = "System.Collections.Generic.Dictionary`2+Entry";

	private readonly string _where;
	private readonly ulong _address;
	private readonly IReadOnlyList<ManagedObject> _entries;

	public ManagedDictionary(ManagedObject dictionary, string where)
	{
		_where = where;
		_address = dictionary.Address;

		// A Dictionary allocates its entries on its first addition, so an empty one may have none.
		var slots = dictionary.ArrayOrNull("_entries", "System.Collections.Generic.Dictionary`2+Entry<TKey, TValue>[]");
		var count = dictionary.Int32("_count").Value;
		var freeCount = dictionary.Int32("_freeCount").Value;
		var length = slots?.Length ?? 0;
		if (count < 0 || count > length || freeCount < 0 || freeCount > count)
		{
			throw Malformed($"its _count is {count} and its _freeCount {freeCount}, with {length} slots");
		}

		_entries = [.. Enumerable.Range(0, count).Select(index => slots!.Value(index, EntryClass)).Where(entry => entry.Int32("hashCode").Value >= 0)];
		if (_entries.Count != count - freeCount)
		{
			throw Malformed($"{_entries.Count} of its slots are in use, but its _count of {count} less its _freeCount of {freeCount} is {count - freeCount}");
		}
	}

	/// <summary>The entries by their int keys.</summary>
	public IReadOnlyDictionary<int, Entry> ByInt32Key() => ByKey(entry => entry.Int32("key", "TKey").Value);

	/// <summary>The entries by their enum keys, as the game's <paramref name="enumType"/> names them.</summary>
	public IReadOnlyDictionary<GameEnumMember, Entry> ByEnumKey(string enumType) => ByKey(entry => entry.EnumMember("key", enumType, declaredType: "TKey").Value);

	/// <summary>The entries by their string keys, none of which may be null.</summary>
	public IReadOnlyDictionary<string, Entry> ByStringKey() =>
		ByKey(entry => entry.String("key", "TKey") is { Value: { } key } ? key : throw Malformed($"an entry at 0x{entry.Address:X} has a null key"));

	private Dictionary<TKey, Entry> ByKey<TKey>(Func<ManagedObject, TKey> key)
		where TKey : notnull
	{
		var entries = new Dictionary<TKey, Entry>();
		foreach (var entry in _entries)
		{
			var entryKey = key(entry);
			var name = entryKey is GameEnumMember member ? member.Name : entryKey.ToString();
			if (!entries.TryAdd(entryKey, new(entry, $"{_where}[{name}]")))
			{
				throw Malformed($"it holds the key {name} twice");
			}
		}

		return entries;
	}

	private GameDataException Malformed(string why) => new($"{_where} at 0x{_address:X} is malformed: {why}.");

	/// <summary>
	/// One entry, whose value is read when it is asked for. An error about the value names the dictionary and the key,
	/// such as <c>HeroById[42]</c>, not only the class library's Entry.value (#38).
	/// </summary>
	public sealed class Entry(ManagedObject entry, string where)
	{
		/// <summary>The object the value refers to, which must be of <paramref name="expectedClass"/>.</summary>
		public ManagedObject Object(string expectedClass) => Named(() => entry.Object("value", "TValue", expectedClass));

		/// <summary>The value, an int.</summary>
		public FieldValue<int> Int32() => Named(() => entry.Int32("value", "TValue")) with { Field = where };

		/// <summary>The value, a double.</summary>
		public FieldValue<double> Double() => Named(() => entry.Double("value", "TValue")) with { Field = where };

		/// <summary>The Dictionary&lt;TKey, TValue&gt; the value refers to, or null when it refers to none. Its entries are named after this one, such as <c>CapitolBonusLevelByStatByElement[Magic][Health]</c>.</summary>
		public ManagedDictionary? DictionaryOrNull() =>
			Named(() => entry.ObjectOrNull("value", "TValue", "System.Collections.Generic.Dictionary`2")) is { } dictionary ? new(dictionary, where) : null;

		/// <summary>The List&lt;T&gt; the value refers to, named after this entry when it is malformed.</summary>
		public ManagedList List() => new(Named(() => entry.Object("value", "TValue", "System.Collections.Generic.List`1")), where);

		/// <summary>The value's text, or null when it holds none.</summary>
		public string? String() => Named(() => entry.String("value", "TValue").Value);

		private T Named<T>(Func<T> read)
		{
			try
			{
				return read();
			}
			catch (GameDataException exception)
			{
				throw new GameDataException($"{where}: {exception.Message}", exception);
			}
		}
	}
}
