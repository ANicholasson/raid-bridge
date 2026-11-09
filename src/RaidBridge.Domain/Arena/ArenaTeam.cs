namespace RaidBridge.Domain.Arena;

/// <summary>An Arena defence team: the ids of the Champions in it, in the game's order, each a Champion on the Account.</summary>
public sealed record ArenaTeam(IReadOnlyList<int> ChampionIds);
