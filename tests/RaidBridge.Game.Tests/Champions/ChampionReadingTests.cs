using RaidBridge.Domain.Champions;
using RaidBridge.Domain.Stats;
using RaidBridge.Game.Testing;
using RaidBridge.Game.Testing.Arena;
using RaidBridge.Game.Testing.Artifacts;
using RaidBridge.Game.Testing.Champions;
using RaidBridge.Game.Testing.FactionGuardians;
using RaidBridge.Game.Testing.Relics;

namespace RaidBridge.Game.Tests.Champions;

/// <summary>
/// The Champions read end to end through the fake memory port: a synthetic Game client in, the Account's Champions
/// or a named refusal out (spec #20, #25). Nothing here looks at the reader's internals.
/// </summary>
public sealed class ChampionReadingTests : IDisposable
{
	private static readonly SyntheticHeroData Heroes = SyntheticHeroData.Default;

	private readonly SyntheticGameClient _game = SyntheticGameClient.Create();

	private static readonly ChampionStats NoStats = new(new Dictionary<Stat, double>(), new Dictionary<Stat, double>(), new Dictionary<Stat, double>(), new Dictionary<Stat, double>());

	/// <summary>
	/// HeroById holds a slot a removal freed, which is skipped. The names come from StaticDataLocalization (the
	/// knight, strike and volley), ClientLocalization (the archer) and a key's default text (the guard). The Artifacts
	/// and the Relics they wear are ArtifactReadingTests' and RelicReadingTests' concern, and the stats
	/// BaseStatsReadingTests'.
	/// </summary>
	[Fact]
	public void Reads_every_champion_with_its_names_and_location()
	{
		var champions = _game.Source().Read().Champions;

		Assert.Equivalent(
			new Champion[]
			{
				new(3, 5240, "Synthetic Knight", "Legendary", "Void", "DarkElves", "Stars6", 60, "Stars2", "Necromancy", 1, [new(52401, "Synthetic Strike", 4), new(52402, "Synthetic Guard", 3)], [500111, 500112], true, "Favorite", ChampionLocation.Roster, [], [], NoStats),
				new(7, 6010, "Synthetic Archer", "Rare", "Force", "BannerLords", "Stars3", 30, null, null, 0, [new(60101, "Synthetic Volley", 1)], [], false, "None", ChampionLocation.Vault, [], [], NoStats),
				new(12, 6010, "Synthetic Archer", "Rare", "Force", "BannerLords", "Stars5", 50, "Stars1", null, 0, [new(60101, "Synthetic Volley", 2)], [], false, "None", ChampionLocation.DeepVault, [], [], NoStats),
			},
			champions.OrderBy(champion => champion.Id).Select(champion => champion with { Artifacts = [], Relics = [], Stats = NoStats }),
			strict: true);
	}

	[Fact]
	public void Reads_an_account_without_champions()
	{
		using var game = SyntheticGameClient.Create(
			heroData: Heroes with { Heroes = [] },
			artifactData: SyntheticArtifactData.Default with { SlotsByHeroId = new Dictionary<int, IReadOnlyDictionary<int, int>>() },
			relicData: SyntheticRelicData.Default with { RelicIdsByHeroId = new Dictionary<int, IReadOnlyList<int>>() },
			guardianData: SyntheticGuardianData.None,
			arenaData: SyntheticArenaData.None);

		Assert.Empty(game.Source().Read().Champions);
	}

	[Fact]
	public void Refuses_a_champion_whose_name_is_in_no_table_and_has_no_default_text()
	{
		using var game = SyntheticGameClient.Create(heroData: Heroes with { ClientLocalization = new Dictionary<string, string>() });

		RefusesMatching(
			game,
			@"^Champion type 6010 has no name: its SharedModel\.Common\.Localization\.SharedLTextKey at 0x[0-9A-F]+ has the key ""l10n:hero-type/name\?id=6010#static"", which isn't in the Game client's localization tables, and no default text\.$");
	}

	/// <summary>A table that holds a key with no text doesn't hide the next table's text for it (code review, #25).</summary>
	[Fact]
	public void Takes_the_client_table_name_when_the_static_data_table_holds_the_key_empty()
	{
		using var game = SyntheticGameClient.Create(heroData: Heroes with
		{
			StaticDataLocalization = new Dictionary<string, string>(Heroes.StaticDataLocalization) { ["l10n:hero-type/name?id=6010#static"] = string.Empty },
		});

		Assert.All(game.Source().Read().Champions.Where(champion => champion.TypeId == 6010), champion => Assert.Equal("Synthetic Archer", champion.Name));
	}

	[Fact]
	public void Refuses_a_skill_whose_name_is_in_no_table_and_has_no_default_text()
	{
		using var game = SyntheticGameClient.Create(heroData: Heroes with { StaticDataLocalization = new Dictionary<string, string>(Heroes.StaticDataLocalization.Where(text => !text.Key.Contains("60101", StringComparison.Ordinal))) });

		RefusesMatching(game, @"^Skill type 60101 has no name: its SharedModel\.Common\.Localization\.SharedLTextKey at 0x[0-9A-F]+ has the key ""l10n:skill-type/name\?id=60101#static"", ");
	}

	[Fact]
	public void Refuses_a_hero_by_id_whose_count_runs_past_its_slots()
	{
		_game.SetHeroByIdCounts(count: 10, freeCount: 1);

		RefusesMatching(_game, @"^SharedModel\.Meta\.Heroes\.UserHeroData\.HeroById at 0x[0-9A-F]+ is malformed: its _count is 10 and its _freeCount 1, with 5 slots\.$");
	}

	[Fact]
	public void Refuses_a_hero_by_id_whose_free_count_misses_a_freed_slot()
	{
		_game.SetHeroByIdCounts(count: 4, freeCount: 0);

		RefusesMatching(_game, @"^SharedModel\.Meta\.Heroes\.UserHeroData\.HeroById at 0x[0-9A-F]+ is malformed: 3 of its slots are in use, but its _count of 4 less its _freeCount of 0 is 4\.$");
	}

	[Fact]
	public void Refuses_a_skill_list_whose_size_runs_past_its_array()
	{
		_game.SetSkillCount(heroId: 3, count: 9);

		RefusesMatching(_game, @"^SharedModel\.Meta\.Heroes\.Hero\.Skills at 0x[0-9A-F]+ is malformed: its _size is 9, but its array holds 3\.$");
	}

	[Fact]
	public void Refuses_an_enum_value_the_game_does_not_name()
	{
		_game.RemoveEnumMember("SharedModel.Meta.Heroes.HeroFraction", "BannerLords");

		RefusesMatching(_game, @"^SharedModel\.Meta\.Heroes\.HeroType\.Fraction at 0x[0-9A-F]+ is 1, which isn't a member of the game's SharedModel\.Meta\.Heroes\.HeroFraction\.$");
	}

	[Fact]
	public void Refuses_a_champion_in_both_the_vault_and_the_deep_vault()
	{
		using var game = SyntheticGameClient.Create(heroData: Heroes with { Heroes = [new SyntheticHero(7, 6010) { InStorage = true, InBathhouse = true }] });

		RefusesMatching(game, @"^Champion 7 is in both the Vault and the Deep Vault: SharedModel\.Meta\.Heroes\.Hero\.InStorage at 0x[0-9A-F]+ is True, and SharedModel\.Meta\.Heroes\.Hero\.InBathhouse at 0x[0-9A-F]+ is True\.$");
	}

	[Fact]
	public void Refuses_a_champion_type_whose_forms_disagree_on_its_affinity()
	{
		using var game = SyntheticGameClient.Create(heroData: Heroes with { HeroTypes = [.. Heroes.HeroTypes.Select(type => type.Id == 6010 ? type with { FormElements = [2, 3] } : type)] });

		RefusesMatching(game, @"^SharedModel\.Meta\.Heroes\.HeroType\.Forms of Champion type 6010 points to forms at 0x[0-9A-F]+ that give the Affinities Force and Spirit, where raid-bridge expects one\.$");
	}

	[Fact]
	public void Refuses_a_champion_whose_type_the_static_data_lacks()
	{
		using var game = SyntheticGameClient.Create(heroData: Heroes with { Heroes = [new SyntheticHero(7, 9999)] });

		RefusesMatching(game, @"^SharedModel\.Meta\.Heroes\.Hero\.TypeId at 0x[0-9A-F]+ is 9999, which isn't a type in the game's StaticHeroData\.HeroTypeById\.$");
	}

	[Fact]
	public void Refuses_a_hero_type_kept_under_another_id()
	{
		_game.SetHeroTypeId(typeId: 6010, id: 6011);

		RefusesMatching(_game, @"^SharedModel\.Meta\.Heroes\.HeroType\.Id at 0x[0-9A-F]+ is 6011, where raid-bridge expects the id HeroTypeById keeps it under, 6010\.$");
	}

	[Fact]
	public void Refuses_a_skill_type_kept_under_another_id()
	{
		_game.SetSkillTypeId(typeId: 60101, id: 60102);

		RefusesMatching(_game, @"^SharedModel\.Meta\.Skills\.SkillType\.Id at 0x[0-9A-F]+ is 60102, where raid-bridge expects the id _skillTypeById keeps it under, 60101\.$");
	}

	[Fact]
	public void Refuses_a_champion_without_a_level()
	{
		using var game = SyntheticGameClient.Create(heroData: Heroes with { Heroes = [new SyntheticHero(7, 6010) { Level = 0 }] });

		RefusesMatching(game, @"^SharedModel\.Meta\.Heroes\.Hero\.Level at 0x[0-9A-F]+ is 0, where raid-bridge expects a positive level\.$");
	}

	/// <summary>A corrupt chain of declaring classes is refused by name, not followed until the stack overflows (code review, #25).</summary>
	[Fact]
	public void Refuses_a_runtime_class_nested_in_itself()
	{
		_game.SetDeclaringType("SharedModel.Meta.Heroes.Hero", _game.RuntimeClass("SharedModel.Meta.Heroes.Hero"));

		RefusesMatching(_game, @"The runtime class 0x[0-9A-F]+ is nested more than 16 classes deep\.$");
	}

	/// <summary>A value of a dictionary is named by the dictionary and its key (#38).</summary>
	[Fact]
	public void Refuses_a_champion_that_is_not_an_object_naming_its_id()
	{
		_game.PointHeroByIdValue(slot: 0, SyntheticGameClient.Unmapped);

		RefusesMatching(_game, @"^SharedModel\.Meta\.Heroes\.UserHeroData\.HeroById\[\d+\]: System\.Collections\.Generic\.Dictionary`2\+Entry\.value at 0x[0-9A-F]+ points to 0xDEAD00000000, which isn't an object\. ");
	}

	[Fact]
	public void Refuses_an_array_whose_element_class_has_base_classes_it_cannot_read()
	{
		_game.SetBaseClass("System.Collections.Generic.Dictionary`2+Entry", SyntheticGameClient.Unmapped);

		RefusesMatching(_game, @"^\S+ points to an array at 0x[0-9A-F]+ of System\.Collections\.Generic\.Dictionary`2\+Entry, whose base classes can't be read\. Can't read the name of the runtime class 0xDEAD00000000 at 0xDEAD[0-9A-F]+: Nothing is mapped at 0xDEAD[0-9A-F]+ for 8 bytes\.$");
	}

	/// <summary>A nested class is known by its declaring class's name and its own, as the metadata names it (#38).</summary>
	[Fact]
	public void Refuses_a_dictionary_entry_class_that_is_not_nested_in_the_dictionary()
	{
		_game.SetDeclaringType("System.Collections.Generic.Dictionary`2+Entry", 0);

		RefusesMatching(_game, @"^System\.Collections\.Generic\.Dictionary`2\._entries points to an array at 0x[0-9A-F]+ of Entry, 24 bytes each, where raid-bridge expects System\.Collections\.Generic\.Dictionary`2\+Entry\.$");
	}

	public void Dispose() => _game.Dispose();

	private static void Refuses(SyntheticGameClient game, string message) =>
		Assert.Equal(message, Assert.ThrowsAny<GameClientException>(() => game.Source().Read()).Message);

	private static void RefusesMatching(SyntheticGameClient game, string pattern) =>
		Assert.Matches(pattern, Assert.ThrowsAny<GameClientException>(() => game.Source().Read()).Message);
}
