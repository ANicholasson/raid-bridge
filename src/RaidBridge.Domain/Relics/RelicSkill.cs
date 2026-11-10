namespace RaidBridge.Domain.Relics;

/// <summary>
/// A passive effect of a Relic or a Relic stone: its skill type, and the description the Game client shows for it.
/// The game holds no real name for these skills, only placeholder text, so none is written.
/// </summary>
public sealed record RelicSkill(int TypeId, string Description);
