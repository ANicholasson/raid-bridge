namespace RaidBridge.Domain.FactionGuardians;

/// <summary>
/// The Faction Guardians of one Faction: the Faction, as the game's own enum names it, such as <c>DarkElves</c>, and
/// its guardian slots by rarity, in the order the game's enum gives the rarities. Only the Champions: the bonus they
/// give is the game's static data, not the Account's.
/// </summary>
public sealed record GuardianFaction(string Faction, IReadOnlyList<GuardianRarity> Rarities);
