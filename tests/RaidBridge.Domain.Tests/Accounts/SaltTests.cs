using RaidBridge.Domain.Accounts;

namespace RaidBridge.Domain.Tests.Accounts;

public class SaltTests
{
	[Theory]
	[InlineData(0)]
	[InlineData(31)]
	[InlineData(33)]
	public void Is_32_bytes(int length) =>
		Assert.Throws<ArgumentException>(() => new Salt(new byte[length]));

	[Fact]
	public void Never_prints_its_bytes() =>
		Assert.Equal("Salt (secret)", new Salt(Enumerable.Repeat((byte)0xAB, Salt.Length).ToArray()).ToString());
}
