using RaidBridge.Game.Testing.Artifacts;

namespace RaidBridge.Game.Testing.Champions;

/// <summary>
/// The heroes a synthetic Game client holds, in the game's own terms, with the static data that names and scales
/// them: hero and skill types, the two localization tables, and <c>BattleStats</c>' rank multipliers and
/// <c>ToPercent</c>. Enum fields hold the game's numbers, such as 6 for Stars6. Every id, name and number is made up
/// (spec #20).
/// </summary>
public sealed record SyntheticHeroData(
	IReadOnlyList<SyntheticHero> Heroes,
	IReadOnlyList<SyntheticHeroType> HeroTypes,
	IReadOnlyList<SyntheticSkillType> SkillTypes,
	IReadOnlyDictionary<string, string> StaticDataLocalization,
	IReadOnlyDictionary<string, string> ClientLocalization)
{
	/// <summary>
	/// Three heroes, one each in the roster, the Vault and the Deep Vault. Their names come from each place a name
	/// can: StaticDataLocalization, ClientLocalization, and a key's default text.
	/// </summary>
	public static SyntheticHeroData Default { get; } = new(
		Heroes:
		[
			new(Id: 7, TypeId: 6010)
			{
				Grade = 3,
				Level = 30,
				InStorage = true,
				Skills = [new(Id: 31, TypeId: 60101, Level: 1)],
			},
			new(Id: 3, TypeId: 5240)
			{
				EmpowerLevel = 1,
				Locked = true,
				Marker = 1,
				Skills = [new(Id: 11, TypeId: 52401, Level: 4), new(Id: 12, TypeId: 52402, Level: 3)],
				Masteries = [500111, 500112],
				DoubleAscend = new(Grade: 2, Blessing: 1101),
			},
			new(Id: 12, TypeId: 6010)
			{
				Grade = 5,
				Level = 50,
				InBathhouse = true,
				Skills = [new(Id: 41, TypeId: 60101, Level: 2)],
				Masteries = [],
				DoubleAscend = new(Grade: 1, Blessing: null),
			},
		],
		HeroTypes:
		[
			new(5240, new("l10n:hero-type/name?id=5240#static", "Synthetic Knight (default)"), Rarity: 5, Fraction: 11, FormElements: [4])
			{
				BaseStats = new(Health: 100, Attack: 80, Defence: 90, Speed: 101, Resistance: 40, Accuracy: 10, CriticalChance: 15, CriticalDamage: 50),
			},
			new(6010, new("l10n:hero-type/name?id=6010#static", null), Rarity: 3, Fraction: 1, FormElements: [2, 2]),
		],
		SkillTypes:
		[
			new(52401, new("l10n:skill-type/name?id=52401#static", null)),
			new(52402, new("l10n:skill-type/name?id=52402#static", "Synthetic Guard")),
			new(60101, new("l10n:skill-type/name?id=60101#static", null)),
		],
		StaticDataLocalization: new Dictionary<string, string>
		{
			["l10n:hero-type/name?id=5240#static"] = "Synthetic Knight",
			["l10n:skill-type/name?id=52401#static"] = "Synthetic Strike",
			["l10n:skill-type/name?id=60101#static"] = "Synthetic Volley",
		},
		ClientLocalization: new Dictionary<string, string>
		{
			["l10n:hero-type/name?id=6010#static"] = "Synthetic Archer",
		});

	/// <summary>
	/// <c>BattleStats.Multipliers</c>: for each rank from Stars1, the start and the growth Health, Attack and Defence
	/// scale by. The game holds it as a <c>double[,]</c>; null leaves the static field null.
	/// </summary>
	public double[,]? RankMultipliers { get; init; } = new[,] { { 1, 2 }, { 1.5, 1.8 }, { 2.25, 1.6 }, { 3, 1.5 }, { 4, 1.5 }, { 5.5, 1.5 } };

	/// <summary>
	/// The static data's mastery types: a mastery made up to add 75 Attack, one like Lore of Steel, made up to add 15% of
	/// its set bonuses, and one that raises no stat.
	/// </summary>
	public IReadOnlyList<SyntheticMasteryType> MasteryTypes { get; init; } =
	[
		new(500111, new(Stat: 2, Value: SyntheticArtifactData.Q32(75), Absolute: true)),
		new(500112, new(Stat: -1, Value: SyntheticArtifactData.Q32(0.15), Absolute: false)),
		new(500113, null),
	];

	/// <summary><c>BattleStats.ToPercent</c>, which turns the critical stats the game holds as whole percents into fractions.</summary>
	public double ToPercent { get; init; } = 100;
}

/// <summary>A hero, as SharedModel.Meta.Heroes.Hero holds it. Null masteries or double ascend data means the hero has none.</summary>
public sealed record SyntheticHero(int Id, int TypeId)
{
	public int Grade { get; init; } = 6;

	public int Level { get; init; } = 60;

	public int EmpowerLevel { get; init; }

	public bool Locked { get; init; }

	public bool InStorage { get; init; }

	public bool InBathhouse { get; init; }

	public int Marker { get; init; }

	public IReadOnlyList<SyntheticSkill> Skills { get; init; } = [];

	public IReadOnlyList<int>? Masteries { get; init; }

	public SyntheticDoubleAscend? DoubleAscend { get; init; }
}

public sealed record SyntheticSkill(int Id, int TypeId, int Level);

/// <summary>A mastery type, and its entry in MasteryBonusById, or null when it raises no stat. A stat kind of -1 marks a bonus to set bonuses.</summary>
public sealed record SyntheticMasteryType(int Id, SyntheticStatBonus? Bonus);

/// <summary>A hero's HeroDoubleAscendData: its grade, and its blessing or null when it has none.</summary>
public sealed record SyntheticDoubleAscend(int Grade, int? Blessing);

/// <summary>
/// A HeroType: its name, rarity and fraction, and the element of each of its forms. Its first form holds
/// <see cref="BaseStats"/>, or no stats when it is null; every other form holds <see cref="SyntheticBattleStats.OtherForm"/>,
/// so a reader that took another form's would be seen to.
/// </summary>
public sealed record SyntheticHeroType(int Id, SyntheticText Name, int Rarity, int Fraction, IReadOnlyList<int> FormElements)
{
	public SyntheticBattleStats? BaseStats { get; init; } = SyntheticBattleStats.Default;
}

/// <summary>
/// A form's <c>BaseStats</c>, a BattleStats, in the game's own units: points, and whole percents for the critical
/// stats, so 15 is 15%. Each is held as a Fixed.
/// </summary>
public sealed record SyntheticBattleStats(
	double Health,
	double Attack,
	double Defence,
	double Speed,
	double Resistance,
	double Accuracy,
	double CriticalChance,
	double CriticalDamage)
{
	public static SyntheticBattleStats Default { get; } = new(Health: 70, Attack: 110, Defence: 60, Speed: 98, Resistance: 30, Accuracy: 0, CriticalChance: 15, CriticalDamage: 57);

	public static SyntheticBattleStats OtherForm { get; } = new(Health: 999, Attack: 999, Defence: 999, Speed: 999, Resistance: 999, Accuracy: 999, CriticalChance: 99, CriticalDamage: 99);

	/// <summary>The two stats a BattleStats holds beyond the eight, which no Stat source in a Snapshot shows.</summary>
	public double CriticalHeal { get; init; }

	public double IgnoreDefence { get; init; }
}

public sealed record SyntheticSkillType(int Id, SyntheticText Name);

/// <summary>A SharedLTextKey: the key the localization tables are looked up by, and the default text.</summary>
public sealed record SyntheticText(string Key, string? DefaultValue);
