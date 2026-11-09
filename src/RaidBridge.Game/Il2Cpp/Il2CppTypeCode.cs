namespace RaidBridge.Game.Il2Cpp;

/// <summary>The type codes and field attribute an Il2CppType holds. They are ECMA-335's, so no Unity version moves them.</summary>
internal static class Il2CppTypeCode
{
	public const byte Boolean = 0x02;
	public const byte Int32 = 0x08;
	public const byte Int64 = 0x0A;
	public const byte Double = 0x0D;
	public const byte String = 0x0E;
	public const byte ValueType = 0x11;
	public const byte Class = 0x12;
	public const byte Array = 0x14;
	public const byte GenericInstance = 0x15;
	public const byte Object = 0x1C;
	public const byte SingleDimensionArray = 0x1D;

	/// <summary>FieldAttributes.Static, in an Il2CppType's attributes.</summary>
	public const int StaticAttribute = 0x10;

	/// <summary>The codes of a field that holds a reference to an object.</summary>
	public static readonly IReadOnlySet<byte> References = new HashSet<byte> { String, Class, Array, GenericInstance, Object, SingleDimensionArray };
}
