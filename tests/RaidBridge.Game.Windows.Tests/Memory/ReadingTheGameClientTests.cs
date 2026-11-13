using System.Text;
using RaidBridge.Game.Memory;
using RaidBridge.Game.Windows.Memory;
using static RaidBridge.Game.Windows.Tests.Memory.FakeWin32Processes;

namespace RaidBridge.Game.Windows.Tests.Memory;

/// <summary>Reading through the Windows adapter with ReadProcessMemory's answers made up: what is copied, and each failure named.</summary>
public sealed class ReadingTheGameClientTests
{
	private const int ErrorAccessDenied = 5;
	private const int ErrorPartialCopy = 299;
	private const int ErrorNoAccess = 998;

	private readonly FakeWin32Processes _processes = new();
	private readonly FakeWin32Process _gameClient;

	public ReadingTheGameClientTests() => _gameClient = _processes.AddGameClient();

	[Fact]
	public void Fills_the_buffer_from_the_game_client_at_the_address()
	{
		_gameClient.Answer = (address, buffer) =>
		{
			Assert.Equal(ImageBase, address);
			"MZ"u8.CopyTo(buffer);
			return (buffer.Length, 0);
		};
		var header = new byte[2];

		WindowsGameMemory.Open(_processes).Read(ImageBase, header);

		Assert.Equal("MZ", Encoding.ASCII.GetString(header));
	}

	[Theory]
	[InlineData(ErrorPartialCopy)]
	[InlineData(ErrorNoAccess)]
	public void Names_an_address_with_nothing_mapped(int error)
	{
		_gameClient.Answer = (_, _) => (0, error);

		Assert.Equal("Nothing is mapped at 0xDEAD0000 for 8 bytes in the Game client.", Failure(0xDEAD_0000));
	}

	[Fact]
	public void Refuses_a_read_cut_short_by_the_end_of_a_region()
	{
		_gameClient.Answer = (_, _) => (3, ErrorPartialCopy);

		Assert.Equal("Only 3 of 8 bytes at 0xDEAD0000 are mapped in the Game client.", Failure(0xDEAD_0000));
	}

	[Fact]
	public void Says_the_game_client_has_exited()
	{
		_gameClient.Answer = (_, _) => (0, ErrorPartialCopy);
		_gameClient.Exited = true;

		Assert.Equal($"The Game client (pid {GameId}) has exited.", Failure(ImageBase));
	}

	[Fact]
	public void Names_any_other_windows_error()
	{
		_gameClient.Answer = (_, _) => (0, ErrorAccessDenied);

		Assert.Equal("Reading 8 bytes at 0xDEAD0000 from the Game client failed with Windows error 5.", Failure(0xDEAD_0000));
	}

	private string Failure(ulong address)
	{
		var memory = WindowsGameMemory.Open(_processes);
		return Assert.Throws<GameMemoryException>(() => memory.Read(address, new byte[8])).Message;
	}
}
