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
		var held = userWrapper
			.Object("Heroes", "Client.Model.Gameplay.Heroes.HeroesWrapper")
			.Object("HeroData", "Client.Model.Gameplay.Heroes.Data.UpdatableHeroData")
			.ListOrNull("HeroesAiPresets", $"System.Collections.Generic.List`1<{Preset}>")?
			.Objects(Preset) ?? [];
		if (held.Count == 0)
		{
			return [];
		}

		var purposes = new Purposes(userWrapper.Runtime, staticData);
		var championIds = champions.Select(champion => champion.Id).ToHashSet();
		var teams = new List<HeldTeam>();
		var ids = new HashSet<int>();
		foreach (var heldTeam in held)
		{
			var team = Team(heldTeam, purposes, championIds);
			if (!ids.Add(team.Id.Value))
			{
				throw new GameDataException($"{team.Id}, but another Saved team in HeroesAiPresets has that id too.");
			}

			teams.Add(team);
		}

		return [.. teams.GroupBy(team => team.Purpose.Order).OrderBy(purpose => purpose.Key).SelectMany(InScreenOrder).Select(team => team.Team)];
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
		foreach (var team in teams)
		{
			if (!positions.Add(team.Position.Value))
			{
				throw new GameDataException($"{team.Position}, but another {team.Purpose.Name} Saved team in HeroesAiPresets is at that position too, so raid-bridge can't tell their order.");
			}
		}

		return teams.OrderBy(team => team.Position.Value);
	}

	private static HeldTeam Team(ManagedObject heldTeam, Purposes purposes, HashSet<int> championIds)
	{
		var id = heldTeam.Int32("Id");
		var purpose = purposes.Of(id);
		var position = heldTeam.Int32("Position");
		// The game refuses to save a team at a position outside 0 to its purpose's slot count (AssertPresetPositionIsValid, RVA 0x2F41E70).
		_ = position.Where(value => value >= 0 && value <= purpose.MaxTeams, $"a position from 0 to {purpose.MaxTeams}, the number of {purpose.Name} Saved teams the static data's HeroesAiPresetInfo.MaxPresetsCount allows");
		var where = $"the {purpose.Name} Saved team at 0x{heldTeam.Address:X}";
		// A list the game has emptied out can be null before it removes the team (RemoveHeroFromAiPreset, RVA 0x2F44CD0).
		var setups = heldTeam.ListOrNull("SkillPrioritiesSetups", $"System.Collections.Generic.List`1<{Setup}>")?.ObjectsOrNull(Setup) ?? [];
		var seen = new HashSet<int>();
		var championsInTeam = new List<int>();
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

			championsInTeam.Add(heroId.Value);
		}

		if (championsInTeam.Count > purpose.MaxChampions)
		{
			throw new GameDataException($"{where} holds {championsInTeam.Count} Champions, where a {purpose.Name} team takes at most {purpose.MaxChampions}.");
		}

		return new(id, position, purpose, new(purpose.Name, heldTeam.Boolean("NameIsNotDefault").Value, championsInTeam));
	}

	/// <summary>A Saved team with what places it on its team screen: its purpose, its position and its id.</summary>
	private sealed record HeldTeam(FieldValue<int> Id, FieldValue<int> Position, TeamPurpose Purpose, SavedTeam Team);

	/// <summary>
	/// What a Saved team is for, under the game's own name; that purpose's number in <c>HeroesAiPresetType</c>, which
	/// orders the screens; and how many Champions a team of it takes, and how many such teams the Account can save.
	/// </summary>
	private sealed record TeamPurpose(string Name, long Order, int MaxChampions, int MaxTeams);

	/// <summary>
	/// What a Saved team is for, and how many Champions and teams such a purpose takes, as the game works them out at
	/// metadata v39. <c>HeroesAiPreset.get_Type</c> first counts the ids <c>HeroesAiPresetIds.IsArenaDefencePreset</c>
	/// (RVA 0x2F46930) matches as Pvp: 0, <c>ArenaDefence</c> and <c>Arena3x3DefenceMin</c> to <c>Arena3x3DefenceMax</c>.
	/// Such an id is an Arena defence team, not a Saved team, and is refused here. Otherwise it takes the first of the
	/// static <c>TypeRanges</c> whose start the id reaches, the largest start first. The static constructor that fills
	/// <c>TypeRanges</c> runs only once the game first asks a team's type, which nothing does while it loads the Account,
	/// so the ranges are made here from the game's own <c>HeroesAiPresetIds</c> constants, as that constructor makes
	/// them, each paired with its purpose by name. Where <c>TypeRanges</c> is filled, its answer must be the same. A
	/// purpose the game has and raid-bridge can't pair is refused even if no team uses it, since its ids could start
	/// anywhere, inside another purpose's. The static data's <c>HeroData.PresetInfoByType</c> gives each purpose's
	/// <c>MaxHeroesCount</c> and <c>MaxPresetsCount</c>.
	/// </summary>
	private sealed class Purposes
	{
		private const string Info = "SharedModel.Meta.Heroes.HeroesAiPresetInfo";

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
		private readonly Dictionary<string, ManagedObject> _infos;
		private readonly (long ArenaDefence, long TagMin, long TagMax) _arenaDefence;

		public Purposes(Il2CppRuntime game, ManagedObject staticData)
		{
			var metadata = game.Metadata;
			_order = metadata.Enum(PresetType).Members.ToDictionary(member => member.Name, member => member.Value);
			var unknown = _order.Keys.Except(Starts.Select(start => start.Purpose)).ToList();
			if (unknown.Count > 0)
			{
				throw new GameDataException($"The game's {PresetType} has {string.Join(" and ", unknown)}, a purpose raid-bridge doesn't know the ids of.");
			}

			var missing = Starts.Where(start => !_order.ContainsKey(start.Purpose)).Select(start => start.Purpose).ToList();
			if (missing.Count > 0)
			{
				throw new GameDataException($"The game's {PresetType} has no {string.Join(" or ", missing)}, a purpose raid-bridge pairs with one of its {PresetIds}.");
			}

			_ranges = [.. Starts.Select(start => (metadata.Constant(PresetIds, start.Constant), start.Purpose)).OrderByDescending(range => range.Item1)];
			_arenaDefence = (metadata.Constant(PresetIds, "ArenaDefence"), metadata.Constant(PresetIds, "Arena3x3DefenceMin"), metadata.Constant(PresetIds, "Arena3x3DefenceMax"));
			_gameRanges = GameRanges(game);
			_infos = staticData
				.Object("HeroData", "SharedModel.Meta.Heroes.StaticHeroData")
				.Dictionary("PresetInfoByType", $"System.Collections.Generic.Dictionary`2<{PresetType}, {Info}>")
				.ByEnumKey(PresetType)
				.ToDictionary(info => info.Key.Name, info => info.Value.Object(Info));
		}

		/// <summary>The purpose of the team with this id.</summary>
		public TeamPurpose Of(FieldValue<int> id)
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

			var info = _infos.TryGetValue(purpose, out var found)
				? found
				: throw new GameDataException($"{id}, a {purpose} Saved team, but the static data's StaticHeroData.PresetInfoByType holds no {purpose}, so raid-bridge can't tell how many Champions it takes.");
			return new(
				purpose,
				_order[purpose],
				info.Int32("MaxHeroesCount").Where(count => count >= 1, "1 or more"),
				info.Int32("MaxPresetsCount").Where(count => count >= 1, "1 or more"));
		}

		/// <summary>
		/// <c>HeroesAiPreset.TypeRanges</c> in its order, or null while the game hasn't filled it. It is asked only once
		/// <c>HeroesAiPresets</c> holds a team, and by then the game has set up the class and its static fields:
		/// <c>HeroesAiPresetFormatter.Deserialize</c> (RVA 0x2850E70) resolves <c>HeroesAiPreset</c>'s TypeInfo slot and
		/// makes each team with il2cpp's object_new, which initialises the class, static fields and all, before it
		/// allocates. Only <c>TypeRanges</c> itself is still null until the static constructor runs.
		/// </summary>
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
