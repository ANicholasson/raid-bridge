namespace RaidBridge.Domain.Relics;

/// <summary>
/// One of a Relic's stat bonuses, in the shape of an Artifact's with no Glyph and no rolls. The value is as the game's
/// static table holds it, so a percentage is <c>0.06</c>, not <c>6</c>; <see cref="Flat"/> says which it is.
/// </summary>
public sealed record RelicBonus(string Stat, double Value, bool Flat);
