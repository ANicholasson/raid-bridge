using RaidBridge.Domain.Arena;
using RaidBridge.Domain.Champions;
using RaidBridge.Game.Il2Cpp;

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

	public static (ClassicArena Classic, TagArena Tag) Read(ManagedObject userWrapper, IReadOnlyList<Champion> champions)
	{
		var championIds = champions.Select(champion => champion.Id).ToHashSet();
		return (Classic(userWrapper, new(championIds)), Tag(userWrapper, new(championIds)));
	}

	/// <summary>An Account that has set no defence team holds no <c>ArenaDefenseTeamSetup</c>, and so no league either.</summary>
	private static ClassicArena Classic(ManagedObject userWrapper, Defenders defenders)
	{
		const string Arena = "the Classic Arena";
		var data = userWrapper
			.Object("Arena", "Client.Model.Gameplay.Battle.ArenaWrapper")
			.Object("ArenaData", ArenaData);
		var (battles, wins, losses) = Week(data, Arena);
		var team = data.ObjectOrNull("ArenaDefenseTeamSetup", TeamSetup);
		return new(
			team is null ? null : LeagueName(team.Enum("ArenaLeagueId", League)),
			data.Int64("ArenaPoints").Value,
			battles,
			wins,
			losses,
			team is null ? new([]) : Team(team, $"{Arena}'s defence team", defenders));
	}

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
			LeagueName(data.Enum("LeagueId", League)),
			data.Int64("ArenaPoints").Value,
			battles,
			wins,
			losses,
			[.. teams.Select((team, index) => Team(team, $"team {index + 1} of {Arena}'s defence", defenders))]);
	}

	private static string? LeagueName(FieldValue<string> league) => league.Value == NoLeague ? null : league.Value;

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

	/// <summary>A position whose <c>InventoryHeroId</c> is 0 holds no Champion, and is left out.</summary>
	private static ArenaTeam Team(ManagedObject team, string where, Defenders defenders) =>
		new([.. team
			.List("HeroSlotSetups", $"System.Collections.Generic.List`1<{HeroSlotSetup}>")
			.Objects(HeroSlotSetup)
			.Select(position => position.Int32("InventoryHeroId"))
			.Where(position => position.Value != 0)
			.Select(position => defenders.Place(position, where))]);

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
