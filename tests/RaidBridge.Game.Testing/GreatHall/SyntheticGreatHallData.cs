namespace RaidBridge.Game.Testing.GreatHall;

/// <summary>
/// The Great Hall a synthetic Game client holds, in the game's own terms: each Affinity's levels by its Element, each
/// a dictionary of levels by StatKindId or null, as the game may hold it; and, in the static data, how many levels the
/// game lists a bonus for, by StatKindId. Keys hold the game's numbers, such as 1 for Magic, and are laid out in the
/// order given here.
/// </summary>
public sealed record SyntheticGreatHallData(
	IReadOnlyDictionary<int, IReadOnlyDictionary<int, int>?> Levels,
	IReadOnlyDictionary<int, int> MaxLevels)
{
	/// <summary>Ten levels of each stat the Great Hall raises: Health, Attack, Defence, Resistance, Accuracy and CriticalDamage.</summary>
	public static IReadOnlyDictionary<int, int> TenLevels { get; } = new Dictionary<int, int> { [1] = 10, [2] = 10, [3] = 10, [5] = 10, [6] = 10, [8] = 10 };

	/// <summary>
	/// A partly built Great Hall: Void and Magic with levels, each out of the game's order, so only the reader can put
	/// them in it; Force held as null and Spirit as an empty dictionary, both with no levels.
	/// </summary>
	public static SyntheticGreatHallData Default { get; } = new(
		new Dictionary<int, IReadOnlyDictionary<int, int>?>
		{
			[4] = new Dictionary<int, int> { [8] = 2, [1] = 10 },
			[2] = null,
			[1] = new Dictionary<int, int> { [2] = 3 },
			[3] = new Dictionary<int, int>(),
		},
		TenLevels);

	/// <summary>A Great Hall the player hasn't bought a level in.</summary>
	public static SyntheticGreatHallData None { get; } = new(new Dictionary<int, IReadOnlyDictionary<int, int>?>(), TenLevels);

	/// <summary>One Affinity's levels, by StatKindId, and nothing else.</summary>
	public static SyntheticGreatHallData Only(int element, IReadOnlyDictionary<int, int> levels) =>
		new(new Dictionary<int, IReadOnlyDictionary<int, int>?> { [element] = levels }, TenLevels);
}
