namespace RaidBridge.Game.Testing.Champions;

/// <summary>
/// The heroes a synthetic Game client holds, in the game's own terms, with the static data that names them: hero and
/// skill types, and the two localization tables. Enum fields hold the game's numbers, such as 6 for Stars6. Every
/// id and name is made up (spec #20).
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
			new(5240, new("l10n:hero-type/name?id=5240#static", "Synthetic Knight (default)"), Rarity: 5, Fraction: 11, FormElements: [4]),
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

/// <summary>A hero's HeroDoubleAscendData: its grade, and its blessing or null when it has none.</summary>
public sealed record SyntheticDoubleAscend(int Grade, int? Blessing);

/// <summary>A HeroType: its name, rarity and fraction, and the element of each of its forms.</summary>
public sealed record SyntheticHeroType(int Id, SyntheticText Name, int Rarity, int Fraction, IReadOnlyList<int> FormElements);

public sealed record SyntheticSkillType(int Id, SyntheticText Name);

/// <summary>A SharedLTextKey: the key the localization tables are looked up by, and the default text.</summary>
public sealed record SyntheticText(string Key, string? DefaultValue);
