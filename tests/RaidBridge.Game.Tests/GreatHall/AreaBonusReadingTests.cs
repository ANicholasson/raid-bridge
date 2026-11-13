using RaidBridge.Game.Testing;
using RaidBridge.Game.Testing.GreatHall;

namespace RaidBridge.Game.Tests.GreatHall;

/// <summary>
/// The Account's Area Bonuses, read end to end through the fake memory port: a synthetic Game client in, each area's
/// levels under the game's own names in the game's order, or a named refusal, out
/// (https://github.com/ANicholasson/raid-bridge/issues/32). Nothing here looks at the reader's internals.
/// </summary>
public sealed class AreaBonusReadingTests : IDisposable
{
	private readonly SyntheticGameClient _game = SyntheticGameClient.Create();

	/// <summary>
	/// The game keeps the areas and the stats out of order; they come out in the order of its enums. IceGolemCave, held
	/// as null, and Hydra, held empty, have no levels and are left out.
	/// </summary>
	[Fact]
	public void Reads_the_area_bonuses_in_the_game_order()
	{
		Assert.Equal(
			[("DragonsLair", "Speed", 10), ("AllianceBoss", "Health", 1), ("AllianceBoss", "Attack", 3)],
			Levels(_game));
	}

	[Fact]
	public void Reads_nothing_when_no_area_bonus_is_bought()
	{
		using var game = SyntheticGameClient.Create(greatHallData: SyntheticGreatHallData.None);

		Assert.Empty(game.Source().Read().AreaBonuses);
	}

	/// <summary>The static data is needed only to check a level, so Area Bonuses with none read without the tiers.</summary>
	[Fact]
	public void Reads_no_area_bonuses_without_the_tiers()
	{
		using var game = SyntheticGameClient.Create(greatHallData: SyntheticGreatHallData.None);
		game.PointField("SharedModel.Meta.ClientStaticData", "VillageData", SyntheticGameClient.Unmapped);

		Assert.Empty(game.Source().Read().AreaBonuses);
	}

	/// <summary>The Great Hall and its Area Bonuses are read apart: levels in one don't put levels in the other.</summary>
	[Fact]
	public void Reads_area_bonuses_beside_an_empty_great_hall()
	{
		using var game = SyntheticGameClient.Create(greatHallData: SyntheticGreatHallData.OnlyArea(area: 1, new Dictionary<int, int> { [10] = 1, [6] = 10 }));

		var account = game.Source().Read();

		Assert.Empty(account.GreatHall);
		Assert.Equal([("PotionKeeps", "Accuracy", 10), ("PotionKeeps", "IgnoreDefence", 1)], Levels(game));
	}

	/// <summary>A Great Hall built for one Affinity holds no Area Bonus, so tiers that list no area check nothing (PR #44).</summary>
	[Fact]
	public void Reads_no_area_bonuses_beside_a_great_hall_of_one_affinity()
	{
		using var game = SyntheticGameClient.Create(greatHallData: SyntheticGreatHallData.Only(element: 1, new Dictionary<int, int> { [1] = 1 }) with { Tiers = [] });

		var account = game.Source().Read();

		Assert.Empty(account.AreaBonuses);
		Assert.NotEmpty(account.GreatHall);
	}

	[Fact]
	public void Refuses_an_area_the_game_does_not_name()
	{
		using var game = SyntheticGameClient.Create(greatHallData: SyntheticGreatHallData.OnlyArea(area: 99, new Dictionary<int, int> { [1] = 1 }));

		RefusesMatching(game, @"^System\.Collections\.Generic\.Dictionary`2\+Entry\.key at 0x[0-9A-F]+ is 99, which isn't a member of the game's SharedModel\.Meta\.Village\.ObservatoryLocationId\.$");
	}

	[Fact]
	public void Refuses_a_stat_the_game_does_not_name()
	{
		using var game = SyntheticGameClient.Create(greatHallData: SyntheticGreatHallData.OnlyArea(area: 7, new Dictionary<int, int> { [99] = 1 }));

		RefusesMatching(game, @"^System\.Collections\.Generic\.Dictionary`2\+Entry\.key at 0x[0-9A-F]+ is 99, which isn't a member of the game's SharedModel\.Battle\.Effects\.StatKindId\.$");
	}

	/// <summary>Siege is an area the game names, but no tier lists it, as in the game's own static data.</summary>
	[Fact]
	public void Refuses_a_level_in_an_area_no_tier_lists()
	{
		using var game = SyntheticGameClient.Create(greatHallData: SyntheticGreatHallData.OnlyArea(area: 14, new Dictionary<int, int> { [1] = 1 }));

		RefusesMatching(game, @"^SharedModel\.Meta\.Village\.UserVillageData\.ObservatoryBonusLevelByStatByLocation\[Siege\]\[Health\] at 0x[0-9A-F]+ is 1, but no SharedModel\.Meta\.Village\.ObservatoryTierInfo in SharedModel\.Meta\.Village\.StaticVillageData\.ObservatoryTiers lists Siege\.$");
	}

	/// <summary>CriticalChance is a stat the game names, but not one the tiers raise.</summary>
	[Fact]
	public void Refuses_a_stat_the_area_tier_has_no_levels_of()
	{
		using var game = SyntheticGameClient.Create(greatHallData: SyntheticGreatHallData.OnlyArea(area: 4, new Dictionary<int, int> { [7] = 1 }));

		RefusesMatching(game, @"^SharedModel\.Meta\.Village\.UserVillageData\.ObservatoryBonusLevelByStatByLocation\[DragonsLair\]\[CriticalChance\] at 0x[0-9A-F]+ is 1, but the SharedModel\.Meta\.Village\.ObservatoryTierInfo that lists DragonsLair holds no levels of CriticalChance\.$");
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(11)]
	public void Refuses_a_level_outside_the_game_range(int level)
	{
		using var game = SyntheticGameClient.Create(greatHallData: SyntheticGreatHallData.OnlyArea(area: 6, new Dictionary<int, int> { [4] = level }));

		RefusesMatching(game, $@"^SharedModel\.Meta\.Village\.UserVillageData\.ObservatoryBonusLevelByStatByLocation\[Hydra\]\[Speed\] at 0x[0-9A-F]+ is {level}, where raid-bridge expects a level from 1 to 10\.$");
	}

	/// <summary>The range is the area's own tier's, not another tier's nor a number raid-bridge holds.</summary>
	[Fact]
	public void Takes_the_highest_level_from_the_area_tier()
	{
		using var game = SyntheticGameClient.Create(greatHallData: SyntheticGreatHallData.OnlyArea(area: 2, new Dictionary<int, int> { [1] = 6 }) with
		{
			Tiers = [new([1], SyntheticGreatHallData.TenAreaLevels), new([2, 4], new Dictionary<int, int> { [1] = 5 })],
		});

		RefusesMatching(game, @"^SharedModel\.Meta\.Village\.UserVillageData\.ObservatoryBonusLevelByStatByLocation\[IceGolemCave\]\[Health\] at 0x[0-9A-F]+ is 6, where raid-bridge expects a level from 1 to 5\.$");
	}

	/// <summary>A stat a tier lists no bonus for has no levels, the same as one it doesn't list.</summary>
	[Fact]
	public void Refuses_a_stat_the_tier_lists_no_bonus_for()
	{
		using var game = SyntheticGameClient.Create(greatHallData: SyntheticGreatHallData.OnlyArea(area: 1, new Dictionary<int, int> { [3] = 1 }) with
		{
			Tiers = [new([1], new Dictionary<int, int> { [3] = 0 })],
		});

		RefusesMatching(game, @"^SharedModel\.Meta\.Village\.UserVillageData\.ObservatoryBonusLevelByStatByLocation\[PotionKeeps\]\[Defence\] at 0x[0-9A-F]+ is 1, but the SharedModel\.Meta\.Village\.ObservatoryTierInfo that lists PotionKeeps holds no levels of Defence\.$");
	}

	/// <summary>The game finds an area's tier by GetTierInfoOrThrow, so an area two tiers list has no one table to check against.</summary>
	[Fact]
	public void Refuses_an_area_two_tiers_list()
	{
		using var game = SyntheticGameClient.Create(greatHallData: SyntheticGreatHallData.Default with
		{
			Tiers = [new([1, 4], SyntheticGreatHallData.TenAreaLevels), new([2, 4], SyntheticGreatHallData.TenAreaLevels), new([6, 7], SyntheticGreatHallData.TenAreaLevels)],
		});

		RefusesMatching(game, @"^System\.Collections\.Generic\.List`1\._items\[1\] at 0x[0-9A-F]+ is DragonsLair, which another SharedModel\.Meta\.Village\.ObservatoryTierInfo in SharedModel\.Meta\.Village\.StaticVillageData\.ObservatoryTiers lists too, where raid-bridge expects one tier for each area\.$");
	}

	[Fact]
	public void Refuses_a_tier_that_is_not_an_object()
	{
		_game.PointAreaBonusTier(1, SyntheticGameClient.Unmapped);

		RefusesMatching(_game, @"^System\.Collections\.Generic\.List`1\._items\[1\] at 0x[0-9A-F]+ points to 0xDEAD00000000, which isn't an object\. ");
	}

	[Fact]
	public void Refuses_area_bonuses_whose_counts_disagree_with_their_slots()
	{
		_game.SetAreaBonusCounts(count: 9, freeCount: 0);

		RefusesMatching(_game, @"^SharedModel\.Meta\.Village\.UserVillageData\.ObservatoryBonusLevelByStatByLocation at 0x[0-9A-F]+ is malformed: its _count is 9 and its _freeCount 0, with 5 slots\.$");
	}

	/// <summary>An error about an area's levels names the dictionary and the area, not only the class library's Entry.value.</summary>
	[Fact]
	public void Refuses_an_area_whose_levels_are_not_an_object_naming_the_area()
	{
		_game.PointAreaBonusArea(0, SyntheticGameClient.Unmapped);

		RefusesMatching(_game, @"^SharedModel\.Meta\.Village\.UserVillageData\.ObservatoryBonusLevelByStatByLocation\[AllianceBoss\]: System\.Collections\.Generic\.Dictionary`2\+Entry\.value at 0x[0-9A-F]+ points to 0xDEAD00000000, which isn't an object\. ");
	}

	[Fact]
	public void Refuses_an_area_whose_levels_are_malformed_naming_the_area()
	{
		_game.SetAreaBonusLevelCounts(area: 4, count: 7, freeCount: 0);

		RefusesMatching(_game, @"^SharedModel\.Meta\.Village\.UserVillageData\.ObservatoryBonusLevelByStatByLocation\[DragonsLair\] at 0x[0-9A-F]+ is malformed: its _count is 7 and its _freeCount 0, with 2 slots\.$");
	}

	[Fact]
	public void Refuses_an_area_bonus_field_missing_from_the_metadata()
	{
		_game.UndeclareField("SharedModel.Meta.Village.UserVillageData", "ObservatoryBonusLevelByStatByLocation");

		Refuses(_game, "The game's class Client.Model.Gameplay.Village.Data.UpdatableVillageData has no field ObservatoryBonusLevelByStatByLocation, base classes included.");
	}

	[Fact]
	public void Refuses_static_area_bonus_data_missing_from_the_metadata()
	{
		_game.UndeclareField("SharedModel.Meta.Village.StaticVillageData", "ObservatoryTiers");

		Refuses(_game, "The game's class SharedModel.Meta.Village.StaticVillageData has no field ObservatoryTiers, base classes included.");
	}

	[Fact]
	public void Refuses_an_area_enum_missing_a_member_the_game_holds()
	{
		_game.RemoveEnumMember("SharedModel.Meta.Village.ObservatoryLocationId", "AllianceBoss");

		RefusesMatching(_game, @"^System\.Collections\.Generic\.Dictionary`2\+Entry\.key at 0x[0-9A-F]+ is 7, which isn't a member of the game's SharedModel\.Meta\.Village\.ObservatoryLocationId\.$");
	}

	public void Dispose() => _game.Dispose();

	private static IEnumerable<(string Area, string Stat, int Level)> Levels(SyntheticGameClient game) =>
		game.Source().Read().AreaBonuses.SelectMany(area => area.Levels.Select(level => (area.Area, level.Stat, level.Level)));

	private static void Refuses(SyntheticGameClient game, string message) =>
		Assert.Equal(message, Assert.ThrowsAny<GameClientException>(() => game.Source().Read()).Message);

	private static void RefusesMatching(SyntheticGameClient game, string pattern) =>
		Assert.Matches(pattern, Assert.ThrowsAny<GameClientException>(() => game.Source().Read()).Message);
}
