using RaidBridge.Domain.Accounts;

namespace RaidBridge.Export.Snapshots;

/// <summary>The names docs/snapshot.md gives the sections of a Snapshot that may be null.</summary>
public static class SnapshotSections
{
	/// <summary>The section's key in the Snapshot, such as <c>missions</c>, which its Problem names too.</summary>
	public static string Key(OptionalSection section) =>
		section switch
		{
			OptionalSection.AreaBonuses => "areaBonuses",
			OptionalSection.LiveArena => "liveArena",
			OptionalSection.Missions => "missions",
			_ => throw new ArgumentOutOfRangeException(nameof(section), section, "Not a section a Snapshot may write as null."),
		};
}
