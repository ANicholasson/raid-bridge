using RaidBridge.Domain.Stats;
using RaidBridge.Game.Testing;
using RaidBridge.Game.Testing.Champions;
using RaidBridge.Game.Testing.FactionGuardians;
using static RaidBridge.Game.Testing.Artifacts.SyntheticArtifactData;

namespace RaidBridge.Game.Tests.FactionGuardians;

/// <summary>
/// What the Account's Faction Guardians add to each Champion's stats, read end to end through the fake memory port
/// (https://github.com/ANicholasson/raid-bridge/issues/13). The synthetic static data gives a Rare Champion 10% Health
/// for its Faction's first full slot and 10% Attack for the second. The archers are Banner Lords Rare, with 3,780 and
/// 6,300 Health and 396 and 660 Attack.
/// </summary>
public sealed class FactionGuardianStatsReadingTests : IDisposable
{
	private const double Unit = 1L << 32;

	private static readonly SyntheticHeroData Heroes = SyntheticHeroData.Default;

	private readonly SyntheticGameClient _game = SyntheticGameClient.Create();

	/// <summary>
	/// The archers fill both places of the Banner Lords' first Rare slot, so each gets its bonus. The knight's Dark Elves
	/// Legendary slots hold it alone, so none is full and it gets nothing.
	/// </summary>
	[Fact]
	public void Adds_the_bonus_of_each_full_slot_of_the_champions_faction_and_rarity()
	{
		var champions = _game.Source().Read().Champions.ToDictionary(champion => champion.Id);

		Assert.Equal(new Dictionary<Stat, double> { [Stat.Health] = 3780 * (Q32(0.1) / Unit) }, champions[7].Stats.FactionGuardians);
		Assert.Equal(new Dictionary<Stat, double> { [Stat.Health] = 6300 * (Q32(0.1) / Unit) }, champions[12].Stats.FactionGuardians);
		Assert.Empty(champions[3].Stats.FactionGuardians);
	}

	/// <summary>Two full slots give the bonuses of the first two slots.</summary>
	[Fact]
	public void Adds_the_bonuses_of_each_full_slot_in_order()
	{
		using var game = WithSlots([new(7, 12), new(20, 21)]);

		Assert.Equal(
			new Dictionary<Stat, double> { [Stat.Health] = 3780 * (Q32(0.1) / Unit), [Stat.Attack] = 396 * (Q32(0.1) / Unit) },
			game.Source().Read().Champions.Single(champion => champion.Id == 7).Stats.FactionGuardians);
	}

	/// <summary>Slots count in order only while they are full, so a full slot after one that isn't gives nothing.</summary>
	[Fact]
	public void Counts_no_full_slot_after_one_that_is_not()
	{
		using var game = WithSlots([new(7, null), new(12, 20)]);

		Assert.All(game.Source().Read().Champions, champion => Assert.Empty(champion.Stats.FactionGuardians));
	}

	[Fact]
	public void Refuses_a_full_slot_the_static_data_has_no_bonuses_for()
	{
		using var game = SyntheticGameClient.Create(guardianData: SyntheticGuardianData.Default with
		{
			BonusesByRarity = SyntheticGuardianData.Default.BonusesByRarity.ToDictionary(rarity => rarity.Key, rarity => rarity.Key == 3 ? [] : rarity.Value),
		});

		Assert.Equal(
			"SharedModel.Meta.Academy.Static.StaticAcademyGuardiansData.BonusesByHeroRarity[Rare] holds the bonuses of 0 slots, but slot 1 is full.",
			Assert.ThrowsAny<GameClientException>(() => game.Source().Read()).Message);
	}

	/// <summary>
	/// The slots are read once, so the Faction Guardians written and what they give the Champions can't come from two
	/// states of a slot the player changes in between (CodeRabbit, PR #22).
	/// </summary>
	[Fact]
	public void Reads_each_guardian_slot_once()
	{
		_game.Source().Read();

		Assert.Equal(1, _game.Memory.TimesRead(_game.GuardianSecondPosition(faction: 1, rarity: 3, index: 0), 1));
	}

	public void Dispose() => _game.Dispose();

	/// <summary>Four Banner Lords Rare Champions, with only these Rare slots of theirs.</summary>
	private static SyntheticGameClient WithSlots(IReadOnlyList<SyntheticGuardianSlot> slots) =>
		SyntheticGameClient.Create(
			heroData: Heroes with { Heroes = [.. Heroes.Heroes, new SyntheticHero(20, 6010) { Grade = 3, Level = 30 }, new SyntheticHero(21, 6010) { Grade = 3, Level = 30 }] },
			guardianData: SyntheticGuardianData.Only(faction: 1, rarity: 3, slots));
}
