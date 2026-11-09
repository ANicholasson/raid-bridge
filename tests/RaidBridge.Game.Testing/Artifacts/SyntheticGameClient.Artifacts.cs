using RaidBridge.Game.Testing.Artifacts;
using RaidBridge.Game.Testing.Il2Cpp;
using static RaidBridge.Game.Testing.Il2Cpp.Unity6000Layout;

namespace RaidBridge.Game.Testing;

/// <summary>The Artifacts of a synthetic Game client, worn and unworn, and what a bad read can do to them.</summary>
public sealed partial class SyntheticGameClient
{
	private readonly Dictionary<int, ulong> _artifacts = [];
	private readonly Dictionary<int, ulong> _substatLists = [];

	/// <summary>An Artifact's main stat points somewhere else, as a corrupt read might see it.</summary>
	public void PointPrimaryBonus(int artifactId, ulong address) => _heap.Pointer(_artifacts[artifactId] + 0x38, address);

	/// <summary>An Artifact's Substat list claims more Substats than its array holds, as a torn or corrupt read might see it.</summary>
	public void SetSubstatCount(int artifactId, int count) => _runtime.SetListSize(_substatLists[artifactId], count);

	/// <summary>
	/// UserWrapper's Artifacts: EquipmentWrapper's ArtifactData, whose ArtifactDataByHeroId holds each hero's slots,
	/// and the Artifacts themselves, in the Account's own list or behind ArtifactStorageResolver._implementation, a
	/// static the reader reaches through the resolver's TypeInfo slot. Fixed is a value type, read through its own.
	/// </summary>
	private ulong LayOutTheArtifacts(SyntheticArtifactData artifactData)
	{
		var systemObject = _runtime.SystemObject;

		var @fixed = _runtime.Class("Plarium.Common.Numerics", "Fixed", _runtime.ValueType, new SyntheticField("m_rawValue", Int64TypeCode, ObjectHeaderSize));
		_metadata.Class(Fixed, "System.ValueType", ("m_rawValue", "System.Int64", false));
		_metadata.Constant(Fixed, "ONE", 1L << 32);
		TypeInfoSlot(Fixed, FixedTypeInfoRva, @fixed);

		var bonusValue = Declare(
			"SharedModel.Meta.Artifacts",
			"BonusValue",
			systemObject,
			"System.Object",
			("_isAbsolute", "System.Boolean", BooleanTypeCode, 0x10),
			("_value", Fixed, ValueTypeTypeCode, 0x18));
		var bonus = Declare(
			"SharedModel.Meta.Artifacts.Bonuses",
			"ArtifactBonus",
			systemObject,
			"System.Object",
			("_kindId", "SharedModel.Battle.Effects.StatKindId", ValueTypeTypeCode, 0x10),
			("_level", "System.Int32", Int32TypeCode, 0x14),
			("_value", "SharedModel.Meta.Artifacts.BonusValue", ClassTypeCode, 0x18),
			("_powerUpValue", Fixed, ValueTypeTypeCode, 0x20));
		var nullableInt = _runtime.Class(
			"System",
			"Nullable`1",
			_runtime.ValueType,
			new SyntheticField("hasValue", BooleanTypeCode, ObjectHeaderSize),
			new SyntheticField("value", Int32TypeCode, ObjectHeaderSize + 0x4));
		var artifact = _runtime.Class(
			"SharedModel.Meta.Artifacts",
			"Artifact",
			systemObject,
			new SyntheticField("_id", Int32TypeCode, 0x10),
			new SyntheticField("_level", Int32TypeCode, 0x14),
			new SyntheticField("_ascendLevel", GenericInstanceTypeCode, 0x18, GenericClass: _runtime.GenericClass(nullableInt)),
			new SyntheticField("_kindId", ValueTypeTypeCode, 0x20),
			new SyntheticField("_rankId", ValueTypeTypeCode, 0x24),
			new SyntheticField("_rarityId", ValueTypeTypeCode, 0x28),
			new SyntheticField("_setKindId", ValueTypeTypeCode, 0x2C),
			new SyntheticField("_requiredFraction", ValueTypeTypeCode, 0x30),
			new SyntheticField("_primaryBonus", ClassTypeCode, 0x38),
			new SyntheticField("_secondaryBonuses", GenericInstanceTypeCode, 0x40),
			new SyntheticField("_ascendBonus", ClassTypeCode, 0x48));
		_metadata.Class(
			"SharedModel.Meta.Artifacts.Artifact",
			"System.Object",
			("_id", "System.Int32", false),
			("_level", "System.Int32", false),
			("_ascendLevel", "System.Nullable`1<System.Int32>", false),
			("_kindId", "SharedModel.Meta.Artifacts.ArtifactKindId", false),
			("_rankId", "SharedModel.Meta.Artifacts.ArtifactRankId", false),
			("_rarityId", "SharedModel.Meta.ItemRarity", false),
			("_setKindId", "SharedModel.Meta.Artifacts.Sets.ArtifactSetKindId", false),
			("_requiredFraction", "SharedModel.Meta.Heroes.HeroFraction", false),
			("_primaryBonus", "SharedModel.Meta.Artifacts.Bonuses.ArtifactBonus", false),
			("_secondaryBonuses", "System.Collections.Generic.List`1<SharedModel.Meta.Artifacts.Bonuses.ArtifactBonus>", false),
			("_ascendBonus", "SharedModel.Meta.Artifacts.Bonuses.ArtifactBonus", false));
		var heroArtifactData = Declare(
			"SharedModel.Meta.Artifacts",
			"HeroArtifactData",
			systemObject,
			"System.Object",
			("ArtifactIdByKind", "System.Collections.Generic.Dictionary`2<SharedModel.Meta.Artifacts.ArtifactKindId, System.Int32>", GenericInstanceTypeCode, 0x10));
		var userArtifactData = Declare(
			"SharedModel.Meta.Artifacts",
			"UserArtifactData",
			systemObject,
			"System.Object",
			("Artifacts", "System.Collections.Generic.List`1<SharedModel.Meta.Artifacts.Artifact>", GenericInstanceTypeCode, 0x10),
			("ArtifactDataByHeroId", "System.Collections.Generic.Dictionary`2<System.Int32, SharedModel.Meta.Artifacts.HeroArtifactData>", GenericInstanceTypeCode, 0x18));
		var updatableArtifactData = Declare("Client.Model.Gameplay.Artifacts.Data", "UpdatableArtifactData", userArtifactData, "SharedModel.Meta.Artifacts.UserArtifactData");
		var equipmentWrapperReadonly = Declare(
			"Client.Model.Gameplay.Artifacts",
			"EquipmentWrapperReadonly",
			systemObject,
			"System.Object",
			("ArtifactData", "SharedModel.Meta.Artifacts.UserArtifactData", ClassTypeCode, 0x10));
		var equipmentWrapper = Declare("Client.Model.Gameplay.Artifacts", "EquipmentWrapper", equipmentWrapperReadonly, "Client.Model.Gameplay.Artifacts.EquipmentWrapperReadonly");
		var cachedArtifacts = Declare(
			"Client.Model.Gameplay.Artifacts",
			"CachedArtifacts",
			systemObject,
			"System.Object",
			("_artifacts", "System.Collections.Generic.Dictionary`2<System.Int32, SharedModel.Meta.Artifacts.Artifact>", GenericInstanceTypeCode, 0x10));
		var externalStorage = Declare(
			"Client.Model.Gameplay.Artifacts",
			"ExternalArtifactsStorage",
			systemObject,
			"System.Object",
			("_cachedArtifacts", "Client.Model.Gameplay.Artifacts.CachedArtifacts", ClassTypeCode, 0x10));
		var resolver = _runtime.Class("SharedModel.Meta.Artifacts.ArtifactStorage", "ArtifactStorageResolver", systemObject, new SyntheticField("_implementation", ClassTypeCode, 0, IsStatic: true));
		_metadata.Class(Resolver, "System.Object", ("_implementation", "SharedModel.Meta.Artifacts.ArtifactStorage.IArtifactStorageAdapter", true));
		TypeInfoSlot(Resolver, ResolverTypeInfoRva, resolver);

		ulong Bonus(SyntheticBonus synthetic)
		{
			var valueObject = _runtime.Object(bonusValue, 0x20);
			_heap.Bytes(valueObject + 0x10, [Bool(synthetic.Absolute)]);
			_heap.Int64(valueObject + 0x18, synthetic.Value);
			var bonusObject = _runtime.Object(bonus, 0x28);
			_heap.Int32(bonusObject + 0x10, synthetic.Stat);
			_heap.Int32(bonusObject + 0x14, synthetic.Level);
			_heap.Pointer(bonusObject + 0x18, valueObject);
			_heap.Int64(bonusObject + 0x20, synthetic.Glyph);
			return bonusObject;
		}

		var bonuses = _runtime.ListType(bonus, "ArtifactBonus", sizeof(ulong));
		var artifacts = artifactData.Artifacts.Select(synthetic =>
		{
			var artifactObject = _runtime.Object(artifact, 0x50);
			_heap.Int32(artifactObject + 0x10, synthetic.Id);
			_heap.Int32(artifactObject + 0x14, synthetic.Level);
			if (synthetic.AscendLevel is { } ascendLevel)
			{
				_heap.Bytes(artifactObject + 0x18, [1]);
				_heap.Int32(artifactObject + 0x1C, ascendLevel);
			}

			_heap.Int32(artifactObject + 0x20, synthetic.Kind);
			_heap.Int32(artifactObject + 0x24, synthetic.Rank);
			_heap.Int32(artifactObject + 0x28, synthetic.Rarity);
			_heap.Int32(artifactObject + 0x2C, synthetic.SetKind);
			_heap.Int32(artifactObject + 0x30, synthetic.RequiredFraction);
			_heap.Pointer(artifactObject + 0x38, Bonus(synthetic.Primary));
			if (synthetic.Secondary is { } secondary)
			{
				_substatLists[synthetic.Id] = _runtime.List(bonuses, [.. secondary.Select(Bonus)]);
				_heap.Pointer(artifactObject + 0x40, _substatLists[synthetic.Id]);
			}

			if (synthetic.AscendBonus is { } ascendBonus)
			{
				_heap.Pointer(artifactObject + 0x48, Bonus(ascendBonus));
			}

			_artifacts[synthetic.Id] = artifactObject;
			return ((ulong)synthetic.Id, artifactObject);
		}).ToList();

		var external = artifactData.Storage == SyntheticArtifactStorage.ExternalStorage;
		var statics = _heap.Allocate(8);
		_heap.Pointer(resolver + ClassStaticFields, statics);
		var implementation = 0UL;
		if (external)
		{
			var cachedArtifactsObject = _runtime.Object(cachedArtifacts, 0x18);
			_heap.Pointer(cachedArtifactsObject + 0x10, _runtime.Dictionary(_runtime.DictionaryType(Int32TypeCode, ClassTypeCode), artifacts));
			implementation = _runtime.Object(externalStorage, 0x18);
			_heap.Pointer(implementation + 0x10, cachedArtifactsObject);
		}

		Slot($"{Resolver}._implementation", statics, implementation);

		var slots = _runtime.DictionaryType(ValueTypeTypeCode, Int32TypeCode);
		var heroes = artifactData.SlotsByHeroId.Select(hero =>
		{
			var heroArtifactDataObject = _runtime.Object(heroArtifactData, 0x18);
			_heap.Pointer(heroArtifactDataObject + 0x10, _runtime.Dictionary(slots, [.. hero.Value.Select(slot => ((ulong)slot.Key, (ulong)slot.Value))]));
			return ((ulong)hero.Key, heroArtifactDataObject);
		});

		var userArtifactDataObject = _runtime.Object(updatableArtifactData, 0x20);
		_heap.Pointer(userArtifactDataObject + 0x10, _runtime.List(_runtime.ListType(artifact, "Artifact", sizeof(ulong)), external ? [] : [.. artifacts.Select(entry => entry.Item2)]));
		_heap.Pointer(userArtifactDataObject + 0x18, _runtime.Dictionary(_runtime.DictionaryType(Int32TypeCode, ClassTypeCode), [.. heroes]));
		var equipmentWrapperObject = _runtime.Object(equipmentWrapper, 0x18);
		_heap.Pointer(equipmentWrapperObject + 0x10, userArtifactDataObject);
		return equipmentWrapperObject;
	}
}
