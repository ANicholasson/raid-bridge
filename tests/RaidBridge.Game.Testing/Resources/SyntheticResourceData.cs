namespace RaidBridge.Game.Testing.Resources;

/// <summary>
/// The Resources, Shards and Items a synthetic Game client holds, in the game's own terms: each Resource's amount by
/// its ResourceTypeId, each Shard's type and count in the order the game lists them, and each Item's count by its
/// BlackMarketItemId. Keys hold the game's numbers, such as 2 for Silver, and are laid out in the order given here.
/// </summary>
public sealed record SyntheticResourceData(
	IReadOnlyDictionary<int, double> Resources,
	IReadOnlyList<SyntheticShard> Shards,
	IReadOnlyDictionary<int, int> Items)
{
	/// <summary>Gems, silver and energy; Ancient and Mystery Shards; an Epic tome and XP boosters: each out of the game's order, so only the reader can put them in it.</summary>
	public static SyntheticResourceData Default { get; } = new(
		Resources: new Dictionary<int, double> { [4] = 42, [2] = 1_250_000, [1] = 130 },
		Shards: [new(Type: 3, Count: 2), new(Type: 1, Count: 14)],
		Items: new Dictionary<int, int> { [5002] = 1, [3001] = 3 });

	/// <summary>An Account that holds no Resource, no Shard and no Item.</summary>
	public static SyntheticResourceData None { get; } = new(new Dictionary<int, double>(), [], new Dictionary<int, int>());
}

/// <summary>A Shard, as SharedModel.Meta.Shards.Shard holds it: its ShardTypeId and its count.</summary>
public sealed record SyntheticShard(int Type, int Count);
