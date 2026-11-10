using System.Buffers.Binary;
using System.Text;
using RaidBridge.Game.Memory;
using RaidBridge.Game.Metadata;

namespace RaidBridge.Game.Il2Cpp;

/// <summary>
/// The running Game client's IL2CPP runtime, read through the memory port. Its own structures are laid out by the
/// installed Unity version's table (ADR-0003). Each field of the game's classes is found by name in its runtime
/// class, which gives the offset, and checked against the type the installed game's metadata declares (ADR-0001).
/// </summary>
internal sealed class Il2CppRuntime
{
	private const int MaxNameLength = 1024;
	private const int PageSize = 4096;

	/// <summary>Far longer than any text the reader looks at, so a corrupt length ends with an error, not a huge read.</summary>
	private const int MaxStringLength = 1 << 20;

	/// <summary>Far deeper than any real nesting of classes, so a corrupt chain of declaring classes ends with an error.</summary>
	private const int MaxNesting = 16;

	private static readonly HashSet<byte> ValueTypeCodes = [Il2CppTypeCode.ValueType];

	private readonly IGameMemory _memory;
	private readonly Dictionary<ulong, RuntimeClass> _classes = [];
	private readonly Dictionary<string, RuntimeClass> _namedClasses = new(StringComparer.Ordinal);

	public Il2CppRuntime(IGameMemory memory, IGameMetadata metadata)
	{
		_memory = memory;
		Metadata = metadata;
		Layout = Il2CppLayout.For(metadata.Versions.Unity);
	}

	public IGameMetadata Metadata { get; }

	public Il2CppLayout Layout { get; }

	/// <summary>
	/// The object a static field of a generic instance class refers to, such as
	/// <c>SingleInstance&lt;AppModel&gt;._instance</c>, which must be of <paramref name="objectClass"/>.
	/// </summary>
	public ManagedObject StaticObject(string genericInstance, string fieldName, string declaredType, string objectClass)
	{
		var (address, where) = StaticField(GenericInstanceClass(genericInstance), genericInstance, fieldName, declaredType, Il2CppTypeCode.References);
		return Object(Pointer(address, () => where), where, address, objectClass);
	}

	/// <summary>
	/// The object a static field of a class that isn't generic refers to, such as
	/// <c>ArtifactStorageResolver._implementation</c>, which must be of <paramref name="objectClass"/>, or null when it
	/// refers to none.
	/// </summary>
	public ManagedObject? StaticObjectOrNull(string className, string fieldName, string declaredType, string objectClass)
	{
		var (address, where) = StaticField(ClassNamed(className), className, fieldName, declaredType, Il2CppTypeCode.References);
		var pointer = Pointer(address, () => where);
		return pointer == 0 ? null : Object(pointer, where, address, objectClass);
	}

	/// <summary>
	/// The value type a static field of a class that isn't generic holds, such as <c>BattleStats.ToPercent</c>, a
	/// <c>Plarium.Common.Numerics.Fixed</c>, read as its runtime class, which the TypeInfo slot of
	/// <paramref name="valueType"/> gives.
	/// </summary>
	public ManagedObject StaticValue(string className, string fieldName, string valueType)
	{
		var (address, where) = StaticField(ClassNamed(className), className, fieldName, valueType, ValueTypeCodes);
		return Value(address, ClassNamed(valueType), valueType, where);
	}

	/// <summary>
	/// The runtime class of a class that isn't generic, such as the value type <c>Plarium.Common.Numerics.Fixed</c>,
	/// from the TypeInfo slot the IL2CPP runtime writes it into when the game's code first uses it (#26). A runtime
	/// class doesn't change while the Game client runs, so one read of the Game client looks each slot up once.
	/// </summary>
	public RuntimeClass ClassNamed(string fullName)
	{
		if (_namedClasses.TryGetValue(fullName, out var known))
		{
			return known;
		}

		var slot = _memory.GameAssembly.ImageBase + Metadata.TypeInfoRva(fullName);
		var classAddress = Pointer(slot, () => $"the TypeInfo slot of {fullName}");
		// Until the runtime sets it up, the slot holds the encoded reference the binary shipped with, which is odd.
		if (classAddress == 0 || (classAddress & 1) != 0)
		{
			throw new GameDataException(
				$"The Game client hasn't set up {fullName} yet: its TypeInfo slot at 0x{slot:X} holds 0x{classAddress:X}, not a runtime class.");
		}

		var runtimeClass = Class(classAddress);
		if (runtimeClass.FullName != fullName)
		{
			throw new GameDataException(
				$"{fullName}'s TypeInfo slot at 0x{slot:X} holds the runtime class {runtimeClass.FullName} at 0x{classAddress:X}: the TypeInfo slots of this Game client aren't where its files say.");
		}

		_namedClasses[fullName] = runtimeClass;
		return runtimeClass;
	}

	/// <summary>The object a pointer read from <paramref name="where"/> at <paramref name="source"/> refers to, checked to be of <paramref name="expectedClass"/>.</summary>
	public ManagedObject Object(ulong address, string where, ulong source, string expectedClass)
	{
		if (address == 0)
		{
			throw new GameDataException($"{where} at 0x{source:X} is null, where raid-bridge expects a {expectedClass}.");
		}

		RuntimeClass runtimeClass;
		try
		{
			var classAddress = Pointer(address + (ulong)Layout.Object.Class, () => "its class pointer");
			runtimeClass = classAddress != 0 ? Class(classAddress) : throw new GameDataException("Its class pointer is null.");
		}
		catch (GameDataException exception)
		{
			throw new GameDataException($"{where} at 0x{source:X} points to 0x{address:X}, which isn't an object. {exception.Message}", exception);
		}

		if (!runtimeClass.Is(expectedClass, () => $"{where} at 0x{source:X} points to a {runtimeClass.FullName} at 0x{address:X}"))
		{
			throw new GameDataException(
				$"{where} at 0x{source:X} points to a {runtimeClass.FullName} at 0x{address:X}, not a {expectedClass}.");
		}

		return new(this, address, runtimeClass, expectedClass);
	}

	/// <summary>
	/// A value type held unboxed at <paramref name="address"/>, such as an array's element, read as
	/// <paramref name="className"/>. IL2CPP counts a value type's field offsets from the start of its boxed form, so
	/// the object it is read through starts the size of an object header earlier.
	/// </summary>
	public ManagedObject Value(ulong address, RuntimeClass runtimeClass, string className, string where) =>
		runtimeClass.Is(className, () => $"{where} at 0x{address:X} holds a {runtimeClass.FullName}")
			? new(this, address - (ulong)Layout.Object.HeaderSize, runtimeClass, className)
			: throw new GameDataException($"{where} at 0x{address:X} holds a {runtimeClass.FullName}, not a {className}.");

	/// <summary>The text a System.String pointer read from <paramref name="where"/> at <paramref name="source"/> refers to, or null.</summary>
	public string? String(ulong address, string where, ulong source)
	{
		if (address == 0)
		{
			return null;
		}

		var text = Object(address, where, source, "System.String");
		var length = Int32(address + (ulong)Layout.String.Length, () => $"the length of {where}");
		if (length is < 0 or > MaxStringLength)
		{
			throw new GameDataException($"{where} at 0x{source:X} points to a System.String at 0x{address:X} of length {length}, which isn't a length.");
		}

		return Encoding.Unicode.GetString(Bytes(text.Address + (ulong)Layout.String.Chars, length * sizeof(char), () => where));
	}

	/// <summary>The Il2CppClass at a non-null address, with its name, its namespace and, for a nested class, its declaring class.</summary>
	public RuntimeClass Class(ulong address) => Class(address, nesting: 0);

	/// <summary>The runtime class of a generic instance field's type, such as a Nullable&lt;T&gt; held in an object.</summary>
	public RuntimeClass FieldTypeClass(RuntimeField field, string where)
	{
		var genericClass = Pointer(field.Type + (ulong)Layout.Type.Data, () => $"the generic class of {where}'s type");
		var classAddress = genericClass != 0
			? Pointer(genericClass + (ulong)Layout.GenericClass.CachedClass, () => $"the runtime class of {where}'s type")
			: 0;
		return classAddress != 0
			? Class(classAddress)
			: throw new GameDataException($"The Game client hasn't set up the type of {where}: its generic class record 0x{genericClass:X} holds no runtime class.");
	}

	/// <summary>
	/// Checks the reader's expectation of a field against the installed game's metadata: the field exists on the
	/// class or a base class, with this declared type, static or not.
	/// </summary>
	public GameField Expect(string className, string fieldName, string declaredType, bool isStatic)
	{
		var declared = Metadata.Field(className, fieldName);
		if (declared.Type != declaredType || declared.IsStatic != isStatic)
		{
			throw new GameDataException(
				$"The game's {declared.DeclaringClass}.{fieldName} is {Describe(declared.Type, declared.IsStatic)}, where raid-bridge expects {Describe(declaredType, isStatic)}.");
		}

		return declared;
	}

	public ulong Pointer(ulong address, Func<string> what) => BinaryPrimitives.ReadUInt64LittleEndian(Bytes(address, sizeof(ulong), what));

	public long Int64(ulong address, Func<string> what) => BinaryPrimitives.ReadInt64LittleEndian(Bytes(address, sizeof(long), what));

	public double Double(ulong address, Func<string> what) => BinaryPrimitives.ReadDoubleLittleEndian(Bytes(address, sizeof(double), what));

	public int Int32(ulong address, Func<string> what) => BinaryPrimitives.ReadInt32LittleEndian(Bytes(address, sizeof(int), what));

	public uint UInt32(ulong address, Func<string> what) => BinaryPrimitives.ReadUInt32LittleEndian(Bytes(address, sizeof(uint), what));

	public ushort UInt16(ulong address, Func<string> what) => BinaryPrimitives.ReadUInt16LittleEndian(Bytes(address, sizeof(ushort), what));

	public byte Byte(ulong address, Func<string> what) => Bytes(address, sizeof(byte), what)[0];

	/// <summary>A null-terminated UTF-8 string, as the runtime keeps class and field names, read a page at a time.</summary>
	public string CString(ulong address, Func<string> what)
	{
		if (address == 0)
		{
			throw new GameDataException($"{what()} is null.");
		}

		var bytes = new List<byte>();
		var position = address;
		while (bytes.Count < MaxNameLength)
		{
			var toPageEnd = PageSize - (int)(position % PageSize);
			var chunk = Bytes(position, Math.Min(toPageEnd, MaxNameLength - bytes.Count), what);
			var end = Array.IndexOf(chunk, (byte)0);
			if (end >= 0)
			{
				bytes.AddRange(chunk.AsSpan(0, end));
				return Encoding.UTF8.GetString([.. bytes]);
			}

			bytes.AddRange(chunk);
			position += (ulong)chunk.Length;
		}

		throw new GameDataException($"{what()} at 0x{address:X} isn't a name: it runs past {MaxNameLength} bytes.");
	}

	/// <summary>A corrupt chain of declaring classes ends with an error, not a stack overflow (code review, #25).</summary>
	private RuntimeClass Class(ulong address, int nesting)
	{
		if (_classes.TryGetValue(address, out var known))
		{
			return known;
		}

		if (nesting > MaxNesting)
		{
			throw new GameDataException($"The runtime class 0x{address:X} is nested more than {MaxNesting} classes deep.");
		}

		string What(string part) => $"the {part} of the runtime class 0x{address:X}";
		string Name(int offset, string part) => CString(Pointer(address + (ulong)offset, () => What(part)), () => What(part));

		var name = Name(Layout.Class.Name, "name");
		var @namespace = Name(Layout.Class.Namespace, "namespace");
		var declaringType = Pointer(address + (ulong)Layout.Class.DeclaringType, () => What("declaring class"));
		var runtimeClass = new RuntimeClass(this, address, name, @namespace, declaringType != 0 ? Class(declaringType, nesting + 1).FullName : null);
		_classes[address] = runtimeClass;
		return runtimeClass;
	}

	/// <summary>The address of a static field of <paramref name="runtimeClass"/> of one of <paramref name="typeCodes"/>, read as <paramref name="className"/>.</summary>
	private (ulong Address, string Where) StaticField(RuntimeClass runtimeClass, string className, string fieldName, string declaredType, IReadOnlySet<byte> typeCodes)
	{
		var field = runtimeClass.Field(Expect(runtimeClass.FullName, fieldName, declaredType, isStatic: true), typeCodes);
		var staticFields = Pointer(runtimeClass.Address + (ulong)Layout.Class.StaticFields, () => $"the static fields of {className}");
		if (staticFields == 0)
		{
			throw new GameDataException(
				$"The runtime class {className} at 0x{runtimeClass.Address:X} has no static fields yet: the Game client hasn't set it up.");
		}

		return (staticFields + (ulong)field.Offset, $"{className}.{fieldName}");
	}

	private RuntimeClass GenericInstanceClass(string fullName)
	{
		var record = _memory.GameAssembly.ImageBase + Metadata.GenericClassRva(fullName);
		var classAddress = Pointer(record + (ulong)Layout.GenericClass.CachedClass, () => $"the generic class record of {fullName}");
		if (classAddress == 0)
		{
			throw new GameDataException(
				$"The Game client hasn't set up {fullName} yet: its generic class record at 0x{record:X} holds no runtime class.");
		}

		var runtimeClass = Class(classAddress);
		var genericClass = Pointer(classAddress + (ulong)Layout.Class.GenericClass, () => $"the generic class of the runtime class at 0x{classAddress:X}");
		if (runtimeClass.FullName != fullName.Split('<', 2)[0] || genericClass != record)
		{
			throw new GameDataException(
				$"{fullName}'s generic class record at 0x{record:X} leads to a runtime class {runtimeClass.FullName} at 0x{classAddress:X} whose record is 0x{genericClass:X}: the IL2CPP layout for Unity {Layout.UnityVersion} doesn't fit this Game client.");
		}

		return runtimeClass;
	}

	private byte[] Bytes(ulong address, int length, Func<string> what)
	{
		var buffer = new byte[length];
		try
		{
			_memory.Read(address, buffer);
		}
		catch (GameMemoryException exception)
		{
			throw new GameDataException($"Can't read {what()} at 0x{address:X}: {exception.Message}", exception);
		}

		return buffer;
	}

	private static string Describe(string type, bool isStatic) => isStatic ? $"a static {type}" : $"a {type}";
}
