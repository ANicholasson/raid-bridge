using RaidBridge.Domain.Relics;
using RaidBridge.Game.Testing;
using RaidBridge.Game.Testing.Artifacts;
using RaidBridge.Game.Testing.Relics;

namespace RaidBridge.Game.Tests.Relics;

/// <summary>
/// The Account's Relics and Relic stones, read end to end through the fake memory port: a synthetic Game client in,
/// each Champion's Relics, the unworn Relics and the unsocketed stones, or a named refusal, out
/// (https://github.com/ANicholasson/raid-bridge/issues/8). Nothing here looks at the reader's internals.
/// </summary>
public sealed class RelicReadingTests : IDisposable
{
	private static readonly SyntheticRelicData Relics = SyntheticRelicData.Default;

	private static readonly RelicStone Spark301 = new(301, 11, "Synthetic Spark", "Rare", "Circle", [new(4000010, "Turn Meter effects are stronger.")]);
	private static readonly RelicStone Star302 = new(302, 12, "Synthetic Star", "Legendary", "Pentagon", [new(4000011, "Speed rises with each buff.")]);

	/// <summary>The skills the harp's type adds for a wearer of each Faction it lists, in its order.</summary>
	private static readonly RelicFactionSkill[] HarpFactionSkills = [new("HighElves", 3100001, "High Elves heal more."), new("DarkElves", 3200001, "Dark Elves strike first.")];

	/// <summary>
	/// The knight's Relics, in the order the game lists them: the harp at level 15, with every stat open, and the grail
	/// at level 9, whose CriticalDamage opens only at 15, its description without the game's colour tags.
	/// </summary>
	private static readonly Relic[] KnightWears =
	[
		new(202, 2, "Synthetic Harp", "Legendary", "Rank6", 15, 5, [new("Health", 5400, true), new("Resistance", 76, true), new("Speed", 18, true)], [new(3000001, "Wearer gains Turn Meter.")], HarpFactionSkills, [new("Diamond", null), new("Pentagon", Star302)]),
		new(201, 1, "Synthetic Grail", "Rare", "Rank2", 9, 3, [new("Attack", 206, true), new("Health", 3600, true)], [new(3000000, "Wearer deals 2% more damage.")], [], [new("Circle", Spark301)]),
	];

	/// <summary>A harp nobody wears, at level 0: only its first stat is open, and it lists every Faction's skills all the same.</summary>
	private static readonly Relic UnwornHarp =
		new(203, 2, "Synthetic Harp", "Legendary", "Rank3", 0, 1, [new("Health", 900, true)], [new(3000001, "Wearer gains Turn Meter.")], HarpFactionSkills, [new("Diamond", null), new("Pentagon", null)]);

	private readonly SyntheticGameClient _game = SyntheticGameClient.Create();

	[Fact]
	public void Reads_the_relics_each_champion_wears()
	{
		var relics = _game.Source().Read().Champions.ToDictionary(champion => champion.Id, champion => champion.Relics);

		Assert.Equivalent(KnightWears, relics[3], strict: true);
	}

	/// <summary>The Vault archer has no entry in RelicDataByHeroId, and the Deep Vault archer an empty list.</summary>
	[Fact]
	public void Reads_no_relics_for_a_champion_that_wears_none()
	{
		var relics = _game.Source().Read().Champions.ToDictionary(champion => champion.Id, champion => champion.Relics);

		Assert.Empty(relics[7]);
		Assert.Empty(relics[12]);
	}

	[Fact]
	public void Reads_the_relics_no_champion_wears()
	{
		Assert.Equivalent(new[] { UnwornHarp }, _game.Source().Read().UnwornRelics, strict: true);
	}

	[Fact]
	public void Reads_the_stones_in_no_socket_sorted_by_id()
	{
		Assert.Equivalent(new[] { Spark301 with { Id = 303 }, Spark301 with { Id = 305 } }, _game.Source().Read().UnsocketedRelicStones, strict: true);
	}

	[Fact]
	public void Reads_every_relic_and_every_stone_exactly_once()
	{
		var account = _game.Source().Read();
		var relics = account.Champions.SelectMany(champion => champion.Relics).Concat(account.UnwornRelics).ToList();

		Assert.Equal([201, 202, 203], relics.Select(relic => relic.Id).Order());
		Assert.Equal(
			[301, 302, 303, 305],
			relics.SelectMany(relic => relic.Sockets).Select(socket => socket.Stone).OfType<RelicStone>().Concat(account.UnsocketedRelicStones).Select(stone => stone.Id).Order());
	}

	[Fact]
	public void Reads_every_relic_as_unworn_when_no_champion_wears_one()
	{
		using var game = SyntheticGameClient.Create(relicData: Relics with { RelicIdsByHeroId = new Dictionary<int, IReadOnlyList<int>>() });

		var account = game.Source().Read();

		Assert.All(account.Champions, champion => Assert.Empty(champion.Relics));
		Assert.Equal([201, 202, 203], account.UnwornRelics.Select(relic => relic.Id));
	}

	/// <summary>Most Relic types hold null for their Faction skills (live check, https://github.com/ANicholasson/raid-bridge/issues/8).</summary>
	[Fact]
	public void Reads_a_relic_whose_type_has_no_faction_skills_with_none()
	{
		using var game = SyntheticGameClient.Create(relicData: Relics with { RelicTypes = [.. Relics.RelicTypes.Select(type => type with { FactionSkills = null })] });

		Assert.All(game.Source().Read().UnwornRelics, relic => Assert.Empty(relic.FactionSkills));
	}

	/// <summary>A skill level rises at the Relic levels the table keys it by, so a level between two keys takes the lower one's.</summary>
	[Theory]
	[InlineData(0, 1)]
	[InlineData(5, 1)]
	[InlineData(8, 2)]
	[InlineData(18, 6)]
	public void Reads_the_skill_level_of_the_highest_step_the_relic_has_reached(int level, int skillLevel)
	{
		using var game = SyntheticGameClient.Create(relicData: Relics with
		{
			Relics = [.. Relics.Relics.Select(relic => relic.Id == 203 ? relic with { Level = level } : relic)],
		});

		Assert.Equal(skillLevel, game.Source().Read().UnwornRelics.Single().SkillLevel);
	}

	/// <summary>A percentage is held as a Fixed fraction, written exactly as the static table holds it.</summary>
	[Fact]
	public void Reads_a_percentage_bonus_as_the_fraction_the_game_holds()
	{
		using var game = SyntheticGameClient.Create(relicData: Relics with
		{
			Relics = [.. Relics.Relics.Select(relic => relic.Id == 201 ? relic with { Level = 15 } : relic)],
		});

		var grail = game.Source().Read().Champions.Single(champion => champion.Id == 3).Relics.Single(relic => relic.Id == 201);

		Assert.Equal(new RelicBonus("CriticalDamage", SyntheticArtifactData.Q32(0.36) / 4294967296.0, false), grail.Bonuses[2]);
	}

	[Fact]
	public void Reads_an_account_with_no_relics_without_the_static_relic_data()
	{
		using var game = SyntheticGameClient.Create(relicData: SyntheticRelicData.None);
		game.PointField("SharedModel.Meta.StaticData", "RelicData", SyntheticGameClient.Unmapped);

		var account = game.Source().Read();

		Assert.Empty(account.UnwornRelics);
		Assert.Empty(account.UnsocketedRelicStones);
		Assert.All(account.Champions, champion => Assert.Empty(champion.Relics));
	}

	[Fact]
	public void Refuses_relics_worn_by_a_champion_the_account_lacks()
	{
		using var game = SyntheticGameClient.Create(relicData: Relics with { RelicIdsByHeroId = new Dictionary<int, IReadOnlyList<int>> { [99] = [201] } });

		Refuses(game, "SharedModel.Meta.Relics.UserData.UserRelicData.RelicDataByHeroId gives Relics to Champion 99, which HeroById doesn't hold.");
	}

	[Fact]
	public void Refuses_a_champion_wearing_a_relic_the_account_lacks()
	{
		using var game = SyntheticGameClient.Create(relicData: Relics with { RelicIdsByHeroId = new Dictionary<int, IReadOnlyList<int>> { [3] = [999] } });

		RefusesMatching(
			game,
			@"^SharedModel\.Meta\.Relics\.UserData\.UserRelicData\.RelicDataByHeroId gives Champion 3 Relic 999, but UserRelicData\.Relics has no Relic 999: System\.Collections\.Generic\.List`1\._items\[0\] at 0x[0-9A-F]+ is 999\.$");
	}

	/// <summary>One Relic under two Champions would be written twice; the research found none (docs/research/2026-11-relics.md).</summary>
	[Fact]
	public void Refuses_a_relic_worn_by_two_champions()
	{
		using var game = SyntheticGameClient.Create(relicData: Relics with { RelicIdsByHeroId = new Dictionary<int, IReadOnlyList<int>> { [7] = [201], [3] = [201] } });

		Refuses(game, "SharedModel.Meta.Relics.UserData.UserRelicData.RelicDataByHeroId gives Relic 201 to both Champion 3 and Champion 7.");
	}

	[Fact]
	public void Refuses_a_socket_naming_a_stone_the_account_lacks()
	{
		using var game = SyntheticGameClient.Create(relicData: Relics with
		{
			Relics = [.. Relics.Relics.Select(relic => relic.Id == 203 ? relic with { Sockets = [new(Shape: 1, StoneId: 999)] } : relic)],
		});

		RefusesMatching(
			game,
			@"^A socket of Relic 203 holds Relic stone 999, but UserRelicData\.Stones has no Relic stone 999: SharedModel\.Meta\.Relics\.UserData\.RelicStoneSocket\.StoneId at 0x[0-9A-F]+ is 999\.$");
	}

	[Fact]
	public void Refuses_a_stone_in_two_sockets()
	{
		using var game = SyntheticGameClient.Create(relicData: Relics with
		{
			Relics = [.. Relics.Relics.Select(relic => relic.Id == 203 ? relic with { Sockets = [new(Shape: 1, StoneId: 301)] } : relic)],
		});

		RefusesMatching(game, @"^Relic stone 301 is in a socket of Relic 201 and one of Relic 203: SharedModel\.Meta\.Relics\.UserData\.RelicStoneSocket\.StoneId at 0x[0-9A-F]+ is 301\.$");
	}

	/// <summary>Every socketed stone fits its socket on the Account read (live check, docs/research/2026-11-relics.md).</summary>
	[Fact]
	public void Refuses_a_stone_in_a_socket_of_another_shape()
	{
		using var game = SyntheticGameClient.Create(relicData: Relics with
		{
			Relics = [.. Relics.Relics.Select(relic => relic.Id == 203 ? relic with { Sockets = [new(Shape: 4, StoneId: 303)] } : relic)],
		});

		RefusesMatching(game, @"^A Diamond socket of Relic 203 holds Relic stone 303, which is a Circle: SharedModel\.Meta\.Relics\.UserData\.RelicStoneSocket\.StoneId at 0x[0-9A-F]+ is 303\.$");
	}

	[Fact]
	public void Refuses_a_relic_level_that_does_not_parse()
	{
		using var game = SyntheticGameClient.Create(relicData: Relics with
		{
			Relics = [.. Relics.Relics.Select(relic => relic.Id == 203 ? relic with { Level = -1 } : relic)],
		});

		RefusesMatching(game, @"^SharedModel\.Meta\.Relics\.UserData\.Relic\.Level at 0x[0-9A-F]+ is -1, where raid-bridge expects a level of 0 or more\.$");
	}

	[Fact]
	public void Refuses_a_relic_rank_the_game_does_not_name()
	{
		using var game = SyntheticGameClient.Create(relicData: Relics with
		{
			Relics = [.. Relics.Relics.Select(relic => relic.Id == 203 ? relic with { Rank = 0 } : relic)],
		});

		RefusesMatching(game, @"^SharedModel\.Meta\.Relics\.UserData\.Relic\.Rank at 0x[0-9A-F]+ is 0, which isn't a member of the game's SharedModel\.Meta\.Relics\.StaticData\.RelicRank\.$");
	}

	[Fact]
	public void Refuses_a_relic_level_past_the_static_table()
	{
		using var game = SyntheticGameClient.Create(relicData: Relics with
		{
			Relics = [.. Relics.Relics.Select(relic => relic.Id == 203 ? relic with { Level = 19 } : relic)],
		});

		RefusesMatching(
			game,
			@"^SharedModel\.Meta\.Relics\.UserData\.Relic\.Level at 0x[0-9A-F]+ is 19, but SharedModel\.Meta\.Relics\.StaticData\.StaticRelicData\.StatValueByLevel\[Health\], which its type 2 raises, holds values for levels 0 to 18\.$");
	}

	[Fact]
	public void Refuses_a_stat_the_static_table_holds_no_values_for()
	{
		using var game = SyntheticGameClient.Create(relicData: Relics with
		{
			StatValueByLevel = Relics.StatValueByLevel.Where(stat => stat.Key != 5).ToDictionary(),
		});

		RefusesMatching(
			game,
			@"^Relic type 2's StatsBonus raises Resistance, which SharedModel\.Meta\.Relics\.StaticData\.StaticRelicData\.StatValueByLevel holds no values for: System\.Collections\.Generic\.List`1\._items\[1\] at 0x[0-9A-F]+\.$");
	}

	[Fact]
	public void Refuses_a_type_raising_more_stats_than_the_static_data_opens()
	{
		using var game = SyntheticGameClient.Create(relicData: Relics with { OpeningLevels = [0, 9] });

		Refuses(game, "Relic type 2 raises 3 stats, but SharedModel.Meta.Relics.StaticData.StaticRelicData.StatBonusOpeningLevels gives an opening level for only 2.");
	}

	[Fact]
	public void Refuses_a_relic_whose_type_the_static_data_lacks()
	{
		using var game = SyntheticGameClient.Create(relicData: Relics with
		{
			Relics = [.. Relics.Relics.Select(relic => relic.Id == 203 ? relic with { TypeId = 77 } : relic)],
		});

		RefusesMatching(game, @"^SharedModel\.Meta\.Relics\.UserData\.Relic\.TypeId at 0x[0-9A-F]+ is 77, which isn't a type in the game's SharedModel\.Meta\.Relics\.StaticData\.StaticRelicData\._relicTypeById\.$");
	}

	[Fact]
	public void Refuses_a_stone_whose_type_the_static_data_lacks()
	{
		using var game = SyntheticGameClient.Create(relicData: Relics with { Stones = [.. Relics.Stones, new(306, 77)] });

		RefusesMatching(game, @"^SharedModel\.Meta\.Relics\.UserData\.RelicStone\.TypeId at 0x[0-9A-F]+ is 77, which isn't a type in the game's SharedModel\.Meta\.Relics\.StaticData\.StaticRelicData\._relicStoneTypeById\.$");
	}

	[Fact]
	public void Refuses_a_relic_id_the_account_holds_twice()
	{
		using var game = SyntheticGameClient.Create(relicData: Relics with { Relics = [.. Relics.Relics, new SyntheticRelic(Id: 201, TypeId: 1)] });

		RefusesMatching(game, @"^SharedModel\.Meta\.Relics\.UserData\.Relic\.Id at 0x[0-9A-F]+ is 201, an id SharedModel\.Meta\.Relics\.UserData\.UserRelicData\.Relics holds twice\.$");
	}

	[Fact]
	public void Refuses_a_faction_its_relic_type_lists_twice()
	{
		using var game = SyntheticGameClient.Create(relicData: Relics with
		{
			RelicTypes = [.. Relics.RelicTypes.Select(type => type.FactionSkills is { } skills ? type with { FactionSkills = [.. skills, skills[1]] } : type)],
		});

		RefusesMatching(game, @"^Relic type 2's SkillIdsByFaction lists DarkElves twice: SharedModel\.Meta\.Relics\.StaticData\.RelicFactionSkillBonus\.FractionId at 0x[0-9A-F]+ is DarkElves\.$");
	}

	[Fact]
	public void Refuses_a_relic_level_below_every_skill_level_step()
	{
		using var game = SyntheticGameClient.Create(relicData: Relics with { SkillLevelByRelicLevel = new Dictionary<int, int> { [6] = 2 } });

		RefusesMatching(
			game,
			@"^SharedModel\.Meta\.Relics\.UserData\.Relic\.Level at 0x[0-9A-F]+ is 0, but SharedModel\.Meta\.Relics\.StaticData\.StaticRelicData\.SkillLevelByRelicLevel gives no skill level at or below it\.$");
	}

	[Fact]
	public void Refuses_a_relic_field_missing_from_the_metadata()
	{
		_game.UndeclareField("SharedModel.Meta.Relics.UserData.Relic", "Rank");

		Refuses(_game, "The game's class SharedModel.Meta.Relics.UserData.Relic has no field Rank, base classes included.");
	}

	public void Dispose() => _game.Dispose();

	private static void Refuses(SyntheticGameClient game, string message) =>
		Assert.Equal(message, Assert.ThrowsAny<GameClientException>(() => game.Source().Read()).Message);

	private static void RefusesMatching(SyntheticGameClient game, string pattern) =>
		Assert.Matches(pattern, Assert.ThrowsAny<GameClientException>(() => game.Source().Read()).Message);
}
