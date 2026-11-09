namespace RaidBridge.Game.Metadata;

/// <summary>
/// What the installed game's files tell the reader: the build's versions, each field's declared type, each enum's
/// members, the game's constants, and where the binary keeps the records and slots that lead to runtime classes and
/// static fields. <see cref="GameMetadata"/> parses them from the files; tests describe a synthetic game instead
/// (spec #20).
/// </summary>
internal interface IGameMetadata
{
	GameVersions Versions { get; }

	/// <inheritdoc cref="GameMetadata.Field"/>
	GameField Field(string className, string fieldName);

	/// <inheritdoc cref="GameMetadata.Enum"/>
	GameEnum Enum(string fullName);

	/// <inheritdoc cref="GameMetadata.Constant"/>
	long Constant(string className, string fieldName);

	/// <inheritdoc cref="GameMetadata.GenericClassRva"/>
	ulong GenericClassRva(string fullName);

	/// <inheritdoc cref="GameMetadata.TypeInfoRva"/>
	ulong TypeInfoRva(string fullName);
}
