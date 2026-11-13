namespace RaidBridge.Game.Windows.Tests;

/// <summary>When the Windows adapter's live checks run (https://github.com/ANicholasson/raid-bridge/pull/49).</summary>
public class LiveGameTests
{
	[Fact]
	public void Runs_on_windows_when_opted_in() => Assert.Null(LiveGame.SkipReason("1", onWindows: true));

	[Theory]
	[InlineData(null)]
	[InlineData("0")]
	public void Skips_unless_opted_in(string? optIn) => Assert.StartsWith("Gated:", LiveGame.SkipReason(optIn, onWindows: true), StringComparison.Ordinal);

	[Fact]
	public void Skips_off_windows_even_when_opted_in() =>
		Assert.Equal("The Windows adapter's live checks run on Windows only.", LiveGame.SkipReason("1", onWindows: false));
}
