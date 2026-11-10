using RaidBridge.Game.Testing.Champions;

namespace RaidBridge.Game.Testing.Artifacts;

/// <summary>
/// The Artifacts a synthetic Game client holds, in the game's own terms: where the Account keeps them, each Artifact,
/// the Artifact each hero wears in each slot, and the static data's sets that name them. Enum fields hold the game's
/// numbers, such as 5 for a Weapon, and Fixed values their raw Q32.32 longs. Every id and name is made up (spec #20).
/// </summary>
public sealed record SyntheticArtifactData(
	SyntheticArtifactStorage Storage,
	IReadOnlyList<SyntheticArtifact> Artifacts,
	IReadOnlyDictionary<int, IReadOnlyDictionary<int, int>> SlotsByHeroId,
	IReadOnlyList<SyntheticArtifactSet> Sets)
{
	/// <summary>
	/// The knight (hero 3) wears a weapon of the Speed set, and a helmet that was ascended and a ring locked to the Dark
	/// Elves, which make a complete Life set, with its chest slot empty; the Deep Vault archer (hero 12) has only an empty slot. Nobody wears Artifact 104, plain boots,
	/// or Artifact 105, a cloak locked to the High Elves with a Glyph on its Substat.
	/// </summary>
	public static SyntheticArtifactData Default { get; } = new(
		Storage: SyntheticArtifactStorage.AccountList,
		Artifacts:
		[
			new(Id: 101, Kind: 5)
			{
				SetKind = 4,
				Primary = new(Stat: 2, Value: Q32(265), Absolute: true),
				Secondary = [new(Stat: 4, Value: Q32(12), Absolute: true, Glyph: Q32(3), Level: 2), new(Stat: 7, Value: Q32(0.06), Absolute: false, Level: 1)],
			},
			new(Id: 102, Kind: 1)
			{
				Rank = 5,
				Rarity = 4,
				Level = 12,
				SetKind = 1,
				AscendLevel = 2,
				Primary = new(Stat: 1, Value: Q32(2120), Absolute: true),
				AscendBonus = new(Stat: 3, Value: Q32(0.04), Absolute: false),
			},
			new(Id: 103, Kind: 7)
			{
				Level = 8,
				SetKind = 1,
				RequiredFraction = 11,
				Primary = new(Stat: 1, Value: Q32(1640), Absolute: true),
				Secondary = [new(Stat: 2, Value: Q32(22), Absolute: true)],
			},
			new(Id: 104, Kind: 4) { Primary = new(Stat: 4, Value: Q32(45), Absolute: true) },
			new(Id: 105, Kind: 8)
			{
				Rank = 5,
				Rarity = 4,
				Level = 4,
				SetKind = 1,
				RequiredFraction = 2,
				Primary = new(Stat: 2, Value: Q32(120), Absolute: true),
				Secondary = [new(Stat: 4, Value: Q32(5), Absolute: true, Glyph: Q32(2), Level: 1)],
			},
		],
		SlotsByHeroId: new Dictionary<int, IReadOnlyDictionary<int, int>>
		{
			[3] = new Dictionary<int, int> { [7] = 103, [5] = 101, [2] = 0, [1] = 102 },
			[12] = new Dictionary<int, int> { [4] = 0 },
		},
		Sets:
		[
			new(1, new("l10n:artifact-set/name?id=1#static", "Synthetic Life")) { Bonus = new(Stat: 1, Value: Q32(0.15), Absolute: false) },
			new(4, new("l10n:artifact-set/name?id=4#static", "Synthetic Speed")) { Bonuses = [new(Stat: 4, Value: Q32(0.12), Absolute: false), new(Stat: 6, Value: Q32(40), Absolute: true)] },
		]);

	/// <summary>A number as the game's Fixed holds it, the nearest step of 2^-32.</summary>
	public static long Q32(double value) => (long)Math.Round(value * (1L << 32));
}

/// <summary>Where the Account keeps its Artifacts: its own list, or behind the storage resolver after storage migration.</summary>
public enum SyntheticArtifactStorage
{
	/// <summary>In UserArtifactData.Artifacts, with ArtifactStorageResolver._implementation null.</summary>
	AccountList,

	/// <summary>In the cache of the ExternalArtifactsStorage that ArtifactStorageResolver._implementation refers to.</summary>
	ExternalStorage,
}

/// <summary>An Artifact, as SharedModel.Meta.Artifacts.Artifact holds it. A null ascension level or bonus means it has none.</summary>
public sealed record SyntheticArtifact(int Id, int Kind)
{
	public int Rank { get; init; } = 6;

	public int Rarity { get; init; } = 5;

	public int Level { get; init; } = 16;

	public int SetKind { get; init; } = 4;

	public int? AscendLevel { get; init; }

	public int RequiredFraction { get; init; }

	public SyntheticBonus Primary { get; init; } = new(Stat: 2, Value: 0, Absolute: true);

	/// <summary>The Substats, or null for no list at all, as the game holds an Artifact that never rolled one.</summary>
	public IReadOnlyList<SyntheticBonus>? Secondary { get; init; } = [];

	public SyntheticBonus? AscendBonus { get; init; }
}

/// <summary>
/// An ArtifactBonus: its stat kind, its value and Glyph as raw Fixed longs, whether the value is absolute, and its
/// level, with the second part of a Glyph the game can hold, <c>_rarityBasedPowerUpValue</c>, or null for none.
/// </summary>
public sealed record SyntheticBonus(int Stat, long Value, bool Absolute, long Glyph = 0, int Level = 0, long? RarityGlyph = null);

/// <summary>
/// An ArtifactSetInfo: its set kind and its name, and what it gives. A set that takes <see cref="PiecesPerSet"/>
/// pieces gives its single <see cref="Bonus"/>, or its <see cref="Bonuses"/> when it has none, for each such many; a set
/// with <see cref="SubSets"/> grows by the piece, and takes 0.
/// </summary>
public sealed record SyntheticArtifactSet(int Kind, SyntheticText Name)
{
	public int PiecesPerSet { get; init; } = 2;

	public SyntheticStatBonus? Bonus { get; init; }

	public IReadOnlyList<SyntheticStatBonus> Bonuses { get; init; } = [];

	/// <summary>The ArtifactSubSetInfos, each the number of pieces it is for and its StatBonuses, or null for no list at all.</summary>
	public IReadOnlyList<(int Pieces, IReadOnlyList<SyntheticStatBonus> Bonuses)>? SubSets { get; init; }
}

/// <summary>A static StatBonus: its stat kind, its value as a raw Fixed long, and whether it is absolute.</summary>
public sealed record SyntheticStatBonus(int Stat, long Value, bool Absolute);
