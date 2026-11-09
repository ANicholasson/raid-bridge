namespace RaidBridge.Domain.FactionGuardians;

/// <summary>A Faction's guardian slots of one rarity: the rarity, as the game's own enum names it, such as <c>Legendary</c>, and each slot that holds a Champion, in the game's order.</summary>
public sealed record GuardianRarity(string Rarity, IReadOnlyList<GuardianSlot> Slots);
