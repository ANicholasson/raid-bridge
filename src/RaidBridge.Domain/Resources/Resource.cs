namespace RaidBridge.Domain.Resources;

/// <summary>
/// A Resource on the Account, such as silver or energy: the name the game's own enum gives it, such as <c>Silver</c>,
/// and the amount the Account holds, exactly as the game holds it.
/// </summary>
public sealed record Resource(string Name, double Amount);
