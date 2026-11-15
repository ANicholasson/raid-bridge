using RaidBridge.Domain.Accounts;
using RaidBridge.Game.Arena;
using RaidBridge.Game.Artifacts;
using RaidBridge.Game.Champions;
using RaidBridge.Game.FactionGuardians;
using RaidBridge.Game.GreatHall;
using RaidBridge.Game.Il2Cpp;
using RaidBridge.Game.Localization;
using RaidBridge.Game.Metadata;
using RaidBridge.Game.Missions;
using RaidBridge.Game.Relics;
using RaidBridge.Game.Resources;
using RaidBridge.Game.SavedTeams;

namespace RaidBridge.Game.Accounts;

/// <summary>
/// Reads the Account from the game's model: <c>SingleInstance&lt;AppModel&gt;._instance</c>, then the id in
/// <c>AppModel.&lt;UserId&gt;k__BackingField</c>, with <c>_userWrapper.User.Id</c> as the fallback (spec #20, #19),
/// then its level, experience and power in <c>_userWrapper.Account.AccountData</c>, then the Champions, the Artifacts and the room for them, and the Relics, named from the static data in
/// <c>AppModel.StaticDataManager.StaticData</c>, the Resources, Shards and Items, the Great Hall and its Area Bonuses,
/// the Faction Guardians, the Classic, Tag and Live Arena, the Missions and the Saved teams, each Guardian and each Champion in an Arena defence team
/// or a Saved team one of those Champions. The Area Bonuses, the Live Arena and the Missions are optional sections: one the game's data
/// refuses is null, with a Problem naming the refusal, and the rest is read as ever (ADR-0006).
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
		var (level, experience, power) = LevelAndPower(userWrapper);
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
		var savedTeams = SavedTeamReader.Read(userWrapper, staticData, champions);

		// The optional sections come last, each on its own, so a failure in one costs only that section (ADR-0006).
		var problems = new List<Problem>();
		var areaBonuses = Optional(OptionalSection.AreaBonuses, () => GreatHallReader.AreaBonuses(userWrapper, staticData), problems);
		var liveArena = Optional(OptionalSection.LiveArena, () => ArenaReader.Live(userWrapper, staticData), problems);
		var missions = Optional(OptionalSection.Missions, () => MissionReader.Read(userWrapper, staticData, names), problems);
		return new(id, level, experience, power, artifacts.Capacity, champions, artifacts.Unworn, relics.Unworn, relics.Unsocketed, resources.Resources, resources.Shards, resources.Items, greatHall.Affinities, areaBonuses, factionGuardians, arena.Classic, arena.Tag, liveArena, missions, savedTeams, problems);
	}

	/// <summary>
	/// An optional section, or null with a Problem when the game's data or files refuse it
	/// (https://github.com/ANicholasson/raid-bridge/issues/47). The refusal is the same check with the same message; only
	/// the rest of the Snapshot is kept. Anything else, such as a bug in raid-bridge, still fails the run.
	/// </summary>
	private static T? Optional<T>(OptionalSection section, Func<T> read, List<Problem> problems)
	{
		try
		{
			return read();
		}
		catch (Exception exception) when (exception is GameDataException or GameMetadataException)
		{
			problems.Add(new(section, exception.Message));
			return default;
		}
	}

	/// <summary>
	/// The Account's <c>UserAccount</c>: its <c>Level</c>, 1 or more; its <c>Experience</c>, 0 or more; and its
	/// <c>TotalPower</c>, which the game holds as a double, so it is read as held, never rounded, but never negative,
	/// infinite or not a number, and never negative zero, as a Resource's amount isn't
	/// (https://github.com/ANicholasson/raid-bridge/issues/27).
	/// </summary>
	private static (int Level, long Experience, double Power) LevelAndPower(ManagedObject userWrapper)
	{
		var account = userWrapper
			.Object("Account", "Client.Model.Gameplay.Account.AccountWrapper")
			.Object("AccountData", "SharedModel.Meta.Account.UserAccount");
		return new(
			account.Int32("Level").Where(level => level >= 1, "a level of 1 or more"),
			account.Int64("Experience").Where(experience => experience >= 0, "experience of 0 or more"),
			account.Double("TotalPower").Where(power => double.IsFinite(power) && !double.IsNegative(power), "a power of 0 or more"));
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
