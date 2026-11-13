using RaidBridge.Domain.Missions;
using RaidBridge.Game.Il2Cpp;
using RaidBridge.Game.Localization;

namespace RaidBridge.Game.Missions;

/// <summary>
/// Reads the Account's Missions from the game's model (https://github.com/ANicholasson/raid-bridge/issues/33), the route
/// docs/research/2026-11-missions.md found at metadata v39. The game's code calls a Mission a recommended quest:
/// <c>_userWrapper.Quests</c> holds every quest the server sent at sign-in in <c>Quests</c>, a Mission being one whose
/// <c>CategoryId</c> is <c>Recommended</c>, and the Account's progress in <c>QuestData</c>: a <c>QuestState</c> for each
/// open quest in <c>OpenedStates</c>, and the open and finished groups in <c>RecommendedQuestsData</c>. The static data's
/// <c>QuestData.RecommendedQuestData.GroupsBySection</c> says which section of the Missions screen each group is in.
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

	/// <summary>The stored count a kind holds when the game works the progress out from the Account instead.</summary>
	private const long Computed = -1;

	/// <summary>
	/// Every kind of task a <c>QuestCompletion</c> can hold, each in a field of its own that is null unless the quest
	/// counts that kind, and whether its <c>CountRequired</c> and <c>CountCollected</c> are <c>long</c>s. A kind with no
	/// counts is null here, and refused on a Mission, since the screen's progress for it isn't a count.
	/// </summary>
	private static readonly (string Field, string Type, bool? Long)[] Kinds =
	[
		("ByClient", $"{Completions}.QuestCompletionByClient", false),
		("ByAction", $"{Completions}.QuestCompletionByAction", false),
		("Periodic", "SharedModel.Meta.PeriodicQuests.QuestCompletionPeriodic", null),
		("ByBattle", $"{Completions}.QuestCompletionByBattle", true),
		("ByArtifact", $"{Completions}.QuestCompletionByArtifact", false),
		("ByQuest", $"{Completions}.QuestCompletionByQuest", null),
		("ByHero", $"{Completions}.QuestCompletionByHero", false),
		("ByCapitolBonus", $"{Completions}.QuestCompletionByCapitolBonus", false),
		("ByArena", $"{Completions}.QuestCompletionByArena", false),
		("ByArena3X3", $"{Completions}.QuestCompletionByArena3X3", false),
		("ByLoot", $"{Completions}.QuestCompletionByLoot", false),
		("ByMastery", $"{Completions}.QuestCompletionByMastery", false),
		("ByFuse", $"{Completions}.QuestCompletionByFuse", false),
		("ByAllianceActivity", $"{Completions}.QuestCompletionByAllianceActivity", false),
		("BySoloEvent", $"{Completions}.QuestCompletionBySoloEvent", null),
		("ByTournament", $"{Completions}.QuestCompletionByTournament", null),
		("ByResourceSpent", $"{Completions}.QuestCompletionByResourceSpent", false),
		("ByResourceGained", $"{Completions}.QuestCompletionByResourceGained", false),
		("ByForge", $"{Completions}.QuestCompletionByForge", false),
		("ByCvc", $"{Completions}.QuestCompletionByCvc", null),
		("ByPowerUp", $"{Completions}.QuestCompletionByPowerUp", false),
		("ByConditionalPromoCode", $"{Completions}.QuestCompletionByConditionalPromoCode", null),
		("ByLiveArena", $"{Completions}.QuestCompletionByLiveArena", false),
		("ByLiveArenaSeasons", $"{Completions}.QuestCompletionByLiveArenaSeasons", false),
		// The game's own spelling.
		("ByAreaBonus", $"{Completions}.QuestCompletionByByAreaBonus", false),
		("BySiege", $"{Completions}.QuestCompletionBySiege", false),
		("ByLeaderboardSeason", $"{Completions}.QuestCompletionByLeaderboardSeason", false),
		("ByFoggyForest", $"{Completions}.QuestCompletionByFoggyForest", false),
		("ByBattleSide", $"{Completions}.QuestCompletionByBattleSide", false),
		("ByRelic", $"{Completions}.QuestCompletionByRelic", false),
		("ByRelicStone", $"{Completions}.QuestCompletionByRelicStone", false),
	];

	/// <summary>
	/// The finished groups, and each open group with its Missions in the order of their <c>Weight</c>, the order the
	/// game gives them. Every Mission must be in an open group: on the main Account the game held only the open group's
	/// Missions, its finished groups' only as ids (2026-11-13).
	/// </summary>
	public static MissionGroups Read(ManagedObject userWrapper, ManagedObject staticData, LocalizedNames names)
	{
		var quests = userWrapper.Object("Quests", "Client.Model.Gameplay.Quests.QuestWrapper");
		var questData = quests.Object("QuestData", "SharedModel.Meta.Quests.Data.UserQuestData");
		var groups = questData.Object("RecommendedQuestsData", "SharedModel.Meta.Quests.RecommendedQuests.UserRecommendedQuestData");
		var open = groups.List("ActiveGroups", Groups).EnumMembers(GroupId);
		var states = States(questData);
		var missions = quests
			.Dictionary("Quests", $"System.Collections.Generic.Dictionary`2<System.Int32, {Quest}>")
			.ByInt32Key()
			.Select(entry => (Id: entry.Key, Quest: entry.Value.Object(Quest)))
			.Where(entry => entry.Quest.Enum("CategoryId", "SharedModel.Meta.Quests.QuestCategoryId").Value == "Recommended")
			.Select(entry => Mission(entry.Id, entry.Quest, states, names))
			.OrderBy(mission => mission.Weight)
			.ToList();

		var openIds = new HashSet<long>();
		foreach (var group in open)
		{
			if (!openIds.Add(group.Value.Value))
			{
				throw new GameDataException($"{Named(group)}, which ActiveGroups holds twice.");
			}
		}

		foreach (var (mission, group, _) in missions.Where(mission => !openIds.Contains(mission.Group.Value.Value)))
		{
			throw new GameDataException(
				$"{Named(group)}, in Mission {mission.PrototypeId}, but the Account's open groups are {(open.Count == 0 ? "none" : string.Join(", ", open.Select(open => open.Value.Name)))}.");
		}

		var sections = new Lazy<IReadOnlyList<(string Section, IReadOnlySet<long> Groups)>>(() => Sections(staticData));
		return new(
			[.. groups.List("CompletedGroups", Groups).EnumMembers(GroupId).Select(group => group.Value.Name)],
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
	private static (Mission Mission, FieldValue<Metadata.GameEnumMember> Group, int Weight) Mission(
		int id,
		ManagedObject quest,
		Dictionary<int, ManagedObject> states,
		LocalizedNames names)
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

		var group = quest.NullableEnumMember("RecommendedQuestGroupId", GroupId);
		var category = quest.NullableEnum("RecommendedQuestCategoryId", "SharedModel.Meta.Quests.RecommendedQuestCategoryId");
		var (collected, required) = Progress(state, named);
		return (
			new(
				prototypeId,
				category.Value ?? throw new GameDataException($"{category.Field} at 0x{category.Address:X} has no value, in {named}, where raid-bridge expects a Mission's category."),
				state.Enum("StateId", "SharedModel.Meta.Quests.QuestStateId").Value,
				names.InTables(quest, "Name", named),
				collected,
				required),
			group.Value is { } member ? new(member, group.Field, group.Address) : throw new GameDataException($"{group.Field} at 0x{group.Address:X} has no value, in {named}, where raid-bridge expects a Mission's group."),
			quest.Int32("Weight").Value);
	}

	/// <summary>
	/// The count the game holds towards the count required, from the one kind of task the Mission's one completion
	/// counts. The game's <c>QuestWrapperReadOnly.CalcProgress</c> shows a stored count of 0 or more as it is, and works
	/// the progress out from the Account when the count is -1, so -1 is written as null. Damage to the Demon Lord is a
	/// stored count too: <c>CalcProgress</c> reads the clan's boss data instead only for a battle outside the Clan Boss's
	/// <c>AreaTypeId</c>, and every Clan Mission read named it (disassembled at metadata v39, 2026-11-13). Every Mission
	/// read on the main Account had one completion counting one kind; any other shape is refused rather than guessed at.
	/// </summary>
	private static (long? Collected, long Required) Progress(ManagedObject state, string named)
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

		var ((field, _, isLong), task) = kinds[0];
		if (isLong is not { } wide)
		{
			throw new GameDataException($"{named}'s completion at 0x{completions[0].Address:X} counts {field}, which holds no count.");
		}

		var where = $"in {named}";
		var required = Count(task!, "CountRequired", wide).Where(count => count >= 1, $"1 or more, {where}");
		var collected = Count(task!, "CountCollected", wide).Where(count => count >= Computed, $"{Computed} or more, {where}");
		return (collected == Computed ? null : collected, required);
	}

	/// <summary>A count, an int on every kind but <c>ByBattle</c>, which counts damage too.</summary>
	private static FieldValue<long> Count(ManagedObject task, string fieldName, bool wide)
	{
		if (wide)
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
	private static string Named(FieldValue<Metadata.GameEnumMember> group) => $"{group.Field} at 0x{group.Address:X} is {group.Value.Name}";

	/// <summary>The one section an open group is in.</summary>
	private static string Section(FieldValue<Metadata.GameEnumMember> group, IReadOnlyList<(string Section, IReadOnlySet<long> Groups)> sections)
	{
		var holding = sections.Where(section => section.Groups.Contains(group.Value.Value)).Select(section => section.Section).ToList();
		return holding.Count == 1
			? holding[0]
			: throw new GameDataException(
				$"{Named(group)}, which the static data's GroupsBySection puts in {(holding.Count == 0 ? "no section" : string.Join(" and ", holding))}, where raid-bridge expects one.");
	}
}
