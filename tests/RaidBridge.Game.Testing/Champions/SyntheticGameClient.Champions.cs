using RaidBridge.Game.Testing.Champions;
using RaidBridge.Game.Testing.Il2Cpp;
using static RaidBridge.Game.Testing.Il2Cpp.Unity6000Layout;

namespace RaidBridge.Game.Testing;

/// <summary>The heroes of a synthetic Game client, in HeroById, and what a bad read can do to them; the static data that types them is laid out with the rest of ClientStaticData.</summary>
public sealed partial class SyntheticGameClient
{
	private readonly Dictionary<int, ulong> _skillLists = [];
	private readonly Dictionary<int, ulong> _heroTypes = [];
	private readonly Dictionary<int, ulong> _skillTypes = [];
	private ulong _heroById;

	/// <summary>A hero's skill list claims more skills than its array holds, as a torn or corrupt read might see it.</summary>
	public void SetSkillCount(int heroId, int count) => _runtime.SetListSize(_skillLists[heroId], count);

	/// <summary>The hero type kept under <paramref name="typeId"/> holds another id, as a corrupt entry or a torn read might.</summary>
	public void SetHeroTypeId(int typeId, int id) => _heap.Int32(_heroTypes[typeId] + 0x10, id);

	/// <summary>The skill type kept under <paramref name="typeId"/> holds another id, as a corrupt entry or a torn read might.</summary>
	public void SetSkillTypeId(int typeId, int id) => _heap.Int32(_skillTypes[typeId] + 0x10, id);

	/// <summary>The value in one of HeroById's slots points somewhere else, as a corrupt entry or a torn read might.</summary>
	public void PointHeroByIdValue(int slot, ulong address) => _heap.Pointer(_runtime.EntryValue(_heroById, slot), address);

	/// <summary>HeroById's count and free count disagree with its slots, as a torn or corrupt read might see them.</summary>
	public void SetHeroByIdCounts(int count, int freeCount) => _runtime.SetDictionaryCounts(_heroById, count, freeCount);

	/// <summary>UserWrapper's heroes: HeroesWrapper's HeroData, whose HeroById holds each hero by its id, after one slot a removal freed.</summary>
	private ulong LayOutTheHeroes(SyntheticHeroData heroData)
	{
		var systemObject = _runtime.SystemObject;

		var skill = Declare(
			"SharedModel.Meta.Skills",
			"Skill",
			systemObject,
			"System.Object",
			("Id", "System.Int32", Int32TypeCode, 0x10),
			("TypeId", "System.Int32", Int32TypeCode, 0x14),
			("Level", "System.Int32", Int32TypeCode, 0x18));
		var masteryData = Declare(
			"SharedModel.Meta.Masteries",
			"HeroMasteryData",
			systemObject,
			"System.Object",
			("Masteries", "System.Collections.Generic.List`1<System.Int32>", GenericInstanceTypeCode, 0x10));
		var nullableBlessing = _runtime.Class(
			"System",
			"Nullable`1",
			_runtime.ValueType,
			new SyntheticField("hasValue", BooleanTypeCode, ObjectHeaderSize),
			new SyntheticField("value", ValueTypeTypeCode, ObjectHeaderSize + 0x4));
		var doubleAscend = _runtime.Class(
			"SharedModel.Meta.DoubleAscend.User",
			"HeroDoubleAscendData",
			systemObject,
			new SyntheticField("Grade", ValueTypeTypeCode, 0x10),
			new SyntheticField("BlessingId", GenericInstanceTypeCode, 0x14, GenericClass: _runtime.GenericClass(nullableBlessing)),
			new SyntheticField("FreeResetUsed", BooleanTypeCode, 0x1C));
		_metadata.Class(
			"SharedModel.Meta.DoubleAscend.User.HeroDoubleAscendData",
			"System.Object",
			("Grade", "SharedModel.Meta.DoubleAscend.User.DoubleAscendGrade", false),
			("BlessingId", "System.Nullable`1<SharedModel.Meta.DoubleAscend.Static.BlessingTypeId>", false),
			("FreeResetUsed", "System.Boolean", false));
		var hero = Declare(
			"SharedModel.Meta.Heroes",
			"Hero",
			systemObject,
			"System.Object",
			("Id", "System.Int32", Int32TypeCode, 0x10),
			("TypeId", "System.Int32", Int32TypeCode, 0x14),
			("Grade", "SharedModel.Meta.Heroes.HeroGrade", ValueTypeTypeCode, 0x18),
			("Level", "System.Int32", Int32TypeCode, 0x1C),
			("EmpowerLevel", "System.Int32", Int32TypeCode, 0x20),
			("Locked", "System.Boolean", BooleanTypeCode, 0x24),
			("InStorage", "System.Boolean", BooleanTypeCode, 0x25),
			("InBathhouse", "System.Boolean", BooleanTypeCode, 0x26),
			("Marker", "SharedModel.Meta.Heroes.HeroMarker", ValueTypeTypeCode, 0x28),
			("Skills", "System.Collections.Generic.List`1<SharedModel.Meta.Skills.Skill>", GenericInstanceTypeCode, 0x30),
			("MasteryData", "SharedModel.Meta.Masteries.HeroMasteryData", ClassTypeCode, 0x38),
			("DoubleAscendData", "SharedModel.Meta.DoubleAscend.User.HeroDoubleAscendData", ClassTypeCode, 0x40));
		var userHeroData = Declare(
			"SharedModel.Meta.Heroes",
			"UserHeroData",
			systemObject,
			"System.Object",
			("HeroById", "System.Collections.Generic.Dictionary`2<System.Int32, SharedModel.Meta.Heroes.Hero>", GenericInstanceTypeCode, 0x10));
		var updatableHeroData = Declare("Client.Model.Gameplay.Heroes.Data", "UpdatableHeroData", userHeroData, "SharedModel.Meta.Heroes.UserHeroData");
		var heroesWrapperReadOnly = Declare(
			"Client.Model.Gameplay.Heroes",
			"HeroesWrapperReadOnly",
			systemObject,
			"System.Object",
			("HeroData", "Client.Model.Gameplay.Heroes.Data.UpdatableHeroData", ClassTypeCode, 0x10));
		var heroesWrapper = Declare("Client.Model.Gameplay.Heroes", "HeroesWrapper", heroesWrapperReadOnly, "Client.Model.Gameplay.Heroes.HeroesWrapperReadOnly");

		var skills = _runtime.ListType(skill, "Skill", sizeof(ulong));
		var ints = _runtime.ListType(_runtime.SystemInt32, "Int32", sizeof(int));
		var heroes = heroData.Heroes.Select(synthetic =>
		{
			var heroObject = _runtime.Object(hero, 0x48);
			_heap.Int32(heroObject + 0x10, synthetic.Id);
			_heap.Int32(heroObject + 0x14, synthetic.TypeId);
			_heap.Int32(heroObject + 0x18, synthetic.Grade);
			_heap.Int32(heroObject + 0x1C, synthetic.Level);
			_heap.Int32(heroObject + 0x20, synthetic.EmpowerLevel);
			_heap.Bytes(heroObject + 0x24, [Bool(synthetic.Locked), Bool(synthetic.InStorage), Bool(synthetic.InBathhouse)]);
			_heap.Int32(heroObject + 0x28, synthetic.Marker);

			var skillList = _runtime.List(skills, [.. synthetic.Skills.Select(heroSkill =>
			{
				var skillObject = _runtime.Object(skill, 0x20);
				_heap.Int32(skillObject + 0x10, heroSkill.Id);
				_heap.Int32(skillObject + 0x14, heroSkill.TypeId);
				_heap.Int32(skillObject + 0x18, heroSkill.Level);
				return skillObject;
			})]);
			_skillLists[synthetic.Id] = skillList;
			_heap.Pointer(heroObject + 0x30, skillList);

			if (synthetic.Masteries is { } masteries)
			{
				var masteryDataObject = _runtime.Object(masteryData, 0x18);
				_heap.Pointer(masteryDataObject + 0x10, _runtime.List(ints, [.. masteries.Select(mastery => (ulong)mastery)]));
				_heap.Pointer(heroObject + 0x38, masteryDataObject);
			}

			if (synthetic.DoubleAscend is { } awakening)
			{
				var doubleAscendObject = _runtime.Object(doubleAscend, 0x20);
				_heap.Int32(doubleAscendObject + 0x10, awakening.Grade);
				if (awakening.Blessing is { } blessing)
				{
					_heap.Bytes(doubleAscendObject + 0x14, [1]);
					_heap.Int32(doubleAscendObject + 0x18, blessing);
				}

				_heap.Pointer(heroObject + 0x40, doubleAscendObject);
			}

			return ((ulong)synthetic.Id, heroObject);
		});
		_heroById = _runtime.Dictionary(_runtime.DictionaryType(Int32TypeCode, ClassTypeCode), [.. heroes], freedSlot: true);

		var updatableHeroDataObject = _runtime.Object(updatableHeroData, 0x18);
		_heap.Pointer(updatableHeroDataObject + 0x10, _heroById);
		var heroesWrapperObject = _runtime.Object(heroesWrapper, 0x18);
		_heap.Pointer(heroesWrapperObject + 0x10, updatableHeroDataObject);
		return heroesWrapperObject;
	}
}
