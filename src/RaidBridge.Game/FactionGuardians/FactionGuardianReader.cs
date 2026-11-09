using RaidBridge.Domain.Champions;
using RaidBridge.Domain.FactionGuardians;
using RaidBridge.Game.Il2Cpp;

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

	public static IReadOnlyList<GuardianFaction> Read(ManagedObject userWrapper, IReadOnlyList<Champion> champions)
	{
		var guardians = new Guardians(champions.ToDictionary(champion => champion.Id));
		return [.. userWrapper
			.Object("Academy", "Client.Model.Gameplay.Academy.AcademyWrapper")
			.Object("AcademyData", "Client.Model.Gameplay.Academy.Data.UpdatableUserAcademyData")
			.Object("Guardians", "SharedModel.Meta.Academy.User.UserAcademyGuardiansData")
			.Dictionary("SlotsByFraction", $"System.Collections.Generic.Dictionary`2<{Fraction}, {RaritySlots}>")
			.ByEnumKey(Fraction)
			.OrderBy(faction => faction.Key.Value)
			.Select(faction => new GuardianFaction(faction.Key.Name, Rarities(faction.Key.Name, faction.Value.Object(RaritySlots), guardians)))
			.Where(faction => faction.Rarities.Count > 0)];
	}

	/// <summary>A rarity none of whose slots holds a Champion is left out, as a slot that holds none is.</summary>
	private static IReadOnlyList<GuardianRarity> Rarities(string faction, ManagedObject raritySlots, Guardians guardians) =>
		[.. raritySlots
			.Dictionary("SlotsByRarity", $"System.Collections.Generic.Dictionary`2<{Rarity}, System.Collections.Generic.List`1<{Slot}>>")
			.ByEnumKey(Rarity)
			.OrderBy(rarity => rarity.Key.Value)
			.Select(rarity => new GuardianRarity(
				rarity.Key.Name,
				[.. rarity.Value.List().Objects(Slot)
					.Select((slot, index) => new GuardianSlot(
					[
						.. new[] { slot.NullableInt32("FirstHero"), slot.NullableInt32("SecondHero") }
							.Where(position => position.Value is not null)
							.Select(position => guardians.Place(position, new(faction, rarity.Key.Name, index + 1))),
					]))
					.Where(slot => slot.ChampionIds.Count > 0)]))
			.Where(rarity => rarity.Slots.Count > 0)];

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
