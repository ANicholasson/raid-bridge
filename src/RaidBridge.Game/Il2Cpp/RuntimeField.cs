namespace RaidBridge.Game.Il2Cpp;

/// <summary>
/// A FieldInfo in the Game client's memory: the field's name, its Il2CppType's address, type code and static flag,
/// and its offset in its object or static block.
/// </summary>
internal sealed record RuntimeField(ulong Address, string Name, ulong Type, byte TypeCode, bool IsStatic, int Offset)
{
	/// <summary>The FieldInfo at the address in the owner's field array, whose name has already been read.</summary>
	public static RuntimeField At(RuntimeClass owner, ulong address, string name)
	{
		var runtime = owner.Runtime;
		var layout = runtime.Layout;

		var parent = runtime.Pointer(address + (ulong)layout.FieldInfo.Parent, () => $"the declaring class of {owner.FullName}.{name}");
		if (parent != owner.Address)
		{
			throw new GameDataException(
				$"The field {owner.FullName}.{name} at 0x{address:X} names 0x{parent:X} as its class, not 0x{owner.Address:X}: the IL2CPP layout for Unity {layout.UnityVersion} doesn't fit this Game client.");
		}

		var type = runtime.Pointer(address + (ulong)layout.FieldInfo.Type, () => $"the type of {owner.FullName}.{name}");
		var bits = runtime.UInt32(type + (ulong)layout.Type.Bits, () => $"the type of {owner.FullName}.{name}");
		var offset = runtime.Int32(address + (ulong)layout.FieldInfo.Offset, () => $"the offset of {owner.FullName}.{name}");
		return new(address, name, type, TypeCode: (byte)(bits >> 16), IsStatic: (bits & Il2CppTypeCode.StaticAttribute) != 0, offset);
	}
}
