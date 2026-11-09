namespace RaidBridge.Game;

/// <summary>
/// The Game client or its installed files can't be read, or aren't what raid-bridge expects. The message names the
/// cause in one line, so the command line can show it as it is.
/// </summary>
public class GameClientException : Exception
{
	public GameClientException()
	{
	}

	public GameClientException(string message)
		: base(message)
	{
	}

	public GameClientException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}
