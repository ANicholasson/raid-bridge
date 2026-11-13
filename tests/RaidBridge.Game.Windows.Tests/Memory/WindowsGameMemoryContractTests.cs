using RaidBridge.Game.Memory;
using RaidBridge.Game.Testing.Memory;
using RaidBridge.Game.Windows.Memory;

namespace RaidBridge.Game.Windows.Tests.Memory;

/// <summary>Gated: the Windows adapter keeps the memory contract on the running Game client (ADR-0002).</summary>
public sealed class WindowsGameMemoryContractTests : GameMemoryContract
{
	private readonly Lazy<WindowsGameMemory> _memory = new(LiveGame.Open);

	protected override IGameMemory Memory => _memory.Value;
}
