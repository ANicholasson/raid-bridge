namespace RaidBridge.Domain.FactionGuardians;

/// <summary>One guardian slot: the ids of the Champions in it, the first position's before the second's, each a Champion on the Account.</summary>
public sealed record GuardianSlot(IReadOnlyList<int> ChampionIds);
