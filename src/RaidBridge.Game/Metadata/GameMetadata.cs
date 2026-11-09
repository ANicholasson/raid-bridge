using System.Buffers.Binary;
using System.Globalization;
using System.Reflection;
using System.Reflection.PortableExecutable;
using LibCpp2IL;
using LibCpp2IL.BinaryStructures;
using LibCpp2IL.Logging;
using LibCpp2IL.Metadata;

namespace RaidBridge.Game.Metadata;

/// <summary>
/// The installed game's classes and fields, from global-metadata.dat and GameAssembly.dll. The only code that sees
/// the vendored LibCpp2IL: what it parses is answered here in the reader's own types (spec #20).
/// </summary>
internal sealed class GameMetadata : IGameMetadata
{
	/// <summary>LibCpp2IL's log writer and settings are process-wide, so one parse runs at a time.</summary>
	private static readonly Lock ParserLock = new();

	/// <summary>
	/// A TypeInfo slot's value on disk, from metadata v27: the usage type 1 (TypeInfo) in bits 29 to 31, the index of
	/// the class's Il2CppType in bits 1 to 28, and bit 0 set. IL2CPP's il2cpp-metadata.h defines the encoding.
	/// </summary>
	private const ulong TypeInfoTag = 0x2000_0001;

	private const ulong TypeInfoTagMask = 0xFFFF_FFFF_E000_0001;

	private readonly ILookup<string, Il2CppTypeDefinition> _classes;
	private readonly Il2CppBinary _binary;
	private readonly string _gameAssembly;
	private ILookup<int, ulong>? _typeInfoSlots;

	// The reader asks for the same field of every entry of a dictionary, so each answer is kept for this parse (code review, #25).
	private readonly Dictionary<(string ClassName, string FieldName), GameField> _fields = [];
	private readonly Dictionary<string, GameEnum> _enums = new(StringComparer.Ordinal);

	private GameMetadata(GameVersions versions, LibCpp2IlContext context, string gameAssembly)
	{
		Versions = versions;
		_classes = context.Metadata.typeDefs.ToLookup(type => type.FullName ?? string.Empty, StringComparer.Ordinal);
		_binary = context.Binary;
		_gameAssembly = gameAssembly;
	}

	public GameVersions Versions { get; }

	/// <summary>
	/// Checks the versions, then parses the files. Refuses a game build the reader doesn't know, and a parse that
	/// LibCpp2IL itself warned about.
	/// </summary>
	public static GameMetadata Load(GameFiles files)
	{
		var versions = GameVersions.Read(files);

		lock (ParserLock)
		{
			var log = new ParserLog();
			LibLogger.Writer = log;
			// The reader never looks up a method by address, and this mapping is most of the parse time.
			LibCpp2IlMain.Settings.DisableMethodPointerMapping = true;

			LibCpp2IlContext context;
			try
			{
				context = LibCpp2IlMain.LoadFromFileAsContext(
					files.GameAssembly, files.GlobalMetadata, AssetRipper.Primitives.UnityVersion.Parse(versions.Unity));
			}
			catch (Exception exception) when (exception is not OutOfMemoryException)
			{
				throw new GameMetadataException(
					$"LibCpp2IL can't parse {files.GlobalMetadata} with {files.GameAssembly}: {exception.Message}", exception);
			}

			if (log.Problems.Count > 0)
			{
				throw new GameMetadataException(
					$"LibCpp2IL parsed {files.GlobalMetadata} with {files.GameAssembly} but reported: {string.Join(" ", log.Problems)}");
			}

			return new(versions, context, files.GameAssembly);
		}
	}

	public GameClass Class(string fullName) => Parsed(fullName, () =>
	{
		var type = Single(fullName);
		var attributes = type.FieldAttributes ?? [];
		return new GameClass(
			FullName: fullName,
			BaseType: type.BaseType?.ToString(),
			Interfaces: [.. (type.Interfaces ?? []).Select(face => face.ToString())],
			Fields: [.. (type.Fields ?? []).Select((field, index) => Describe(fullName, field, attributes[index]))]);
	});

	/// <summary>
	/// The field with this name on the class or the nearest base class that declares it. A field declared on a
	/// generic base class keeps its declared type, such as <c>T</c>.
	/// </summary>
	public GameField Field(string className, string fieldName)
	{
		if (!_fields.TryGetValue((className, fieldName), out var field))
		{
			field = ParseField(className, fieldName);
			_fields[(className, fieldName)] = field;
		}

		return field;
	}

	/// <summary>An enum's underlying type and its members, each with the value its metadata holds as the member's constant.</summary>
	public GameEnum Enum(string fullName)
	{
		if (!_enums.TryGetValue(fullName, out var declared))
		{
			declared = ParseEnum(fullName);
			_enums[fullName] = declared;
		}

		return declared;
	}

	private GameField ParseField(string className, string fieldName) => Parsed($"{className}.{fieldName}", () =>
	{
		for (var type = Single(className); type is not null; type = type.BaseType?.baseType)
		{
			var fields = type.Fields ?? [];
			var index = Array.FindIndex(fields, field => field.Name == fieldName);
			if (index >= 0)
			{
				return Describe(type.FullName!, fields[index], type.FieldAttributes![index]);
			}
		}

		throw new GameMetadataException($"The game's class {className} has no field {fieldName}, base classes included.");
	});

	private GameEnum ParseEnum(string fullName) => Parsed(fullName, () =>
	{
		var type = Single(fullName);
		if (!type.IsEnumType)
		{
			throw new GameMetadataException($"The game's {fullName} isn't an enum.");
		}

		var fields = type.Fields ?? [];
		var attributes = type.FieldAttributes ?? [];
		var defaults = type.FieldDefaults ?? [];
		var underlying = Array.FindIndex(fields, field => field.Name == "value__") is var index and >= 0
			? Describe(fullName, fields[index], attributes[index]).Type
			: throw new GameMetadataException($"The game's enum {fullName} has no value__ field.");
		var members = fields
			.Select((field, index) => (Field: field, Attributes: attributes[index], Default: defaults[index]))
			.Where(member => member.Attributes.HasFlag(FieldAttributes.Static | FieldAttributes.Literal))
			.Select(member => new GameEnumMember(
				member.Field.Name ?? throw new GameMetadataException($"A member of the game's enum {fullName} has no name."),
				Convert.ToInt64(
					member.Default ?? throw new GameMetadataException($"The game's {fullName}.{member.Field.Name} has no value."),
					CultureInfo.InvariantCulture)));
		return new GameEnum(fullName, underlying, [.. members]);
	});

	/// <summary>
	/// Where GameAssembly.dll holds the Il2CppGenericClass record of a generic instance class, such as
	/// <c>Client.App.SingleInstance`1&lt;Client.Model.AppModel&gt;</c>, as an offset from the module's image base. The
	/// record is static data, and the IL2CPP runtime writes the instance's runtime class into it when it first uses
	/// the class, so this is how the reader reaches a static field of a generic instance.
	/// </summary>
	public ulong GenericClassRva(string fullName) => Parsed(fullName, () =>
	{
		// Il2CppType's own name drops namespaces, so the name is compared in the form GameField.Type uses, and only
		// for instances of the right generic class.
		var definition = fullName.Split('<', 2)[0];
		var records = _binary.AllTypes
			.Where(type => type.Type == Il2CppTypeEnum.IL2CPP_TYPE_GENERICINST)
			.Where(type => type.GetGenericClass().TypeDefinition.FullName == definition)
			.Where(type => LibCpp2ILUtils.GetTypeReflectionData(type).ToString() == fullName)
			.Select(type => type.Data.GenericClass)
			.Distinct()
			.ToList();

		return records.Count switch
		{
			1 => _binary.GetRva(records[0]),
			0 => throw new GameMetadataException($"The game's binary has no generic instance class {fullName}."),
			_ => throw new GameMetadataException($"The game's binary has {records.Count} generic class records for {fullName}."),
		};
	});

	/// <summary>The value of a constant the game declares, such as <c>Plarium.Common.Numerics.Fixed.ONE</c>.</summary>
	public long Constant(string className, string fieldName) => Parsed($"{className}.{fieldName}", () =>
	{
		var type = Single(className);
		var fields = type.Fields ?? [];
		var index = Array.FindIndex(fields, field => field.Name == fieldName);
		if (index < 0)
		{
			throw new GameMetadataException($"The game's class {className} has no field {fieldName}.");
		}

		return type.FieldAttributes![index].HasFlag(FieldAttributes.Static | FieldAttributes.Literal) && type.FieldDefaults?[index] is { } value
			? Convert.ToInt64(value, CultureInfo.InvariantCulture)
			: throw new GameMetadataException($"The game's {className}.{fieldName} isn't a constant.");
	});

	/// <summary>
	/// Where GameAssembly.dll holds the TypeInfo slot of a class that isn't generic, such as
	/// <c>SharedModel.Meta.Artifacts.ArtifactStorage.ArtifactStorageResolver</c>, as an offset from the module's
	/// image base. The game's code keeps one such slot per class it uses, in writable data, holding an encoded
	/// reference to the class until the IL2CPP runtime first uses it and writes the runtime class there instead. So
	/// this is how the reader reaches the static fields and the runtime class of a class that isn't generic (#26).
	/// </summary>
	public ulong TypeInfoRva(string fullName) => Parsed(fullName, () =>
	{
		var definition = Single(fullName);
		var slots = _binary.AllTypes
			.Select((type, index) => (Type: type, Index: index))
			.Where(type => type.Type.Type is Il2CppTypeEnum.IL2CPP_TYPE_CLASS or Il2CppTypeEnum.IL2CPP_TYPE_VALUETYPE && type.Type.Byref == 0)
			.Where(type => ReferenceEquals(type.Type.AsClass(), definition))
			.SelectMany(type => TypeInfoSlots()[type.Index])
			.Distinct()
			.ToList();

		return slots.Count switch
		{
			1 => slots[0],
			0 => throw new GameMetadataException($"The game's binary has no TypeInfo slot for {fullName}."),
			_ => throw new GameMetadataException($"The game's binary has {slots.Count} TypeInfo slots for {fullName}."),
		};
	});

	/// <summary>Every TypeInfo slot in GameAssembly.dll's writable data, by the index of the Il2CppType it refers to, found once per parse.</summary>
	private ILookup<int, ulong> TypeInfoSlots()
	{
		if (_typeInfoSlots is not null)
		{
			return _typeInfoSlots;
		}

		using var file = File.OpenRead(_gameAssembly);
		var raw = _binary.GetRawBinaryContent();
		var slots = new List<(int TypeIndex, ulong Rva)>();
		foreach (var section in new PEHeaders(file).SectionHeaders.Where(section => section.SectionCharacteristics.HasFlag(SectionCharacteristics.MemWrite)))
		{
			var data = raw.Slice(section.PointerToRawData, Math.Min(section.SizeOfRawData, section.VirtualSize));
			for (var offset = 0; offset + sizeof(ulong) <= data.Length; offset += sizeof(ulong))
			{
				var value = BinaryPrimitives.ReadUInt64LittleEndian(data[offset..]);
				if ((value & TypeInfoTagMask) == TypeInfoTag)
				{
					slots.Add(((int)((value & 0x1FFF_FFFF) >> 1), (ulong)(section.VirtualAddress + offset)));
				}
			}
		}

		return _typeInfoSlots = slots.ToLookup(slot => slot.TypeIndex, slot => slot.Rva);
	}

	/// <summary>LibCpp2IL resolves types lazily, so a parse fault can surface here: it is named, never passed on raw.</summary>
	private static T Parsed<T>(string what, Func<T> read)
	{
		try
		{
			return read();
		}
		catch (Exception exception) when (exception is not (GameMetadataException or OutOfMemoryException))
		{
			throw new GameMetadataException($"LibCpp2IL can't resolve the game's {what}: {exception.Message}", exception);
		}
	}

	private Il2CppTypeDefinition Single(string fullName)
	{
		var matches = _classes[fullName].ToList();
		return matches.Count switch
		{
			1 => matches[0],
			0 => throw new GameMetadataException($"The game's metadata has no class {fullName}."),
			_ => throw new GameMetadataException(
				$"The game's metadata has {matches.Count} classes named {fullName}, in {string.Join(", ", matches.Select(type => type.DeclaringAssembly?.Name))}."),
		};
	}

	private static GameField Describe(string declaringClass, Il2CppFieldDefinition field, FieldAttributes attributes) =>
		new(
			DeclaringClass: declaringClass,
			Name: field.Name ?? throw new GameMetadataException($"A field of the game's class {declaringClass} has no name."),
			Type: field.FieldType?.ToString()
				?? throw new GameMetadataException($"The type of the game's field {declaringClass}.{field.Name} can't be resolved."),
			IsStatic: attributes.HasFlag(FieldAttributes.Static));
}
