using RaidBridge.Game.Il2Cpp;

namespace RaidBridge.Game.Numerics;

/// <summary>
/// The game's fixed-point numbers, <c>Plarium.Common.Numerics.Fixed</c>: a long, <c>m_rawValue</c>, that counts in
/// steps of one over the type's own constant <c>ONE</c>, which the installed game declares (2^32 at metadata v39, so
/// Q32.32). A value is written as the game holds it, scaled by <c>ONE</c> and nothing else (#10, #26).
/// </summary>
internal static class FixedPoint
{
	private const string Fixed = "Plarium.Common.Numerics.Fixed";

	/// <summary>The largest raw value a double holds exactly, so no value is rounded on its way into a Snapshot.</summary>
	private const long MaxExact = 1L << 53;

	/// <summary>The Fixed a field of <paramref name="owner"/> holds, as the number it stands for.</summary>
	public static double Of(ManagedObject owner, string fieldName)
	{
		var raw = owner.Value(fieldName, Fixed).Int64("m_rawValue");
		var one = owner.Runtime.Metadata.Constant(Fixed, "ONE");
		// A power of two keeps the division exact.
		if (one <= 0 || (one & (one - 1)) != 0)
		{
			throw new GameDataException($"The game's {Fixed}.ONE is {one}, where raid-bridge expects a power of two.");
		}

		return raw.Value is > -MaxExact and < MaxExact
			? (double)raw.Value / one
			: throw new GameDataException($"{owner.ClassName}.{fieldName} holds a {Fixed} whose {raw}, which raid-bridge can't write exactly.");
	}
}
