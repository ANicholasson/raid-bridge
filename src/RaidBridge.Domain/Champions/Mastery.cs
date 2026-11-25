namespace RaidBridge.Domain.Champions;

/// <summary>One of the masteries a Champion took: its id, and its name as the Game client shows it.</summary>
public sealed record Mastery(int Id, string Name);
