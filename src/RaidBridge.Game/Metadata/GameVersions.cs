namespace RaidBridge.Game.Metadata;

/// <summary>
/// The installed game's IL2CPP metadata version and Unity version, checked against the one build the reader knows
/// before anything else is parsed. The Unity version comes from data.unity3d and must match UnityPlayer.dll's.
/// </summary>
internal sealed record GameVersions(int Metadata, string Unity)
{
	public static GameVersions Read(GameFiles files)
	{
		var metadata = MetadataHeader.Version(Start(files.GlobalMetadata, MetadataHeader.Length), files.GlobalMetadata);
		if (metadata != SupportedVersions.Metadata)
		{
			throw new GameMetadataException(
				$"The installed game has IL2CPP metadata version {metadata}; raid-bridge reads only version {SupportedVersions.Metadata}.");
		}

		var unity = UnityBundleHeader.UnityVersion(Start(files.DataBundle, UnityBundleHeader.MaxLength), files.DataBundle);
		string playerVersion;
		using (var unityPlayer = File.OpenRead(files.UnityPlayer))
		{
			playerVersion = PeProductVersion.Read(unityPlayer, files.UnityPlayer);
		}

		var player = playerVersion.Split(' ', 2)[0];
		if (player != unity)
		{
			throw new GameMetadataException(
				$"The installed game's files disagree on its Unity version: {files.DataBundle} says {unity}, {files.UnityPlayer} says {player}.");
		}

		if (!SupportedVersions.Unity.Contains(unity))
		{
			throw new GameMetadataException(
				$"The installed game is built with Unity {unity}; raid-bridge reads only Unity {string.Join(", ", SupportedVersions.Unity)}.");
		}

		return new(metadata, unity);
	}

	private static byte[] Start(string path, int maxLength)
	{
		using var file = File.OpenRead(path);
		var buffer = new byte[Math.Min(maxLength, file.Length)];
		file.ReadExactly(buffer);
		return buffer;
	}
}
