using RaidBridge.Domain.SavedTeams;
using RaidBridge.Game.Testing;
using RaidBridge.Game.Testing.Champions;
using RaidBridge.Game.Testing.SavedTeams;

namespace RaidBridge.Game.Tests.SavedTeams;

/// <summary>
/// The Account's Saved teams, read end to end through the fake memory port: a synthetic Game client in, each team's
/// purpose under the game's own name, whether it was named, and its Champions, or a named refusal, out
/// (https://github.com/ANicholasson/raid-bridge/issues/57). Nothing here looks at the reader's internals.
/// </summary>
public sealed class SavedTeamReadingTests : IDisposable
{
	private readonly SyntheticGameClient _game = SyntheticGameClient.Create();

	/// <summary>The teams come in the player's order, by Position, not the game's, and the empty position is left out.</summary>
	[Fact]
	public void Reads_the_saved_teams_in_the_players_order()
	{
		Assert.Equivalent(
			new SavedTeam[] { new("GeneralPve", false, [7]), new("Hydra", true, [12, 3]) },
			_game.Source().Read().SavedTeams,
			strict: true);
	}

	[Fact]
	public void Reads_no_saved_teams_when_the_game_holds_no_list()
	{
		using var game = WithTeams(null);

		Assert.Empty(game.Source().Read().SavedTeams);
	}

	[Fact]
	public void Reads_no_saved_teams_from_an_empty_list()
	{
		using var game = WithTeams([]);

		Assert.Empty(game.Source().Read().SavedTeams);
	}

	/// <summary>A team with no Champion is still a team, whether its list is empty, all null, or not there.</summary>
	[Theory]
	[MemberData(nameof(TeamsWithNoChampion))]
	public void Reads_a_saved_team_with_no_champion(int?[]? heroIds)
	{
		using var game = WithTeams([new(Id: 3, Position: 1, NameIsNotDefault: false, heroIds)]);

		Assert.Equivalent(new SavedTeam[] { new("GeneralPve", false, []) }, game.Source().Read().SavedTeams, strict: true);
	}

	public static TheoryData<int?[]?> TeamsWithNoChampion() => new() { { [] }, { [null, null] }, { (int?[]?)null } };

	/// <summary>Each purpose starts at the game's own constant and runs up to the next; ids past the last are the last's.</summary>
	[Theory]
	[InlineData(1, "GeneralPve")]
	[InlineData(100, "GeneralPve")]
	[InlineData(101, "Pvp")]
	[InlineData(201, "Fractions")]
	[InlineData(302, "Fractions")]
	[InlineData(401, "DoomTower")]
	[InlineData(604, "DoomTower")]
	[InlineData(701, "Hydra")]
	[InlineData(801, "Chimera")]
	[InlineData(901, "CooperationEventWorldBoss")]
	[InlineData(1001, "Siege")]
	[InlineData(1101, "IronTwins")]
	[InlineData(1201, "PotionKeeps")]
	[InlineData(1301, "ArtifactAscendAndMasteries")]
	[InlineData(1401, "EventDungeon")]
	[InlineData(1501, "ArtifactDungeons")]
	[InlineData(2000, "ArtifactDungeons")]
	public void Reads_each_purpose_from_the_teams_id(int id, string purpose)
	{
		using var game = WithTeams([new(id, Position: 1, NameIsNotDefault: false, [7])]);

		Assert.Equal(purpose, Assert.Single(game.Source().Read().SavedTeams).Purpose);
	}

	/// <summary>
	/// The game fills TypeRanges only once it first asks a team's type, which it doesn't while it loads the Account, so
	/// the purposes don't need it.
	/// </summary>
	[Fact]
	public void Reads_the_purposes_before_the_game_fills_its_ranges()
	{
		using var game = SyntheticGameClient.Create(heroData: SyntheticHeroData.Default with { PresetTypeRanges = null });

		Assert.Equal(["GeneralPve", "Hydra"], game.Source().Read().SavedTeams.Select(team => team.Purpose));
	}

	/// <summary>Where the game has filled TypeRanges, a purpose it gives otherwise than raid-bridge's pairing is refused.</summary>
	[Fact]
	public void Refuses_a_purpose_the_games_ranges_give_otherwise()
	{
		using var game = SyntheticGameClient.Create(heroData: SyntheticHeroData.Default with
		{
			PresetTypeRanges = [(1501, 13), (1401, 12), (1301, 11), (1201, 10), (1101, 9), (1001, 8), (901, 7), (801, 3), (701, 6), (401, 5), (201, 4), (101, 2), (0, 1)],
		});

		RefusesMatching(game, @"^SharedModel\.Meta\.Heroes\.HeroesAiPreset\.Id at 0x[0-9A-F]+ is 701, in HeroesAiPresets, is Chimera by the game's SharedModel\.Meta\.Heroes\.HeroesAiPreset\.TypeRanges, but Hydra by its SharedModel\.Meta\.Heroes\.HeroesAiPresetIds, which raid-bridge pairs as the game's code did at metadata v39\.$");
	}

	/// <summary>The ids the game keeps for the Arena defence teams aren't Saved teams.</summary>
	[Theory]
	[InlineData(0)]
	[InlineData(301)]
	[InlineData(601)]
	[InlineData(603)]
	public void Refuses_an_arena_defence_id(int id)
	{
		using var game = WithTeams([new(id, Position: 1, NameIsNotDefault: false, [7])]);

		RefusesMatching(game, $@"^SharedModel\.Meta\.Heroes\.HeroesAiPreset\.Id at 0x[0-9A-F]+ is {id}, in HeroesAiPresets, an id the game keeps for an Arena defence team, where raid-bridge expects a Saved team\.$");
	}

	[Fact]
	public void Refuses_an_id_below_every_purpose()
	{
		using var game = WithTeams([new(-5, Position: 1, NameIsNotDefault: false, [7])]);

		RefusesMatching(game, @"^SharedModel\.Meta\.Heroes\.HeroesAiPreset\.Id at 0x[0-9A-F]+ is -5, in HeroesAiPresets, below the first id of every purpose in SharedModel\.Meta\.Heroes\.HeroesAiPresetIds, so raid-bridge can't tell what the team is for\.$");
	}

	/// <summary>A game update that adds a purpose has ids raid-bridge doesn't know, so it can't tell which teams are for it.</summary>
	[Fact]
	public void Refuses_a_purpose_raid_bridge_does_not_know()
	{
		_game.AddEnumMember("SharedModel.Meta.Heroes.HeroesAiPresetType", "Grimforge", 14);

		Refuses(_game, "The game's SharedModel.Meta.Heroes.HeroesAiPresetType has Grimforge, a purpose raid-bridge doesn't know the ids of.");
	}

	/// <summary>The game takes a Champion that leaves the Account out of every Saved team, so one still in it is refused.</summary>
	[Fact]
	public void Refuses_a_champion_the_account_does_not_hold()
	{
		using var game = WithTeams([new(1, Position: 1, NameIsNotDefault: false, [7, 99])]);

		RefusesMatching(game, @"^SharedModel\.Meta\.Heroes\.SkillPrioritiesSetup\.HeroId at 0x[0-9A-F]+ is 99, in the GeneralPve Saved team at 0x[0-9A-F]+, but HeroById holds no Champion 99\.$");
	}

	[Fact]
	public void Refuses_a_champion_twice_in_a_saved_team()
	{
		using var game = WithTeams([new(1, Position: 1, NameIsNotDefault: false, [7, null, 7])]);

		RefusesMatching(game, @"^SharedModel\.Meta\.Heroes\.SkillPrioritiesSetup\.HeroId at 0x[0-9A-F]+ is 7, in the GeneralPve Saved team at 0x[0-9A-F]+, but Champion 7 is already in it\.$");
	}

	/// <summary>A Champion may be in several Saved teams, as it may be in a defence team too.</summary>
	[Fact]
	public void Reads_a_champion_in_two_saved_teams()
	{
		using var game = WithTeams([new(1, Position: 1, NameIsNotDefault: false, [7, 3]), new(2, Position: 2, NameIsNotDefault: false, [3])]);

		Assert.Equal([[7, 3], [3]], game.Source().Read().SavedTeams.Select(team => team.ChampionIds));
	}

	[Fact]
	public void Refuses_more_champions_than_the_purpose_takes()
	{
		using var game = SyntheticGameClient.Create(heroData: SyntheticHeroData.Default with
		{
			SavedTeams = [new(101, Position: 1, NameIsNotDefault: false, [7, 3, 12])],
			MaxHeroesByPresetType = new Dictionary<int, int>(SyntheticHeroData.Default.MaxHeroesByPresetType) { [2] = 2 },
		});

		RefusesMatching(game, @"^the Pvp Saved team at 0x[0-9A-F]+ holds 3 Champions, where a Pvp team takes at most 2\.$");
	}

	[Fact]
	public void Refuses_a_purpose_that_takes_no_champion()
	{
		using var game = SyntheticGameClient.Create(heroData: SyntheticHeroData.Default with
		{
			MaxHeroesByPresetType = new Dictionary<int, int>(SyntheticHeroData.Default.MaxHeroesByPresetType) { [3] = 0 },
		});

		RefusesMatching(game, @"^SharedModel\.Meta\.Heroes\.HeroesAiPresetInfo\.MaxHeroesCount at 0x[0-9A-F]+ is 0, where raid-bridge expects 1 or more\.$");
	}

	[Fact]
	public void Refuses_a_purpose_the_static_data_holds_no_size_for()
	{
		var sizes = new Dictionary<int, int>(SyntheticHeroData.Default.MaxHeroesByPresetType);
		sizes.Remove(3);
		using var game = SyntheticGameClient.Create(heroData: SyntheticHeroData.Default with { MaxHeroesByPresetType = sizes });

		RefusesMatching(game, @"^SharedModel\.Meta\.Heroes\.HeroesAiPreset\.Id at 0x[0-9A-F]+ is 701, a Hydra Saved team, but the static data's StaticHeroData\.PresetInfoByType holds no Hydra, so raid-bridge can't tell how many Champions it takes\.$");
	}

	/// <summary>Two teams at one position leave their order unknown.</summary>
	[Fact]
	public void Refuses_two_saved_teams_at_one_position()
	{
		using var game = WithTeams([new(1, Position: 1, NameIsNotDefault: false, [7]), new(2, Position: 1, NameIsNotDefault: false, [3])]);

		RefusesMatching(game, @"^SharedModel\.Meta\.Heroes\.HeroesAiPreset\.Position at 0x[0-9A-F]+ is 1, but another Saved team in HeroesAiPresets is at that position too, so raid-bridge can't tell their order\.$");
	}

	public void Dispose() => _game.Dispose();

	private static SyntheticGameClient WithTeams(IReadOnlyList<SyntheticSavedTeam>? teams) =>
		SyntheticGameClient.Create(heroData: SyntheticHeroData.Default with { SavedTeams = teams });

	private static void Refuses(SyntheticGameClient game, string message) =>
		Assert.Equal(message, Assert.ThrowsAny<GameClientException>(() => game.Source().Read()).Message);

	private static void RefusesMatching(SyntheticGameClient game, string pattern) =>
		Assert.Matches(pattern, Assert.ThrowsAny<GameClientException>(() => game.Source().Read()).Message);
}
