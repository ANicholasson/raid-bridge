using RaidBridge.Domain.Missions;
using RaidBridge.Game.Il2Cpp;
using RaidBridge.Game.Localization;
using RaidBridge.Game.Metadata;

namespace RaidBridge.Game.Missions;

/// <summary>
/// Reads the Account's Missions from the game's model (https://github.com/ANicholasson/raid-bridge/issues/33), on the
/// route the missions research found at metadata v39 (https://github.com/ANicholasson/raid-bridge/pull/37). The game's
/// code calls a Mission a recommended quest: <c>_userWrapper.Quests</c> holds every quest the server sent at sign-in in
/// <c>Quests</c>, a Mission being one whose <c>CategoryId</c> is <c>Recommended</c>, and the Account's progress in
/// <c>QuestData</c>: a <c>QuestState</c> for each open quest in <c>OpenedStates</c>, and the open and finished groups in
/// <c>RecommendedQuestsData</c>. The static data's <c>QuestData.RecommendedQuestData.GroupsBySection</c> says which
/// section of the Missions screen each group is in.
/// </summary>
internal static class MissionReader
{
	private const string GroupId = "SharedModel.Meta.Quests.RecommendedQuestGroupId";
	private const string SectionId = "SharedModel.Meta.Quests.RecommendedQuestSectionId";
	private const string Quest = "SharedModel.Meta.Quests.Data.Quest";
	private const string QuestState = "SharedModel.Meta.Quests.Data.QuestState";
	private const string Completion = "SharedModel.Meta.Quests.Completions.QuestCompletion";
	private const string Completions = "SharedModel.Meta.Quests.Completions";
	private const string Groups = $"System.Collections.Generic.List`1<{GroupId}>";

	/// <summary>The stored count a kind holds when the game keeps no count of its own.</summary>
	private const long NoCount = -1;

	/// <summary>
	/// The prototype ids <c>QuestExtensions.IsAllianceQuest</c> takes for a clan's quest, from 7,360,000 to under
	/// 8,360,000: literals in the game's code at metadata v39, not static data.
	/// </summary>
	private const int FirstClanQuest = 7_360_000;

	private const int PastLastClanQuest = 8_360_000;

	/// <summary>
	/// Every kind of task a <c>QuestCompletion</c> can hold, each in a field of its own that is null unless the quest
	/// counts that kind, and how it holds its <c>CountRequired</c> and <c>CountCollected</c>. A kind with no counts is
	/// refused on a Mission, since the screen's progress for it isn't a count.
	/// </summary>
	private static readonly (string Field, string Type, Counts Counts)[] Kinds =
	[
		("ByClient", $"{Completions}.QuestCompletionByClient", Counts.Int32),
		("ByAction", $"{Completions}.QuestCompletionByAction", Counts.Int32),
		("Periodic", "SharedModel.Meta.PeriodicQuests.QuestCompletionPeriodic", Counts.None),
		("ByBattle", $"{Completions}.QuestCompletionByBattle", Counts.Int64),
		("ByArtifact", $"{Completions}.QuestCompletionByArtifact", Counts.Int32),
		("ByQuest", $"{Completions}.QuestCompletionByQuest", Counts.None),
		("ByHero", $"{Completions}.QuestCompletionByHero", Counts.Int32),
		("ByCapitolBonus", $"{Completions}.QuestCompletionByCapitolBonus", Counts.Int32),
		("ByArena", $"{Completions}.QuestCompletionByArena", Counts.Int32),
		("ByArena3X3", $"{Completions}.QuestCompletionByArena3X3", Counts.Int32),
		("ByLoot", $"{Completions}.QuestCompletionByLoot", Counts.Int32),
		("ByMastery", $"{Completions}.QuestCompletionByMastery", Counts.Int32),
		("ByFuse", $"{Completions}.QuestCompletionByFuse", Counts.Int32),
		("ByAllianceActivity", $"{Completions}.QuestCompletionByAllianceActivity", Counts.Int32),
		("BySoloEvent", $"{Completions}.QuestCompletionBySoloEvent", Counts.None),
		("ByTournament", $"{Completions}.QuestCompletionByTournament", Counts.None),
		("ByResourceSpent", $"{Completions}.QuestCompletionByResourceSpent", Counts.Int32),
		("ByResourceGained", $"{Completions}.QuestCompletionByResourceGained", Counts.Int32),
		("ByForge", $"{Completions}.QuestCompletionByForge", Counts.Int32),
		("ByCvc", $"{Completions}.QuestCompletionByCvc", Counts.None),
		("ByPowerUp", $"{Completions}.QuestCompletionByPowerUp", Counts.Int32),
		("ByConditionalPromoCode", $"{Completions}.QuestCompletionByConditionalPromoCode", Counts.None),
		("ByLiveArena", $"{Completions}.QuestCompletionByLiveArena", Counts.Int32),
		("ByLiveArenaSeasons", $"{Completions}.QuestCompletionByLiveArenaSeasons", Counts.Int32),
		// The game's own spelling.
		("ByAreaBonus", $"{Completions}.QuestCompletionByByAreaBonus", Counts.Int32),
		("BySiege", $"{Completions}.QuestCompletionBySiege", Counts.Int32),
		("ByLeaderboardSeason", $"{Completions}.QuestCompletionByLeaderboardSeason", Counts.Int32),
		("ByFoggyForest", $"{Completions}.QuestCompletionByFoggyForest", Counts.Int32),
		("ByBattleSide", $"{Completions}.QuestCompletionByBattleSide", Counts.Int32),
		("ByRelic", $"{Completions}.QuestCompletionByRelic", Counts.Int32),
		("ByRelicStone", $"{Completions}.QuestCompletionByRelicStone", Counts.Int32),
	];

	/// <summary>How a kind of task holds its counts: not at all, as ints, or as longs, as <c>ByBattle</c> does, since it counts damage too.</summary>
	private enum Counts
	{
		None,
		Int32,
		Int64,
	}

	/// <summary>
	/// The finished groups, and each open group with its Missions in the order of their <c>Weight</c>, the order the
	/// game gives them. Every Mission must be in an open group: on the main Account the game held only the open group's
	/// Missions, its finished groups' only as ids (2026-11-13). No group may be both open and finished, or either twice.
	/// </summary>
	public static MissionGroups Read(ManagedObject userWrapper, ManagedObject staticData, LocalizedNames names)
	{
		var quests = userWrapper.Object("Quests", "Client.Model.Gameplay.Quests.QuestWrapper");
		var questData = quests.Object("QuestData", "SharedModel.Meta.Quests.Data.UserQuestData");
		var groups = questData.Object("RecommendedQuestsData", "SharedModel.Meta.Quests.RecommendedQuests.UserRecommendedQuestData");
		var open = groups.List("ActiveGroups", Groups).EnumMembers(GroupId);
		var completed = groups.List("CompletedGroups", Groups).EnumMembers(GroupId);
		var seen = new Dictionary<long, string>();
		foreach (var (group, list) in open.Select(group => (group, "ActiveGroups")).Concat(completed.Select(group => (group, "CompletedGroups"))))
		{
			if (!seen.TryAdd(group.Value.Value, list))
			{
				throw new GameDataException($"{Named(group)}, in {list}, but {seen[group.Value.Value]} holds it already.");
			}
		}

		var states = States(questData);
		var missions = quests
			.Dictionary("Quests", $"System.Collections.Generic.Dictionary`2<System.Int32, {Quest}>")
			.ByInt32Key()
			.Select(entry => (Id: entry.Key, Quest: entry.Value.Object(Quest)))
			.Where(entry => entry.Quest.Enum("CategoryId", "SharedModel.Meta.Quests.QuestCategoryId").Value == "Recommended")
			.Select(entry => Placed(entry.Id, entry.Quest, states, names))
			.OrderBy(mission => mission.Weight)
			.ToList();

		var openIds = open.Select(group => group.Value.Value).ToHashSet();
		if (missions.FirstOrDefault(mission => !openIds.Contains(mission.Group.Value.Value)) is { } stray)
		{
			throw new GameDataException(
				$"{Named(stray.Group)}, in Mission {stray.Mission.PrototypeId}, but the Account's open groups are {(open.Count == 0 ? "none" : string.Join(", ", open.Select(group => group.Value.Name)))}.");
		}

		var sections = new Lazy<IReadOnlyList<(string Section, IReadOnlySet<long> Groups)>>(() => Sections(staticData));
		return new(
			[.. completed.Select(group => group.Value.Name)],
			[.. open.Select(group => new MissionGroup(
				group.Value.Name,
				Section(group, sections.Value),
				[.. missions.Where(mission => mission.Group.Value.Value == group.Value.Value).Select(mission => mission.Mission)]))]);
	}

	/// <summary>Each open quest's state, by its <c>QuestId</c>. A quest with two states is refused, since raid-bridge can't tell which the screen shows.</summary>
	private static Dictionary<int, ManagedObject> States(ManagedObject questData)
	{
		var states = new Dictionary<int, ManagedObject>();
		foreach (var state in questData.List("OpenedStates", $"System.Collections.Generic.List`1<{QuestState}>").Objects(QuestState))
		{
			var questId = state.Int32("QuestId");
			if (!states.TryAdd(questId.Value, state))
			{
				throw new GameDataException($"{questId}, which OpenedStates holds two states of.");
			}
		}

		return states;
	}

	/// <summary>
	/// One Mission, with its group and <c>Weight</c> to place it. Its state must name the same prototype, and its text
	/// is the <c>Name</c>, from the localization tables only: its default text is a developer's note.
	/// </summary>
	private static PlacedMission Placed(int id, ManagedObject quest, Dictionary<int, ManagedObject> states, LocalizedNames names)
	{
		var prototypeId = quest.Int32("PrototypeId").Value;
		var named = $"Mission {prototypeId}";
		if (!states.TryGetValue(id, out var state))
		{
			throw new GameDataException($"{named}, quest {id} in Quests at 0x{quest.Address:X}, has no state in OpenedStates.");
		}

		var statePrototypeId = state.Int32("PrototypeId");
		if (statePrototypeId.Value != prototypeId)
		{
			throw new GameDataException($"{statePrototypeId}, in the state of {named}, quest {id}.");
		}

		var stateId = state.Enum("StateId", "SharedModel.Meta.Quests.QuestStateId").Value;
		var (collected, required) = Progress(state, prototypeId, finished: stateId == "Completed", named);
		var group = Required(quest.NullableEnumMember("RecommendedQuestGroupId", GroupId), named, "group");
		return new(
			new(
				prototypeId,
				Required(quest.NullableEnum("RecommendedQuestCategoryId", "SharedModel.Meta.Quests.RecommendedQuestCategoryId"), named, "category").Value,
				stateId,
				names.InTables(quest, "Name", named),
				collected,
				required),
			group,
			quest.Int32("Weight").Value);
	}

	/// <summary>A Nullable field a Mission must hold a value in, such as its group.</summary>
	private static FieldValue<T> Required<T>(FieldValue<T?> field, string named, string what)
		where T : class =>
		field.Value is { } value
			? new(value, field.Field, field.Address)
			: throw new GameDataException($"{field.Field} at 0x{field.Address:X} has no value, in {named}, where raid-bridge expects a Mission's {what}.");

	/// <summary>
	/// The count the game holds towards the count required, from the one kind of task the Mission's one completion
	/// counts, or null for a count the game holds none of. Every Mission read on the main Account had one completion
	/// counting one kind (2026-11-13); any other shape is refused rather than guessed at. A stored count of -1 is the
	/// game's mark for no count of its own: for an unfinished Mission of a kind that counts from the Account, such as
	/// <c>ByHero</c>, <c>QuestWrapperReadOnly.CalcProgress</c> works the progress out each time the screen opens.
	/// </summary>
	private static (long? Collected, long Required) Progress(ManagedObject state, int prototypeId, bool finished, string named)
	{
		var completions = state.List("Completions", $"System.Collections.Generic.List`1<{Completion}>").Objects(Completion);
		if (completions.Count != 1)
		{
			throw new GameDataException($"{named}'s state at 0x{state.Address:X} holds {completions.Count} completions, where raid-bridge expects one.");
		}

		var kinds = Kinds
			.Select(kind => (Kind: kind, Task: completions[0].ObjectOrNull(kind.Field, kind.Type)))
			.Where(kind => kind.Task is not null)
			.ToList();
		if (kinds.Count != 1)
		{
			throw new GameDataException(
				$"{named}'s completion at 0x{completions[0].Address:X} counts {(kinds.Count == 0 ? "no task" : string.Join(" and ", kinds.Select(kind => kind.Kind.Field)))}, where raid-bridge expects one.");
		}

		var ((field, _, counts), task) = kinds[0];
		if (counts == Counts.None)
		{
			throw new GameDataException($"{named}'s completion at 0x{completions[0].Address:X} counts {field}, which holds no count.");
		}

		var where = $"in {named}";
		var required = Count(task!, "CountRequired", counts).Where(count => count >= 1, $"1 or more, {where}");
		var collected = Count(task!, "CountCollected", counts).Where(count => count >= NoCount, $"{NoCount} or more, {where}");
		return (collected == NoCount || (!finished && field == "ByBattle" && ShowsClanBossDamage(state, prototypeId, task!)) ? null : collected, required);
	}

	/// <summary>
	/// Whether the screen shows an unfinished battle task's progress from the clan's Clan Boss data rather than from its
	/// stored count. The game's <c>QuestWrapperReadOnly.CalcProgress</c> for <c>ByBattle</c> goes to
	/// <c>CalcProgressByDamageToAllianceBoss</c> for a clan's quest (<c>IsAllianceQuest</c>, an Advanced quest, or one that
	/// <c>AllowDifferentAlliances</c>), unless the battle names the Clan Boss: its <c>AreaTypeId</c> is
	/// <c>AllianceBoss</c>, or it has an <c>AllianceBossId</c> or <c>AllianceBossRevision</c>; then it keeps the stored
	/// count (disassembled at metadata v39, 2026-11-13). Every Demon Lord Mission read on the main Account named the
	/// Clan Boss's area, so its stored count is the screen's.
	/// </summary>
	private static bool ShowsClanBossDamage(ManagedObject state, int prototypeId, ManagedObject battle)
	{
		var clans = prototypeId is >= FirstClanQuest and < PastLastClanQuest
			|| state.HasValue("AdvancedDailyTypeId", "SharedModel.Meta.AdvancedDaily.AdvancedDailyTypeId")
			|| battle.NullableBoolean("AllowDifferentAlliances").Value == true;
		var namesTheClanBoss = battle.NullableEnum("AreaTypeId", "SharedModel.Meta.Stages.AreaTypeId").Value == "AllianceBoss"
			|| battle.HasValue("AllianceBossId", "SharedModel.Meta.Alliances.Boss.AllianceBossTypeId")
			|| battle.HasValue("AllianceBossRevision", "System.Int64");
		return clans && !namesTheClanBoss;
	}

	/// <summary>A count, as the kind holds it.</summary>
	private static FieldValue<long> Count(ManagedObject task, string fieldName, Counts counts)
	{
		if (counts == Counts.Int64)
		{
			return task.Int64(fieldName);
		}

		var count = task.Int32(fieldName);
		return new(count.Value, count.Field, count.Address);
	}

	/// <summary>Each section of the Missions screen, and the groups in it.</summary>
	private static IReadOnlyList<(string Section, IReadOnlySet<long> Groups)> Sections(ManagedObject staticData) =>
		[.. staticData
			.Object("QuestData", "SharedModel.Meta.Quests.Data.StaticQuestData")
			.Object("RecommendedQuestData", "SharedModel.Meta.Quests.RecommendedQuests.RecommendedQuestData")
			.Dictionary("GroupsBySection", $"System.Collections.Generic.Dictionary`2<{SectionId}, {Groups}>")
			.ByEnumKey(SectionId)
			.Select(section => (section.Key.Name, (IReadOnlySet<long>)section.Value.List().EnumMembers(GroupId).Select(group => group.Value.Value).ToHashSet()))];

	/// <summary>A group as an error names it: where it was read, and its name.</summary>
	private static string Named(FieldValue<GameEnumMember> group) => $"{group.Field} at 0x{group.Address:X} is {group.Value.Name}";

	/// <summary>The one section an open group is in.</summary>
	private static string Section(FieldValue<GameEnumMember> group, IReadOnlyList<(string Section, IReadOnlySet<long> Groups)> sections)
	{
		var holding = sections.Where(section => section.Groups.Contains(group.Value.Value)).Select(section => section.Section).ToList();
		return holding.Count == 1
			? holding[0]
			: throw new GameDataException(
				$"{Named(group)}, which the static data's GroupsBySection puts in {(holding.Count == 0 ? "no section" : string.Join(" and ", holding))}, where raid-bridge expects one.");
	}

	/// <summary>A Mission, with the group it is in and its <c>Weight</c>, which orders it in the group.</summary>
	private sealed record PlacedMission(Mission Mission, FieldValue<GameEnumMember> Group, int Weight);
}
