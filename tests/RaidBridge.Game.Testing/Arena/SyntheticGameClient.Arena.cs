using RaidBridge.Game.Testing.Arena;
using RaidBridge.Game.Testing.Champions;
using RaidBridge.Game.Testing.Il2Cpp;
using static RaidBridge.Game.Testing.Il2Cpp.Unity6000Layout;

namespace RaidBridge.Game.Testing;

/// <summary>The Classic and Tag Arena of a synthetic Game client, and what a bad read can do to them.</summary>
public sealed partial class SyntheticGameClient
{
	private ulong _tagTeams;

	/// <summary>The Tag Arena's list of defence teams claims more teams than its array holds, as a torn or corrupt read might see it.</summary>
	public void SetTagTeamCount(int count) => _runtime.SetListSize(_tagTeams, count);

	/// <summary>
	/// UserWrapper's Arena and Arena3x3, whose read-only base classes declare the ArenaData and the Data: the Classic
	/// Arena's UserArenaData, with its defence team in a TeamSetup, and the Tag Arena's UpdatableArena3x3Data, a
	/// UserArenaData too, with its league and a list of TeamSetups. Each TeamSetup holds a HeroSlotSetup for each of its
	/// positions, beside the fields the reader doesn't read.
	/// </summary>
	private void LayOutTheArena(ulong userWrapperObject, SyntheticArenaData arenaData)
	{
		const string League = "SharedModel.Meta.Battle.Arena.ArenaLeagueId";
		var systemObject = _runtime.SystemObject;

		var heroSlotSetup = Declare(
			"SharedModel.Battle.Core.Setup",
			"HeroSlotSetup",
			systemObject,
			"System.Object",
			("Slot", "System.Int32", Int32TypeCode, 0x10),
			("InventoryHeroId", "System.Int32", Int32TypeCode, 0x14));
		var teamSetup = Declare(
			"SharedModel.Battle.Core.Setup",
			"TeamSetup",
			systemObject,
			"System.Object",
			("ArenaLeagueId", League, ValueTypeTypeCode, 0x10),
			("HeroSlotSetups", "System.Collections.Generic.List`1<SharedModel.Battle.Core.Setup.HeroSlotSetup>", GenericInstanceTypeCode, 0x18));
		var userArenaData = Declare(
			"SharedModel.Meta.Battle.Arena",
			"UserArenaData",
			systemObject,
			"System.Object",
			("ArenaPoints", "System.Int64", Int64TypeCode, 0x10),
			("BattlesStartedThisWeek", "System.Int32", Int32TypeCode, 0x18),
			("ArenaDefenseTeamSetup", "SharedModel.Battle.Core.Setup.TeamSetup", ClassTypeCode, 0x20),
			("VictoriesThisWeek", "System.Int32", Int32TypeCode, 0x28),
			("LossesThisWeek", "System.Int32", Int32TypeCode, 0x2C),
			("DefenseHeroIds", "System.Int32[]", SingleDimensionArrayTypeCode, 0x30));
		var userArena3X3Data = Declare(
			"SharedModel.Meta.Battle.Arena3x3",
			"UserArena3X3Data",
			userArenaData,
			"SharedModel.Meta.Battle.Arena.UserArenaData",
			("LeagueId", League, ValueTypeTypeCode, 0x38),
			("Arena3X3DefenseTeamSetup", "System.Collections.Generic.List`1<SharedModel.Battle.Core.Setup.TeamSetup>", GenericInstanceTypeCode, 0x40));
		var updatableArena3x3Data = Declare("Client.Model.Gameplay.Battle.Data", "UpdatableArena3x3Data", userArena3X3Data, "SharedModel.Meta.Battle.Arena3x3.UserArena3X3Data");
		var arenaWrapperReadOnly = Declare(
			"Client.Model.Gameplay.Battle",
			"ArenaWrapperReadOnly",
			systemObject,
			"System.Object",
			("ArenaData", "SharedModel.Meta.Battle.Arena.UserArenaData", ClassTypeCode, 0x10));
		var arenaWrapper = Declare("Client.Model.Gameplay.Battle", "ArenaWrapper", arenaWrapperReadOnly, "Client.Model.Gameplay.Battle.ArenaWrapperReadOnly");
		var arena3x3WrapperReadOnly = Declare(
			"Client.Model.Gameplay.Arena3x3",
			"Arena3x3WrapperReadOnly",
			systemObject,
			"System.Object",
			("Data", "Client.Model.Gameplay.Battle.Data.UpdatableArena3x3Data", ClassTypeCode, 0x10));
		var arena3x3Wrapper = Declare("Client.Model.Gameplay.Arena3x3", "Arena3x3Wrapper", arena3x3WrapperReadOnly, "Client.Model.Gameplay.Arena3x3.Arena3x3WrapperReadOnly");

		var positions = _runtime.ListType(heroSlotSetup, "HeroSlotSetup", sizeof(ulong));
		ulong Team(SyntheticTeamSetup team)
		{
			var teamObject = _runtime.Object(teamSetup, 0x20);
			_heap.Int32(teamObject + 0x10, team.League);
			_heap.Pointer(teamObject + 0x18, _runtime.List(positions, [.. team.HeroIds.Select((heroId, slot) =>
			{
				var position = _runtime.Object(heroSlotSetup, 0x18);
				_heap.Int32(position + 0x10, slot);
				_heap.Int32(position + 0x14, heroId);
				return position;
			})]));
			return teamObject;
		}

		void Week(ulong data, long points, int battles, int wins, int losses)
		{
			_heap.Int64(data + 0x10, points);
			_heap.Int32(data + 0x18, battles);
			_heap.Int32(data + 0x28, wins);
			_heap.Int32(data + 0x2C, losses);
		}

		var classic = arenaData.Classic;
		var classicData = _runtime.Object(userArenaData, 0x38);
		Week(classicData, classic.Points, classic.Battles, classic.Wins, classic.Losses);
		if (classic.DefenseHeroIds is { } defenseHeroIds)
		{
			var ids = _runtime.Array(_runtime.ArrayClass(_runtime.SystemInt32, "Int32", sizeof(int)), defenseHeroIds.Count, sizeof(int));
			for (var index = 0; index < defenseHeroIds.Count; index++)
			{
				_heap.Int32(SyntheticRuntime.Element(ids, index, sizeof(int)), defenseHeroIds[index]);
			}

			_heap.Pointer(classicData + 0x30, ids);
		}

		if (classic.DefenceTeam is { } defenceTeam)
		{
			Slot("SharedModel.Meta.Battle.Arena.UserArenaData.ArenaDefenseTeamSetup", classicData + 0x20, Team(defenceTeam));
		}

		var arenaWrapperObject = _runtime.Object(arenaWrapper, 0x18);
		_heap.Pointer(arenaWrapperObject + 0x10, classicData);
		Slot("Client.Model.Guard.UserWrapper.Arena", userWrapperObject + 0x58, arenaWrapperObject);

		var tag = arenaData.Tag;
		var tagData = _runtime.Object(updatableArena3x3Data, 0x48);
		Week(tagData, tag.Points, tag.Battles, tag.Wins, tag.Losses);
		_heap.Int32(tagData + 0x38, tag.League);
		if (tag.DefenceTeams is { } defenceTeams)
		{
			_tagTeams = _runtime.List(_runtime.ListType(teamSetup, "TeamSetup", sizeof(ulong)), [.. defenceTeams.Select(Team)]);
			_heap.Pointer(tagData + 0x40, _tagTeams);
		}

		var arena3x3WrapperObject = _runtime.Object(arena3x3Wrapper, 0x18);
		_heap.Pointer(arena3x3WrapperObject + 0x10, tagData);
		Slot("Client.Model.Guard.UserWrapper.Arena3x3", userWrapperObject + 0x60, arena3x3WrapperObject);
		LayOutTheLeagues(arenaData.LeagueBonuses);
		LayOutTheLiveArena(userWrapperObject, arenaData.Live, arenaData.LiveLeagues);
	}

	/// <summary>
	/// UserWrapper's LiveArena, whose read-only base class declares the LiveArenaData, an UpdatableUserLiveArenaData
	/// whose base class UserLiveArenaData holds the points, wins and losses; and the static data's LiveArenaData, whose
	/// LeagueInfoById holds each league's LiveArenaLeagueInfo, with its MinPoints.
	/// </summary>
	private void LayOutTheLiveArena(ulong userWrapperObject, SyntheticLiveArena? live, IReadOnlyList<(int League, int MinPoints)> leagues)
	{
		var systemObject = _runtime.SystemObject;
		var userLiveArenaData = Declare(
			"SharedModel.Meta.LiveArena",
			"UserLiveArenaData",
			systemObject,
			"System.Object",
			("Points", "System.Int64", Int64TypeCode, 0x10),
			("Victories", "System.Int32", Int32TypeCode, 0x18),
			("Defeats", "System.Int32", Int32TypeCode, 0x1C));
		var updatableUserLiveArenaData = Declare("Client.Model.Gameplay.LiveArena", "UpdatableUserLiveArenaData", userLiveArenaData, "SharedModel.Meta.LiveArena.UserLiveArenaData");
		var liveArenaWrapperReadonly = Declare(
			"Client.Model.Gameplay.LiveArena",
			"LiveArenaWrapperReadonly",
			systemObject,
			"System.Object",
			("LiveArenaData", "Client.Model.Gameplay.LiveArena.UpdatableUserLiveArenaData", ClassTypeCode, 0x10));
		var liveArenaWrapper = Declare("Client.Model.Gameplay.LiveArena", "LiveArenaWrapper", liveArenaWrapperReadonly, "Client.Model.Gameplay.LiveArena.LiveArenaWrapperReadonly");
		var leagueInfo = Declare(
			"SharedModel.Meta.LiveArena",
			"LiveArenaLeagueInfo",
			systemObject,
			"System.Object",
			("MinPoints", "System.Int32", Int32TypeCode, 0x10));
		var staticLiveArenaData = Declare(
			"SharedModel.Meta.LiveArena",
			"StaticLiveArenaData",
			systemObject,
			"System.Object",
			("LeagueInfoById", "System.Collections.Generic.Dictionary`2<SharedModel.Meta.LiveArena.LiveArenaLeagueId, SharedModel.Meta.LiveArena.LiveArenaLeagueInfo>", GenericInstanceTypeCode, 0x10));

		var wrapperObject = _runtime.Object(liveArenaWrapper, 0x18);
		if (live is not null)
		{
			var dataObject = _runtime.Object(updatableUserLiveArenaData, 0x20);
			_heap.Int64(dataObject + 0x10, live.Points);
			_heap.Int32(dataObject + 0x18, live.Wins);
			_heap.Int32(dataObject + 0x1C, live.Losses);
			_heap.Pointer(wrapperObject + 0x10, dataObject);
		}

		Slot("Client.Model.Guard.UserWrapper.LiveArena", userWrapperObject + 0x78, wrapperObject);

		var staticObject = _runtime.Object(staticLiveArenaData, 0x18);
		_heap.Pointer(staticObject + 0x10, _runtime.Dictionary(_runtime.DictionaryType(ValueTypeTypeCode, ClassTypeCode), [.. leagues.Select(league =>
		{
			var infoObject = _runtime.Object(leagueInfo, 0x18);
			_heap.Int32(infoObject + 0x10, league.MinPoints);
			return ((ulong)league.League, infoObject);
		})]));
		Slot("SharedModel.Meta.StaticData.LiveArenaData", _clientStaticData + 0x70, staticObject);
	}

	/// <summary>The static data's ArenaData, whose LeagueInfos hold each league's Id and BattleBonuses, a BattleStats.</summary>
	private void LayOutTheLeagues(IReadOnlyDictionary<int, SyntheticBattleStats> leagueBonuses)
	{
		var leagueInfo = Declare(
			"SharedModel.Meta.Battle.Arena",
			"ArenaLeagueInfo",
			_runtime.SystemObject,
			"System.Object",
			("Id", "SharedModel.Meta.Battle.Arena.ArenaLeagueId", ValueTypeTypeCode, 0x10),
			("BattleBonuses", "SharedModel.Meta.Heroes.BattleStats", ClassTypeCode, 0x18));
		var arenaData = Declare(
			"SharedModel.Meta.Battle.Arena",
			"StaticStandardArenaData",
			_runtime.SystemObject,
			"System.Object",
			("LeagueInfos", "System.Collections.Generic.List`1<SharedModel.Meta.Battle.Arena.ArenaLeagueInfo>", GenericInstanceTypeCode, 0x10));

		var arenaDataObject = _runtime.Object(arenaData, 0x18);
		_heap.Pointer(arenaDataObject + 0x10, _runtime.List(_runtime.ListType(leagueInfo, "ArenaLeagueInfo", sizeof(ulong)), [.. leagueBonuses.Select(league =>
		{
			var infoObject = _runtime.Object(leagueInfo, 0x20);
			_heap.Int32(infoObject + 0x10, league.Key);
			_heap.Pointer(infoObject + 0x18, BattleStatsObject(_battleStats, league.Value));
			return infoObject;
		})]));
		Slot("SharedModel.Meta.StaticData.ArenaData", _clientStaticData + 0x60, arenaDataObject);
	}
}
