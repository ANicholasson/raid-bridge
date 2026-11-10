using RaidBridge.Domain.Stats;
using RaidBridge.Game.Testing;
using RaidBridge.Game.Testing.Arena;
using RaidBridge.Game.Testing.Artifacts;
using RaidBridge.Game.Testing.Champions;
using RaidBridge.Game.Testing.FactionGuardians;
using RaidBridge.Game.Testing.Relics;

namespace RaidBridge.Game.Tests.Champions;

/// <summary>
/// Each Champion's Base stats, read end to end through the fake memory port: a synthetic Game client with made-up
/// base stats and rank multipliers in, the Champions' <c>stats.bySource.base</c> or a named refusal out
/// (https://github.com/ANicholasson/raid-bridge/issues/10). Each expected number is worked out by hand from the
/// synthetic data, in the game's Q32.32 steps: 1.6 is held as the step below it, so 157.5 times it is 251.99999998,
/// which rounds to 252.
/// </summary>
public sealed class BaseStatsReadingTests : IDisposable
{
	private const double FifteenPercent = 644245094 / 4294967296.0;
	private const double FiftySevenPercent = 2448131359 / 4294967296.0;

	private static readonly SyntheticHeroData Heroes = SyntheticHeroData.Default;

	/// <summary>An archer type one ascension up, with a higher base, as the game keeps an ascended Champion's type apart.</summary>
	private static readonly SyntheticHeroType AscendedArcher = new(6013, new("l10n:hero-type/name?id=6010#static", null), Rarity: 3, Fraction: 1, FormElements: [2])
	{
		BaseStats = SyntheticBattleStats.Default with { Health = 76, Attack = 118, Defence = 65 },
	};

	private readonly SyntheticGameClient _game = SyntheticGameClient.Create();

	/// <summary>
	/// At the top of a rank, a stat is its base times the rank's start times its growth: 8.25 at Stars6, 3.6 at Stars3
	/// and 6 at Stars5. The knight's Defence is 742.5 so, and a half goes to the even number. Health is 15 times its
	/// scaled base, Speed, Resistance and Accuracy are the base, and the critical stats are fractions. The archer's
	/// second form holds other stats, which aren't taken: the first is the type's default.
	/// </summary>
	[Fact]
	public void Reads_each_champions_base_stats_at_its_rank_and_level()
	{
		var stats = _game.Source().Read().Champions.ToDictionary(champion => champion.Id, champion => champion.Stats.Base);

		Assert.Equal(Base(12375, 660, 742, 101, 40, 10, FifteenPercent, 0.5), stats[3]);
		Assert.Equal(Base(3780, 396, 216, 98, 30, 0, FifteenPercent, FiftySevenPercent), stats[7]);
		Assert.Equal(Base(6300, 660, 360, 98, 30, 0, FifteenPercent, FiftySevenPercent), stats[12]);
	}

	/// <summary>
	/// Within a rank, a stat grows by the growth raised to how far through the rank's levels the Champion is: level 15
	/// of Stars3's 30 is 14/29 of the way, so the archer's Attack is 110 x 2.25 x 1.6^(14/29), 310.54, which rounds to
	/// 311. Level 1 takes only the start, and Stars1's 10 levels reach its growth at level 10. An ascended Champion
	/// takes its own type's base.
	/// </summary>
	[Fact]
	public void Scales_base_stats_within_a_rank_and_by_ascension()
	{
		using var game = SyntheticGameClient.Create(heroData: Heroes with
		{
			Heroes =
			[
				new(1, 6010) { Grade = 2, Level = 1 },
				new(2, 6010) { Grade = 3, Level = 15 },
				new(4, 6010) { Grade = 1, Level = 10 },
				new(5, 6013) { Grade = 3, Level = 15 },
			],
			HeroTypes = [.. Heroes.HeroTypes, AscendedArcher],
		},
		artifactData: SyntheticArtifactData.Default with { SlotsByHeroId = new Dictionary<int, IReadOnlyDictionary<int, int>>() },
		relicData: SyntheticRelicData.Default with { RelicIdsByHeroId = new Dictionary<int, IReadOnlyList<int>>() },
		guardianData: SyntheticGuardianData.None,
		arenaData: SyntheticArenaData.None);

		var stats = game.Source().Read().Champions.ToDictionary(champion => champion.Id, champion => champion.Stats.Base);

		Assert.Equal(Base(1575, 165, 90, 98, 30, 0, FifteenPercent, FiftySevenPercent), stats[1]);
		Assert.Equal(Base(2970, 311, 169, 98, 30, 0, FifteenPercent, FiftySevenPercent), stats[2]);
		Assert.Equal(Base(2100, 220, 120, 98, 30, 0, FifteenPercent, FiftySevenPercent), stats[4]);
		Assert.Equal(Base(3225, 333, 184, 98, 30, 0, FifteenPercent, FiftySevenPercent), stats[5]);
	}

	/// <summary>An Account with no Champions builds no Base stats, so it doesn't need the multipliers.</summary>
	[Fact]
	public void Reads_an_account_without_champions_without_the_multipliers()
	{
		using var game = SyntheticGameClient.Create(
			heroData: Heroes with { Heroes = [], RankMultipliers = null },
			artifactData: SyntheticArtifactData.Default with { SlotsByHeroId = new Dictionary<int, IReadOnlyDictionary<int, int>>() },
			relicData: SyntheticRelicData.Default with { RelicIdsByHeroId = new Dictionary<int, IReadOnlyList<int>>() },
			guardianData: SyntheticGuardianData.None,
			arenaData: SyntheticArenaData.None);

		Assert.Empty(game.Source().Read().Champions);
	}

	[Fact]
	public void Refuses_a_rank_the_multipliers_do_not_hold()
	{
		using var game = SyntheticGameClient.Create(heroData: Heroes with { RankMultipliers = new[,] { { 1, 2 }, { 1.5, 1.8 }, { 2.25, 1.6 }, { 3, 1.5 }, { 4, 1.5 } } });

		RefusesMatching(game, @"^Champion 3 is at Stars6, but SharedModel\.Meta\.Heroes\.BattleStats\.Multipliers holds the multipliers of 5 ranks\.$");
	}

	[Fact]
	public void Refuses_a_level_past_the_levels_of_the_champions_rank()
	{
		using var game = SyntheticGameClient.Create(heroData: Heroes with { Heroes = [new SyntheticHero(7, 6010) { Grade = 3, Level = 31 }] });

		RefusesMatching(game, @"^SharedModel\.Meta\.Heroes\.Hero\.Level at 0x[0-9A-F]+ is 31, where raid-bridge expects a level from 1 to 30, the levels of Stars3\.$");
	}

	[Fact]
	public void Refuses_a_champion_type_with_no_base_stats()
	{
		using var game = SyntheticGameClient.Create(heroData: Heroes with { HeroTypes = [.. Heroes.HeroTypes.Select(type => type.Id == 6010 ? type with { BaseStats = null } : type)] });

		RefusesMatching(game, @"^SharedModel\.Meta\.Heroes\.HeroForm\.BaseStats at 0x[0-9A-F]+ is null, where raid-bridge expects a SharedModel\.Meta\.Heroes\.BattleStats\.$");
	}

	[Fact]
	public void Refuses_a_base_stat_below_zero()
	{
		using var game = SyntheticGameClient.Create(heroData: Heroes with { HeroTypes = [.. Heroes.HeroTypes.Select(type => type.Id == 6010 ? type with { BaseStats = SyntheticBattleStats.Default with { Defence = -1 } } : type)] });

		RefusesMatching(game, @"^SharedModel\.Meta\.Heroes\.BattleStats\.Defence at 0x[0-9A-F]+ is -4294967296, where raid-bridge expects a stat of 0 or more \(a raw Fixed, in steps of 2\^-32\)\.$");
	}

	/// <summary>A game update that drops or renames a stat the reader takes stops the run by name.</summary>
	[Fact]
	public void Refuses_a_stat_the_game_no_longer_declares()
	{
		_game.UndeclareField("SharedModel.Meta.Heroes.BattleStats", "Accuracy");

		RefusesMatching(_game, @"^The game's class SharedModel\.Meta\.Heroes\.BattleStats has no field Accuracy, base classes included\.$");
	}

	[Fact]
	public void Refuses_missing_multipliers()
	{
		using var game = SyntheticGameClient.Create(heroData: Heroes with { RankMultipliers = null });

		RefusesMatching(game, @"^SharedModel\.Meta\.Heroes\.BattleStats\.Multipliers is null, where raid-bridge expects the rank and level multipliers\.$");
	}

	/// <summary>The metadata declares an array of arrays, so a one-dimensional array is refused by its rank, not read as rows.</summary>
	[Fact]
	public void Refuses_multipliers_that_are_not_a_two_dimensional_array()
	{
		_game.HoldRankMultipliersInOneDimension();

		RefusesMatching(_game, @"^SharedModel\.Meta\.Heroes\.BattleStats\.Multipliers points to an array at 0x[0-9A-F]+ of rank 1, of System\.Double, 8 bytes each, where raid-bridge expects a two-dimensional array of System\.Double, 8 bytes each\.$");
	}

	[Fact]
	public void Refuses_multipliers_whose_lengths_do_not_fit_their_length()
	{
		_game.SetRankMultipliersShape(rows: 6, columns: 3);

		RefusesMatching(_game, @"^SharedModel\.Meta\.Heroes\.BattleStats\.Multipliers points to an array at 0x[0-9A-F]+ of 6 by 3 elements and length 12, which aren't the lengths of a two-dimensional array\.$");
	}

	[Fact]
	public void Refuses_multipliers_with_more_than_a_start_and_a_growth_for_each_rank()
	{
		_game.SetRankMultipliersShape(rows: 4, columns: 3);

		RefusesMatching(_game, @"^SharedModel\.Meta\.Heroes\.BattleStats\.Multipliers holds 3 values for each rank, where raid-bridge expects a start and a growth\.$");
	}

	[Fact]
	public void Refuses_a_multiplier_that_is_not_positive()
	{
		_game.SetRankMultiplier(row: 5, column: 1, value: 0);

		RefusesMatching(_game, @"^SharedModel\.Meta\.Heroes\.BattleStats\.Multipliers\[5, 1\] is 0, where raid-bridge expects a positive number\.$");
	}

	[Fact]
	public void Refuses_a_to_percent_that_is_not_positive()
	{
		using var game = SyntheticGameClient.Create(heroData: Heroes with { ToPercent = 0 });

		RefusesMatching(game, @"^SharedModel\.Meta\.Heroes\.BattleStats\.ToPercent at 0x[0-9A-F]+ is 0, where raid-bridge expects a positive number \(a raw Fixed, in steps of 2\^-32\)\.$");
	}

	/// <summary>Artifacts read in any power-of-two fixed point, but the game's own arithmetic is done in Q32.32 only.</summary>
	[Fact]
	public void Refuses_to_do_the_games_arithmetic_in_another_fixed_point()
	{
		_game.SetConstant("Plarium.Common.Numerics.Fixed", "ONE", 1L << 16);

		RefusesMatching(_game, @"^The game's Plarium\.Common\.Numerics\.Fixed\.ONE is 65536: raid-bridge does the game's own arithmetic in Q32\.32 only, where it is 4294967296\.$");
	}

	public void Dispose() => _game.Dispose();

	private static Dictionary<Stat, double> Base(double health, double attack, double defence, double speed, double resistance, double accuracy, double criticalChance, double criticalDamage) =>
		new()
		{
			[Stat.Health] = health,
			[Stat.Attack] = attack,
			[Stat.Defence] = defence,
			[Stat.Speed] = speed,
			[Stat.Resistance] = resistance,
			[Stat.Accuracy] = accuracy,
			[Stat.CriticalChance] = criticalChance,
			[Stat.CriticalDamage] = criticalDamage,
		};

	private static void RefusesMatching(SyntheticGameClient game, string pattern) =>
		Assert.Matches(pattern, Assert.ThrowsAny<GameClientException>(() => game.Source().Read()).Message);
}
