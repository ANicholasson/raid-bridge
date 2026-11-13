namespace RaidBridge.Domain.Missions;

/// <summary>
/// One open group of Missions: the group and the section of the Missions screen it is in, as the game's own enums name
/// them, such as <c>Group4</c> and <c>ArbiterSection</c>, and its Missions, in the game's order.
/// </summary>
public sealed record MissionGroup(string Group, string Section, IReadOnlyList<Mission> Missions);
