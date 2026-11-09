using RaidBridge.Game.Testing;
using RaidBridge.Game.Testing.Champions;
using RaidBridge.Game.Testing.FactionGuardians;

namespace RaidBridge.Game.Tests.FactionGuardians;

/// <summary>
/// The Account's Faction Guardians, read end to end through the fake memory port: a synthetic Game client in, each
/// Faction's slots of each rarity under the game's own names in the game's order, or a named refusal, out (spec #32,
/// #36). Nothing here looks at the reader's internals.
/// </summary>
public sealed class FactionGuardianReadingTests : IDisposable
{
	private readonly SyntheticGameClient _game = SyntheticGameClient.Create();

	/// <summary>
	/// The game keeps DarkElves before BannerLords; they come out in the order of its enum. Empty slots, the empty
	/// position of a half-filled slot, and HighElves' Epic slots, of which there are none, are left out.
	/// </summary>
	[Fact]
	public void Reads_the_faction_guardians_in_the_game_order()
	{
		Assert.Equal(
			[("BannerLords", "Rare", [7, 12]), ("DarkElves", "Legendary", [3])],
			Slots(_game));
	}

	[Fact]
	public void Reads_nothing_when_no_champion_is_placed()
	{
		using var game = SyntheticGameClient.Create(guardianData: SyntheticGuardianData.None);

		Assert.Empty(game.Source().Read().FactionGuardians);
	}

	/// <summary>The slots keep the order the game holds them in, each Champion in the position it fills, first or second.</summary>
	[Fact]
	public void Keeps_the_slots_in_the_game_order()
	{
		using var game = SyntheticGameClient.Create(guardianData: SyntheticGuardianData.Only(faction: 1, rarity: 3, [new(null, 12), new(null, null), new(7, null)]));

		Assert.Equal([("BannerLords", "Rare", [12]), ("BannerLords", "Rare", [7])], Slots(game));
	}

	/// <summary>The game keeps Legendary before Rare; they come out in the order of its enum.</summary>
	[Fact]
	public void Reads_the_rarities_of_a_faction_in_the_game_order()
	{
		var heroes = SyntheticHeroData.Default with
		{
			Heroes = [.. SyntheticHeroData.Default.Heroes, new(Id: 20, TypeId: 7000)],
			HeroTypes = [.. SyntheticHeroData.Default.HeroTypes, new(7000, new("l10n:hero-type/name?id=7000#static", "Synthetic Scout"), Rarity: 3, Fraction: 11, FormElements: [1])],
		};
		using var game = SyntheticGameClient.Create(
			heroData: heroes,
			guardianData: new(new Dictionary<int, IReadOnlyDictionary<int, IReadOnlyList<SyntheticGuardianSlot>>>
			{
				[11] = new Dictionary<int, IReadOnlyList<SyntheticGuardianSlot>> { [5] = [new(3, null)], [3] = [new(20, null)] },
			}));

		Assert.Equal([("DarkElves", "Rare", [20]), ("DarkElves", "Legendary", [3])], Slots(game));
	}

	[Fact]
	public void Refuses_a_guardian_the_account_does_not_hold()
	{
		using var game = SyntheticGameClient.Create(guardianData: SyntheticGuardianData.Only(faction: 1, rarity: 3, [new(7, 99)]));

		RefusesMatching(game, @"^SharedModel\.Meta\.Academy\.User\.AcademyGuardiansSlot\.SecondHero at 0x[0-9A-F]+ is 99, in slot 1 of BannerLords Rare, but HeroById holds no Champion 99\.$");
	}

	[Fact]
	public void Refuses_a_champion_guarding_in_two_slots()
	{
		using var game = SyntheticGameClient.Create(guardianData: SyntheticGuardianData.Only(faction: 1, rarity: 3, [new(7, 12), new(null, 7)]));

		RefusesMatching(game, @"^SharedModel\.Meta\.Academy\.User\.AcademyGuardiansSlot\.SecondHero at 0x[0-9A-F]+ is 7, in slot 2 of BannerLords Rare, but Champion 7 already guards in slot 1 of BannerLords Rare\.$");
	}

	[Fact]
	public void Refuses_a_champion_in_both_positions_of_a_slot()
	{
		using var game = SyntheticGameClient.Create(guardianData: SyntheticGuardianData.Only(faction: 1, rarity: 3, [new(12, 12)]));

		RefusesMatching(game, @"^SharedModel\.Meta\.Academy\.User\.AcademyGuardiansSlot\.SecondHero at 0x[0-9A-F]+ is 12, in slot 1 of BannerLords Rare, but Champion 12 already guards in slot 1 of BannerLords Rare\.$");
	}

	/// <summary>A slot of one Faction and rarity takes only a Champion of that Faction and rarity.</summary>
	[Theory]
	[InlineData(11, 3, "DarkElves Rare")]
	[InlineData(1, 5, "BannerLords Legendary")]
	public void Refuses_a_champion_of_another_faction_or_rarity(int faction, int rarity, string slot)
	{
		using var game = SyntheticGameClient.Create(guardianData: SyntheticGuardianData.Only(faction, rarity, [new(7, null)]));

		RefusesMatching(game, $@"^SharedModel\.Meta\.Academy\.User\.AcademyGuardiansSlot\.FirstHero at 0x[0-9A-F]+ is 7, in slot 1 of {slot}, but Champion 7 is BannerLords Rare\.$");
	}

	[Fact]
	public void Refuses_a_faction_the_game_does_not_name()
	{
		using var game = SyntheticGameClient.Create(guardianData: SyntheticGuardianData.Only(faction: 99, rarity: 3, [new(7, null)]));

		RefusesMatching(game, @"^System\.Collections\.Generic\.Dictionary`2\+Entry\.key at 0x[0-9A-F]+ is 99, which isn't a member of the game's SharedModel\.Meta\.Heroes\.HeroFraction\.$");
	}

	[Fact]
	public void Refuses_a_rarity_the_game_does_not_name()
	{
		using var game = SyntheticGameClient.Create(guardianData: SyntheticGuardianData.Only(faction: 1, rarity: 9, [new(7, null)]));

		RefusesMatching(game, @"^System\.Collections\.Generic\.Dictionary`2\+Entry\.key at 0x[0-9A-F]+ is 9, which isn't a member of the game's SharedModel\.Meta\.Heroes\.HeroRarity\.$");
	}

	[Fact]
	public void Refuses_guardians_whose_counts_disagree_with_their_slots()
	{
		_game.SetGuardianCounts(count: 9, freeCount: 0);

		RefusesMatching(_game, @"^SharedModel\.Meta\.Academy\.User\.UserAcademyGuardiansData\.SlotsByFraction at 0x[0-9A-F]+ is malformed: its _count is 9 and its _freeCount 0, with 4 slots\.$");
	}

	/// <summary>An error about a rarity's list of slots names the dictionary and the rarity.</summary>
	[Fact]
	public void Refuses_a_list_of_slots_that_claims_more_than_it_holds_naming_the_rarity()
	{
		_game.SetGuardianSlotCount(faction: 1, rarity: 3, count: 5);

		RefusesMatching(_game, @"^SharedModel\.Meta\.Academy\.User\.AcademyGuardiansRaritySlots\.SlotsByRarity\[Rare\] at 0x[0-9A-F]+ is malformed: its _size is 5, but its array holds 3\.$");
	}

	/// <summary>An error about a Faction's slots names the dictionary and the Faction, not only the class library's Entry.value.</summary>
	[Fact]
	public void Refuses_a_faction_whose_slots_are_not_an_object_naming_the_faction()
	{
		_game.PointGuardianFaction(0, SyntheticGameClient.Unmapped);

		RefusesMatching(_game, @"^SharedModel\.Meta\.Academy\.User\.UserAcademyGuardiansData\.SlotsByFraction\[DarkElves\]: System\.Collections\.Generic\.Dictionary`2\+Entry\.value at 0x[0-9A-F]+ points to 0xDEAD00000000, which isn't an object\. ");
	}

	[Fact]
	public void Refuses_an_academy_that_is_not_an_object()
	{
		_game.PointField("Client.Model.Guard.UserWrapper", "Academy", SyntheticGameClient.Unmapped);

		RefusesMatching(_game, @"^Client\.Model\.Guard\.UserWrapper\.Academy at 0x[0-9A-F]+ points to 0xDEAD00000000, which isn't an object\. ");
	}

	[Fact]
	public void Refuses_a_guardians_field_missing_from_the_metadata()
	{
		_game.UndeclareField("SharedModel.Meta.Academy.User.UserAcademyData", "Guardians");

		Refuses(_game, "The game's class Client.Model.Gameplay.Academy.Data.UpdatableUserAcademyData has no field Guardians, base classes included.");
	}

	[Fact]
	public void Refuses_a_slot_position_missing_from_the_metadata()
	{
		_game.UndeclareField("SharedModel.Meta.Academy.User.AcademyGuardiansSlot", "SecondHero");

		Refuses(_game, "The game's class SharedModel.Meta.Academy.User.AcademyGuardiansSlot has no field SecondHero, base classes included.");
	}

	[Fact]
	public void Refuses_an_academy_class_missing_from_the_metadata()
	{
		_game.RenameClass("Client.Model.Gameplay.Academy.AcademyWrapper", "Client.Model.Gameplay.Academy.SchoolWrapper");

		Refuses(_game, "The game's metadata has no class Client.Model.Gameplay.Academy.AcademyWrapper.");
	}

	/// <summary>A game update that keeps the Factions behind a wrapper again, as Raid Toolkit read them, is refused rather than read as a dictionary.</summary>
	[Fact]
	public void Refuses_factions_of_another_type()
	{
		_game.RetypeField(
			"SharedModel.Meta.Academy.User.UserAcademyGuardiansData",
			"SlotsByFraction",
			"Plarium.Common.Collections.EnumDictionary`2<SharedModel.Meta.Heroes.HeroFraction, SharedModel.Meta.Academy.User.AcademyGuardiansRaritySlots>");

		RefusesMatching(_game, @"^The game's SharedModel\.Meta\.Academy\.User\.UserAcademyGuardiansData\.SlotsByFraction is a Plarium\.Common\.Collections\.EnumDictionary`2<.*>, where raid-bridge expects a System\.Collections\.Generic\.Dictionary`2<.*>\.$");
	}

	public void Dispose() => _game.Dispose();

	private static IEnumerable<(string Faction, string Rarity, int[] ChampionIds)> Slots(SyntheticGameClient game) =>
		game.Source().Read().FactionGuardians.SelectMany(faction => faction.Rarities.SelectMany(rarity => rarity.Slots.Select(slot => (faction.Faction, rarity.Rarity, slot.ChampionIds.ToArray()))));

	private static void Refuses(SyntheticGameClient game, string message) =>
		Assert.Equal(message, Assert.ThrowsAny<GameClientException>(() => game.Source().Read()).Message);

	private static void RefusesMatching(SyntheticGameClient game, string pattern) =>
		Assert.Matches(pattern, Assert.ThrowsAny<GameClientException>(() => game.Source().Read()).Message);
}
