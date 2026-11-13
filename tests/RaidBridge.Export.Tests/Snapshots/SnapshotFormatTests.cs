using System.Text;
using System.Text.RegularExpressions;
using RaidBridge.Domain.Accounts;
using RaidBridge.Domain.Arena;
using RaidBridge.Domain.Artifacts;
using RaidBridge.Domain.Champions;
using RaidBridge.Domain.Missions;
using RaidBridge.Domain.Relics;
using RaidBridge.Domain.Snapshots;
using RaidBridge.Domain.Stats;
using RaidBridge.Export.Snapshots;

namespace RaidBridge.Export.Tests.Snapshots;

/// <summary>
/// docs/snapshot.md's example is what the writer writes for the synthetic Account, Champions, Artifacts, Resources,
/// Shards, Items, Great Hall, Faction Guardians and Classic and Tag Arena it describes (spec #20, #33, #34, #35, #36,
/// #37).
/// </summary>
public partial class SnapshotFormatTests
{
	private static readonly ClassicArena NoClassicArena = new(null, 0, 0, 0, 0, new([]));
	private static readonly TagArena NoTagArena = new(null, 0, 0, 0, 0, []);
	private static readonly MissionGroups NoMissions = new([], []);

	/// <summary>A helmet that was ascended, a weapon with a Glyph, and a ring locked to a Faction. 0.06 is the nearest Fixed to it, 257698038 / 2^32.</summary>
	private static readonly Artifact[] KnightWears =
	[
		new(102, "Helmet", "Hp", "Synthetic Life", "Five", "Epic", 12, 2, null, new("Health", 2120, true, 0, 0), new("Defence", 171798692 / 4294967296.0, false, 0, 0), []),
		new(101, "Weapon", "AttackSpeed", "Synthetic Speed", "Six", "Legendary", 16, null, null, new("Attack", 265, true, 0, 0), null, [new("Speed", 12, true, 3, 2), new("CriticalChance", 257698038 / 4294967296.0, false, 0, 1)]),
		new(103, "Ring", "Hp", "Synthetic Life", "Six", "Legendary", 8, null, "DarkElves", new("Health", 1640, true, 0, 0), null, [new("Attack", 22, true, 0, 0)]),
	];

	/// <summary>The bonuses of what the knight wears, each with its Glyph added on, as the reader gives them.</summary>
	private static readonly StatBonus[] KnightBonuses =
	[
		new(Stat.Health, 2120, true), new(Stat.Defence, 171798692 / 4294967296.0, false),
		new(Stat.Attack, 265, true), new(Stat.Speed, 12 + 3, true), new(Stat.CriticalChance, 257698038 / 4294967296.0, false),
		new(Stat.Health, 1640, true), new(Stat.Attack, 22, true),
	];

	/// <summary>The knight's helmet and ring make a complete two-piece Life set, made up to give 15% Health, the nearest Fixed being 644245094 / 2^32.</summary>
	private static readonly StatBonus KnightSetBonus = new(Stat.Health, 644245094 / 4294967296.0, false);

	/// <summary>
	/// The knight's two masteries: one made up to add 75 Attack, and one like Lore of Steel, made up to add 15% of its
	/// set bonuses: 15% of the Life set's 15% Health, 2.25%, which raises 12,375 Health to 12,653.4…, rounded to 12,653.
	/// </summary>
	private static readonly MasteryBonuses KnightMasteries = new([new(Stat.Attack, 75, true)], [644245094 / 4294967296.0]);

	/// <summary>The knight's Awakening to Stars2 with its blessing, made up to give 7,500 Health and 750 Attack.</summary>
	private static readonly StatBonus[] KnightAwakening = [new(Stat.Health, 7500, true), new(Stat.Attack, 750, true)];

	/// <summary>The knight's Empowerment to level 1, made up to give 10 Resistance and 10 Accuracy.</summary>
	private static readonly StatBonus[] KnightEmpowerment = [new(Stat.Resistance, 10, true), new(Stat.Accuracy, 10, true)];

	/// <summary>
	/// SilverIII's bonus, which every Champion gets while the Account has a Classic Arena defence team: 10% of Health,
	/// Attack and Defence, the nearest Fixed being 429496730 / 2^32, as the reader gives it, never a plain 0.1
	/// (https://github.com/ANicholasson/raid-bridge/pull/24).
	/// </summary>
	private static readonly StatBonus[] SilverIII =
	[
		new(Stat.Health, 429496730 / 4294967296.0, false), new(Stat.Attack, 429496730 / 4294967296.0, false), new(Stat.Defence, 429496730 / 4294967296.0, false),
	];

	/// <summary>Level 10 of the Void Great Hall's CriticalDamage, made up to give 25%. The knight is Void.</summary>
	private static readonly StatBonus[] KnightGreatHall = [new(Stat.CriticalDamage, 0.25, false)];

	/// <summary>
	/// The archers fill both places of the Banner Lords' first Rare guardian slot, made up to give 10% Health, the nearest
	/// Fixed being 429496730 / 2^32. The knight guards alone, so its slot isn't full and gives nothing.
	/// </summary>
	private static readonly StatBonus[] ArcherGuardians = [new(Stat.Health, 429496730 / 4294967296.0, false)];

	/// <summary>Boots with no Substats, and a cloak locked to a Faction with a Glyph on its Substat.</summary>
	private static readonly Artifact[] WornByNobody =
	[
		new(104, "Boots", "AttackSpeed", "Synthetic Speed", "Six", "Legendary", 16, null, null, new("Speed", 45, true, 0, 0), null, []),
		new(105, "Cloak", "Hp", "Synthetic Life", "Five", "Epic", 4, null, "HighElves", new("Attack", 120, true, 0, 0), null, [new("Speed", 5, true, 2, 1)]),
	];

	/// <summary>A synthetic stone, in a socket or not.</summary>
	private static readonly RelicStone Spark = new(301, 11, "Synthetic Spark", "Rare", "Circle", [new(4000010, "Turn Meter effects are stronger.")]);

	/// <summary>
	/// A grail at level 15, every stat open, CriticalDamage as a fraction; its own skill and the one it adds for a Dark
	/// Elf; a stone in one socket and the other empty. 0.36 is the nearest Fixed to it, 1546188227 / 2^32.
	/// </summary>
	private static readonly Relic KnightRelic = new(
		201,
		1,
		"Synthetic Grail",
		"Epic",
		"Rank5",
		15,
		5,
		[new("Attack", 308, true), new("Health", 5400, true), new("CriticalDamage", 1546188227 / 4294967296.0, false)],
		[new(3000000, "Wearer deals more damage.")],
		[new("DarkElves", 3200000, "Dark Elves strike first.")],
		[new("Circle", Spark), new("Diamond", null)]);

	/// <summary>The grail's bonuses, as the reader gives them to the knight's stats.</summary>
	private static readonly StatBonus[] KnightRelicBonuses = [new(Stat.Attack, 308, true), new(Stat.Health, 5400, true), new(Stat.CriticalDamage, 1546188227 / 4294967296.0, false)];

	/// <summary>A harp at level 0: only its first stat is open, and its one socket is empty.</summary>
	private static readonly Relic UnwornRelic = new(203, 2, "Synthetic Harp", "Legendary", "Rank3", 0, 1, [new("Health", 900, true)], [new(3000001, "Wearer gains Turn Meter.")], [], [new("Pentagon", null)]);

	[Fact]
	public void The_documented_example_is_what_the_writer_writes()
	{
		var salt = new Salt([.. Enumerable.Range(0, Salt.Length).Select(value => (byte)value)]);
		var snapshot = new Snapshot(
			new DateTimeOffset(2026, 10, 31, 21, 40, 0, TimeSpan.Zero),
			AccountIdHash.Of(new AccountId(123456789), salt),
			[
				new(3, 5240, "Synthetic Knight", "Legendary", "Void", "DarkElves", "Stars6", 60, "Stars2", "Necromancy", 1, [new(52401, "Synthetic Strike", 4), new(52402, "Synthetic Guard", 3)], [500111, 500112], true, "Favorite", ChampionLocation.Roster, KnightWears, [KnightRelic], StatsFormula.For(Base(12375, 660, 742, 101, 40, 10, 0.14999999990686774, 0.5), new ChampionBonuses(KnightBonuses, [KnightSetBonus], KnightMasteries, KnightGreatHall, [], KnightAwakening, KnightEmpowerment, KnightRelicBonuses, SilverIII))),
				new(7, 6010, "Synthetic Archer", "Rare", "Force", "BannerLords", "Stars3", 30, null, null, 0, [new(60101, "Synthetic Volley", 1)], [], false, "None", ChampionLocation.Vault, [], [], StatsFormula.For(Base(3780, 396, 216, 98, 30, 0, 0.14999999990686774, 0.5700000000651926), ChampionBonuses.None with { FactionGuardians = ArcherGuardians, ArenaLeague = SilverIII })),
				new(12, 6010, "Synthetic Archer", "Rare", "Force", "BannerLords", "Stars5", 50, "Stars1", null, 0, [new(60101, "Synthetic Volley", 2)], [], false, "None", ChampionLocation.DeepVault, [], [], StatsFormula.For(Base(6300, 660, 360, 98, 30, 0, 0.14999999990686774, 0.5700000000651926), ChampionBonuses.None with { FactionGuardians = ArcherGuardians, ArenaLeague = SilverIII })),
			],
			WornByNobody,
			[UnwornRelic],
			[Spark with { Id = 303 }],
			[new("Energy", 130), new("Silver", 1_250_000), new("Gem", 42)],
			[new("Mystery", 14), new("Ancient", 2)],
			[new("HeroXpBooster100P_1Day", 3), new("SkillUpgradeItemEpic", 1)],
			[new("Magic", [new("Health", 3), new("Attack", 2)]), new("Void", [new("CriticalDamage", 10)])],
			[new("BannerLords", [new("Rare", [new([7, 12])])]), new("DarkElves", [new("Legendary", [new([3])])])],
			new("SilverIII", 1450, 12, 8, 3, new([12, 3, 7])),
			new("BronzeIV", 820, 6, 2, 4, [new([3]), new([7, 12]), new([])]),
			new(
				["Group1"],
				[new("Group2", "ArbiterSection", [
					new(9101, "Heroes", "Completed", "Upgrade 2 Champions to Level 30", null, 2),
					new(9102, "Dungeons", "InProgress", "Clear Stage 10 of a Synthetic Keep 10 times", 4, 10),
					new(9103, "Clan", "New", "Deal 2,000,000 damage to the Synthetic Boss", 0, 2_000_000),
				])]));

		var written = Encoding.UTF8.GetString(SnapshotJson.Write(snapshot, new Generator("raid-bridge", "0.1.0")));

		Assert.Equal(DocumentedExample(), written);
	}

	[Fact]
	public void Writes_the_capture_time_in_utc()
	{
		var snapshot = new Snapshot(new DateTimeOffset(2026, 11, 1, 1, 40, 0, TimeSpan.FromHours(2)), AccountIdHash.Of(new AccountId(1), new Salt(new byte[Salt.Length])), [], [], [], [], [], [], [], [], [], NoClassicArena, NoTagArena, NoMissions);

		var written = Encoding.UTF8.GetString(SnapshotJson.Write(snapshot, new Generator("raid-bridge", "0.1.0")));

		Assert.Contains("\"capturedAt\": \"2026-10-31T23:40:00Z\"", written, StringComparison.Ordinal);
	}

	/// <summary>Each object is written even when the Account holds nothing in it, so "none" reads apart from "not yet read".</summary>
	[Fact]
	public void Writes_empty_resources_shards_items_great_hall_and_faction_guardians_as_empty_objects()
	{
		var snapshot = new Snapshot(DateTimeOffset.UnixEpoch, AccountIdHash.Of(new AccountId(1), new Salt(new byte[Salt.Length])), [], [], [], [], [], [], [], [], [], NoClassicArena, NoTagArena, NoMissions);

		var written = Encoding.UTF8.GetString(SnapshotJson.Write(snapshot, new Generator("raid-bridge", "0.1.0")));

		Assert.Contains("\"resources\": {},\n  \"shards\": {},\n  \"items\": {},\n  \"greatHall\": {},\n  \"factionGuardians\": {},\n  \"classicArena\": {", written, StringComparison.Ordinal);
	}

	/// <summary>
	/// An Arena with no league and no defence team is written with a null league and empty teams, never left out, and so
	/// are Missions with no group open or finished.
	/// </summary>
	[Fact]
	public void Writes_an_arena_and_missions_with_nothing_set_up()
	{
		var snapshot = new Snapshot(DateTimeOffset.UnixEpoch, AccountIdHash.Of(new AccountId(1), new Salt(new byte[Salt.Length])), [], [], [], [], [], [], [], [], [], NoClassicArena, NoTagArena, NoMissions);

		var written = Encoding.UTF8.GetString(SnapshotJson.Write(snapshot, new Generator("raid-bridge", "0.1.0")));

		Assert.EndsWith(
			"""
			  "classicArena": {
			    "league": null,
			    "points": 0,
			    "battles": 0,
			    "wins": 0,
			    "losses": 0,
			    "defenceTeam": []
			  },
			  "tagArena": {
			    "league": null,
			    "points": 0,
			    "battles": 0,
			    "wins": 0,
			    "losses": 0,
			    "defenceTeams": []
			  },
			  "missions": {
			    "completedGroups": [],
			    "openGroups": []
			  }
			}

			""".ReplaceLineEndings("\n"),
			written,
			StringComparison.Ordinal);
	}

	/// <summary>An amount the game holds as a fraction is written as held, never rounded.</summary>
	[Fact]
	public void Writes_a_resource_amount_exactly_as_the_game_holds_it()
	{
		var snapshot = new Snapshot(DateTimeOffset.UnixEpoch, AccountIdHash.Of(new AccountId(1), new Salt(new byte[Salt.Length])), [], [], [], [], [new("Energy", 0.1 + 0.2)], [], [], [], [], NoClassicArena, NoTagArena, NoMissions);

		var written = Encoding.UTF8.GetString(SnapshotJson.Write(snapshot, new Generator("raid-bridge", "0.1.0")));

		Assert.Contains("\"Energy\": 0.30000000000000004", written, StringComparison.Ordinal);
	}

	/// <summary>Base stats in the game's order of stats, as the Game client's own arithmetic gives them for the made-up scaling of the example.</summary>
	private static Dictionary<Stat, double> Base(double health, double attack, double defence, double speed, double resistance, double accuracy, double criticalChance, double criticalDamage) =>
		new()
		{
			[Stat.Health] = health,
			[Stat.Attack] = attack,
			[Stat.Defence] = defence,
			[Stat.Speed] = speed,
			[Stat.Resistance] = resistance,
			[Stat.Accuracy] = accuracy,
			[Stat.CriticalChance] = criticalChance,
			[Stat.CriticalDamage] = criticalDamage,
		};

	private static string DocumentedExample()
	{
		var document = File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "snapshot.md")).ReplaceLineEndings("\n");
		return JsonBlock().Match(document) is { Success: true } match
			? match.Groups["json"].Value
			: throw new InvalidOperationException("docs/snapshot.md has no ```json example.");
	}

	private static string RepositoryRoot()
	{
		for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
		{
			if (File.Exists(Path.Combine(directory.FullName, "RaidBridge.slnx")))
			{
				return directory.FullName;
			}
		}

		throw new InvalidOperationException($"No RaidBridge.slnx above {AppContext.BaseDirectory}.");
	}

	[GeneratedRegex(@"```json\n(?<json>.*?\n)```", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
	private static partial Regex JsonBlock();
}
