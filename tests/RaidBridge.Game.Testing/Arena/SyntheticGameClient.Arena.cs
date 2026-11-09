using RaidBridge.Game.Testing.Arena;
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
			("LossesThisWeek", "System.Int32", Int32TypeCode, 0x2C));
		var userArena3X3Data = Declare(
			"SharedModel.Meta.Battle.Arena3x3",
			"UserArena3X3Data",
			userArenaData,
			"SharedModel.Meta.Battle.Arena.UserArenaData",
			("LeagueId", League, ValueTypeTypeCode, 0x30),
			("Arena3X3DefenseTeamSetup", "System.Collections.Generic.List`1<SharedModel.Battle.Core.Setup.TeamSetup>", GenericInstanceTypeCode, 0x38));
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
		var classicData = _runtime.Object(userArenaData, 0x30);
		Week(classicData, classic.Points, classic.Battles, classic.Wins, classic.Losses);
		if (classic.DefenceTeam is { } defenceTeam)
		{
			Slot("SharedModel.Meta.Battle.Arena.UserArenaData.ArenaDefenseTeamSetup", classicData + 0x20, Team(defenceTeam));
		}

		var arenaWrapperObject = _runtime.Object(arenaWrapper, 0x18);
		_heap.Pointer(arenaWrapperObject + 0x10, classicData);
		Slot("Client.Model.Guard.UserWrapper.Arena", userWrapperObject + 0x58, arenaWrapperObject);

		var tag = arenaData.Tag;
		var tagData = _runtime.Object(updatableArena3x3Data, 0x40);
		Week(tagData, tag.Points, tag.Battles, tag.Wins, tag.Losses);
		_heap.Int32(tagData + 0x30, tag.League);
		if (tag.DefenceTeams is { } defenceTeams)
		{
			_tagTeams = _runtime.List(_runtime.ListType(teamSetup, "TeamSetup", sizeof(ulong)), [.. defenceTeams.Select(Team)]);
			_heap.Pointer(tagData + 0x38, _tagTeams);
		}

		var arena3x3WrapperObject = _runtime.Object(arena3x3Wrapper, 0x18);
		_heap.Pointer(arena3x3WrapperObject + 0x10, tagData);
		Slot("Client.Model.Guard.UserWrapper.Arena3x3", userWrapperObject + 0x60, arena3x3WrapperObject);
	}
}
