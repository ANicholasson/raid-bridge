using RaidBridge.Domain.Stats;
using RaidBridge.Game.Testing;
using RaidBridge.Game.Testing.Artifacts;
using RaidBridge.Game.Testing.Champions;
using static RaidBridge.Game.Testing.Artifacts.SyntheticArtifactData;

namespace RaidBridge.Game.Tests.Champions;

/// <summary>
/// What a Champion's masteries add to its stats, read end to end through the fake memory port: a synthetic Game
/// client with made-up masteries in, the Champions' <c>stats.bySource.masteries</c> or a named refusal out
/// (https://github.com/ANicholasson/raid-bridge/issues/12). The knight's Base stats are 12,375 Health and 101 Speed,
/// and its helmet and ring complete a Life set for 15% Health.
/// </summary>
public sealed class MasteryStatsReadingTests : IDisposable
{
	private const double Unit = 1L << 32;

	private static readonly SyntheticHeroData Heroes = SyntheticHeroData.Default;

	private readonly SyntheticGameClient _game = SyntheticGameClient.Create();

	/// <summary>
	/// The knight's first mastery adds 75 Attack as it is. Its second adds 15% of its set bonuses: 15% of the Life set's
	/// 15% Health is 2.25%, which raises 12,375 Health to 12,653.4…, rounded to 12,653, so it adds 278. The archer with
	/// an empty list of masteries, and the one with none at all, have no masteries source.
	/// </summary>
	[Fact]
	public void Reads_what_the_masteries_add()
	{
		var champions = _game.Source().Read().Champions.ToDictionary(champion => champion.Id);

		Assert.Equal(new Dictionary<Stat, double> { [Stat.Health] = 278, [Stat.Attack] = 75 }, champions[3].Stats.Masteries);
		Assert.Empty(champions[7].Stats.Masteries);
		Assert.Empty(champions[12].Stats.Masteries);
	}

	/// <summary>A mastery's value adds as it is, a critical stat's as a fraction; a mastery with no stat bonus adds nothing.</summary>
	[Fact]
	public void Adds_a_masterys_value_as_it_is()
	{
		using var game = WithMasteries([500113, 500120], [.. Heroes.MasteryTypes, new(500120, new(Stat: 8, Value: Q32(0.1), Absolute: false))]);

		Assert.Equal(new Dictionary<Stat, double> { [Stat.CriticalDamage] = Q32(0.1) / Unit }, Knight(game).Stats.Masteries);
	}

	/// <summary>
	/// Lore of Steel's share of a Speed set's 12% Speed scales the Base stat and rounds: 101 x 1.018 is 102.8…, so it
	/// adds 2. Its share of the set's 40 Accuracy adds as it is: 6.
	/// </summary>
	[Fact]
	public void Raises_set_bonuses_by_the_set_bonus_share()
	{
		using var game = SyntheticGameClient.Create(
			heroData: Heroes with { Heroes = [.. Heroes.Heroes.Select(hero => hero.Id == 3 ? hero with { Masteries = [500112] } : hero)] },
			artifactData: SyntheticArtifactData.Default with
			{
				Artifacts = [new(Id: 201, Kind: 1) { SetKind = 4 }, new(Id: 202, Kind: 2) { SetKind = 4 }],
				SlotsByHeroId = new Dictionary<int, IReadOnlyDictionary<int, int>> { [3] = new Dictionary<int, int> { [1] = 201, [2] = 202 } },
			});

		Assert.Equal(new Dictionary<Stat, double> { [Stat.Speed] = 2, [Stat.Accuracy] = (Q32(0.15) / Unit) * (Q32(40) / Unit) }, Knight(game).Stats.Masteries);
	}

	/// <summary>
	/// The game adds a stat mastery's value to its stat as it is, whatever its IsAbsolute says, so a Defence mastery of
	/// 0.05 that isn't absolute adds 0.05 points, not 5% of the Base stat.
	/// </summary>
	[Fact]
	public void Adds_a_masterys_value_as_it_is_whatever_it_says_it_is()
	{
		using var game = WithMasteries([500121], [new(500121, new(Stat: 3, Value: Q32(0.05), Absolute: false))]);

		Assert.Equal(new Dictionary<Stat, double> { [Stat.Defence] = Q32(0.05) / Unit }, Knight(game).Stats.Masteries);
	}

	/// <summary>Without a set, a mastery that raises set bonuses adds nothing.</summary>
	[Fact]
	public void Raises_nothing_without_a_set()
	{
		using var game = SyntheticGameClient.Create(artifactData: SyntheticArtifactData.Default with { SlotsByHeroId = new Dictionary<int, IReadOnlyDictionary<int, int>>() });

		Assert.Equal(new Dictionary<Stat, double> { [Stat.Attack] = 75 }, Knight(game).Stats.Masteries);
	}

	[Fact]
	public void Refuses_a_mastery_the_static_data_does_not_hold()
	{
		using var game = WithMasteries([500111, 599999], Heroes.MasteryTypes);

		Assert.Equal(
			"Champion 3 has mastery 599999, which isn't a mastery in the game's SharedModel.Meta.Masteries.StaticMasteryData.MasteryById.",
			Assert.ThrowsAny<GameClientException>(() => game.Source().Read()).Message);
	}

	/// <summary>A corrupt entry or a torn read could key one mastery under another's id (PR #21).</summary>
	[Fact]
	public void Refuses_a_mastery_kept_under_another_id()
	{
		_game.SetMasteryTypeId(key: 500112, id: 500113);

		Assert.Matches(
			@"^SharedModel\.Meta\.Masteries\.MasteryType\.Id at 0x[0-9A-F]+ is 500113, where raid-bridge expects the id MasteryById keeps it under, 500112\.$",
			Assert.ThrowsAny<GameClientException>(() => _game.Source().Read()).Message);
	}

	/// <summary>
	/// A StatKindId over a wider type is refused before its low four bytes could pass for the -1 that marks a bonus to
	/// set bonuses. Nothing else on this Account reads the enum, so the masteries reach it first (PR #21).
	/// </summary>
	[Fact]
	public void Refuses_a_stat_kind_over_a_wider_type_before_reading_the_set_bonus_marker()
	{
		using var game = SyntheticGameClient.Create(
			heroData: Heroes with { Heroes = [new SyntheticHero(3, 5240) { Masteries = [500112] }] },
			artifactData: SyntheticArtifactData.Default with { Artifacts = [], SlotsByHeroId = new Dictionary<int, IReadOnlyDictionary<int, int>>() },
			relicData: Testing.Relics.SyntheticRelicData.None,
			greatHallData: Testing.GreatHall.SyntheticGreatHallData.None,
			guardianData: Testing.FactionGuardians.SyntheticGuardianData.None,
			arenaData: Testing.Arena.SyntheticArenaData.None);
		game.RetypeEnum("SharedModel.Battle.Effects.StatKindId", "System.Int64");

		Assert.Equal(
			"The game's enum SharedModel.Battle.Effects.StatKindId is a System.Int64, where raid-bridge expects a System.Int32.",
			Assert.ThrowsAny<GameClientException>(() => game.Source().Read()).Message);
	}

	public void Dispose() => _game.Dispose();

	private static SyntheticGameClient WithMasteries(IReadOnlyList<int> masteries, IReadOnlyList<SyntheticMasteryType> types) =>
		SyntheticGameClient.Create(heroData: Heroes with
		{
			Heroes = [.. Heroes.Heroes.Select(hero => hero.Id == 3 ? hero with { Masteries = masteries } : hero)],
			MasteryTypes = types,
		});

	private static Domain.Champions.Champion Knight(SyntheticGameClient game) => game.Source().Read().Champions.Single(champion => champion.Id == 3);
}
