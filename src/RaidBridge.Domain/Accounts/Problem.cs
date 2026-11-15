namespace RaidBridge.Domain.Accounts;

/// <summary>
/// A part of the Account raid-bridge couldn't read, which the Snapshot writes as null, and why: the same message naming
/// the type, the field and the address that would otherwise have failed the run
/// (https://github.com/ANicholasson/raid-bridge/issues/47).
/// </summary>
public sealed record Problem(OptionalSection Section, string Message);

/// <summary>
/// The parts of the Account a Snapshot may write as null when they can't be read, because nothing else in the Snapshot
/// is worked out from them (ADR-0006). A part any other value is worked out from, such as a Champion's Total stats, is
/// never one: a failure there still fails the run. They are in the order of their keys in the Snapshot.
/// </summary>
public enum OptionalSection
{
	AreaBonuses,
	LiveArena,
	SavedTeams,
	Missions,
}
