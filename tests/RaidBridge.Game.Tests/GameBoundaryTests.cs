using RaidBridge.Game.Snapshots;

namespace RaidBridge.Game.Tests;

/// <summary>ADR-0004: dependencies point inward, so the Game layer sees the Domain and its parser, never Export or the Cli.</summary>
public class GameBoundaryTests
{
	private static readonly string RuntimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

	[Fact]
	public void The_game_layer_references_only_the_domain_and_the_vendored_parser()
	{
		var references = typeof(GameSnapshotSource).Assembly.GetReferencedAssemblies()
			.Select(reference => reference.Name!)
			.Where(name => !File.Exists(Path.Combine(RuntimeDirectory, $"{name}.dll")))
			.Order(StringComparer.Ordinal)
			.ToList();

		Assert.Equal(["AssetRipper.Primitives", "LibCpp2IL", "RaidBridge.Domain"], references);
	}
}
