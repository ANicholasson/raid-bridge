using System.Text;
using RaidBridge.Game.Memory;

namespace RaidBridge.Game.Testing.Memory;

/// <summary>
/// What every memory adapter does, the fake included (ADR-0002). An adapter's test project derives from this and
/// gives it the adapter, opened on a running Game client.
/// </summary>
public abstract class GameMemoryContract
{
	protected abstract IGameMemory Memory { get; }

	[Fact]
	public void Game_assembly_is_mapped_from_its_file()
	{
		var module = Memory.GameAssembly;

		Assert.Equal("GameAssembly.dll", Path.GetFileName(module.FilePath));
		Assert.True(File.Exists(module.FilePath), $"{module.FilePath} doesn't exist on this machine.");
	}

	[Fact]
	public void Game_assembly_image_starts_with_a_pe_header()
	{
		var header = new byte[2];

		Memory.Read(Memory.GameAssembly.ImageBase, header);

		Assert.Equal("MZ", Encoding.ASCII.GetString(header));
	}

	[Fact]
	public void The_game_client_started_in_the_past() =>
		Assert.True(Memory.StartedAt <= DateTimeOffset.UtcNow, $"The Game client started at {Memory.StartedAt:O}, in the future.");

	[Fact]
	public void Refuses_an_address_with_nothing_mapped_and_names_it()
	{
		var error = Assert.Throws<GameMemoryException>(() => Memory.Read(0, new byte[8]));

		Assert.Contains("0x0", error.Message, StringComparison.Ordinal);
	}
}
