using System.Runtime.Versioning;
using RaidBridge.Game.Linux.Memory;
using RaidBridge.Game.Memory;
using static RaidBridge.Game.Linux.Tests.Memory.FakeProcFileSystem;

namespace RaidBridge.Game.Linux.Tests.Memory;

/// <summary>Opening the Linux adapter on a made-up process file system: finding Raid.exe and GameAssembly.dll, and every refusal on the way.</summary>
public sealed class FindingTheGameClientTests : IDisposable
{
	private readonly FakeProcFileSystem _proc = new();
	private int _reads;

	[Fact]
	public void Finds_the_game_client_by_its_comm_and_game_assembly_by_its_offset_zero_mapping()
	{
		_proc.AddProcess(100, "steam.exe");
		_proc.AddGameClient();
		_proc.AddProcess(5000, "Vuplex WebView");

		var memory = Open();

		Assert.Equal(new GameModule(ImageBase, _proc.GameAssemblyPath), memory.GameAssembly);
	}

	[Fact]
	public void Gives_the_time_the_game_client_started()
	{
		_proc.AddGameClient();

		// A second past the start, because the boot time is whole seconds rounded down.
		Assert.Equal(BootedAt.AddHours(1).AddSeconds(1), Open().StartedAt);
	}

	[Fact]
	public void Reads_nothing_from_the_game_client_to_open_it()
	{
		_proc.AddGameClient();

		_ = Open();

		Assert.Equal(0, _reads);
	}

	[Fact]
	public void Says_the_game_client_is_not_running_when_no_process_is_named_raid_exe()
	{
		_proc.AddProcess(100, "PlariumPlay.exe");
		_proc.AddExitedProcess(101);

		Assert.Equal("The Game client isn't running: no process is named Raid.exe. Start RAID from Steam, then try again.", Refusal());
	}

	[Fact]
	[UnsupportedOSPlatform("windows")]
	public void Passes_over_a_process_whose_comm_linux_hides()
	{
		Assert.SkipWhen(OperatingSystem.IsWindows() || Environment.IsPrivilegedProcess, "Needs file modes, which root reads past and Windows doesn't have.");
		_proc.AddProcess(1, "systemd", userId: 0);
		_proc.HideComm(1);
		_proc.AddGameClient();

		Assert.Equal(new GameModule(ImageBase, _proc.GameAssemblyPath), Open().GameAssembly);
	}

	[Fact]
	public void Names_a_process_file_it_cannot_parse()
	{
		_proc.AddGameClient();
		_proc.Garble(GamePid, "stat", "4242 (Raid.exe) S 1 2 3\n");

		var refusal = Assert.Throws<GameMemoryException>(() => Open());

		Assert.Matches(@"^\S+stat isn't in the format raid-bridge reads\.$", refusal.Message);
	}

	[Fact]
	public void Refuses_to_guess_between_two_processes_named_raid_exe()
	{
		_proc.AddGameClient();
		_proc.AddProcess(4300, "Raid.exe");

		Assert.Equal("2 processes are named Raid.exe (4242, 4300), so raid-bridge can't tell which is the Game client. Close all but one, then try again.", Refusal());
	}

	[Fact]
	public void Says_game_assembly_is_not_loaded_yet()
	{
		_proc.AddGameClient(maps: "140000000-140001000 r--p 00000000 00:24 7610001 /games/raid/Raid.exe\n");

		Assert.Equal($"The Game client (pid {GamePid}) hasn't loaded GameAssembly.dll yet. Wait until RAID shows its main screen, then try again.", Refusal());
	}

	[Fact]
	public void Refuses_a_game_assembly_replaced_after_the_game_client_loaded_it()
	{
		_proc.AddGameClient(maps: GameClientMaps(_proc.GameAssemblyPath + " (deleted)"));

		Assert.Equal(
			$"{_proc.GameAssemblyPath} was replaced after the Game client loaded it: the game updated under the running Game client. Restart the Game client and try again.",
			Refusal());
	}

	[Fact]
	public void Takes_a_mapping_the_kernel_repeats_as_one()
	{
		var maps = GameClientMaps(_proc.GameAssemblyPath);
		_proc.AddGameClient(maps: maps + maps.Split('\n')[1] + "\n");

		Assert.Equal(ImageBase, Open().GameAssembly.ImageBase);
	}

	[Fact]
	public void Refuses_two_images_of_game_assembly()
	{
		_proc.AddGameClient(maps: GameClientMaps(_proc.GameAssemblyPath) + $"7f0000000000-7f0000001000 r--p 00000000 00:24 7610756 {_proc.GameAssemblyPath}\n");

		Assert.Equal(
			$"The Game client (pid {GamePid}) maps GameAssembly.dll from offset 0 more than once (0x{ImageBase:X}, 0x7F0000000000), so raid-bridge can't tell which is its image.",
			Refusal());
	}

	[Theory]
	[InlineData(0u, 0u)]
	[InlineData(PlayerId, 0u)]
	[InlineData(0u, PlayerId)]
	public void Refuses_to_start_as_root(uint userId, uint effectiveUserId)
	{
		_proc.AddGameClient();

		Assert.Equal(
			"raid-bridge refuses to run as root: it reads the Game client with your own user's rights and needs nothing more. Run it as the user who plays RAID.",
			Refusal(new LinuxUser(userId, effectiveUserId, 0)));
	}

	[Fact]
	public void Refuses_to_start_with_any_effective_capability()
	{
		_proc.AddGameClient();

		// CAP_SYS_PTRACE alone, the capability #13 chose not to need.
		Assert.Equal(
			"raid-bridge refuses to run with capabilities (CapEff 0000000000080000): it reads the Game client with your own user's rights and needs nothing more. Run it as the user who plays RAID.",
			Refusal(new LinuxUser(PlayerId, PlayerId, 1ul << 19)));
	}

	[Fact]
	[UnsupportedOSPlatform("windows")]
	public void Names_the_cause_when_linux_will_not_show_the_game_clients_maps()
	{
		Assert.SkipWhen(OperatingSystem.IsWindows() || Environment.IsPrivilegedProcess, "Needs file modes, which root reads past and Windows doesn't have.");
		_proc.AddGameClient(userId: 1001);
		_proc.HideMaps(GamePid);

		Assert.Equal(
			$"raid-bridge isn't allowed to read the Game client (pid {GamePid}): it runs as user 1001, and raid-bridge as user {PlayerId}. Run raid-bridge as the user who plays RAID.",
			Refusal());
	}

	public void Dispose() => _proc.Dispose();

	private LinuxGameMemory Open(LinuxUser? user = null) =>
		LinuxGameMemory.Open(_proc.FileSystem, user ?? Player, (_, _, _) =>
		{
			_reads++;
			return (-1, 14);
		});

	private string Refusal(LinuxUser? user = null) => Assert.Throws<GameMemoryException>(() => Open(user)).Message;
}
