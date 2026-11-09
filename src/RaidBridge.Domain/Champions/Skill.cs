namespace RaidBridge.Domain.Champions;

/// <summary>One of a Champion's skills: its type, its name as the Game client shows it, and the level the Champion has it at.</summary>
public sealed record Skill(int TypeId, string Name, int Level);
