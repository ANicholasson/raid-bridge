using RaidBridge.Game.Testing;
using RaidBridge.Game.Testing.GreatHall;

namespace RaidBridge.Game.Tests.GreatHall;

/// <summary>
/// The Account's Great Hall, read end to end through the fake memory port: a synthetic Game client in, each Affinity's
/// levels under the game's own names in the game's order, or a named refusal, out (spec #32, #35). Nothing here looks
/// at the reader's internals.
/// </summary>
public sealed class GreatHallReadingTests : IDisposable
{
	private readonly SyntheticGameClient _game = SyntheticGameClient.Create();

	/// <summary>
	/// The game keeps the Affinities and the stats out of order; they come out in the order of its enums. Force, held as
	/// null, and Spirit, held empty, have no levels and are left out.
	/// </summary>
	[Fact]
	public void Reads_a_partly_built_great_hall_in_the_game_order()
	{
		Assert.Equal(
			[("Magic", "Attack", 3), ("Void", "Health", 10), ("Void", "CriticalDamage", 2)],
			Levels(_game));
	}

	[Fact]
	public void Reads_nothing_from_a_great_hall_with_no_levels()
	{
		using var game = SyntheticGameClient.Create(greatHallData: SyntheticGreatHallData.None);

		Assert.Empty(game.Source().Read().GreatHall);
	}

	/// <summary>Level 1 and the highest level the static data lists a bonus for are both in the game's range.</summary>
	[Fact]
	public void Reads_the_lowest_and_highest_levels()
	{
		using var game = SyntheticGameClient.Create(greatHallData: SyntheticGreatHallData.Only(element: 3, new Dictionary<int, int> { [5] = 1, [6] = 10 }));

		Assert.Equal([("Spirit", "Resistance", 1), ("Spirit", "Accuracy", 10)], Levels(game));
	}

	[Fact]
	public void Refuses_an_affinity_the_game_does_not_name()
	{
		using var game = SyntheticGameClient.Create(greatHallData: SyntheticGreatHallData.Only(element: 9, new Dictionary<int, int> { [1] = 1 }));

		RefusesMatching(game, @"^System\.Collections\.Generic\.Dictionary`2\+Entry\.key at 0x[0-9A-F]+ is 9, which isn't a member of the game's SharedModel\.Meta\.Heroes\.Element\.$");
	}

	[Fact]
	public void Refuses_a_stat_the_game_does_not_name()
	{
		using var game = SyntheticGameClient.Create(greatHallData: SyntheticGreatHallData.Only(element: 1, new Dictionary<int, int> { [99] = 1 }));

		RefusesMatching(game, @"^System\.Collections\.Generic\.Dictionary`2\+Entry\.key at 0x[0-9A-F]+ is 99, which isn't a member of the game's SharedModel\.Battle\.Effects\.StatKindId\.$");
	}

	/// <summary>Speed is a stat the game names, but not one the Great Hall raises, so the static data lists no levels of it.</summary>
	[Fact]
	public void Refuses_a_stat_the_great_hall_has_no_levels_of()
	{
		using var game = SyntheticGameClient.Create(greatHallData: SyntheticGreatHallData.Only(element: 1, new Dictionary<int, int> { [4] = 1 }));

		RefusesMatching(game, @"^SharedModel\.Meta\.Village\.UserVillageData\.CapitolBonusLevelByStatByElement\[Magic\]\[Speed\] at 0x[0-9A-F]+ is 1, but SharedModel\.Meta\.Village\.StaticVillageData\.CapitolBonusByStatKind holds no levels of Speed\.$");
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(11)]
	public void Refuses_a_level_outside_the_game_range(int level)
	{
		using var game = SyntheticGameClient.Create(greatHallData: SyntheticGreatHallData.Only(element: 2, new Dictionary<int, int> { [3] = level }));

		RefusesMatching(game, $@"^SharedModel\.Meta\.Village\.UserVillageData\.CapitolBonusLevelByStatByElement\[Force\]\[Defence\] at 0x[0-9A-F]+ is {level}, where raid-bridge expects a level from 1 to 10\.$");
	}

	/// <summary>The range is the static data's, not a number raid-bridge holds: a stat the game lists five levels of goes to 5.</summary>
	[Fact]
	public void Takes_the_highest_level_from_the_static_data()
	{
		using var game = SyntheticGameClient.Create(greatHallData: new(
			new Dictionary<int, IReadOnlyDictionary<int, int>?> { [1] = new Dictionary<int, int> { [1] = 6 } },
			new Dictionary<int, int> { [1] = 5 }));

		RefusesMatching(game, @"^SharedModel\.Meta\.Village\.UserVillageData\.CapitolBonusLevelByStatByElement\[Magic\]\[Health\] at 0x[0-9A-F]+ is 6, where raid-bridge expects a level from 1 to 5\.$");
	}

	/// <summary>A stat the static data lists no bonus for has no levels, the same as one it doesn't list.</summary>
	[Fact]
	public void Refuses_a_stat_the_static_data_lists_no_bonus_for()
	{
		using var game = SyntheticGameClient.Create(greatHallData: new(
			new Dictionary<int, IReadOnlyDictionary<int, int>?> { [1] = new Dictionary<int, int> { [1] = 1 } },
			new Dictionary<int, int> { [1] = 0 }));

		RefusesMatching(game, @"^SharedModel\.Meta\.Village\.UserVillageData\.CapitolBonusLevelByStatByElement\[Magic\]\[Health\] at 0x[0-9A-F]+ is 1, but SharedModel\.Meta\.Village\.StaticVillageData\.CapitolBonusByStatKind holds no levels of Health\.$");
	}

	/// <summary>Each bonus the static data lists must be one, or the highest level would count a slot that holds none.</summary>
	[Fact]
	public void Refuses_a_static_bonus_that_is_not_one()
	{
		_game.PointGreatHallBonus(stat: 8, index: 9, 0);

		RefusesMatching(_game, @"^System\.Collections\.Generic\.List`1\._items\[9\] at 0x[0-9A-F]+ is null, where raid-bridge expects a SharedModel\.Meta\.Artifacts\.BonusValue\.$");
	}

	/// <summary>The static data is needed only to check a level, so a Great Hall with none reads without it.</summary>
	[Fact]
	public void Reads_a_great_hall_with_no_levels_without_the_static_data()
	{
		using var game = SyntheticGameClient.Create(greatHallData: SyntheticGreatHallData.None);
		game.PointField("SharedModel.Meta.ClientStaticData", "VillageData", SyntheticGameClient.Unmapped);

		Assert.Empty(game.Source().Read().GreatHall);
	}

	[Fact]
	public void Refuses_a_great_hall_whose_counts_disagree_with_its_slots()
	{
		_game.SetGreatHallCounts(count: 9, freeCount: 0);

		RefusesMatching(_game, @"^SharedModel\.Meta\.Village\.UserVillageData\.CapitolBonusLevelByStatByElement at 0x[0-9A-F]+ is malformed: its _count is 9 and its _freeCount 0, with 5 slots\.$");
	}

	/// <summary>An error about an Affinity's levels names the dictionary and the Affinity, not only the class library's Entry.value.</summary>
	[Fact]
	public void Refuses_an_affinity_whose_levels_are_not_an_object_naming_the_affinity()
	{
		_game.PointGreatHallAffinity(0, SyntheticGameClient.Unmapped);

		RefusesMatching(_game, @"^SharedModel\.Meta\.Village\.UserVillageData\.CapitolBonusLevelByStatByElement\[Void\]: System\.Collections\.Generic\.Dictionary`2\+Entry\.value at 0x[0-9A-F]+ points to 0xDEAD00000000, which isn't an object\. ");
	}

	/// <summary>An error about an Affinity's own dictionary of levels names the Affinity too.</summary>
	[Fact]
	public void Refuses_an_affinity_whose_levels_are_malformed_naming_the_affinity()
	{
		_game.SetGreatHallLevelCounts(element: 4, count: 7, freeCount: 0);

		RefusesMatching(_game, @"^SharedModel\.Meta\.Village\.UserVillageData\.CapitolBonusLevelByStatByElement\[Void\] at 0x[0-9A-F]+ is malformed: its _count is 7 and its _freeCount 0, with 3 slots\.$");
	}

	[Fact]
	public void Refuses_a_village_that_is_not_an_object()
	{
		_game.PointField("Client.Model.Guard.UserWrapper", "Village", SyntheticGameClient.Unmapped);

		RefusesMatching(_game, @"^Client\.Model\.Guard\.UserWrapper\.Village at 0x[0-9A-F]+ points to 0xDEAD00000000, which isn't an object\. ");
	}

	[Fact]
	public void Refuses_a_great_hall_field_missing_from_the_metadata()
	{
		_game.UndeclareField("SharedModel.Meta.Village.UserVillageData", "CapitolBonusLevelByStatByElement");

		Refuses(_game, "The game's class Client.Model.Gameplay.Village.Data.UpdatableVillageData has no field CapitolBonusLevelByStatByElement, base classes included.");
	}

	[Fact]
	public void Refuses_a_village_class_missing_from_the_metadata()
	{
		_game.RenameClass("Client.Model.Gameplay.Village.VillageWrapper", "Client.Model.Gameplay.Village.TownWrapper");

		Refuses(_game, "The game's metadata has no class Client.Model.Gameplay.Village.VillageWrapper.");
	}

	[Fact]
	public void Refuses_static_great_hall_data_missing_from_the_metadata()
	{
		_game.UndeclareField("SharedModel.Meta.Village.StaticVillageData", "CapitolBonusByStatKind");

		Refuses(_game, "The game's class SharedModel.Meta.Village.StaticVillageData has no field CapitolBonusByStatKind, base classes included.");
	}

	/// <summary>A game update that made the levels anything but ints is refused rather than read as ints.</summary>
	[Fact]
	public void Refuses_levels_of_another_type()
	{
		_game.RetypeField(
			"SharedModel.Meta.Village.UserVillageData",
			"CapitolBonusLevelByStatByElement",
			"System.Collections.Generic.Dictionary`2<SharedModel.Meta.Heroes.Element, System.Collections.Generic.Dictionary`2<SharedModel.Battle.Effects.StatKindId, System.Int64>>");

		RefusesMatching(
			_game,
			@"^The game's SharedModel\.Meta\.Village\.UserVillageData\.CapitolBonusLevelByStatByElement is .*System\.Int64>>.*, where raid-bridge expects .*System\.Int32>>.*\.$");
	}

	public void Dispose() => _game.Dispose();

	private static IEnumerable<(string Affinity, string Stat, int Level)> Levels(SyntheticGameClient game) =>
		game.Source().Read().GreatHall.SelectMany(affinity => affinity.Levels.Select(level => (affinity.Affinity, level.Stat, level.Level)));

	private static void Refuses(SyntheticGameClient game, string message) =>
		Assert.Equal(message, Assert.ThrowsAny<GameClientException>(() => game.Source().Read()).Message);

	private static void RefusesMatching(SyntheticGameClient game, string pattern) =>
		Assert.Matches(pattern, Assert.ThrowsAny<GameClientException>(() => game.Source().Read()).Message);
}
