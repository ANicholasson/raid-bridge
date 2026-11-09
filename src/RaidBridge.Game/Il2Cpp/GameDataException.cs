namespace RaidBridge.Game.Il2Cpp;

/// <summary>
/// A value read from the Game client isn't what the reader expects: a class, field or field type that moved, a bad
/// pointer, or a value out of range. The message names the type, the field and the address.
/// </summary>
internal sealed class GameDataException : GameClientException
{
	public GameDataException(string message)
		: base(message)
	{
	}

	public GameDataException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}
