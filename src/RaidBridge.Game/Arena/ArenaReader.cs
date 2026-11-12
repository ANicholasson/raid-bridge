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
/// in a team must be a Champion on the Account, defending only once in that Arena.
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
	public static ClassicCapture Capture(ManagedObject userWrapper)
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
			team?.EnumMember("ArenaLeagueId", League).Value,
			data.Int64("ArenaPoints").Value,
			(battles, wins, losses),
			team is null ? null : Positions(team),
			defenseHeroIds is not null && Enumerable.Range(0, defenseHeroIds.Length).Any(index => defenseHeroIds.Int32(index).Value != -1));
	}

	public static (ClassicArena Classic, TagArena Tag) Read(ClassicCapture classic, ManagedObject userWrapper, IReadOnlyList<Champion> champions)
	{
		var championIds = champions.Select(champion => champion.Id).ToHashSet();
		return (Classic(classic, new(championIds)), Tag(userWrapper, new(championIds)));
	}

	/// <summary>
	/// What the Classic Arena league gives every Champion, as the champion screen counts it at metadata v39
	/// (https://github.com/ANicholasson/raid-bridge/issues/20): nothing unless the Account has a defence team
	/// (<c>ArenaWrapperReadOnly.HasDefence</c>), and otherwise the league's <c>ArenaLeagueInfo.BattleBonuses</c> in
	/// <c>StaticData.ArenaData.LeagueInfos</c>, each a fraction of the Base stat. <c>CalcPercentageStats</c> takes no
	/// Arena term, so a bonus to a critical stat would count for nothing, and is left out.
	/// </summary>
	public static IReadOnlyList<StatBonus> LeagueBonuses(ClassicCapture classic, ManagedObject staticData)
	{
		if (!classic.HasDefense || classic.League is not { } league || league.Name == NoLeague)
		{
			return [];
		}

		const string LeagueInfo = "SharedModel.Meta.Battle.Arena.ArenaLeagueInfo";
		var infos = staticData
			.Object("ArenaData", "SharedModel.Meta.Battle.Arena.StaticStandardArenaData")
			.List("LeagueInfos", $"System.Collections.Generic.List`1<{LeagueInfo}>")
			.Objects(LeagueInfo)
			.Where(info => info.EnumMember("Id", League).Value.Value == league.Value)
			.ToList();
		var bonuses = infos.Count == 1
			? infos[0].Object("BattleBonuses", "SharedModel.Meta.Heroes.BattleStats")
			: throw new GameDataException(
				$"The Classic Arena's league is {league.Name}, which SharedModel.Meta.Battle.Arena.StaticStandardArenaData.LeagueInfos holds {infos.Count} times, where raid-bridge expects once.");
		return [.. new[] { Stat.Health, Stat.Attack, Stat.Defence, Stat.Speed, Stat.Resistance, Stat.Accuracy }
			.Select(stat => new StatBonus(stat, FixedPoint.Of(bonuses, stat.ToString()), Flat: false))
			.Where(bonus => bonus.Value != 0)];
	}

	/// <summary>An Account that has set no defence team holds no <c>ArenaDefenseTeamSetup</c>, and so no league either.</summary>
	private static ClassicArena Classic(ClassicCapture classic, Defenders defenders) =>
		new(
			classic.League is { } league ? LeagueName(league.Name) : null,
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
internal sealed record ClassicCapture(
	GameEnumMember? League,
	long Points,
	(int Battles, int Wins, int Losses) Week,
	IReadOnlyList<FieldValue<int>>? DefencePositions,
	bool HasDefense);
