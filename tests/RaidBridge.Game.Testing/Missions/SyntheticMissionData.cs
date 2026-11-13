using RaidBridge.Game.Testing.Champions;

namespace RaidBridge.Game.Testing.Missions;

/// <summary>
/// The quests a synthetic Game client holds, in the game's own terms: groups, sections, categories and states hold the
/// game's numbers, such as 4 for Group4 and 30 for Completed, and each quest's text is a key the static data's
/// localization table holds, or doesn't. The quests are laid out in the order given here, and their states, in
/// OpenedStates, in the order of <see cref="States"/>.
/// </summary>
public sealed record SyntheticMissionData(
	IReadOnlyList<int> OpenGroups,
	IReadOnlyList<int> CompletedGroups,
	IReadOnlyList<SyntheticQuest> Quests,
	IReadOnlyList<SyntheticQuestState> States,
	IReadOnlyDictionary<int, IReadOnlyList<int>> GroupsBySection,
	IReadOnlyDictionary<string, string> Localization)
{
	public const int Recommended = 7;
	public const int Periodic = 3;
	public const int New = 10;
	public const int InProgress = 20;
	public const int Completed = 30;

	/// <summary>The game's sections of the Missions screen: Arbiter's Group1 to Group4 and Ramantu's Group5 to Group7.</summary>
	public static IReadOnlyDictionary<int, IReadOnlyList<int>> Sections { get; } = new Dictionary<int, IReadOnlyList<int>>
	{
		[1] = [1, 2, 3, 4],
		[2] = [5, 6, 7],
	};

	/// <summary>
	/// An Account on Arbiter's Group2, Group1 finished: three Missions, held out of the game's order, one finished on a
	/// count the game works out (-1), one under way on a count it holds, one new; and a daily quest beside them, which
	/// isn't a Mission.
	/// </summary>
	public static SyntheticMissionData Default { get; } = new(
		OpenGroups: [2],
		CompletedGroups: [1],
		Quests:
		[
			Mission(id: 52, prototypeId: 9102, weight: 220, category: 1, group: 2),
			Mission(id: 51, prototypeId: 9101, weight: 210, category: 5, group: 2),
			Mission(id: 53, prototypeId: 9103, weight: 230, category: 6, group: 2),
			new(Id: 60, PrototypeId: 7001, CategoryId: Periodic, Weight: 10, MissionCategory: null, Group: null, Name: new("l10n:quest/name?id=7001#static", "daily")),
		],
		States:
		[
			State(51, 9101, Completed, new SyntheticTask("ByHero", Collected: -1, Required: 2)),
			State(52, 9102, InProgress, new SyntheticTask("ByBattle", Collected: 4, Required: 10)),
			State(53, 9103, New, new SyntheticTask("ByBattle", Collected: 0, Required: 2_000_000)),
			State(60, 7001, InProgress, new SyntheticTask("ByFuse", Collected: 1, Required: 3)),
		],
		GroupsBySection: Sections,
		Localization: new Dictionary<string, string>
		{
			["l10n:quest/name?id=9101#static"] = "Upgrade 2 Champions to Level 30",
			["l10n:quest/name?id=9102#static"] = "Clear Stage 10 of a Synthetic Keep 10 times",
			["l10n:quest/name?id=9103#static"] = "Deal 2,000,000 damage to the Synthetic Boss",
			["l10n:quest/name?id=7001#static"] = "Win 3 battles",
		});

	/// <summary>An Account with no quests at all, and no Mission group open or finished.</summary>
	public static SyntheticMissionData None { get; } = new([], [], [], [], Sections, new Dictionary<string, string>());

	/// <summary>A Mission, its name the key of its prototype, its default text a developer's note.</summary>
	public static SyntheticQuest Mission(int id, int prototypeId, int weight, int category, int group) =>
		new(id, prototypeId, Recommended, weight, category, group, new($"l10n:quest/name?id={prototypeId}#static", $"dev note {prototypeId}"));

	/// <summary>A quest's state with one completion, which counts the tasks given.</summary>
	public static SyntheticQuestState State(int questId, int prototypeId, int stateId, params SyntheticTask[] tasks) =>
		new(questId, prototypeId, stateId, [tasks]);
}

/// <summary>
/// A quest, as SharedModel.Meta.Quests.Data.Quest holds it: its CategoryId, and the Nullable RecommendedQuestCategoryId
/// and RecommendedQuestGroupId a Mission sets.
/// </summary>
public sealed record SyntheticQuest(int Id, int PrototypeId, int CategoryId, int Weight, int? MissionCategory, int? Group, SyntheticText Name);

/// <summary>
/// A quest's state, as SharedModel.Meta.Quests.Data.QuestState holds it: its quest, prototype and StateId, and its
/// completions, each the tasks it counts, or a null list when <see cref="Completions"/> is null.
/// </summary>
public sealed record SyntheticQuestState(int QuestId, int PrototypeId, int StateId, IReadOnlyList<IReadOnlyList<SyntheticTask>>? Completions);

/// <summary>
/// A task a completion counts, in the QuestCompletion field named <see cref="Kind"/>, such as ByHero, with its counts.
/// ByBattle's are longs, the others' ints; ByQuest holds none.
/// </summary>
public sealed record SyntheticTask(string Kind, long Collected, long Required);
