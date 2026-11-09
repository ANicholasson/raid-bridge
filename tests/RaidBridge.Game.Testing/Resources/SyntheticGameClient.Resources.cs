using RaidBridge.Game.Testing.Resources;
using static RaidBridge.Game.Testing.Il2Cpp.Unity6000Layout;

namespace RaidBridge.Game.Testing;

/// <summary>The Resources, Shards and Items of a synthetic Game client, and what a bad read can do to them.</summary>
public sealed partial class SyntheticGameClient
{
	private ulong _resources;
	private ulong _shards;
	private ulong _items;

	/// <summary>The Resources' RawValues has a count and free count that disagree with its slots, as a torn or corrupt read might see them.</summary>
	public void SetResourceCounts(int count, int freeCount) => _runtime.SetDictionaryCounts(_resources, count, freeCount);

	/// <summary>The Shard list claims more Shards than its array holds, as a torn or corrupt read might see it.</summary>
	public void SetShardCount(int count) => _runtime.SetListSize(_shards, count);

	/// <summary>The value in one of the Items' slots points somewhere else, as a corrupt entry or a torn read might.</summary>
	public void PointItemValue(int slot, ulong address) => _heap.Pointer(_runtime.EntryValue(_items, slot), address);

	/// <summary>
	/// UserWrapper's Resources, Shards and Items, each behind a wrapper whose read-only base class declares the data:
	/// AccountData's Resources, whose RawValues holds each amount as a double by its ResourceTypeId; ShardData's list of
	/// Shards; and BlackMarketData's Items, each a box holding its count, by its BlackMarketItemId.
	/// </summary>
	private void LayOutTheResources(ulong userWrapperObject, SyntheticResourceData resourceData)
	{
		var systemObject = _runtime.SystemObject;

		var resources = Declare(
			"SharedModel.Meta.Account",
			"Resources",
			systemObject,
			"System.Object",
			("RawValues", "System.Collections.Generic.Dictionary`2<SharedModel.Meta.Account.ResourceTypeId, System.Double>", GenericInstanceTypeCode, 0x10));
		var userAccount = Declare("SharedModel.Meta.Account", "UserAccount", systemObject, "System.Object", ("Resources", "SharedModel.Meta.Account.Resources", ClassTypeCode, 0x10));
		var accountWrapperReadOnly = Declare(
			"Client.Model.Gameplay.Account",
			"AccountWrapperReadOnly",
			systemObject,
			"System.Object",
			("AccountData", "SharedModel.Meta.Account.UserAccount", ClassTypeCode, 0x10));
		var accountWrapper = Declare("Client.Model.Gameplay.Account", "AccountWrapper", accountWrapperReadOnly, "Client.Model.Gameplay.Account.AccountWrapperReadOnly");

		var shard = Declare(
			"SharedModel.Meta.Shards",
			"Shard",
			systemObject,
			"System.Object",
			("TypeId", "SharedModel.Meta.Shards.ShardTypeId", ValueTypeTypeCode, 0x10),
			("Count", "System.Int32", Int32TypeCode, 0x14));
		var userShardData = Declare(
			"SharedModel.Meta.Shards",
			"UserShardData",
			systemObject,
			"System.Object",
			("Shards", "System.Collections.Generic.List`1<SharedModel.Meta.Shards.Shard>", GenericInstanceTypeCode, 0x10));
		var shardWrapperReadOnly = Declare(
			"Client.Model.Gameplay.Shards",
			"ShardWrapperReadOnly",
			systemObject,
			"System.Object",
			("ShardData", "SharedModel.Meta.Shards.UserShardData", ClassTypeCode, 0x10));
		var shardWrapper = Declare("Client.Model.Gameplay.Shards", "ShardWrapper", shardWrapperReadOnly, "Client.Model.Gameplay.Shards.ShardWrapperReadOnly");

		var itemBox = Declare("SharedModel.Meta.BlackMarket", "BmiBox", systemObject, "System.Object", ("Count", "System.Int32", Int32TypeCode, 0x10));
		var userBlackMarketData = Declare(
			"SharedModel.Meta.BlackMarket",
			"UserBlackMarketData",
			systemObject,
			"System.Object",
			("Items", "System.Collections.Generic.Dictionary`2<SharedModel.Meta.BlackMarket.BlackMarketItemId, SharedModel.Meta.BlackMarket.BmiBox>", GenericInstanceTypeCode, 0x10));
		var blackMarketWrapperReadOnly = Declare(
			"Client.Model.Gameplay.BlackMarket",
			"BlackMarketWrapperReadOnly",
			systemObject,
			"System.Object",
			("BlackMarketData", "SharedModel.Meta.BlackMarket.UserBlackMarketData", ClassTypeCode, 0x10));
		var blackMarketWrapper = Declare("Client.Model.Gameplay.BlackMarket", "BlackMarketWrapper", blackMarketWrapperReadOnly, "Client.Model.Gameplay.BlackMarket.BlackMarketWrapperReadOnly");

		_resources = _runtime.Dictionary(
			_runtime.DictionaryType(ValueTypeTypeCode, DoubleTypeCode),
			[.. resourceData.Resources.Select(resource => ((ulong)resource.Key, BitConverter.DoubleToUInt64Bits(resource.Value)))]);
		var resourcesObject = _runtime.Object(resources, 0x18);
		_heap.Pointer(resourcesObject + 0x10, _resources);
		var userAccountObject = _runtime.Object(userAccount, 0x18);
		_heap.Pointer(userAccountObject + 0x10, resourcesObject);
		var accountWrapperObject = _runtime.Object(accountWrapper, 0x18);
		_heap.Pointer(accountWrapperObject + 0x10, userAccountObject);
		Slot("Client.Model.Guard.UserWrapper.Account", userWrapperObject + 0x30, accountWrapperObject);

		_shards = _runtime.List(_runtime.ListType(shard, "Shard", sizeof(ulong)), [.. resourceData.Shards.Select(synthetic =>
		{
			var shardObject = _runtime.Object(shard, 0x18);
			_heap.Int32(shardObject + 0x10, synthetic.Type);
			_heap.Int32(shardObject + 0x14, synthetic.Count);
			return shardObject;
		})]);
		var userShardDataObject = _runtime.Object(userShardData, 0x18);
		Slot("SharedModel.Meta.Shards.UserShardData.Shards", userShardDataObject + 0x10, _shards);
		var shardWrapperObject = _runtime.Object(shardWrapper, 0x18);
		_heap.Pointer(shardWrapperObject + 0x10, userShardDataObject);
		Slot("Client.Model.Guard.UserWrapper.Shards", userWrapperObject + 0x38, shardWrapperObject);

		_items = _runtime.Dictionary(_runtime.DictionaryType(ValueTypeTypeCode, ClassTypeCode), [.. resourceData.Items.Select(item =>
		{
			var boxObject = _runtime.Object(itemBox, 0x18);
			_heap.Int32(boxObject + 0x10, item.Value);
			return ((ulong)item.Key, boxObject);
		})]);
		var userBlackMarketDataObject = _runtime.Object(userBlackMarketData, 0x18);
		_heap.Pointer(userBlackMarketDataObject + 0x10, _items);
		var blackMarketWrapperObject = _runtime.Object(blackMarketWrapper, 0x18);
		_heap.Pointer(blackMarketWrapperObject + 0x10, userBlackMarketDataObject);
		Slot("Client.Model.Guard.UserWrapper.BlackMarket", userWrapperObject + 0x40, blackMarketWrapperObject);
	}
}
