using RaidBridge.Domain.Accounts;

namespace RaidBridge.Domain.Tests.Accounts;

public class AccountIdTests
{
	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(long.MinValue)]
	public void Is_positive(long value) =>
		Assert.Throws<ArgumentOutOfRangeException>(() => new AccountId(value));
}
