using RaidBridge.Domain.Champions;
using RaidBridge.Domain.Stats;
using RaidBridge.Game.Testing;
using RaidBridge.Game.Testing.Champions;
using static RaidBridge.Game.Testing.Artifacts.SyntheticArtifactData;

namespace RaidBridge.Game.Tests.Champions;

/// <summary>
/// What a Champion's Awakening, through its blessing, and its Empowerment add to its stats, read end to end through the
/// fake memory port (https://github.com/ANicholasson/raid-bridge/issues/14). The synthetic static data's Necromancy
/// raises Health at Stars1, Attack at Stars2, and Health and CriticalDamage at Stars3, worth 7,500, 750, 1,000 and 38%
/// for a Legendary; Empowerment level 1 gives 10 Resistance and 10 Accuracy, level 2 5 Speed, and an Epic's level 1 5%
/// Health. The knight is a Legendary awakened to Stars2 with Necromancy and empowered to level 1.
/// </summary>
public sealed class AwakeningAndEmpowermentStatsReadingTests : IDisposable
{
	private const double Unit = 1L << 32;

	private static readonly SyntheticHeroData Heroes = SyntheticHeroData.Default;

	/// <summary>An Epic Banner Lords type of the synthetic data.</summary>
	private static readonly SyntheticHeroType Epic = new(7000, new("l10n:hero-type/name?id=6010#static", null), Rarity: 4, Fraction: 1, FormElements: [2])
	{
		BaseStats = SyntheticBattleStats.Default with { Health = 80 },
	};

	private readonly SyntheticGameClient _game = SyntheticGameClient.Create();

	/// <summary>
	/// The knight gets each Awakening grade's stats up to its own, and its Empowerment level's. The archer awakened to
	/// Stars1 without a blessing gets nothing from it, as the game gives it nothing; the other archer has neither.
	/// </summary>
	[Fact]
	public void Adds_the_awakening_of_a_blessed_champion_and_its_empowerment()
	{
		var champions = _game.Source().Read().Champions.ToDictionary(champion => champion.Id);

		Assert.Equal(new Dictionary<Stat, double> { [Stat.Health] = 7500, [Stat.Attack] = 750 }, champions[3].Stats.Awakening);
		Assert.Equal(new Dictionary<Stat, double> { [Stat.Resistance] = 10, [Stat.Accuracy] = 10 }, champions[3].Stats.Empowerment);
		Assert.Empty(champions[12].Stats.Awakening);
		Assert.All(new[] { champions[7], champions[12] }, champion => Assert.Empty(champion.Stats.Empowerment));
	}

	/// <summary>Every grade up to the Champion's adds, so Stars3 adds Health twice and CriticalDamage as it is.</summary>
	[Fact]
	public void Adds_every_awakening_grade_up_to_the_champions()
	{
		using var game = WithKnight(new SyntheticHero(3, 5240) { DoubleAscend = new(Grade: 3, Blessing: 1101) });

		Assert.Equal(
			new Dictionary<Stat, double> { [Stat.Health] = 8500, [Stat.Attack] = 750, [Stat.CriticalDamage] = Q32(0.38) / Unit },
			Knight(game).Stats.Awakening);
	}

	/// <summary>Empowerment levels add up: level 2 gives level 1's bonuses and its own.</summary>
	[Fact]
	public void Adds_every_empowerment_level_up_to_the_champions()
	{
		using var game = WithKnight(new SyntheticHero(3, 5240) { EmpowerLevel = 2 });

		Assert.Equal(new Dictionary<Stat, double> { [Stat.Speed] = 5, [Stat.Resistance] = 10, [Stat.Accuracy] = 10 }, Knight(game).Stats.Empowerment);
	}

	/// <summary>An Epic Champion takes the Epic levels, here 5% of its Base Health; without them, the others.</summary>
	[Fact]
	public void Empowers_an_epic_champion_by_the_epic_levels()
	{
		using var game = WithKnight(new SyntheticHero(3, 7000) { EmpowerLevel = 1 });
		using var withoutEpicLevels = WithKnight(new SyntheticHero(3, 7000) { EmpowerLevel = 1 }, Heroes with { EpicEmpowerBonuses = null });

		var health = Knight(game).Stats.Base[Stat.Health];
		Assert.Equal(new Dictionary<Stat, double> { [Stat.Health] = health * (Q32(0.05) / Unit) }, Knight(game).Stats.Empowerment);
		Assert.Equal(new Dictionary<Stat, double> { [Stat.Resistance] = 10, [Stat.Accuracy] = 10 }, Knight(withoutEpicLevels).Stats.Empowerment);
	}

	[Fact]
	public void Refuses_a_blessing_the_static_data_does_not_hold()
	{
		using var game = WithKnight(new SyntheticHero(3, 5240) { DoubleAscend = new(Grade: 1, Blessing: 1102) }, Heroes with { Blessings = [.. Heroes.Blessings.Where(blessing => blessing.Id != 1102)] });

		Assert.Equal(
			"Champion 3 has the blessing TimeSlowdown, which isn't a blessing in the game's SharedModel.Meta.DoubleAscend.Static.StaticDoubleAscendData.BlessingById.",
			Refusal(game));
	}

	[Fact]
	public void Refuses_a_blessed_grade_the_static_data_has_no_stats_for()
	{
		using var game = WithKnight(new SyntheticHero(3, 5240) { DoubleAscend = new(Grade: 1, Blessing: 1102) });

		Assert.Equal(
			"The blessing TimeSlowdown raises Speed at Stars1, but SharedModel.Meta.DoubleAscend.Static.StaticDoubleAscendData.BlessingStatsByRarity has no Speed for a Legendary Champion at Stars1.",
			Refusal(game));
	}

	[Fact]
	public void Refuses_an_empowerment_level_the_static_data_does_not_hold()
	{
		using var game = WithKnight(new SyntheticHero(3, 5240) { EmpowerLevel = 3 });

		Assert.Equal(
			"Champion 3 is a Legendary Champion empowered to level 3, but SharedModel.Meta.Academy.Static.StaticAcademyEmpowerData.Bonuses holds 2 levels.",
			Refusal(game));
	}

	public void Dispose() => _game.Dispose();

	/// <summary>The default Account with the knight, Champion 3, replaced, the Epic type beside the others, and no Faction Guardians.</summary>
	private static SyntheticGameClient WithKnight(SyntheticHero knight, SyntheticHeroData? heroes = null)
	{
		var data = heroes ?? Heroes;
		// The knight may change type, so it guards nowhere.
		return SyntheticGameClient.Create(
			heroData: data with
			{
				Heroes = [.. data.Heroes.Select(hero => hero.Id == 3 ? knight with { Skills = hero.Skills } : hero)],
				HeroTypes = [.. data.HeroTypes, Epic],
			},
			guardianData: Testing.FactionGuardians.SyntheticGuardianData.None);
	}

	private static Champion Knight(SyntheticGameClient game) => game.Source().Read().Champions.Single(champion => champion.Id == 3);

	private static string Refusal(SyntheticGameClient game) => Assert.ThrowsAny<GameClientException>(() => game.Source().Read()).Message;
}
