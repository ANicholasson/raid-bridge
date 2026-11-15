namespace RaidBridge.Domain.Arena;

/// <summary>An Arena team: the ids of the Champions in it, in the game's order, each a Champion on the Account, and in the team once.</summary>
public sealed record ArenaTeam(IReadOnlyList<int> ChampionIds);
