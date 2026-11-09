using RaidBridge.Domain.Accounts;

namespace RaidBridge.Domain.Snapshots;

/// <summary>
/// The one use case: read the Account, hash its id with the player's salt, stamp the moment in UTC, and sort the
/// Champions and the Unworn Artifacts by id. The Resources, Shards, Items, the Great Hall and the Faction Guardians keep
/// the order the game gives them.
/// </summary>
public sealed class TakeSnapshot(ISnapshotSource source, ISaltStore salts, TimeProvider clock)
{
	public Snapshot Run()
	{
		// The salt first, so a broken salt file stops the run before anything reads the Game client.
		var salt = salts.LoadOrCreate();
		var account = source.Read();
		return new(
			clock.GetUtcNow(),
			AccountIdHash.Of(account.Id, salt),
			[.. account.Champions.OrderBy(champion => champion.Id)],
			[.. account.UnwornArtifacts.OrderBy(artifact => artifact.Id)],
			account.Resources,
			account.Shards,
			account.Items,
			account.GreatHall,
			account.FactionGuardians,
			account.ClassicArena,
			account.TagArena);
	}
}
