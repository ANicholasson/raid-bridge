using RaidBridge.Game.Testing.Il2Cpp;
using RaidBridge.Game.Testing.Relics;
using static RaidBridge.Game.Testing.Il2Cpp.Unity6000Layout;

namespace RaidBridge.Game.Testing;

/// <summary>The Relics and Relic stones of a synthetic Game client, and the static data that types them.</summary>
public sealed partial class SyntheticGameClient
{
	private const string Ints = "System.Collections.Generic.List`1<System.Int32>";

	/// <summary>
	/// UserWrapper's Relics: RelicsWrapper, whose read-only base class declares the RelicsData, whose base class
	/// UserRelicData holds the Relics, the stones, and each hero's Relic ids; and the static data's RelicData, declared on
	/// ClientStaticData's base class, with the Relic and stone types by id, each stat's value at each level, and the level
	/// each of a type's stats opens at. The enum lists hold their values unboxed, four bytes each.
	/// </summary>
	private void LayOutTheRelics(ulong userWrapperObject, SyntheticRelicData relicData)
	{
		var systemObject = _runtime.SystemObject;

		var nullableInt = _runtime.Class(
			"System",
			"Nullable`1",
			_runtime.ValueType,
			new SyntheticField("hasValue", BooleanTypeCode, ObjectHeaderSize),
			new SyntheticField("value", Int32TypeCode, ObjectHeaderSize + 0x4));
		var socket = _runtime.Class(
			"SharedModel.Meta.Relics.UserData",
			"RelicStoneSocket",
			systemObject,
			new SyntheticField("ShapeKindId", ValueTypeTypeCode, 0x10),
			new SyntheticField("StoneId", GenericInstanceTypeCode, 0x18, GenericClass: _runtime.GenericClass(nullableInt)));
		_metadata.Class(
			"SharedModel.Meta.Relics.UserData.RelicStoneSocket",
			"System.Object",
			("ShapeKindId", "SharedModel.Meta.Relics.StaticData.RelicStoneShapeKindId", false),
			("StoneId", "System.Nullable`1<System.Int32>", false));
		var relic = Declare(
			"SharedModel.Meta.Relics.UserData",
			"Relic",
			systemObject,
			"System.Object",
			("Id", "System.Int32", Int32TypeCode, 0x10),
			("TypeId", "System.Int32", Int32TypeCode, 0x14),
			("Rank", "SharedModel.Meta.Relics.StaticData.RelicRank", ValueTypeTypeCode, 0x18),
			("Level", "System.Int32", Int32TypeCode, 0x1C),
			("Sockets", "System.Collections.Generic.List`1<SharedModel.Meta.Relics.UserData.RelicStoneSocket>", GenericInstanceTypeCode, 0x20));
		var stone = Declare(
			"SharedModel.Meta.Relics.UserData",
			"RelicStone",
			systemObject,
			"System.Object",
			("Id", "System.Int32", Int32TypeCode, 0x10),
			("TypeId", "System.Int32", Int32TypeCode, 0x14));
		var heroRelicData = Declare("SharedModel.Meta.Relics.UserData", "HeroRelicData", systemObject, "System.Object", ("RelicIds", Ints, GenericInstanceTypeCode, 0x10));
		var userRelicData = Declare(
			"SharedModel.Meta.Relics.UserData",
			"UserRelicData",
			systemObject,
			"System.Object",
			("Relics", "System.Collections.Generic.List`1<SharedModel.Meta.Relics.UserData.Relic>", GenericInstanceTypeCode, 0x10),
			("Stones", "System.Collections.Generic.List`1<SharedModel.Meta.Relics.UserData.RelicStone>", GenericInstanceTypeCode, 0x18),
			("RelicDataByHeroId", "System.Collections.Generic.Dictionary`2<System.Int32, SharedModel.Meta.Relics.UserData.HeroRelicData>", GenericInstanceTypeCode, 0x20));
		var updatableUserRelicData = Declare("Client.Model.Gameplay.Relics", "UpdatableUserRelicData", userRelicData, "SharedModel.Meta.Relics.UserData.UserRelicData");
		var relicsWrapperReadonly = Declare(
			"Client.Model.Gameplay.Relics",
			"RelicsWrapperReadonly",
			systemObject,
			"System.Object",
			("RelicsData", "Client.Model.Gameplay.Relics.UpdatableUserRelicData", ClassTypeCode, 0x10));
		var relicsWrapper = Declare("Client.Model.Gameplay.Relics", "RelicsWrapper", relicsWrapperReadonly, "Client.Model.Gameplay.Relics.RelicsWrapperReadonly");

		var factionSkills = Declare(
			"SharedModel.Meta.Relics.StaticData",
			"RelicFactionSkillBonus",
			systemObject,
			"System.Object",
			("Description", "SharedModel.Common.Localization.SharedLTextKey", ClassTypeCode, 0x10),
			("FractionId", "SharedModel.Meta.Heroes.HeroFraction", ValueTypeTypeCode, 0x18),
			("SkillIds", Ints, GenericInstanceTypeCode, 0x20));
		var relicType = Declare(
			"SharedModel.Meta.Relics.StaticData",
			"RelicType",
			systemObject,
			"System.Object",
			("Id", "System.Int32", Int32TypeCode, 0x10),
			("Name", "SharedModel.Common.Localization.SharedLTextKey", ClassTypeCode, 0x18),
			("Description", "SharedModel.Common.Localization.SharedLTextKey", ClassTypeCode, 0x20),
			("Rarity", "SharedModel.Meta.ItemRarity", ValueTypeTypeCode, 0x28),
			("SkillIds", Ints, GenericInstanceTypeCode, 0x30),
			("SkillIdsByFaction", "System.Collections.Generic.List`1<SharedModel.Meta.Relics.StaticData.RelicFactionSkillBonus>", GenericInstanceTypeCode, 0x38),
			("StatsBonus", "System.Collections.Generic.List`1<SharedModel.Battle.Effects.StatKindId>", GenericInstanceTypeCode, 0x40));
		var stoneType = Declare(
			"SharedModel.Meta.Relics.StaticData",
			"RelicStoneType",
			systemObject,
			"System.Object",
			("Id", "System.Int32", Int32TypeCode, 0x10),
			("Name", "SharedModel.Common.Localization.SharedLTextKey", ClassTypeCode, 0x18),
			("Description", "SharedModel.Common.Localization.SharedLTextKey", ClassTypeCode, 0x20),
			("Rarity", "SharedModel.Meta.ItemRarity", ValueTypeTypeCode, 0x28),
			("Shape", "SharedModel.Meta.Relics.StaticData.RelicStoneShapeKindId", ValueTypeTypeCode, 0x2C),
			("SkillIds", Ints, GenericInstanceTypeCode, 0x30));
		var staticRelicData = Declare(
			"SharedModel.Meta.Relics.StaticData",
			"StaticRelicData",
			systemObject,
			"System.Object",
			("_relicTypeById", "System.Collections.Generic.Dictionary`2<System.Int32, SharedModel.Meta.Relics.StaticData.RelicType>", GenericInstanceTypeCode, 0x10),
			("_relicStoneTypeById", "System.Collections.Generic.Dictionary`2<System.Int32, SharedModel.Meta.Relics.StaticData.RelicStoneType>", GenericInstanceTypeCode, 0x18),
			("StatValueByLevel", "System.Collections.Generic.Dictionary`2<SharedModel.Battle.Effects.StatKindId, System.Collections.Generic.List`1<SharedModel.Meta.Artifacts.BonusValue>>", GenericInstanceTypeCode, 0x20),
			("StatBonusOpeningLevels", Ints, GenericInstanceTypeCode, 0x28));
		var statKind = _runtime.Class("SharedModel.Battle.Effects", "StatKindId", _runtime.ValueType);

		var ints = _runtime.ListType(_runtime.SystemInt32, "Int32", sizeof(int));
		ulong IntList(IEnumerable<int> values) => _runtime.List(ints, [.. values.Select(value => unchecked((ulong)value))]);
		var byInt = _runtime.DictionaryType(Int32TypeCode, ClassTypeCode);

		// The user's data: each Relic with its sockets, each stone, and each hero's Relic ids.
		var sockets = _runtime.ListType(socket, "RelicStoneSocket", sizeof(ulong));
		var relics = relicData.Relics.Select(synthetic =>
		{
			var relicObject = _runtime.Object(relic, 0x28);
			_heap.Int32(relicObject + 0x10, synthetic.Id);
			_heap.Int32(relicObject + 0x14, synthetic.TypeId);
			_heap.Int32(relicObject + 0x18, synthetic.Rank);
			_heap.Int32(relicObject + 0x1C, synthetic.Level);
			_heap.Pointer(relicObject + 0x20, _runtime.List(sockets, [.. synthetic.Sockets.Select(syntheticSocket =>
			{
				var socketObject = _runtime.Object(socket, 0x20);
				_heap.Int32(socketObject + 0x10, syntheticSocket.Shape);
				if (syntheticSocket.StoneId is { } stoneId)
				{
					_heap.Bytes(socketObject + 0x18, [1]);
					_heap.Int32(socketObject + 0x1C, stoneId);
				}

				return socketObject;
			})]));
			return relicObject;
		});
		var stones = relicData.Stones.Select(synthetic =>
		{
			var stoneObject = _runtime.Object(stone, 0x18);
			_heap.Int32(stoneObject + 0x10, synthetic.Id);
			_heap.Int32(stoneObject + 0x14, synthetic.TypeId);
			return stoneObject;
		});
		var heroes = relicData.RelicIdsByHeroId.Select(hero =>
		{
			var heroRelicDataObject = _runtime.Object(heroRelicData, 0x18);
			_heap.Pointer(heroRelicDataObject + 0x10, IntList(hero.Value));
			return ((ulong)hero.Key, heroRelicDataObject);
		});

		var userRelicDataObject = _runtime.Object(updatableUserRelicData, 0x28);
		_heap.Pointer(userRelicDataObject + 0x10, _runtime.List(_runtime.ListType(relic, "Relic", sizeof(ulong)), [.. relics]));
		_heap.Pointer(userRelicDataObject + 0x18, _runtime.List(_runtime.ListType(stone, "RelicStone", sizeof(ulong)), [.. stones]));
		_heap.Pointer(userRelicDataObject + 0x20, _runtime.Dictionary(byInt, [.. heroes]));
		var relicsWrapperObject = _runtime.Object(relicsWrapper, 0x18);
		_heap.Pointer(relicsWrapperObject + 0x10, userRelicDataObject);
		Slot("Client.Model.Guard.UserWrapper.Relics", userWrapperObject + 0x68, relicsWrapperObject);

		// The static data: the types, each stat's values by level, and the opening levels.
		var factionSkillList = _runtime.ListType(factionSkills, "RelicFactionSkillBonus", sizeof(ulong));
		var statKinds = _runtime.ListType(statKind, "StatKindId", sizeof(int));
		var relicTypes = relicData.RelicTypes.Select(synthetic =>
		{
			var typeObject = _runtime.Object(relicType, 0x48);
			_heap.Int32(typeObject + 0x10, synthetic.Id);
			_heap.Pointer(typeObject + 0x18, Text(synthetic.Name));
			_heap.Pointer(typeObject + 0x20, Text(synthetic.Description));
			_heap.Int32(typeObject + 0x28, synthetic.Rarity);
			_heap.Pointer(typeObject + 0x30, IntList(synthetic.SkillIds));
			if (synthetic.FactionSkills is { } bonuses)
			{
				_heap.Pointer(typeObject + 0x38, _runtime.List(factionSkillList, [.. bonuses.Select(bonus =>
				{
					var bonusObject = _runtime.Object(factionSkills, 0x28);
					_heap.Pointer(bonusObject + 0x10, Text(bonus.Description));
					_heap.Int32(bonusObject + 0x18, bonus.Fraction);
					_heap.Pointer(bonusObject + 0x20, IntList(bonus.SkillIds));
					return bonusObject;
				})]));
			}

			_heap.Pointer(typeObject + 0x40, _runtime.List(statKinds, [.. synthetic.Stats.Select(value => (ulong)value)]));
			return ((ulong)synthetic.Id, typeObject);
		});
		var stoneTypes = relicData.StoneTypes.Select(synthetic =>
		{
			var typeObject = _runtime.Object(stoneType, 0x38);
			_heap.Int32(typeObject + 0x10, synthetic.Id);
			_heap.Pointer(typeObject + 0x18, Text(synthetic.Name));
			_heap.Pointer(typeObject + 0x20, Text(synthetic.Description));
			_heap.Int32(typeObject + 0x28, synthetic.Rarity);
			_heap.Int32(typeObject + 0x2C, synthetic.Shape);
			_heap.Pointer(typeObject + 0x30, IntList(synthetic.SkillIds));
			return ((ulong)synthetic.Id, typeObject);
		});

		var bonusValue = _runtime.RuntimeClass("SharedModel.Meta.Artifacts.BonusValue");
		var bonusValues = _runtime.ListType(bonusValue, "BonusValue", sizeof(ulong));
		var values = _runtime.Dictionary(_runtime.DictionaryType(ValueTypeTypeCode, ClassTypeCode), [.. relicData.StatValueByLevel.Select(stat => (
			(ulong)stat.Key,
			_runtime.List(bonusValues, [.. stat.Value.Select(value =>
			{
				var valueObject = _runtime.Object(bonusValue, 0x20);
				_heap.Bytes(valueObject + 0x10, [Bool(value.Absolute)]);
				_heap.Int64(valueObject + 0x18, value.Value);
				return valueObject;
			})])))]);

		var staticRelicDataObject = _runtime.Object(staticRelicData, 0x30);
		_heap.Pointer(staticRelicDataObject + 0x10, _runtime.Dictionary(byInt, [.. relicTypes]));
		_heap.Pointer(staticRelicDataObject + 0x18, _runtime.Dictionary(byInt, [.. stoneTypes]));
		_heap.Pointer(staticRelicDataObject + 0x20, values);
		_heap.Pointer(staticRelicDataObject + 0x28, IntList(relicData.OpeningLevels));
		Slot("SharedModel.Meta.StaticData.RelicData", _clientStaticData + 0x40, staticRelicDataObject);
	}
}
