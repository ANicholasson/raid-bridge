namespace RaidBridge.Domain.Missions;

/// <summary>
/// One Mission: its prototype id, the same on every Account; its category and state, as the game's own enums name them,
/// such as <c>Dungeons</c> and <c>InProgress</c>; the line the Missions screen shows for it; and its progress, the count
/// the game holds towards the required count. The count is null when the game holds no count,
/// because it works the progress out from the Account each time the screen opens.
/// </summary>
public sealed record Mission(int PrototypeId, string Category, string State, string Name, long? Collected, long Required);
