using RaidBridge.Domain.GreatHall;
using RaidBridge.Domain.Stats;
using RaidBridge.Game.Il2Cpp;
using RaidBridge.Game.Metadata;
using RaidBridge.Game.Stats;

namespace RaidBridge.Game.GreatHall;

/// <summary>
/// Reads the Account's Great Hall from the game's model (spec #32, #35):
/// <c>_userWrapper.Village.VillageData.CapitolBonusLevelByStatByElement</c>, each Affinity's level of each stat, by
/// <c>Element</c> and then by <c>StatKindId</c>, each in the enum's order. Raid Toolkit's ArenaExtension named the
/// route, which the installed game still declares at metadata v39. Each level is checked against the static data's
/// <c>VillageData.CapitolBonusByStatKind</c>, which lists the bonus of each level of each stat the Great Hall raises.
/// The Area Bonuses are beside them in the same VillageData, as <c>ObservatoryBonusLevelByStatByLocation</c>, each
/// area's level of each stat, by <c>ObservatoryLocationId</c> and then by <c>StatKindId</c>
/// (https://github.com/ANicholasson/raid-bridge/issues/32). Each of those levels is checked against the static data's
/// <c>VillageData.ObservatoryTiers</c>, whose tier for the area lists the bonus of each level of each stat.
/// </summary>
internal static class GreatHallReader
{
	private const string Element = "SharedModel.Meta.Heroes.Element";
	private const string StatKind = "SharedModel.Battle.Effects.StatKindId";
	private const string StaticVillageData = "SharedModel.Meta.Village.StaticVillageData";
	private const string BonusValue = "SharedModel.Meta.Artifacts.BonusValue";
	private const string Location = "SharedModel.Meta.Village.ObservatoryLocationId";
	private const string TierInfo = "SharedModel.Meta.Village.ObservatoryTierInfo";
	private const string Levels = $"System.Collections.Generic.Dictionary`2<{StatKind}, System.Int32>";

	/// <summary>
	/// The Account's Great Hall, and what it gives a Champion of each Affinity that has levels: each stat's bonus at its
	/// level, as the game's own <c>BuildingSetup.FromVillageData</c> takes it from the static table
	/// (https://github.com/ANicholasson/raid-bridge/issues/13). The static data is read only once a level needs checking,
	/// so a Great Hall with no levels doesn't depend on it.
	/// </summary>
	public static AccountGreatHall Read(ManagedObject userWrapper, ManagedObject staticData)
	{
		var table = new Lazy<Dictionary<GameEnumMember, IReadOnlyList<ManagedObject>>>(() => Table(staticData));
		var village = userWrapper
			.Object("Village", "Client.Model.Gameplay.Village.VillageWrapper")
			.Object("VillageData", "Client.Model.Gameplay.Village.Data.UpdatableVillageData");
		var affinities = village
			.Dictionary("CapitolBonusLevelByStatByElement", $"System.Collections.Generic.Dictionary`2<{Element}, {Levels}>")
			.ByEnumKey(Element)
			.OrderBy(affinity => affinity.Key.Value)
			.Select(affinity => (Name: affinity.Key.Name, Levels: LevelsOf(affinity.Value.DictionaryOrNull(), table)))
			.Where(affinity => affinity.Levels.Count > 0)
			.ToList();
		return new(
			[.. affinities.Select(affinity => new GreatHallAffinity(affinity.Name, [.. affinity.Levels.Select(level => level.Level)]))],
			Areas(village, staticData),
			affinities.ToDictionary(affinity => affinity.Name, affinity => (IReadOnlyList<StatBonus>)[.. affinity.Levels.SelectMany(level => level.Bonuses)], StringComparer.Ordinal));
	}

	/// <summary>
	/// The Area Bonuses, each area with levels in the order of the game's enum, its stats likewise. An area the game holds
	/// null for has no levels, the same as one it holds an empty dictionary for, and is left out. Each level is from 1 to
	/// the number of bonuses its area's tier lists for the stat, as the Great Hall's are. The tiers are read only once a
	/// level needs checking.
	/// </summary>
	private static IReadOnlyList<GreatHallArea> Areas(ManagedObject village, ManagedObject staticData)
	{
		var tiers = new Lazy<Dictionary<GameEnumMember, Dictionary<GameEnumMember, IReadOnlyList<ManagedObject>>>>(() => Tiers(staticData));
		return [.. village
			.Dictionary("ObservatoryBonusLevelByStatByLocation", $"System.Collections.Generic.Dictionary`2<{Location}, {Levels}>")
			.ByEnumKey(Location)
			.OrderBy(area => area.Key.Value)
			.Select(area => new GreatHallArea(
				area.Key.Name,
				area.Value.DictionaryOrNull() is { } levels
					? [.. levels
						.ByEnumKey(StatKind)
						.OrderBy(stat => stat.Key.Value)
						.Select(stat =>
						{
							var level = stat.Value.Int32();
							if (!tiers.Value.TryGetValue(area.Key, out var table))
							{
								throw new GameDataException($"{level}, but no {TierInfo} in {StaticVillageData}.ObservatoryTiers lists {area.Key.Name}.");
							}

							if (!table.TryGetValue(stat.Key, out var bonuses))
							{
								throw new GameDataException($"{level}, but the {TierInfo} that lists {area.Key.Name} holds no levels of {stat.Key.Name}.");
							}

							return new GreatHallLevel(stat.Key.Name, level.Where(value => value >= 1 && value <= bonuses.Count, $"a level from 1 to {bonuses.Count}"));
						})]
					: []))
			.Where(area => area.Levels.Count > 0)];
	}

	/// <summary>
	/// An Affinity the game holds null for has no levels, the same as one it holds an empty dictionary for. A level is
	/// from 1: Raid Toolkit's ArenaExtension reads the bonus of a level at index level - 1, so the game holds no 0. Each
	/// level comes with its bonus, if it raises a stat the champion screen shows.
	/// </summary>
	private static IReadOnlyList<(GreatHallLevel Level, IReadOnlyList<StatBonus> Bonuses)> LevelsOf(
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
					return (
						new GreatHallLevel(stat.Key.Name, value),
						StatNames.BonusOf(stat.Key.Name, bonuses[value - 1], $"The Great Hall's {stat.Key.Name}") is { } bonus ? (IReadOnlyList<StatBonus>)[bonus] : []);
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

	/// <summary>
	/// Each area's table of bonuses, from the tier that lists it: the bonus of each level of each stat, level 1 taking
	/// the first, a stat with no bonuses left out as in the Great Hall's table. The game finds an area's tier by
	/// <c>VillageExtensions.GetTierInfoOrThrow</c>, so an area two tiers list is refused rather than given either.
	/// </summary>
	private static Dictionary<GameEnumMember, Dictionary<GameEnumMember, IReadOnlyList<ManagedObject>>> Tiers(ManagedObject staticData)
	{
		var tables = new Dictionary<GameEnumMember, Dictionary<GameEnumMember, IReadOnlyList<ManagedObject>>>();
		foreach (var tier in staticData
			.Object("VillageData", StaticVillageData)
			.List("ObservatoryTiers", $"System.Collections.Generic.List`1<{TierInfo}>")
			.Objects(TierInfo))
		{
			var table = tier
				.Dictionary("BonusByStatKind", $"System.Collections.Generic.Dictionary`2<{StatKind}, System.Collections.Generic.List`1<{BonusValue}>>")
				.ByEnumKey(StatKind)
				.Select(stat => (stat.Key, Bonuses: stat.Value.List().Objects(BonusValue)))
				.Where(stat => stat.Bonuses.Count > 0)
				.ToDictionary(stat => stat.Key, stat => stat.Bonuses);
			foreach (var area in tier.List("LocationIds", $"System.Collections.Generic.List`1<{Location}>").EnumMembers(Location))
			{
				if (!tables.TryAdd(area.Value, table))
				{
					throw new GameDataException($"{area.Field} at 0x{area.Address:X} is {area.Value.Name}, which another {TierInfo} in {StaticVillageData}.ObservatoryTiers lists too, where raid-bridge expects one tier for each area.");
				}
			}
		}

		return tables;
	}
}

/// <summary>The Account's Great Hall, its Area Bonuses, and the bonuses the Great Hall gives a Champion of each Affinity, by the Affinity's name.</summary>
internal sealed record AccountGreatHall(
	IReadOnlyList<GreatHallAffinity> Affinities,
	IReadOnlyList<GreatHallArea> Areas,
	IReadOnlyDictionary<string, IReadOnlyList<StatBonus>> BonusesByAffinity);
