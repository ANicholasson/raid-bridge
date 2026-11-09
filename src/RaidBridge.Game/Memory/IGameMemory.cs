namespace RaidBridge.Game.Memory;

/// <summary>
/// The running Game client's memory, read and nothing more (ADR-0001). Each platform has one adapter behind this
/// port, which finds the Game client when it is made, and a fake implements it for tests (ADR-0002).
/// </summary>
public interface IGameMemory
{
	/// <summary>When the Game client's process started, so a game updated under it can be noticed.</summary>
	DateTimeOffset StartedAt { get; }

	/// <summary>GameAssembly.dll as the Game client loaded it: where its image starts in memory and the file it came from.</summary>
	GameModule GameAssembly { get; }

	/// <summary>Fills the buffer from the Game client's memory at the address, or throws <see cref="GameMemoryException"/>.</summary>
	void Read(ulong address, Span<byte> buffer);
}

/// <summary>A module the Game client loaded: its image base in memory, and the path of its file on this machine.</summary>
public sealed record GameModule(ulong ImageBase, string FilePath);
