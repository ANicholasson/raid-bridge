namespace RaidBridge.Export.Snapshots;

/// <summary>The raid-bridge build that wrote a Snapshot, as its <c>generator</c> says.</summary>
public sealed record Generator(string Name, string Version);
