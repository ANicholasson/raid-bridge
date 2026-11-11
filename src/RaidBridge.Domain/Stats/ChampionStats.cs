namespace RaidBridge.Domain.Stats;

/// <summary>
/// A Champion's stats, by what gives them (ADR-0005), each in points, with <see cref="Stat.CriticalChance"/> and
/// <see cref="Stat.CriticalDamage"/> as fractions, as an Artifact's bonuses are:
/// <list type="bullet">
/// <item><see cref="Base"/>: its Base stats, the eight values the champion screen shows in white, at the Champion's
/// rank, level and ascension;</item>
/// <item><see cref="Artifacts"/>: what the Artifacts it wears add, Glyphs and ascension bonuses included;</item>
/// <item><see cref="Sets"/>: what their sets add;</item>
/// <item><see cref="Masteries"/>: what its masteries add, Lore of Steel's share of its set bonuses included;</item>
/// <item><see cref="GreatHall"/>: what the Account's Great Hall adds for the Champion's Affinity;</item>
/// <item><see cref="FactionGuardians"/>: what the Account's Faction Guardians add for its Faction and rarity.</item>
/// </list>
/// A Stat source holds only the stats it raises, and none when it raises nothing. The other Stat sources, and the
/// Total stats, come one ticket at a time (https://github.com/ANicholasson/raid-bridge/issues/5).
/// </summary>
public sealed record ChampionStats(
	IReadOnlyDictionary<Stat, double> Base,
	IReadOnlyDictionary<Stat, double> Artifacts,
	IReadOnlyDictionary<Stat, double> Sets,
	IReadOnlyDictionary<Stat, double> Masteries,
	IReadOnlyDictionary<Stat, double> GreatHall,
	IReadOnlyDictionary<Stat, double> FactionGuardians);
