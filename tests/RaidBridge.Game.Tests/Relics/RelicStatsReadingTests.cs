using RaidBridge.Domain.Stats;
using RaidBridge.Game.Testing;
using RaidBridge.Game.Testing.Relics;
using static RaidBridge.Game.Testing.Artifacts.SyntheticArtifactData;

namespace RaidBridge.Game.Tests.Relics;

/// <summary>
/// What the Relics a Champion wears add to its stats, and its Total stats, read end to end through the fake memory port
/// (https://github.com/ANicholasson/raid-bridge/issues/15). The knight, Champion 3, wears a Harp at level 15, giving
/// 5,400 Health, 76 Resistance and 18 Speed, and a Grail at level 9, giving 206 Attack and 3,600 Health.
/// </summary>
public sealed class RelicStatsReadingTests : IDisposable
{
	private const double Unit = 1L << 32;

	private readonly SyntheticGameClient _game = SyntheticGameClient.Create();

	[Fact]
	public void Adds_the_bonuses_of_every_relic_the_champion_wears()
	{
		var champions = _game.Source().Read().Champions.ToDictionary(champion => champion.Id);

		Assert.Equal(
			new Dictionary<Stat, double> { [Stat.Health] = 5400 + 3600, [Stat.Attack] = 206, [Stat.Speed] = 18, [Stat.Resistance] = 76 },
			champions[3].Stats.Relics);
		Assert.Empty(champions[12].Stats.Relics);
	}

	/// <summary>At level 15 the Grail opens CriticalDamage, 36%, which adds as it is.</summary>
	[Fact]
	public void Adds_a_critical_bonus_as_it_is()
	{
		using var game = SyntheticGameClient.Create(relicData: SyntheticRelicData.Default with
		{
			Relics = [.. SyntheticRelicData.Default.Relics.Select(relic => relic.Id == 201 ? relic with { Level = 15 } : relic)],
		});

		var knight = game.Source().Read().Champions.Single(champion => champion.Id == 3);

		Assert.Equal(Q32(0.36) / Unit, knight.Stats.Relics[Stat.CriticalDamage]);
	}

	/// <summary>A bonus that isn't absolute adds that share of the Base stat.</summary>
	[Fact]
	public void Adds_a_percentage_bonus_as_a_share_of_the_base_stat()
	{
		using var game = SyntheticGameClient.Create(relicData: SyntheticRelicData.Default with
		{
			StatValueByLevel = new Dictionary<int, IReadOnlyList<SyntheticBonusValue>>(SyntheticRelicData.Default.StatValueByLevel)
			{
				[1] = [.. Enumerable.Repeat(new SyntheticBonusValue(Q32(0.1), Absolute: false), 19)],
			},
		});

		var knight = game.Source().Read().Champions.Single(champion => champion.Id == 3);

		Assert.Equal(knight.Stats.Base[Stat.Health] * (2 * Q32(0.1) / Unit), knight.Stats.Relics[Stat.Health]);
	}

	/// <summary>The game adds a share of Resistance as it is, where raid-bridge would take a share of the Base stat, so it is refused.</summary>
	[Fact]
	public void Refuses_a_share_of_resistance()
	{
		using var game = SyntheticGameClient.Create(relicData: SyntheticRelicData.Default with
		{
			StatValueByLevel = new Dictionary<int, IReadOnlyList<SyntheticBonusValue>>(SyntheticRelicData.Default.StatValueByLevel)
			{
				[5] = [.. Enumerable.Repeat(new SyntheticBonusValue(Q32(0.1), Absolute: false), 19)],
			},
		});

		Assert.Equal(
			"Relic 202 gives a share of Resistance, which the game adds as it is, not as a share of the Base stat, and raid-bridge doesn't work out.",
			Assert.ThrowsAny<GameClientException>(() => game.Source().Read()).Message);
	}

	/// <summary>Each Champion's Total stats are its Base stats plus every Stat source, all eight of them.</summary>
	[Fact]
	public void Totals_every_stat_source()
	{
		var champions = _game.Source().Read().Champions;

		Assert.All(champions, champion => Assert.Equal(Sum(champion.Stats), champion.Stats.Total));
		Assert.Contains(champions, champion => champion.Stats.Relics.Count > 0 && champion.Stats.Artifacts.Count > 0 && champion.Stats.ArenaLeague.Count > 0);
	}

	public void Dispose() => _game.Dispose();

	private static Dictionary<Stat, double> Sum(ChampionStats stats)
	{
		var total = new Dictionary<Stat, double>(stats.Base);
		foreach (var source in new[] { stats.Artifacts, stats.Sets, stats.Masteries, stats.GreatHall, stats.FactionGuardians, stats.Awakening, stats.Empowerment, stats.Relics, stats.ArenaLeague })
		{
			foreach (var (stat, value) in source)
			{
				total[stat] += value;
			}
		}

		return total;
	}
}
