namespace RaidBridge.Game.Testing.Il2Cpp;

/// <summary>
/// The IL2CPP runtime structures as Unity 6000.3.18f1 lays them out for x86-64, from its libil2cpp headers. Written
/// here apart from the reader's own table, so a change to that table fails the tests rather than passing with it.
/// </summary>
internal static class Unity6000Layout
{
	public const string UnityVersion = "6000.3.18f1";

	public const int ObjectClass = 0x0;
	public const int ObjectHeaderSize = 0x10;

	public const int ClassName = 0x10;
	public const int ClassNamespace = 0x18;
	public const int ClassElementClass = 0x40;
	public const int ClassDeclaringType = 0x50;
	public const int ClassParent = 0x58;
	public const int ClassGenericClass = 0x60;
	public const int ClassFields = 0x80;
	public const int ClassStaticFields = 0xB8;
	public const int ClassElementSize = 0x104;
	public const int ClassFieldCount = 0x124;
	public const int ClassSize = 0x140;

	public const int FieldInfoName = 0x0;
	public const int FieldInfoType = 0x8;
	public const int FieldInfoParent = 0x10;
	public const int FieldInfoOffset = 0x18;
	public const int FieldInfoSize = 0x20;

	public const int TypeData = 0x0;
	public const int TypeBits = 0x8;
	public const int TypeSize = 0x10;

	public const int GenericClassType = 0x0;
	public const int GenericClassClassInstance = 0x8;
	public const int GenericClassCachedClass = 0x18;
	public const int GenericClassSize = 0x20;

	public const int StringLength = 0x10;
	public const int StringChars = 0x14;

	public const int ArrayMaxLength = 0x18;
	public const int ArrayData = 0x20;

	public const byte BooleanTypeCode = 0x02;
	public const byte Int32TypeCode = 0x08;
	public const byte Int64TypeCode = 0x0A;
	public const byte DoubleTypeCode = 0x0D;
	public const byte StringTypeCode = 0x0E;
	public const byte ValueTypeTypeCode = 0x11;
	public const byte ClassTypeCode = 0x12;
	public const byte GenericInstanceTypeCode = 0x15;
	public const byte SingleDimensionArrayTypeCode = 0x1D;
	public const ushort StaticAttribute = 0x10;
}
