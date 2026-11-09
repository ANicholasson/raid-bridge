namespace RaidBridge.Game.Metadata;

/// <summary>The installed Game client's files the reader parses, found under its install folder, the one holding Raid.exe.</summary>
internal sealed record GameFiles(string GameAssembly, string GlobalMetadata, string DataBundle, string UnityPlayer)
{
	public static GameFiles In(string gameDirectory)
	{
		var dataDirectory = Path.Combine(gameDirectory, "Raid_Data");
		var files = new GameFiles(
			GameAssembly: Path.Combine(gameDirectory, "GameAssembly.dll"),
			GlobalMetadata: Path.Combine(dataDirectory, "il2cpp_data", "Metadata", "global-metadata.dat"),
			DataBundle: Path.Combine(dataDirectory, "data.unity3d"),
			UnityPlayer: Path.Combine(gameDirectory, "UnityPlayer.dll"));

		var missing = new[] { files.GameAssembly, files.GlobalMetadata, files.DataBundle, files.UnityPlayer }
			.FirstOrDefault(path => !File.Exists(path));
		if (missing is not null)
		{
			throw new GameMetadataException(
				$"{gameDirectory} isn't the Game client's folder: it has no {Path.GetRelativePath(gameDirectory, missing)}.");
		}

		return files;
	}
}
