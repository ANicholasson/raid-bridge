using RaidBridge.Domain.Arena;
using RaidBridge.Domain.Champions;
using RaidBridge.Domain.Stats;
using RaidBridge.Game.Il2Cpp;
using RaidBridge.Game.Metadata;
using RaidBridge.Game.Numerics;

namespace RaidBridge.Game.Arena;

/// <summary>
/// Reads the Account's Classic and Tag Arena from the game's model (spec #32, #37). The Classic Arena is
/// <c>_userWrapper.Arena.ArenaData</c>, its league and defence team in <c>ArenaDefenseTeamSetup</c>; the Tag Arena is
/// <c>_userWrapper.Arena3x3.Data</c>, a <c>UserArenaData</c> too, its league in <c>LeagueId</c> and its defence teams in
/// <c>Arena3X3DefenseTeamSetup</c>. Each team's Champions are its <c>HeroSlotSetups</c>' <c>InventoryHeroId</c>s. Raid
/// Toolkit's ArenaExtension named the routes, which the installed game still declares at metadata v39. Each Champion
/// in a team must be a Champion on the Account, defending only once in that Arena. The Live Arena is
/// <c>_userWrapper.LiveArena.LiveArenaData</c>, its points, wins and losses, its league worked out from its points
/// (https://github.com/ANicholasson/raid-bridge/issues/32).
/// </summary>
internal static class ArenaReader
{
	private const string ArenaData = "SharedModel.Meta.Battle.Arena.UserArenaData";
	private const string League = "SharedModel.Meta.Battle.Arena.ArenaLeagueId";
	private const string TeamSetup = "SharedModel.Battle.Core.Setup.TeamSetup";
	private const string HeroSlotSetup = "SharedModel.Battle.Core.Setup.HeroSlotSetup";

	/// <summary>The league's member for "no league", which is written as null, as an Artifact's set <c>None</c> is.</summary>
	private const string NoLeague = "None";

	/// <summary>
	/// The Classic Arena as read once, so the league written and the bonus it gives every Champion come from the same
	/// state of the Game client: the league, points, this week's counts, each defence position's
	/// <c>InventoryHeroId</c>, and whether <c>DefenseHeroIds</c> names any Champion.
	/// </summary>
	public static ClassicArenaState Capture(ManagedObject userWrapper)
	{
		const string Arena = "the Classic Arena";
		var data = userWrapper
			.Object("Arena", "Client.Model.Gameplay.Battle.ArenaWrapper")
			.Object("ArenaData", ArenaData);
		var (battles, wins, losses) = Week(data, Arena);
		var team = data.ObjectOrNull("ArenaDefenseTeamSetup", TeamSetup);
		// BaseArenaExtensions.HasDefense: any id but -1 in DefenseHeroIds.
		var defenseHeroIds = data.ArrayOrNull("DefenseHeroIds", "System.Int32[]");
		return new(
			team?.EnumMember("ArenaLeagueId", League),
			data.Int64("ArenaPoints").Value,
			(battles, wins, losses),
			team is null ? null : Positions(team),
			defenseHeroIds is not null && Enumerable.Range(0, defenseHeroIds.Length).Any(index => defenseHeroIds.Int32(index).Value != -1));
	}

	public static (ClassicArena Classic, TagArena Tag, LiveArena Live) Read(ClassicArenaState classic, ManagedObject userWrapper, ManagedObject staticData, IReadOnlyList<Champion> champions)
	{
		var championIds = champions.Select(champion => champion.Id).ToHashSet();
		return (Classic(classic, new(championIds)), Tag(userWrapper, new(championIds)), Live(userWrapper, staticData));
	}

	/// <summary>
	/// What the Classic Arena league gives every Champion, as the champion screen counts it at metadata v39
	/// (https://github.com/ANicholasson/raid-bridge/issues/20): nothing unless the Account has a defence team
	/// (<c>ArenaWrapperReadOnly.HasDefence</c>), and otherwise the league's <c>ArenaLeagueInfo.BattleBonuses</c> in
	/// <c>StaticData.ArenaData.LeagueInfos</c>, each a fraction of the Base stat. <c>CalcPercentageStats</c> takes no
	/// Arena term, so a bonus to a critical stat would count for nothing, and is left out.
	/// </summary>
	public static IReadOnlyList<StatBonus> LeagueBonuses(ClassicArenaState classic, ManagedObject staticData)
	{
		if (!classic.HasDefence)
		{
			return [];
		}

		// The game holds the league on the defence team's setup, so a defence with no setup has no league to count.
		var league = classic.League
			?? throw new GameDataException(
				"The Classic Arena's SharedModel.Meta.Battle.Arena.UserArenaData.DefenseHeroIds names a Champion, but its ArenaDefenseTeamSetup is null, so raid-bridge has no league to count.");
		if (league.Value.Name == NoLeague)
		{
			return [];
		}

		const string LeagueInfo = "SharedModel.Meta.Battle.Arena.ArenaLeagueInfo";
		var infos = staticData
			.Object("ArenaData", "SharedModel.Meta.Battle.Arena.StaticStandardArenaData")
			.List("LeagueInfos", $"System.Collections.Generic.List`1<{LeagueInfo}>")
			.Objects(LeagueInfo)
			.Where(info => info.EnumMember("Id", League).Value.Value == league.Value.Value)
			.ToList();
		var bonuses = infos.Count == 1
			? infos[0].Object("BattleBonuses", "SharedModel.Meta.Heroes.BattleStats")
			: throw new GameDataException(
				$"{league.Field} at 0x{league.Address:X} is {league.Value.Name}, in the Classic Arena, which SharedModel.Meta.Battle.Arena.StaticStandardArenaData.LeagueInfos holds {infos.Count} times, where raid-bridge expects once.");
		return [.. new[] { Stat.Health, Stat.Attack, Stat.Defence, Stat.Speed, Stat.Resistance, Stat.Accuracy }
			.Select(stat => new StatBonus(stat, FixedPoint.Of(bonuses, stat.ToString()), Flat: false))
			.Where(bonus => bonus.Value != 0)];
	}

	/// <summary>An Account that has set no defence team holds no <c>ArenaDefenseTeamSetup</c>, and so no league either.</summary>
	private static ClassicArena Classic(ClassicArenaState classic, Defenders defenders) =>
		new(
			classic.League is { } league ? LeagueName(league.Value.Name) : null,
			classic.Points,
			classic.Week.Battles,
			classic.Week.Wins,
			classic.Week.Losses,
			classic.DefencePositions is { } positions ? Team(positions, "the Classic Arena's defence team", defenders) : new([]));

	/// <summary>An Account that holds no <c>Arena3X3DefenseTeamSetup</c> has no defence teams.</summary>
	private static TagArena Tag(ManagedObject userWrapper, Defenders defenders)
	{
		const string Arena = "the Tag Arena";
		var data = userWrapper
			.Object("Arena3x3", "Client.Model.Gameplay.Arena3x3.Arena3x3Wrapper")
			.Object("Data", "Client.Model.Gameplay.Battle.Data.UpdatableArena3x3Data");
		var (battles, wins, losses) = Week(data, Arena);
		var teams = data.ListOrNull("Arena3X3DefenseTeamSetup", $"System.Collections.Generic.List`1<{TeamSetup}>")?.Objects(TeamSetup) ?? [];
		return new(
			LeagueName(data.Enum("LeagueId", League).Value),
			data.Int64("ArenaPoints").Value,
			battles,
			wins,
			losses,
			[.. teams.Select((team, index) => Team(Positions(team), $"team {index + 1} of {Arena}'s defence", defenders))]);
	}

	private static string? LeagueName(string league) => league == NoLeague ? null : league;

	/// <summary>
	/// The Live Arena's points, wins and losses, each 0 or more, and its league. The game holds no league for the Live
	/// Arena: <c>LiveArenaExtensions.CalcLeagueId</c> works it out from the points each time
	/// (<c>GetLeagueIdByPoints</c>, RVA 0x2EFE070 at metadata v39), walking the static data's
	/// <c>LiveArenaData.LeagueInfoById</c> in its order and taking each league whose <c>MinPoints</c> the points reach,
	/// until one they don't. raid-bridge takes the last league the points reach, the same league as long as each
	/// league needs more points than the one before it, which it checks rather than trusts.
	/// </summary>
	private static LiveArena Live(ManagedObject userWrapper, ManagedObject staticData)
	{
		const string Arena = "the Live Arena";
		var data = userWrapper
			.Object("LiveArena", "Client.Model.Gameplay.LiveArena.LiveArenaWrapper")
			.Object("LiveArenaData", "Client.Model.Gameplay.LiveArena.UpdatableUserLiveArenaData");
		var points = data.Int64("Points");
		if (points.Value < 0)
		{
			throw new GameDataException($"{points}, in {Arena}, where raid-bridge expects 0 or more.");
		}

		return new(LiveLeague(points, staticData), points.Value, Count(data, "Victories", Arena).Value, Count(data, "Defeats", Arena).Value);
	}

	/// <summary>
	/// The league whose <c>MinPoints</c> is the highest the points reach. The game starts its walk at BronzeI, its code's
	/// literal 1, so points below every league's are BronzeI to it, and to raid-bridge too (code review,
	/// https://github.com/ANicholasson/raid-bridge/issues/32): an Account starts at BronzeI's own 900 points at metadata
	/// v39, and refusing such points would refuse the whole Snapshot over a league the screen does show.
	/// </summary>
	private static string LiveLeague(FieldValue<long> points, ManagedObject staticData)
	{
		const string LiveLeagueId = "SharedModel.Meta.LiveArena.LiveArenaLeagueId";
		const string LeagueInfo = "SharedModel.Meta.LiveArena.LiveArenaLeagueInfo";
		const string Leagues = "SharedModel.Meta.LiveArena.StaticLiveArenaData.LeagueInfoById";
		// LiveArenaExtensions.GetLeagueIdByPoints starts from the literal 1, BronzeI's number.
		var reached = "BronzeI";
		FieldValue<int>? previous = null;
		foreach (var (league, entry) in staticData
			.Object("LiveArenaData", "SharedModel.Meta.LiveArena.StaticLiveArenaData")
			.Dictionary("LeagueInfoById", $"System.Collections.Generic.Dictionary`2<{LiveLeagueId}, {LeagueInfo}>")
			.ByEnumKey(LiveLeagueId))
		{
			var minPoints = entry.Object(LeagueInfo).Int32("MinPoints");
			if (previous is not null && minPoints.Value <= previous.Value)
			{
				throw new GameDataException($"{minPoints}, for {league.Name}, but the league before it in {Leagues} needs {previous.Value}, where raid-bridge expects each league to need more points than the one before it.");
			}

			previous = minPoints;
			if (points.Value >= minPoints.Value)
			{
				reached = league.Name;
			}
		}

		return reached;
	}

	/// <summary>
	/// This week's battles, wins and losses, each 0 or more. Wins and losses aren't checked against battles: on the main
	/// Account the Classic Arena held 83 wins and 56 losses beside 66 battles started (2026-11-01), so they count more
	/// than the battles the player started, such as those fought on defence.
	/// </summary>
	private static (int Battles, int Wins, int Losses) Week(ManagedObject data, string arena) =>
		(Count(data, "BattlesStartedThisWeek", arena).Value, Count(data, "VictoriesThisWeek", arena).Value, Count(data, "LossesThisWeek", arena).Value);

	private static FieldValue<int> Count(ManagedObject data, string fieldName, string arena)
	{
		var count = data.Int32(fieldName);
		return count.Value >= 0 ? count : throw new GameDataException($"{count}, in {arena}, where raid-bridge expects 0 or more.");
	}

	/// <summary>Each position's <c>InventoryHeroId</c>, in the team's order.</summary>
	private static IReadOnlyList<FieldValue<int>> Positions(ManagedObject team) =>
		[.. team
			.List("HeroSlotSetups", $"System.Collections.Generic.List`1<{HeroSlotSetup}>")
			.Objects(HeroSlotSetup)
			.Select(position => position.Int32("InventoryHeroId"))];

	/// <summary>A position whose <c>InventoryHeroId</c> is 0 holds no Champion, and is left out.</summary>
	private static ArenaTeam Team(IReadOnlyList<FieldValue<int>> positions, string where, Defenders defenders) =>
		new([.. positions.Where(position => position.Value != 0).Select(position => defenders.Place(position, where))]);

	/// <summary>One Arena's defenders placed so far, each checked against the Account's Champions and the teams before it.</summary>
	private sealed class Defenders(IReadOnlySet<int> championIds)
	{
		private readonly Dictionary<int, string> _placed = [];

		public int Place(FieldValue<int> position, string team)
		{
			var id = position.Value;
			if (!championIds.Contains(id))
			{
				throw new GameDataException($"{position}, in {team}, but HeroById holds no Champion {id}.");
			}

			return _placed.TryAdd(id, team)
				? id
				: throw new GameDataException($"{position}, in {team}, but Champion {id} already defends in {_placed[id]}.");
		}
	}
}

/// <summary>The Classic Arena as read once: its league, if any, points, this week's counts, its defence positions, if it has a team, and whether it has a defence.</summary>
internal sealed record ClassicArenaState(
	FieldValue<GameEnumMember>? League,
	long Points,
	(int Battles, int Wins, int Losses) Week,
	IReadOnlyList<FieldValue<int>>? DefencePositions,
	bool HasDefence);
