using RaidBridge.Domain.Accounts;

namespace RaidBridge.Domain.Snapshots;

/// <summary>
/// The one use case: read the Account, hash its id with the player's salt, stamp the moment in UTC, and sort the
/// Champions, the Unworn Artifacts, the Unworn Relics and the Relic stones in no socket by id. The
/// Resources, Shards, Items, the Great Hall, its Area Bonuses, the Faction Guardians and the Missions keep the order the game gives them. The
/// Problems are sorted by section, the order of the Snapshot's keys.
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
			account.Level,
			account.Experience,
			account.Power,
			account.ArtifactCapacity,
			[.. account.Champions.OrderBy(champion => champion.Id)],
			[.. account.UnwornArtifacts.OrderBy(artifact => artifact.Id)],
			[.. account.UnwornRelics.OrderBy(relic => relic.Id)],
			[.. account.UnsocketedRelicStones.OrderBy(stone => stone.Id)],
			account.Resources,
			account.Shards,
			account.Items,
			account.GreatHall,
			account.AreaBonuses,
			account.FactionGuardians,
			account.ClassicArena,
			account.TagArena,
			account.LiveArena,
			account.Missions,
			Problems(account));
	}

	/// <summary>
	/// The source's Problems by section, once they agree with its sections: each Problem names a section it returned as
	/// null, no section has two, and the Area Bonuses and the Missions are null only with one. The Live Arena may be null
	/// without one, since the game holds none for some Accounts. A source that breaks this has a bug, and a Snapshot that
	/// said a section was empty when it failed, or failed when it was read, would hide it (code review, https://github.com/ANicholasson/raid-bridge/pull/58).
	/// </summary>
	private static List<Problem> Problems(Account account)
	{
		var sections = account.Problems.Select(problem => problem.Section).ToList();
		if (sections.Distinct().Count() != sections.Count)
		{
			throw new InvalidOperationException($"The Account's source gave more than one Problem for a section: {string.Join(", ", sections)}.");
		}

		foreach (var (section, isNull, mayBeNull) in new[]
		{
			(OptionalSection.AreaBonuses, account.AreaBonuses is null, false),
			(OptionalSection.LiveArena, account.LiveArena is null, true),
			(OptionalSection.Missions, account.Missions is null, false),
		})
		{
			if (sections.Contains(section) ? !isNull : isNull && !mayBeNull)
			{
				throw new InvalidOperationException(
					isNull
						? $"The Account's source gave {section} as null with no Problem."
						: $"The Account's source gave a Problem for {section} but read it too.");
			}
		}

		return [.. account.Problems.OrderBy(problem => problem.Section)];
	}
}
