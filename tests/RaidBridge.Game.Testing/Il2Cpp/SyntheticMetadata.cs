using RaidBridge.Game.Metadata;

namespace RaidBridge.Game.Testing.Il2Cpp;

/// <summary>
/// A synthetic game's metadata: the classes and fields it declares, with their base classes, its enums, and where its
/// binary keeps generic class records. It refuses what isn't declared with the same errors the real metadata raises.
/// </summary>
internal sealed class SyntheticMetadata(GameVersions versions) : IGameMetadata
{
	private readonly Dictionary<string, (string? BaseClass, List<GameField> Fields)> _classes = new(StringComparer.Ordinal);
	private readonly Dictionary<string, ulong> _genericClassRvas = new(StringComparer.Ordinal);
	private readonly Dictionary<string, ulong> _typeInfoRvas = new(StringComparer.Ordinal);
	private readonly Dictionary<string, GameEnum> _enums = new(StringComparer.Ordinal);
	private readonly Dictionary<(string ClassName, string FieldName), long> _constants = [];

	public GameVersions Versions { get; } = versions;

	public void Class(string fullName, string? baseClass, params (string Name, string Type, bool IsStatic)[] fields) =>
		_classes[fullName] = (baseClass, [.. fields.Select(field => new GameField(fullName, field.Name, field.Type, field.IsStatic))]);

	public void GenericClass(string fullName, ulong rva) => _genericClassRvas[fullName] = rva;

	public void TypeInfo(string fullName, ulong rva) => _typeInfoRvas[fullName] = rva;

	public void Constant(string className, string fieldName, long value) => _constants[(className, fieldName)] = value;

	public void Enum(string fullName, params (string Name, long Value)[] members) => Enum(fullName, "System.Int32", members);

	/// <summary>An enum over another underlying type than the game's enums use.</summary>
	public void Enum(string fullName, string underlyingType, params (string Name, long Value)[] members) =>
		_enums[fullName] = new(fullName, underlyingType, [.. members.Select(member => new GameEnumMember(member.Name, member.Value))]);

	/// <summary>A game update dropped a member from an enum.</summary>
	public void RemoveEnumMember(string fullName, string name)
	{
		var declared = _enums[fullName];
		_enums[fullName] = declared with { Members = [.. declared.Members.Where(member => member.Name != name)] };
	}

	/// <summary>A game update added a member to an enum.</summary>
	public void AddEnumMember(string fullName, string name, long value)
	{
		var declared = _enums[fullName];
		_enums[fullName] = declared with { Members = [.. declared.Members, new GameEnumMember(name, value)] };
	}

	public void Rename(string fullName, string newName)
	{
		var (baseClass, fields) = _classes[fullName];
		_classes.Remove(fullName);
		_classes[newName] = (baseClass, [.. fields.Select(field => field with { DeclaringClass = newName })]);
	}

	public void Undeclare(string className, string fieldName) => _classes[className].Fields.RemoveAll(field => field.Name == fieldName);

	public void Retype(string className, string fieldName, string type)
	{
		var fields = _classes[className].Fields;
		var index = fields.FindIndex(field => field.Name == fieldName);
		fields[index] = fields[index] with { Type = type };
	}

	public GameField Field(string className, string fieldName)
	{
		if (!_classes.ContainsKey(className))
		{
			throw new GameMetadataException($"The game's metadata has no class {className}.");
		}

		for (var name = className; name is not null && _classes.TryGetValue(name, out var declared); name = declared.BaseClass)
		{
			if (declared.Fields.Find(field => field.Name == fieldName) is { } field)
			{
				return field;
			}
		}

		throw new GameMetadataException($"The game's class {className} has no field {fieldName}, base classes included.");
	}

	public GameEnum Enum(string fullName) =>
		_enums.TryGetValue(fullName, out var declared)
			? declared
			: throw new GameMetadataException($"The game's metadata has no class {fullName}.");

	public long Constant(string className, string fieldName) =>
		_constants.TryGetValue((className, fieldName), out var value)
			? value
			: throw new GameMetadataException($"The game's {className}.{fieldName} isn't a constant.");

	public ulong TypeInfoRva(string fullName) =>
		_typeInfoRvas.TryGetValue(fullName, out var rva)
			? rva
			: throw new GameMetadataException($"The game's binary has no TypeInfo slot for {fullName}.");

	public ulong GenericClassRva(string fullName) =>
		_genericClassRvas.TryGetValue(fullName, out var rva)
			? rva
			: throw new GameMetadataException($"The game's binary has no generic instance class {fullName}.");
}
