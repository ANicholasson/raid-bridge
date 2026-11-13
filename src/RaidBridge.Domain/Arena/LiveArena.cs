namespace RaidBridge.Domain.Arena;

/// <summary>
/// The Account's Live Arena: its league, as the game's own enum names it, such as <c>SilverII</c>, worked out from its
/// points as the game works it out; its points; and the wins and losses the game holds for it. An Account the game
/// holds no Live Arena data for has none, which is null on the Account.
/// </summary>
public sealed record LiveArena(string League, long Points, int Wins, int Losses);
