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
	/// <summary>
	/// A Champion's stats from its Base stats, the bonuses of the Artifacts it wears, each with its Glyph, and what
	/// their sets give it.
	/// </summary>
	public static ChampionStats For(IReadOnlyDictionary<Stat, double> baseStats, IEnumerable<StatBonus> artifactBonuses, IEnumerable<StatBonus> setBonuses) =>
		new(
			Base: baseStats,
			Artifacts: Add(baseStats, artifactBonuses),
			Sets: Add(baseStats, setBonuses));

	/// <summary>
	/// What the bonuses add to each stat they raise. A stat no bonus raises is left out, so bonuses that raise nothing
	/// give nothing.
	/// </summary>
	private static SortedDictionary<Stat, double> Add(IReadOnlyDictionary<Stat, double> baseStats, IEnumerable<StatBonus> bonuses)
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
}
