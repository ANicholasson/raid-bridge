namespace RaidBridge.Domain.Artifacts;

/// <summary>
/// The room the Account has for its Unworn Artifacts: the most it can hold, 1 or more, which the game sets the same
/// for every Account, and how many it holds. Accessories, the Artifacts for the Ring, Cloak and Banner slots, are
/// counted against the limit apart from the other Artifacts, each up to the whole limit. Artifacts a Champion wears
/// take no room (https://github.com/ANicholasson/raid-bridge/issues/27).
/// </summary>
public sealed record ArtifactCapacity(int Limit, int Artifacts, int Accessories);
