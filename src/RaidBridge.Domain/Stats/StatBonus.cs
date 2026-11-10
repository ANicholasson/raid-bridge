namespace RaidBridge.Domain.Stats;

/// <summary>
/// One bonus to one of a Champion's stats, as the game's static data or an Artifact gives it: <see cref="Value"/>
/// points when <see cref="Flat"/>, or that share of the Champion's Base stat when not, so 0.12 is 12%.
/// </summary>
public sealed record StatBonus(Stat Stat, double Value, bool Flat);
