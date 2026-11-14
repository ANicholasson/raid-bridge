namespace RaidBridge.Game.Testing.Accounts;

/// <summary>
/// What a synthetic Game client's SharedModel.Meta.Account.UserAccount holds besides its Resources: the Account's
/// Level, its Experience and its TotalPower, a double.
/// </summary>
public sealed record SyntheticAccountData(int Level, int Experience, double TotalPower)
{
	/// <summary>Level 54, 31 250 experience and a power that isn't a whole number, so only a reader that rounds would write it as one.</summary>
	public static SyntheticAccountData Default { get; } = new(Level: 54, Experience: 31_250, TotalPower: 1_234_567.891);
}
