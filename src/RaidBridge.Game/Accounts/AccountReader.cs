using RaidBridge.Domain.Accounts;
using RaidBridge.Game.Arena;
using RaidBridge.Game.Artifacts;
using RaidBridge.Game.Champions;
using RaidBridge.Game.FactionGuardians;
using RaidBridge.Game.GreatHall;
using RaidBridge.Game.Il2Cpp;
using RaidBridge.Game.Localization;
using RaidBridge.Game.Missions;
using RaidBridge.Game.Relics;
using RaidBridge.Game.Resources;

namespace RaidBridge.Game.Accounts;

/// <summary>
/// Reads the Account from the game's model: <c>SingleInstance&lt;AppModel&gt;._instance</c>, then the id in
/// <c>AppModel.&lt;UserId&gt;k__BackingField</c>, with <c>_userWrapper.User.Id</c> as the fallback (spec #20, #19),
/// then the Champions, the Artifacts and the Relics, named from the static data in
/// <c>AppModel.StaticDataManager.StaticData</c>, the Resources, Shards and Items, the Great Hall and its Area Bonuses,
/// the Faction Guardians, the Classic, Tag and Live Arena, and the Missions, each Guardian and each Champion in an Arena defence team one
/// of those Champions.
/// It never goes near <c>_userWrapper.Social</c>: its UserSocialData holds emails, real names and birth dates.
/// </summary>
internal static class AccountReader
{
	private const string AppModel = "Client.Model.AppModel";
	private const string UserWrapper = "Client.Model.Guard.UserWrapper";

	public static Account Read(Il2CppRuntime game)
	{
		var appModel = game.StaticObject($"Client.App.SingleInstance`1<{AppModel}>", "_instance", declaredType: "T", objectClass: AppModel);
		var id = Id(appModel);
		var staticData = appModel
			.Object("<StaticDataManager>k__BackingField", "Client.Model.Gameplay.StaticData.IClientStaticDataManager", "Client.Model.Gameplay.StaticData.ClientStaticDataManager")
			.Object("<StaticData>k__BackingField", "SharedModel.Meta.ClientStaticData");
		var userWrapper = appModel.Object("_userWrapper", UserWrapper);
		var names = new LocalizedNames(staticData);
		var artifacts = ArtifactReader.Read(userWrapper, staticData, names);
		var relics = RelicReader.Read(userWrapper, staticData, names);
		// The Great Hall, Faction Guardians and Classic Arena come first, since every Champion's stats count what they give.
		var greatHall = GreatHallReader.Read(userWrapper, staticData);
		var guardianSlots = FactionGuardianReader.Capture(userWrapper);
		var classicArena = ArenaReader.Capture(userWrapper);
		var accountBonuses = new ChampionReader.AccountStatBonuses(
			greatHall.BonusesByAffinity,
			FactionGuardianReader.Bonuses(guardianSlots, staticData),
			ArenaReader.LeagueBonuses(classicArena, staticData));
		var champions = ChampionReader.Read(userWrapper, staticData, names, artifacts.WornBy, artifacts.Sets, relics.WornBy, accountBonuses);
		var resources = ResourceReader.Read(userWrapper);
		var factionGuardians = FactionGuardianReader.Read(guardianSlots, champions);
		var arena = ArenaReader.Read(classicArena, userWrapper, staticData, champions);
		var missions = MissionReader.Read(userWrapper, staticData, names);
		return new(id, champions, artifacts.Unworn, relics.Unworn, relics.Unsocketed, resources.Resources, resources.Shards, resources.Items, greatHall.Affinities, greatHall.Areas, factionGuardians, arena.Classic, arena.Tag, arena.Live, missions);
	}

	private static AccountId Id(ManagedObject appModel)
	{
		var userId = appModel.Int64("<UserId>k__BackingField");
		if (userId.Value > 0)
		{
			return new(userId.Value);
		}

		var userIdOnUser = appModel
			.Object("_userWrapper", UserWrapper)
			.Object("User", "Client.Model.Gameplay.User.User")
			.Int64("Id");
		if (userIdOnUser.Value > 0)
		{
			return new(userIdOnUser.Value);
		}

		throw new GameDataException($"Neither copy of the Account id is a positive long: {userId}, and {userIdOnUser}.");
	}
}
