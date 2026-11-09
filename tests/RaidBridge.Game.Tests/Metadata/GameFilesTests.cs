using RaidBridge.Game.Metadata;

namespace RaidBridge.Game.Tests.Metadata;

public class GameFilesTests
{
	[Fact]
	public void Finds_the_files_under_the_install_folder()
	{
		using var game = SyntheticGame.Create();

		var files = GameFiles.In(game.Directory);

		Assert.Equal(game.PathOf(Path.Combine("Raid_Data", "il2cpp_data", "Metadata", "global-metadata.dat")), files.GlobalMetadata);
		Assert.Equal(game.PathOf("GameAssembly.dll"), files.GameAssembly);
	}

	[Theory]
	[InlineData("GameAssembly.dll")]
	[InlineData("UnityPlayer.dll")]
	[InlineData("Raid_Data/data.unity3d")]
	[InlineData("Raid_Data/il2cpp_data/Metadata/global-metadata.dat")]
	public void Names_the_file_that_is_missing(string file)
	{
		using var game = SyntheticGame.Create();
		game.Delete(file);

		var error = Assert.Throws<GameMetadataException>(() => GameFiles.In(game.Directory));

		Assert.Equal($"{game.Directory} isn't the Game client's folder: it has no {file.Replace('/', Path.DirectorySeparatorChar)}.", error.Message);
	}
}
