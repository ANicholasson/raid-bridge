namespace RaidBridge.Domain.Champions;

/// <summary>
/// One of a Champion's skills: its type, its name as the Game client shows it, the level the Champion has it at, and
/// the highest level the skill can reach, 1 for a skill that can't be raised.
/// </summary>
public sealed record Skill(int TypeId, string Name, int Level, int MaxLevel);
