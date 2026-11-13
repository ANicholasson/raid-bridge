using RaidBridge.Game.Testing.Champions;

namespace RaidBridge.Game.Testing.Arena;

/// <summary>
/// The Classic and Tag Arena a synthetic Game client holds, in the game's own terms: leagues hold the game's numbers,
/// such as 13 for SilverIII, and each defence team holds the InventoryHeroId of each of its positions, 0 for a position
/// with no Champion in it.
/// </summary>
public sealed record SyntheticArenaData(SyntheticClassicArena Classic, SyntheticTagArena Tag)
{
	/// <summary>
	/// The static data's ArenaLeagueInfos: each league's BattleBonuses, by its number, as fractions of the Base stats.
	/// BronzeIV gives 4% and SilverIII 10% of Health, Attack and Defence, as the game's own table does; GoldII gives
	/// Speed too, which the game's doesn't, so a test can see every stat taken.
	/// </summary>
	public IReadOnlyDictionary<int, SyntheticBattleStats> LeagueBonuses { get; init; } = new Dictionary<int, SyntheticBattleStats>
	{
		[4] = Percent(0.04),
		[13] = Percent(0.1),
		[22] = Percent(0.16) with { Speed = 0.05, Resistance = 0.05, Accuracy = 0.05 },
	};

	/// <summary>The Live Arena: 1520 points, which reach SilverII in <see cref="LiveLeagues"/>, 14 wins and 9 losses. Null holds no LiveArenaData.</summary>
	public SyntheticLiveArena? Live { get; init; } = new(Points: 1520, Wins: 14, Losses: 9);

	/// <summary>
	/// The static data's LiveArenaData.LeagueInfoById: each league, by its number, with its MinPoints, in the order the
	/// game holds them, which the game walks. A few of the game's own leagues and their points at metadata v39.
	/// </summary>
	public IReadOnlyList<(int League, int MinPoints)> LiveLeagues { get; init; } = [(1, 900), (2, 950), (11, 1300), (12, 1500), (24, 4800)];

	/// <summary>
	/// The Classic Arena in SilverIII, its defence team the default Champions 12, 3 and 7 with an empty second
	/// position, which DefenseHeroIds names too; the Tag Arena in BronzeIV, Champion 3 in its first team, 7 and 12 in its second and none in its third.
	/// </summary>
	public static SyntheticArenaData Default { get; } = new(
		new(Points: 1450, Battles: 12, Wins: 8, Losses: 3, DefenceTeam: new(League: 13, [12, 0, 3, 7]), DefenseHeroIds: [12, -1, 3, 7]),
		new(League: 4, Points: 820, Battles: 6, Wins: 2, Losses: 4, DefenceTeams: [new(4, [3, 0, 0]), new(4, [7, 12]), new(4, [])]));

	/// <summary>No Arena set up: no defence team in either Arena, no league in the Tag Arena, no battles this week, and the Live Arena's starting 900 points.</summary>
	public static SyntheticArenaData None { get; } = new(
		new(Points: 1000, Battles: 0, Wins: 0, Losses: 0, DefenceTeam: null, DefenseHeroIds: null),
		new(League: 0, Points: 0, Battles: 0, Wins: 0, Losses: 0, DefenceTeams: null))
	{
		Live = new(Points: 900, Wins: 0, Losses: 0),
	};

	private static SyntheticBattleStats Percent(double value) => new(value, value, value, 0, 0, 0, 0, 0);
}

/// <summary>
/// The Classic Arena, as SharedModel.Meta.Battle.Arena.UserArenaData holds it: its defence team is null when none is
/// set, and DefenseHeroIds, -1 for an empty position, is null when the game holds none.
/// </summary>
public sealed record SyntheticClassicArena(long Points, int Battles, int Wins, int Losses, SyntheticTeamSetup? DefenceTeam, IReadOnlyList<int>? DefenseHeroIds);

/// <summary>The Tag Arena, as SharedModel.Meta.Battle.Arena3x3.UserArena3X3Data holds it: its list of defence teams is null when none is set.</summary>
public sealed record SyntheticTagArena(int League, long Points, int Battles, int Wins, int Losses, IReadOnlyList<SyntheticTeamSetup>? DefenceTeams);

/// <summary>A defence team, as SharedModel.Battle.Core.Setup.TeamSetup holds it: its ArenaLeagueId, and each HeroSlotSetup's InventoryHeroId.</summary>
public sealed record SyntheticTeamSetup(int League, IReadOnlyList<int> HeroIds);

/// <summary>The Live Arena, as SharedModel.Meta.LiveArena.UserLiveArenaData holds it: its points, wins and losses.</summary>
public sealed record SyntheticLiveArena(long Points, int Wins, int Losses);
