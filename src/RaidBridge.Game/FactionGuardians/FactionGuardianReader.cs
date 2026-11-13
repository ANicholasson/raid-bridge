using RaidBridge.Domain.Champions;
using RaidBridge.Domain.FactionGuardians;
using RaidBridge.Domain.Stats;
using RaidBridge.Game.Il2Cpp;
using RaidBridge.Game.Metadata;
using RaidBridge.Game.Stats;

namespace RaidBridge.Game.FactionGuardians;

/// <summary>
/// Reads the Account's Faction Guardians from the game's model (spec #32, #36):
/// <c>_userWrapper.Academy.AcademyData.Guardians.SlotsByFraction</c>, each Faction's slots by <c>HeroFraction</c>, then
/// by <c>HeroRarity</c> in <c>SlotsByRarity</c>, each in the enum's order, and each slot's <c>FirstHero</c> and
/// <c>SecondHero</c>. Raid Toolkit's AcademyExtension named the route, which the installed game still declares at
/// metadata v39, but with plain dictionaries where Raid Toolkit read through a wrapper's <c>UnderlyingDictionary</c>.
/// Each Guardian must be a Champion on the Account of the slot's Faction and rarity, guarding only once.
/// </summary>
internal static class FactionGuardianReader
{
	private const string Fraction = "SharedModel.Meta.Heroes.HeroFraction";
	private const string Rarity = "SharedModel.Meta.Heroes.HeroRarity";
	private const string RaritySlots = "SharedModel.Meta.Academy.User.AcademyGuardiansRaritySlots";
	private const string Slot = "SharedModel.Meta.Academy.User.AcademyGuardiansSlot";
	private const string StatBonuses = "SharedModel.Meta.Academy.Static.StatBonuses";
	private const string StatKind = "SharedModel.Battle.Effects.StatKindId";

	/// <summary>
	/// The Account's guardian slots, read once, so the Faction Guardians written and what they give the Champions come
	/// from the same state of the Game client (PR #22): each Faction's, in the enum's order, then each
	/// rarity's, in the enum's order, then each slot's two positions, in the game's order.
	/// </summary>
	public static GuardianSlots Capture(ManagedObject userWrapper) =>
		new([.. userWrapper
			.Object("Academy", "Client.Model.Gameplay.Academy.AcademyWrapper")
			.Object("AcademyData", "Client.Model.Gameplay.Academy.Data.UpdatableUserAcademyData")
			.Object("Guardians", "SharedModel.Meta.Academy.User.UserAcademyGuardiansData")
			.Dictionary("SlotsByFraction", $"System.Collections.Generic.Dictionary`2<{Fraction}, {RaritySlots}>")
			.ByEnumKey(Fraction)
			.OrderBy(faction => faction.Key.Value)
			.SelectMany(faction => faction.Value.Object(RaritySlots)
				.Dictionary("SlotsByRarity", $"System.Collections.Generic.Dictionary`2<{Rarity}, System.Collections.Generic.List`1<{Slot}>>")
				.ByEnumKey(Rarity)
				.OrderBy(rarity => rarity.Key.Value)
				.Select(rarity => new GuardianSlots.Of(
					faction.Key,
					rarity.Key,
					[.. rarity.Value.List().Objects(Slot).Select(slot => (slot.NullableInt32("FirstHero"), slot.NullableInt32("SecondHero")))])))]);

	/// <summary>The Account's Faction Guardians, each a Champion on the Account of the slot's Faction and rarity, guarding only once.</summary>
	public static IReadOnlyList<GuardianFaction> Read(GuardianSlots captured, IReadOnlyList<Champion> champions)
	{
		var guardians = new Guardians(champions.ToDictionary(champion => champion.Id));
		return [.. captured.All
			.GroupBy(slots => slots.Faction)
			.Select(faction => new GuardianFaction(
				faction.Key.Name,
				[.. faction
					.Select(rarity => new GuardianRarity(
						rarity.Rarity.Name,
						[.. rarity.Slots
							.Select((slot, index) => new GuardianSlot(
							[
								.. new[] { slot.First, slot.Second }
									.Where(position => position.Value is not null)
									.Select(position => guardians.Place(position, new(faction.Key.Name, rarity.Rarity.Name, index + 1))),
							]))
							.Where(slot => slot.ChampionIds.Count > 0)]))
					.Where(rarity => rarity.Slots.Count > 0)]))
			.Where(faction => faction.Rarities.Count > 0)];
	}

	/// <summary>
	/// What the Faction Guardians give a Champion of each Faction and rarity, as the game's own
	/// <c>AcademySetup.Create</c> works it out at metadata v39 (https://github.com/ANicholasson/raid-bridge/issues/13):
	/// the guardian slots of the Champion's own Faction and rarity count in order while they are full, both positions
	/// filled, and slot <c>n</c> counted gives the static data's bonuses <c>n</c> for the rarity, in
	/// <c>AcademyData.Guardians.BonusesByHeroRarity</c>. The static data is read only once a slot counts.
	/// </summary>
	public static IReadOnlyDictionary<(string Faction, string Rarity), IReadOnlyList<StatBonus>> Bonuses(GuardianSlots captured, ManagedObject staticData)
	{
		var table = new Lazy<IReadOnlyDictionary<GameEnumMember, ManagedDictionary.Entry>>(() => staticData
			.Object("AcademyData", "SharedModel.Meta.Academy.Static.StaticAcademyData")
			.Object("Guardians", "SharedModel.Meta.Academy.Static.StaticAcademyGuardiansData")
			.Dictionary("BonusesByHeroRarity", $"System.Collections.Generic.Dictionary`2<{Rarity}, System.Collections.Generic.List`1<{StatBonuses}>>")
			.ByEnumKey(Rarity));
		var bonuses = new Dictionary<(string Faction, string Rarity), IReadOnlyList<StatBonus>>();
		foreach (var slots in captured.All)
		{
			var counted = slots.Slots.TakeWhile(slot => slot.First.Value is not null && slot.Second.Value is not null).Count();
			if (counted > 0)
			{
				bonuses[(slots.Faction.Name, slots.Rarity.Name)] = [.. Enumerable.Range(0, counted).SelectMany(index => SlotBonuses(table.Value, slots.Rarity, index))];
			}
		}

		return bonuses;
	}

	/// <summary>The bonuses the static data gives for the slot at <paramref name="index"/> of a rarity, each a BonusValue by stat.</summary>
	private static List<StatBonus> SlotBonuses(IReadOnlyDictionary<GameEnumMember, ManagedDictionary.Entry> table, GameEnumMember rarity, int index)
	{
		var where = $"SharedModel.Meta.Academy.Static.StaticAcademyGuardiansData.BonusesByHeroRarity[{rarity.Name}]";
		var slots = table.TryGetValue(rarity, out var entry)
			? entry.List().Objects(StatBonuses)
			: throw new GameDataException($"{where} doesn't exist, but a {rarity.Name} guardian slot is full.");
		return index < slots.Count
			? slots[index]
				.Dictionary("Bonuses", $"System.Collections.Generic.Dictionary`2<{StatKind}, SharedModel.Meta.Artifacts.BonusValue>")
				.ByEnumKey(StatKind)
				.OrderBy(bonus => bonus.Key.Value)
				.Select(bonus => StatNames.BonusOf(bonus.Key.Name, bonus.Value.Object("SharedModel.Meta.Artifacts.BonusValue"), $"{where}[{index}]"))
				.OfType<StatBonus>()
				.ToList()
			: throw new GameDataException($"{where} holds the bonuses of {slots.Count} slots, but slot {index + 1} is full.");
	}

	/// <summary>A guardian slot, by the Faction and rarity it is kept under and its place among them, counting from 1.</summary>
	private sealed record SlotName(string Faction, string Rarity, int Number)
	{
		public override string ToString() => $"slot {Number} of {Faction} {Rarity}";
	}

	/// <summary>The Champions placed so far, each checked against the Account's Champions and the slot it is in.</summary>
	private sealed class Guardians(IReadOnlyDictionary<int, Champion> champions)
	{
		private readonly Dictionary<int, SlotName> _placed = [];

		public int Place(FieldValue<int?> position, SlotName slot)
		{
			var id = position.Value!.Value;
			if (!champions.TryGetValue(id, out var champion))
			{
				throw new GameDataException($"{position}, in {slot}, but HeroById holds no Champion {id}.");
			}

			if (champion.Faction != slot.Faction || champion.Rarity != slot.Rarity)
			{
				throw new GameDataException($"{position}, in {slot}, but Champion {id} is {champion.Faction} {champion.Rarity}.");
			}

			return _placed.TryAdd(id, slot)
				? id
				: throw new GameDataException($"{position}, in {slot}, but Champion {id} already guards in {_placed[id]}.");
		}
	}
}

/// <summary>The Account's guardian slots, as read once: each Faction's slots of each rarity, each slot's two positions.</summary>
internal sealed record GuardianSlots(IReadOnlyList<GuardianSlots.Of> All)
{
	/// <summary>One Faction's slots of one rarity, in the game's order.</summary>
	public sealed record Of(GameEnumMember Faction, GameEnumMember Rarity, IReadOnlyList<(FieldValue<int?> First, FieldValue<int?> Second)> Slots);
}
