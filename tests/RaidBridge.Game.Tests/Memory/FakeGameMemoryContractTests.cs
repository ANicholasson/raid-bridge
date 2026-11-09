using RaidBridge.Game.Memory;
using RaidBridge.Game.Testing;
using RaidBridge.Game.Testing.Memory;

namespace RaidBridge.Game.Tests.Memory;

/// <summary>The fake keeps the contract the real adapters keep, so tests through it say something about them.</summary>
public sealed class FakeGameMemoryContractTests : GameMemoryContract, IDisposable
{
	private readonly SyntheticGameClient _game = SyntheticGameClient.Create();

	protected override IGameMemory Memory => _game.Memory;

	public void Dispose() => _game.Dispose();
}
