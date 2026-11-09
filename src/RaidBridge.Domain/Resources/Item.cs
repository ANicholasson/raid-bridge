namespace RaidBridge.Domain.Resources;

/// <summary>An Item on the Account, such as a potion or a tome: the name the game's own enum gives it, and how many the Account holds.</summary>
public sealed record Item(string Name, int Count);
