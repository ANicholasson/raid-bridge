using RaidBridge.Domain.GreatHall;
using RaidBridge.Domain.Stats;
using RaidBridge.Game.Il2Cpp;
using RaidBridge.Game.Metadata;
using RaidBridge.Game.Numerics;
using RaidBridge.Game.Stats;

namespace RaidBridge.Game.GreatHall;

/// <summary>
/// Reads the Account's Great Hall from the game's model (spec #32, #35):
/// <c>_userWrapper.Village.VillageData.CapitolBonusLevelByStatByElement</c>, each Affinity's level of each stat, by
/// <c>Element</c> and then by <c>StatKindId</c>, each in the enum's order. Raid Toolkit's ArenaExtension named the
/// route, which the installed game still declares at metadata v39. Each level is checked against the static data's
/// <c>VillageData.CapitolBonusByStatKind</c>, which lists the bonus of each level of each stat the Great Hall raises.
/// </summary>
internal static class GreatHallReader
{
	private const string Element = "SharedModel.Meta.Heroes.Element";
	private const string StatKind = "SharedModel.Battle.Effects.StatKindId";
	private const string StaticVillageData = "SharedModel.Meta.Village.StaticVillageData";
	private const string BonusValue = "SharedModel.Meta.Artifacts.BonusValue";

	/// <summary>
	/// The Account's Great Hall, and what it gives a Champion of each Affinity that has levels: each stat's bonus at its
	/// level, as the game's own <c>BuildingSetup.FromVillageData</c> takes it from the static table
	/// (https://github.com/ANicholasson/raid-bridge/issues/13). The static data is read only once a level needs checking,
	/// so a Great Hall with no levels doesn't depend on it.
	/// </summary>
	public static AccountGreatHall Read(ManagedObject userWrapper, ManagedObject staticData)
	{
		var table = new Lazy<Dictionary<GameEnumMember, IReadOnlyList<ManagedObject>>>(() => Table(staticData));
		var affinities = userWrapper
			.Object("Village", "Client.Model.Gameplay.Village.VillageWrapper")
			.Object("VillageData", "Client.Model.Gameplay.Village.Data.UpdatableVillageData")
			.Dictionary("CapitolBonusLevelByStatByElement", $"System.Collections.Generic.Dictionary`2<{Element}, System.Collections.Generic.Dictionary`2<{StatKind}, System.Int32>>")
			.ByEnumKey(Element)
			.OrderBy(affinity => affinity.Key.Value)
			.Select(affinity => (Name: affinity.Key.Name, Levels: Levels(affinity.Value.DictionaryOrNull(), table)))
			.Where(affinity => affinity.Levels.Count > 0)
			.ToList();
		return new(
			[.. affinities.Select(affinity => new GreatHallAffinity(affinity.Name, [.. affinity.Levels.Select(level => level.Level)]))],
			affinities.ToDictionary(affinity => affinity.Name, affinity => (IReadOnlyList<StatBonus>)[.. affinity.Levels.SelectMany(level => level.Bonus)], StringComparer.Ordinal));
	}

	/// <summary>
	/// An Affinity the game holds null for has no levels, the same as one it holds an empty dictionary for. A level is
	/// from 1: Raid Toolkit's ArenaExtension reads the bonus of a level at index level - 1, so the game holds no 0. Each
	/// level comes with its bonus, if it raises a stat the champion screen shows.
	/// </summary>
	private static IReadOnlyList<(GreatHallLevel Level, IReadOnlyList<StatBonus> Bonus)> Levels(
		ManagedDictionary? levels,
		Lazy<Dictionary<GameEnumMember, IReadOnlyList<ManagedObject>>> table) =>
		levels is null
			? []
			: [.. levels
				.ByEnumKey(StatKind)
				.OrderBy(stat => stat.Key.Value)
				.Select(stat =>
				{
					var level = stat.Value.Int32();
					if (!table.Value.TryGetValue(stat.Key, out var bonuses))
					{
						throw new GameDataException($"{level}, but {StaticVillageData}.CapitolBonusByStatKind holds no levels of {stat.Key.Name}.");
					}

					var value = level.Where(value => value >= 1 && value <= bonuses.Count, $"a level from 1 to {bonuses.Count}");
					var bonus = bonuses[value - 1];
					return (
						new GreatHallLevel(stat.Key.Name, value),
						StatNames.Of(stat.Key.Name, $"The Great Hall's {stat.Key.Name}") is { } shown
							? (IReadOnlyList<StatBonus>)[new StatBonus(shown, FixedPoint.Of(bonus, "_value"), bonus.Boolean("_isAbsolute").Value)]
							: []);
				})];

	/// <summary>
	/// The bonus of each level of each stat the Great Hall raises, each a BonusValue, level 1 taking the first. A stat it
	/// lists no bonus for has no levels, as one it doesn't list.
	/// </summary>
	private static Dictionary<GameEnumMember, IReadOnlyList<ManagedObject>> Table(ManagedObject staticData) =>
		staticData
			.Object("VillageData", StaticVillageData)
			.Dictionary("CapitolBonusByStatKind", $"System.Collections.Generic.Dictionary`2<{StatKind}, System.Collections.Generic.List`1<{BonusValue}>>")
			.ByEnumKey(StatKind)
			.Select(stat => (stat.Key, Bonuses: stat.Value.List().Objects(BonusValue)))
			.Where(stat => stat.Bonuses.Count > 0)
			.ToDictionary(stat => stat.Key, stat => stat.Bonuses);
}

/// <summary>The Account's Great Hall, and the bonuses it gives a Champion of each Affinity, by the Affinity's name.</summary>
internal sealed record AccountGreatHall(IReadOnlyList<GreatHallAffinity> Affinities, IReadOnlyDictionary<string, IReadOnlyList<StatBonus>> BonusesByAffinity);
