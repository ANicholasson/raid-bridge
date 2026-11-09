namespace RaidBridge.Game.Testing.Arena;

/// <summary>
/// The Classic and Tag Arena a synthetic Game client holds, in the game's own terms: leagues hold the game's numbers,
/// such as 13 for SilverIII, and each defence team holds the InventoryHeroId of each of its positions, 0 for a position
/// with no Champion in it.
/// </summary>
public sealed record SyntheticArenaData(SyntheticClassicArena Classic, SyntheticTagArena Tag)
{
	/// <summary>
	/// The Classic Arena in SilverIII, its defence team the default Champions 12, 3 and 7 with an empty second
	/// position; the Tag Arena in BronzeIV, Champion 3 in its first team, 7 and 12 in its second and none in its third.
	/// </summary>
	public static SyntheticArenaData Default { get; } = new(
		new(Points: 1450, Battles: 12, Wins: 8, Losses: 3, DefenceTeam: new(League: 13, [12, 0, 3, 7])),
		new(League: 4, Points: 820, Battles: 6, Wins: 2, Losses: 4, DefenceTeams: [new(4, [3, 0, 0]), new(4, [7, 12]), new(4, [])]));

	/// <summary>No Arena set up: no defence team in either Arena, no league in the Tag Arena, and no battles this week.</summary>
	public static SyntheticArenaData None { get; } = new(
		new(Points: 1000, Battles: 0, Wins: 0, Losses: 0, DefenceTeam: null),
		new(League: 0, Points: 0, Battles: 0, Wins: 0, Losses: 0, DefenceTeams: null));
}

/// <summary>The Classic Arena, as SharedModel.Meta.Battle.Arena.UserArenaData holds it: its defence team is null when none is set.</summary>
public sealed record SyntheticClassicArena(long Points, int Battles, int Wins, int Losses, SyntheticTeamSetup? DefenceTeam);

/// <summary>The Tag Arena, as SharedModel.Meta.Battle.Arena3x3.UserArena3X3Data holds it: its list of defence teams is null when none is set.</summary>
public sealed record SyntheticTagArena(int League, long Points, int Battles, int Wins, int Losses, IReadOnlyList<SyntheticTeamSetup>? DefenceTeams);

/// <summary>A defence team, as SharedModel.Battle.Core.Setup.TeamSetup holds it: its ArenaLeagueId, and each HeroSlotSetup's InventoryHeroId.</summary>
public sealed record SyntheticTeamSetup(int League, IReadOnlyList<int> HeroIds);
