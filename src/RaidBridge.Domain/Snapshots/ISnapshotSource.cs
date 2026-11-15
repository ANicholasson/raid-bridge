using RaidBridge.Domain.Accounts;

namespace RaidBridge.Domain.Snapshots;

/// <summary>
/// Reads the Account a Snapshot is taken of. Throws, naming the cause, rather than return part of one, apart from an
/// <see cref="OptionalSection"/> it couldn't read, which it returns as null with a <see cref="Problem"/> naming the cause
/// (ADR-0006).
/// </summary>
public interface ISnapshotSource
{
	Account Read();
}
