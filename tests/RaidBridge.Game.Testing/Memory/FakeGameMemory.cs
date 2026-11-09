using RaidBridge.Game.Memory;

namespace RaidBridge.Game.Testing.Memory;

/// <summary>
/// A Game client's address space made of mapped regions, behind the same port as the real adapters. It keeps every
/// read it serves, so a test can prove that something was never read.
/// </summary>
public sealed class FakeGameMemory(GameModule gameAssembly, DateTimeOffset startedAt) : IGameMemory
{
	private readonly List<(ulong Start, byte[] Bytes)> _regions = [];
	private readonly List<(ulong Address, int Length)> _reads = [];

	public DateTimeOffset StartedAt { get; } = startedAt;

	public GameModule GameAssembly { get; } = gameAssembly;

	/// <summary>Maps the bytes at the address. The array is kept, not copied, so writes to it show in later reads.</summary>
	public void Map(ulong address, byte[] bytes) => _regions.Add((address, bytes));

	public void Read(ulong address, Span<byte> buffer)
	{
		var length = (ulong)buffer.Length;
		foreach (var (start, bytes) in _regions)
		{
			if (address >= start && address - start <= (ulong)bytes.Length && length <= (ulong)bytes.Length - (address - start))
			{
				bytes.AsSpan((int)(address - start), buffer.Length).CopyTo(buffer);
				_reads.Add((address, buffer.Length));
				return;
			}
		}

		throw new GameMemoryException($"Nothing is mapped at 0x{address:X} for {buffer.Length} bytes.");
	}

	public bool WasRead(ulong start, int length) =>
		_reads.Any(read => read.Address < start + (ulong)length && start < read.Address + (ulong)read.Length);
}
