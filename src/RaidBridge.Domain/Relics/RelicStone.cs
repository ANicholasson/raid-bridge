namespace RaidBridge.Domain.Relics;

/// <summary>
/// A Relic stone on the Account, in a socket or not. Its rarity and shape are the names the game's own enums give them;
/// it fits only a socket of its shape. It adds skills, not stats.
/// </summary>
public sealed record RelicStone(int Id, int TypeId, string Name, string Rarity, string Shape, IReadOnlyList<RelicSkill> Skills);
