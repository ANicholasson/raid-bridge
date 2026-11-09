using RaidBridge.Game.Metadata;

namespace RaidBridge.Game.Tests.Metadata;

public class PeProductVersionTests
{
	[Fact]
	public void Reads_the_product_version_string() =>
		Assert.Equal(
			"6000.3.18f1 (5ebeb53e4c07)",
			Read(SyntheticPe.WithProductVersion("6000.3.18f1 (5ebeb53e4c07)")));

	[Fact]
	public void Refuses_a_file_that_is_not_a_pe_file()
	{
		var error = Assert.Throws<GameMetadataException>(() => Read("\u007fELF and the rest of a file that isn't PE"u8.ToArray()));

		Assert.StartsWith("Can't read the product version of UnityPlayer.dll: it isn't a valid PE file", error.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void Refuses_a_file_without_a_version_resource()
	{
		var iconOnly = SyntheticPe.WithStrings(new() { ["ProductVersion"] = "6000.3.18f1" }, resourceType: 3);

		var error = Assert.Throws<GameMetadataException>(() => Read(iconOnly));

		Assert.Equal("Can't read the product version of UnityPlayer.dll: it has no RT_VERSION resource.", error.Message);
	}

	[Fact]
	public void Refuses_a_version_resource_without_a_product_version()
	{
		var fileVersionOnly = SyntheticPe.WithStrings(new() { ["FileVersion"] = "6000.3.18.6209205" });

		var error = Assert.Throws<GameMetadataException>(() => Read(fileVersionOnly));

		Assert.Equal("Can't read the product version of UnityPlayer.dll: its version resource has no ProductVersion.", error.Message);
	}

	[Fact]
	public void Refuses_a_file_cut_short_inside_the_version_resource()
	{
		var file = SyntheticPe.WithProductVersion("6000.3.18f1");

		Assert.Throws<GameMetadataException>(() => Read(file[..^40]));
	}

	private static string Read(byte[] file)
	{
		using var stream = new MemoryStream(file);
		return PeProductVersion.Read(stream, "UnityPlayer.dll");
	}
}
