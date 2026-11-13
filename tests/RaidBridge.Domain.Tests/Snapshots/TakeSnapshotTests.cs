using RaidBridge.Domain.Accounts;
using RaidBridge.Domain.Arena;
using RaidBridge.Domain.Artifacts;
using RaidBridge.Domain.Champions;
using RaidBridge.Domain.Missions;
using RaidBridge.Domain.Relics;
using RaidBridge.Domain.Snapshots;
using RaidBridge.Domain.Stats;

namespace RaidBridge.Domain.Tests.Snapshots;

public class TakeSnapshotTests
{
	private static readonly DateTimeOffset Now = new(2026, 10, 31, 21, 40, 0, TimeSpan.Zero);
	private static readonly ClassicArena NoClassicArena = new(null, 0, 0, 0, 0, new([]));
	private static readonly TagArena NoTagArena = new(null, 0, 0, 0, 0, []);
	private static readonly MissionGroups NoMissions = new([], []);

	[Fact]
	public void Hashes_the_account_id_with_the_salt_and_stamps_the_time()
	{
		var salt = new Salt(new byte[Salt.Length]);

		var snapshot = new TakeSnapshot(new Source(new Account(new AccountId(7), [], [], [], [], [], [], [], [], [], NoClassicArena, NoTagArena, NoMissions)), new Salts(salt), new Clock(Now)).Run();

		Assert.Equivalent(new Snapshot(Now, AccountIdHash.Of(new AccountId(7), salt), [], [], [], [], [], [], [], [], [], NoClassicArena, NoTagArena, NoMissions), snapshot, strict: true);
	}

	[Fact]
	public void Sorts_the_champions_by_id()
	{
		var account = new Account(new AccountId(7), [Champion(12), Champion(3), Champion(7)], [], [], [], [], [], [], [], [], NoClassicArena, NoTagArena, NoMissions);

		var snapshot = new TakeSnapshot(new Source(account), new Salts(new Salt(new byte[Salt.Length])), new Clock(Now)).Run();

		Assert.Equal([3, 7, 12], snapshot.Champions.Select(champion => champion.Id));
	}

	[Fact]
	public void Sorts_the_unworn_artifacts_by_id()
	{
		var account = new Account(new AccountId(7), [], [Artifact(105), Artifact(101), Artifact(104)], [], [], [], [], [], [], [], NoClassicArena, NoTagArena, NoMissions);

		var snapshot = new TakeSnapshot(new Source(account), new Salts(new Salt(new byte[Salt.Length])), new Clock(Now)).Run();

		Assert.Equal([101, 104, 105], snapshot.UnwornArtifacts.Select(artifact => artifact.Id));
	}

	/// <summary>The reader sorts them too, so this pins the sort the use case promises (https://github.com/ANicholasson/raid-bridge/pull/17).</summary>
	[Fact]
	public void Sorts_the_unworn_relics_and_unsocketed_stones_by_id()
	{
		var account = new Account(new AccountId(7), [], [], [Relic(203), Relic(201)], [Stone(305), Stone(303)], [], [], [], [], [], NoClassicArena, NoTagArena, NoMissions);

		var snapshot = new TakeSnapshot(new Source(account), new Salts(new Salt(new byte[Salt.Length])), new Clock(Now)).Run();

		Assert.Equal([201, 203], snapshot.UnwornRelics.Select(relic => relic.Id));
		Assert.Equal([303, 305], snapshot.UnsocketedRelicStones.Select(stone => stone.Id));
	}

	/// <summary>The source gives them in the order of the game's enums, which no sort by name would keep.</summary>
	[Fact]
	public void Keeps_all_but_the_champions_and_unworn_artifacts_in_the_order_the_game_gives_them()
	{
		var account = new Account(
			new AccountId(7),
			[],
			[],
			[],
			[],
			[new("Silver", 1_250_000), new("Energy", 130)],
			[new("Mystery", 14), new("Ancient", 2)],
			[new("SkillUpgradeItemEpic", 1), new("HeroXpBooster100P_1Day", 3)],
			[new("Magic", [new("Defence", 4), new("Attack", 2)]), new("Force", [new("Health", 1)])],
			[new("DarkElves", [new("Legendary", [new([12, 3])])]), new("BannerLords", [new("Rare", [new([7])])])],
			new("SilverIII", 1450, 12, 8, 3, new([12, 3, 7])),
			new("BronzeIV", 820, 6, 2, 4, [new([12]), new([7, 3])]),
			new(["Group1"], [new("Group2", "ArbiterSection", [new(9102, "Dungeons", "InProgress", "Clear a stage", 4, 10), new(9101, "Heroes", "Completed", "Upgrade a Champion", null, 2)])]));

		var snapshot = new TakeSnapshot(new Source(account), new Salts(new Salt(new byte[Salt.Length])), new Clock(Now)).Run();

		Assert.Equal(account.Resources, snapshot.Resources);
		Assert.Equal(account.Shards, snapshot.Shards);
		Assert.Equal(account.Items, snapshot.Items);
		Assert.Equal(account.GreatHall, snapshot.GreatHall);
		Assert.Equal(account.FactionGuardians, snapshot.FactionGuardians);
		Assert.Equal(account.ClassicArena, snapshot.ClassicArena);
		Assert.Equal(account.TagArena, snapshot.TagArena);
		Assert.Equal(account.Missions, snapshot.Missions);
	}

	[Fact]
	public void Loads_the_salt_before_reading_the_game()
	{
		var source = new Source(new Account(new AccountId(7), [], [], [], [], [], [], [], [], [], NoClassicArena, NoTagArena, NoMissions));
		var salts = new BrokenSalts();

		Assert.Throws<InvalidDataException>(() => new TakeSnapshot(source, salts, new Clock(Now)).Run());
		Assert.False(source.WasRead);
	}

	private static Champion Champion(int id) =>
		new(id, 5240, "Synthetic Knight", "Legendary", "Void", "DarkElves", "Stars6", 60, null, null, 0, [], [], false, "None", ChampionLocation.Roster, [], [], StatsFormula.For(new Dictionary<Stat, double>(), ChampionBonuses.None));

	private static Artifact Artifact(int id) =>
		new(id, "Boots", "AttackSpeed", "Synthetic Speed", "Six", "Legendary", 16, null, null, new("Speed", 45, true, 0, 0), null, []);

	private static Relic Relic(int id) => new(id, 1, "Synthetic Grail", "Rare", "Rank1", 0, 1, [], [], [], []);

	private static RelicStone Stone(int id) => new(id, 11, "Synthetic Spark", "Rare", "Circle", []);

	private sealed class Source(Account account) : ISnapshotSource
	{
		public bool WasRead { get; private set; }

		public Account Read()
		{
			WasRead = true;
			return account;
		}
	}

	private sealed class Salts(Salt salt) : ISaltStore
	{
		public Salt LoadOrCreate() => salt;
	}

	private sealed class BrokenSalts : ISaltStore
	{
		public Salt LoadOrCreate() => throw new InvalidDataException("broken salt");
	}

	private sealed class Clock(DateTimeOffset now) : TimeProvider
	{
		public override DateTimeOffset GetUtcNow() => now;
	}
}
