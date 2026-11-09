using System.Buffers.Binary;
using System.Text;
using RaidBridge.Game.Testing.Memory;

namespace RaidBridge.Game.Testing.Il2Cpp;

/// <summary>One mapped region of a fake address space, filled front to back, with the writes an IL2CPP layout needs.</summary>
internal sealed class SyntheticHeap
{
	private readonly byte[] _bytes;
	private int _used;

	public SyntheticHeap(FakeGameMemory memory, ulong start, int size)
	{
		Start = start;
		_bytes = new byte[size];
		memory.Map(start, _bytes);
	}

	public ulong Start { get; }

	public ulong Allocate(int size)
	{
		var address = Start + (ulong)_used;
		_used += (size + 15) & ~15;
		if (_used > _bytes.Length)
		{
			throw new InvalidOperationException($"The synthetic heap at 0x{Start:X} is full.");
		}

		return address;
	}

	public ulong CString(string text)
	{
		var bytes = Encoding.UTF8.GetBytes(text);
		var address = Allocate(bytes.Length + 1);
		bytes.CopyTo(Span(address, bytes.Length));
		return address;
	}

	public ulong ReadPointer(ulong address) => BinaryPrimitives.ReadUInt64LittleEndian(Span(address, 8));

	public void Pointer(ulong address, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(Span(address, 8), value);

	public void Int64(ulong address, long value) => BinaryPrimitives.WriteInt64LittleEndian(Span(address, 8), value);

	public void Int32(ulong address, int value) => BinaryPrimitives.WriteInt32LittleEndian(Span(address, 4), value);

	public void UInt32(ulong address, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(Span(address, 4), value);

	public void UInt16(ulong address, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(Span(address, 2), value);

	public void Bytes(ulong address, ReadOnlySpan<byte> value) => value.CopyTo(Span(address, value.Length));

	private Span<byte> Span(ulong address, int length) => _bytes.AsSpan((int)(address - Start), length);
}
