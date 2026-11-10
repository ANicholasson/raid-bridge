using RaidBridge.Domain.Accounts;
using RaidBridge.Domain.Arena;
using RaidBridge.Domain.Artifacts;
using RaidBridge.Domain.Champions;
using RaidBridge.Domain.FactionGuardians;
using RaidBridge.Domain.GreatHall;
using RaidBridge.Domain.Relics;
using RaidBridge.Domain.Resources;

namespace RaidBridge.Domain.Snapshots;

/// <summary>
/// Everything raid-bridge read from one Account at one moment. The Account id is in it only as its hash, the
/// Champions, the Unworn Artifacts, the Unworn Relics and the Relic stones in no socket are sorted by id, and the
/// Resources, Shards, Items, the Great Hall and the Faction Guardians keep the game's order, so two Snapshots diff
/// cleanly.
/// </summary>
public sealed record Snapshot(
	DateTimeOffset CapturedAt,
	AccountIdHash AccountIdHash,
	IReadOnlyList<Champion> Champions,
	IReadOnlyList<Artifact> UnwornArtifacts,
	IReadOnlyList<Relic> UnwornRelics,
	IReadOnlyList<RelicStone> UnsocketedRelicStones,
	IReadOnlyList<Resource> Resources,
	IReadOnlyList<Shard> Shards,
	IReadOnlyList<Item> Items,
	IReadOnlyList<GreatHallAffinity> GreatHall,
	IReadOnlyList<GuardianFaction> FactionGuardians,
	ClassicArena ClassicArena,
	TagArena TagArena);
