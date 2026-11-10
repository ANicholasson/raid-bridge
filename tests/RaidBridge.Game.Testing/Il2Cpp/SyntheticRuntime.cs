using static RaidBridge.Game.Testing.Il2Cpp.Unity6000Layout;

namespace RaidBridge.Game.Testing.Il2Cpp;

/// <summary>A field of a synthetic runtime class: its name, type code and offset, and for a generic instance its Il2CppGenericClass record.</summary>
internal sealed record SyntheticField(string Name, byte TypeCode, int Offset, bool IsStatic = false, ulong GenericClass = 0);

/// <summary>A synthetic Dictionary&lt;TKey, TValue&gt; instantiation: its runtime class, and the class of the array its entries live in.</summary>
internal sealed record SyntheticDictionaryType(ulong Class, ulong EntryArrayClass, byte KeyTypeCode);

/// <summary>A synthetic List&lt;T&gt; instantiation: its runtime class, and the class and element size of its items array.</summary>
internal sealed record SyntheticListType(ulong Class, ulong ItemsClass, int ItemSize);

/// <summary>
/// The IL2CPP runtime's structures, laid out as Unity 6000.3.18f1 lays them out, in a synthetic heap: runtime classes
/// and their FieldInfos, objects, strings, arrays, and the List&lt;T&gt; and Dictionary&lt;TKey, TValue&gt; of the
/// class library, which keep the fields the reader uses at offsets of this runtime's own choosing.
/// </summary>
internal sealed class SyntheticRuntime
{
	/// <summary>Dictionary&lt;TKey, TValue&gt;.Entry for a key and a value of up to 8 bytes: hashCode, next, key, value.</summary>
	public const int EntrySize = 0x18;

	private const int ListItems = 0x10;
	private const int ListSize = 0x18;
	private const int DictionaryEntries = 0x10;
	private const int DictionaryCount = 0x18;
	private const int DictionaryFreeCount = 0x1C;

	private readonly Dictionary<string, ulong> _fieldInfos = new(StringComparer.Ordinal);
	private readonly Dictionary<string, ulong> _runtimeClasses = new(StringComparer.Ordinal);
	private readonly Dictionary<ulong, string> _fullNames = [];

	public SyntheticRuntime(SyntheticHeap heap)
	{
		Heap = heap;
		SystemObject = Class("System", "Object", parent: 0);
		ValueType = Class("System", "ValueType", SystemObject);
		SystemString = Class("System", "String", SystemObject);
		SystemArray = Class("System", "Array", SystemObject);
		SystemInt32 = Class("System", "Int32", ValueType);
	}

	public SyntheticHeap Heap { get; }

	public ulong SystemObject { get; }

	public ulong ValueType { get; }

	public ulong SystemString { get; }

	public ulong SystemArray { get; }

	public ulong SystemInt32 { get; }

	/// <summary>The FieldInfo of a field, by its class's full name and its own, for the first class of that name.</summary>
	public ulong FieldInfo(string field) => _fieldInfos[field];

	public ulong RuntimeClass(string fullName) => _runtimeClasses[fullName];

	private string FullName(ulong runtimeClass) => _fullNames[runtimeClass];

	public ulong Class(string ns, string name, ulong parent, params SyntheticField[] fields) => Class(ns, name, parent, declaringType: 0, fields);

	public ulong Class(string ns, string name, ulong parent, ulong declaringType, params SyntheticField[] fields)
	{
		var address = Heap.Allocate(ClassSize);
		Heap.Pointer(address + ClassName, Heap.CString(name));
		Heap.Pointer(address + ClassNamespace, Heap.CString(ns));
		Heap.Pointer(address + ClassDeclaringType, declaringType);
		Heap.Pointer(address + ClassParent, parent);
		Heap.UInt16(address + ClassFieldCount, (ushort)fields.Length);
		// A nested class is named after its declaring class, as the reader and the installed game's metadata name it (#38).
		var fullName = declaringType != 0 ? $"{FullName(declaringType)}+{name}"
			: ns.Length > 0 ? $"{ns}.{name}"
			: name;
		_runtimeClasses.TryAdd(fullName, address);
		_fullNames[address] = fullName;
		if (fields.Length == 0)
		{
			return address;
		}

		var infos = Heap.Allocate(fields.Length * FieldInfoSize);
		Heap.Pointer(address + ClassFields, infos);
		for (var index = 0; index < fields.Length; index++)
		{
			var field = fields[index];
			var info = infos + (ulong)(index * FieldInfoSize);
			Heap.Pointer(info + FieldInfoName, Heap.CString(field.Name));
			Heap.Pointer(info + FieldInfoType, Type(field.TypeCode, field.IsStatic, field.GenericClass));
			Heap.Pointer(info + FieldInfoParent, address);
			Heap.Int32(info + FieldInfoOffset, field.Offset);
			_fieldInfos.TryAdd($"{fullName}.{field.Name}", info);
		}

		return address;
	}

	/// <summary>The class of a one-dimensional array of <paramref name="elementClass"/>, each element taking <paramref name="elementSize"/> bytes.</summary>
	public ulong ArrayClass(ulong elementClass, string elementName, int elementSize)
	{
		var address = Class(string.Empty, $"{elementName}[]", SystemArray);
		Heap.Pointer(address + ClassElementClass, elementClass);
		Heap.Int32(address + ClassElementSize, elementSize);
		return address;
	}

	public ulong Type(byte typeCode, bool isStatic = false, ulong data = 0)
	{
		var type = Heap.Allocate(TypeSize);
		Heap.Pointer(type + TypeData, data);
		Heap.UInt32(type + TypeBits, ((uint)typeCode << 16) | (isStatic ? StaticAttribute : 0u));
		return type;
	}

	/// <summary>A generic instance's Il2CppGenericClass record, holding the runtime class the runtime set up for it.</summary>
	public ulong GenericClass(ulong cachedClass)
	{
		var record = Heap.Allocate(GenericClassSize);
		Heap.Pointer(record + GenericClassCachedClass, cachedClass);
		return record;
	}

	public ulong Object(ulong runtimeClass, int size)
	{
		var address = Heap.Allocate(size);
		Heap.Pointer(address + ObjectClass, runtimeClass);
		return address;
	}

	/// <summary>A System.String holding the text, or 0 for null.</summary>
	public ulong String(string? text)
	{
		if (text is null)
		{
			return 0;
		}

		var address = Object(SystemString, StringChars + (text.Length * sizeof(char)) + sizeof(char));
		Heap.Int32(address + StringLength, text.Length);
		Heap.Bytes(address + StringChars, System.Text.Encoding.Unicode.GetBytes(text));
		return address;
	}

	public ulong Array(ulong arrayClass, int length, int elementSize)
	{
		var address = Object(arrayClass, ArrayData + (length * elementSize));
		Heap.Pointer(address + ArrayMaxLength, (ulong)length);
		return address;
	}

	public static ulong Element(ulong array, int index, int elementSize) => array + ArrayData + (ulong)(index * elementSize);

	/// <summary>The class of a two-dimensional array of <paramref name="elementClass"/>, a <c>T[,]</c>.</summary>
	public ulong MatrixClass(ulong elementClass, string elementName, int elementSize)
	{
		var address = Class("System", $"{elementName}[,]", SystemArray);
		Heap.Pointer(address + ClassElementClass, elementClass);
		Heap.Int32(address + ClassElementSize, elementSize);
		Heap.Bytes(address + ClassRank, [2]);
		return address;
	}

	/// <summary>A <c>double[,]</c> of the values, its two lengths in its bounds and its elements row by row.</summary>
	public ulong Matrix(ulong matrixClass, double[,] values)
	{
		var (rows, columns) = (values.GetLength(0), values.GetLength(1));
		var array = Array(matrixClass, rows * columns, sizeof(double));
		var bounds = Heap.Allocate(2 * ArrayBoundsSize);
		Heap.Pointer(bounds + ArrayBoundsLength, (ulong)rows);
		Heap.Pointer(bounds + ArrayBoundsSize + ArrayBoundsLength, (ulong)columns);
		Heap.Pointer(array + ArrayBounds, bounds);
		for (var row = 0; row < rows; row++)
		{
			for (var column = 0; column < columns; column++)
			{
				Heap.Int64(Element(array, (row * columns) + column, sizeof(double)), BitConverter.DoubleToInt64Bits(values[row, column]));
			}
		}

		return array;
	}

	/// <summary>An array of references to the objects.</summary>
	public ulong ReferenceArray(ulong arrayClass, IReadOnlyList<ulong> objects)
	{
		var array = Array(arrayClass, objects.Count, sizeof(ulong));
		for (var index = 0; index < objects.Count; index++)
		{
			Heap.Pointer(Element(array, index, sizeof(ulong)), objects[index]);
		}

		return array;
	}

	public SyntheticListType ListType(ulong itemClass, string itemName, int itemSize)
	{
		var list = Class(
			"System.Collections.Generic",
			"List`1",
			SystemObject,
			new SyntheticField("_items", SingleDimensionArrayTypeCode, ListItems),
			new SyntheticField("_size", Int32TypeCode, ListSize));
		return new(list, ArrayClass(itemClass, itemName, itemSize), itemSize);
	}

	/// <summary>A List&lt;T&gt; of the items, ints or references by its type, with one spare slot as a List keeps.</summary>
	public ulong List(SyntheticListType type, IReadOnlyList<ulong> items)
	{
		var array = Array(type.ItemsClass, items.Count + 1, type.ItemSize);
		for (var index = 0; index < items.Count; index++)
		{
			Write(Element(array, index, type.ItemSize), type.ItemSize, items[index]);
		}

		var list = Object(type.Class, 0x20);
		Heap.Pointer(list + ListItems, array);
		Heap.Int32(list + ListSize, items.Count);
		return list;
	}

	public SyntheticDictionaryType DictionaryType(byte keyTypeCode, byte valueTypeCode)
	{
		var dictionary = Class(
			"System.Collections.Generic",
			"Dictionary`2",
			SystemObject,
			new SyntheticField("_entries", SingleDimensionArrayTypeCode, DictionaryEntries),
			new SyntheticField("_count", Int32TypeCode, DictionaryCount),
			new SyntheticField("_freeCount", Int32TypeCode, DictionaryFreeCount));
		var entry = Class(
			string.Empty,
			"Entry",
			ValueType,
			declaringType: dictionary,
			new SyntheticField("hashCode", Int32TypeCode, ObjectHeaderSize),
			new SyntheticField("next", Int32TypeCode, ObjectHeaderSize + 0x4),
			new SyntheticField("key", keyTypeCode, ObjectHeaderSize + 0x8),
			new SyntheticField("value", valueTypeCode, ObjectHeaderSize + 0x10));
		return new(dictionary, ArrayClass(entry, "Entry", EntrySize), keyTypeCode);
	}

	/// <summary>
	/// A Dictionary&lt;TKey, TValue&gt; of the entries, keys and values as raw ints or pointers. With
	/// <paramref name="freedSlot"/>, the second slot is one a removal freed, as a Dictionary leaves it.
	/// </summary>
	public ulong Dictionary(SyntheticDictionaryType type, IReadOnlyList<(ulong Key, ulong Value)> entries, bool freedSlot = false)
	{
		var slots = entries.Count + (freedSlot ? 1 : 0);
		var freed = freedSlot ? Math.Min(1, entries.Count) : -1;
		var array = Array(type.EntryArrayClass, slots + 1, EntrySize);
		for (int slot = 0, index = 0; slot < slots; slot++)
		{
			var entry = Element(array, slot, EntrySize);
			if (slot == freed)
			{
				Heap.Int32(entry, -1);
				Heap.Int32(entry + 0x4, -1);
				continue;
			}

			var (key, value) = entries[index++];
			Heap.Int32(entry, (int)(key & 0x7FFF_FFFF));
			Heap.Int32(entry + 0x4, -1);
			Write(entry + 0x8, type.KeyTypeCode == Int32TypeCode ? sizeof(int) : sizeof(ulong), key);
			Heap.Pointer(entry + 0x10, value);
		}

		var dictionary = Object(type.Class, 0x20);
		Heap.Pointer(dictionary + DictionaryEntries, array);
		Heap.Int32(dictionary + DictionaryCount, slots);
		Heap.Int32(dictionary + DictionaryFreeCount, freedSlot ? 1 : 0);
		return dictionary;
	}

	/// <summary>Where the value of a Dictionary&lt;TKey, TValue&gt;'s slot is held.</summary>
	public ulong EntryValue(ulong dictionary, int slot) => Element(Heap.ReadPointer(dictionary + DictionaryEntries), slot, EntrySize) + 0x10;

	/// <summary>Where a List&lt;T&gt; of references holds the reference at <paramref name="index"/>.</summary>
	public ulong ListItem(ulong list, int index) => Element(Heap.ReadPointer(list + ListItems), index, sizeof(ulong));

	/// <summary>Sets a List&lt;T&gt;'s size, as a corrupt read might see it.</summary>
	public void SetListSize(ulong list, int size) => Heap.Int32(list + ListSize, size);

	/// <summary>Sets a Dictionary&lt;TKey, TValue&gt;'s count and free count, as a corrupt read might see them.</summary>
	public void SetDictionaryCounts(ulong dictionary, int count, int freeCount)
	{
		Heap.Int32(dictionary + DictionaryCount, count);
		Heap.Int32(dictionary + DictionaryFreeCount, freeCount);
	}

	private void Write(ulong address, int size, ulong value)
	{
		if (size == sizeof(int))
		{
			Heap.Int32(address, (int)value);
		}
		else
		{
			Heap.Pointer(address, value);
		}
	}
}
