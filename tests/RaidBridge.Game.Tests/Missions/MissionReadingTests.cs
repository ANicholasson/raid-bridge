using RaidBridge.Domain.Missions;
using RaidBridge.Game.Testing;
using RaidBridge.Game.Testing.Missions;
using static RaidBridge.Game.Testing.Missions.SyntheticMissionData;

namespace RaidBridge.Game.Tests.Missions;

/// <summary>
/// The Account's Missions, read end to end through the fake memory port: a synthetic Game client in, each open group's
/// Missions under the game's own names in the game's order, or a named refusal, out
/// (https://github.com/ANicholasson/raid-bridge/issues/33). Nothing here looks at the reader's internals.
/// </summary>
public sealed class MissionReadingTests : IDisposable
{
	private readonly SyntheticGameClient _game = SyntheticGameClient.Create();

	/// <summary>
	/// The game holds the Missions out of order; they come out in the order of their weight, in their group and its
	/// section. The daily quest beside them isn't a Mission and is left out, and a count of -1, which the game works out
	/// from the Account, is written as null.
	/// </summary>
	[Fact]
	public void Reads_the_open_group_in_the_game_order()
	{
		var missions = _game.Source().Read().Missions;

		Assert.Equal(["Group1"], missions.Completed);
		var group = Assert.Single(missions.Open);
		Assert.Equal(("Group2", "ArbiterSection"), (group.Group, group.Section));
		Assert.Equal(
			[
				new Mission(9101, "Heroes", "Completed", "Upgrade 2 Champions to Level 30", null, 2),
				new Mission(9102, "Dungeons", "InProgress", "Clear Stage 10 of a Synthetic Keep 10 times", 4, 10),
				new Mission(9103, "Clan", "New", "Deal 2,000,000 damage to the Synthetic Boss", 0, 2_000_000),
			],
			group.Missions);
	}

	/// <summary>Damage is counted in longs, past what an int holds.</summary>
	[Fact]
	public void Reads_a_damage_count_past_an_int()
	{
		using var game = Create(Default with { States = [.. Default.States.Select(state => state.QuestId == 53 ? State(53, 9103, InProgress, new SyntheticTask("ByBattle", 3_000_000_000, 5_000_000_000)) : state)] });

		Assert.Equal((3_000_000_000, 5_000_000_000), Progress(game, 9103));
	}

	/// <summary>A count past what is required is the game's own, as it held 25,347,525 of 10,000,000 damage on the main Account.</summary>
	[Fact]
	public void Reads_a_count_past_what_is_required()
	{
		using var game = Create(Default with { States = [.. Default.States.Select(state => state.QuestId == 52 ? State(52, 9102, Completed, new SyntheticTask("ByBattle", 12, 10)) : state)] });

		Assert.Equal((12, 10), Progress(game, 9102));
	}

	/// <summary>Groups open in the order the game holds them, each in its own section.</summary>
	[Fact]
	public void Reads_two_open_groups_in_their_sections()
	{
		using var game = Create(Default with
		{
			OpenGroups = [5, 2],
			Quests = [.. Default.Quests, Mission(id: 54, prototypeId: 9501, weight: 10, category: 1, group: 5)],
			States = [.. Default.States, State(54, 9501, New, new SyntheticTask("ByFuse", 0, 10))],
			Localization = new Dictionary<string, string>(Default.Localization) { ["l10n:quest/name?id=9501#static"] = "Mix 10 Synthetic Potions" },
		});

		var open = game.Source().Read().Missions.Open;

		Assert.Equal([("Group5", "RamantuSection", 1), ("Group2", "ArbiterSection", 3)], open.Select(group => (group.Group, group.Section, group.Missions.Count)));
	}

	[Fact]
	public void Reads_an_account_with_no_missions()
	{
		using var game = Create(None);

		var missions = game.Source().Read().Missions;

		Assert.Empty(missions.Completed);
		Assert.Empty(missions.Open);
	}

	/// <summary>The sections are needed only to place an open group, so an Account with none reads without them.</summary>
	[Fact]
	public void Reads_an_account_with_no_open_group_without_the_static_data()
	{
		using var game = Create(None);
		game.PointField("SharedModel.Meta.StaticData", "QuestData", SyntheticGameClient.Unmapped);

		Assert.Empty(game.Source().Read().Missions.Open);
	}

	/// <summary>A Mission's default text is a developer's note, not the screen's text, so it never stands in.</summary>
	[Fact]
	public void Refuses_a_mission_whose_text_is_not_in_the_localization_tables()
	{
		using var game = Create(Default with { Localization = Default.Localization.Where(text => !text.Key.Contains("9102", StringComparison.Ordinal)).ToDictionary() });

		RefusesMatching(game, @"^Mission 9102 has no text: its SharedModel\.Common\.Localization\.SharedLTextKey at 0x[0-9A-F]+ has the key ""l10n:quest/name\?id=9102#static"", which isn't in the Game client's localization tables\.$");
	}

	[Fact]
	public void Refuses_a_mission_with_no_state()
	{
		using var game = Create(Default with { States = [.. Default.States.Where(state => state.QuestId != 51)] });

		RefusesMatching(game, @"^Mission 9101, quest 51 in Quests at 0x[0-9A-F]+, has no state in OpenedStates\.$");
	}

	[Fact]
	public void Refuses_a_quest_with_two_states()
	{
		using var game = Create(Default with { States = [.. Default.States, State(52, 9102, New, new SyntheticTask("ByBattle", 0, 10))] });

		RefusesMatching(game, @"^SharedModel\.Meta\.Quests\.Data\.QuestState\.QuestId at 0x[0-9A-F]+ is 52, which OpenedStates holds two states of\.$");
	}

	[Fact]
	public void Refuses_a_state_of_another_prototype()
	{
		using var game = Create(Default with { States = [.. Default.States.Select(state => state.QuestId == 51 ? state with { PrototypeId = 9999 } : state)] });

		RefusesMatching(game, @"^SharedModel\.Meta\.Quests\.Data\.QuestState\.PrototypeId at 0x[0-9A-F]+ is 9999, in the state of Mission 9101, quest 51\.$");
	}

	/// <summary>The game held only the open group's Missions on the main Account, so one in another group is refused rather than placed.</summary>
	[Fact]
	public void Refuses_a_mission_in_a_group_that_is_not_open()
	{
		using var game = Create(Default with { Quests = [.. Default.Quests.Select(quest => quest.Id == 53 ? quest with { Group = 3 } : quest)] });

		RefusesMatching(game, @"^SharedModel\.Meta\.Quests\.Data\.Quest\.RecommendedQuestGroupId at 0x[0-9A-F]+ is Group3, in Mission 9103, but the Account's open groups are Group2\.$");
	}

	[Fact]
	public void Refuses_a_mission_with_no_group()
	{
		using var game = Create(Default with { Quests = [.. Default.Quests.Select(quest => quest.Id == 53 ? quest with { Group = null } : quest)] });

		RefusesMatching(game, @"^SharedModel\.Meta\.Quests\.Data\.Quest\.RecommendedQuestGroupId at 0x[0-9A-F]+ has no value, in Mission 9103, where raid-bridge expects a Mission's group\.$");
	}

	[Fact]
	public void Refuses_a_mission_with_no_category()
	{
		using var game = Create(Default with { Quests = [.. Default.Quests.Select(quest => quest.Id == 53 ? quest with { MissionCategory = null } : quest)] });

		RefusesMatching(game, @"^SharedModel\.Meta\.Quests\.Data\.Quest\.RecommendedQuestCategoryId at 0x[0-9A-F]+ has no value, in Mission 9103, where raid-bridge expects a Mission's category\.$");
	}

	[Fact]
	public void Refuses_a_category_the_game_does_not_name()
	{
		using var game = Create(Default with { Quests = [.. Default.Quests.Select(quest => quest.Id == 53 ? quest with { MissionCategory = 99 } : quest)] });

		RefusesMatching(game, @"^System\.Nullable`1\.value at 0x[0-9A-F]+ is 99, which isn't a member of the game's SharedModel\.Meta\.Quests\.RecommendedQuestCategoryId\.$");
	}

	[Theory]
	[InlineData(0)]
	[InlineData(2)]
	public void Refuses_a_mission_with_other_than_one_completion(int count)
	{
		using var game = Create(Default with
		{
			States = [.. Default.States.Select(state => state.QuestId == 52 ? state with { Completions = [.. Enumerable.Repeat<IReadOnlyList<SyntheticTask>>([new("ByBattle", 4, 10)], count)] } : state)],
		});

		RefusesMatching(game, $@"^Mission 9102's state at 0x[0-9A-F]+ holds {count} completions, where raid-bridge expects one\.$");
	}

	[Fact]
	public void Refuses_a_mission_whose_completions_are_null()
	{
		using var game = Create(Default with { States = [.. Default.States.Select(state => state.QuestId == 52 ? state with { Completions = null } : state)] });

		RefusesMatching(game, @"^SharedModel\.Meta\.Quests\.Data\.QuestState\.Completions at 0x[0-9A-F]+ is null, ");
	}

	[Fact]
	public void Refuses_a_completion_counting_two_kinds()
	{
		using var game = Create(Default with { States = [.. Default.States.Select(state => state.QuestId == 52 ? State(52, 9102, InProgress, new SyntheticTask("ByBattle", 4, 10), new SyntheticTask("ByHero", 1, 2)) : state)] });

		RefusesMatching(game, @"^Mission 9102's completion at 0x[0-9A-F]+ counts ByBattle and ByHero, where raid-bridge expects one\.$");
	}

	[Fact]
	public void Refuses_a_completion_counting_nothing()
	{
		using var game = Create(Default with { States = [.. Default.States.Select(state => state.QuestId == 52 ? State(52, 9102, InProgress) : state)] });

		RefusesMatching(game, @"^Mission 9102's completion at 0x[0-9A-F]+ counts no task, where raid-bridge expects one\.$");
	}

	/// <summary>A kind with no counts, such as one that waits for other quests, has no progress to write as a count.</summary>
	[Fact]
	public void Refuses_a_completion_of_a_kind_with_no_count()
	{
		using var game = Create(Default with { States = [.. Default.States.Select(state => state.QuestId == 52 ? State(52, 9102, InProgress, new SyntheticTask("ByQuest", 0, 0)) : state)] });

		RefusesMatching(game, @"^Mission 9102's completion at 0x[0-9A-F]+ counts ByQuest, which holds no count\.$");
	}

	[Fact]
	public void Refuses_a_required_count_below_one()
	{
		using var game = Create(Default with { States = [.. Default.States.Select(state => state.QuestId == 51 ? State(51, 9101, Completed, new SyntheticTask("ByHero", -1, 0)) : state)] });

		RefusesMatching(game, @"^SharedModel\.Meta\.Quests\.Completions\.QuestCompletionByHero\.CountRequired at 0x[0-9A-F]+ is 0, where raid-bridge expects 1 or more, in Mission 9101\.$");
	}

	/// <summary>-1 is the game's mark for a count it works out; any other negative count is refused.</summary>
	[Fact]
	public void Refuses_a_collected_count_below_minus_one()
	{
		using var game = Create(Default with { States = [.. Default.States.Select(state => state.QuestId == 52 ? State(52, 9102, InProgress, new SyntheticTask("ByBattle", -2, 10)) : state)] });

		RefusesMatching(game, @"^SharedModel\.Meta\.Quests\.Completions\.QuestCompletionByBattle\.CountCollected at 0x[0-9A-F]+ is -2, where raid-bridge expects -1 or more, in Mission 9102\.$");
	}

	[Fact]
	public void Refuses_an_open_group_held_twice()
	{
		using var game = Create(Default with { OpenGroups = [2, 2] });

		RefusesMatching(game, @"^System\.Collections\.Generic\.List`1\._items\[1\] at 0x[0-9A-F]+ is Group2, in ActiveGroups, but ActiveGroups holds it already\.$");
	}

	[Fact]
	public void Refuses_a_finished_group_held_twice()
	{
		using var game = Create(Default with { CompletedGroups = [1, 1] });

		RefusesMatching(game, @"^System\.Collections\.Generic\.List`1\._items\[1\] at 0x[0-9A-F]+ is Group1, in CompletedGroups, but CompletedGroups holds it already\.$");
	}

	[Fact]
	public void Refuses_a_group_both_open_and_finished()
	{
		using var game = Create(Default with { CompletedGroups = [1, 2] });

		RefusesMatching(game, @"^System\.Collections\.Generic\.List`1\._items\[1\] at 0x[0-9A-F]+ is Group2, in CompletedGroups, but ActiveGroups holds it already\.$");
	}

	/// <summary>
	/// The screen shows an unfinished clan battle's progress from the clan's Clan Boss data, not from its stored count,
	/// so the stored count isn't the Mission's progress, and is written as null: for a clan's quest, by its prototype
	/// id or its AllowDifferentAlliances, unless the battle is in the Clan Boss's area.
	/// </summary>
	[Theory]
	[InlineData(9103, null, true, null)]
	[InlineData(7_360_000, null, null, null)]
	[InlineData(8_359_999, null, null, null)]
	[InlineData(9103, 4, true, 7L)]
	[InlineData(8_360_000, null, null, 7L)]
	[InlineData(9103, 2, false, 7L)]
	public void Reads_a_clan_battle_from_the_clan_boss_data_only_outside_its_area(int prototypeId, int? area, bool? allowDifferentAlliances, long? expected)
	{
		using var game = Create(Default with
		{
			Quests = [.. Default.Quests.Select(quest => quest.Id == 53 ? quest with { PrototypeId = prototypeId, Name = new($"l10n:quest/name?id={prototypeId}#static", "dev") } : quest)],
			States = [.. Default.States.Select(state => state.QuestId == 53 ? State(53, prototypeId, InProgress, new SyntheticTask("ByBattle", 7, 2_000_000, area, allowDifferentAlliances)) : state)],
			Localization = new Dictionary<string, string>(Default.Localization) { [$"l10n:quest/name?id={prototypeId}#static"] = "Deal damage" },
		});

		Assert.Equal((expected, 2_000_000), Progress(game, prototypeId));
	}

	/// <summary>A finished Mission's progress is its stored count, wherever the battle was.</summary>
	[Fact]
	public void Reads_a_finished_clan_battle_from_its_stored_count()
	{
		using var game = Create(Default with { States = [.. Default.States.Select(state => state.QuestId == 53 ? State(53, 9103, Completed, new SyntheticTask("ByBattle", 2_500_000, 2_000_000, AllowDifferentAlliances: true)) : state)] });

		Assert.Equal((2_500_000, 2_000_000), Progress(game, 9103));
	}

	[Fact]
	public void Refuses_an_open_group_in_no_section()
	{
		using var game = Create(Default with { GroupsBySection = new Dictionary<int, IReadOnlyList<int>> { [1] = [1], [2] = [5] } });

		RefusesMatching(game, @"^System\.Collections\.Generic\.List`1\._items\[0\] at 0x[0-9A-F]+ is Group2, which the static data's GroupsBySection puts in no section, where raid-bridge expects one\.$");
	}

	[Fact]
	public void Refuses_an_open_group_in_two_sections()
	{
		using var game = Create(Default with { GroupsBySection = new Dictionary<int, IReadOnlyList<int>> { [1] = [1, 2], [2] = [2, 5] } });

		RefusesMatching(game, @"^System\.Collections\.Generic\.List`1\._items\[0\] at 0x[0-9A-F]+ is Group2, which the static data's GroupsBySection puts in ArbiterSection and RamantuSection, where raid-bridge expects one\.$");
	}

	[Fact]
	public void Refuses_a_quest_state_that_is_not_an_object()
	{
		_game.PointQuestState(1, SyntheticGameClient.Unmapped);

		RefusesMatching(_game, @"^System\.Collections\.Generic\.List`1\._items\[1\] at 0x[0-9A-F]+ points to 0xDEAD00000000, which isn't an object\. ");
	}

	[Fact]
	public void Refuses_quests_whose_counts_disagree_with_their_slots()
	{
		_game.SetQuestCounts(count: 9, freeCount: 0);

		RefusesMatching(_game, @"^Client\.Model\.Gameplay\.Quests\.QuestWrapperReadOnly\.Quests at 0x[0-9A-F]+ is malformed: its _count is 9 and its _freeCount 0, with 5 slots\.$");
	}

	[Fact]
	public void Refuses_a_quest_wrapper_that_is_not_an_object()
	{
		_game.PointField("Client.Model.Guard.UserWrapper", "Quests", SyntheticGameClient.Unmapped);

		RefusesMatching(_game, @"^Client\.Model\.Guard\.UserWrapper\.Quests at 0x[0-9A-F]+ points to 0xDEAD00000000, which isn't an object\. ");
	}

	[Fact]
	public void Refuses_a_quest_class_missing_from_the_metadata()
	{
		_game.RenameClass("Client.Model.Gameplay.Quests.QuestWrapper", "Client.Model.Gameplay.Quests.TaskWrapper");

		Refuses(_game, "The game's metadata has no class Client.Model.Gameplay.Quests.QuestWrapper.");
	}

	/// <summary>A game update that drops a kind of task is refused, so a Mission of that kind can't pass for one with no task.</summary>
	[Fact]
	public void Refuses_a_kind_of_task_missing_from_the_metadata()
	{
		_game.UndeclareField("SharedModel.Meta.Quests.Completions.QuestCompletion", "ByRelicStone");

		Refuses(_game, "The game's class SharedModel.Meta.Quests.Completions.QuestCompletion has no field ByRelicStone, base classes included.");
	}

	/// <summary>A game update that made a kind's counts anything but ints is refused rather than read as ints.</summary>
	[Fact]
	public void Refuses_counts_of_another_type()
	{
		_game.RetypeField("SharedModel.Meta.Quests.Completions.QuestCompletionByHero", "CountRequired", "System.Int64");

		RefusesMatching(_game, @"^The game's SharedModel\.Meta\.Quests\.Completions\.QuestCompletionByHero\.CountRequired is a System\.Int64, where raid-bridge expects a System\.Int32\.$");
	}

	public void Dispose() => _game.Dispose();

	private static SyntheticGameClient Create(SyntheticMissionData missionData) => SyntheticGameClient.Create(missionData: missionData);

	private static (long? Collected, long Required) Progress(SyntheticGameClient game, int prototypeId)
	{
		var mission = game.Source().Read().Missions.Open.SelectMany(group => group.Missions).Single(mission => mission.PrototypeId == prototypeId);
		return (mission.Collected, mission.Required);
	}

	private static void Refuses(SyntheticGameClient game, string message) =>
		Assert.Equal(message, Assert.ThrowsAny<GameClientException>(() => game.Source().Read()).Message);

	private static void RefusesMatching(SyntheticGameClient game, string pattern) =>
		Assert.Matches(pattern, Assert.ThrowsAny<GameClientException>(() => game.Source().Read()).Message);

}
