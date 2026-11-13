using RaidBridge.Game.Windows.Memory;

namespace RaidBridge.Game.Windows.Tests;

/// <summary>
/// The opt-in for checks that read the running Game client (spec #20): RAIDBRIDGE_LIVE=1, and only ever with one of
/// the maintainer's own Accounts logged in (#12, #39). Opting in without the Game client is a failure, not a skip, so
/// a gated run never passes by reading nothing.
/// </summary>
internal static class LiveGame
{
	public static WindowsGameMemory Open()
	{
		Assert.SkipUnless(Environment.GetEnvironmentVariable("RAIDBRIDGE_LIVE") == "1", "Gated: set RAIDBRIDGE_LIVE=1, with the Game client running on one of the maintainer's Accounts, to run.");
		return OperatingSystem.IsWindows()
			? WindowsGameMemory.Open()
			: throw new PlatformNotSupportedException("The Windows adapter's live checks run on Windows only.");
	}
}
