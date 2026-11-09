using RaidBridge.Game.Metadata;

namespace RaidBridge.Game.Tests.Metadata;

public class GameVersionsTests
{
	[Fact]
	public void Reads_the_supported_build()
	{
		using var game = SyntheticGame.Create();

		Assert.Equal(new GameVersions(39, "6000.3.18f1"), GameVersions.Read(GameFiles.In(game.Directory)));
	}

	[Theory]
	[InlineData(38)]
	[InlineData(31)]
	[InlineData(104)]
	public void Refuses_another_metadata_version_by_name(int version)
	{
		using var game = SyntheticGame.Create(metadataVersion: version);

		var error = Assert.Throws<GameMetadataException>(() => GameVersions.Read(GameFiles.In(game.Directory)));

		Assert.Equal($"The installed game has IL2CPP metadata version {version}; raid-bridge reads only version 39.", error.Message);
	}

	[Theory]
	[InlineData("6000.3.17f1")]
	[InlineData("6000.3.19f1")]
	[InlineData("6000.4.0f1")]
	public void Refuses_another_unity_version_by_name(string version)
	{
		using var game = SyntheticGame.Create(bundleUnity: version, playerUnity: version);

		var error = Assert.Throws<GameMetadataException>(() => GameVersions.Read(GameFiles.In(game.Directory)));

		Assert.Equal($"The installed game is built with Unity {version}; raid-bridge reads only Unity 6000.3.18f1.", error.Message);
	}

	[Fact]
	public void Refuses_files_that_disagree_on_the_unity_version()
	{
		using var game = SyntheticGame.Create(bundleUnity: "6000.3.18f1", playerUnity: "6000.3.17f1");

		var error = Assert.Throws<GameMetadataException>(() => GameVersions.Read(GameFiles.In(game.Directory)));

		Assert.Contains("disagree on its Unity version", error.Message, StringComparison.Ordinal);
		Assert.Contains("says 6000.3.18f1", error.Message, StringComparison.Ordinal);
		Assert.Contains("says 6000.3.17f1", error.Message, StringComparison.Ordinal);
	}
}
