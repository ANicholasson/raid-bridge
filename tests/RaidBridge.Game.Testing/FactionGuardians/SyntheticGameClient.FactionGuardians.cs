using RaidBridge.Game.Testing.FactionGuardians;
using RaidBridge.Game.Testing.Il2Cpp;
using static RaidBridge.Game.Testing.Il2Cpp.Unity6000Layout;

namespace RaidBridge.Game.Testing;

/// <summary>The Faction Guardians of a synthetic Game client, and what a bad read can do to them.</summary>
public sealed partial class SyntheticGameClient
{
	private readonly Dictionary<(int Faction, int Rarity), ulong> _guardianSlots = [];
	private ulong _guardians;

	/// <summary>The Academy's SlotsByFraction has a count and free count that disagree with its slots, as a torn or corrupt read might see them.</summary>
	public void SetGuardianCounts(int count, int freeCount) => _runtime.SetDictionaryCounts(_guardians, count, freeCount);

	/// <summary>The value in a slot of the Academy's SlotsByFraction points somewhere else, as a bad read might see it.</summary>
	public void PointGuardianFaction(int slot, ulong address) => _heap.Pointer(_runtime.EntryValue(_guardians, slot), address);

	/// <summary>A Faction's guardian slots of a rarity, by HeroFraction and HeroRarity, claim more slots than their array holds, as a torn or corrupt read might see them.</summary>
	public void SetGuardianSlotCount(int faction, int rarity, int count) => _runtime.SetListSize(_guardianSlots[(faction, rarity)], count);

	/// <summary>
	/// UserWrapper's Academy, whose read-only base class declares the AcademyData, whose base class UserAcademyData holds
	/// the Guardians: each Faction's slots by its HeroFraction, then a list of slots by HeroRarity, each slot holding a
	/// Nullable&lt;int&gt; hero id in each of its two positions, beside the fields the reader doesn't read.
	/// </summary>
	private void LayOutTheFactionGuardians(ulong userWrapperObject, SyntheticGuardianData guardianData)
	{
		const string NullableInt = "System.Nullable`1<System.Int32>";
		var systemObject = _runtime.SystemObject;

		var nullableInt = _runtime.Class(
			"System",
			"Nullable`1",
			_runtime.ValueType,
			new SyntheticField("hasValue", BooleanTypeCode, ObjectHeaderSize),
			new SyntheticField("value", Int32TypeCode, ObjectHeaderSize + 0x4));
		var nullableBool = _runtime.Class(
			"System",
			"Nullable`1",
			_runtime.ValueType,
			new SyntheticField("hasValue", BooleanTypeCode, ObjectHeaderSize),
			new SyntheticField("value", BooleanTypeCode, ObjectHeaderSize + 0x1));
		var slot = _runtime.Class(
			"SharedModel.Meta.Academy.User",
			"AcademyGuardiansSlot",
			systemObject,
			new SyntheticField("FirstHero", GenericInstanceTypeCode, 0x10, GenericClass: _runtime.GenericClass(nullableInt)),
			new SyntheticField("SecondHero", GenericInstanceTypeCode, 0x18, GenericClass: _runtime.GenericClass(nullableInt)),
			new SyntheticField("Consumed", GenericInstanceTypeCode, 0x20, GenericClass: _runtime.GenericClass(nullableBool)),
			new SyntheticField("HeroBaseTypeId", GenericInstanceTypeCode, 0x24, GenericClass: _runtime.GenericClass(nullableInt)));
		_metadata.Class(
			"SharedModel.Meta.Academy.User.AcademyGuardiansSlot",
			"System.Object",
			("FirstHero", NullableInt, false),
			("SecondHero", NullableInt, false),
			("Consumed", "System.Nullable`1<System.Boolean>", false),
			("HeroBaseTypeId", NullableInt, false));
		var raritySlots = Declare(
			"SharedModel.Meta.Academy.User",
			"AcademyGuardiansRaritySlots",
			systemObject,
			"System.Object",
			("SlotsByRarity", "System.Collections.Generic.Dictionary`2<SharedModel.Meta.Heroes.HeroRarity, System.Collections.Generic.List`1<SharedModel.Meta.Academy.User.AcademyGuardiansSlot>>", GenericInstanceTypeCode, 0x10));
		var guardiansData = Declare(
			"SharedModel.Meta.Academy.User",
			"UserAcademyGuardiansData",
			systemObject,
			"System.Object",
			("SlotsByFraction", "System.Collections.Generic.Dictionary`2<SharedModel.Meta.Heroes.HeroFraction, SharedModel.Meta.Academy.User.AcademyGuardiansRaritySlots>", GenericInstanceTypeCode, 0x10));
		var userAcademyData = Declare(
			"SharedModel.Meta.Academy.User",
			"UserAcademyData",
			systemObject,
			"System.Object",
			("SeenGuide", "System.Boolean", BooleanTypeCode, 0x10),
			("Guardians", "SharedModel.Meta.Academy.User.UserAcademyGuardiansData", ClassTypeCode, 0x18));
		var updatableAcademyData = Declare("Client.Model.Gameplay.Academy.Data", "UpdatableUserAcademyData", userAcademyData, "SharedModel.Meta.Academy.User.UserAcademyData");
		var academyWrapperReadOnly = Declare(
			"Client.Model.Gameplay.Academy",
			"AcademyWrapperReadOnly",
			systemObject,
			"System.Object",
			("AcademyData", "Client.Model.Gameplay.Academy.Data.UpdatableUserAcademyData", ClassTypeCode, 0x10));
		var academyWrapper = Declare("Client.Model.Gameplay.Academy", "AcademyWrapper", academyWrapperReadOnly, "Client.Model.Gameplay.Academy.AcademyWrapperReadOnly");

		void Position(ulong at, int? heroId)
		{
			if (heroId is { } id)
			{
				_heap.Bytes(at, [1]);
				_heap.Int32(at + 0x4, id);
			}
		}

		var slots = _runtime.ListType(slot, "AcademyGuardiansSlot", sizeof(ulong));
		var byRarity = _runtime.DictionaryType(ValueTypeTypeCode, ClassTypeCode);
		_guardians = _runtime.Dictionary(_runtime.DictionaryType(ValueTypeTypeCode, ClassTypeCode), [.. guardianData.SlotsByFaction.Select(faction =>
		{
			var raritySlotsObject = _runtime.Object(raritySlots, 0x18);
			_heap.Pointer(raritySlotsObject + 0x10, _runtime.Dictionary(byRarity, [.. faction.Value.Select(rarity => (
				(ulong)rarity.Key,
				_guardianSlots[(faction.Key, rarity.Key)] = _runtime.List(slots, [.. rarity.Value.Select(synthetic =>
				{
					var slotObject = _runtime.Object(slot, 0x30);
					Position(slotObject + 0x10, synthetic.FirstHero);
					Position(slotObject + 0x18, synthetic.SecondHero);
					return slotObject;
				})])))]));
			return ((ulong)faction.Key, raritySlotsObject);
		})]);
		var guardiansDataObject = _runtime.Object(guardiansData, 0x18);
		_heap.Pointer(guardiansDataObject + 0x10, _guardians);
		var updatableAcademyDataObject = _runtime.Object(updatableAcademyData, 0x20);
		_heap.Pointer(updatableAcademyDataObject + 0x18, guardiansDataObject);
		var academyWrapperObject = _runtime.Object(academyWrapper, 0x18);
		_heap.Pointer(academyWrapperObject + 0x10, updatableAcademyDataObject);
		Slot("Client.Model.Guard.UserWrapper.Academy", userWrapperObject + 0x50, academyWrapperObject);
	}
}
