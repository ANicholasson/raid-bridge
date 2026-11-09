using RaidBridge.Domain.Resources;
using RaidBridge.Game.Testing;
using RaidBridge.Game.Testing.Resources;

namespace RaidBridge.Game.Tests.Resources;

/// <summary>
/// The Account's Resources, Shards and Items, read end to end through the fake memory port: a synthetic Game client
/// in, each under the game's own name in the game's order, or a named refusal, out (spec #32, #34). Nothing here looks
/// at the reader's internals.
/// </summary>
public sealed class ResourceReadingTests : IDisposable
{
	private static readonly SyntheticResourceData Held = SyntheticResourceData.Default;

	private readonly SyntheticGameClient _game = SyntheticGameClient.Create();

	/// <summary>The game keeps them out of order; they come out in the order of its enum, not of its slots or of their names.</summary>
	[Fact]
	public void Reads_each_resource_by_its_name_in_the_game_order()
	{
		Assert.Equal([new Resource("Energy", 130), new Resource("Silver", 1_250_000), new Resource("Gem", 42)], _game.Source().Read().Resources);
	}

	[Fact]
	public void Reads_each_shard_type_by_its_name_in_the_game_order()
	{
		Assert.Equal([new Shard("Mystery", 14), new Shard("Ancient", 2)], _game.Source().Read().Shards);
	}

	[Fact]
	public void Reads_each_item_by_its_name_in_the_game_order()
	{
		Assert.Equal([new Item("HeroXpBooster100P_1Day", 3), new Item("SkillUpgradeItemEpic", 1)], _game.Source().Read().Items);
	}

	/// <summary>The game holds an amount as a double: it is read as held, not rounded to a whole number.</summary>
	[Fact]
	public void Reads_a_resource_amount_exactly_as_the_game_holds_it()
	{
		using var game = SyntheticGameClient.Create(resourceData: Held with { Resources = new Dictionary<int, double> { [1] = 129.75 } });

		Assert.Equal(129.75, game.Source().Read().Resources.Single().Amount);
	}

	/// <summary>A count the game keeps at 0 is one it holds, so it is read, not dropped.</summary>
	[Fact]
	public void Reads_a_count_of_zero_the_game_keeps()
	{
		using var game = SyntheticGameClient.Create(resourceData: new(
			new Dictionary<int, double> { [2] = 0 },
			[new(Type: 6, Count: 0)],
			new Dictionary<int, int> { [5001] = 0 }));

		var account = game.Source().Read();

		Assert.Equal((new Resource("Silver", 0), new Shard("Void", 0), new Item("SkillUpgradeItemRare", 0)), (account.Resources.Single(), account.Shards.Single(), account.Items.Single()));
	}

	[Fact]
	public void Reads_nothing_from_an_account_that_holds_nothing()
	{
		using var game = SyntheticGameClient.Create(resourceData: SyntheticResourceData.None);

		var account = game.Source().Read();

		Assert.Equal((0, 0, 0), (account.Resources.Count, account.Shards.Count, account.Items.Count));
	}

	[Fact]
	public void Refuses_a_resource_the_game_does_not_name()
	{
		using var game = SyntheticGameClient.Create(resourceData: Held with { Resources = new Dictionary<int, double> { [99] = 1 } });

		RefusesMatching(game, @"^System\.Collections\.Generic\.Dictionary`2\+Entry\.key at 0x[0-9A-F]+ is 99, which isn't a member of the game's SharedModel\.Meta\.Account\.ResourceTypeId\.$");
	}

	[Fact]
	public void Refuses_a_shard_type_the_game_does_not_name()
	{
		using var game = SyntheticGameClient.Create(resourceData: Held with { Shards = [new(Type: 2, Count: 1)] });

		RefusesMatching(game, @"^SharedModel\.Meta\.Shards\.Shard\.TypeId at 0x[0-9A-F]+ is 2, which isn't a member of the game's SharedModel\.Meta\.Shards\.ShardTypeId\.$");
	}

	[Fact]
	public void Refuses_an_item_the_game_does_not_name()
	{
		using var game = SyntheticGameClient.Create(resourceData: Held with { Items = new Dictionary<int, int> { [1] = 1 } });

		RefusesMatching(game, @"^System\.Collections\.Generic\.Dictionary`2\+Entry\.key at 0x[0-9A-F]+ is 1, which isn't a member of the game's SharedModel\.Meta\.BlackMarket\.BlackMarketItemId\.$");
	}

	/// <summary>The value is named the same on every machine, whatever its culture (code review, #34).</summary>
	[Theory]
	[InlineData(-1, "-1")]
	[InlineData(-0.0, "-0")]
	[InlineData(double.NaN, "NaN")]
	[InlineData(double.PositiveInfinity, "Infinity")]
	public void Refuses_a_resource_amount_that_is_not_one(double amount, string written)
	{
		using var game = SyntheticGameClient.Create(resourceData: Held with { Resources = new Dictionary<int, double> { [2] = amount } });

		RefusesMatching(game, $@"^SharedModel\.Meta\.Account\.Resources\.RawValues\[Silver\] at 0x[0-9A-F]+ is {written}, where raid-bridge expects an amount of 0 or more\.$");
	}

	[Fact]
	public void Refuses_a_negative_shard_count()
	{
		using var game = SyntheticGameClient.Create(resourceData: Held with { Shards = [new(Type: 3, Count: -2)] });

		RefusesMatching(game, @"^SharedModel\.Meta\.Shards\.Shard\.Count at 0x[0-9A-F]+ is -2, where raid-bridge expects a count of 0 or more\.$");
	}

	[Fact]
	public void Refuses_a_negative_item_count()
	{
		using var game = SyntheticGameClient.Create(resourceData: Held with { Items = new Dictionary<int, int> { [5002] = -1 } });

		RefusesMatching(game, @"^SharedModel\.Meta\.BlackMarket\.BmiBox\.Count at 0x[0-9A-F]+ is -1, where raid-bridge expects a count of 0 or more\.$");
	}

	/// <summary>Two entries for one type would have to be added up or one dropped, and either would be a guess.</summary>
	[Fact]
	public void Refuses_a_shard_type_listed_twice()
	{
		using var game = SyntheticGameClient.Create(resourceData: Held with { Shards = [new(Type: 3, Count: 2), new(Type: 3, Count: 5)] });

		RefusesMatching(game, @"^SharedModel\.Meta\.Shards\.Shard\.TypeId at 0x[0-9A-F]+ is Ancient, a Shard type SharedModel\.Meta\.Shards\.UserShardData\.Shards holds twice\.$");
	}

	[Fact]
	public void Refuses_resources_whose_counts_disagree_with_their_slots()
	{
		_game.SetResourceCounts(count: 9, freeCount: 0);

		RefusesMatching(_game, @"^SharedModel\.Meta\.Account\.Resources\.RawValues at 0x[0-9A-F]+ is malformed: its _count is 9 and its _freeCount 0, with 4 slots\.$");
	}

	[Fact]
	public void Refuses_a_shard_list_that_runs_past_its_array()
	{
		_game.SetShardCount(9);

		RefusesMatching(_game, @"^SharedModel\.Meta\.Shards\.UserShardData\.Shards at 0x[0-9A-F]+ is malformed: its _size is 9, but its array holds 3\.$");
	}

	[Fact]
	public void Refuses_a_shard_list_that_is_not_an_object()
	{
		_game.PointField("SharedModel.Meta.Shards.UserShardData", "Shards", SyntheticGameClient.Unmapped);

		RefusesMatching(_game, @"^SharedModel\.Meta\.Shards\.UserShardData\.Shards at 0x[0-9A-F]+ points to 0xDEAD00000000, which isn't an object\. ");
	}

	/// <summary>An error about an Item's box names the dictionary and the Item, not only the class library's Entry.value.</summary>
	[Fact]
	public void Refuses_an_item_whose_box_is_not_an_object_naming_the_item()
	{
		_game.PointItemValue(0, SyntheticGameClient.Unmapped);

		RefusesMatching(_game, @"^SharedModel\.Meta\.BlackMarket\.UserBlackMarketData\.Items\[SkillUpgradeItemEpic\]: System\.Collections\.Generic\.Dictionary`2\+Entry\.value at 0x[0-9A-F]+ points to 0xDEAD00000000, which isn't an object\. ");
	}

	[Fact]
	public void Refuses_a_resource_field_missing_from_the_metadata()
	{
		_game.UndeclareField("SharedModel.Meta.Account.Resources", "RawValues");

		Refuses(_game, "The game's class SharedModel.Meta.Account.Resources has no field RawValues, base classes included.");
	}

	[Fact]
	public void Refuses_an_item_class_missing_from_the_metadata()
	{
		_game.RenameClass("SharedModel.Meta.BlackMarket.BmiBox", "SharedModel.Meta.BlackMarket.ItemBox");

		Refuses(_game, "The game's metadata has no class SharedModel.Meta.BlackMarket.BmiBox.");
	}

	/// <summary>A game update that made the amounts Fixed, or anything else, is refused rather than read as doubles.</summary>
	[Fact]
	public void Refuses_resource_amounts_of_another_type()
	{
		_game.RetypeField("SharedModel.Meta.Account.Resources", "RawValues", "System.Collections.Generic.Dictionary`2<SharedModel.Meta.Account.ResourceTypeId, Plarium.Common.Numerics.Fixed>");

		RefusesMatching(
			_game,
			@"^The game's SharedModel\.Meta\.Account\.Resources\.RawValues is .*<SharedModel\.Meta\.Account\.ResourceTypeId, Plarium\.Common\.Numerics\.Fixed>.*, where raid-bridge expects .*<SharedModel\.Meta\.Account\.ResourceTypeId, System\.Double>.*\.$");
	}

	public void Dispose() => _game.Dispose();

	private static void Refuses(SyntheticGameClient game, string message) =>
		Assert.Equal(message, Assert.ThrowsAny<GameClientException>(() => game.Source().Read()).Message);

	private static void RefusesMatching(SyntheticGameClient game, string pattern) =>
		Assert.Matches(pattern, Assert.ThrowsAny<GameClientException>(() => game.Source().Read()).Message);
}
