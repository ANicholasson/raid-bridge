using RaidBridge.Game.Il2Cpp;

namespace RaidBridge.Game.Numerics;

/// <summary>
/// The game's fixed-point numbers, <c>Plarium.Common.Numerics.Fixed</c>: a long, <c>m_rawValue</c>, that counts in
/// steps of one over the type's own constant <c>ONE</c>, which the installed game declares (2^32 at metadata v39, so
/// Q32.32). A value is written as the game holds it, scaled by <c>ONE</c> and nothing else (#10, #26).
/// </summary>
/// <remarks>
/// Where raid-bridge works a value out as the game does, such as a Champion's Base stats, it does the game's own
/// arithmetic on raw values, so the result is the step the game itself would hold
/// (https://github.com/ANicholasson/raid-bridge/issues/10). That arithmetic is Q32.32 only, as the installed v39
/// build's <c>Fixed</c> operators do it: a double becomes a Fixed by truncation, a product is rounded down to a step,
/// and a quotient to the nearest step, a half step rounding up.
/// </remarks>
internal static class FixedPoint
{
	private const string Fixed = "Plarium.Common.Numerics.Fixed";

	/// <summary>The fraction bits of the Q32.32 the game's arithmetic works in.</summary>
	private const int FractionBits = 32;

	private const long One = 1L << FractionBits;

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

	/// <summary>The raw value a Fixed field of <paramref name="owner"/> holds, for the game's own arithmetic, named after the field.</summary>
	public static FieldValue<long> RawOf(ManagedObject owner, string fieldName) => RawOfValue(owner.Value(fieldName, Fixed), $"{owner.ClassName}.{fieldName}");

	/// <summary>
	/// The raw value of a Fixed read as its own object, such as a static one, for the game's own arithmetic, named
	/// <paramref name="field"/>.
	/// </summary>
	public static FieldValue<long> RawOfValue(ManagedObject value, string field)
	{
		var one = value.Runtime.Metadata.Constant(Fixed, "ONE");
		return one == One
			? value.Int64("m_rawValue") with { Field = field }
			: throw new GameDataException($"The game's {Fixed}.ONE is {one}: raid-bridge does the game's own arithmetic in Q32.32 only, where it is {One}.");
	}

	/// <summary>A whole number as a Fixed, as the game's <c>(Fixed)long</c> makes it.</summary>
	public static long FromInteger(long value) =>
		value is > -(1L << 31) and < 1L << 31
			? value << FractionBits
			: throw new GameDataException($"{value} is out of the range of a {Fixed}.");

	/// <summary>A double as a Fixed, as the game's <c>(Fixed)double</c> makes it: scaled by 2^32 and truncated.</summary>
	public static long FromDouble(double value, string what)
	{
		var scaled = value * One;
		return double.IsFinite(scaled) && scaled > long.MinValue && scaled < long.MaxValue
			? (long)scaled
			: throw new GameDataException($"{what} is {value}, which is out of the range of a {Fixed}.");
	}

	/// <summary>The product, as the game's <c>Fixed * Fixed</c> gives it: rounded down to a step.</summary>
	public static long Multiply(long x, long y)
	{
		var product = ((Int128)x * y) >> FractionBits;
		return product >= long.MinValue && product <= long.MaxValue
			? (long)product
			: throw new GameDataException($"The product of the {Fixed} values {ToText(x)} and {ToText(y)} is out of the range of a {Fixed}.");
	}

	/// <summary>
	/// The quotient, as the game's <c>Fixed / Fixed</c> gives it: the quotient to one bit past a step, plus half a step,
	/// then rounded down to a step, with the sign of the two.
	/// </summary>
	public static long Divide(long x, long y)
	{
		if (y == 0)
		{
			throw new GameDataException($"raid-bridge would divide the {Fixed} {ToText(x)} by 0.");
		}

		var quotient = (((UInt128)Magnitude(x) << (FractionBits + 1)) / Magnitude(y)) + 1;
		var magnitude = quotient >> 1;
		return magnitude <= long.MaxValue
			? (x < 0) != (y < 0) ? -(long)magnitude : (long)magnitude
			: throw new GameDataException($"The quotient of the {Fixed} values {ToText(x)} and {ToText(y)} is out of the range of a {Fixed}.");
	}

	/// <summary>
	/// The whole number nearest the value, a half going to the even one, as the game's
	/// <c>FixedMath.Round(value, MidpointRounding.ToEven)</c> gives it.
	/// </summary>
	public static long RoundToEven(long value)
	{
		const long fraction = One - 1;
		const long half = One >> 1;
		var floor = value & ~fraction;
		return (value & fraction) switch
		{
			< half => floor,
			> half => floor + One,
			_ => (floor & One) == 0 ? floor : floor + One,
		};
	}

	/// <summary>A raw value as the number it stands for, exactly.</summary>
	public static double ToDouble(long raw) =>
		raw is > -MaxExact and < MaxExact
			? (double)raw / One
			: throw new GameDataException($"The {Fixed} {raw} / 2^{FractionBits} can't be written exactly.");

	private static ulong Magnitude(long value) => value < 0 ? (ulong)-(Int128)value : (ulong)value;

	private static string ToText(long raw) => FormattableString.Invariant($"{raw} / 2^{FractionBits}");
}
