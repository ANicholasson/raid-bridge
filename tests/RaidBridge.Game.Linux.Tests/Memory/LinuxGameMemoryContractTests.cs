using RaidBridge.Game.Linux.Memory;
using RaidBridge.Game.Memory;
using RaidBridge.Game.Testing.Memory;

namespace RaidBridge.Game.Linux.Tests.Memory;

/// <summary>Gated: the Linux adapter keeps the memory contract on the running Game client (ADR-0002).</summary>
public sealed class LinuxGameMemoryContractTests : GameMemoryContract
{
	private readonly Lazy<LinuxGameMemory> _memory = new(LiveGame.Open);

	protected override IGameMemory Memory => _memory.Value;
}
