namespace RaidBridge.Game.Il2Cpp;

/// <summary>
/// A two-dimensional array of doubles in the Game client's memory, a <c>double[,]</c>: its runtime class must be an
/// array class of rank 2 whose elements are System.Double, and it keeps its two lengths in its bounds, with its
/// elements row by row after its header. <see cref="Where"/> names the field it was read from, for errors.
/// </summary>
/// <remarks>
/// The installed game can declare such an array as <c>double[][]</c>: <c>BattleStats.Multipliers</c> is declared so
/// at metadata v39, but the Game client holds a <c>double[,]</c> in it
/// (https://github.com/ANicholasson/raid-bridge/issues/9). The two are laid out differently, so the reader checks
/// which it holds rather than trust the declaration.
/// </remarks>
internal sealed class ManagedMatrix
{
	/// <summary>Far longer than any array the reader looks at, so a corrupt length ends with an error, not a huge read.</summary>
	private const ulong MaxLength = 1 << 16;

	private const int Rank = 2;

	private readonly ManagedObject _array;

	public ManagedMatrix(ManagedObject array, string where)
	{
		_array = array;
		Where = where;
		var runtime = array.Runtime;
		var layout = runtime.Layout;
		string What(string part) => $"the {part} of {where}";

		var (elementClass, elementSize) = array.Class.Element();
		var rank = runtime.Byte(array.Class.Address + (ulong)layout.Class.Rank, () => What("rank"));
		if (rank != Rank || elementSize != sizeof(double) || !elementClass.Is("System.Double", () => What("element class")))
		{
			throw new GameDataException(
				$"{where} points to an array at 0x{array.Address:X} of rank {rank}, of {elementClass.FullName}, {elementSize} bytes each, where raid-bridge expects a two-dimensional array of System.Double, 8 bytes each.");
		}

		var bounds = runtime.Pointer(array.Address + (ulong)layout.Array.Bounds, () => What("bounds"));
		if (bounds == 0)
		{
			throw new GameDataException($"{where} points to an array at 0x{array.Address:X} of rank {Rank} with no bounds.");
		}

		var lengths = new ulong[Rank];
		for (var dimension = 0; dimension < Rank; dimension++)
		{
			var bound = bounds + (ulong)(dimension * layout.ArrayBounds.Size);
			lengths[dimension] = runtime.Pointer(bound + (ulong)layout.ArrayBounds.Length, () => What($"length of dimension {dimension}"));
			var lowerBound = runtime.Int32(bound + (ulong)layout.ArrayBounds.LowerBound, () => What($"lower bound of dimension {dimension}"));
			if (lowerBound != 0)
			{
				throw new GameDataException($"{where} points to an array at 0x{array.Address:X} whose dimension {dimension} starts at {lowerBound}, where raid-bridge expects 0.");
			}
		}

		var length = runtime.Pointer(array.Address + (ulong)layout.Array.MaxLength, () => What("length"));
		if (lengths[0] > MaxLength || lengths[1] > MaxLength || length != lengths[0] * lengths[1])
		{
			throw new GameDataException(
				$"{where} points to an array at 0x{array.Address:X} of {lengths[0]} by {lengths[1]} elements and length {length}, which aren't the lengths of a two-dimensional array.");
		}

		Rows = (int)lengths[0];
		Columns = (int)lengths[1];
	}

	public string Where { get; }

	public int Rows { get; }

	public int Columns { get; }

	/// <summary>The double at <paramref name="row"/> and <paramref name="column"/>.</summary>
	public double Double(int row, int column)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(row);
		ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(row, Rows);
		ArgumentOutOfRangeException.ThrowIfNegative(column);
		ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(column, Columns);
		var index = ((ulong)row * (ulong)Columns) + (ulong)column;
		return _array.Runtime.Double(_array.Address + (ulong)_array.Runtime.Layout.Array.Data + (index * sizeof(double)), () => $"{Where}[{row}, {column}]");
	}
}
