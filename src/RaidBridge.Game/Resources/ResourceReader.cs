using RaidBridge.Domain.Resources;
using RaidBridge.Game.Il2Cpp;
using RaidBridge.Game.Metadata;

namespace RaidBridge.Game.Resources;

/// <summary>
/// Reads the Account's Resources, Shards and Items from the game's model (spec #32, #34), each under the name the
/// game's own enum gives it and in the enum's order: the Resources in
/// <c>_userWrapper.Account.AccountData.Resources.RawValues</c>, by <c>ResourceTypeId</c>; the Shards in
/// <c>_userWrapper.Shards.ShardData.Shards</c>, by <c>ShardTypeId</c>; and the Items in
/// <c>_userWrapper.BlackMarket.BlackMarketData.Items</c>, by <c>BlackMarketItemId</c>. Raid Toolkit's
/// ResourcesExtension named the routes, which the installed game still declares at metadata v39.
/// </summary>
internal static class ResourceReader
{
	private const string ResourceType = "SharedModel.Meta.Account.ResourceTypeId";
	private const string Shard = "SharedModel.Meta.Shards.Shard";
	private const string ShardType = "SharedModel.Meta.Shards.ShardTypeId";
	private const string UserShardData = "SharedModel.Meta.Shards.UserShardData";
	private const string ItemId = "SharedModel.Meta.BlackMarket.BlackMarketItemId";
	private const string ItemBox = "SharedModel.Meta.BlackMarket.BmiBox";

	public static AccountResources Read(ManagedObject userWrapper) => new(Resources(userWrapper), Shards(userWrapper), Items(userWrapper));

	/// <summary>
	/// The game holds each amount as a double, so it is written as held: never rounded, but never negative, infinite or
	/// not a number. Negative zero is refused too, or it would be written as -0 (code review, #34).
	/// </summary>
	private static IReadOnlyList<Resource> Resources(ManagedObject userWrapper) =>
		[.. userWrapper
			.Object("Account", "Client.Model.Gameplay.Account.AccountWrapper")
			.Object("AccountData", "SharedModel.Meta.Account.UserAccount")
			.Object("Resources", "SharedModel.Meta.Account.Resources")
			.Dictionary("RawValues", $"System.Collections.Generic.Dictionary`2<{ResourceType}, System.Double>")
			.ByEnumKey(ResourceType)
			.OrderBy(resource => resource.Key.Value)
			.Select(resource => new Resource(
				resource.Key.Name,
				resource.Value.Double().Where(amount => double.IsFinite(amount) && !double.IsNegative(amount), "an amount of 0 or more")))];

	/// <summary>The game keeps the Shards as a list of types and counts, so a type it lists twice is refused rather than added up or dropped.</summary>
	private static IReadOnlyList<Shard> Shards(ManagedObject userWrapper)
	{
		var counts = new Dictionary<GameEnumMember, int>();
		foreach (var shard in userWrapper
			.Object("Shards", "Client.Model.Gameplay.Shards.ShardWrapper")
			.Object("ShardData", UserShardData)
			.List("Shards", $"System.Collections.Generic.List`1<{Shard}>")
			.Objects(Shard))
		{
			var type = shard.EnumMember("TypeId", ShardType);
			if (!counts.TryAdd(type.Value, shard.Int32("Count").Where(count => count >= 0, "a count of 0 or more")))
			{
				throw new GameDataException($"{type.Field} at 0x{type.Address:X} is {type.Value.Name}, a Shard type {UserShardData}.Shards holds twice.");
			}
		}

		return [.. counts.OrderBy(shard => shard.Key.Value).Select(shard => new Shard(shard.Key.Name, shard.Value))];
	}

	private static IReadOnlyList<Item> Items(ManagedObject userWrapper) =>
		[.. userWrapper
			.Object("BlackMarket", "Client.Model.Gameplay.BlackMarket.BlackMarketWrapper")
			.Object("BlackMarketData", "SharedModel.Meta.BlackMarket.UserBlackMarketData")
			.Dictionary("Items", $"System.Collections.Generic.Dictionary`2<{ItemId}, {ItemBox}>")
			.ByEnumKey(ItemId)
			.OrderBy(item => item.Key.Value)
			.Select(item => new Item(
				item.Key.Name,
				item.Value.Object(ItemBox).Int32("Count").Where(count => count >= 0, "a count of 0 or more")))];
}

/// <summary>The Account's Resources, Shards and Items, each in the order of the game's enum.</summary>
internal sealed record AccountResources(IReadOnlyList<Resource> Resources, IReadOnlyList<Shard> Shards, IReadOnlyList<Item> Items);
