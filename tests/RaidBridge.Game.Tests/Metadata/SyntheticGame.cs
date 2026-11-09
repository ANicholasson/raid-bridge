using System.Buffers.Binary;
using System.Text;

namespace RaidBridge.Game.Tests.Metadata;

/// <summary>
/// A Game client install folder in a temporary directory, holding only the headers the version checks read. No real
/// game file is committed or needed (spec #20, synthetic fixtures only).
/// </summary>
internal sealed class SyntheticGame : IDisposable
{
	private SyntheticGame(string directory) => Directory = directory;

	public string Directory { get; }

	public static SyntheticGame Create(int metadataVersion = 39, string bundleUnity = "6000.3.18f1", string playerUnity = "6000.3.18f1")
	{
		var game = new SyntheticGame(System.IO.Directory.CreateTempSubdirectory("raid-bridge-game-").FullName);
		game.Write("GameAssembly.dll", Encoding.ASCII.GetBytes("not a real binary"));
		game.Write(Path.Combine("Raid_Data", "il2cpp_data", "Metadata", "global-metadata.dat"), MetadataHeader(metadataVersion));
		game.Write(Path.Combine("Raid_Data", "data.unity3d"), BundleHeader(bundleUnity));
		game.Write("UnityPlayer.dll", SyntheticPe.WithProductVersion($"{playerUnity} (5ebeb53e4c07)"));
		return game;
	}

	public static byte[] MetadataHeader(int version)
	{
		var header = new byte[16];
		BinaryPrimitives.WriteUInt32LittleEndian(header, 0xFAB11BAF);
		BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4), version);
		return header;
	}

	/// <summary>A UnityFS header as Unity writes it: signature, big-endian format version 8, "5.x.x", engine version, then more header.</summary>
	public static byte[] BundleHeader(string unityVersion) =>
		[.. Encoding.ASCII.GetBytes("UnityFS\0"), 0, 0, 0, 8, .. Encoding.ASCII.GetBytes($"5.x.x\0{unityVersion}\0"), .. new byte[64]];

	public string PathOf(string relativePath) => Path.Combine(Directory, relativePath);

	public void Write(string relativePath, byte[] bytes)
	{
		var path = PathOf(relativePath);
		System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		File.WriteAllBytes(path, bytes);
	}

	public void Delete(string relativePath) => File.Delete(PathOf(relativePath));

	public void Dispose() => System.IO.Directory.Delete(Directory, recursive: true);
}
