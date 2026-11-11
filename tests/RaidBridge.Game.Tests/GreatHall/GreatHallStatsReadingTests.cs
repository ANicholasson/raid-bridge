using RaidBridge.Domain.Stats;
using RaidBridge.Game.Testing;
using RaidBridge.Game.Testing.GreatHall;
using static RaidBridge.Game.Testing.Artifacts.SyntheticArtifactData;

namespace RaidBridge.Game.Tests.GreatHall;

/// <summary>
/// What the Account's Great Hall adds to each Champion's stats, read end to end through the fake memory port
/// (https://github.com/ANicholasson/raid-bridge/issues/13). The synthetic static data gives Resistance and Accuracy
/// 5 points a level and the other stats 2% a level. The knight is Void, with 12,375 Health; the archers are Force.
/// </summary>
public sealed class GreatHallStatsReadingTests : IDisposable
{
	private const double Unit = 1L << 32;

	private readonly SyntheticGameClient _game = SyntheticGameClient.Create();

	/// <summary>
	/// Void has CriticalDamage at level 2, 4%, which adds as it is, and Health at level 10, 20% of the Base stat. Force,
	/// held as null, has no levels, so the archers have no Great Hall source.
	/// </summary>
	[Fact]
	public void Adds_the_levels_of_the_champions_affinity()
	{
		var champions = _game.Source().Read().Champions.ToDictionary(champion => champion.Id);

		Assert.Equal(
			new Dictionary<Stat, double> { [Stat.Health] = 12375 * (Q32(0.2) / Unit), [Stat.CriticalDamage] = Q32(0.04) / Unit },
			champions[3].Stats.GreatHall);
		Assert.Empty(champions[7].Stats.GreatHall);
	}

	/// <summary>A level of a flat bonus adds its points.</summary>
	[Fact]
	public void Adds_a_flat_level_as_points()
	{
		using var game = SyntheticGameClient.Create(greatHallData: SyntheticGreatHallData.Only(element: 4, new Dictionary<int, int> { [6] = 3 }));

		Assert.Equal(new Dictionary<Stat, double> { [Stat.Accuracy] = 15 }, game.Source().Read().Champions.Single(champion => champion.Id == 3).Stats.GreatHall);
	}

	/// <summary>A level of a stat the champion screen doesn't show is written as a level, but adds nothing to the stats.</summary>
	[Fact]
	public void Writes_a_level_of_a_stat_the_screen_does_not_show_without_adding_it()
	{
		using var game = SyntheticGameClient.Create(greatHallData: new(
			new Dictionary<int, IReadOnlyDictionary<int, int>?> { [4] = new Dictionary<int, int> { [10] = 1 } },
			new Dictionary<int, int> { [10] = 1 }));

		var account = game.Source().Read();

		Assert.Equal("IgnoreDefence", Assert.Single(Assert.Single(account.GreatHall).Levels).Stat);
		Assert.Empty(account.Champions.Single(champion => champion.Id == 3).Stats.GreatHall);
	}

	public void Dispose() => _game.Dispose();
}
