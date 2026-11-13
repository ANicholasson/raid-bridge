using RaidBridge.Game.Windows.Memory;

namespace RaidBridge.Game.Windows.Tests;

/// <summary>ADR-0004: the Windows adapter implements the Game layer's memory port and sees nothing else of raid-bridge.</summary>
public class GameWindowsBoundaryTests
{
	private static readonly string RuntimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

	[Fact]
	public void The_windows_adapter_references_only_the_game_layer()
	{
		var references = typeof(WindowsGameMemory).Assembly.GetReferencedAssemblies()
			.Select(reference => reference.Name!)
			.Where(name => !File.Exists(Path.Combine(RuntimeDirectory, $"{name}.dll")))
			.ToList();

		Assert.Equal(["RaidBridge.Game"], references);
	}
}
