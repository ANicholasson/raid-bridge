namespace RaidBridge.Domain.Relics;

/// <summary>One of a Relic's sockets: its shape, such as <c>Circle</c>, and the Relic stone in it, or null when it is empty.</summary>
public sealed record RelicSocket(string Shape, RelicStone? Stone);
