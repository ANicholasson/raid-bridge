using RaidBridge.Domain.Arena;
using RaidBridge.Domain.Artifacts;
using RaidBridge.Domain.Champions;
using RaidBridge.Domain.FactionGuardians;
using RaidBridge.Domain.GreatHall;
using RaidBridge.Domain.Missions;
using RaidBridge.Domain.Relics;
using RaidBridge.Domain.Resources;

namespace RaidBridge.Domain.Accounts;

/// <summary>
/// One player's Account, as the Game client holds it: its id; its level, 1 or more; its experience, 0 or more; its
/// power, exactly as the game holds it, never rounded (https://github.com/ANicholasson/raid-bridge/issues/27); every
/// Champion on it with the Artifacts and Relics each wears, the Unworn Artifacts and the Unworn Relics, the Relic stones in no socket, its Resources, Shards and
/// Items, its Great Hall and its Area Bonuses, its Faction Guardians, its Classic, Tag and Live Arena, and its Missions by group. Each Artifact, Relic and Relic stone on
/// the Account is in exactly one of those places, a stone in a socket being in that socket's Relic. Each Faction
/// Guardian is one of its Champions, in one slot, and so is each Champion in an Arena defence team. The Resources,
/// Shards, Items, the Great Hall, the Area Bonuses and the Faction Guardians come in the order of the numbers the game's enums give
/// them, each name once.
/// </summary>
public sealed record Account(
	AccountId Id,
	int Level,
	int Experience,
	double Power,
	IReadOnlyList<Champion> Champions,
	IReadOnlyList<Artifact> UnwornArtifacts,
	IReadOnlyList<Relic> UnwornRelics,
	IReadOnlyList<RelicStone> UnsocketedRelicStones,
	IReadOnlyList<Resource> Resources,
	IReadOnlyList<Shard> Shards,
	IReadOnlyList<Item> Items,
	IReadOnlyList<GreatHallAffinity> GreatHall,
	IReadOnlyList<AreaBonus> AreaBonuses,
	IReadOnlyList<GuardianFaction> FactionGuardians,
	ClassicArena ClassicArena,
	TagArena TagArena,
	LiveArena? LiveArena,
	MissionGroups Missions);
