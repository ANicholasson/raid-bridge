namespace RaidBridge.Game.Il2Cpp;

/// <summary>
/// Where one Unity version's IL2CPP runtime keeps the fields the reader uses, inside its own structures, for the
/// 64-bit Windows build the Game client is. No game file holds these, so they are the one offset table raid-bridge
/// keeps: one per exact Unity version, and any other version is refused by name (ADR-0003).
/// </summary>
internal sealed record Il2CppLayout(
	string UnityVersion,
	ObjectLayout Object,
	ClassLayout Class,
	FieldInfoLayout FieldInfo,
	TypeLayout Type,
	GenericClassLayout GenericClass,
	StringLayout String,
	ArrayLayout Array)
{
	private static readonly Dictionary<string, Il2CppLayout> Known = new(StringComparer.Ordinal)
	{
		// Source: libil2cpp's il2cpp-class-internals.h, il2cpp-object-internals.h and il2cpp-runtime-metadata.h for
		// Unity 6000.3.18f1, as nneonneo/Il2CppVersions collects them (headers/6000.3.18f1.h at 8a51ff0), with each
		// offset computed by offsetof for x86-64 (ADR-0003).
		["6000.3.18f1"] = new(
			UnityVersion: "6000.3.18f1",
			Object: new(Class: 0x0, HeaderSize: 0x10),
			Class: new(
				Name: 0x10, Namespace: 0x18, ElementClass: 0x40, DeclaringType: 0x50, Parent: 0x58, GenericClass: 0x60, Fields: 0x80,
				StaticFields: 0xB8, ElementSize: 0x104, FieldCount: 0x124),
			FieldInfo: new(Size: 0x20, Name: 0x0, Type: 0x8, Parent: 0x10, Offset: 0x18),
			Type: new(Data: 0x0, Bits: 0x8),
			GenericClass: new(CachedClass: 0x18),
			String: new(Length: 0x10, Chars: 0x14),
			Array: new(MaxLength: 0x18, Data: 0x20)),
	};

	public static IReadOnlyCollection<string> UnityVersions => Known.Keys;

	public static Il2CppLayout For(string unityVersion) =>
		Known.TryGetValue(unityVersion, out var layout)
			? layout
			: throw new GameDataException(
				$"raid-bridge has no IL2CPP runtime layout for Unity {unityVersion}; it knows only Unity {string.Join(", ", Known.Keys)}.");
}

/// <summary>
/// Il2CppObject, the header of every managed object: its runtime class pointer, and its size. A value type's field
/// offsets count from the start of its boxed form, so a value held unboxed starts <see cref="HeaderSize"/> later.
/// </summary>
internal sealed record ObjectLayout(int Class, int HeaderSize);

/// <summary>
/// Il2CppClass, a runtime class: its names, an array's element class, a nested class's declaring class, its base
/// class, generic class record, field array, static field block, an array's element size and its field count.
/// </summary>
internal sealed record ClassLayout(
	int Name,
	int Namespace,
	int ElementClass,
	int DeclaringType,
	int Parent,
	int GenericClass,
	int Fields,
	int StaticFields,
	int ElementSize,
	int FieldCount);

/// <summary>FieldInfo, one entry of a runtime class's field array: its name, type, declaring class and offset.</summary>
internal sealed record FieldInfoLayout(int Size, int Name, int Type, int Parent, int Offset);

/// <summary>
/// Il2CppType: its data, which for a generic instance is its Il2CppGenericClass record, and the 32 bits holding the
/// attributes (bits 0 to 15) and the type code (bits 16 to 23).
/// </summary>
internal sealed record TypeLayout(int Data, int Bits);

/// <summary>Il2CppGenericClass, a generic instance's record in the binary: the slot the runtime writes its class into.</summary>
internal sealed record GenericClassLayout(int CachedClass);

/// <summary>Il2CppString, a System.String: its length in UTF-16 code units, then the code units themselves.</summary>
internal sealed record StringLayout(int Length, int Chars);

/// <summary>Il2CppArray, a one-dimensional array: its length, then its elements.</summary>
internal sealed record ArrayLayout(int MaxLength, int Data);
