using RaidBridge.Domain.Accounts;

namespace RaidBridge.Domain.Snapshots;

/// <summary>Reads the Account a Snapshot is taken of. Throws, naming the cause, rather than return part of one.</summary>
public interface ISnapshotSource
{
	Account Read();
}
