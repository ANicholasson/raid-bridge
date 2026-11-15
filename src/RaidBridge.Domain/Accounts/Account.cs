using RaidBridge.Domain.Arena;
using RaidBridge.Domain.Artifacts;
using RaidBridge.Domain.Champions;
using RaidBridge.Domain.FactionGuardians;
using RaidBridge.Domain.GreatHall;
using RaidBridge.Domain.Missions;
using RaidBridge.Domain.Relics;
using RaidBridge.Domain.Resources;
using RaidBridge.Domain.SavedTeams;

namespace RaidBridge.Domain.Accounts;

/// <summary>
/// One player's Account, as the Game client holds it: its id; its level, 1 or more; its experience, 0 or more; its
/// power, exactly as the game holds it, never rounded, and the room it has for its Unworn Artifacts
/// (https://github.com/ANicholasson/raid-bridge/issues/27); every
/// Champion on it with the Artifacts and Relics each wears, the Unworn Artifacts and the Unworn Relics, the Relic stones in no socket, its Resources, Shards and
/// Items, its Great Hall and its Area Bonuses, its Faction Guardians, its Classic, Tag and Live Arena, its Missions by group, and its Saved teams in the order the player sorted them. Each Artifact, Relic and Relic stone on
/// the Account is in exactly one of those places, a stone in a socket being in that socket's Relic. Each Faction
/// Guardian is one of its Champions, in one slot, and so is each Champion in an Arena defence team and a Saved team. The Resources,
/// Shards, Items, the Great Hall, the Area Bonuses and the Faction Guardians come in the order of the numbers the game's enums give
/// them, each name once. An Area Bonuses, Live Arena or Missions part raid-bridge couldn't read is null, with a Problem
/// that says why; nothing else is ever left out (ADR-0006). The Live Arena is also null, with no Problem, when the game
/// holds no Live Arena data for the Account.
/// </summary>
public sealed record Account(
	AccountId Id,
	int Level,
	long Experience,
	double Power,
	ArtifactCapacity ArtifactCapacity,
	IReadOnlyList<Champion> Champions,
	IReadOnlyList<Artifact> UnwornArtifacts,
	IReadOnlyList<Relic> UnwornRelics,
	IReadOnlyList<RelicStone> UnsocketedRelicStones,
	IReadOnlyList<Resource> Resources,
	IReadOnlyList<Shard> Shards,
	IReadOnlyList<Item> Items,
	IReadOnlyList<GreatHallAffinity> GreatHall,
	IReadOnlyList<AreaBonus>? AreaBonuses,
	IReadOnlyList<GuardianFaction> FactionGuardians,
	ClassicArena ClassicArena,
	TagArena TagArena,
	LiveArena? LiveArena,
	MissionGroups? Missions,
	IReadOnlyList<SavedTeam> SavedTeams,
	IReadOnlyList<Problem> Problems);
