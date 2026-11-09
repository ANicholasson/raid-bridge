namespace RaidBridge.Domain.Accounts;

/// <summary>The id the game gives an Account: a positive number. It never leaves the machine; a Snapshot carries only its hash.</summary>
public readonly record struct AccountId
{
	public AccountId(long value)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
		Value = value;
	}

	public long Value { get; }
}
