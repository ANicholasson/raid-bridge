namespace RaidBridge.Domain.Arena;

/// <summary>
/// The Account's Classic Arena: its league, as the game's own enum names it, such as <c>GoldII</c>, or null when the
/// game holds none; its points; this week's battles, wins and losses, as of the moment of capture; and its defence
/// team.
/// </summary>
public sealed record ClassicArena(string? League, long Points, int Battles, int Wins, int Losses, ArenaTeam DefenceTeam);
