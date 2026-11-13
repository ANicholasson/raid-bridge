using RaidBridge.Game.Testing.Il2Cpp;
using RaidBridge.Game.Testing.Missions;
using static RaidBridge.Game.Testing.Il2Cpp.Unity6000Layout;

namespace RaidBridge.Game.Testing;

/// <summary>The quests and Missions of a synthetic Game client, and what a bad read can do to them.</summary>
public sealed partial class SyntheticGameClient
{
	private const string Completions = "SharedModel.Meta.Quests.Completions";

	/// <summary>
	/// QuestCompletion's fields, one per kind of task, in the game's order, and the class of each. The synthetic client
	/// lays out the classes of ByHero, ByFuse, ByBattle and ByQuest; the others' fields are declared and always null.
	/// </summary>
	private static readonly (string Field, string Type)[] CompletionKinds =
	[
		("ByClient", $"{Completions}.QuestCompletionByClient"),
		("ByAction", $"{Completions}.QuestCompletionByAction"),
		("Periodic", "SharedModel.Meta.PeriodicQuests.QuestCompletionPeriodic"),
		("ByBattle", $"{Completions}.QuestCompletionByBattle"),
		("ByArtifact", $"{Completions}.QuestCompletionByArtifact"),
		("ByQuest", $"{Completions}.QuestCompletionByQuest"),
		("ByHero", $"{Completions}.QuestCompletionByHero"),
		("ByCapitolBonus", $"{Completions}.QuestCompletionByCapitolBonus"),
		("ByArena", $"{Completions}.QuestCompletionByArena"),
		("ByArena3X3", $"{Completions}.QuestCompletionByArena3X3"),
		("ByLoot", $"{Completions}.QuestCompletionByLoot"),
		("ByMastery", $"{Completions}.QuestCompletionByMastery"),
		("ByFuse", $"{Completions}.QuestCompletionByFuse"),
		("ByAllianceActivity", $"{Completions}.QuestCompletionByAllianceActivity"),
		("BySoloEvent", $"{Completions}.QuestCompletionBySoloEvent"),
		("ByTournament", $"{Completions}.QuestCompletionByTournament"),
		("ByResourceSpent", $"{Completions}.QuestCompletionByResourceSpent"),
		("ByResourceGained", $"{Completions}.QuestCompletionByResourceGained"),
		("ByForge", $"{Completions}.QuestCompletionByForge"),
		("ByCvc", $"{Completions}.QuestCompletionByCvc"),
		("ByPowerUp", $"{Completions}.QuestCompletionByPowerUp"),
		("ByConditionalPromoCode", $"{Completions}.QuestCompletionByConditionalPromoCode"),
		("ByLiveArena", $"{Completions}.QuestCompletionByLiveArena"),
		("ByLiveArenaSeasons", $"{Completions}.QuestCompletionByLiveArenaSeasons"),
		("ByAreaBonus", $"{Completions}.QuestCompletionByByAreaBonus"),
		("BySiege", $"{Completions}.QuestCompletionBySiege"),
		("ByLeaderboardSeason", $"{Completions}.QuestCompletionByLeaderboardSeason"),
		("ByFoggyForest", $"{Completions}.QuestCompletionByFoggyForest"),
		("ByBattleSide", $"{Completions}.QuestCompletionByBattleSide"),
		("ByRelic", $"{Completions}.QuestCompletionByRelic"),
		("ByRelicStone", $"{Completions}.QuestCompletionByRelicStone"),
	];

	private ulong _quests;
	private ulong _openedStates;

	/// <summary>The Account's dictionary of quests has a count and free count that disagree with its slots, as a torn or corrupt read might see them.</summary>
	public void SetQuestCounts(int count, int freeCount) => _runtime.SetDictionaryCounts(_quests, count, freeCount);

	/// <summary>One of the open quests' states points somewhere else, as a bad read might see it.</summary>
	public void PointQuestState(int index, ulong address) => _heap.Pointer(_runtime.ListItem(_openedStates, index), address);

	/// <summary>
	/// UserWrapper's Quests, whose read-only base class declares the Quests, a dictionary of every quest by its id, and
	/// the QuestData, holding a QuestState for each open quest and the Mission groups open and finished; and the static
	/// data's QuestData, whose RecommendedQuestData puts each group in a section of the Missions screen.
	/// </summary>
	private void LayOutTheMissions(ulong userWrapperObject, SyntheticMissionData missionData)
	{
		const string GroupId = "SharedModel.Meta.Quests.RecommendedQuestGroupId";
		const string Groups = $"System.Collections.Generic.List`1<{GroupId}>";
		var systemObject = _runtime.SystemObject;

		var groupId = _runtime.Class("SharedModel.Meta.Quests", "RecommendedQuestGroupId", _runtime.ValueType);
		var groupList = _runtime.ListType(groupId, "RecommendedQuestGroupId", sizeof(int));
		ulong GroupList(IEnumerable<int> groups) => _runtime.List(groupList, [.. groups.Select(group => (ulong)group)]);

		var hero = Counts("QuestCompletionByHero", wide: false);
		var fuse = Counts("QuestCompletionByFuse", wide: false);
		var battle = Counts("QuestCompletionByBattle", wide: true);
		var quest = Declare(
			Completions,
			"QuestCompletionByQuest",
			systemObject,
			"System.Object",
			("RequiredPrototypeIds", "System.Collections.Generic.List`1<System.Int32>", GenericInstanceTypeCode, 0x10));
		var tasks = new Dictionary<string, (ulong Class, bool Wide)>(StringComparer.Ordinal)
		{
			["ByHero"] = (hero, false),
			["ByFuse"] = (fuse, false),
			["ByBattle"] = (battle, true),
			["ByQuest"] = (quest, false),
		};
		var completion = Declare(
			Completions,
			"QuestCompletion",
			systemObject,
			"System.Object",
			[("StateId", "SharedModel.Meta.Quests.QuestCompletionStateId", ValueTypeTypeCode, 0x10), .. CompletionKinds.Select((kind, index) => (kind.Field, kind.Type, ClassTypeCode, 0x18 + (index * 8)))]);
		var completionSize = 0x18 + (CompletionKinds.Length * 8);
		var questState = Declare(
			"SharedModel.Meta.Quests.Data",
			"QuestState",
			systemObject,
			"System.Object",
			("QuestId", "System.Int32", Int32TypeCode, 0x10),
			("PrototypeId", "System.Int32", Int32TypeCode, 0x14),
			("StateId", "SharedModel.Meta.Quests.QuestStateId", ValueTypeTypeCode, 0x18),
			("Completions", $"System.Collections.Generic.List`1<{Completions}.QuestCompletion>", GenericInstanceTypeCode, 0x20));

		var nullableCategory = _runtime.Class("System", "Nullable`1", _runtime.ValueType, new SyntheticField("hasValue", BooleanTypeCode, ObjectHeaderSize), new SyntheticField("value", ValueTypeTypeCode, ObjectHeaderSize + 0x4));
		var nullableGroup = _runtime.Class("System", "Nullable`1", _runtime.ValueType, new SyntheticField("hasValue", BooleanTypeCode, ObjectHeaderSize), new SyntheticField("value", ValueTypeTypeCode, ObjectHeaderSize + 0x4));
		var questClass = _runtime.Class(
			"SharedModel.Meta.Quests.Data",
			"Quest",
			systemObject,
			new SyntheticField("Id", Int32TypeCode, 0x10),
			new SyntheticField("PrototypeId", Int32TypeCode, 0x14),
			new SyntheticField("CategoryId", ValueTypeTypeCode, 0x18),
			new SyntheticField("Weight", Int32TypeCode, 0x1C),
			new SyntheticField("Name", ClassTypeCode, 0x20),
			new SyntheticField("RecommendedQuestCategoryId", GenericInstanceTypeCode, 0x28, GenericClass: _runtime.GenericClass(nullableCategory)),
			new SyntheticField("RecommendedQuestGroupId", GenericInstanceTypeCode, 0x30, GenericClass: _runtime.GenericClass(nullableGroup)));
		_metadata.Class(
			"SharedModel.Meta.Quests.Data.Quest",
			"System.Object",
			("Id", "System.Int32", false),
			("PrototypeId", "System.Int32", false),
			("CategoryId", "SharedModel.Meta.Quests.QuestCategoryId", false),
			("Weight", "System.Int32", false),
			("Name", "SharedModel.Common.Localization.SharedLTextKey", false),
			("RecommendedQuestCategoryId", "System.Nullable`1<SharedModel.Meta.Quests.RecommendedQuestCategoryId>", false),
			("RecommendedQuestGroupId", $"System.Nullable`1<{GroupId}>", false));
		var userRecommendedQuestData = Declare(
			"SharedModel.Meta.Quests.RecommendedQuests",
			"UserRecommendedQuestData",
			systemObject,
			"System.Object",
			("ActiveGroups", Groups, GenericInstanceTypeCode, 0x10),
			("CompletedGroups", Groups, GenericInstanceTypeCode, 0x18));
		var userQuestData = Declare(
			"SharedModel.Meta.Quests.Data",
			"UserQuestData",
			systemObject,
			"System.Object",
			("OpenedStates", "System.Collections.Generic.List`1<SharedModel.Meta.Quests.Data.QuestState>", GenericInstanceTypeCode, 0x10),
			("RecommendedQuestsData", "SharedModel.Meta.Quests.RecommendedQuests.UserRecommendedQuestData", ClassTypeCode, 0x18));
		var questWrapperReadOnly = Declare(
			"Client.Model.Gameplay.Quests",
			"QuestWrapperReadOnly",
			systemObject,
			"System.Object",
			("QuestData", "SharedModel.Meta.Quests.Data.UserQuestData", ClassTypeCode, 0x10),
			("Quests", "System.Collections.Generic.Dictionary`2<System.Int32, SharedModel.Meta.Quests.Data.Quest>", GenericInstanceTypeCode, 0x18));
		var questWrapper = Declare("Client.Model.Gameplay.Quests", "QuestWrapper", questWrapperReadOnly, "Client.Model.Gameplay.Quests.QuestWrapperReadOnly");

		var completionList = _runtime.ListType(completion, "QuestCompletion", sizeof(ulong));
		ulong Task(SyntheticTask task)
		{
			var (taskClass, wide) = tasks[task.Kind];
			var taskObject = _runtime.Object(taskClass, 0x20);
			if (task.Kind == "ByQuest")
			{
				return taskObject;
			}

			if (wide)
			{
				_heap.Int64(taskObject + 0x10, task.Required);
				_heap.Int64(taskObject + 0x18, task.Collected);
			}
			else
			{
				_heap.Int32(taskObject + 0x10, checked((int)task.Required));
				_heap.Int32(taskObject + 0x14, checked((int)task.Collected));
			}

			return taskObject;
		}

		var states = missionData.States.Select(state =>
		{
			var stateObject = _runtime.Object(questState, 0x28);
			_heap.Int32(stateObject + 0x10, state.QuestId);
			_heap.Int32(stateObject + 0x14, state.PrototypeId);
			_heap.Int32(stateObject + 0x18, state.StateId);
			if (state.Completions is { } completions)
			{
				_heap.Pointer(stateObject + 0x20, _runtime.List(completionList, [.. completions.Select(counted =>
				{
					var completionObject = _runtime.Object(completion, completionSize);
					foreach (var task in counted)
					{
						var index = Array.FindIndex(CompletionKinds, kind => kind.Field == task.Kind);
						_heap.Pointer(completionObject + 0x18 + (ulong)(index * 8), Task(task));
					}

					return completionObject;
				})]));
			}

			return stateObject;
		});
		_openedStates = _runtime.List(_runtime.ListType(questState, "QuestState", sizeof(ulong)), [.. states]);

		void Nullable(ulong address, int? value)
		{
			if (value is { } held)
			{
				_heap.Bytes(address, [1]);
				_heap.Int32(address + 4, held);
			}
		}

		_quests = _runtime.Dictionary(_runtime.DictionaryType(Int32TypeCode, ClassTypeCode), [.. missionData.Quests.Select(synthetic =>
		{
			var questObject = _runtime.Object(questClass, 0x38);
			_heap.Int32(questObject + 0x10, synthetic.Id);
			_heap.Int32(questObject + 0x14, synthetic.PrototypeId);
			_heap.Int32(questObject + 0x18, synthetic.CategoryId);
			_heap.Int32(questObject + 0x1C, synthetic.Weight);
			_heap.Pointer(questObject + 0x20, Text(synthetic.Name));
			Nullable(questObject + 0x28, synthetic.MissionCategory);
			Nullable(questObject + 0x30, synthetic.Group);
			return ((ulong)synthetic.Id, questObject);
		})]);

		var userRecommendedQuestDataObject = _runtime.Object(userRecommendedQuestData, 0x20);
		_heap.Pointer(userRecommendedQuestDataObject + 0x10, GroupList(missionData.OpenGroups));
		_heap.Pointer(userRecommendedQuestDataObject + 0x18, GroupList(missionData.CompletedGroups));
		var userQuestDataObject = _runtime.Object(userQuestData, 0x20);
		_heap.Pointer(userQuestDataObject + 0x10, _openedStates);
		Slot("SharedModel.Meta.Quests.Data.UserQuestData.RecommendedQuestsData", userQuestDataObject + 0x18, userRecommendedQuestDataObject);
		var questWrapperObject = _runtime.Object(questWrapper, 0x20);
		_heap.Pointer(questWrapperObject + 0x10, userQuestDataObject);
		_heap.Pointer(questWrapperObject + 0x18, _quests);
		Slot("Client.Model.Guard.UserWrapper.Quests", userWrapperObject + 0x70, questWrapperObject);

		var recommendedQuestData = Declare(
			"SharedModel.Meta.Quests.RecommendedQuests",
			"RecommendedQuestData",
			systemObject,
			"System.Object",
			("GroupsBySection", $"System.Collections.Generic.Dictionary`2<SharedModel.Meta.Quests.RecommendedQuestSectionId, {Groups}>", GenericInstanceTypeCode, 0x10));
		var staticQuestData = Declare(
			"SharedModel.Meta.Quests.Data",
			"StaticQuestData",
			systemObject,
			"System.Object",
			("RecommendedQuestData", "SharedModel.Meta.Quests.RecommendedQuests.RecommendedQuestData", ClassTypeCode, 0x10));
		var recommendedQuestDataObject = _runtime.Object(recommendedQuestData, 0x18);
		_heap.Pointer(
			recommendedQuestDataObject + 0x10,
			_runtime.Dictionary(_runtime.DictionaryType(ValueTypeTypeCode, ClassTypeCode), [.. missionData.GroupsBySection.Select(section => ((ulong)section.Key, GroupList(section.Value)))]));
		var staticQuestDataObject = _runtime.Object(staticQuestData, 0x18);
		_heap.Pointer(staticQuestDataObject + 0x10, recommendedQuestDataObject);
		Slot("SharedModel.Meta.StaticData.QuestData", _clientStaticData + 0x68, staticQuestDataObject);

		ulong Counts(string name, bool wide) =>
			Declare(
				Completions,
				name,
				systemObject,
				"System.Object",
				("CountRequired", wide ? "System.Int64" : "System.Int32", wide ? Int64TypeCode : Int32TypeCode, 0x10),
				("CountCollected", wide ? "System.Int64" : "System.Int32", wide ? Int64TypeCode : Int32TypeCode, wide ? 0x18 : 0x14));
	}
}
