using RaidBridge.Domain.Arena;
using RaidBridge.Game.Testing;
using RaidBridge.Game.Testing.Arena;
using RaidBridge.Game.Testing.Champions;

namespace RaidBridge.Game.Tests.Arena;

/// <summary>
/// The Account's Classic and Tag Arena, read end to end through the fake memory port: a synthetic Game client in, each
/// Arena's league, points, week and defence teams under the game's own names, or a named refusal, out (spec #32, #37).
/// Nothing here looks at the reader's internals.
/// </summary>
public sealed class ArenaReadingTests : IDisposable
{
	/// <summary>The default heroes, with a team remembered for the Clan Boss's key, 4015, but none for the Classic Arena's.</summary>
	private static readonly SyntheticHeroData NeverAttacked = SyntheticHeroData.Default with
	{
		BattlePresets = new Dictionary<int, IReadOnlyList<int>> { [4015] = [12, 3] },
	};

	private readonly SyntheticGameClient _game = SyntheticGameClient.Create();

	/// <summary>The empty second position of the defence team is left out, and the others keep the game's order.</summary>
	[Fact]
	public void Reads_the_classic_arena()
	{
		Assert.Equivalent(new ClassicArena("SilverIII", 1450, 12, 8, 3, new([12, 3, 7]), new([7, 3, 12])), _game.Source().Read().ClassicArena, strict: true);
	}

	/// <summary>The empty positions of the first team are left out, and so is every position of the third, which stays in its place.</summary>
	[Fact]
	public void Reads_the_tag_arena()
	{
		Assert.Equivalent(new TagArena("BronzeIV", 820, 6, 2, 4, [new([3]), new([7, 12]), new([])]), _game.Source().Read().TagArena, strict: true);
	}

	/// <summary>
	/// An Account with no Arena set up holds no defence team in either Arena, so neither has a league, and the game
	/// remembers no Classic Arena team, though it remembers one for the Clan Boss.
	/// </summary>
	[Fact]
	public void Reads_an_account_with_no_arena_set_up()
	{
		using var game = SyntheticGameClient.Create(heroData: NeverAttacked, arenaData: SyntheticArenaData.None);

		var account = game.Source().Read();

		Assert.Equivalent(new ClassicArena(null, 1000, 0, 0, 0, new([]), null), account.ClassicArena, strict: true);
		Assert.Equivalent(new TagArena(null, 0, 0, 0, 0, []), account.TagArena, strict: true);
	}

	/// <summary>
	/// Tag Arena teams that hold no Champion between them are no defence, as no list of teams is: a live read wrote one
	/// empty team for an Account with no Tag Arena defence set (https://github.com/ANicholasson/raid-bridge/issues/51).
	/// </summary>
	[Theory]
	[MemberData(nameof(TeamsWithNoChampion))]
	public void Reads_tag_teams_with_no_champion_as_no_defence(SyntheticTeamSetup[] teams)
	{
		using var game = SyntheticGameClient.Create(arenaData: SyntheticArenaData.Default with
		{
			Tag = SyntheticArenaData.Default.Tag with { DefenceTeams = teams },
		});

		Assert.Empty(game.Source().Read().TagArena.DefenceTeams);
	}

	public static TheoryData<SyntheticTeamSetup[]> TeamsWithNoChampion() => new()
	{
		{ [new(4, [])] },
		{ [new(4, [0, 0, 0, 0])] },
		{ [new(4, [0, 0, 0, 0]), new(4, []), new(4, [0])] },
	};

	/// <summary>An empty team keeps its place before a team with Champions in it, as it does after one.</summary>
	[Fact]
	public void Keeps_an_empty_tag_team_before_a_team_with_champions()
	{
		using var game = SyntheticGameClient.Create(arenaData: SyntheticArenaData.Default with
		{
			Tag = SyntheticArenaData.Default.Tag with { DefenceTeams = [new(4, [0, 0]), new(4, [7])] },
		});

		Assert.Equivalent(new ArenaTeam[] { new([]), new([7]) }, game.Source().Read().TagArena.DefenceTeams, strict: true);
	}

	/// <summary>Four Champions in every position of every team, twelve in the Tag Arena, each defending once in it.</summary>
	[Fact]
	public void Reads_full_defence_teams()
	{
		var heroes = SyntheticHeroData.Default with
		{
			Heroes = [.. SyntheticHeroData.Default.Heroes, .. Enumerable.Range(20, 9).Select(id => new SyntheticHero(id, TypeId: 5240))],
		};
		using var game = SyntheticGameClient.Create(
			heroData: heroes,
			arenaData: SyntheticArenaData.Default with
			{
				Classic = SyntheticArenaData.Default.Classic with { DefenceTeam = new(22, [20, 12, 7, 3]) },
				Tag = SyntheticArenaData.Default.Tag with { DefenceTeams = [new(4, [3, 7, 12, 20]), new(4, [21, 22, 23, 24]), new(4, [25, 26, 27, 28])] },
			});

		var account = game.Source().Read();

		Assert.Equivalent(new ClassicArena("GoldII", 1450, 12, 8, 3, new([20, 12, 7, 3]), new([7, 3, 12])), account.ClassicArena, strict: true);
		Assert.Equivalent(new ArenaTeam[] { new([3, 7, 12, 20]), new([21, 22, 23, 24]), new([25, 26, 27, 28]) }, account.TagArena.DefenceTeams, strict: true);
	}

	/// <summary>The game's member for no league is written as none, as an Artifact's set None is.</summary>
	[Fact]
	public void Reads_no_league_when_the_game_holds_none()
	{
		using var game = SyntheticGameClient.Create(arenaData: SyntheticArenaData.Default with
		{
			Classic = SyntheticArenaData.Default.Classic with { DefenceTeam = new(0, [3]) },
			Tag = SyntheticArenaData.Default.Tag with { League = 0 },
		});

		var account = game.Source().Read();

		Assert.Equal((null, null), (account.ClassicArena.League, account.TagArena.League));
	}

	/// <summary>
	/// The Last attack team is under the key the static data's team size makes: with five Champions to a Classic Arena
	/// team, 3015, not 3014 (https://github.com/ANicholasson/raid-bridge/issues/56).
	/// </summary>
	[Fact]
	public void Reads_the_last_attack_team_under_the_key_the_team_size_makes()
	{
		using var game = SyntheticGameClient.Create(
			heroData: SyntheticHeroData.Default with
			{
				BattlePresets = new Dictionary<int, IReadOnlyList<int>> { [3014] = [7], [3015] = [12, 7, 3] },
			},
			arenaData: SyntheticArenaData.Default with { HeroSlotsCount = 5 });

		Assert.Equivalent(new ArenaTeam([12, 7, 3]), game.Source().Read().ClassicArena.LastAttackTeam, strict: true);
	}

	/// <summary>A remembered team with no Champion in it is a team, written empty, not a team the game doesn't remember.</summary>
	[Fact]
	public void Reads_a_last_attack_team_with_no_champion()
	{
		using var game = SyntheticGameClient.Create(heroData: SyntheticHeroData.Default with
		{
			BattlePresets = new Dictionary<int, IReadOnlyList<int>> { [3014] = [] },
		});

		Assert.Equivalent(new ArenaTeam([]), game.Source().Read().ClassicArena.LastAttackTeam, strict: true);
	}

	/// <summary>
	/// The game keeps the id of a Champion that has left the Account until the next attack, and its team screen leaves
	/// that position empty, so the Last attack team leaves it out (https://github.com/ANicholasson/raid-bridge/issues/56).
	/// </summary>
	[Fact]
	public void Leaves_a_champion_no_longer_on_the_account_out_of_the_last_attack_team()
	{
		using var game = SyntheticGameClient.Create(heroData: SyntheticHeroData.Default with
		{
			BattlePresets = new Dictionary<int, IReadOnlyList<int>> { [3014] = [7, 99, 12] },
		});

		Assert.Equivalent(new ArenaTeam([7, 12]), game.Source().Read().ClassicArena.LastAttackTeam, strict: true);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void Refuses_a_last_attack_team_id_that_is_not_a_champion_id(int heroId)
	{
		using var game = SyntheticGameClient.Create(heroData: SyntheticHeroData.Default with
		{
			BattlePresets = new Dictionary<int, IReadOnlyList<int>> { [3014] = [7, heroId] },
		});

		RefusesMatching(game, $@"^SharedModel\.Meta\.Heroes\.UserHeroData\.BattlePresets\[3014\]\[1\] at 0x[0-9A-F]+ is {heroId}, in the Classic Arena's Last attack team, where raid-bridge expects a Champion's id, which is positive\.$");
	}

	[Fact]
	public void Refuses_a_champion_twice_in_the_last_attack_team()
	{
		using var game = SyntheticGameClient.Create(heroData: SyntheticHeroData.Default with
		{
			BattlePresets = new Dictionary<int, IReadOnlyList<int>> { [3014] = [7, 3, 7] },
		});

		RefusesMatching(game, @"^SharedModel\.Meta\.Heroes\.UserHeroData\.BattlePresets\[3014\]\[2\] at 0x[0-9A-F]+ is 7, in the Classic Arena's Last attack team, but Champion 7 is already in it\.$");
	}

	/// <summary>A Champion that has left the Account is still checked to be in the team once (code review, https://github.com/ANicholasson/raid-bridge/pull/59).</summary>
	[Fact]
	public void Refuses_a_champion_no_longer_on_the_account_twice_in_the_last_attack_team()
	{
		using var game = SyntheticGameClient.Create(heroData: SyntheticHeroData.Default with
		{
			BattlePresets = new Dictionary<int, IReadOnlyList<int>> { [3014] = [99, 7, 99] },
		});

		RefusesMatching(game, @"^SharedModel\.Meta\.Heroes\.UserHeroData\.BattlePresets\[3014\]\[2\] at 0x[0-9A-F]+ is 99, in the Classic Arena's Last attack team, but Champion 99 is already in it\.$");
	}

	[Fact]
	public void Refuses_a_last_attack_team_larger_than_a_classic_arena_team()
	{
		using var game = SyntheticGameClient.Create(heroData: SyntheticHeroData.Default with
		{
			BattlePresets = new Dictionary<int, IReadOnlyList<int>> { [3014] = [7, 3, 12, 7, 3] },
		});

		RefusesMatching(game, @"^SharedModel\.Meta\.Heroes\.UserHeroData\.BattlePresets\[3014\] at 0x[0-9A-F]+ holds 5 Champions, in the Classic Arena's Last attack team, where a Classic Arena team takes at most 4\.$");
	}

	[Theory]
	[InlineData(0)]
	[InlineData(10)]
	public void Refuses_a_classic_arena_team_size_the_key_cannot_hold(int heroSlotsCount)
	{
		using var game = SyntheticGameClient.Create(arenaData: SyntheticArenaData.Default with { HeroSlotsCount = heroSlotsCount });

		RefusesMatching(game, $@"^SharedModel\.Meta\.Battle\.Arena\.StaticArenaData\.HeroSlotsCount at 0x[0-9A-F]+ is {heroSlotsCount}, where raid-bridge expects a Classic Arena team of 1 to 9 Champions, which the key keeps in one digit\.$");
	}

	[Fact]
	public void Refuses_a_classic_defender_the_account_does_not_hold()
	{
		using var game = SyntheticGameClient.Create(arenaData: SyntheticArenaData.Default with
		{
			Classic = SyntheticArenaData.Default.Classic with { DefenceTeam = new(13, [3, 99]) },
		});

		RefusesMatching(game, @"^SharedModel\.Battle\.Core\.Setup\.HeroSlotSetup\.InventoryHeroId at 0x[0-9A-F]+ is 99, in the Classic Arena's defence team, but HeroById holds no Champion 99\.$");
	}

	[Fact]
	public void Refuses_a_tag_defender_the_account_does_not_hold()
	{
		using var game = SyntheticGameClient.Create(arenaData: SyntheticArenaData.Default with
		{
			Tag = SyntheticArenaData.Default.Tag with { DefenceTeams = [new(4, [3]), new(4, [7, 99])] },
		});

		RefusesMatching(game, @"^SharedModel\.Battle\.Core\.Setup\.HeroSlotSetup\.InventoryHeroId at 0x[0-9A-F]+ is 99, in team 2 of the Tag Arena's defence, but HeroById holds no Champion 99\.$");
	}

	[Fact]
	public void Refuses_a_champion_in_two_positions_of_the_classic_defence()
	{
		using var game = SyntheticGameClient.Create(arenaData: SyntheticArenaData.Default with
		{
			Classic = SyntheticArenaData.Default.Classic with { DefenceTeam = new(13, [12, 3, 12]) },
		});

		RefusesMatching(game, @"^SharedModel\.Battle\.Core\.Setup\.HeroSlotSetup\.InventoryHeroId at 0x[0-9A-F]+ is 12, in the Classic Arena's defence team, but Champion 12 already defends in the Classic Arena's defence team\.$");
	}

	[Fact]
	public void Refuses_a_champion_in_two_tag_teams()
	{
		using var game = SyntheticGameClient.Create(arenaData: SyntheticArenaData.Default with
		{
			Tag = SyntheticArenaData.Default.Tag with { DefenceTeams = [new(4, [3]), new(4, [7]), new(4, [12, 3])] },
		});

		RefusesMatching(game, @"^SharedModel\.Battle\.Core\.Setup\.HeroSlotSetup\.InventoryHeroId at 0x[0-9A-F]+ is 3, in team 3 of the Tag Arena's defence, but Champion 3 already defends in team 1 of the Tag Arena's defence\.$");
	}

	[Fact]
	public void Refuses_a_classic_league_the_game_does_not_name()
	{
		using var game = SyntheticGameClient.Create(arenaData: SyntheticArenaData.Default with
		{
			Classic = SyntheticArenaData.Default.Classic with { DefenceTeam = new(99, [3]) },
		});

		RefusesMatching(game, @"^SharedModel\.Battle\.Core\.Setup\.TeamSetup\.ArenaLeagueId at 0x[0-9A-F]+ is 99, which isn't a member of the game's SharedModel\.Meta\.Battle\.Arena\.ArenaLeagueId\.$");
	}

	[Fact]
	public void Refuses_a_tag_league_the_game_does_not_name()
	{
		using var game = SyntheticGameClient.Create(arenaData: SyntheticArenaData.Default with
		{
			Tag = SyntheticArenaData.Default.Tag with { League = 99 },
		});

		RefusesMatching(game, @"^SharedModel\.Meta\.Battle\.Arena3x3\.UserArena3X3Data\.LeagueId at 0x[0-9A-F]+ is 99, which isn't a member of the game's SharedModel\.Meta\.Battle\.Arena\.ArenaLeagueId\.$");
	}

	[Theory]
	[InlineData(-1, 0, 0, "BattlesStartedThisWeek", -1)]
	[InlineData(3, -1, 0, "VictoriesThisWeek", -1)]
	[InlineData(3, 0, -2, "LossesThisWeek", -2)]
	public void Refuses_a_negative_count(int battles, int wins, int losses, string field, int count)
	{
		using var game = SyntheticGameClient.Create(arenaData: SyntheticArenaData.Default with
		{
			Tag = SyntheticArenaData.Default.Tag with { Battles = battles, Wins = wins, Losses = losses },
		});

		RefusesMatching(game, $@"^SharedModel\.Meta\.Battle\.Arena\.UserArenaData\.{field} at 0x[0-9A-F]+ is {count}, in the Tag Arena, where raid-bridge expects 0 or more\.$");
	}

	/// <summary>The game counts more wins and losses than battles started, as the main Account's Classic Arena held live (#37).</summary>
	[Fact]
	public void Reads_more_wins_and_losses_than_battles_started()
	{
		using var game = SyntheticGameClient.Create(arenaData: SyntheticArenaData.Default with
		{
			Classic = SyntheticArenaData.Default.Classic with { Battles = 66, Wins = 83, Losses = 56 },
		});

		var classic = game.Source().Read().ClassicArena;

		Assert.Equal((66, 83, 56), (classic.Battles, classic.Wins, classic.Losses));
	}

	[Fact]
	public void Refuses_a_list_of_tag_teams_that_claims_more_than_it_holds()
	{
		_game.SetTagTeamCount(5);

		RefusesMatching(_game, @"^SharedModel\.Meta\.Battle\.Arena3x3\.UserArena3X3Data\.Arena3X3DefenseTeamSetup at 0x[0-9A-F]+ is malformed: its _size is 5, but its array holds 4\.$");
	}

	[Fact]
	public void Refuses_a_classic_defence_team_that_is_not_an_object()
	{
		_game.PointField("SharedModel.Meta.Battle.Arena.UserArenaData", "ArenaDefenseTeamSetup", SyntheticGameClient.Unmapped);

		RefusesMatching(_game, @"^SharedModel\.Meta\.Battle\.Arena\.UserArenaData\.ArenaDefenseTeamSetup at 0x[0-9A-F]+ points to 0xDEAD00000000, which isn't an object\. ");
	}

	[Fact]
	public void Refuses_a_tag_arena_that_is_not_an_object()
	{
		_game.PointField("Client.Model.Guard.UserWrapper", "Arena3x3", SyntheticGameClient.Unmapped);

		RefusesMatching(_game, @"^Client\.Model\.Guard\.UserWrapper\.Arena3x3 at 0x[0-9A-F]+ points to 0xDEAD00000000, which isn't an object\. ");
	}

	[Fact]
	public void Refuses_a_week_field_missing_from_the_metadata()
	{
		_game.UndeclareField("SharedModel.Meta.Battle.Arena.UserArenaData", "VictoriesThisWeek");

		Refuses(_game, "The game's class SharedModel.Meta.Battle.Arena.UserArenaData has no field VictoriesThisWeek, base classes included.");
	}

	[Fact]
	public void Refuses_a_defender_field_missing_from_the_metadata()
	{
		_game.UndeclareField("SharedModel.Battle.Core.Setup.HeroSlotSetup", "InventoryHeroId");

		Refuses(_game, "The game's class SharedModel.Battle.Core.Setup.HeroSlotSetup has no field InventoryHeroId, base classes included.");
	}

	[Fact]
	public void Refuses_an_arena_class_missing_from_the_metadata()
	{
		_game.RenameClass("Client.Model.Gameplay.Arena3x3.Arena3x3Wrapper", "Client.Model.Gameplay.Arena3x3.TagArenaWrapper");

		Refuses(_game, "The game's metadata has no class Client.Model.Gameplay.Arena3x3.Arena3x3Wrapper.");
	}

	public void Dispose() => _game.Dispose();

	private static void Refuses(SyntheticGameClient game, string message) =>
		Assert.Equal(message, Assert.ThrowsAny<GameClientException>(() => game.Source().Read()).Message);

	private static void RefusesMatching(SyntheticGameClient game, string pattern) =>
		Assert.Matches(pattern, Assert.ThrowsAny<GameClientException>(() => game.Source().Read()).Message);
}
