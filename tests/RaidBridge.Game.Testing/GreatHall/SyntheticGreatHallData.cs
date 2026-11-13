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
	/// <summary>
	/// The Area Bonuses, each area's levels by its ObservatoryLocationId, each a dictionary of levels by StatKindId or
	/// null. By default AllianceBoss and DragonsLair hold levels, each out of the game's order; IceGolemCave is held as
	/// null and Hydra as an empty dictionary, both with no levels.
	/// </summary>
	public IReadOnlyDictionary<int, IReadOnlyDictionary<int, int>?> Areas { get; init; } = new Dictionary<int, IReadOnlyDictionary<int, int>?>
	{
		[7] = new Dictionary<int, int> { [2] = 3, [1] = 1 },
		[2] = null,
		[4] = new Dictionary<int, int> { [4] = 10 },
		[6] = new Dictionary<int, int>(),
	};

	/// <summary>
	/// The static data's ObservatoryTiers: each tier's areas, by ObservatoryLocationId, and how many levels it lists a
	/// bonus for, by StatKindId. Three tiers, as the game's own, each with ten levels of the eight stats the game's
	/// tiers raise; Siege is in none, as in the game's.
	/// </summary>
	public IReadOnlyList<SyntheticAreaTier> Tiers { get; init; } =
	[
		new([1], TenAreaLevels),
		new([2, 4], TenAreaLevels),
		new([6, 7], TenAreaLevels),
	];

	/// <summary>Ten levels of each stat an Area Bonus tier raises: Health, Attack, Defence, Speed, Resistance, Accuracy, CriticalDamage and IgnoreDefence.</summary>
	public static IReadOnlyDictionary<int, int> TenAreaLevels { get; } = new Dictionary<int, int> { [1] = 10, [2] = 10, [3] = 10, [4] = 10, [5] = 10, [6] = 10, [8] = 10, [10] = 10 };

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

	/// <summary>
	/// The made-up bonus the static data gives a level of a stat, as a raw Fixed and whether it is absolute: Resistance
	/// and Accuracy 5 points a level, the others 2% a level.
	/// </summary>
	public static (long Value, bool Absolute) Bonus(int stat, int level) =>
		stat is 5 or 6 ? (Artifacts.SyntheticArtifactData.Q32(5 * level), true) : (Artifacts.SyntheticArtifactData.Q32(0.02 * level), false);

	/// <summary>A Great Hall the player hasn't bought a level in, nor an Area Bonus.</summary>
	public static SyntheticGreatHallData None { get; } = new(new Dictionary<int, IReadOnlyDictionary<int, int>?>(), TenLevels)
	{
		Areas = new Dictionary<int, IReadOnlyDictionary<int, int>?>(),
	};

	/// <summary>One area's Area Bonus levels, by StatKindId, and nothing else in the Great Hall.</summary>
	public static SyntheticGreatHallData OnlyArea(int area, IReadOnlyDictionary<int, int> levels) =>
		None with { Areas = new Dictionary<int, IReadOnlyDictionary<int, int>?> { [area] = levels } };

	/// <summary>One Affinity's levels, by StatKindId, and nothing else: no Area Bonus either (Cursor Bugbot, PR 44).</summary>
	public static SyntheticGreatHallData Only(int element, IReadOnlyDictionary<int, int> levels) =>
		None with { Levels = new Dictionary<int, IReadOnlyDictionary<int, int>?> { [element] = levels } };
}

/// <summary>An Area Bonus tier, as SharedModel.Meta.Village.ObservatoryTierInfo holds it: its areas, and how many levels it lists a bonus for, by StatKindId.</summary>
public sealed record SyntheticAreaTier(IReadOnlyList<int> Areas, IReadOnlyDictionary<int, int> MaxLevels);
