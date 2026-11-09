using RaidBridge.Game.Metadata;

namespace RaidBridge.Game.Tests.Metadata;

public class GameMetadataTests
{
	[Fact]
	public void Refuses_an_unknown_build_before_parsing_it()
	{
		// The synthetic GameAssembly.dll isn't a binary at all, so reaching LibCpp2IL would fail with its own error.
		using var game = SyntheticGame.Create(bundleUnity: "6000.3.19f1", playerUnity: "6000.3.19f1");

		var error = Assert.Throws<GameMetadataException>(() => GameMetadata.Load(GameFiles.In(game.Directory)));

		Assert.Equal("The installed game is built with Unity 6000.3.19f1; raid-bridge reads only Unity 6000.3.18f1.", error.Message);
	}

	[Fact]
	public void Names_the_files_when_the_parser_fails()
	{
		using var game = SyntheticGame.Create();

		var error = Assert.Throws<GameMetadataException>(() => GameMetadata.Load(GameFiles.In(game.Directory)));

		Assert.StartsWith($"LibCpp2IL can't parse {game.PathOf(Path.Combine("Raid_Data", "il2cpp_data", "Metadata", "global-metadata.dat"))}", error.Message, StringComparison.Ordinal);
	}
}
