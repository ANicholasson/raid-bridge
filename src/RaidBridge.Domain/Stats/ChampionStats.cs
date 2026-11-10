namespace RaidBridge.Domain.Stats;

/// <summary>
/// A Champion's stats by Stat source (ADR-0005). So far only its Base stats: the eight values the champion screen
/// shows in white, at the Champion's rank, level and ascension, in points, with <see cref="Stat.CriticalChance"/> and
/// <see cref="Stat.CriticalDamage"/> as fractions, as an Artifact's bonuses are. The other Stat sources, and the
/// Total stats, come one ticket at a time (https://github.com/ANicholasson/raid-bridge/issues/5).
/// </summary>
public sealed record ChampionStats(IReadOnlyDictionary<Stat, double> Base);
