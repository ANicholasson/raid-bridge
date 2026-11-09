using System.Text;
using RaidBridge.Game.Metadata;

namespace RaidBridge.Game.Tests.Metadata;

public class UnityBundleHeaderTests
{
	[Theory]
	[InlineData("6000.3.18f1")]
	[InlineData("2022.3.33f1")]
	public void Reads_the_engine_version(string version) =>
		Assert.Equal(version, UnityBundleHeader.UnityVersion(SyntheticGame.BundleHeader(version), "data.unity3d"));

	[Fact]
	public void Refuses_a_file_that_is_not_a_unity_bundle()
	{
		var error = Assert.Throws<GameMetadataException>(
			() => UnityBundleHeader.UnityVersion("UnityWeb\05.x.x\06000.3.18f1\0"u8, "data.unity3d"));

		Assert.Equal("data.unity3d isn't a Unity bundle: it doesn't start with UnityFS.", error.Message);
	}

	[Fact]
	public void Refuses_a_header_that_ends_inside_the_version()
	{
		var header = SyntheticGame.BundleHeader("6000.3.18f1").AsSpan(0, 20).ToArray();

		var error = Assert.Throws<GameMetadataException>(() => UnityBundleHeader.UnityVersion(header, "data.unity3d"));

		Assert.Equal("data.unity3d's Unity bundle header ends early, at byte 18.", error.Message);
	}

	[Fact]
	public void Refuses_a_version_that_is_not_a_unity_version()
	{
		byte[] header = [.. Encoding.ASCII.GetBytes("UnityFS\0"), 0, 0, 0, 8, .. Encoding.ASCII.GetBytes("5.x.x\0unity six\0")];

		var error = Assert.Throws<GameMetadataException>(() => UnityBundleHeader.UnityVersion(header, "data.unity3d"));

		Assert.Equal("data.unity3d names Unity version \"unity six\", which isn't a Unity version.", error.Message);
	}
}
