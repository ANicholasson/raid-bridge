using RaidBridge.Game.Metadata;

namespace RaidBridge.Game.Tests.Metadata;

public class MetadataHeaderTests
{
	[Theory]
	[InlineData(39)]
	[InlineData(31)]
	public void Reads_the_declared_version(int version) =>
		Assert.Equal(version, MetadataHeader.Version(SyntheticGame.MetadataHeader(version), "global-metadata.dat"));

	[Fact]
	public void Refuses_a_file_that_is_not_metadata()
	{
		var error = Assert.Throws<GameMetadataException>(() => MetadataHeader.Version("UnityFS\0"u8, "global-metadata.dat"));

		Assert.Equal("global-metadata.dat isn't IL2CPP metadata: it doesn't start with 0xFAB11BAF.", error.Message);
	}

	[Fact]
	public void Refuses_a_header_cut_short() =>
		Assert.Throws<GameMetadataException>(() => MetadataHeader.Version([0xAF, 0x1B, 0xB1, 0xFA], "global-metadata.dat"));
}
