using System.Runtime.Versioning;
using RaidBridge.Domain.Accounts;
using RaidBridge.Export.Accounts;

namespace RaidBridge.Export.Tests.Accounts;

public sealed class SaltFileTests : IDisposable
{
	private readonly string _home = Directory.CreateTempSubdirectory("raid-bridge-home-").FullName;

	[Fact]
	public void Lives_in_the_xdg_config_home() =>
		Assert.Equal(Path.Combine(_home, "xdg", "raid-bridge", "salt"), SaltFile.InConfigDirectory(Path.Combine(_home, "xdg"), _home).Path);

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("relative/config")]
	public void Lives_in_dot_config_without_a_usable_xdg_config_home(string? xdgConfigHome) =>
		Assert.Equal(Path.Combine(_home, ".config", "raid-bridge", "salt"), SaltFile.InConfigDirectory(xdgConfigHome, _home).Path);

	[Fact]
	public void Makes_32_random_bytes_on_the_first_run()
	{
		var file = SaltFile.InConfigDirectory(null, _home);

		_ = file.LoadOrCreate();

		Assert.Equal(Salt.Length, File.ReadAllBytes(file.Path).Length);
		Assert.NotEqual(new byte[Salt.Length], File.ReadAllBytes(file.Path));
	}

	[Fact]
	[UnsupportedOSPlatform("windows")]
	public void Makes_the_file_readable_by_the_player_only()
	{
		Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes only.");
		var file = SaltFile.InConfigDirectory(null, _home);

		_ = file.LoadOrCreate();

		Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(file.Path));
	}

	[Theory]
	[InlineData(UnixFileMode.GroupRead, "0640")]
	[InlineData(UnixFileMode.OtherRead, "0604")]
	[InlineData(UnixFileMode.GroupWrite, "0620")]
	[UnsupportedOSPlatform("windows")]
	public void Refuses_and_keeps_a_salt_other_users_may_reach(UnixFileMode extra, string mode)
	{
		Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes only.");
		var file = SaltFile.InConfigDirectory(null, _home);
		_ = file.LoadOrCreate();
		var bytes = File.ReadAllBytes(file.Path);
		File.SetUnixFileMode(file.Path, UnixFileMode.UserRead | UnixFileMode.UserWrite | extra);

		var error = Assert.Throws<InvalidDataException>(new SaltFile(file.Path).LoadOrCreate);

		Assert.Equal(
			$"{file.Path} has mode {mode}, so other users may read or change the salt. Make it the player's only with chmod 600 {file.Path}, then try again.",
			error.Message);
		Assert.Equal(bytes, File.ReadAllBytes(file.Path));
	}

	[Fact]
	[UnsupportedOSPlatform("windows")]
	public void Refuses_a_salt_in_a_folder_other_users_may_change()
	{
		Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes only.");
		var file = SaltFile.InConfigDirectory(null, _home);
		_ = file.LoadOrCreate();
		var directory = Path.GetDirectoryName(file.Path)!;
		File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute);

		var error = Assert.Throws<InvalidDataException>(new SaltFile(file.Path).LoadOrCreate);

		Assert.Equal(
			$"{directory} has mode 0703, so other users may replace the salt in it. Make it the player's only with chmod go-w {directory}, then try again.",
			error.Message);
	}

	[Fact]
	public void Gives_the_same_salt_on_every_later_run()
	{
		var file = SaltFile.InConfigDirectory(null, _home);
		var id = new AccountId(42);

		var first = AccountIdHash.Of(id, file.LoadOrCreate());
		var bytes = File.ReadAllBytes(file.Path);
		var second = AccountIdHash.Of(id, new SaltFile(file.Path).LoadOrCreate());

		Assert.Equal(first, second);
		Assert.Equal(bytes, File.ReadAllBytes(file.Path));
	}

	[Fact]
	public void Refuses_and_keeps_a_file_that_is_not_a_salt()
	{
		var file = SaltFile.InConfigDirectory(null, _home);
		Directory.CreateDirectory(Path.GetDirectoryName(file.Path)!);
		File.WriteAllBytes(file.Path, new byte[7]);
		if (!OperatingSystem.IsWindows())
		{
			File.SetUnixFileMode(Path.GetDirectoryName(file.Path)!, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
			File.SetUnixFileMode(file.Path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
		}

		var error = Assert.Throws<InvalidDataException>(file.LoadOrCreate);

		Assert.Equal(
			$"{file.Path} isn't a raid-bridge salt: it holds 7 bytes, not 32. raid-bridge won't replace it, because every Account id hash depends on it.",
			error.Message);
		Assert.Equal(new byte[7], File.ReadAllBytes(file.Path));
	}

	[Fact]
	public void Refuses_a_relative_home()
	{
		var file = SaltFile.InConfigDirectory(null, "relative-home");

		var error = Assert.Throws<InvalidDataException>(file.LoadOrCreate);

		Assert.Equal(
			$"The salt file's place, {Path.Combine("relative-home", ".config", "raid-bridge", "salt")}, isn't an absolute path: set HOME or XDG_CONFIG_HOME to an absolute folder.",
			error.Message);
	}

	[Fact]
	public void Leaves_only_the_salt_in_its_folder()
	{
		var file = SaltFile.InConfigDirectory(null, _home);

		_ = file.LoadOrCreate();

		Assert.Equal([file.Path], Directory.GetFiles(Path.GetDirectoryName(file.Path)!));
	}

	public void Dispose() => Directory.Delete(_home, recursive: true);
}
