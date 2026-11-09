using System.Text;
using RaidBridge.Game.Il2Cpp;
using RaidBridge.Game.Memory;
using RaidBridge.Game.Metadata;
using RaidBridge.Game.Testing.Il2Cpp;
using RaidBridge.Game.Testing.Memory;
using static RaidBridge.Game.Testing.Il2Cpp.Unity6000Layout;

namespace RaidBridge.Game.Tests.Il2Cpp;

/// <summary>
/// The IL2CPP kernel's own refusals, each in a runtime of one class: what a corrupt read, a torn object or a layout
/// table that doesn't fit the Game client leaves in memory is refused by name, never read as a value. The feature
/// readers' tests reach the kernel only along the paths the Account takes.
/// </summary>
public sealed class Il2CppObjectReadingTests
{
	private const string Thing = "Game.Thing";
	private const string Holder = "Game.Holder.Thing";
	private const string Kind = "Game.Kind";
	private const string Table = "System.Collections.Generic.Dictionary`2<System.String, System.Int32>";
	private const ulong Source = 0x1234;

	private const int CountOffset = 0x10;
	private const int FlagOffset = 0x14;
	private const int NameOffset = 0x18;
	private const int ItemsOffset = 0x20;
	private const int KindOffset = 0x28;
	private const int TableOffset = 0x30;

	private readonly FakeGameMemory _memory = new(new GameModule(0x1_8000_0000, "GameAssembly.dll"), DateTimeOffset.UnixEpoch);
	private readonly SyntheticMetadata _metadata = new(new GameVersions(39, UnityVersion));
	private readonly SyntheticHeap _heap;
	private readonly SyntheticRuntime _runtime;
	private readonly ulong _class;
	private readonly ulong _object;

	public Il2CppObjectReadingTests()
	{
		_heap = new SyntheticHeap(_memory, 0x2_0000_0000, 0x10000);
		_runtime = new SyntheticRuntime(_heap);
		_metadata.Class(
			Thing,
			"System.Object",
			("Count", "System.Int32", false),
			("Flag", "System.Boolean", false),
			("Name", "System.String", false),
			("Items", "System.Int32[]", false),
			("Kind", Kind, false),
			("Table", Table, false));
		_metadata.Class(
			"System.Collections.Generic.Dictionary`2",
			"System.Object",
			("_entries", "System.Collections.Generic.Dictionary`2+Entry<TKey, TValue>[]", false),
			("_count", "System.Int32", false),
			("_freeCount", "System.Int32", false));
		_metadata.Class(
			"System.Collections.Generic.Dictionary`2+Entry",
			"System.ValueType",
			("hashCode", "System.Int32", false),
			("next", "System.Int32", false),
			("key", "TKey", false),
			("value", "TValue", false));
		_class = _runtime.Class(
			"Game",
			"Thing",
			_runtime.SystemObject,
			new SyntheticField("Count", Int32TypeCode, CountOffset),
			new SyntheticField("Flag", BooleanTypeCode, FlagOffset),
			new SyntheticField("Name", StringTypeCode, NameOffset),
			new SyntheticField("Items", SingleDimensionArrayTypeCode, ItemsOffset),
			new SyntheticField("Kind", ValueTypeTypeCode, KindOffset),
			new SyntheticField("Table", GenericInstanceTypeCode, TableOffset));
		_object = _runtime.Object(_class, 0x38);
		_heap.Int32(_object + CountOffset, 7);
	}

	[Fact]
	public void Reads_a_field_by_name()
	{
		Assert.Equal(7, ReadThing().Int32("Count").Value);
	}

	[Fact]
	public void Refuses_an_object_whose_class_pointer_is_null()
	{
		_heap.Pointer(_object + ObjectClass, 0);

		Refuses($"{Holder} at 0x{Source:X} points to 0x{_object:X}, which isn't an object. Its class pointer is null.");
	}

	[Fact]
	public void Refuses_a_class_whose_base_classes_loop()
	{
		_heap.Pointer(_class + ClassParent, _class);

		Refuses(
			$"{Holder} at 0x{Source:X} points to a {Thing} at 0x{_object:X}, whose base classes can't be read. The runtime class {Thing} at 0x{_class:X} has more than 64 base classes.");
	}

	[Fact]
	public void Refuses_a_class_name_that_never_ends()
	{
		var name = _heap.Allocate(2048);
		_heap.Bytes(name, Encoding.UTF8.GetBytes(new string('a', 2048)));
		_heap.Pointer(_class + ClassName, name);

		var error = Assert.Throws<GameDataException>(ReadThing);

		Assert.StartsWith($"{Holder} at 0x{Source:X} points to 0x{_object:X}, which isn't an object.", error.Message, StringComparison.Ordinal);
		Assert.EndsWith($"at 0x{name:X} isn't a name: it runs past 1024 bytes.", error.Message, StringComparison.Ordinal);
	}

	/// <summary>A name is read a page at a time, so one that ends just before unmapped memory is read whole.</summary>
	[Fact]
	public void Reads_a_class_name_that_ends_where_the_mapping_does()
	{
		var page = new byte[4096];
		var name = "Thing\0"u8;
		name.CopyTo(page.AsSpan(page.Length - name.Length));
		_memory.Map(0x3_0000_0000, page);
		_heap.Pointer(_class + ClassName, 0x3_0000_0000 + (ulong)(page.Length - name.Length));

		Assert.Equal(Thing, ReadThing().Class.FullName);
	}

	[Fact]
	public void Refuses_a_class_with_fields_but_no_field_array()
	{
		_heap.Pointer(_class + ClassFields, 0);

		Refuses(thing => thing.Int32("Count"), $"The runtime class {Thing} at 0x{_class:X} has 6 fields but no field array.");
	}

	/// <summary>The check that catches a layout table that doesn't fit the Game client (ADR-0003).</summary>
	[Fact]
	public void Refuses_a_field_info_that_names_another_class()
	{
		var info = _runtime.FieldInfo($"{Thing}.Count");
		_heap.Pointer(info + FieldInfoParent, 0xBAD0);

		Refuses(
			thing => thing.Int32("Count"),
			$"The field {Thing}.Count at 0x{info:X} names 0xBAD0 as its class, not 0x{_class:X}: the IL2CPP layout for Unity {UnityVersion} doesn't fit this Game client.");
	}

	[Fact]
	public void Refuses_a_thread_static_field()
	{
		var info = _runtime.FieldInfo($"{Thing}.Count");
		_heap.Int32(info + FieldInfoOffset, -1);

		Refuses(thing => thing.Int32("Count"), $"The runtime field {Thing}.Count at 0x{info:X} has offset -1: it is thread-static.");
	}

	[Theory]
	[InlineData(2)]
	[InlineData(255)]
	public void Refuses_a_bool_that_is_neither_0_nor_1(byte value)
	{
		_heap.Bytes(_object + FlagOffset, [value]);

		Refuses(thing => thing.Boolean("Flag"), $"{Thing}.Flag at 0x{_object + FlagOffset:X} is {value}, which isn't a bool.");
	}

	[Fact]
	public void Reads_a_string()
	{
		_heap.Pointer(_object + NameOffset, _runtime.String("Synthetic"));

		Assert.Equal("Synthetic", ReadThing().String("Name").Value);
	}

	[Theory]
	[InlineData(-1)]
	[InlineData((1 << 20) + 1)]
	public void Refuses_a_string_of_a_length_no_string_has(int length)
	{
		var text = _runtime.String("Synthetic");
		_heap.Int32(text + StringLength, length);
		_heap.Pointer(_object + NameOffset, text);

		Refuses(
			thing => thing.String("Name"),
			$"{Thing}.Name at 0x{_object + NameOffset:X} points to a System.String at 0x{text:X} of length {length}, which isn't a length.");
	}

	[Fact]
	public void Reads_an_array_of_ints()
	{
		var array = IntArray(elementSize: sizeof(int));
		_heap.Int32(SyntheticRuntime.Element(array, 1, sizeof(int)), 42);

		Assert.Equal(42, ReadThing().Array("Items", "System.Int32[]").Int32(1).Value);
	}

	[Fact]
	public void Refuses_an_array_of_a_length_no_array_has()
	{
		var array = IntArray(elementSize: sizeof(int));
		_heap.Pointer(array + ArrayMaxLength, (1UL << 24) + 1);

		Refuses(
			thing => thing.Array("Items", "System.Int32[]"),
			$"{Thing}.Items points to an array at 0x{array:X} of length 16777217, which isn't a length.");
	}

	[Fact]
	public void Refuses_an_array_class_with_no_element_class()
	{
		var array = IntArray(elementSize: sizeof(int));
		var arrayClass = _heap.ReadPointer(array + ObjectClass);
		_heap.Pointer(arrayClass + ClassElementClass, 0);

		Refuses(
			thing => thing.Array("Items", "System.Int32[]"),
			$"The runtime class System.Int32[] at 0x{arrayClass:X} has element class 0x0 and element size 4: it isn't an array class.");
	}

	[Fact]
	public void Refuses_array_elements_of_another_size()
	{
		var array = IntArray(elementSize: sizeof(long));

		Refuses(
			thing => thing.Array("Items", "System.Int32[]").Int32(0),
			$"{Thing}.Items points to an array at 0x{array:X} of System.Int32, 8 bytes each, where raid-bridge expects System.Int32, 4 bytes each.");
	}

	[Fact]
	public void Reads_an_enum_as_the_name_of_its_value()
	{
		_metadata.Enum(Kind, ("Plain", 1), ("Fancy", 2));
		_heap.Int32(_object + KindOffset, 2);

		Assert.Equal("Fancy", ReadThing().Enum("Kind", Kind).Value);
	}

	[Fact]
	public void Refuses_an_enum_over_another_type_than_int()
	{
		_metadata.Enum(Kind, "System.Byte", ("Plain", 1));

		Refuses(thing => thing.Enum("Kind", Kind), $"The game's enum {Kind} is a System.Byte, where raid-bridge expects a System.Int32.");
	}

	/// <summary>Two names for one value are common in C#, so a game update could add one; neither is picked.</summary>
	[Fact]
	public void Refuses_an_enum_value_two_members_name()
	{
		_metadata.Enum(Kind, ("Plain", 1), ("Basic", 1));
		_heap.Int32(_object + KindOffset, 1);

		Refuses(thing => thing.Enum("Kind", Kind), $"{Thing}.Kind at 0x{_object + KindOffset:X} is 1, which the game's {Kind} names Plain and Basic.");
	}

	[Fact]
	public void Reads_a_dictionary_by_its_string_keys()
	{
		StringTable("first", "second");

		Assert.Equal(["first", "second"], ReadThing().Dictionary("Table", Table).ByStringKey().Keys.Order(StringComparer.Ordinal));
	}

	[Fact]
	public void Refuses_a_dictionary_entry_with_a_null_key()
	{
		var table = StringTable("first", null);
		var entry = SyntheticRuntime.Element(_heap.ReadPointer(table + 0x10), 1, SyntheticRuntime.EntrySize) - ObjectHeaderSize;

		Refuses(
			thing => thing.Dictionary("Table", Table).ByStringKey(),
			$"{Thing}.Table at 0x{table:X} is malformed: an entry at 0x{entry:X} has a null key.");
	}

	[Fact]
	public void Refuses_a_dictionary_that_holds_a_key_twice()
	{
		var table = StringTable("first", "first");

		Refuses(thing => thing.Dictionary("Table", Table).ByStringKey(), $"{Thing}.Table at 0x{table:X} is malformed: it holds the key first twice.");
	}

	/// <summary>The object, as a field of another object might reach it.</summary>
	private ManagedObject ReadThing() => new Il2CppRuntime(_memory, _metadata).Object(_object, Holder, Source, Thing);

	/// <summary>An array of two ints in the object's Items field, each element taking <paramref name="elementSize"/> bytes.</summary>
	private ulong IntArray(int elementSize)
	{
		var array = _runtime.Array(_runtime.ArrayClass(_runtime.SystemInt32, "System.Int32", elementSize), 2, elementSize);
		_heap.Pointer(_object + ItemsOffset, array);
		return array;
	}

	/// <summary>A Dictionary&lt;string, int&gt; in the object's Table field, one entry per key, a null key held as a null string.</summary>
	private ulong StringTable(params string?[] keys)
	{
		var table = _runtime.Dictionary(
			_runtime.DictionaryType(StringTypeCode, Int32TypeCode),
			[.. keys.Select((key, index) => (_runtime.String(key), (ulong)index))]);
		_heap.Pointer(_object + TableOffset, table);
		return table;
	}

	private void Refuses(string message) => Assert.Equal(message, Assert.Throws<GameDataException>(ReadThing).Message);

	private void Refuses(Func<ManagedObject, object> read, string message)
	{
		var thing = ReadThing();
		Assert.Equal(message, Assert.Throws<GameDataException>(() => read(thing)).Message);
	}
}
