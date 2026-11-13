using RaidBridge.Game.Windows.Memory;
using Xunit.Sdk;

namespace RaidBridge.Game.Windows.Tests;

/// <summary>
/// The opt-in for checks that read the running Game client (spec #20): RAIDBRIDGE_LIVE=1, and only ever with one of
/// the maintainer's own Accounts logged in (#12, #39). Opting in without the Game client is a failure, not a skip, so
/// a gated run never passes by reading nothing. On another platform the checks skip, so an opted-in run there reads
/// that platform's own adapter (https://github.com/ANicholasson/raid-bridge/pull/49).
/// </summary>
internal static class LiveGame
{
	public static WindowsGameMemory Open() =>
		SkipReason(Environment.GetEnvironmentVariable("RAIDBRIDGE_LIVE"), OperatingSystem.IsWindows()) is { } reason ? throw SkipException.ForSkip(reason)
		: OperatingSystem.IsWindows() ? WindowsGameMemory.Open()
		: throw new InvalidOperationException("Unreachable: off Windows the checks skip.");

	/// <summary>Why the live checks skip, or null when they run.</summary>
	internal static string? SkipReason(string? optIn, bool onWindows) =>
		optIn != "1" ? "Gated: set RAIDBRIDGE_LIVE=1, with the Game client running on one of the maintainer's Accounts, to run."
		: !onWindows ? "The Windows adapter's live checks run on Windows only."
		: null;
}
