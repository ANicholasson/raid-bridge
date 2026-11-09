using System.Text;
using RaidBridge.Game.Linux.Memory;
using RaidBridge.Game.Memory;
using static RaidBridge.Game.Linux.Tests.Memory.FakeProcFileSystem;

namespace RaidBridge.Game.Linux.Tests.Memory;

/// <summary>
/// Reading through the Linux adapter with process_vm_readv's answers made up: what is copied, and each failure named,
/// with every cause of EPERM from #13. No cause suggests a fix that needs root.
/// </summary>
public sealed class ReadingTheGameClientTests : IDisposable
{
	private const int Eperm = 1;
	private const int Esrch = 3;
	private const int Efault = 14;

	private readonly FakeProcFileSystem _proc = new();

	[Fact]
	public void Fills_the_buffer_from_the_game_client_at_the_address()
	{
		_proc.AddGameClient();
		var memory = Open((pid, address, buffer) =>
		{
			Assert.Equal((GamePid, ImageBase), (pid, address));
			"MZ"u8.CopyTo(buffer);
			return (buffer.Length, 0);
		});
		var header = new byte[2];

		memory.Read(ImageBase, header);

		Assert.Equal("MZ", Encoding.ASCII.GetString(header));
	}

	[Fact]
	public void Names_an_address_with_nothing_mapped()
	{
		_proc.AddGameClient();

		Assert.Equal("Nothing is mapped at 0xDEAD0000 for 8 bytes in the Game client.", Failure(Fails(Efault), 0xDEAD_0000));
	}

	[Fact]
	public void Refuses_a_read_cut_short_by_the_end_of_a_mapping()
	{
		_proc.AddGameClient();

		Assert.Equal("Only 3 of 8 bytes at 0xDEAD0000 are mapped in the Game client.", Failure((_, _, _) => (3, 0), 0xDEAD_0000));
	}

	[Fact]
	public void Says_the_game_client_has_exited()
	{
		_proc.AddGameClient();

		Assert.Equal($"The Game client (pid {GamePid}) has exited.", Failure(Fails(Esrch)));
	}

	[Fact]
	public void Names_another_user_as_the_cause_of_eperm()
	{
		_proc.AddGameClient(userId: 1001);

		Assert.Equal(
			$"raid-bridge isn't allowed to read the Game client (pid {GamePid}): it runs as user 1001, and raid-bridge as user {PlayerId}. Run raid-bridge as the user who plays RAID.",
			Denial());
	}

	[Theory]
	[InlineData(1001u, PlayerId, PlayerId)]
	[InlineData(PlayerId, 1001u, PlayerId)]
	[InlineData(PlayerId, PlayerId, 1001u)]
	public void Names_another_user_as_the_cause_of_eperm_whichever_id_differs(uint real, uint effective, uint saved)
	{
		_proc.AddGameClient();
		_proc.UserIds(GamePid, real, effective, saved);

		Assert.Equal(
			$"raid-bridge isn't allowed to read the Game client (pid {GamePid}): it runs as user 1001, and raid-bridge as user {PlayerId}. Run raid-bridge as the user who plays RAID.",
			Denial());
	}

	[Fact]
	public void Names_ptrace_scope_3_as_the_cause_of_eperm()
	{
		_proc.AddGameClient();
		_proc.PtraceScope(3);

		Assert.Equal($"raid-bridge isn't allowed to read the Game client (pid {GamePid}): kernel.yama.ptrace_scope is 3 on this system, so no process may read another's memory.", Denial());
	}

	[Theory]
	[InlineData(1, "the program that started it")]
	[InlineData(2, "an administrator")]
	public void Names_a_game_outside_steams_container_as_the_cause_of_eperm(int scope, string allowed)
	{
		_proc.AddGameClient(userNamespace: HostNamespace);
		_proc.PtraceScope(scope);

		Assert.Equal(
			$"raid-bridge isn't allowed to read the Game client (pid {GamePid}): it isn't in Steam's container, so kernel.yama.ptrace_scope {scope} lets only {allowed} read it. Launch RAID from Steam with Proton, then try again.",
			Denial());
	}

	[Fact]
	public void Says_so_when_it_does_not_know_the_cause_of_eperm()
	{
		_proc.AddGameClient();
		_proc.RemoveYama();

		Assert.Equal(
			$"raid-bridge isn't allowed to read the Game client (pid {GamePid}), for a reason raid-bridge doesn't know: kernel.yama.ptrace_scope is not set, and the game runs as user {PlayerId} in its own user namespace.",
			Denial());
	}

	[Fact]
	public void Names_any_other_error_by_its_errno()
	{
		_proc.AddGameClient();

		Assert.Equal("Reading 8 bytes at 0x1000 from the Game client failed with errno 12.", Failure(Fails(12)));
	}

	public void Dispose() => _proc.Dispose();

	private static ProcessVmRead Fails(int error) => (_, _, _) => (-1, error);

	private LinuxGameMemory Open(ProcessVmRead read) => LinuxGameMemory.Open(_proc.FileSystem, Player, read);

	private string Failure(ProcessVmRead read, ulong address = 0x1000)
	{
		var memory = Open(read);
		return Assert.Throws<GameMemoryException>(() => memory.Read(address, new byte[8])).Message;
	}

	private string Denial()
	{
		var message = Failure(Fails(Eperm));
		Assert.DoesNotMatch(@"(?i)\b(sudo|su|pkexec|root|sysctl|chmod|chown)\b", message);
		return message;
	}
}
