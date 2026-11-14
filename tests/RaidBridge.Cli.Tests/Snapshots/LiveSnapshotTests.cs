using System.Text.Json;
using RaidBridge.Cli.Snapshots;
using RaidBridge.Export.Accounts;

namespace RaidBridge.Cli.Tests.Snapshots;

/// <summary>
/// Gated: <c>raid-bridge snapshot</c>, wired as Program wires it, against the running Game client (spec #20), through
/// whichever memory adapter this platform has. The salt and the Snapshot go to throwaway folders, so the run leaves the
/// player's own salt alone. Opt in with RAIDBRIDGE_LIVE=1, and only ever with one of the maintainer's own Accounts
/// logged in (#12, #39); opting in without the Game client is a failure, not a skip.
/// </summary>
public sealed class LiveSnapshotTests : IDisposable
{
	private static readonly string[] Affinities = ["Magic", "Force", "Spirit", "Void"];
	private static readonly string[] MissionStates = ["New", "InProgress", "Completed"];

	private readonly string _workingDirectory = Directory.CreateTempSubdirectory("raid-bridge-live-").FullName;
	private readonly string _home = Directory.CreateTempSubdirectory("raid-bridge-live-home-").FullName;

	[Fact]
	public void Reads_the_account_id_from_the_running_game_client()
	{
		using var json = Snapshot();

		Assert.Matches("^hmac-sha256:[0-9a-f]{64}$", json.RootElement.GetProperty("account").GetProperty("idHash").GetString());
	}

	/// <summary>
	/// The Account's level is 1 or more, and an Account that has Champions has power, so the check can't pass by
	/// reading zeros (https://github.com/ANicholasson/raid-bridge/issues/27).
	/// </summary>
	[Fact]
	public void Reads_the_account_level_and_power()
	{
		using var json = Snapshot();

		var account = json.RootElement.GetProperty("account");
		Assert.InRange(account.GetProperty("level").GetInt32(), 1, int.MaxValue);
		Assert.InRange(account.GetProperty("experience").GetInt32(), 0, int.MaxValue);
		Assert.True(account.GetProperty("power").GetDouble() > 0);
	}

	/// <summary>
	/// Every Champion has its Artifacts filed by slot, and the secondary Account's Champions wear at least one, so the
	/// check can't pass by reading none (#26).
	/// </summary>
	[Fact]
	public void Reads_the_artifacts_the_champions_wear()
	{
		using var json = Snapshot();

		var artifacts = json.RootElement.GetProperty("champions").EnumerateArray()
			.SelectMany(champion => champion.GetProperty("artifacts").EnumerateObject())
			.ToList();

		Assert.NotEmpty(artifacts);
		Assert.All(artifacts, slot => Assert.Equal(slot.Name, slot.Value.GetProperty("slot").GetString()));
	}

	/// <summary>
	/// The Artifacts no Champion wears are written, sorted by id, none of them also worn. The secondary Account has
	/// some, so the check can't pass by reading none (#33).
	/// </summary>
	[Fact]
	public void Reads_the_artifacts_no_champion_wears()
	{
		using var json = Snapshot();

		var worn = json.RootElement.GetProperty("champions").EnumerateArray()
			.SelectMany(champion => champion.GetProperty("artifacts").EnumerateObject())
			.Select(slot => slot.Value.GetProperty("id").GetInt32());
		var unworn = json.RootElement.GetProperty("unwornArtifacts").EnumerateArray()
			.Select(artifact => artifact.GetProperty("id").GetInt32())
			.ToList();

		Assert.NotEmpty(unworn);
		Assert.Equal(unworn.Order(), unworn);
		Assert.Empty(unworn.Intersect(worn));
	}

	/// <summary>
	/// The Resources, Shards and Items are written as objects of counts, none negative. Every Account holds silver and
	/// energy, and the secondary Account holds a Shard and Items, so the check can't pass by reading none of any
	/// (#34; PR #43).
	/// </summary>
	[Fact]
	public void Reads_the_resources_shards_and_items()
	{
		using var json = Snapshot();

		var resources = json.RootElement.GetProperty("resources").EnumerateObject().ToDictionary(resource => resource.Name, resource => resource.Value.GetDouble());
		var shards = json.RootElement.GetProperty("shards").EnumerateObject().Select(shard => shard.Value.GetInt32()).ToList();
		var items = json.RootElement.GetProperty("items").EnumerateObject().Select(item => item.Value.GetInt32()).ToList();

		Assert.Contains("Silver", resources.Keys);
		Assert.Contains("Energy", resources.Keys);
		Assert.NotEmpty(shards);
		Assert.NotEmpty(items);
		Assert.All(resources.Values, amount => Assert.True(amount >= 0, $"{amount} is negative."));
		Assert.All(shards.Concat(items), count => Assert.True(count >= 0, $"{count} is negative."));
	}

	/// <summary>
	/// The Great Hall is written as an object of each Affinity's levels, none below 1; the reader holds each to the
	/// highest the game's static data offers. The secondary Account has bought no level yet (a level costs Arena medals
	/// it doesn't have, 2026-11-01), so it reads as {}, which matches its Great Hall screen. On the main Account the
	/// levels read matched its Great Hall screen (#35, PR #44).
	/// </summary>
	[Fact]
	public void Reads_the_great_hall()
	{
		using var json = Snapshot();

		var written = json.RootElement.GetProperty("greatHall");
		var greatHall = written.EnumerateObject().ToList();
		var levels = greatHall.SelectMany(affinity => affinity.Value.EnumerateObject()).Select(level => level.Value.GetInt32()).ToList();

		Assert.Equal(JsonValueKind.Object, written.ValueKind);
		Assert.All(greatHall, affinity => Assert.NotEmpty(affinity.Value.EnumerateObject()));
		Assert.All(greatHall, affinity => Assert.Contains(affinity.Name, Affinities));
		Assert.All(levels, level => Assert.True(level >= 1, $"{level} is below 1."));
	}

	/// <summary>
	/// The Area Bonuses are written as each area's levels by stat, each from 1, an area with none left out
	/// (https://github.com/ANicholasson/raid-bridge/issues/32). It accepts {}: the main Account had bought no level and
	/// held no Crests to buy one with (2026-11-13), as the player confirmed; a bought level is proven only by the synthetic Game client so far.
	/// </summary>
	[Fact]
	public void Reads_the_area_bonuses()
	{
		using var json = Snapshot();

		var written = json.RootElement.GetProperty("areaBonuses");
		var areas = written.EnumerateObject().ToList();

		Assert.Equal(JsonValueKind.Object, written.ValueKind);
		Assert.All(areas, area => Assert.NotEmpty(area.Value.EnumerateObject()));
		Assert.All(areas.SelectMany(area => area.Value.EnumerateObject()), level => Assert.True(level.Value.GetInt32() >= 1, $"{level} is below 1."));
	}

	/// <summary>
	/// The Faction Guardians are written as each Faction's slots by rarity, every slot one or two Champions of
	/// <c>champions</c>, of that Faction and rarity, none guarding twice. It accepts {}, because the secondary Account,
	/// one of the two live runs use, has placed none. On the main Account, with Guardians placed in five Factions (2026-11-01),
	/// every assertion held, and so did one that the slots aren't empty (#36; PR #45).
	/// </summary>
	[Fact]
	public void Reads_the_faction_guardians()
	{
		using var json = Snapshot();

		var champions = json.RootElement.GetProperty("champions").EnumerateArray()
			.ToDictionary(champion => champion.GetProperty("id").GetInt32(), champion => (Faction: champion.GetProperty("faction").GetString(), Rarity: champion.GetProperty("rarity").GetString()));
		var guardians = json.RootElement.GetProperty("factionGuardians").EnumerateObject()
			.SelectMany(faction => faction.Value.EnumerateObject().SelectMany(rarity => rarity.Value.EnumerateArray().Select(slot => (
				Faction: faction.Name,
				Rarity: rarity.Name,
				ChampionIds: slot.EnumerateArray().Select(id => id.GetInt32()).ToList()))))
			.ToList();
		var championIds = guardians.SelectMany(slot => slot.ChampionIds).ToList();

		Assert.Equal(JsonValueKind.Object, json.RootElement.GetProperty("factionGuardians").ValueKind);
		Assert.All(guardians, slot => Assert.InRange(slot.ChampionIds.Count, 1, 2));
		Assert.All(guardians, slot => Assert.All(slot.ChampionIds, id => Assert.Equal((slot.Faction, slot.Rarity), champions[id])));
		Assert.Equal(championIds.Distinct(), championIds);
	}

	/// <summary>
	/// Both Arenas are written with a league the game names or null, counts of 0 or more, and defence teams of
	/// Champions in <c>champions</c>: one team of up to four in the Classic Arena, and up to three in the Tag Arena.
	/// Proven on the main Account (2026-11-01): each Arena read the league and points the player's screens showed, the
	/// Classic Arena its four defenders in the order the game's Defense screen shows them, and the Tag Arena three teams
	/// of four (#37).
	/// </summary>
	[Fact]
	public void Reads_the_classic_and_tag_arena()
	{
		using var json = Snapshot();

		var champions = json.RootElement.GetProperty("champions").EnumerateArray().Select(champion => champion.GetProperty("id").GetInt32()).ToHashSet();
		var classic = json.RootElement.GetProperty("classicArena");
		var tag = json.RootElement.GetProperty("tagArena");
		var teams = tag.GetProperty("defenceTeams").EnumerateArray().Select(Team).Prepend(Team(classic.GetProperty("defenceTeam"))).ToList();

		Assert.All([classic, tag], arena =>
		{
			Assert.Contains(arena.GetProperty("league").ValueKind, new[] { JsonValueKind.String, JsonValueKind.Null });
			Assert.All(["battles", "wins", "losses"], count => Assert.True(arena.GetProperty(count).GetInt32() >= 0, $"{count} is below 0."));
		});
		Assert.InRange(tag.GetProperty("defenceTeams").GetArrayLength(), 0, 3);
		Assert.All(teams, team => Assert.InRange(team.Count, 0, 4));
		Assert.All(teams, team => Assert.All(team, id => Assert.Contains(id, champions)));

		static List<int> Team(JsonElement team) => [.. team.EnumerateArray().Select(id => id.GetInt32())];
	}

	/// <summary>
	/// The Live Arena is written with a league, which the game always works out, and points and counts of 0 or more, or
	/// as null when the game holds no Live Arena data, as an Account below its unlock level may
	/// (https://github.com/ANicholasson/raid-bridge/issues/32; PR #44). On the main Account, which had never fought in it
	/// (2026-11-13), as the player confirmed, it read BronzeI with 900 points, 0 wins and 0 losses; a league above BronzeI is proven only by the synthetic Game client so far.
	/// </summary>
	[Fact]
	public void Reads_the_live_arena()
	{
		using var json = Snapshot();

		var live = json.RootElement.GetProperty("liveArena");
		if (live.ValueKind == JsonValueKind.Null)
		{
			return;
		}

		Assert.False(string.IsNullOrEmpty(live.GetProperty("league").GetString()));
		Assert.All(["points", "wins", "losses"], count => Assert.True(live.GetProperty(count).GetInt64() >= 0, $"{count} is below 0."));
	}

	/// <summary>
	/// Every Relic is written once, the unworn ones and the stones in no socket sorted by id, every bonus a stat the
	/// Relic's level has opened, and every stone in a socket of its own shape, in that socket only. It accepts an Account
	/// with no Relics. On the main Account (2026-11-10), which wears one Relic and holds another and 19 stones, every
	/// assertion held, and the worn Unholy Grail's Attack +104 and the unworn Harp of Kasbiel's Health +900 matched its
	/// Relic screens (https://github.com/ANicholasson/raid-bridge/issues/8).
	/// </summary>
	[Fact]
	public void Reads_the_relics_and_relic_stones()
	{
		using var json = Snapshot();

		var worn = json.RootElement.GetProperty("champions").EnumerateArray().SelectMany(champion => champion.GetProperty("relics").EnumerateArray()).ToList();
		var unworn = json.RootElement.GetProperty("unwornRelics").EnumerateArray().ToList();
		var relics = worn.Concat(unworn).ToList();
		var sockets = relics.SelectMany(relic => relic.GetProperty("sockets").EnumerateArray()).ToList();
		var socketed = sockets.Where(socket => socket.GetProperty("stone").ValueKind == JsonValueKind.Object).Select(socket => socket.GetProperty("stone")).ToList();
		var unsocketed = json.RootElement.GetProperty("unsocketedRelicStones").EnumerateArray().Select(stone => stone.GetProperty("id").GetInt32()).ToList();
		var unwornIds = unworn.Select(relic => relic.GetProperty("id").GetInt32()).ToList();
		var stoneIds = socketed.Select(stone => stone.GetProperty("id").GetInt32()).Concat(unsocketed).ToList();

		Assert.Equal(relics.Count, relics.Select(relic => relic.GetProperty("id").GetInt32()).Distinct().Count());
		Assert.Equal(unwornIds.Order(), unwornIds);
		Assert.Equal(unsocketed.Order(), unsocketed);
		Assert.Equal(stoneIds.Count, stoneIds.Distinct().Count());
		Assert.All(relics, relic => Assert.InRange(relic.GetProperty("bonuses").GetArrayLength(), 1, 3));
		Assert.All(sockets.Where(socket => socket.GetProperty("stone").ValueKind == JsonValueKind.Object), socket =>
			Assert.Equal(socket.GetProperty("shape").GetString(), socket.GetProperty("stone").GetProperty("shape").GetString()));
		Assert.All(relics.SelectMany(relic => relic.GetProperty("skills").EnumerateArray()), skill => Assert.False(string.IsNullOrEmpty(skill.GetProperty("description").GetString())));
	}

	/// <summary>
	/// No skill is written with the game's placeholder text for a name, as the champion screen's hidden skills were
	/// (https://github.com/ANicholasson/raid-bridge/issues/26). On the main Account (2026-11-13) the run before the fix wrote Dorothy Gale's 107305 and
	/// Lightward Siendra's 96605 so, and the run after it none.
	/// </summary>
	[Fact]
	public void Writes_no_skill_with_a_placeholder_name()
	{
		using var json = Snapshot();

		var skills = json.RootElement.GetProperty("champions").EnumerateArray().SelectMany(champion => champion.GetProperty("skills").EnumerateArray()).ToList();

		Assert.NotEmpty(skills);
		Assert.All(skills, skill => Assert.DoesNotMatch(@"^Skill \d+ name$", skill.GetProperty("name").GetString()));
	}

	/// <summary>
	/// The Missions are written as finished groups and open groups, each open group in a section, each Mission with
	/// text from the localization tables, a state the game names, and a count of 1 or more required, with a count
	/// collected of 0 or more or null where the game holds none. On the main Account (2026-11-13), on Arbiter's Group4
	/// with Group1 to Group3 finished, the run wrote all 60 of its Missions
	/// (https://github.com/ANicholasson/raid-bridge/issues/33).
	/// </summary>
	[Fact]
	public void Reads_the_missions()
	{
		using var json = Snapshot();

		var missions = json.RootElement.GetProperty("missions");
		var open = missions.GetProperty("openGroups").EnumerateArray().ToList();
		var all = open.SelectMany(group => group.GetProperty("missions").EnumerateArray()).ToList();

		Assert.All(missions.GetProperty("completedGroups").EnumerateArray(), group => Assert.Matches("^Group[0-9]+$", group.GetString()));
		Assert.All(open, group => Assert.Matches("^Group[0-9]+$", group.GetProperty("group").GetString()));
		Assert.All(open, group => Assert.False(string.IsNullOrEmpty(group.GetProperty("section").GetString())));
		Assert.Equal(all.Count, all.Select(mission => mission.GetProperty("prototypeId").GetInt32()).Distinct().Count());
		Assert.All(all, mission =>
		{
			Assert.Contains(mission.GetProperty("state").GetString(), MissionStates);
			Assert.False(string.IsNullOrEmpty(mission.GetProperty("name").GetString()));
			Assert.True(mission.GetProperty("required").GetInt64() >= 1, "required is below 1.");
			var collected = mission.GetProperty("collected");
			Assert.True(collected.ValueKind == JsonValueKind.Null || collected.GetInt64() >= 0, "collected is below 0.");
		});
	}

	public void Dispose()
	{
		Directory.Delete(_workingDirectory, recursive: true);
		Directory.Delete(_home, recursive: true);
	}

	/// <summary>A Snapshot of the running Game client, written as the command line writes it.</summary>
	private JsonDocument Snapshot()
	{
		Assert.SkipUnless(Environment.GetEnvironmentVariable("RAIDBRIDGE_LIVE") == "1", "Gated: set RAIDBRIDGE_LIVE=1, with the Game client running on one of the maintainer's Accounts, to run.");
		using var output = new StringWriter();
		using var error = new StringWriter();

		var exitCode = SnapshotCommand.Run(["snapshot"], Program.OpenGame, SaltFile.InConfigDirectory(null, _home), TimeProvider.System, output, error, _workingDirectory);

		Assert.Equal((0, string.Empty), (exitCode, error.ToString()));
		return JsonDocument.Parse(File.ReadAllBytes(output.ToString().Trim()));
	}
}
