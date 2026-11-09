using RaidBridge.Game.Linux.Memory;

namespace RaidBridge.Game.Linux.Tests;

/// <summary>ADR-0004: the Linux adapter implements the Game layer's memory port and sees nothing else of raid-bridge.</summary>
public class GameLinuxBoundaryTests
{
	private static readonly string RuntimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

	[Fact]
	public void The_linux_adapter_references_only_the_game_layer()
	{
		var references = typeof(LinuxGameMemory).Assembly.GetReferencedAssemblies()
			.Select(reference => reference.Name!)
			.Where(name => !File.Exists(Path.Combine(RuntimeDirectory, $"{name}.dll")))
			.ToList();

		Assert.Equal(["RaidBridge.Game"], references);
	}
}
