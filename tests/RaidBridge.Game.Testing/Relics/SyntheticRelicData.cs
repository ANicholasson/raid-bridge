using RaidBridge.Game.Testing.Artifacts;
using RaidBridge.Game.Testing.Champions;

namespace RaidBridge.Game.Testing.Relics;

/// <summary>
/// The Relics a synthetic Game client holds, in the game's own terms: each Relic and Relic stone the Account owns, the
/// Relics each hero wears by its id, and the static data that types them and gives their bonuses. Enum fields hold the
/// game's numbers, such as 2 for Rank2, and Fixed values their raw Q32.32 longs. Every id and name is made up
/// (https://github.com/ANicholasson/raid-bridge/issues/8).
/// </summary>
public sealed record SyntheticRelicData(
	IReadOnlyList<SyntheticRelic> Relics,
	IReadOnlyList<SyntheticRelicStone> Stones,
	IReadOnlyDictionary<int, IReadOnlyList<int>> RelicIdsByHeroId,
	IReadOnlyList<SyntheticRelicType> RelicTypes,
	IReadOnlyList<SyntheticRelicStoneType> StoneTypes,
	IReadOnlyDictionary<int, IReadOnlyList<SyntheticBonusValue>> StatValueByLevel,
	IReadOnlyList<int> OpeningLevels,
	IReadOnlyDictionary<int, int> SkillLevelByRelicLevel)
{
	/// <summary>
	/// The knight (hero 3, a Dark Elf) wears two Relics: a grail at level 9, whose third stat is still locked, with a
	/// stone in its socket, and a harp at level 15, with one socket empty and one holding a stone. The Vault archer
	/// (hero 7) has no entry, and the Deep Vault archer (hero 12) an empty list. Nobody wears Relic 203, a harp at level
	/// 0, whose sockets are empty, and no socket holds stones 305 and 303, listed out of order. The harp's type adds
	/// skills for the High Elves and the Dark Elves. The skill levels rise at Relic levels 0, 6, 9, 12, 15 and 18, held out
	/// of order.
	/// </summary>
	public static SyntheticRelicData Default { get; } = new(
		Relics:
		[
			new(Id: 201, TypeId: 1) { Rank = 2, Level = 9, Sockets = [new(Shape: 1, StoneId: 301)] },
			new(Id: 203, TypeId: 2) { Rank = 3, Level = 0, Sockets = [new(Shape: 4, StoneId: null), new(Shape: 5, StoneId: null)] },
			new(Id: 202, TypeId: 2) { Rank = 6, Level = 15, Sockets = [new(Shape: 4, StoneId: null), new(Shape: 5, StoneId: 302)] },
		],
		Stones: [new(301, 11), new(305, 11), new(302, 12), new(303, 11)],
		RelicIdsByHeroId: new Dictionary<int, IReadOnlyList<int>> { [12] = [], [3] = [202, 201] },
		RelicTypes:
		[
			new(1, new("l10n:relic/name?id=1#static", "Synthetic Grail"), new("l10n:relic/description?id=1#static", "Wearer deals <color=#1ee600>2%</color> more damage."), Rarity: 3, Stats: [2, 1, 8], SkillIds: [3000000]),
			new(2, new("l10n:relic/name?id=2#static", "Synthetic Harp"), new("l10n:relic/description?id=2#static", "Wearer gains Turn Meter."), Rarity: 5, Stats: [1, 5, 4], SkillIds: [3000001])
			{
				FactionSkills =
				[
					new(Fraction: 2, SkillIds: [3100001], new("l10n:relic-faction/description?id=2-2#static", "High Elves heal more.")),
					new(Fraction: 11, SkillIds: [3200001], new("l10n:relic-faction/description?id=2-11#static", "Dark Elves strike first.")),
				],
			},
		],
		StoneTypes:
		[
			new(11, new("l10n:relic-stone/name?id=11#static", "Synthetic Spark"), new("l10n:relic-stone/description?id=11#static", "Turn Meter effects are stronger."), Rarity: 3, Shape: 1, SkillIds: [4000010]),
			new(12, new("l10n:relic-stone/name?id=12#static", "Synthetic Star"), new("l10n:relic-stone/description?id=12#static", "Speed rises with each buff."), Rarity: 5, Shape: 5, SkillIds: [4000011]),
		],
		StatValueByLevel: new Dictionary<int, IReadOnlyList<SyntheticBonusValue>>
		{
			[1] = Levels(level => new(SyntheticArtifactData.Q32(900 + (300 * level)), Absolute: true)),
			[2] = Levels(level => new(SyntheticArtifactData.Q32(53 + (17 * level)), Absolute: true)),
			[4] = Levels(level => new(SyntheticArtifactData.Q32(3 + level), Absolute: true)),
			[5] = Levels(level => new(SyntheticArtifactData.Q32(16 + (4 * level)), Absolute: true)),
			[8] = Levels(level => new(SyntheticArtifactData.Q32(0.06 + (0.02 * level)), Absolute: false)),
		},
		OpeningLevels: [0, 9, 15],
		SkillLevelByRelicLevel: new Dictionary<int, int> { [9] = 3, [0] = 1, [15] = 5, [6] = 2, [12] = 4, [18] = 6 });

	/// <summary>Relics the Account doesn't hold at all, and no stone, as an Account that never got one.</summary>
	public static SyntheticRelicData None { get; } = Default with { Relics = [], Stones = [], RelicIdsByHeroId = new Dictionary<int, IReadOnlyList<int>>() };

	/// <summary>A value for each level from 0 to 18, as the game's static table holds them.</summary>
	private static SyntheticBonusValue[] Levels(Func<int, SyntheticBonusValue> value) => [.. Enumerable.Range(0, 19).Select(value)];
}

/// <summary>A Relic, as SharedModel.Meta.Relics.UserData.Relic holds it.</summary>
public sealed record SyntheticRelic(int Id, int TypeId)
{
	public int Rank { get; init; } = 1;

	public int Level { get; init; }

	public IReadOnlyList<SyntheticRelicSocket> Sockets { get; init; } = [];
}

/// <summary>A RelicStoneSocket: its shape, and the id of the stone in it, or null when it is empty.</summary>
public sealed record SyntheticRelicSocket(int Shape, int? StoneId);

/// <summary>A Relic stone, as SharedModel.Meta.Relics.UserData.RelicStone holds it.</summary>
public sealed record SyntheticRelicStone(int Id, int TypeId);

/// <summary>A RelicType: its id, name, description, rarity, the stats it raises, its own skills, and its Faction skills, or null for none, as most types hold it.</summary>
public sealed record SyntheticRelicType(int Id, SyntheticText Name, SyntheticText Description, int Rarity, IReadOnlyList<int> Stats, IReadOnlyList<int> SkillIds)
{
	public IReadOnlyList<SyntheticFactionSkills>? FactionSkills { get; init; }
}

/// <summary>A RelicFactionSkillBonus: the Faction, by its HeroFraction, the skills it adds, and its description.</summary>
public sealed record SyntheticFactionSkills(int Fraction, IReadOnlyList<int> SkillIds, SyntheticText Description);

/// <summary>A RelicStoneType: its id, name, description, rarity, shape and skills.</summary>
public sealed record SyntheticRelicStoneType(int Id, SyntheticText Name, SyntheticText Description, int Rarity, int Shape, IReadOnlyList<int> SkillIds);

/// <summary>A BonusValue: its value as a raw Fixed long, and whether it is absolute.</summary>
public sealed record SyntheticBonusValue(long Value, bool Absolute);
