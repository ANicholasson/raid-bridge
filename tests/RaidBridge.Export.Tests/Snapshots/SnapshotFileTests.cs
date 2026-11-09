using System.Text;
using RaidBridge.Domain.Accounts;
using RaidBridge.Domain.Snapshots;
using RaidBridge.Export.Snapshots;

namespace RaidBridge.Export.Tests.Snapshots;

public sealed class SnapshotFileTests : IDisposable
{
	private static readonly Snapshot Snapshot = new(
		new DateTimeOffset(2026, 10, 31, 21, 40, 5, TimeSpan.Zero),
		AccountIdHash.Of(new AccountId(1), new Salt(new byte[Salt.Length])),
		[],
		[],
		[],
		[],
		[],
		[],
		[],
		new(null, 0, 0, 0, 0, new([])),
		new(null, 0, 0, 0, 0, []));

	private readonly string _directory = Directory.CreateTempSubdirectory("raid-bridge-export-").FullName;

	[Fact]
	public void Names_a_snapshot_after_its_capture_time_in_utc() =>
		Assert.Equal("snapshot-20261031T214005Z.json", SnapshotFile.DefaultName(new DateTimeOffset(2026, 10, 31, 23, 40, 5, TimeSpan.FromHours(2))));

	[Fact]
	public void Writes_the_snapshot_and_returns_its_full_path()
	{
		var path = Path.Combine(_directory, "first.json");

		var written = SnapshotFile.Write(Snapshot, new Generator("raid-bridge", "0.1.0"), path);

		Assert.Equal(path, written);
		Assert.Equal(SnapshotJson.Write(Snapshot, new Generator("raid-bridge", "0.1.0")), File.ReadAllBytes(path));
	}

	[Fact]
	public void Never_overwrites_a_file()
	{
		var path = Path.Combine(_directory, "taken.json");
		File.WriteAllText(path, "the player's own file");

		var error = Assert.Throws<IOException>(() => SnapshotFile.Write(Snapshot, new Generator("raid-bridge", "0.1.0"), path));

		Assert.Equal($"{path} already exists, and raid-bridge never overwrites a file.", error.Message);
		Assert.Equal("the player's own file", File.ReadAllText(path, Encoding.UTF8));
	}

	[Fact]
	public void Leaves_nothing_behind_when_it_does_not_write()
	{
		var path = Path.Combine(_directory, "taken.json");
		File.WriteAllText(path, "the player's own file");

		_ = Assert.Throws<IOException>(() => SnapshotFile.Write(Snapshot, new Generator("raid-bridge", "0.1.0"), path));

		Assert.Equal([path], Directory.GetFiles(_directory));
	}

	[Fact]
	public void Never_overwrites_a_folder()
	{
		var path = Directory.CreateDirectory(Path.Combine(_directory, "folder.json")).FullName;

		var error = Assert.Throws<IOException>(() => SnapshotFile.Write(Snapshot, new Generator("raid-bridge", "0.1.0"), path));

		Assert.Equal($"{path} already exists, and raid-bridge never overwrites a file.", error.Message);
	}

	public void Dispose() => Directory.Delete(_directory, recursive: true);
}
