using RaidBridge.Game.Testing.Champions;
using RaidBridge.Game.Testing.Il2Cpp;
using static RaidBridge.Game.Testing.Il2Cpp.Unity6000Layout;

namespace RaidBridge.Game.Testing;

/// <summary>The heroes of a synthetic Game client, in HeroById, and what a bad read can do to them; the static data that types them is laid out with the rest of ClientStaticData.</summary>
public sealed partial class SyntheticGameClient
{
	private const string BattleStats = "SharedModel.Meta.Heroes.BattleStats";

	private static readonly string[] BattleStatNames =
		["Health", "Attack", "Defence", "Speed", "Resistance", "Accuracy", "CriticalChance", "CriticalDamage", "CriticalHeal", "IgnoreDefence"];

	private ulong _battleStatsStatics;
	private ulong _rankMultipliers;
	private readonly Dictionary<int, ulong> _masteryTypes = [];

	/// <summary>The mastery type kept under <paramref name="key"/> in MasteryById holds another id, as a corrupt entry or a torn read might.</summary>
	public void SetMasteryTypeId(int key, int id) => _heap.Int32(_masteryTypes[key] + 0x10, id);
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

	/// <summary>BattleStats.Multipliers holds another number, as a game update or a corrupt read might.</summary>
	public void SetRankMultiplier(int row, int column, double value) =>
		_heap.Int64(SyntheticRuntime.Element(_rankMultipliers, (row * 2) + column, sizeof(double)), BitConverter.DoubleToInt64Bits(value));

	/// <summary>BattleStats.Multipliers holds its twelve numbers in a one-dimensional <c>double[]</c>, as a game update might.</summary>
	public void HoldRankMultipliersInOneDimension()
	{
		var arrayClass = _runtime.ArrayClass(_runtime.RuntimeClass("System.Double"), "Double", sizeof(double));
		_heap.Bytes(arrayClass + ClassRank, [1]);
		_heap.Pointer(_battleStatsStatics + 0x8, _runtime.Array(arrayClass, 12, sizeof(double)));
	}

	/// <summary>The bounds of BattleStats.Multipliers give it other lengths, as a game update or a corrupt read might.</summary>
	public void SetRankMultipliersShape(int rows, int columns)
	{
		var bounds = _heap.ReadPointer(_rankMultipliers + ArrayBounds);
		_heap.Pointer(bounds + ArrayBoundsLength, (ulong)rows);
		_heap.Pointer(bounds + ArrayBoundsSize + ArrayBoundsLength, (ulong)columns);
	}

	/// <summary>
	/// BattleStats, with a Fixed for each stat, and its statics: ToPercent, a Fixed, and Multipliers, which the
	/// metadata declares a <c>double[][]</c> but which holds a <c>double[,]</c>, as the Game client does at v39
	/// (https://github.com/ANicholasson/raid-bridge/issues/9). The reader reaches them through BattleStats' TypeInfo slot.
	/// </summary>
	private ulong LayOutTheBattleStats(SyntheticHeroData heroData)
	{
		var battleStats = _runtime.Class(
			"SharedModel.Meta.Heroes",
			"BattleStats",
			_runtime.SystemObject,
			[
				.. BattleStatNames.Select((name, index) => new SyntheticField(name, ValueTypeTypeCode, 0x10 + (index * 0x8))),
				new SyntheticField("ToPercent", ValueTypeTypeCode, 0x0, IsStatic: true),
				new SyntheticField("Multipliers", SingleDimensionArrayTypeCode, 0x8, IsStatic: true),
			]);
		_metadata.Class(
			BattleStats,
			"System.Object",
			[
				.. BattleStatNames.Select(name => (name, Fixed, false)),
				("ToPercent", Fixed, true),
				("Multipliers", "System.Double[][]", true),
			]);
		TypeInfoSlot(BattleStats, BattleStatsTypeInfoRva, battleStats);

		_battleStatsStatics = _heap.Allocate(0x10);
		_heap.Pointer(battleStats + ClassStaticFields, _battleStatsStatics);
		_heap.Int64(_battleStatsStatics, FixedRaw(heroData.ToPercent));
		if (heroData.RankMultipliers is { } multipliers)
		{
			var systemDouble = _runtime.Class("System", "Double", _runtime.ValueType);
			_rankMultipliers = _runtime.Matrix(_runtime.MatrixClass(systemDouble, "Double", sizeof(double)), multipliers);
			_heap.Pointer(_battleStatsStatics + 0x8, _rankMultipliers);
		}

		return battleStats;
	}

	/// <summary>
	/// The static data's MasteryData: every mastery type in MasteryById, and the stat bonus of those that have one in
	/// MasteryBonusById, with BonusSourceArtifactSet, the constant that marks a bonus to set bonuses, at -1 as at v39.
	/// </summary>
	private ulong LayOutTheMasteries(SyntheticHeroData heroData, ulong statBonus)
	{
		const string StaticMasteryData = "SharedModel.Meta.Masteries.StaticMasteryData";
		var masteryType = Declare("SharedModel.Meta.Masteries", "MasteryType", _runtime.SystemObject, "System.Object", ("Id", "System.Int32", Int32TypeCode, 0x10));
		var staticMasteryData = Declare(
			"SharedModel.Meta.Masteries",
			"StaticMasteryData",
			_runtime.SystemObject,
			"System.Object",
			("MasteryBonusById", "System.Collections.Generic.Dictionary`2<System.Int32, SharedModel.Meta.General.StatBonus>", GenericInstanceTypeCode, 0x10),
			("MasteryById", "System.Collections.Generic.Dictionary`2<System.Int32, SharedModel.Meta.Masteries.MasteryType>", GenericInstanceTypeCode, 0x18));
		_metadata.Constant(StaticMasteryData, "BonusSourceArtifactSet", -1);

		var types = heroData.MasteryTypes.Select(mastery =>
		{
			var typeObject = _runtime.Object(masteryType, 0x18);
			_heap.Int32(typeObject + 0x10, mastery.Id);
			_masteryTypes[mastery.Id] = typeObject;
			return ((ulong)mastery.Id, typeObject);
		});
		var bonuses = heroData.MasteryTypes.Where(mastery => mastery.Bonus is not null).Select(mastery =>
		{
			var bonusObject = _runtime.Object(statBonus, 0x20);
			_heap.Int32(bonusObject + 0x10, mastery.Bonus!.Stat);
			_heap.Bytes(bonusObject + 0x14, [Bool(mastery.Bonus.Absolute)]);
			_heap.Int64(bonusObject + 0x18, mastery.Bonus.Value);
			return ((ulong)mastery.Id, bonusObject);
		});
		var staticMasteryDataObject = _runtime.Object(staticMasteryData, 0x20);
		_heap.Pointer(staticMasteryDataObject + 0x10, _runtime.Dictionary(_runtime.DictionaryType(Int32TypeCode, ClassTypeCode), [.. bonuses]));
		_heap.Pointer(staticMasteryDataObject + 0x18, _runtime.Dictionary(_runtime.DictionaryType(Int32TypeCode, ClassTypeCode), [.. types]));
		return staticMasteryDataObject;
	}

	/// <summary>
	/// The static data's DoubleAscendData: each blessing in BlessingById, with the StatKindIds its GradeBonuses name for
	/// each grade, and BlessingStatsByRarity, each grade's StatBonus for each stat.
	/// </summary>
	private ulong LayOutTheAwakening(SyntheticHeroData heroData, ulong statBonus)
	{
		var systemObject = _runtime.SystemObject;
		var blessingBonus = Declare(
			"SharedModel.Meta.DoubleAscend.User",
			"BlessingBonus",
			systemObject,
			"System.Object",
			("StatKindIds", "System.Collections.Generic.List`1<SharedModel.Battle.Effects.StatKindId>", GenericInstanceTypeCode, 0x10));
		var blessingType = Declare(
			"SharedModel.Meta.DoubleAscend.Static",
			"BlessingType",
			systemObject,
			"System.Object",
			("Id", "SharedModel.Meta.DoubleAscend.Static.BlessingTypeId", ValueTypeTypeCode, 0x10),
			("GradeBonuses", "System.Collections.Generic.Dictionary`2<SharedModel.Meta.DoubleAscend.User.DoubleAscendGrade, SharedModel.Meta.DoubleAscend.User.BlessingBonus>", GenericInstanceTypeCode, 0x18));
		var blessingStats = Declare(
			"SharedModel.Meta.DoubleAscend.Static",
			"BlessingStats",
			systemObject,
			"System.Object",
			("Grade", "SharedModel.Meta.DoubleAscend.User.DoubleAscendGrade", ValueTypeTypeCode, 0x10),
			("StatBonusByKindId", "System.Collections.Generic.Dictionary`2<SharedModel.Battle.Effects.StatKindId, SharedModel.Meta.General.StatBonus>", GenericInstanceTypeCode, 0x18));
		var doubleAscendData = Declare(
			"SharedModel.Meta.DoubleAscend.Static",
			"StaticDoubleAscendData",
			systemObject,
			"System.Object",
			("BlessingById", "System.Collections.Generic.Dictionary`2<SharedModel.Meta.DoubleAscend.Static.BlessingTypeId, SharedModel.Meta.DoubleAscend.Static.BlessingType>", GenericInstanceTypeCode, 0x10),
			("BlessingStatsByRarity", "System.Collections.Generic.Dictionary`2<SharedModel.Meta.Heroes.HeroRarity, System.Collections.Generic.List`1<SharedModel.Meta.DoubleAscend.Static.BlessingStats>>", GenericInstanceTypeCode, 0x18));

		var statKinds = _runtime.ListType(_runtime.Class("SharedModel.Battle.Effects", "StatKindId", _runtime.ValueType), "StatKindId", sizeof(int));
		var byEnum = _runtime.DictionaryType(ValueTypeTypeCode, ClassTypeCode);
		var blessings = heroData.Blessings.Select(blessing =>
		{
			var typeObject = _runtime.Object(blessingType, 0x20);
			_heap.Int32(typeObject + 0x10, blessing.Id);
			_heap.Pointer(typeObject + 0x18, _runtime.Dictionary(byEnum, [.. blessing.StatsByGrade.Select(grade =>
			{
				var bonusObject = _runtime.Object(blessingBonus, 0x18);
				_heap.Pointer(bonusObject + 0x10, _runtime.List(statKinds, [.. grade.Value.Select(stat => (ulong)stat)]));
				return ((ulong)grade.Key, bonusObject);
			})]));
			return ((ulong)blessing.Id, typeObject);
		});
		var statsList = _runtime.ListType(blessingStats, "BlessingStats", sizeof(ulong));
		var byRarity = heroData.BlessingStatsByRarity.Select(rarity => ((ulong)rarity.Key, _runtime.List(statsList, [.. rarity.Value.Select(grade =>
		{
			var statsObject = _runtime.Object(blessingStats, 0x20);
			_heap.Int32(statsObject + 0x10, grade.Grade);
			_heap.Pointer(statsObject + 0x18, _runtime.Dictionary(byEnum, [.. grade.Stats.Select(stat => ((ulong)stat.Key, StaticStatBonus(statBonus, stat.Key, stat.Value.Value, stat.Value.Absolute)))]));
			return statsObject;
		})])));
		var dataObject = _runtime.Object(doubleAscendData, 0x20);
		_heap.Pointer(dataObject + 0x10, _runtime.Dictionary(byEnum, [.. blessings]));
		_heap.Pointer(dataObject + 0x18, _runtime.Dictionary(byEnum, [.. byRarity]));
		return dataObject;
	}

	/// <summary>The static data's AcademyData.Empower: the levels' StatBonuses, and the Epic ones', each a BonusValue by StatKindId.</summary>
	private ulong LayOutTheEmpowerment(SyntheticHeroData heroData)
	{
		var systemObject = _runtime.SystemObject;
		var statBonuses = _runtime.RuntimeClass("SharedModel.Meta.Academy.Static.StatBonuses");
		var empower = Declare(
			"SharedModel.Meta.Academy.Static",
			"StaticAcademyEmpowerData",
			systemObject,
			"System.Object",
			("Bonuses", "System.Collections.Generic.List`1<SharedModel.Meta.Academy.Static.StatBonuses>", GenericInstanceTypeCode, 0x10),
			("EpicBonuses", "System.Collections.Generic.List`1<SharedModel.Meta.Academy.Static.StatBonuses>", GenericInstanceTypeCode, 0x18));
		var bonusValue = _runtime.RuntimeClass("SharedModel.Meta.Artifacts.BonusValue");
		var levels = _runtime.ListType(statBonuses, "StatBonuses", sizeof(ulong));
		ulong Levels(IReadOnlyList<IReadOnlyDictionary<int, (long Value, bool Absolute)>> list) => _runtime.List(levels, [.. list.Select(level =>
		{
			var levelObject = _runtime.Object(statBonuses, 0x18);
			_heap.Pointer(levelObject + 0x10, _runtime.Dictionary(_runtime.DictionaryType(ValueTypeTypeCode, ClassTypeCode), [.. level.Select(bonus =>
			{
				var bonusObject = _runtime.Object(bonusValue, 0x20);
				_heap.Bytes(bonusObject + 0x10, [Bool(bonus.Value.Absolute)]);
				_heap.Int64(bonusObject + 0x18, bonus.Value.Value);
				return ((ulong)bonus.Key, bonusObject);
			})]));
			return levelObject;
		})]);
		var empowerObject = _runtime.Object(empower, 0x20);
		_heap.Pointer(empowerObject + 0x10, Levels(heroData.EmpowerBonuses));
		_heap.Pointer(empowerObject + 0x18, heroData.EpicEmpowerBonuses is { } epic ? Levels(epic) : 0);
		return empowerObject;
	}

	/// <summary>A static StatBonus of the stat.</summary>
	private ulong StaticStatBonus(ulong statBonus, int stat, long value, bool absolute)
	{
		var bonusObject = _runtime.Object(statBonus, 0x20);
		_heap.Int32(bonusObject + 0x10, stat);
		_heap.Bytes(bonusObject + 0x14, [Bool(absolute)]);
		_heap.Int64(bonusObject + 0x18, value);
		return bonusObject;
	}

	/// <summary>A BattleStats holding the stats, each as a Fixed.</summary>
	private ulong BattleStatsObject(ulong battleStats, SyntheticBattleStats stats)
	{
		var battleStatsObject = _runtime.Object(battleStats, 0x60);
		double[] values = [stats.Health, stats.Attack, stats.Defence, stats.Speed, stats.Resistance, stats.Accuracy, stats.CriticalChance, stats.CriticalDamage, stats.CriticalHeal, stats.IgnoreDefence];
		for (var index = 0; index < values.Length; index++)
		{
			_heap.Int64(battleStatsObject + 0x10 + (ulong)(index * 0x8), FixedRaw(values[index]));
		}

		return battleStatsObject;
	}

	/// <summary>A number as the raw value of the Fixed nearest it below, as the game's (Fixed)double makes it.</summary>
	private static long FixedRaw(double value) => (long)(value * (1L << 32));

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
