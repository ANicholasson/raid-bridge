namespace RaidBridge.Domain.Accounts;

/// <summary>
/// The player's secret, 32 random bytes made once on their machine, that keys the Account id hash. It is never
/// printed or logged: only the hash reads its bytes.
/// </summary>
public sealed class Salt
{
	public const int Length = 32;

	private readonly byte[] _bytes;

	public Salt(ReadOnlySpan<byte> bytes)
	{
		if (bytes.Length != Length)
		{
			throw new ArgumentException($"A salt is {Length} bytes, not {bytes.Length}.", nameof(bytes));
		}

		_bytes = bytes.ToArray();
	}

	internal ReadOnlySpan<byte> Bytes => _bytes;

	public override string ToString() => "Salt (secret)";
}
