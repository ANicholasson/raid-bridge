using RaidBridge.Domain.Relics;
using RaidBridge.Domain.Stats;
using RaidBridge.Game.Stats;

namespace RaidBridge.Game.Relics;

/// <summary>
/// What a worn Relic adds to its Champion's stats, as the game's own <c>RelicSetup.PartialCreateFromRelic</c> and
/// <c>BattleBonuses.InitRelics</c> add it at metadata v39 (https://github.com/ANicholasson/raid-bridge/issues/15): each
/// stat its level has opened, worth what the static table gives that level, as the Relic's own bonuses already hold it.
/// A bonus that isn't absolute adds a share of the Base stat, and any other adds its value, as an Artifact's does. A
/// Relic stone adds skills, not stats.
/// </summary>
internal static class RelicStats
{
	public static IEnumerable<StatBonus> Of(Relic relic) =>
		relic.Bonuses
			.Select(bonus => (Stat: StatNames.Of(bonus.Stat, $"A bonus of Relic {relic.Id}"), Bonus: bonus))
			.Where(bonus => bonus.Stat is not null)
			.Select(bonus => new StatBonus(bonus.Stat!.Value, bonus.Bonus.Value, bonus.Bonus.Flat));
}
