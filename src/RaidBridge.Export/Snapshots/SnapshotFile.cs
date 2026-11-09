using System.Globalization;
using RaidBridge.Domain.Snapshots;

namespace RaidBridge.Export.Snapshots;

/// <summary>Writes a Snapshot to a new file. An existing file is never overwritten, so the player keeps every Snapshot.</summary>
public static class SnapshotFile
{
	/// <summary>The name a Snapshot gets unless the player chooses a path: its capture time in UTC, such as <c>snapshot-20261031T214000Z.json</c>.</summary>
	public static string DefaultName(DateTimeOffset capturedAt) =>
		$"snapshot-{capturedAt.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture)}.json";

	/// <summary>Writes the Snapshot to <paramref name="path"/> and returns the full path it wrote.</summary>
	public static string Write(Snapshot snapshot, Generator generator, string path)
	{
		var fullPath = Path.GetFullPath(path);
		try
		{
			NewFile.Write(fullPath, SnapshotJson.Write(snapshot, generator), ownerOnly: false);
		}
		catch (NewFile.AlreadyExistsException exception)
		{
			throw new IOException($"{fullPath} already exists, and raid-bridge never overwrites a file.", exception);
		}

		return fullPath;
	}
}
