using System.Text.Json;
using RaidBridge.Cli.Snapshots;
using RaidBridge.Domain.Accounts;
using RaidBridge.Export.Accounts;
using RaidBridge.Game.Memory;
using RaidBridge.Game.Testing;
using RaidBridge.Game.Testing.Arena;
using RaidBridge.Game.Testing.Artifacts;
using RaidBridge.Game.Testing.FactionGuardians;
using RaidBridge.Game.Testing.GreatHall;
using RaidBridge.Game.Testing.Resources;

namespace RaidBridge.Cli.Tests.Snapshots;

/// <summary>
/// <c>raid-bridge snapshot</c> end to end: a synthetic Game client behind the fake memory port in, a Snapshot file
/// and its path out, or one line naming the cause (spec #20).
/// </summary>
public sealed class SnapshotCommandTests : IDisposable
{
	private static readonly DateTimeOffset Now = new(2026, 10, 31, 21, 40, 5, TimeSpan.Zero);

	private readonly SyntheticGameClient _game = SyntheticGameClient.Create(userId: 4_000_000_001);
	private readonly string _workingDirectory = Directory.CreateTempSubdirectory("raid-bridge-cli-").FullName;
	private readonly string _home = Directory.CreateTempSubdirectory("raid-bridge-home-").FullName;
	// The command writes lines as the console does; "\n" keeps the expected output the same on Windows (code review, #23).
	private readonly StringWriter _output = new() { NewLine = "\n" };
	private readonly StringWriter _error = new() { NewLine = "\n" };

	private SaltFile Salts => SaltFile.InConfigDirectory(null, _home);

	[Fact]
	public void Writes_a_snapshot_named_after_its_capture_time_and_prints_its_path()
	{
		var exitCode = Run("snapshot");

		var path = Path.Combine(_workingDirectory, "snapshot-20261031T214005Z.json");
		Assert.Equal((0, $"{path}\n", string.Empty), (exitCode, _output.ToString(), _error.ToString()));
		using var json = JsonDocument.Parse(File.ReadAllBytes(path));
		var root = json.RootElement;
		Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
		Assert.Equal("raid-bridge", root.GetProperty("generator").GetProperty("name").GetString());
		Assert.Matches(@"^0\.1\.0(\+[0-9a-f]{40})?$", root.GetProperty("generator").GetProperty("version").GetString());
		Assert.Equal("2026-10-31T21:40:05Z", root.GetProperty("capturedAt").GetString());
		Assert.Equal(AccountIdHash.Of(new AccountId(4_000_000_001), Salts.LoadOrCreate()).Value, root.GetProperty("account").GetProperty("idHash").GetString());
		Assert.Equal([3, 7, 12], root.GetProperty("champions").EnumerateArray().Select(champion => champion.GetProperty("id").GetInt32()));
		Assert.Equal("Synthetic Knight", root.GetProperty("champions")[0].GetProperty("name").GetString());
		Assert.Equal([104, 105], root.GetProperty("unwornArtifacts").EnumerateArray().Select(artifact => artifact.GetProperty("id").GetInt32()));
	}

	/// <summary>An Unworn Artifact reaches the file whole from either storage, its Faction lock and Glyph included (PR #41).</summary>
	[Theory]
	[InlineData(SyntheticArtifactStorage.AccountList)]
	[InlineData(SyntheticArtifactStorage.ExternalStorage)]
	public void Writes_the_artifacts_no_champion_wears_from_either_storage(SyntheticArtifactStorage storage)
	{
		using var game = SyntheticGameClient.Create(artifactData: SyntheticArtifactData.Default with { Storage = storage });

		Assert.Equal(0, Run(game, "snapshot"));

		using var json = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(_workingDirectory, "snapshot-20261031T214005Z.json")));
		var unworn = json.RootElement.GetProperty("unwornArtifacts");
		Assert.Equal([104, 105], unworn.EnumerateArray().Select(artifact => artifact.GetProperty("id").GetInt32()));
		Assert.Equal("HighElves", unworn[1].GetProperty("factionLock").GetString());
		Assert.Equal(2, unworn[1].GetProperty("substats")[0].GetProperty("glyph").GetDouble());
	}

	/// <summary>The list is written even when every Artifact is worn, so "none" reads apart from "not yet read" (PR #41).</summary>
	[Fact]
	public void Writes_an_empty_unworn_list_when_every_artifact_is_worn()
	{
		var artifacts = SyntheticArtifactData.Default;
		using var game = SyntheticGameClient.Create(artifactData: artifacts with { Artifacts = [.. artifacts.Artifacts.Where(artifact => artifact.Id < 104)] });

		Assert.Equal(0, Run(game, "snapshot"));

		using var json = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(_workingDirectory, "snapshot-20261031T214005Z.json")));
		Assert.Equal(0, json.RootElement.GetProperty("unwornArtifacts").GetArrayLength());
	}

	/// <summary>Resources, Shards and Items reach the file under the game's own names, in the game's order, with each count as held.</summary>
	[Fact]
	public void Writes_the_resources_shards_and_items_in_the_game_order()
	{
		using var game = SyntheticGameClient.Create(resourceData: SyntheticResourceData.Default with { Resources = new Dictionary<int, double> { [2] = 1_250_000, [1] = 129.75 } });

		Assert.Equal(0, Run(game, "snapshot"));

		using var json = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(_workingDirectory, "snapshot-20261031T214005Z.json")));
		var root = json.RootElement;
		Assert.Equal([("Energy", 129.75), ("Silver", 1_250_000)], root.GetProperty("resources").EnumerateObject().Select(resource => (resource.Name, resource.Value.GetDouble())));
		Assert.Equal([("Mystery", 14), ("Ancient", 2)], root.GetProperty("shards").EnumerateObject().Select(shard => (shard.Name, shard.Value.GetInt32())));
		Assert.Equal([("HeroXpBooster100P_1Day", 3), ("SkillUpgradeItemEpic", 1)], root.GetProperty("items").EnumerateObject().Select(item => (item.Name, item.Value.GetInt32())));
	}

	/// <summary>Each object is written even when the Account holds nothing in it, so "none" reads apart from "not yet read".</summary>
	[Fact]
	public void Writes_empty_resources_shards_and_items_when_the_account_holds_none()
	{
		using var game = SyntheticGameClient.Create(resourceData: SyntheticResourceData.None);

		Assert.Equal(0, Run(game, "snapshot"));

		using var json = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(_workingDirectory, "snapshot-20261031T214005Z.json")));
		var root = json.RootElement;
		Assert.Equal("{}", root.GetProperty("resources").GetRawText());
		Assert.Equal("{}", root.GetProperty("shards").GetRawText());
		Assert.Equal("{}", root.GetProperty("items").GetRawText());
	}

	/// <summary>A Shard type the game lists twice fails the run by name, and no file is written.</summary>
	[Fact]
	public void Fails_naming_a_shard_type_listed_twice_and_writes_nothing()
	{
		using var game = SyntheticGameClient.Create(resourceData: SyntheticResourceData.Default with { Shards = [new(Type: 1, Count: 1), new(Type: 1, Count: 2)] });

		var exitCode = Run(game, "snapshot");

		Assert.Equal((1, string.Empty), (exitCode, _output.ToString()));
		Assert.Matches(@"^raid-bridge: SharedModel\.Meta\.Shards\.Shard\.TypeId at 0x[0-9A-F]+ is Mystery, a Shard type SharedModel\.Meta\.Shards\.UserShardData\.Shards holds twice\.\n$", _error.ToString());
		Assert.Empty(Directory.EnumerateFileSystemEntries(_workingDirectory));
	}

	/// <summary>The Great Hall reaches the file under the game's own names, in the game's order, with only the Affinities that have levels.</summary>
	[Fact]
	public void Writes_the_great_hall_in_the_game_order()
	{
		Assert.Equal(0, Run("snapshot"));

		using var json = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(_workingDirectory, "snapshot-20261031T214005Z.json")));
		Assert.Equal(
			[("Magic", "Attack", 3), ("Void", "Health", 10), ("Void", "CriticalDamage", 2)],
			json.RootElement.GetProperty("greatHall").EnumerateObject()
				.SelectMany(affinity => affinity.Value.EnumerateObject().Select(level => (affinity.Name, level.Name, level.Value.GetInt32()))));
	}

	/// <summary>The Great Hall is written even when the player hasn't bought a level, so "none" reads apart from "not yet read".</summary>
	[Fact]
	public void Writes_an_empty_great_hall_when_no_level_is_bought()
	{
		using var game = SyntheticGameClient.Create(greatHallData: SyntheticGreatHallData.None);

		Assert.Equal(0, Run(game, "snapshot"));

		using var json = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(_workingDirectory, "snapshot-20261031T214005Z.json")));
		Assert.Equal("{}", json.RootElement.GetProperty("greatHall").GetRawText());
	}

	/// <summary>A level past the highest the game offers fails the run by name, and no file is written.</summary>
	[Fact]
	public void Fails_naming_a_great_hall_level_outside_the_game_range_and_writes_nothing()
	{
		using var game = SyntheticGameClient.Create(greatHallData: SyntheticGreatHallData.Only(element: 4, new Dictionary<int, int> { [2] = 11 }));

		var exitCode = Run(game, "snapshot");

		Assert.Equal((1, string.Empty), (exitCode, _output.ToString()));
		Assert.Matches(@"^raid-bridge: SharedModel\.Meta\.Village\.UserVillageData\.CapitolBonusLevelByStatByElement\[Void\]\[Attack\] at 0x[0-9A-F]+ is 11, where raid-bridge expects a level from 1 to 10\.\n$", _error.ToString());
		Assert.Empty(Directory.EnumerateFileSystemEntries(_workingDirectory));
	}

	/// <summary>The Faction Guardians reach the file under the game's own names, in the game's order, with only the slots and positions that hold a Champion.</summary>
	[Fact]
	public void Writes_the_faction_guardians_in_the_game_order()
	{
		Assert.Equal(0, Run("snapshot"));

		using var json = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(_workingDirectory, "snapshot-20261031T214005Z.json")));
		Assert.Equal(
			"""{"BannerLords":{"Rare":[[7,12]]},"DarkElves":{"Legendary":[[3]]}}""",
			JsonSerializer.Serialize(json.RootElement.GetProperty("factionGuardians")));
	}

	/// <summary>The Faction Guardians are written even when the player hasn't placed a Champion, so "none" reads apart from "not yet read".</summary>
	[Fact]
	public void Writes_no_faction_guardians_when_no_champion_is_placed()
	{
		using var game = SyntheticGameClient.Create(guardianData: SyntheticGuardianData.None);

		Assert.Equal(0, Run(game, "snapshot"));

		using var json = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(_workingDirectory, "snapshot-20261031T214005Z.json")));
		Assert.Equal("{}", json.RootElement.GetProperty("factionGuardians").GetRawText());
	}

	/// <summary>A Guardian the Account doesn't hold fails the run by name, and no file is written.</summary>
	[Fact]
	public void Fails_naming_a_guardian_the_account_does_not_hold_and_writes_nothing()
	{
		using var game = SyntheticGameClient.Create(guardianData: SyntheticGuardianData.Only(faction: 11, rarity: 5, [new(3, 99)]));

		var exitCode = Run(game, "snapshot");

		Assert.Equal((1, string.Empty), (exitCode, _output.ToString()));
		Assert.Matches(@"^raid-bridge: SharedModel\.Meta\.Academy\.User\.AcademyGuardiansSlot\.SecondHero at 0x[0-9A-F]+ is 99, in slot 1 of DarkElves Legendary, but HeroById holds no Champion 99\.\n$", _error.ToString());
		Assert.Empty(Directory.EnumerateFileSystemEntries(_workingDirectory));
	}

	/// <summary>The Classic and Tag Arena reach the file with the game's league names, leaving out each empty position.</summary>
	[Fact]
	public void Writes_the_classic_and_tag_arena()
	{
		Assert.Equal(0, Run("snapshot"));

		using var json = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(_workingDirectory, "snapshot-20261031T214005Z.json")));
		Assert.Equal(
			("""{"league":"SilverIII","points":1450,"battles":12,"wins":8,"losses":3,"defenceTeam":[12,3,7]}""", """{"league":"BronzeIV","points":820,"battles":6,"wins":2,"losses":4,"defenceTeams":[[3],[7,12],[]]}"""),
			(JsonSerializer.Serialize(json.RootElement.GetProperty("classicArena")), JsonSerializer.Serialize(json.RootElement.GetProperty("tagArena"))));
	}

	/// <summary>Both Arenas are written even when the player has set up neither, so "none" reads apart from "not yet read".</summary>
	[Fact]
	public void Writes_both_arenas_when_neither_is_set_up()
	{
		using var game = SyntheticGameClient.Create(arenaData: SyntheticArenaData.None);

		Assert.Equal(0, Run(game, "snapshot"));

		using var json = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(_workingDirectory, "snapshot-20261031T214005Z.json")));
		Assert.Equal(
			("""{"league":null,"points":1000,"battles":0,"wins":0,"losses":0,"defenceTeam":[]}""", """{"league":null,"points":0,"battles":0,"wins":0,"losses":0,"defenceTeams":[]}"""),
			(JsonSerializer.Serialize(json.RootElement.GetProperty("classicArena")), JsonSerializer.Serialize(json.RootElement.GetProperty("tagArena"))));
	}

	/// <summary>An Arena defender the Account doesn't hold fails the run by name, and no file is written.</summary>
	[Fact]
	public void Fails_naming_an_arena_defender_the_account_does_not_hold_and_writes_nothing()
	{
		using var game = SyntheticGameClient.Create(arenaData: SyntheticArenaData.Default with
		{
			Tag = SyntheticArenaData.Default.Tag with { DefenceTeams = [new(4, [3]), new(4, [7]), new(4, [99])] },
		});

		var exitCode = Run(game, "snapshot");

		Assert.Equal((1, string.Empty), (exitCode, _output.ToString()));
		Assert.Matches(@"^raid-bridge: SharedModel\.Battle\.Core\.Setup\.HeroSlotSetup\.InventoryHeroId at 0x[0-9A-F]+ is 99, in team 3 of the Tag Arena's defence, but HeroById holds no Champion 99\.\n$", _error.ToString());
		Assert.Empty(Directory.EnumerateFileSystemEntries(_workingDirectory));
	}

	[Fact]
	public void Never_writes_the_account_id_itself()
	{
		_ = Run("snapshot");

		Assert.DoesNotContain("4000000001", File.ReadAllText(Path.Combine(_workingDirectory, "snapshot-20261031T214005Z.json")), StringComparison.Ordinal);
	}

	[Fact]
	public void Writes_to_the_chosen_path_relative_to_the_working_directory()
	{
		Directory.CreateDirectory(Path.Combine(_workingDirectory, "uploads"));

		var exitCode = Run("snapshot", "--out", Path.Combine("uploads", "mine.json"));

		var path = Path.Combine(_workingDirectory, "uploads", "mine.json");
		Assert.Equal((0, $"{path}\n"), (exitCode, _output.ToString()));
		Assert.True(File.Exists(path));
	}

	[Fact]
	public void Gives_the_account_the_same_hash_on_every_run()
	{
		_ = Run("snapshot", "--out", "first.json");
		_ = Run("snapshot", "--out", "second.json");

		Assert.Equal(IdHash("first.json"), IdHash("second.json"));
	}

	[Fact]
	public void Never_overwrites_a_file()
	{
		var path = Path.Combine(_workingDirectory, "taken.json");
		File.WriteAllText(path, "the player's own file");

		var exitCode = Run("snapshot", "--out", "taken.json");

		Assert.Equal((1, string.Empty, $"raid-bridge: {path} already exists, and raid-bridge never overwrites a file.\n"), (exitCode, _output.ToString(), _error.ToString()));
		Assert.Equal("the player's own file", File.ReadAllText(path));
	}

	[Fact]
	public void Fails_with_one_line_naming_the_cause_and_writes_nothing()
	{
		_game.UndeclareField("Client.Model.AppModel", "<UserId>k__BackingField");

		var exitCode = Run("snapshot");

		Assert.Equal(
			(1, string.Empty, "raid-bridge: The game's class Client.Model.AppModel has no field <UserId>k__BackingField, base classes included.\n"),
			(exitCode, _output.ToString(), _error.ToString()));
		Assert.Empty(Directory.EnumerateFileSystemEntries(_workingDirectory));
	}

	[Fact]
	public void Fails_with_one_line_when_the_chosen_folder_does_not_exist()
	{
		var missing = Path.Combine(_workingDirectory, "missing");

		var exitCode = Run("snapshot", "--out", Path.Combine("missing", "x.json"));

		Assert.Equal((1, $"raid-bridge: {missing} doesn't exist, so {Path.Combine(missing, "x.json")} can't be written.\n"), (exitCode, _error.ToString()));
	}

	[Fact]
	public void Reads_the_game_files_from_the_given_folder()
	{
		var elsewhere = Directory.CreateDirectory(Path.Combine(_workingDirectory, "elsewhere")).FullName;

		var exitCode = Run("snapshot", "--game-dir", "elsewhere");

		Assert.Equal((1, $"raid-bridge: {elsewhere} isn't the Game client's folder: it has no GameAssembly.dll.\n"), (exitCode, _error.ToString()));
	}

	[Fact]
	public void Makes_no_salt_when_the_game_client_cant_be_read()
	{
		var exitCode = SnapshotCommand.Run(
			["snapshot"], _ => throw new GameMemoryException("The Game client isn't running."), Salts, new Clock(Now), _output, _error, _workingDirectory);

		Assert.Equal((1, "raid-bridge: The Game client isn't running.\n"), (exitCode, _error.ToString()));
		Assert.False(File.Exists(Salts.Path));
	}

	[Fact]
	public void Puts_a_multi_line_cause_on_one_line()
	{
		var exitCode = SnapshotCommand.Run(
			["snapshot"], _ => throw new GameMemoryException("first\nsecond"), Salts, new Clock(Now), _output, _error, _workingDirectory);

		Assert.Equal((1, "raid-bridge: first second\n"), (exitCode, _error.ToString()));
	}

	[Theory]
	[InlineData(new string[0], "no command")]
	[InlineData(new[] { "snap" }, "unknown command snap")]
	[InlineData(new[] { "snapshot", "--output", "x.json" }, "unknown option --output")]
	[InlineData(new[] { "snapshot", "--out" }, "--out needs a value")]
	[InlineData(new[] { "snapshot", "--out", "" }, "--out needs a value")]
	[InlineData(new[] { "snapshot", "--out", "a.json", "--out", "b.json" }, "--out is given twice")]
	[InlineData(new[] { "snapshot", "--out", "--game-dir", "/games/raid" }, "--out needs a value")]
	[InlineData(new[] { "snapshot", "--game-dir", "--out", "x.json" }, "--game-dir needs a value")]
	public void Refuses_a_bad_command_line_with_its_usage(string[] args, string problem)
	{
		var exitCode = Run(args);

		Assert.Equal((2, $"raid-bridge: {problem}; {SnapshotCommand.Usage}\n"), (exitCode, _error.ToString()));
		Assert.Empty(Directory.EnumerateFileSystemEntries(_workingDirectory));
	}

	[Theory]
	[InlineData("--help")]
	[InlineData("-h")]
	public void Prints_its_usage_on_request(string option) =>
		Assert.Equal((0, $"{SnapshotCommand.Usage}\n"), (Run(option), _output.ToString()));

	public void Dispose()
	{
		_game.Dispose();
		Directory.Delete(_workingDirectory, recursive: true);
		Directory.Delete(_home, recursive: true);
	}

	private int Run(params string[] args) => Run(_game, args);

	private int Run(SyntheticGameClient game, params string[] args) =>
		SnapshotCommand.Run(args, game.Source, Salts, new Clock(Now), _output, _error, _workingDirectory);

	private string? IdHash(string file)
	{
		using var json = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(_workingDirectory, file)));
		return json.RootElement.GetProperty("account").GetProperty("idHash").GetString();
	}

	private sealed class Clock(DateTimeOffset now) : TimeProvider
	{
		public override DateTimeOffset GetUtcNow() => now;
	}
}
