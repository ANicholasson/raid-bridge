namespace RaidBridge.Domain.Relics;

/// <summary>
/// A Relic on the Account, worn or not, as the Game client holds it. The rarity and the Relic rank are the names the
/// game's own enums give them, such as <c>Legendary</c> or <c>Rank3</c>. <see cref="Bonuses"/> are the stats its type
/// raises that its level has opened, worked out from the game's static tables, in the type's order. <see cref="Skills"/>
/// are its passive effects: its type's own, then those its wearer's Faction adds. <see cref="Sockets"/> are in the
/// game's order, each with the Relic stone in it or none.
/// </summary>
public sealed record Relic(
	int Id,
	int TypeId,
	string Name,
	string Rarity,
	string Rank,
	int Level,
	IReadOnlyList<RelicBonus> Bonuses,
	IReadOnlyList<RelicSkill> Skills,
	IReadOnlyList<RelicSocket> Sockets);
