namespace RaidBridge.Domain.Accounts;

/// <summary>
/// The Account's level, its experience and its power, which the game's profile screen shows, as the game holds them: the level 1 or more, the experience 0
/// or more, and the power exactly as held, a double, never rounded (https://github.com/ANicholasson/raid-bridge/issues/27).
/// </summary>
public sealed record AccountProfile(int Level, int Experience, double Power);
