using RaidBridge.Domain.Arena;
using RaidBridge.Domain.Artifacts;
using RaidBridge.Domain.Champions;
using RaidBridge.Domain.FactionGuardians;
using RaidBridge.Domain.GreatHall;
using RaidBridge.Domain.Resources;

namespace RaidBridge.Domain.Accounts;

/// <summary>
/// One player's Account, as the Game client holds it: its id, every Champion on it with the Artifacts each wears, the
/// Artifacts no Champion wears, its Resources, Shards and Items, its Great Hall, its Faction Guardians, and its Classic
/// and Tag Arena. Each Artifact on the Account is in exactly one of those places, each Faction Guardian is one of its
/// Champions, in one slot, and so is each Champion in an Arena defence team. The Resources, Shards, Items, the Great
/// Hall and the Faction Guardians come in the order of the numbers the game's enums give them, each name once.
/// </summary>
public sealed record Account(
	AccountId Id,
	IReadOnlyList<Champion> Champions,
	IReadOnlyList<Artifact> UnwornArtifacts,
	IReadOnlyList<Resource> Resources,
	IReadOnlyList<Shard> Shards,
	IReadOnlyList<Item> Items,
	IReadOnlyList<GreatHallAffinity> GreatHall,
	IReadOnlyList<GuardianFaction> FactionGuardians,
	ClassicArena ClassicArena,
	TagArena TagArena);
