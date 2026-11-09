namespace RaidBridge.Domain.Artifacts;

/// <summary>
/// One of an Artifact's bonuses: its main stat, its ascension bonus or a Substat. The value and the Glyph are as the
/// game holds them, so a percentage is <c>0.06</c>, not <c>6</c>; <see cref="Flat"/> says which it is, and
/// <see cref="Rolls"/> is how many times the bonus was raised.
/// </summary>
public sealed record ArtifactBonus(string Stat, double Value, bool Flat, double Glyph, int Rolls);
