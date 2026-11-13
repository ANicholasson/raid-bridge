namespace RaidBridge.Game.Linux.Tests;

/// <summary>When the Linux adapter's live checks run (https://github.com/ANicholasson/raid-bridge/pull/49).</summary>
public class LiveGameTests
{
	[Fact]
	public void Runs_on_linux_when_opted_in() => Assert.Null(LiveGame.SkipReason("1", onLinux: true));

	[Theory]
	[InlineData(null)]
	[InlineData("0")]
	public void Skips_unless_opted_in(string? optIn) => Assert.StartsWith("Gated:", LiveGame.SkipReason(optIn, onLinux: true), StringComparison.Ordinal);

	[Fact]
	public void Skips_off_linux_even_when_opted_in() =>
		Assert.Equal("The Linux adapter's live checks run on Linux only.", LiveGame.SkipReason("1", onLinux: false));
}
