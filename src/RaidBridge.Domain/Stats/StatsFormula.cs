using RaidBridge.Domain.Artifacts;

namespace RaidBridge.Domain.Stats;

/// <summary>
/// How a Champion's Stat sources add to its Base stats (ADR-0005), in the shape the game's own
/// <c>BattleStats.UpdateWithBonuses</c> gives them (https://github.com/ANicholasson/raid-bridge/issues/11):
/// <list type="bullet">
/// <item>a flat bonus adds its points;</item>
/// <item>a percentage bonus adds that share of the Base stat;</item>
/// <item>any bonus to <see cref="Stat.CriticalChance"/> or <see cref="Stat.CriticalDamage"/> adds its value as it is,
/// since those stats are fractions already.</item>
/// </list>
/// The sums are unrounded. The champion screen rounds each of its Stat sources to a whole number, or a whole percent,
/// and counts Artifacts and their sets as one source.
/// </summary>
public static class StatsFormula
{
	private static readonly Dictionary<string, Stat> StatsByName = Enum.GetValues<Stat>().ToDictionary(stat => stat.ToString(), StringComparer.Ordinal);

	/// <summary>A Champion's stats from its Base stats, the Artifacts it wears, and what their sets give it.</summary>
	public static ChampionStats For(IReadOnlyDictionary<Stat, double> baseStats, IEnumerable<Artifact> artifacts, IEnumerable<StatBonus> setBonuses) =>
		new(
			Base: baseStats,
			Artifacts: Add(baseStats, artifacts.SelectMany(Bonuses)),
			Sets: Add(baseStats, setBonuses));

	/// <summary>
	/// What the bonuses add to each stat they raise. A stat no bonus raises is left out, so bonuses that raise nothing
	/// give nothing.
	/// </summary>
	public static IReadOnlyDictionary<Stat, double> Add(IReadOnlyDictionary<Stat, double> baseStats, IEnumerable<StatBonus> bonuses)
	{
		var added = new SortedDictionary<Stat, double>();
		foreach (var bonus in bonuses)
		{
			var value = bonus.Flat || bonus.Stat is Stat.CriticalChance or Stat.CriticalDamage
				? bonus.Value
				: baseStats[bonus.Stat] * bonus.Value;
			added[bonus.Stat] = added.GetValueOrDefault(bonus.Stat) + value;
		}

		return added;
	}

	/// <summary>
	/// An Artifact's main stat, Substats and ascension bonus, each with its Glyph. A stat outside the eight the champion
	/// screen shows, which no Artifact raises today, isn't part of the Champion's stats.
	/// </summary>
	private static IEnumerable<StatBonus> Bonuses(Artifact artifact) =>
		new[] { artifact.Primary }
			.Concat(artifact.AscensionBonus is { } ascension ? [ascension] : [])
			.Concat(artifact.Substats)
			.Where(bonus => StatsByName.ContainsKey(bonus.Stat))
			.Select(bonus => new StatBonus(StatsByName[bonus.Stat], bonus.Value + bonus.Glyph, bonus.Flat));
}
