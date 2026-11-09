using RaidBridge.Domain.Accounts;

namespace RaidBridge.Domain.Tests.Accounts;

public class AccountIdHashTests
{
	/// <summary>The expected value is from Python's hmac module: hmac.new(bytes(range(32)), b"123456789", "sha256").</summary>
	[Fact]
	public void Is_hmac_sha256_of_the_decimal_id_keyed_by_the_salt() =>
		Assert.Equal(
			"hmac-sha256:a2fb4a2e5d7a21b17d1213d493a97fffd9c65f4f57dcd304c0b8bc58d310027f",
			AccountIdHash.Of(new AccountId(123456789), CountingSalt()).Value);

	[Fact]
	public void Is_the_same_for_the_same_account_and_salt() =>
		Assert.Equal(AccountIdHash.Of(new AccountId(42), CountingSalt()), AccountIdHash.Of(new AccountId(42), CountingSalt()));

	[Fact]
	public void Differs_for_another_salt() =>
		Assert.NotEqual(AccountIdHash.Of(new AccountId(42), CountingSalt()), AccountIdHash.Of(new AccountId(42), new Salt(new byte[Salt.Length])));

	[Fact]
	public void Differs_for_another_account() =>
		Assert.NotEqual(AccountIdHash.Of(new AccountId(42), CountingSalt()), AccountIdHash.Of(new AccountId(43), CountingSalt()));

	private static Salt CountingSalt() => new([.. Enumerable.Range(0, Salt.Length).Select(value => (byte)value)]);
}
