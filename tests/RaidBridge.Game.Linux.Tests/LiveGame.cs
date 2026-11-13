using RaidBridge.Game.Linux.Memory;
using Xunit.Sdk;

namespace RaidBridge.Game.Linux.Tests;

/// <summary>
/// The opt-in for checks that read the running Game client (spec #20): RAIDBRIDGE_LIVE=1, and only ever with one of
/// the maintainer's own Accounts logged in, the secondary or, since 2026-11-01, the main (#12, #39). Opting in without
/// the Game client is a failure, not a skip, so a gated run never passes by reading nothing. On another platform the
/// checks skip, so an opted-in run there reads that platform's own adapter
/// (https://github.com/ANicholasson/raid-bridge/pull/49).
/// </summary>
internal static class LiveGame
{
	public static LinuxGameMemory Open() =>
		SkipReason(Environment.GetEnvironmentVariable("RAIDBRIDGE_LIVE"), OperatingSystem.IsLinux()) is { } reason ? throw SkipException.ForSkip(reason)
		: OperatingSystem.IsLinux() ? LinuxGameMemory.Open()
		: throw new InvalidOperationException("Unreachable: off Linux the checks skip.");

	/// <summary>Why the live checks skip, or null when they run.</summary>
	internal static string? SkipReason(string? optIn, bool onLinux) =>
		optIn != "1" ? "Gated: set RAIDBRIDGE_LIVE=1, with the Game client running on one of the maintainer's Accounts, to run."
		: !onLinux ? "The Linux adapter's live checks run on Linux only."
		: null;
}
