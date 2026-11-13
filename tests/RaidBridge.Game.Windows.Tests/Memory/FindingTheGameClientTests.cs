using RaidBridge.Game.Memory;
using RaidBridge.Game.Windows.Memory;
using static RaidBridge.Game.Windows.Tests.Memory.FakeWin32Processes;

namespace RaidBridge.Game.Windows.Tests.Memory;

/// <summary>Opening the Windows adapter on made-up processes: finding Raid.exe and GameAssembly.dll, and every refusal on the way.</summary>
public sealed class FindingTheGameClientTests
{
	private const int ErrorAccessDenied = 5;
	private const int ErrorInvalidParameter = 87;
	private const int ErrorPartialCopy = 299;

	private readonly FakeWin32Processes _processes = new();

	[Fact]
	public void Finds_the_game_client_by_its_image_name_and_game_assembly_among_its_modules()
	{
		_processes.AddProcess(100, "PlariumPlay");
		_processes.AddGameClient();

		var memory = WindowsGameMemory.Open(_processes);

		Assert.Equal(new GameModule(ImageBase, GameAssemblyPath), memory.GameAssembly);
		Assert.Equal([GameId], _processes.Opened.Select(process => process.Id));
	}

	[Fact]
	public void Gives_the_time_the_game_client_started() =>
		Assert.Equal(StartedAt, OpenGameClient().StartedAt);

	[Fact]
	public void Reads_nothing_from_the_game_client_to_open_it()
	{
		var process = _processes.AddGameClient();

		_ = WindowsGameMemory.Open(_processes);

		Assert.Equal(0, process.Reads);
	}

	[Fact]
	public void Matches_game_assembly_whatever_the_case_of_its_file_name()
	{
		_processes.AddProcess(GameId, "Raid", new LoadedModule(ImageBase, @"C:\Games\Raid\gameassembly.DLL"));

		Assert.Equal(new GameModule(ImageBase, @"C:\Games\Raid\gameassembly.DLL"), WindowsGameMemory.Open(_processes).GameAssembly);
	}

	[Fact]
	public void Says_the_game_client_is_not_running_when_no_process_is_named_raid()
	{
		_processes.AddProcess(100, "PlariumPlay");

		Assert.Equal("The Game client isn't running: no process is named Raid.exe. Start RAID from Plarium Play, then try again.", Refusal());
	}

	[Fact]
	public void Refuses_to_guess_between_several_processes_named_raid()
	{
		_processes.AddGameClient(4242);
		_processes.AddGameClient(4343);

		Assert.Equal("2 processes are named Raid.exe (4242, 4343), so raid-bridge can't tell which is the Game client. Close all but one, then try again.", Refusal());
		Assert.Empty(_processes.Opened);
	}

	[Fact]
	public void Names_the_users_rights_when_windows_refuses_to_open_the_game_client()
	{
		_processes.AddGameClient();
		_processes.RefuseToOpen(GameId, ErrorAccessDenied);

		Assert.Equal(
			$"Windows doesn't let raid-bridge read the Game client (pid {GameId}). raid-bridge asks only to read it, with your own user's rights: run raid-bridge as the user who plays RAID, and start RAID as that user, not as administrator.",
			Refusal());
	}

	[Fact]
	public void Says_the_game_client_exited_while_raid_bridge_looked_for_it()
	{
		_processes.AddGameClient();
		_processes.RefuseToOpen(GameId, ErrorInvalidParameter);

		Assert.Equal($"The Game client (pid {GameId}) exited while raid-bridge looked for it.", Refusal());
	}

	[Fact]
	public void Says_the_game_client_is_still_starting_when_its_modules_cant_be_listed_yet()
	{
		_processes.AddGameClient().ModulesError = ErrorPartialCopy;

		Assert.Equal(
			$"The Game client (pid {GameId}) is still starting: Windows can't list its modules yet. Wait until RAID shows its main screen, then try again.",
			Refusal());
	}

	[Fact]
	public void Names_windows_error_when_the_modules_cant_be_listed()
	{
		_processes.AddGameClient().ModulesError = ErrorAccessDenied;

		Assert.StartsWith($"Listing the modules of the Game client (pid {GameId}) failed with Windows error 5: ", Refusal(), StringComparison.Ordinal);
	}

	[Fact]
	public void Says_game_assembly_is_not_loaded_yet()
	{
		_processes.AddProcess(GameId, "Raid", new LoadedModule(0x7FF6_0000_0000, @"C:\Games\Raid\Raid.exe"));

		Assert.Equal($"The Game client (pid {GameId}) hasn't loaded GameAssembly.dll yet. Wait until RAID shows its main screen, then try again.", Refusal());
	}

	[Fact]
	public void Refuses_to_guess_between_two_game_assembly_modules()
	{
		_processes.AddProcess(GameId, "Raid", new LoadedModule(0x1000_0000, GameAssemblyPath), new LoadedModule(0x2000_0000, @"C:\Elsewhere\GameAssembly.dll"));

		Assert.Equal(
			$"The Game client (pid {GameId}) has loaded GameAssembly.dll more than once (0x10000000, 0x20000000), so raid-bridge can't tell which is its image.",
			Refusal());
	}

	[Fact]
	public void Closes_the_game_client_when_it_refuses_to_open_the_adapter()
	{
		var process = _processes.AddGameClient();
		process.ModulesError = ErrorPartialCopy;

		_ = Refusal();

		Assert.True(process.Disposed);
	}

	private WindowsGameMemory OpenGameClient()
	{
		_processes.AddGameClient();
		return WindowsGameMemory.Open(_processes);
	}

	private string Refusal() => Assert.Throws<GameMemoryException>(() => WindowsGameMemory.Open(_processes)).Message;
}
