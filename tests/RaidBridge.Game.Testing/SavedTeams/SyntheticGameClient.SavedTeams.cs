using RaidBridge.Game.Testing.Champions;
using RaidBridge.Game.Testing.Il2Cpp;
using static RaidBridge.Game.Testing.Il2Cpp.Unity6000Layout;

namespace RaidBridge.Game.Testing;

/// <summary>The Saved teams of a synthetic Game client, and what the game knows of them in its code and static data.</summary>
public sealed partial class SyntheticGameClient
{
	private const string Preset = "SharedModel.Meta.Heroes.HeroesAiPreset";
	private const string PresetType = "SharedModel.Meta.Heroes.HeroesAiPresetType";
	private const ulong PresetTypeInfoRva = 0x1118;

	/// <summary>The game's HeroesAiPresetIds constants, as metadata v39 holds them.</summary>
	private static readonly (string Name, long Value)[] PresetIdConstants =
	[
		("GeneralPveIdMin", 1), ("PvpIdMin", 101), ("FractionsIdMin", 201), ("ArenaDefence", 301), ("DoomTowerIdMin", 401),
		("Arena3x3DefenceMin", 601), ("Arena3x3DefenceMax", 603), ("AllianceHydraIdMin", 701), ("AllianceChimeraIdMin", 801),
		("CooperationEventWorldBossIdMin", 901), ("SiegeIdMin", 1001), ("IronTwinsIdMin", 1101), ("PotionKeepsIdMin", 1201),
		("ArtifactAscendAndMasteriesIdMin", 1301), ("EventDungeonIdMin", 1401), ("ArtifactDungeonsIdMin", 1501),
	];

	/// <summary>
	/// UserHeroData's HeroesAiPresets: a List of HeroesAiPreset, each with its SkillPrioritiesSetups, a list whose
	/// entries can be null; and HeroesAiPreset's static TypeRanges, an array of <c>(int, HeroesAiPresetType)</c> tuples,
	/// which the reader reaches through HeroesAiPreset's TypeInfo slot. Returns the list, or 0 for none.
	/// </summary>
	private ulong LayOutTheSavedTeams(SyntheticHeroData heroData)
	{
		var systemObject = _runtime.SystemObject;
		var setup = Declare(
			"SharedModel.Meta.Heroes",
			"SkillPrioritiesSetup",
			systemObject,
			"System.Object",
			("HeroId", "System.Int32", Int32TypeCode, 0x10));
		var preset = _runtime.Class(
			"SharedModel.Meta.Heroes",
			"HeroesAiPreset",
			systemObject,
			new SyntheticField("Id", Int32TypeCode, 0x10),
			new SyntheticField("Position", Int32TypeCode, 0x14),
			new SyntheticField("NameIsNotDefault", BooleanTypeCode, 0x18),
			new SyntheticField("SkillPrioritiesSetups", GenericInstanceTypeCode, 0x20),
			new SyntheticField("TypeRanges", SingleDimensionArrayTypeCode, 0x0, IsStatic: true));
		_metadata.Class(
			Preset,
			"System.Object",
			("Id", "System.Int32", false),
			("Position", "System.Int32", false),
			("NameIsNotDefault", "System.Boolean", false),
			("SkillPrioritiesSetups", "System.Collections.Generic.List`1<SharedModel.Meta.Heroes.SkillPrioritiesSetup>", false),
			("TypeRanges", $"System.ValueTuple`2<System.Int32, {PresetType}>[]", true));
		TypeInfoSlot(Preset, PresetTypeInfoRva, preset);
		foreach (var (name, value) in PresetIdConstants)
		{
			_metadata.Constant("SharedModel.Meta.Heroes.HeroesAiPresetIds", name, value);
		}

		var statics = _heap.Allocate(8);
		_heap.Pointer(preset + ClassStaticFields, statics);
		if (heroData.PresetTypeRanges is { } ranges)
		{
			var range = _runtime.Class(
				"System",
				"ValueTuple`2",
				_runtime.ValueType,
				new SyntheticField("Item1", Int32TypeCode, ObjectHeaderSize),
				new SyntheticField("Item2", ValueTypeTypeCode, ObjectHeaderSize + 0x4));
			var array = _runtime.Array(_runtime.ArrayClass(range, "ValueTuple`2", 0x8), ranges.Count, 0x8);
			for (var index = 0; index < ranges.Count; index++)
			{
				var element = SyntheticRuntime.Element(array, index, 0x8);
				_heap.Int32(element, ranges[index].Start);
				_heap.Int32(element + 0x4, ranges[index].Type);
			}

			Slot($"{Preset}.TypeRanges", statics, array);
		}

		if (heroData.SavedTeams is not { } savedTeams)
		{
			return 0;
		}

		var setups = _runtime.ListType(setup, "SkillPrioritiesSetup", sizeof(ulong));
		return _runtime.List(_runtime.ListType(preset, "HeroesAiPreset", sizeof(ulong)), [.. savedTeams.Select(team =>
		{
			var presetObject = _runtime.Object(preset, 0x28);
			_heap.Int32(presetObject + 0x10, team.Id);
			_heap.Int32(presetObject + 0x14, team.Position);
			_heap.Bytes(presetObject + 0x18, [Bool(team.NameIsNotDefault)]);
			if (team.HeroIds is { } heroIds)
			{
				_heap.Pointer(presetObject + 0x20, _runtime.List(setups, [.. heroIds.Select(heroId =>
				{
					if (heroId is not { } id)
					{
						return 0UL;
					}

					var setupObject = _runtime.Object(setup, 0x18);
					_heap.Int32(setupObject + 0x10, id);
					return setupObject;
				})]));
			}

			return presetObject;
		})]);
	}

	/// <summary>The static data's HeroData.PresetInfoByType: each purpose's HeroesAiPresetInfo, with its MaxHeroesCount.</summary>
	private ulong PresetInfoByType(SyntheticHeroData heroData)
	{
		var info = Declare(
			"SharedModel.Meta.Heroes",
			"HeroesAiPresetInfo",
			_runtime.SystemObject,
			"System.Object",
			("MaxHeroesCount", "System.Int32", Int32TypeCode, 0x10));
		return _runtime.Dictionary(_runtime.DictionaryType(ValueTypeTypeCode, ClassTypeCode), [.. heroData.MaxHeroesByPresetType.Select(max =>
		{
			var infoObject = _runtime.Object(info, 0x18);
			_heap.Int32(infoObject + 0x10, max.Value);
			return ((ulong)max.Key, infoObject);
		})]);
	}
}
