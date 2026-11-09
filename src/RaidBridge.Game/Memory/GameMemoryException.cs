namespace RaidBridge.Game.Memory;

/// <summary>The Game client can't be found, isn't readable, or has nothing readable at an address. The message names which.</summary>
public sealed class GameMemoryException : GameClientException
{
	public GameMemoryException()
	{
	}

	public GameMemoryException(string message)
		: base(message)
	{
	}

	public GameMemoryException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}
