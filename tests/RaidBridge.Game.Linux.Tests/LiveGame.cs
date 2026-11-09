using RaidBridge.Game.Linux.Memory;

namespace RaidBridge.Game.Linux.Tests;

/// <summary>
/// The opt-in for checks that read the running Game client (spec #20): RAIDBRIDGE_LIVE=1, and only ever with one of
/// the maintainer's own Accounts logged in, the secondary or, since 2026-11-01, the main (#12, #39). Opting in without
/// the Game client is a failure, not a skip, so a gated run never passes by reading nothing.
/// </summary>
internal static class LiveGame
{
	public static void RequireOptIn() =>
		Assert.SkipUnless(Environment.GetEnvironmentVariable("RAIDBRIDGE_LIVE") == "1", "Gated: set RAIDBRIDGE_LIVE=1, with the Game client running on one of the maintainer's Accounts, to run.");

	public static LinuxGameMemory Open()
	{
		RequireOptIn();
		return OperatingSystem.IsLinux()
			? LinuxGameMemory.Open()
			: throw new PlatformNotSupportedException("The Linux adapter's live checks run on Linux only.");
	}
}
