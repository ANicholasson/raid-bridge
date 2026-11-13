using RaidBridge.Game.Testing.GreatHall;
using static RaidBridge.Game.Testing.Il2Cpp.Unity6000Layout;

namespace RaidBridge.Game.Testing;

/// <summary>The Great Hall of a synthetic Game client, and what a bad read can do to it.</summary>
public sealed partial class SyntheticGameClient
{
	private readonly Dictionary<int, ulong> _greatHallLevels = [];
	private readonly Dictionary<int, ulong> _greatHallBonuses = [];
	private readonly Dictionary<int, ulong> _areaLevels = [];
	private ulong _greatHall;
	private ulong _areas;
	private ulong _tiers;

	/// <summary>The Great Hall's CapitolBonusLevelByStatByElement has a count and free count that disagree with its slots, as a torn or corrupt read might see them.</summary>
	public void SetGreatHallCounts(int count, int freeCount) => _runtime.SetDictionaryCounts(_greatHall, count, freeCount);

	/// <summary>The value in a slot of the Great Hall's CapitolBonusLevelByStatByElement points somewhere else, as a bad read might see it.</summary>
	public void PointGreatHallAffinity(int slot, ulong address) => _heap.Pointer(_runtime.EntryValue(_greatHall, slot), address);

	/// <summary>An Affinity's levels, by its Element, have a count and free count that disagree with their slots, as a torn or corrupt read might see them.</summary>
	public void SetGreatHallLevelCounts(int element, int count, int freeCount) => _runtime.SetDictionaryCounts(_greatHallLevels[element], count, freeCount);

	/// <summary>One of the bonuses the static data lists for a stat, by its StatKindId, points somewhere else, as a bad read might see it.</summary>
	public void PointGreatHallBonus(int stat, int index, ulong address) => _heap.Pointer(_runtime.ListItem(_greatHallBonuses[stat], index), address);

	/// <summary>The Area Bonuses' ObservatoryBonusLevelByStatByLocation has a count and free count that disagree with its slots, as a torn or corrupt read might see them.</summary>
	public void SetAreaBonusCounts(int count, int freeCount) => _runtime.SetDictionaryCounts(_areas, count, freeCount);

	/// <summary>The value in a slot of ObservatoryBonusLevelByStatByLocation points somewhere else, as a bad read might see it.</summary>
	public void PointAreaBonusArea(int slot, ulong address) => _heap.Pointer(_runtime.EntryValue(_areas, slot), address);

	/// <summary>An area's levels, by its ObservatoryLocationId, have a count and free count that disagree with their slots, as a torn or corrupt read might see them.</summary>
	public void SetAreaBonusLevelCounts(int area, int count, int freeCount) => _runtime.SetDictionaryCounts(_areaLevels[area], count, freeCount);

	/// <summary>One of the static data's ObservatoryTiers points somewhere else, as a bad read might see it.</summary>
	public void PointAreaBonusTier(int index, ulong address) => _heap.Pointer(_runtime.ListItem(_tiers, index), address);

	/// <summary>
	/// UserWrapper's Village, whose read-only base class declares the VillageData, whose base class UserVillageData holds
	/// each Affinity's levels by its Element and each area's Area Bonus levels by its ObservatoryLocationId, each a
	/// dictionary of levels by StatKindId or null; and the static data's VillageData, whose CapitolBonusByStatKind lists a
	/// bonus for each level of each stat the Great Hall raises, and whose ObservatoryTiers list the same for the stats
	/// of each tier's areas.
	/// </summary>
	private void LayOutTheGreatHall(ulong userWrapperObject, SyntheticGreatHallData greatHallData)
	{
		const string Levels = "System.Collections.Generic.Dictionary`2<SharedModel.Battle.Effects.StatKindId, System.Int32>";
		const string Location = "SharedModel.Meta.Village.ObservatoryLocationId";
		const string Bonuses = "System.Collections.Generic.Dictionary`2<SharedModel.Battle.Effects.StatKindId, System.Collections.Generic.List`1<SharedModel.Meta.Artifacts.BonusValue>>";
		var systemObject = _runtime.SystemObject;

		var userVillageData = Declare(
			"SharedModel.Meta.Village",
			"UserVillageData",
			systemObject,
			"System.Object",
			("CapitolBonusLevelByStatByElement", $"System.Collections.Generic.Dictionary`2<SharedModel.Meta.Heroes.Element, {Levels}>", GenericInstanceTypeCode, 0x10),
			("ObservatoryBonusLevelByStatByLocation", $"System.Collections.Generic.Dictionary`2<{Location}, {Levels}>", GenericInstanceTypeCode, 0x18));
		var updatableVillageData = Declare("Client.Model.Gameplay.Village.Data", "UpdatableVillageData", userVillageData, "SharedModel.Meta.Village.UserVillageData");
		var villageWrapperReadOnly = Declare(
			"Client.Model.Gameplay.Village",
			"VillageWrapperReadOnly",
			systemObject,
			"System.Object",
			("VillageData", "Client.Model.Gameplay.Village.Data.UpdatableVillageData", ClassTypeCode, 0x10));
		var villageWrapper = Declare("Client.Model.Gameplay.Village", "VillageWrapper", villageWrapperReadOnly, "Client.Model.Gameplay.Village.VillageWrapperReadOnly");
		var staticVillageData = Declare(
			"SharedModel.Meta.Village",
			"StaticVillageData",
			systemObject,
			"System.Object",
			("CapitolBonusByStatKind", Bonuses, GenericInstanceTypeCode, 0x10),
			("ObservatoryTiers", "System.Collections.Generic.List`1<SharedModel.Meta.Village.ObservatoryTierInfo>", GenericInstanceTypeCode, 0x18));
		var tierInfo = Declare(
			"SharedModel.Meta.Village",
			"ObservatoryTierInfo",
			systemObject,
			"System.Object",
			("TierId", "System.Int32", Int32TypeCode, 0x10),
			("LocationIds", $"System.Collections.Generic.List`1<{Location}>", GenericInstanceTypeCode, 0x18),
			("BonusByStatKind", Bonuses, GenericInstanceTypeCode, 0x20));

		var levelsType = _runtime.DictionaryType(ValueTypeTypeCode, Int32TypeCode);
		_greatHall = _runtime.Dictionary(_runtime.DictionaryType(ValueTypeTypeCode, ClassTypeCode), [.. greatHallData.Levels.Select(affinity => (
			(ulong)affinity.Key,
			affinity.Value is { } levels
				? _greatHallLevels[affinity.Key] = _runtime.Dictionary(levelsType, [.. levels.Select(level => ((ulong)level.Key, unchecked((ulong)level.Value)))])
				: 0UL))]);
		_areas = _runtime.Dictionary(_runtime.DictionaryType(ValueTypeTypeCode, ClassTypeCode), [.. greatHallData.Areas.Select(area => (
			(ulong)area.Key,
			area.Value is { } levels
				? _areaLevels[area.Key] = _runtime.Dictionary(levelsType, [.. levels.Select(level => ((ulong)level.Key, unchecked((ulong)level.Value)))])
				: 0UL))]);
		var updatableVillageDataObject = _runtime.Object(updatableVillageData, 0x20);
		_heap.Pointer(updatableVillageDataObject + 0x10, _greatHall);
		_heap.Pointer(updatableVillageDataObject + 0x18, _areas);
		var villageWrapperObject = _runtime.Object(villageWrapper, 0x18);
		_heap.Pointer(villageWrapperObject + 0x10, updatableVillageDataObject);
		Slot("Client.Model.Guard.UserWrapper.Village", userWrapperObject + 0x48, villageWrapperObject);

		var bonusValue = _runtime.RuntimeClass("SharedModel.Meta.Artifacts.BonusValue");
		var bonusValues = _runtime.ListType(bonusValue, "BonusValue", sizeof(ulong));
		ulong BonusList(int stat, int count) => _runtime.List(bonusValues, [.. Enumerable.Range(1, count).Select(level =>
		{
			var (value, absolute) = SyntheticGreatHallData.Bonus(stat, level);
			var bonusObject = _runtime.Object(bonusValue, 0x20);
			_heap.Bytes(bonusObject + 0x10, [Bool(absolute)]);
			_heap.Int64(bonusObject + 0x18, value);
			return bonusObject;
		})]);
		var byStat = _runtime.DictionaryType(ValueTypeTypeCode, ClassTypeCode);
		var bonuses = _runtime.Dictionary(byStat, [.. greatHallData.MaxLevels.Select(stat => ((ulong)stat.Key, _greatHallBonuses[stat.Key] = BonusList(stat.Key, stat.Value)))]);
		var locations = _runtime.ListType(_runtime.Class("SharedModel.Meta.Village", "ObservatoryLocationId", _runtime.ValueType), "ObservatoryLocationId", sizeof(int));
		_tiers = _runtime.List(_runtime.ListType(tierInfo, "ObservatoryTierInfo", sizeof(ulong)), [.. greatHallData.Tiers.Select((tier, index) =>
		{
			var tierObject = _runtime.Object(tierInfo, 0x28);
			_heap.Int32(tierObject + 0x10, index + 1);
			_heap.Pointer(tierObject + 0x18, _runtime.List(locations, [.. tier.Areas.Select(area => (ulong)area)]));
			_heap.Pointer(tierObject + 0x20, _runtime.Dictionary(byStat, [.. tier.MaxLevels.Select(stat => ((ulong)stat.Key, BonusList(stat.Key, stat.Value)))]));
			return tierObject;
		})]);
		var staticVillageDataObject = _runtime.Object(staticVillageData, 0x20);
		_heap.Pointer(staticVillageDataObject + 0x10, bonuses);
		_heap.Pointer(staticVillageDataObject + 0x18, _tiers);
		Slot("SharedModel.Meta.ClientStaticData.VillageData", _clientStaticData + 0x38, staticVillageDataObject);
	}
}
