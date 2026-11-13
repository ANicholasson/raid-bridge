namespace RaidBridge.Domain.Missions;

/// <summary>
/// The Account's Missions, by group: the groups it has finished, and each group it has open with its Missions, as the
/// game's own enum names the groups, such as <c>Group4</c>. The game holds a finished group's Missions only as ids, so
/// a finished group is written by name alone.
/// </summary>
public sealed record MissionGroups(IReadOnlyList<string> Completed, IReadOnlyList<MissionGroup> Open);
