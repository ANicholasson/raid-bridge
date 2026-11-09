using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace RaidBridge.Domain.Accounts;

/// <summary>
/// The Account id as it leaves the machine: <c>hmac-sha256:</c> and the lowercase hex of HMAC-SHA256, keyed by the
/// player's salt, over the UTF-8 of the id as an invariant decimal string (spec #20). The same Account and salt
/// always give the same hash, and without the salt the id can't be recovered from it.
/// </summary>
public sealed record AccountIdHash
{
	public const string Prefix = "hmac-sha256:";

	private AccountIdHash(string value) => Value = value;

	public string Value { get; }

	public static AccountIdHash Of(AccountId id, Salt salt)
	{
		var message = Encoding.UTF8.GetBytes(id.Value.ToString(CultureInfo.InvariantCulture));
		return new(Prefix + Convert.ToHexStringLower(HMACSHA256.HashData(salt.Bytes, message)));
	}

	public override string ToString() => Value;
}
