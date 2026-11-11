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
/// Masteries add as the game's <c>HeroExtensions.CalcMasteriesBonus</c> adds them
/// (https://github.com/ANicholasson/raid-bridge/issues/12): a stat mastery adds its value as it is, and a mastery that
/// raises set bonuses, such as Lore of Steel, adds its share of each set bonus. For Resistance, Accuracy and the
/// critical stats that share adds as it is; for the other stats it scales the Base stat, rounded to a whole number,
/// a half going to the even one.
/// The sums are otherwise unrounded. The champion screen rounds each of its Stat sources to a whole number, or a whole
/// percent, and counts Artifacts and their sets as one source.
/// </summary>
public static class StatsFormula
{
	/// <summary>
	/// A Champion's stats from its Base stats, the bonuses of the Artifacts it wears, each with its Glyph, and what
	/// their sets give it.
	/// </summary>
	public static ChampionStats For(
		IReadOnlyDictionary<Stat, double> baseStats,
		IReadOnlyList<StatBonus> artifactBonuses,
		IReadOnlyList<StatBonus> setBonuses,
		MasteryBonuses masteries) =>
		new(
			Base: baseStats,
			Artifacts: Add(baseStats, artifactBonuses),
			Sets: Add(baseStats, setBonuses),
			Masteries: Masteries(baseStats, setBonuses, masteries));

	/// <summary>What the masteries add: each stat mastery's value, then each set bonus share of the set bonuses.</summary>
	private static SortedDictionary<Stat, double> Masteries(IReadOnlyDictionary<Stat, double> baseStats, IReadOnlyList<StatBonus> setBonuses, MasteryBonuses masteries)
	{
		var added = new SortedDictionary<Stat, double>();
		foreach (var bonus in masteries.Stats)
		{
			added[bonus.Stat] = added.GetValueOrDefault(bonus.Stat) + bonus.Value;
		}

		foreach (var share in masteries.SetBonusShares)
		{
			foreach (var (stat, raised) in setBonuses.GroupBy(bonus => bonus.Stat).Select(bonuses => (bonuses.Key, bonuses.Sum(bonus => share * bonus.Value))))
			{
				var value = stat is Stat.Resistance or Stat.Accuracy or Stat.CriticalChance or Stat.CriticalDamage
					? raised
					: Math.Round(baseStats[stat] * (1 + raised), MidpointRounding.ToEven) - baseStats[stat];
				added[stat] = added.GetValueOrDefault(stat) + value;
			}
		}

		return added;
	}

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
