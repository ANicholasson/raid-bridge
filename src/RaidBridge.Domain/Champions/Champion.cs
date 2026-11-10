using RaidBridge.Domain.Artifacts;
using RaidBridge.Domain.Relics;

namespace RaidBridge.Domain.Champions;

/// <summary>
/// One Champion on the Account, as the Game client holds it. Rarity, Affinity, Faction, rank, Awakening, blessing
/// and marker are the names the game's own enums give them, such as <c>Legendary</c> or <c>Stars6</c>; Awakening
/// and blessing are null when the Champion has none. <see cref="Artifacts"/> holds the Artifacts it wears, one per
/// slot it fills, in the game's order of slots, and <see cref="Relics"/> the Relics it wears, in the game's order.
/// Ascension and stats are left out (#18).
/// </summary>
public sealed record Champion(
	int Id,
	int TypeId,
	string Name,
	string Rarity,
	string Affinity,
	string Faction,
	string Rank,
	int Level,
	string? Awakening,
	string? Blessing,
	int Empowerment,
	IReadOnlyList<Skill> Skills,
	IReadOnlyList<int> Masteries,
	bool Locked,
	string Marker,
	ChampionLocation Location,
	IReadOnlyList<Artifact> Artifacts,
	IReadOnlyList<Relic> Relics);
