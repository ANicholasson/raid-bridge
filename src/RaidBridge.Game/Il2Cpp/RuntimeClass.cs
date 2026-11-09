namespace RaidBridge.Game.Il2Cpp;

/// <summary>
/// An Il2CppClass in the Game client's memory. A generic instance has its definition's name, such as
/// <c>SingleInstance`1</c>, and a nested class its declaring class's, such as <c>Dictionary`2+Entry</c>, as the
/// installed game's metadata names them. A runtime class doesn't change while the Game client runs, so one read of
/// the Game client reads each class's base classes and fields once (#25).
/// </summary>
internal sealed class RuntimeClass(Il2CppRuntime runtime, ulong address, string name, string @namespace, string? declaringType)
{
	/// <summary>Far deeper than any real class hierarchy, so a corrupt parent chain ends with an error, not a loop.</summary>
	private const int MaxDepth = 64;

	private readonly Dictionary<string, RuntimeField?> _fields = new(StringComparer.Ordinal);
	private List<RuntimeClass>? _lineage;

	public Il2CppRuntime Runtime { get; } = runtime;

	public ulong Address { get; } = address;

	public string Name { get; } = name;

	public string FullName { get; } =
		declaringType is not null ? $"{declaringType}+{name}"
		: @namespace.Length > 0 ? $"{@namespace}.{name}"
		: name;

	/// <summary>This class, then each base class up to System.Object.</summary>
	public IReadOnlyList<RuntimeClass> Lineage()
	{
		if (_lineage is not null)
		{
			return _lineage;
		}

		var layout = Runtime.Layout.Class;
		var lineage = new List<RuntimeClass>();
		for (var current = this; lineage.Count < MaxDepth;)
		{
			lineage.Add(current);
			var child = current;
			var parent = Runtime.Pointer(child.Address + (ulong)layout.Parent, () => $"the base class of {child.FullName} at 0x{child.Address:X}");
			if (parent == 0)
			{
				return _lineage = lineage;
			}

			current = Runtime.Class(parent);
		}

		throw new GameDataException($"The runtime class {FullName} at 0x{Address:X} has more than {MaxDepth} base classes.");
	}

	/// <summary>
	/// Whether this class is <paramref name="fullName"/> or derives from it. When its base classes can't be read, the
	/// error names what holds the object first, such as the field it was reached through (code review, #38).
	/// </summary>
	public bool Is(string fullName, Func<string> holder)
	{
		try
		{
			return Lineage().Any(ancestor => ancestor.FullName == fullName);
		}
		catch (GameDataException exception)
		{
			throw new GameDataException($"{holder()}, whose base classes can't be read. {exception.Message}", exception);
		}
	}

	/// <summary>An array class's element class, and the size each element takes in the array.</summary>
	public (RuntimeClass Class, int Size) Element()
	{
		var layout = Runtime.Layout.Class;
		var element = Runtime.Pointer(Address + (ulong)layout.ElementClass, () => $"the element class of {FullName} at 0x{Address:X}");
		var size = Runtime.Int32(Address + (ulong)layout.ElementSize, () => $"the element size of {FullName} at 0x{Address:X}");
		if (element == 0 || size <= 0)
		{
			throw new GameDataException(
				$"The runtime class {FullName} at 0x{Address:X} has element class 0x{element:X} and element size {size}: it isn't an array class.");
		}

		return (Runtime.Class(element), size);
	}

	/// <summary>
	/// The runtime field the installed game's metadata declares, found by name in the class that declares it, with its
	/// type code checked against <paramref name="typeCodes"/> and its static flag against the declaration.
	/// </summary>
	public RuntimeField Field(Metadata.GameField declared, IReadOnlySet<byte> typeCodes)
	{
		var declaring = Lineage().FirstOrDefault(ancestor => ancestor.FullName == declared.DeclaringClass)
			?? throw new GameDataException(
				$"The runtime class {FullName} at 0x{Address:X} doesn't derive from {declared.DeclaringClass}, which declares {declared.Name}.");

		var field = declaring.FieldNamed(declared.Name)
			?? throw new GameDataException(
				$"The runtime class {declaring.FullName} at 0x{declaring.Address:X} has no field {declared.Name}.");

		var where = $"{declaring.FullName}.{declared.Name}";
		if (field.IsStatic != declared.IsStatic || !typeCodes.Contains(field.TypeCode))
		{
			throw new GameDataException(
				$"The runtime field {where} at 0x{field.Address:X} has type code 0x{field.TypeCode:X2}{(field.IsStatic ? " and is static" : string.Empty)}, which doesn't fit the game's declared {(declared.IsStatic ? "static " : string.Empty)}{declared.Type}.");
		}

		if (field.Offset < 0)
		{
			throw new GameDataException($"The runtime field {where} at 0x{field.Address:X} has offset {field.Offset}: it is thread-static.");
		}

		return field;
	}

	private RuntimeField? FieldNamed(string name)
	{
		if (!_fields.TryGetValue(name, out var field))
		{
			field = FieldInfo(name) is { } info ? RuntimeField.At(this, info, name) : null;
			_fields[name] = field;
		}

		return field;
	}

	/// <summary>
	/// The address of the FieldInfo with this name, or null. Only the names of the fields before it are read, so the
	/// rest of a field the reader doesn't use can't stop it (code review, #23).
	/// </summary>
	private ulong? FieldInfo(string name)
	{
		var layout = Runtime.Layout;
		var count = Runtime.UInt16(Address + (ulong)layout.Class.FieldCount, () => $"the field count of {FullName} at 0x{Address:X}");
		if (count == 0)
		{
			return null;
		}

		var fields = Runtime.Pointer(Address + (ulong)layout.Class.Fields, () => $"the fields of {FullName} at 0x{Address:X}");
		if (fields == 0)
		{
			throw new GameDataException($"The runtime class {FullName} at 0x{Address:X} has {count} fields but no field array.");
		}

		for (var index = 0; index < count; index++)
		{
			var info = fields + (ulong)(index * layout.FieldInfo.Size);
			string What() => $"the name of field info 0x{info:X} of {FullName}";
			if (Runtime.CString(Runtime.Pointer(info + (ulong)layout.FieldInfo.Name, What), What) == name)
			{
				return info;
			}
		}

		return null;
	}
}
