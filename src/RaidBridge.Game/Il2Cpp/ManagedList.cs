namespace RaidBridge.Game.Il2Cpp;

/// <summary>A List&lt;T&gt; in the Game client's memory: the first <c>_size</c> elements of its <c>_items</c> array.</summary>
internal sealed class ManagedList
{
	private readonly ManagedArray _items;

	public ManagedList(ManagedObject list, string where)
	{
		_items = list.Array("_items", "T[]");
		var size = list.Int32("_size").Value;
		Count = size >= 0 && size <= _items.Length
			? size
			: throw new GameDataException($"{where} at 0x{list.Address:X} is malformed: its _size is {size}, but its array holds {_items.Length}.");
	}

	public int Count { get; }

	/// <summary>Each element, a reference to an object of <paramref name="expectedClass"/>.</summary>
	public IReadOnlyList<ManagedObject> Objects(string expectedClass) =>
		[.. Enumerable.Range(0, Count).Select(index => _items.Object(index, expectedClass))];

	/// <summary>Each element, an int.</summary>
	public IReadOnlyList<FieldValue<int>> Int32s() => [.. Enumerable.Range(0, Count).Select(_items.Int32)];
}
