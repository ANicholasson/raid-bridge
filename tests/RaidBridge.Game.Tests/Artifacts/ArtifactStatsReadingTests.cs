using RaidBridge.Domain.Stats;
using RaidBridge.Game.Testing;
using RaidBridge.Game.Testing.Artifacts;
using static RaidBridge.Game.Testing.Artifacts.SyntheticArtifactData;

namespace RaidBridge.Game.Tests.Artifacts;

/// <summary>
/// What a Champion's Artifacts and their sets add to its stats, read end to end through the fake memory port: a
/// synthetic Game client with made-up Artifacts and sets in, the Champions' <c>stats.bySource.artifacts</c> and
/// <c>sets</c> or a named refusal out (https://github.com/ANicholasson/raid-bridge/issues/11). The knight's Base stats are
/// 12,375 Health, 660 Attack, 742 Defence and 101 Speed, as BaseStatsReadingTests reads them.
/// </summary>
public sealed class ArtifactStatsReadingTests : IDisposable
{
	private const double Unit = 1L << 32;

	private static readonly SyntheticArtifactData Artifacts = SyntheticArtifactData.Default;

	private readonly SyntheticGameClient _game = SyntheticGameClient.Create();

	/// <summary>
	/// Flat bonuses add their points, a Glyph adding onto its bonus; the helmet's ascension bonus of 4% Defence adds 4%
	/// of the Base stat, and the weapon's 6% CriticalChance adds 6% as it is. The helmet and the ring complete a Life set
	/// for 15% Health; the weapon alone is one piece short of a Speed set, which adds nothing. A Champion that wears
	/// nothing has neither source.
	/// </summary>
	[Fact]
	public void Reads_what_the_artifacts_and_their_sets_add()
	{
		var champions = _game.Source().Read().Champions.ToDictionary(champion => champion.Id);

		Assert.Equal(
			new Dictionary<Stat, double>
			{
				[Stat.Health] = 2120 + 1640,
				[Stat.Attack] = 265 + 22,
				[Stat.Defence] = 742 * (Q32(0.04) / Unit),
				[Stat.Speed] = 12 + 3,
				[Stat.CriticalChance] = Q32(0.06) / Unit,
			},
			champions[3].Stats.Artifacts);
		Assert.Equal(new Dictionary<Stat, double> { [Stat.Health] = 12375 * (Q32(0.15) / Unit) }, champions[3].Stats.Sets);
		Assert.Empty(champions[12].Stats.Artifacts);
		Assert.Empty(champions[12].Stats.Sets);
	}

	/// <summary>Four pieces of a two-piece set are two sets, so its 12% Speed and 40 Accuracy count twice.</summary>
	[Fact]
	public void Counts_two_complete_sets_of_one_kind()
	{
		using var game = Wearing(setKind: 4, pieces: 4);

		Assert.Equal(
			new Dictionary<Stat, double> { [Stat.Speed] = 2 * 101 * (Q32(0.12) / Unit), [Stat.Accuracy] = 80 },
			Knight(game).Stats.Sets);
	}

	/// <summary>Three pieces of a two-piece set are one set and one piece short.</summary>
	[Fact]
	public void Counts_only_complete_sets()
	{
		using var game = Wearing(setKind: 4, pieces: 3);

		Assert.Equal(
			new Dictionary<Stat, double> { [Stat.Speed] = 101 * (Q32(0.12) / Unit), [Stat.Accuracy] = 40 },
			Knight(game).Stats.Sets);
	}

	/// <summary>
	/// A set that grows by the piece gives what its entry for the number of pieces worn gives, here 20% Health and 15%
	/// CriticalDamage, which adds as it is; its IgnoreDefence isn't one of the eight stats, so it is left out.
	/// </summary>
	[Fact]
	public void Reads_a_set_that_grows_by_the_piece()
	{
		using var game = Wearing(setKind: 9, pieces: 3, GrowingSet);

		Assert.Equal(
			new Dictionary<Stat, double> { [Stat.Health] = 12375 * (Q32(0.2) / Unit), [Stat.CriticalDamage] = Q32(0.15) / Unit },
			Knight(game).Stats.Sets);
	}

	[Fact]
	public void Refuses_a_set_that_grows_by_the_piece_without_an_entry_for_the_pieces_worn()
	{
		using var game = Wearing(setKind: 9, pieces: 4, GrowingSet);

		RefusesMatching(game, @"^Champion 3 wears 4 pieces of the set GrowthByPiece, whose 0 SharedModel\.Meta\.Artifacts\.Sets\.ArtifactSetInfo\.SubSetInfos for that many pieces aren't one\.$");
	}

	[Fact]
	public void Refuses_a_set_that_takes_no_pieces_and_does_not_grow_by_the_piece()
	{
		using var game = Wearing(setKind: 9, pieces: 2, GrowingSet with { SubSets = null, PiecesPerSet = 0 });

		RefusesMatching(game, @"^SharedModel\.Meta\.Artifacts\.Sets\.ArtifactSetInfo\.ArtifactCount at 0x[0-9A-F]+ is 0, where raid-bridge expects the positive number of pieces a set of GrowthByPiece takes, since it has no SubSetInfos\.$");
	}

	[Fact]
	public void Refuses_a_set_kind_the_static_data_does_not_hold()
	{
		using var game = SyntheticGameClient.Create(artifactData: Artifacts with { Sets = [.. Artifacts.Sets.Where(set => set.Kind != 1)] });

		RefusesMatching(game, @"^SharedModel\.Meta\.Artifacts\.Artifact\._setKindId at 0x[0-9A-F]+ is Hp, which isn't a set in the game's StaticArtifactData\.SetInfos\.$");
	}

	/// <summary>A stat a game update adds is refused, not dropped, since raid-bridge can't tell whether the screen shows it.</summary>
	[Fact]
	public void Refuses_a_bonus_to_a_stat_the_reader_does_not_know()
	{
		using var game = Wearing(setKind: 9, pieces: 1, GrowingSet with { SubSets = [(1, [new(Stat: 11, Value: Q32(5), Absolute: true)])] });
		game.AddEnumMember("SharedModel.Battle.Effects.StatKindId", "Vigour", 11);

		RefusesMatching(game, @"^A bonus of the set GrowthByPiece raises Vigour, a stat raid-bridge doesn't know\.$");
	}

	/// <summary>The game adds both parts of a Glyph it can hold onto the bonus, so the Snapshot's Glyph is both.</summary>
	[Fact]
	public void Adds_the_rarity_based_part_of_a_glyph()
	{
		using var game = SyntheticGameClient.Create(artifactData: Artifacts with
		{
			Artifacts = [.. Artifacts.Artifacts.Select(artifact => artifact.Id == 101
				? artifact with { Secondary = [new(Stat: 4, Value: Q32(12), Absolute: true, Glyph: Q32(3), Level: 2, RarityGlyph: Q32(2))] }
				: artifact)],
		});

		var knight = Knight(game);

		Assert.Equal(5, knight.Artifacts.Single(artifact => artifact.Id == 101).Substats.Single().Glyph);
		Assert.Equal(12 + 5, knight.Stats.Artifacts[Stat.Speed]);
	}

	public void Dispose() => _game.Dispose();

	/// <summary>A set kind made up to grow by the piece: one piece gives 10% Health, three give 20% and 15% CriticalDamage, and 15 IgnoreDefence.</summary>
	private static SyntheticArtifactSet GrowingSet { get; } = new(9, new("l10n:artifact-set/name?id=9#static", "Synthetic Growth"))
	{
		SubSets =
		[
			(1, [new(Stat: 1, Value: Q32(0.1), Absolute: false)]),
			(3, [new(Stat: 1, Value: Q32(0.2), Absolute: false), new(Stat: 8, Value: Q32(0.15), Absolute: false), new(Stat: 10, Value: Q32(15), Absolute: false)]),
		],
	};

	/// <summary>The knight wearing <paramref name="pieces"/> Artifacts of one set kind and nothing else, with the set's static data beside the default ones.</summary>
	private static SyntheticGameClient Wearing(int setKind, int pieces, SyntheticArtifactSet? set = null)
	{
		var worn = Enumerable.Range(0, pieces).Select(index => new SyntheticArtifact(Id: 201 + index, Kind: 1 + index) { SetKind = setKind }).ToList();
		return SyntheticGameClient.Create(artifactData: Artifacts with
		{
			Artifacts = worn,
			SlotsByHeroId = new Dictionary<int, IReadOnlyDictionary<int, int>> { [3] = worn.ToDictionary(artifact => artifact.Kind, artifact => artifact.Id) },
			Sets = [.. Artifacts.Sets, .. set is null ? [] : new[] { set }],
		});
	}

	private static Domain.Champions.Champion Knight(SyntheticGameClient game) => game.Source().Read().Champions.Single(champion => champion.Id == 3);

	private static void RefusesMatching(SyntheticGameClient game, string pattern) =>
		Assert.Matches(pattern, Assert.ThrowsAny<GameClientException>(() => game.Source().Read()).Message);
}
