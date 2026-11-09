namespace RaidBridge.Game.Metadata;

/// <summary>The installed game's files can't be read, or aren't what the reader expects. The message names why.</summary>
internal sealed class GameMetadataException : GameClientException
{
	public GameMetadataException(string message)
		: base(message)
	{
	}

	public GameMetadataException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}
