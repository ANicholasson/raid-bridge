using RaidBridge.Game.Metadata;

namespace RaidBridge.Game.Il2Cpp;

/// <summary>
/// A one-dimensional array in the Game client's memory: its length, and its elements laid out as its runtime class's
/// element class and size say. <see cref="Where"/> names the field it was read from, for errors.
/// </summary>
internal sealed class ManagedArray
{
	/// <summary>Far longer than any array the reader looks at, so a corrupt length ends with an error, not a huge read.</summary>
	private const ulong MaxLength = 1 << 24;

	private readonly ManagedObject _array;
	private readonly RuntimeClass _elementClass;
	private readonly int _elementSize;

	public ManagedArray(ManagedObject array, string where)
	{
		_array = array;
		Where = where;
		(_elementClass, _elementSize) = array.Class.Element();
		var length = array.Runtime.Pointer(array.Address + (ulong)array.Runtime.Layout.Array.MaxLength, () => $"the length of {where}");
		Length = length <= MaxLength
			? (int)length
			: throw new GameDataException($"{where} points to an array at 0x{array.Address:X} of length {length}, which isn't a length.");
	}

	public string Where { get; }

	public ulong Address => _array.Address;

	public int Length { get; }

	/// <summary>The object the reference at <paramref name="index"/> refers to, which must be of <paramref name="expectedClass"/>.</summary>
	public ManagedObject Object(int index, string expectedClass)
	{
		var (where, address) = Element(index, sizeof(ulong), elementClass: null);
		return _array.Runtime.Object(_array.Runtime.Pointer(address, () => where), where, address, expectedClass);
	}

	/// <summary>The int at <paramref name="index"/>.</summary>
	public FieldValue<int> Int32(int index)
	{
		var (where, address) = Element(index, sizeof(int), "System.Int32");
		return new(_array.Runtime.Int32(address, () => where), where, address);
	}

	/// <summary>The enum value at <paramref name="index"/>, in an array of the game's <paramref name="enumType"/>, as the member it stands for.</summary>
	public FieldValue<GameEnumMember> EnumMember(int index, string enumType)
	{
		var (where, address) = Element(index, sizeof(int), enumType);
		return ManagedObject.EnumAt(_array.Runtime, address, enumType, where);
	}

	/// <summary>The value type held unboxed at <paramref name="index"/>, which must be of <paramref name="className"/>.</summary>
	public ManagedObject Value(int index, string className)
	{
		var (where, address) = Element(index, size: null, className);
		return _array.Runtime.Value(address, _elementClass, className, where);
	}

	/// <summary>An element's address, once the array is checked to hold elements of this size, unless null, and this class, unless null.</summary>
	private (string Where, ulong Address) Element(int index, int? size, string? elementClass)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(index);
		ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Length);
		if ((size is not null && _elementSize != size) || (elementClass is not null && !_elementClass.Is(elementClass, () => $"{Where} points to an array at 0x{_array.Address:X} of {_elementClass.FullName}")))
		{
			throw new GameDataException(
				$"{Where} points to an array at 0x{_array.Address:X} of {_elementClass.FullName}, {_elementSize} bytes each, where raid-bridge expects {elementClass ?? "references"}{(size is null ? string.Empty : $", {size} bytes each")}.");
		}

		// In ulong, so a corrupt element size can't overflow the offset into another address (code review, #25).
		return ($"{Where}[{index}]", _array.Address + (ulong)_array.Runtime.Layout.Array.Data + ((ulong)index * (ulong)_elementSize));
	}
}
