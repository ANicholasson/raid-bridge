namespace RaidBridge.Domain.Resources;

/// <summary>The Shards of one type on the Account: the type, as the game's own enum names it, such as <c>Ancient</c>, and how many.</summary>
public sealed record Shard(string Type, int Count);
