using RaidBridge.Domain.Relics;
using RaidBridge.Domain.Stats;
using RaidBridge.Game.Il2Cpp;
using RaidBridge.Game.Stats;

namespace RaidBridge.Game.Relics;

/// <summary>
/// What a worn Relic adds to its Champion's stats, as the game's own <c>RelicSetup.PartialCreateFromRelic</c> and
/// <c>BattleBonuses.InitRelics</c> add it at metadata v39 (https://github.com/ANicholasson/raid-bridge/issues/15): each
/// stat its level has opened, worth what the static table gives that level, as the Relic's own bonuses already hold it.
/// A bonus that isn't absolute adds a share of the Base stat, and any other adds its value, as an Artifact's does. A
/// Relic stone adds skills, not stats.
/// </summary>
/// <remarks>
/// The game sends a bonus that isn't absolute to a share of the Base stat only for a stat its <c>StatCatalog</c> doesn't
/// mark relative. At v39 it marks Resistance, Accuracy and the critical stats relative, so a share of Resistance or
/// Accuracy would add as it is, where <see cref="StatsFormula"/> takes a share of the Base stat. No Relic has one: the
/// static <c>StatValueByLevel</c> holds every stat absolute but CriticalDamage (live check, 2026-11-12). Such a bonus is
/// refused rather than worked out wrong.
/// </remarks>
internal static class RelicStats
{
	public static IEnumerable<StatBonus> Of(Relic relic) =>
		relic.Bonuses
			.Select(bonus => StatNames.BonusOf(bonus.Stat, bonus.Value, bonus.Flat, $"A bonus of Relic {relic.Id}"))
			.OfType<StatBonus>()
			.Select(bonus => bonus is { Flat: false, Stat: Stat.Resistance or Stat.Accuracy }
				? throw new GameDataException(
					$"Relic {relic.Id} gives a share of {bonus.Stat}, which the game adds as it is, not as a share of the Base stat, and raid-bridge doesn't work out.")
				: bonus);
}
