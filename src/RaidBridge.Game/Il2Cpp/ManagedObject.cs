using System.Globalization;
using RaidBridge.Game.Metadata;

namespace RaidBridge.Game.Il2Cpp;

/// <summary>
/// A managed object in the Game client's memory, read as <see cref="ClassName"/>: the class the reader expects,
/// which its runtime class is or derives from. Its fields are read by name, never by a fixed offset.
/// </summary>
internal sealed record ManagedObject(Il2CppRuntime Runtime, ulong Address, RuntimeClass Class, string ClassName)
{
	private static readonly HashSet<byte> BooleanCode = [Il2CppTypeCode.Boolean];
	private static readonly HashSet<byte> Int32Code = [Il2CppTypeCode.Int32];
	private static readonly HashSet<byte> Int64Code = [Il2CppTypeCode.Int64];
	private static readonly HashSet<byte> DoubleCode = [Il2CppTypeCode.Double];
	private static readonly HashSet<byte> StringCode = [Il2CppTypeCode.String];
	private static readonly HashSet<byte> ValueTypeCode = [Il2CppTypeCode.ValueType];
	private static readonly HashSet<byte> GenericInstanceCode = [Il2CppTypeCode.GenericInstance];

	public FieldValue<long> Int64(string fieldName)
	{
		var (where, address, _) = Locate(fieldName, "System.Int64", Int64Code);
		return new(Runtime.Int64(address, () => where), where, address);
	}

	/// <summary>An int field. <paramref name="declaredType"/> is <c>System.Int32</c> unless the field is a generic class's, such as <c>TKey</c>.</summary>
	public FieldValue<int> Int32(string fieldName, string declaredType = "System.Int32")
	{
		var (where, address, _) = Locate(fieldName, declaredType, Int32Code);
		return new(Runtime.Int32(address, () => where), where, address);
	}

	/// <summary>A double field. <paramref name="declaredType"/> is <c>System.Double</c> unless the field is a generic class's, such as <c>TValue</c>.</summary>
	public FieldValue<double> Double(string fieldName, string declaredType = "System.Double")
	{
		var (where, address, _) = Locate(fieldName, declaredType, DoubleCode);
		return new(Runtime.Double(address, () => where), where, address);
	}

	/// <summary>A bool field, which holds 0 or 1 and nothing else.</summary>
	public FieldValue<bool> Boolean(string fieldName)
	{
		var (where, address, _) = Locate(fieldName, "System.Boolean", BooleanCode);
		var value = Runtime.Byte(address, () => where);
		return value <= 1
			? new(value == 1, where, address)
			: throw new GameDataException($"{where} at 0x{address:X} is {value}, which isn't a bool.");
	}

	/// <summary>A string field's text, or null when it holds none.</summary>
	public FieldValue<string?> String(string fieldName, string declaredType = "System.String")
	{
		var (where, address, _) = Locate(fieldName, declaredType, StringCode);
		return new(Runtime.String(Runtime.Pointer(address, () => where), where, address), where, address);
	}

	/// <summary>
	/// An enum field, as the name the game's own enum gives its value. <paramref name="declaredType"/> is the enum
	/// unless the field is a generic class's, such as Nullable&lt;T&gt;'s <c>T</c>. A value the enum doesn't name, or
	/// names twice, is refused rather than written as a number.
	/// </summary>
	public FieldValue<string> Enum(string fieldName, string enumType, string? declaredType = null)
	{
		var member = EnumMember(fieldName, enumType, declaredType);
		return new(member.Value.Name, member.Field, member.Address);
	}

	/// <summary>An enum field, as <see cref="Enum"/> reads it, with the value its name stands for, such as to put members in the game's order.</summary>
	public FieldValue<GameEnumMember> EnumMember(string fieldName, string enumType, string? declaredType = null)
	{
		var (where, address, _) = Locate(fieldName, declaredType ?? enumType, ValueTypeCode);
		var declared = Runtime.Metadata.Enum(enumType);
		if (declared.UnderlyingType != "System.Int32")
		{
			throw new GameDataException($"The game's enum {enumType} is a {declared.UnderlyingType}, where raid-bridge expects a System.Int32.");
		}

		var value = Runtime.Int32(address, () => where);
		var members = declared.Members.Where(member => member.Value == value).ToList();
		return members.Count switch
		{
			1 => new(members[0], where, address),
			0 => throw new GameDataException($"{where} at 0x{address:X} is {value}, which isn't a member of the game's {enumType}."),
			_ => throw new GameDataException($"{where} at 0x{address:X} is {value}, which the game's {enumType} names {string.Join(" and ", members.Select(member => member.Name))}."),
		};
	}

	/// <summary>A Nullable&lt;enum&gt; field, as the enum's name for its value, or null when it has none.</summary>
	public FieldValue<string?> NullableEnum(string fieldName, string enumType)
	{
		var (nullable, where, address) = Nullable(fieldName, enumType);
		return new(nullable?.Enum("value", enumType, declaredType: "T").Value, where, address);
	}

	/// <summary>A Nullable&lt;int&gt; field's value, or null when it has none.</summary>
	public FieldValue<int?> NullableInt32(string fieldName)
	{
		var (nullable, where, address) = Nullable(fieldName, "System.Int32");
		return new(nullable?.Int32("value", "T").Value, where, address);
	}

	/// <summary>
	/// A value type held in a field, such as a <c>Plarium.Common.Numerics.Fixed</c>, read as its runtime class, which
	/// the TypeInfo slot of <paramref name="valueType"/> gives.
	/// </summary>
	public ManagedObject Value(string fieldName, string valueType)
	{
		var (where, address, _) = Locate(fieldName, valueType, ValueTypeCode);
		return Runtime.Value(address, Runtime.ClassNamed(valueType), valueType, where);
	}

	/// <summary>This object read as <paramref name="className"/>, a class its runtime class is or derives from, such as the class behind an interface-typed field.</summary>
	public ManagedObject As(string className) =>
		Class.Is(className, () => $"The {Class.FullName} at 0x{Address:X}")
			? this with { ClassName = className }
			: throw new GameDataException($"The {Class.FullName} at 0x{Address:X} isn't a {className}.");

	/// <summary>The object a field refers to. It must be of <paramref name="objectClass"/>, the declared type unless the field is an interface or a base class.</summary>
	public ManagedObject Object(string fieldName, string declaredType, string? objectClass = null) =>
		Reference(fieldName, declaredType, objectClass, nullable: false).Object!;

	/// <summary>The object a field refers to, as <see cref="Object"/>, or null when it refers to none.</summary>
	public ManagedObject? ObjectOrNull(string fieldName, string declaredType, string? objectClass = null) =>
		Reference(fieldName, declaredType, objectClass, nullable: true).Object;

	/// <summary>The one-dimensional array a field refers to.</summary>
	public ManagedArray Array(string fieldName, string declaredType) => Array(fieldName, declaredType, nullable: false)!;

	/// <summary>The one-dimensional array a field refers to, or null when it refers to none.</summary>
	public ManagedArray? ArrayOrNull(string fieldName, string declaredType) => Array(fieldName, declaredType, nullable: true);

	/// <summary>The List&lt;T&gt; a field refers to.</summary>
	public ManagedList List(string fieldName, string declaredType) => List(fieldName, declaredType, nullable: false)!;

	/// <summary>The List&lt;T&gt; a field refers to, or null when it refers to none.</summary>
	public ManagedList? ListOrNull(string fieldName, string declaredType) => List(fieldName, declaredType, nullable: true);

	/// <summary>The Dictionary&lt;TKey, TValue&gt; a field refers to.</summary>
	public ManagedDictionary Dictionary(string fieldName, string declaredType)
	{
		var (dictionary, where) = Reference(fieldName, declaredType, "System.Collections.Generic.Dictionary`2", nullable: false);
		return new(dictionary!, where);
	}

	/// <summary>A Nullable&lt;T&gt; field of <paramref name="typeArgument"/>, read through its own runtime class, or null when it has no value.</summary>
	private (ManagedObject? Nullable, string Where, ulong Address) Nullable(string fieldName, string typeArgument)
	{
		var (where, address, field) = Locate(fieldName, $"System.Nullable`1<{typeArgument}>", GenericInstanceCode);
		var nullable = Runtime.Value(address, Runtime.FieldTypeClass(field, where), "System.Nullable`1", where);
		return (nullable.Boolean("hasValue").Value ? nullable : null, where, address);
	}

	private ManagedList? List(string fieldName, string declaredType, bool nullable)
	{
		var (list, where) = Reference(fieldName, declaredType, "System.Collections.Generic.List`1", nullable);
		return list is null ? null : new(list, where);
	}

	private ManagedArray? Array(string fieldName, string declaredType, bool nullable)
	{
		var (array, where) = Reference(fieldName, declaredType, "System.Array", nullable);
		return array is null ? null : new(array, where);
	}

	private (ManagedObject? Object, string Where) Reference(string fieldName, string declaredType, string? objectClass, bool nullable)
	{
		var (where, address, _) = Locate(fieldName, declaredType, Il2CppTypeCode.References);
		var pointer = Runtime.Pointer(address, () => where);
		return pointer == 0 && nullable
			? (null, where)
			: (Runtime.Object(pointer, where, address, objectClass ?? declaredType), where);
	}

	private (string Where, ulong Address, RuntimeField Field) Locate(string fieldName, string declaredType, IReadOnlySet<byte> typeCodes)
	{
		var declared = Runtime.Expect(ClassName, fieldName, declaredType, isStatic: false);
		var field = Class.Field(declared, typeCodes);
		return ($"{declared.DeclaringClass}.{fieldName}", Address + (ulong)field.Offset, field);
	}
}

/// <summary>A value read from a field, with the field and the address it was read at, so an error about it can name both.</summary>
internal sealed record FieldValue<T>(T Value, string Field, ulong Address)
{
	/// <summary>The value, checked: one that isn't <paramref name="expectation"/> is refused, naming the field and the address.</summary>
	public T Where(Func<T, bool> valid, string expectation) =>
		valid(Value) ? Value : throw new GameDataException($"{this}, where raid-bridge expects {expectation}.");

	/// <summary>In the invariant culture, so an error reads the same on every machine (code review, #34).</summary>
	public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Field} at 0x{Address:X} is {Value}");
}
