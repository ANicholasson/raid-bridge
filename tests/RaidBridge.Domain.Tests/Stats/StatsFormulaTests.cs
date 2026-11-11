using RaidBridge.Domain.Stats;

namespace RaidBridge.Domain.Tests.Stats;

/// <summary>
/// The one case of the formula the synthetic Game client can't express exactly: a mastery's share of a set bonus that
/// scales a Base stat to exactly a half, which the game rounds to the even whole number
/// (https://github.com/ANicholasson/raid-bridge/issues/12).
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
}
