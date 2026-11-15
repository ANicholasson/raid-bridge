using RaidBridge.Domain.Champions;
using RaidBridge.Domain.SavedTeams;
using RaidBridge.Game.Il2Cpp;

namespace RaidBridge.Game.SavedTeams;

/// <summary>
/// Reads the Account's Saved teams (https://github.com/ANicholasson/raid-bridge/issues/57) from
/// <c>_userWrapper.Heroes.HeroData.HeroesAiPresets</c>, a list of <c>HeroesAiPreset</c>s, as the research in
/// <c>docs/research/2026-11-saved-teams.md</c> found them at metadata v39. Each team's Champions are its
/// <c>SkillPrioritiesSetups</c>' <c>HeroId</c>s, in the order of its positions; a null entry is an empty position, which
/// the game can hold. The game removes a Champion that leaves the Account from every Saved team
/// (<c>HeroesAiPresetExtensions.RemoveHeroFromAiPresets</c>, RVA 0x2F45150), so each must be a Champion on the Account.
/// What a team is for comes from its id, as <c>HeroesAiPreset.get_Type</c> (RVA 0x2F471F0) works it out.
/// </summary>
internal static class SavedTeamReader
{
	private const string Preset = "SharedModel.Meta.Heroes.HeroesAiPreset";
	private const string Setup = "SharedModel.Meta.Heroes.SkillPrioritiesSetup";
	private const string PresetType = "SharedModel.Meta.Heroes.HeroesAiPresetType";
	private const string PresetIds = "SharedModel.Meta.Heroes.HeroesAiPresetIds";

	/// <summary>
	/// The teams in the order the game's team screens show them. Each screen shows one purpose, so the purposes come in
	/// the order of the game's <c>HeroesAiPresetType</c>. Within a purpose, <c>HeroesAiPresetExtensions.Descriptors</c>
	/// (RVA 0x387C310) puts the team at each <c>Position</c> in that slot, counted from 1 within the purpose; if any of
	/// the purpose's teams is at 0, it puts each team in the slot its id gives instead (RVA 0x3880000), which is id order.
	/// An Account the game holds no list of Saved teams for has none, as one with an empty list has none.
	/// </summary>
	public static IReadOnlyList<SavedTeam> Read(ManagedObject userWrapper, ManagedObject staticData, IReadOnlyList<Champion> champions)
	{
		var presets = userWrapper
			.Object("Heroes", "Client.Model.Gameplay.Heroes.HeroesWrapper")
			.Object("HeroData", "Client.Model.Gameplay.Heroes.Data.UpdatableHeroData")
			.ListOrNull("HeroesAiPresets", $"System.Collections.Generic.List`1<{Preset}>")?
			.Objects(Preset) ?? [];
		if (presets.Count == 0)
		{
			return [];
		}

		var purposes = new Purposes(userWrapper.Runtime, staticData);
		var championIds = champions.Select(champion => champion.Id).ToHashSet();
		var held = new List<HeldTeam>();
		var ids = new HashSet<int>();
		foreach (var preset in presets)
		{
			var team = Team(preset, purposes, championIds);
			if (!ids.Add(team.Id.Value))
			{
				throw new GameDataException($"{team.Id}, but another Saved team in HeroesAiPresets has that id too.");
			}

			held.Add(team);
		}

		return [.. held.GroupBy(team => team.PurposeOrder).OrderBy(purpose => purpose.Key).SelectMany(InScreenOrder).Select(team => team.Team)];
	}

	/// <summary>One purpose's teams by <c>Position</c>, or by id when any of them is at 0, as its team screen shows them.</summary>
	private static IEnumerable<HeldTeam> InScreenOrder(IEnumerable<HeldTeam> purpose)
	{
		var teams = purpose.ToList();
		if (teams.Any(team => team.Position.Value == 0))
		{
			return teams.OrderBy(team => team.Id.Value);
		}

		var positions = new HashSet<int>();
		foreach (var team in teams.Where(team => !positions.Add(team.Position.Value)))
		{
			throw new GameDataException($"{team.Position}, but another {team.Team.Purpose} Saved team in HeroesAiPresets is at that position too, so raid-bridge can't tell their order.");
		}

		return teams.OrderBy(team => team.Position.Value);
	}

	private static HeldTeam Team(ManagedObject preset, Purposes purposes, HashSet<int> championIds)
	{
		var id = preset.Int32("Id");
		var (purpose, purposeOrder, maxChampions) = purposes.Of(id);
		var position = preset.Int32("Position");
		_ = position.Where(value => value >= 0, "a position of 0 or more");
		var where = $"the {purpose} Saved team at 0x{preset.Address:X}";
		// A list the game has emptied out can be null before it removes the team (RemoveHeroFromAiPreset, RVA 0x2F44CD0).
		var setups = preset.ListOrNull("SkillPrioritiesSetups", $"System.Collections.Generic.List`1<{Setup}>")?.ObjectsOrNull(Setup) ?? [];
		var seen = new HashSet<int>();
		var team = new List<int>();
		foreach (var heroId in setups.OfType<ManagedObject>().Select(setup => setup.Int32("HeroId")))
		{
			if (!championIds.Contains(heroId.Value))
			{
				throw new GameDataException($"{heroId}, in {where}, but HeroById holds no Champion {heroId.Value}.");
			}

			if (!seen.Add(heroId.Value))
			{
				throw new GameDataException($"{heroId}, in {where}, but Champion {heroId.Value} is already in it.");
			}

			team.Add(heroId.Value);
		}

		if (team.Count > maxChampions)
		{
			throw new GameDataException($"{where} holds {team.Count} Champions, where a {purpose} team takes at most {maxChampions}.");
		}

		return new(id, position, purposeOrder, new(purpose, preset.Boolean("NameIsNotDefault").Value, team));
	}

	/// <summary>A Saved team with what places it on its team screen: its purpose's number, its position and its id.</summary>
	private sealed record HeldTeam(FieldValue<int> Id, FieldValue<int> Position, long PurposeOrder, SavedTeam Team);

	/// <summary>
	/// What a Saved team is for, and how many Champions such a team takes, as the game works them out at metadata v39.
	/// <c>HeroesAiPreset.get_Type</c> first counts the ids <c>HeroesAiPresetIds.IsArenaDefencePreset</c> (RVA 0x2F46930)
	/// matches as Pvp: 0, <c>ArenaDefence</c> and <c>Arena3x3DefenceMin</c> to <c>Arena3x3DefenceMax</c>. Such an id is an
	/// Arena defence team, not a Saved team, and is refused here. Otherwise it takes the first of the static
	/// <c>TypeRanges</c> whose start the id reaches, the largest start first. The static constructor that fills
	/// <c>TypeRanges</c> runs only once the game first asks a team's type, which nothing does while it loads the Account,
	/// so the ranges are made here from the game's own <c>HeroesAiPresetIds</c> constants, as that constructor makes
	/// them, each paired with its purpose by name. Where <c>TypeRanges</c> is filled, its answer must be the same. The
	/// static data's <c>HeroData.PresetInfoByType</c> gives each purpose's <c>MaxHeroesCount</c>.
	/// </summary>
	private sealed class Purposes
	{
		/// <summary>Each <c>HeroesAiPresetIds</c> constant that starts a purpose's ids, and that purpose, as <c>HeroesAiPreset</c>'s static constructor (RVA 0x2F46CD0) pairs them.</summary>
		private static readonly (string Constant, string Purpose)[] Starts =
		[
			("GeneralPveIdMin", "GeneralPve"),
			("PvpIdMin", "Pvp"),
			("FractionsIdMin", "Fractions"),
			("DoomTowerIdMin", "DoomTower"),
			("AllianceHydraIdMin", "Hydra"),
			("AllianceChimeraIdMin", "Chimera"),
			("CooperationEventWorldBossIdMin", "CooperationEventWorldBoss"),
			("SiegeIdMin", "Siege"),
			("IronTwinsIdMin", "IronTwins"),
			("PotionKeepsIdMin", "PotionKeeps"),
			("ArtifactAscendAndMasteriesIdMin", "ArtifactAscendAndMasteries"),
			("EventDungeonIdMin", "EventDungeon"),
			("ArtifactDungeonsIdMin", "ArtifactDungeons"),
		];

		private readonly Dictionary<string, long> _order;
		private readonly List<(long Start, string Purpose)> _ranges;
		private readonly List<(int Start, string Purpose)>? _gameRanges;
		private readonly Dictionary<string, FieldValue<int>> _maxChampions;
		private readonly (long ArenaDefence, long TagMin, long TagMax) _arenaDefence;

		public Purposes(Il2CppRuntime game, ManagedObject staticData)
		{
			var metadata = game.Metadata;
			var members = metadata.Enum(PresetType).Members;
			var unknown = members.Select(member => member.Name).Except(Starts.Select(start => start.Purpose)).ToList();
			if (unknown.Count > 0)
			{
				throw new GameDataException($"The game's {PresetType} has {string.Join(" and ", unknown)}, a purpose raid-bridge doesn't know the ids of.");
			}

			_order = members.ToDictionary(member => member.Name, member => member.Value);
			_ranges = [.. Starts.Select(start => (metadata.Constant(PresetIds, start.Constant), start.Purpose)).OrderByDescending(range => range.Item1)];
			_arenaDefence = (metadata.Constant(PresetIds, "ArenaDefence"), metadata.Constant(PresetIds, "Arena3x3DefenceMin"), metadata.Constant(PresetIds, "Arena3x3DefenceMax"));
			_gameRanges = GameRanges(game);
			_maxChampions = staticData
				.Object("HeroData", "SharedModel.Meta.Heroes.StaticHeroData")
				.Dictionary("PresetInfoByType", $"System.Collections.Generic.Dictionary`2<{PresetType}, SharedModel.Meta.Heroes.HeroesAiPresetInfo>")
				.ByEnumKey(PresetType)
				.ToDictionary(info => info.Key.Name, info => info.Value.Object("SharedModel.Meta.Heroes.HeroesAiPresetInfo").Int32("MaxHeroesCount"));
		}

		/// <summary>What the team with this id is for, that purpose's number in <c>HeroesAiPresetType</c>, and how many Champions it takes.</summary>
		public (string Purpose, long Order, int MaxChampions) Of(FieldValue<int> id)
		{
			var (arenaDefence, tagMin, tagMax) = _arenaDefence;
			if (id.Value == 0 || id.Value == arenaDefence || (id.Value >= tagMin && id.Value <= tagMax))
			{
				throw new GameDataException($"{id}, in HeroesAiPresets, an id the game keeps for an Arena defence team, where raid-bridge expects a Saved team.");
			}

			var purpose = _ranges.FirstOrDefault(range => id.Value >= range.Start).Purpose
				?? throw new GameDataException($"{id}, in HeroesAiPresets, below the first id of every purpose in {PresetIds}, so raid-bridge can't tell what the team is for.");
			if (_gameRanges is not null && _gameRanges.FirstOrDefault(range => id.Value >= range.Start).Purpose is var held && held != purpose)
			{
				throw new GameDataException($"{id}, in HeroesAiPresets, is {held ?? "nothing"} by the game's {Preset}.TypeRanges, but {purpose} by its {PresetIds}, which raid-bridge pairs as the game's code did at metadata v39.");
			}

			var maxChampions = _maxChampions.TryGetValue(purpose, out var max)
				? max.Where(count => count >= 1, "1 or more")
				: throw new GameDataException($"{id}, a {purpose} Saved team, but the static data's StaticHeroData.PresetInfoByType holds no {purpose}, so raid-bridge can't tell how many Champions it takes.");
			return (purpose, _order.TryGetValue(purpose, out var order)
				? order
				: throw new GameDataException($"{id}, a {purpose} Saved team by the game's {PresetIds}, but its {PresetType} has no {purpose}."), maxChampions);
		}

		/// <summary><c>HeroesAiPreset.TypeRanges</c> in its order, or null while the game hasn't filled it.</summary>
		private static List<(int Start, string Purpose)>? GameRanges(Il2CppRuntime game)
		{
			const string Range = "System.ValueTuple`2";
			if (game.StaticObjectOrNull(Preset, "TypeRanges", $"{Range}<System.Int32, {PresetType}>[]", "System.Array") is not { } ranges)
			{
				return null;
			}

			var array = new ManagedArray(ranges, $"{Preset}.TypeRanges");
			return [.. Enumerable.Range(0, array.Length).Select(index =>
			{
				var range = array.Value(index, Range);
				return (range.Int32("Item1", "T1").Value, range.EnumMember("Item2", PresetType, declaredType: "T2").Value.Name);
			})];
		}
	}
}
