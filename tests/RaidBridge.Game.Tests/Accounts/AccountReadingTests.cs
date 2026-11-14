using RaidBridge.Domain.Accounts;
using RaidBridge.Game.Testing;

namespace RaidBridge.Game.Tests.Accounts;

/// <summary>
/// The Account read end to end through the fake memory port: a synthetic Game client in, an Account or a named
/// refusal out (spec #20). Nothing here looks at the reader's internals.
/// </summary>
public sealed class AccountReadingTests : IDisposable
{
	private const string AppModel = "Client.Model.AppModel";
	private const string UserId = "<UserId>k__BackingField";

	private readonly SyntheticGameClient _game = SyntheticGameClient.Create(userId: 4_000_000_001, userIdOnUser: 4_000_000_002);

	[Fact]
	public void Reads_the_account_id_from_the_app_model() =>
		Assert.Equal(new AccountId(4_000_000_001), _game.Source().Read().Id);

	/// <summary>The power is a double the game holds, read as held, not rounded to a whole number (https://github.com/ANicholasson/raid-bridge/issues/27).</summary>
	[Fact]
	public void Reads_the_account_level_experience_and_power() =>
		Assert.Equal(new AccountStanding(54, 31_250, 1_234_567.891), _game.Source().Read().Standing);

	/// <summary>A new Account: level 1, no experience and no power yet.</summary>
	[Fact]
	public void Reads_a_new_account()
	{
		using var game = SyntheticGameClient.Create(accountData: new(Level: 1, Experience: 0, TotalPower: 0));

		Assert.Equal(new AccountStanding(1, 0, 0), game.Source().Read().Standing);
	}

	[Theory]
	[InlineData(0, 0, 0, "Level", "0", "a level of 1 or more")]
	[InlineData(1, -1, 0, "Experience", "-1", "experience of 0 or more")]
	[InlineData(1, 0, -0.5, "TotalPower", "-0.5", "a power of 0 or more")]
	[InlineData(1, 0, -0.0, "TotalPower", "-0", "a power of 0 or more")]
	[InlineData(1, 0, double.NaN, "TotalPower", "NaN", "a power of 0 or more")]
	[InlineData(1, 0, double.PositiveInfinity, "TotalPower", "Infinity", "a power of 0 or more")]
	public void Refuses_a_standing_out_of_range(int level, int experience, double power, string field, string value, string expectation)
	{
		using var game = SyntheticGameClient.Create(accountData: new(level, experience, power));

		var error = Assert.ThrowsAny<GameClientException>(() => game.Source().Read());

		Assert.Matches(
			$@"^SharedModel\.Meta\.Account\.UserAccount\.{field} at 0x[0-9A-F]+ is {System.Text.RegularExpressions.Regex.Escape(value)}, where raid-bridge expects {expectation}\.$",
			error.Message);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-7)]
	public void Falls_back_to_the_user_id_when_the_app_model_has_none(long appModelId)
	{
		using var game = SyntheticGameClient.Create(userId: appModelId, userIdOnUser: 4_000_000_002);

		Assert.Equal(new AccountId(4_000_000_002), game.Source().Read().Id);
	}

	[Fact]
	public void Refuses_when_neither_copy_of_the_id_is_positive()
	{
		using var game = SyntheticGameClient.Create(userId: 0, userIdOnUser: -1);

		var error = Assert.ThrowsAny<GameClientException>(() => game.Source().Read());

		Assert.Matches(
			@"^Neither copy of the Account id is a positive long: Client\.Model\.AppModel\.<UserId>k__BackingField at 0x[0-9A-F]+ is 0, and Client\.Model\.Gameplay\.User\.User\.Id at 0x[0-9A-F]+ is -1\.$",
			error.Message);
	}

	[Fact]
	public void Never_reads_the_social_data()
	{
		using var game = SyntheticGameClient.Create(userId: 0, userIdOnUser: 4_000_000_002);

		_ = game.Source().Read();

		Assert.False(game.SocialDataWasRead);
	}

	[Fact]
	public void Finds_the_game_files_beside_the_loaded_game_assembly()
	{
		_ = _game.Source().Read();

		Assert.Equal(Path.Combine(_game.Directory, "GameAssembly.dll"), _game.MetadataReadFrom);
	}

	[Fact]
	public void Takes_the_game_files_from_the_given_folder_instead()
	{
		var elsewhere = Directory.CreateTempSubdirectory("raid-bridge-elsewhere-").FullName;
		try
		{
			var error = Assert.ThrowsAny<GameClientException>(() => _game.Source(elsewhere).Read());

			Assert.Equal($"{elsewhere} isn't the Game client's folder: it has no GameAssembly.dll.", error.Message);
		}
		finally
		{
			Directory.Delete(elsewhere);
		}
	}

	[Fact]
	public void Refuses_an_unknown_unity_version()
	{
		using var game = SyntheticGameClient.Create(unityVersion: "6000.3.19f1");

		Refuses(game, "raid-bridge has no IL2CPP runtime layout for Unity 6000.3.19f1; it knows only Unity 6000.3.18f1.");
	}

	[Fact]
	public void Refuses_a_game_assembly_replaced_after_the_game_client_started_whatever_its_time()
	{
		_game.ReplaceGameAssemblyAfterStart();

		var error = Assert.ThrowsAny<GameClientException>(() => _game.Source().Read());

		Assert.Equal(AnotherBuild(_game.Directory), error.Message);
	}

	[Fact]
	public void Refuses_a_game_assembly_replaced_while_the_files_are_parsed()
	{
		_game.WhileParsing = _game.ReplaceGameAssemblyAfterStart;

		var error = Assert.ThrowsAny<GameClientException>(() => _game.Source().Read());

		Assert.Equal(AnotherBuild(_game.Directory), error.Message);
	}

	[Fact]
	public void Reads_a_game_assembly_touched_after_the_game_client_started()
	{
		_game.TouchAfterStart("GameAssembly.dll");

		Assert.Equal(new AccountId(4_000_000_001), _game.Source().Read().Id);
	}

	[Theory]
	[InlineData("Raid_Data/il2cpp_data/Metadata/global-metadata.dat")]
	[InlineData("Raid_Data/data.unity3d")]
	[InlineData("UnityPlayer.dll")]
	public void Refuses_another_game_file_newer_than_the_running_game_client(string file)
	{
		_game.TouchAfterStart(file);

		var error = Assert.ThrowsAny<GameClientException>(() => _game.Source().Read());

		Assert.Equal(
			$"{Path.Combine(_game.Directory, file.Replace('/', Path.DirectorySeparatorChar))} changed at 2026-10-31 21:00:00Z, after the Game client started at 2026-10-31 20:00:00Z: the game updated under the running Game client. Restart the Game client and try again.",
			error.Message);
	}

	[Fact]
	public void Reads_a_given_folder_of_the_running_build()
	{
		var copy = _game.AnotherInstall(sameBuild: true);

		Assert.Equal(new AccountId(4_000_000_001), _game.Source(copy).Read().Id);
		Assert.Equal(Path.Combine(copy, "GameAssembly.dll"), _game.MetadataReadFrom);
	}

	[Fact]
	public void Refuses_a_given_folder_of_another_build()
	{
		var otherBuild = _game.AnotherInstall(sameBuild: false);

		var error = Assert.ThrowsAny<GameClientException>(() => _game.Source(otherBuild).Read());

		Assert.Equal(AnotherBuild(otherBuild), error.Message);
		Assert.Null(_game.MetadataReadFrom);
	}

	[Theory]
	[InlineData(0x60u)]
	[InlineData(0x10_0000u)]
	public void Refuses_a_game_assembly_whose_headers_have_no_size_it_can_read(uint size)
	{
		_game.SetSizeOfHeaders(size);

		var error = Assert.ThrowsAny<GameClientException>(() => _game.Source().Read());

		Assert.Equal($"{Path.Combine(_game.Directory, "GameAssembly.dll")} isn't a PE image: Its SizeOfHeaders is {size}.", error.Message);
	}

	[Fact]
	public void Refuses_a_game_assembly_that_is_not_a_pe_image()
	{
		File.WriteAllBytes(Path.Combine(_game.Directory, "GameAssembly.dll"), "synthetic"u8.ToArray());

		var error = Assert.ThrowsAny<GameClientException>(() => _game.Source().Read());

		Assert.StartsWith($"{Path.Combine(_game.Directory, "GameAssembly.dll")} isn't a PE image: ", error.Message);
	}

	[Fact]
	public void Reads_past_a_field_it_does_not_use()
	{
		_game.BreakRuntimeFieldType("Client.Model.Guard.UserWrapper", "Social");

		Assert.Equal(new AccountId(4_000_000_001), _game.Source().Read().Id);
	}

	[Fact]
	public void Refuses_a_missing_class()
	{
		_game.RenameClass(AppModel, "Client.Model.ApplicationModel");

		Refuses(_game, "The game's metadata has no class Client.Model.AppModel.");
	}

	[Fact]
	public void Refuses_a_root_whose_generic_instance_is_missing_from_the_binary()
	{
		_game.RenameClass("Client.App.SingleInstance`1", "Client.App.Singleton`1");

		Refuses(_game, "The game's metadata has no class Client.App.SingleInstance`1.");
	}

	[Fact]
	public void Refuses_a_field_missing_from_the_metadata()
	{
		_game.UndeclareField(AppModel, UserId);

		Refuses(_game, "The game's class Client.Model.AppModel has no field <UserId>k__BackingField, base classes included.");
	}

	[Fact]
	public void Refuses_a_field_missing_from_the_runtime_class()
	{
		_game.RenameRuntimeField(AppModel, UserId, "<AccountId>k__BackingField");

		RefusesMatching(_game, @"^The runtime class Client\.Model\.AppModel at 0x[0-9A-F]+ has no field <UserId>k__BackingField\.$");
	}

	[Fact]
	public void Refuses_a_field_the_metadata_declares_with_another_type()
	{
		_game.RetypeField(AppModel, UserId, "System.Int32");

		Refuses(_game, "The game's Client.Model.AppModel.<UserId>k__BackingField is a System.Int32, where raid-bridge expects a System.Int64.");
	}

	[Fact]
	public void Refuses_a_runtime_field_whose_type_code_does_not_fit_its_declaration()
	{
		_game.SetRuntimeTypeCode(AppModel, UserId, 0x08);

		RefusesMatching(_game, @"^The runtime field Client\.Model\.AppModel\.<UserId>k__BackingField at 0x[0-9A-F]+ has type code 0x08, which doesn't fit the game's declared System\.Int64\.$");
	}

	[Fact]
	public void Refuses_a_root_that_points_at_nothing_mapped()
	{
		_game.PointField("Client.App.SingleInstance`1", "_instance", SyntheticGameClient.Unmapped);

		RefusesMatching(_game, @"^Client\.App\.SingleInstance`1<Client\.Model\.AppModel>\._instance at 0x[0-9A-F]+ points to 0xDEAD00000000, which isn't an object\. Can't read its class pointer at 0xDEAD00000000: Nothing is mapped at 0xDEAD00000000 for 8 bytes\.$");
	}

	[Fact]
	public void Refuses_a_null_root()
	{
		_game.PointField("Client.App.SingleInstance`1", "_instance", 0);

		RefusesMatching(_game, @"^Client\.App\.SingleInstance`1<Client\.Model\.AppModel>\._instance at 0x[0-9A-F]+ is null, where raid-bridge expects a Client\.Model\.AppModel\.$");
	}

	[Fact]
	public void Refuses_a_fallback_that_points_at_nothing_mapped()
	{
		using var game = SyntheticGameClient.Create(userId: 0);
		game.PointField(AppModel, "_userWrapper", SyntheticGameClient.Unmapped);

		RefusesMatching(game, @"^Client\.Model\.AppModel\._userWrapper at 0x[0-9A-F]+ points to 0xDEAD00000000, which isn't an object\. ");
	}

	[Fact]
	public void Refuses_a_field_that_points_at_an_object_of_another_class()
	{
		using var game = SyntheticGameClient.Create(userId: 0);
		game.PointField(AppModel, "_userWrapper", game.Target("Client.Model.Guard.UserWrapper", "User"));

		RefusesMatching(game, @"^Client\.Model\.AppModel\._userWrapper at 0x[0-9A-F]+ points to a Client\.Model\.Gameplay\.User\.User at 0x[0-9A-F]+, not a Client\.Model\.Guard\.UserWrapper\.$");
	}

	/// <summary>An object whose base classes can't be read is named with the field it was reached through (#38).</summary>
	[Fact]
	public void Refuses_an_object_whose_base_classes_cannot_be_read()
	{
		_game.SetBaseClass("Client.Model.Guard.UserWrapper", SyntheticGameClient.Unmapped);

		RefusesMatching(_game, @"^Client\.Model\.AppModel\._userWrapper at 0x[0-9A-F]+ points to a Client\.Model\.Guard\.UserWrapper at 0x[0-9A-F]+, whose base classes can't be read\. Can't read the name of the runtime class 0xDEAD00000000 at 0xDEAD[0-9A-F]+: Nothing is mapped at 0xDEAD[0-9A-F]+ for 8 bytes\.$");
	}

	[Fact]
	public void Refuses_a_field_that_points_at_something_that_is_not_an_object()
	{
		using var game = SyntheticGameClient.Create(userId: 0);
		game.PointField(AppModel, "_userWrapper", game.Memory.GameAssembly.ImageBase);

		RefusesMatching(game, @"^Client\.Model\.AppModel\._userWrapper at 0x[0-9A-F]+ points to 0x180000000, which isn't an object\. Can't read the name of the runtime class 0x5A4D at 0x5A5D: Nothing is mapped at 0x5A5D for 8 bytes\.$");
	}

	[Fact]
	public void Refuses_a_root_class_the_game_client_has_not_set_up()
	{
		_game.SetRootClass(0);

		Refuses(_game, "The Game client hasn't set up Client.App.SingleInstance`1<Client.Model.AppModel> yet: its generic class record at 0x180001000 holds no runtime class.");
	}

	/// <summary>A wrong layout table reads the wrong slots, and the cross-check between record and class catches it.</summary>
	[Fact]
	public void Refuses_a_generic_class_record_whose_class_does_not_lead_back_to_it()
	{
		_game.SetRootClass(_game.RuntimeClass(AppModel));

		RefusesMatching(_game, @"^Client\.App\.SingleInstance`1<Client\.Model\.AppModel>'s generic class record at 0x180001000 leads to a runtime class Client\.Model\.AppModel at 0x[0-9A-F]+ whose record is 0x0: the IL2CPP layout for Unity 6000\.3\.18f1 doesn't fit this Game client\.$");
	}

	public void Dispose() => _game.Dispose();

	private static void Refuses(SyntheticGameClient game, string message) =>
		Assert.Equal(message, Assert.ThrowsAny<GameClientException>(() => game.Source().Read()).Message);

	private static void RefusesMatching(SyntheticGameClient game, string pattern) =>
		Assert.Matches(pattern, Assert.ThrowsAny<GameClientException>(() => game.Source().Read()).Message);

	/// <summary>The refusal of a folder whose GameAssembly.dll differs from the loaded one in its build stamp, the file header's TimeDateStamp at 0x48.</summary>
	private string AnotherBuild(string directory) =>
		$"{Path.Combine(directory, "GameAssembly.dll")} isn't the build the Game client is running: its headers differ from those of the image it loaded from {Path.Combine(_game.Directory, "GameAssembly.dll")}, from byte 0x48. If the game updated, restart the Game client; if a game folder was given, give the running game's. Then try again.";
}
