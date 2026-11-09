using LibCpp2IL.Logging;

namespace RaidBridge.Game.Metadata;

/// <summary>
/// Keeps LibCpp2IL off the console, where its default writer prints its progress, and keeps its warnings and errors so
/// the parse that raised them is refused rather than trusted.
/// </summary>
internal sealed class ParserLog : LogWriter
{
	private readonly List<string> _problems = [];

	public IReadOnlyList<string> Problems => _problems;

	public override void Info(string message)
	{
	}

	public override void Verbose(string message)
	{
	}

	public override void Warn(string message) => _problems.Add($"warning: {message.Trim()}");

	public override void Error(string message) => _problems.Add($"error: {message.Trim()}");
}
