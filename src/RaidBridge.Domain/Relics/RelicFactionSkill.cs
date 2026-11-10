namespace RaidBridge.Domain.Relics;

/// <summary>
/// A passive effect a Relic adds only when its wearer is of <see cref="Faction"/>, the name the game's own enum gives
/// it, such as <c>DarkElves</c>: its skill type, and the description the Game client shows for it.
/// </summary>
public sealed record RelicFactionSkill(string Faction, int TypeId, string Description);
