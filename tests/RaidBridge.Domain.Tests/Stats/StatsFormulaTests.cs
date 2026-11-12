using RaidBridge.Domain.Stats;

namespace RaidBridge.Domain.Tests.Stats;

/// <summary>
/// The cases of the formula the synthetic Game client can't express exactly: a mastery's share of a set bonus that
/// scales a Base stat to exactly a half, which the game rounds to the even whole number
/// (https://github.com/ANicholasson/raid-bridge/issues/12), and Total stats for every stat whatever the Base stats hold
/// (https://github.com/ANicholasson/raid-bridge/issues/15).
/// </summary>
public class StatsFormulaTests
{
	/// <summary>A share of 1/8 of a 1/16 Speed set bonus raises 64 Speed by 1/128 of it, to 64.5, which rounds to 64.</summary>
	[Fact]
	public void Rounds_a_scaled_half_to_the_even_number()
	{
		var stats = StatsFormula.For(
			new Dictionary<Stat, double> { [Stat.Speed] = 64 },
			ChampionBonuses.None with { Sets = [new(Stat.Speed, 0.0625, false)], Masteries = new MasteryBonuses([], [0.125]) });

		Assert.Equal(new Dictionary<Stat, double> { [Stat.Speed] = 0 }, stats.Masteries);
	}

	/// <summary>The Total stats hold all eight stats, the Base stat plus every source, even where nothing raises one.</summary>
	[Fact]
	public void Totals_all_eight_stats()
	{
		var stats = StatsFormula.For(
			new Dictionary<Stat, double> { [Stat.Health] = 1000, [Stat.Speed] = 100 },
			ChampionBonuses.None with { Artifacts = [new(Stat.Health, 0.1, false)], Relic = [new(Stat.Health, 50, true)], ArenaLeague = [new(Stat.Health, 0.25, false)] });

		Assert.Equal(
			new Dictionary<Stat, double>
			{
				[Stat.Health] = 1000 + 100 + 50 + 250,
				[Stat.Attack] = 0,
				[Stat.Defence] = 0,
				[Stat.Speed] = 100,
				[Stat.Resistance] = 0,
				[Stat.Accuracy] = 0,
				[Stat.CriticalChance] = 0,
				[Stat.CriticalDamage] = 0,
			},
			stats.Total);
	}
}
