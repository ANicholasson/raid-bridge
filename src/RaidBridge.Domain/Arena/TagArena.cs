namespace RaidBridge.Domain.Arena;

/// <summary>
/// The Account's Tag Arena: its league, as the game's own enum names it, such as <c>GoldII</c>, or null when the game
/// holds none; its points; this week's battles, wins and losses, as of the moment of capture; and its defence teams,
/// in the game's order.
/// </summary>
public sealed record TagArena(string? League, long Points, int Battles, int Wins, int Losses, IReadOnlyList<ArenaTeam> DefenceTeams);
