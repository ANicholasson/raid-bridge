namespace RaidBridge.Domain.GreatHall;

/// <summary>A stat's level in the Great Hall of one Affinity: the stat, as the game's own enum names it, such as <c>Health</c>, and its level.</summary>
public sealed record GreatHallLevel(string Stat, int Level);
