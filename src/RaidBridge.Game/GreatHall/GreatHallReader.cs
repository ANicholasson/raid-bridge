using RaidBridge.Domain.GreatHall;
using RaidBridge.Game.Il2Cpp;
using RaidBridge.Game.Metadata;

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

	/// <summary>The static data is read only once a level needs checking, so a Great Hall with no levels doesn't depend on it.</summary>
	public static IReadOnlyList<GreatHallAffinity> Read(ManagedObject userWrapper, ManagedObject staticData)
	{
		var maxLevels = new Lazy<Dictionary<GameEnumMember, int>>(() => MaxLevels(staticData));
		return [.. userWrapper
			.Object("Village", "Client.Model.Gameplay.Village.VillageWrapper")
			.Object("VillageData", "Client.Model.Gameplay.Village.Data.UpdatableVillageData")
			.Dictionary("CapitolBonusLevelByStatByElement", $"System.Collections.Generic.Dictionary`2<{Element}, System.Collections.Generic.Dictionary`2<{StatKind}, System.Int32>>")
			.ByEnumKey(Element)
			.OrderBy(affinity => affinity.Key.Value)
			.Select(affinity => new GreatHallAffinity(affinity.Key.Name, Levels(affinity.Value.DictionaryOrNull(), maxLevels)))
			.Where(affinity => affinity.Levels.Count > 0)];
	}

	/// <summary>
	/// An Affinity the game holds null for has no levels, the same as one it holds an empty dictionary for. A level is
	/// from 1: Raid Toolkit's ArenaExtension reads the bonus of a level at index level - 1, so the game holds no 0.
	/// </summary>
	private static IReadOnlyList<GreatHallLevel> Levels(ManagedDictionary? levels, Lazy<Dictionary<GameEnumMember, int>> maxLevels) =>
		levels is null
			? []
			: [.. levels
				.ByEnumKey(StatKind)
				.OrderBy(stat => stat.Key.Value)
				.Select(stat =>
				{
					var level = stat.Value.Int32();
					return maxLevels.Value.TryGetValue(stat.Key, out var maxLevel)
						? new GreatHallLevel(stat.Key.Name, level.Where(value => value >= 1 && value <= maxLevel, $"a level from 1 to {maxLevel}"))
						: throw new GameDataException($"{level}, but {StaticVillageData}.CapitolBonusByStatKind holds no levels of {stat.Key.Name}.");
				})];

	/// <summary>
	/// The highest level of each stat the Great Hall raises: the number of bonuses the static data lists for it, each a
	/// BonusValue, since level 1 takes the first. A stat it lists no bonus for has no levels, as one it doesn't list.
	/// </summary>
	private static Dictionary<GameEnumMember, int> MaxLevels(ManagedObject staticData) =>
		staticData
			.Object("VillageData", StaticVillageData)
			.Dictionary("CapitolBonusByStatKind", $"System.Collections.Generic.Dictionary`2<{StatKind}, System.Collections.Generic.List`1<SharedModel.Meta.Artifacts.BonusValue>>")
			.ByEnumKey(StatKind)
			.Select(stat => (stat.Key, Count: stat.Value.List().Objects("SharedModel.Meta.Artifacts.BonusValue").Count))
			.Where(stat => stat.Count > 0)
			.ToDictionary(stat => stat.Key, stat => stat.Count);
}
