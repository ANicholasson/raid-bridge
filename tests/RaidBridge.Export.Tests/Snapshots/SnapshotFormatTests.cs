using System.Text;
using System.Text.RegularExpressions;
using RaidBridge.Domain.Accounts;
using RaidBridge.Domain.Arena;
using RaidBridge.Domain.Artifacts;
using RaidBridge.Domain.Champions;
using RaidBridge.Domain.Relics;
using RaidBridge.Domain.Snapshots;
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

	/// <summary>A helmet that was ascended, a weapon with a Glyph, and a ring locked to a Faction. 0.06 is the nearest Fixed to it, 257698038 / 2^32.</summary>
	private static readonly Artifact[] KnightWears =
	[
		new(102, "Helmet", "Hp", "Synthetic Life", "Five", "Epic", 12, 2, null, new("Health", 2120, true, 0, 0), new("Defence", 171798692 / 4294967296.0, false, 0, 0), []),
		new(101, "Weapon", "AttackSpeed", "Synthetic Speed", "Six", "Legendary", 16, null, null, new("Attack", 265, true, 0, 0), null, [new("Speed", 12, true, 3, 2), new("CriticalChance", 257698038 / 4294967296.0, false, 0, 1)]),
		new(103, "Ring", "Hp", "Synthetic Life", "Six", "Legendary", 8, null, "DarkElves", new("Health", 1640, true, 0, 0), null, [new("Attack", 22, true, 0, 0)]),
	];

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
				new(3, 5240, "Synthetic Knight", "Legendary", "Void", "DarkElves", "Stars6", 60, "Stars2", "Necromancy", 1, [new(52401, "Synthetic Strike", 4), new(52402, "Synthetic Guard", 3)], [500111, 500112], true, "Favorite", ChampionLocation.Roster, KnightWears, [KnightRelic]),
				new(7, 6010, "Synthetic Archer", "Rare", "Force", "BannerLords", "Stars3", 30, null, null, 0, [new(60101, "Synthetic Volley", 1)], [], false, "None", ChampionLocation.Vault, [], []),
				new(12, 6010, "Synthetic Archer", "Rare", "Force", "BannerLords", "Stars5", 50, "Stars1", null, 0, [new(60101, "Synthetic Volley", 2)], [], false, "None", ChampionLocation.DeepVault, [], []),
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
			new("BronzeIV", 820, 6, 2, 4, [new([3]), new([7, 12]), new([])]));

		var written = Encoding.UTF8.GetString(SnapshotJson.Write(snapshot, new Generator("raid-bridge", "0.1.0")));

		Assert.Equal(DocumentedExample(), written);
	}

	[Fact]
	public void Writes_the_capture_time_in_utc()
	{
		var snapshot = new Snapshot(new DateTimeOffset(2026, 11, 1, 1, 40, 0, TimeSpan.FromHours(2)), AccountIdHash.Of(new AccountId(1), new Salt(new byte[Salt.Length])), [], [], [], [], [], [], [], [], [], NoClassicArena, NoTagArena);

		var written = Encoding.UTF8.GetString(SnapshotJson.Write(snapshot, new Generator("raid-bridge", "0.1.0")));

		Assert.Contains("\"capturedAt\": \"2026-10-31T23:40:00Z\"", written, StringComparison.Ordinal);
	}

	/// <summary>Each object is written even when the Account holds nothing in it, so "none" reads apart from "not yet read".</summary>
	[Fact]
	public void Writes_empty_resources_shards_items_great_hall_and_faction_guardians_as_empty_objects()
	{
		var snapshot = new Snapshot(DateTimeOffset.UnixEpoch, AccountIdHash.Of(new AccountId(1), new Salt(new byte[Salt.Length])), [], [], [], [], [], [], [], [], [], NoClassicArena, NoTagArena);

		var written = Encoding.UTF8.GetString(SnapshotJson.Write(snapshot, new Generator("raid-bridge", "0.1.0")));

		Assert.Contains("\"resources\": {},\n  \"shards\": {},\n  \"items\": {},\n  \"greatHall\": {},\n  \"factionGuardians\": {},\n  \"classicArena\": {", written, StringComparison.Ordinal);
	}

	/// <summary>An Arena with no league and no defence team is written with a null league and empty teams, never left out.</summary>
	[Fact]
	public void Writes_an_arena_with_nothing_set_up()
	{
		var snapshot = new Snapshot(DateTimeOffset.UnixEpoch, AccountIdHash.Of(new AccountId(1), new Salt(new byte[Salt.Length])), [], [], [], [], [], [], [], [], [], NoClassicArena, NoTagArena);

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
		var snapshot = new Snapshot(DateTimeOffset.UnixEpoch, AccountIdHash.Of(new AccountId(1), new Salt(new byte[Salt.Length])), [], [], [], [], [new("Energy", 0.1 + 0.2)], [], [], [], [], NoClassicArena, NoTagArena);

		var written = Encoding.UTF8.GetString(SnapshotJson.Write(snapshot, new Generator("raid-bridge", "0.1.0")));

		Assert.Contains("\"Energy\": 0.30000000000000004", written, StringComparison.Ordinal);
	}

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
