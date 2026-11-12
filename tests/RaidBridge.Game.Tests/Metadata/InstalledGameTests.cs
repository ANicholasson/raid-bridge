using System.Reflection.PortableExecutable;
using RaidBridge.Game.Metadata;

namespace RaidBridge.Game.Tests.Metadata;

/// <summary>
/// Gated: parses the installed game's own files and resolves every class, field and enum on spec #20's path to the
/// Account. Runs only with RAIDBRIDGE_LIVE=1 and RAIDBRIDGE_GAME_DIR set to the folder holding Raid.exe. It reads
/// files only and touches no Account.
/// </summary>
public class InstalledGameTests
{
	/// <summary>
	/// Spec #20's path to the Account, as the installed game declares it at metadata v39, and the routes the research for
	/// Relics and Total stats found (https://github.com/ANicholasson/raid-bridge/issues/7 and
	/// https://github.com/ANicholasson/raid-bridge/issues/9). The Relics reader takes the first
	/// (https://github.com/ANicholasson/raid-bridge/issues/8), and the stats readers the second's Base stats and sets
	/// (https://github.com/ANicholasson/raid-bridge/issues/10, https://github.com/ANicholasson/raid-bridge/issues/11).
	/// </summary>
	public static TheoryData<string, string, string, bool> AccountPath => new()
	{
		// The root: a static on the generic instance SingleInstance<AppModel>, AppModel's base class.
		{ "Client.App.SingleInstance`1", "_instance", "T", true },
		{ "Client.Model.AppModel", "<UserId>k__BackingField", "System.Int64", false },
		{ "Client.Model.AppModel", "_userWrapper", "Client.Model.Guard.UserWrapper", false },
		{ "Client.Model.Guard.UserWrapper", "User", "Client.Model.Gameplay.User.User", false },
		{ "Client.Model.Gameplay.User.User", "Id", "System.Int64", false },
		// Champions. HeroData is declared on HeroesWrapperReadOnly, and HeroById on UserHeroData.
		{ "Client.Model.Guard.UserWrapper", "Heroes", "Client.Model.Gameplay.Heroes.HeroesWrapper", false },
		{ "Client.Model.Gameplay.Heroes.HeroesWrapper", "HeroData", "Client.Model.Gameplay.Heroes.Data.UpdatableHeroData", false },
		{ "Client.Model.Gameplay.Heroes.Data.UpdatableHeroData", "HeroById", "System.Collections.Generic.Dictionary`2<System.Int32, SharedModel.Meta.Heroes.Hero>", false },
		// Each Champion: its hero, skills, masteries and awakening, read by field, never by a property such as Hero.Type (#25).
		{ "SharedModel.Meta.Heroes.Hero", "Id", "System.Int32", false },
		{ "SharedModel.Meta.Heroes.Hero", "TypeId", "System.Int32", false },
		{ "SharedModel.Meta.Heroes.Hero", "Grade", "SharedModel.Meta.Heroes.HeroGrade", false },
		{ "SharedModel.Meta.Heroes.Hero", "Level", "System.Int32", false },
		{ "SharedModel.Meta.Heroes.Hero", "EmpowerLevel", "System.Int32", false },
		{ "SharedModel.Meta.Heroes.Hero", "Locked", "System.Boolean", false },
		{ "SharedModel.Meta.Heroes.Hero", "InStorage", "System.Boolean", false },
		{ "SharedModel.Meta.Heroes.Hero", "InBathhouse", "System.Boolean", false },
		{ "SharedModel.Meta.Heroes.Hero", "Marker", "SharedModel.Meta.Heroes.HeroMarker", false },
		{ "SharedModel.Meta.Heroes.Hero", "Skills", "System.Collections.Generic.List`1<SharedModel.Meta.Skills.Skill>", false },
		{ "SharedModel.Meta.Heroes.Hero", "MasteryData", "SharedModel.Meta.Masteries.HeroMasteryData", false },
		{ "SharedModel.Meta.Heroes.Hero", "DoubleAscendData", "SharedModel.Meta.DoubleAscend.User.HeroDoubleAscendData", false },
		{ "SharedModel.Meta.Skills.Skill", "TypeId", "System.Int32", false },
		{ "SharedModel.Meta.Skills.Skill", "Level", "System.Int32", false },
		{ "SharedModel.Meta.Masteries.HeroMasteryData", "Masteries", "System.Collections.Generic.List`1<System.Int32>", false },
		{ "SharedModel.Meta.DoubleAscend.User.HeroDoubleAscendData", "Grade", "SharedModel.Meta.DoubleAscend.User.DoubleAscendGrade", false },
		{ "SharedModel.Meta.DoubleAscend.User.HeroDoubleAscendData", "BlessingId", "System.Nullable`1<SharedModel.Meta.DoubleAscend.Static.BlessingTypeId>", false },
		// Equipped Artifacts. ArtifactData is declared on EquipmentWrapperReadonly.
		{ "Client.Model.Guard.UserWrapper", "Artifacts", "Client.Model.Gameplay.Artifacts.EquipmentWrapper", false },
		{ "Client.Model.Gameplay.Artifacts.EquipmentWrapper", "ArtifactData", "SharedModel.Meta.Artifacts.UserArtifactData", false },
		{ "SharedModel.Meta.Artifacts.UserArtifactData", "ArtifactDataByHeroId", "System.Collections.Generic.Dictionary`2<System.Int32, SharedModel.Meta.Artifacts.HeroArtifactData>", false },
		{ "SharedModel.Meta.Artifacts.HeroArtifactData", "ArtifactIdByKind", "System.Collections.Generic.Dictionary`2<SharedModel.Meta.Artifacts.ArtifactKindId, System.Int32>", false },
		// Artifact storage: in the Account's own list, or behind the storage resolver after storage migration.
		{ "SharedModel.Meta.Artifacts.UserArtifactData", "Artifacts", "System.Collections.Generic.List`1<SharedModel.Meta.Artifacts.Artifact>", false },
		{ "SharedModel.Meta.Artifacts.ArtifactStorage.ArtifactStorageResolver", "_implementation", "SharedModel.Meta.Artifacts.ArtifactStorage.IArtifactStorageAdapter", true },
		// Spec #20 names ExternalArtifactsStorage._state._artifacts. At v39 there is no _state: the Artifacts are in _cachedArtifacts.
		{ "Client.Model.Gameplay.Artifacts.ExternalArtifactsStorage", "_cachedArtifacts", "Client.Model.Gameplay.Artifacts.CachedArtifacts", false },
		{ "Client.Model.Gameplay.Artifacts.CachedArtifacts", "_artifacts", "System.Collections.Generic.Dictionary`2<System.Int32, SharedModel.Meta.Artifacts.Artifact>", false },
		// Each Artifact and its bonuses, read by field, never by a property such as Artifact.SetInfo or Artifact.SetKindId (#26).
		{ "SharedModel.Meta.Artifacts.Artifact", "_id", "System.Int32", false },
		{ "SharedModel.Meta.Artifacts.Artifact", "_kindId", "SharedModel.Meta.Artifacts.ArtifactKindId", false },
		{ "SharedModel.Meta.Artifacts.Artifact", "_setKindId", "SharedModel.Meta.Artifacts.Sets.ArtifactSetKindId", false },
		{ "SharedModel.Meta.Artifacts.Artifact", "_rankId", "SharedModel.Meta.Artifacts.ArtifactRankId", false },
		{ "SharedModel.Meta.Artifacts.Artifact", "_rarityId", "SharedModel.Meta.ItemRarity", false },
		{ "SharedModel.Meta.Artifacts.Artifact", "_level", "System.Int32", false },
		{ "SharedModel.Meta.Artifacts.Artifact", "_ascendLevel", "System.Nullable`1<System.Int32>", false },
		{ "SharedModel.Meta.Artifacts.Artifact", "_requiredFraction", "SharedModel.Meta.Heroes.HeroFraction", false },
		{ "SharedModel.Meta.Artifacts.Artifact", "_primaryBonus", "SharedModel.Meta.Artifacts.Bonuses.ArtifactBonus", false },
		{ "SharedModel.Meta.Artifacts.Artifact", "_ascendBonus", "SharedModel.Meta.Artifacts.Bonuses.ArtifactBonus", false },
		{ "SharedModel.Meta.Artifacts.Artifact", "_secondaryBonuses", "System.Collections.Generic.List`1<SharedModel.Meta.Artifacts.Bonuses.ArtifactBonus>", false },
		{ "SharedModel.Meta.Artifacts.Bonuses.ArtifactBonus", "_kindId", "SharedModel.Battle.Effects.StatKindId", false },
		{ "SharedModel.Meta.Artifacts.Bonuses.ArtifactBonus", "_value", "SharedModel.Meta.Artifacts.BonusValue", false },
		{ "SharedModel.Meta.Artifacts.Bonuses.ArtifactBonus", "_powerUpValue", "Plarium.Common.Numerics.Fixed", false },
		{ "SharedModel.Meta.Artifacts.Bonuses.ArtifactBonus", "_level", "System.Int32", false },
		{ "SharedModel.Meta.Artifacts.BonusValue", "_isAbsolute", "System.Boolean", false },
		{ "SharedModel.Meta.Artifacts.BonusValue", "_value", "Plarium.Common.Numerics.Fixed", false },
		{ "Plarium.Common.Numerics.Fixed", "m_rawValue", "System.Int64", false },
		// Names: Champion types, sets, skills and the localization tables.
		{ "Client.Model.AppModel", "<StaticDataManager>k__BackingField", "Client.Model.Gameplay.StaticData.IClientStaticDataManager", false },
		{ "Client.Model.Gameplay.StaticData.ClientStaticDataManager", "<StaticData>k__BackingField", "SharedModel.Meta.ClientStaticData", false },
		{ "SharedModel.Meta.ClientStaticData", "HeroData", "SharedModel.Meta.Heroes.StaticHeroData", false },
		{ "SharedModel.Meta.Heroes.StaticHeroData", "HeroTypeById", "System.Collections.Generic.Dictionary`2<System.Int32, SharedModel.Meta.Heroes.HeroType>", false },
		{ "SharedModel.Meta.ClientStaticData", "ArtifactData", "SharedModel.Meta.Artifacts.StaticArtifactData", false },
		{ "SharedModel.Meta.Artifacts.StaticArtifactData", "SetInfos", "System.Collections.Generic.List`1<SharedModel.Meta.Artifacts.Sets.ArtifactSetInfo>", false },
		{ "SharedModel.Meta.Artifacts.Sets.ArtifactSetInfo", "ArtifactSetKindId", "SharedModel.Meta.Artifacts.Sets.ArtifactSetKindId", false },
		{ "SharedModel.Meta.Artifacts.Sets.ArtifactSetInfo", "Name", "SharedModel.Common.Localization.SharedLTextKey", false },
		{ "SharedModel.Meta.ClientStaticData", "SkillData", "SharedModel.Meta.Skills.StaticSkillData", false },
		{ "SharedModel.Meta.Skills.StaticSkillData", "_skillTypeById", "System.Collections.Generic.Dictionary`2<System.Int32, SharedModel.Meta.Skills.SkillType>", false },
		// A hero type's name, rarity and fraction, and its Affinity on each of its forms; a skill type's name.
		{ "SharedModel.Meta.Heroes.HeroType", "Id", "System.Int32", false },
		{ "SharedModel.Meta.Heroes.HeroType", "Name", "SharedModel.Common.Localization.SharedLTextKey", false },
		{ "SharedModel.Meta.Heroes.HeroType", "Rarity", "SharedModel.Meta.Heroes.HeroRarity", false },
		{ "SharedModel.Meta.Heroes.HeroType", "Fraction", "SharedModel.Meta.Heroes.HeroFraction", false },
		{ "SharedModel.Meta.Heroes.HeroType", "Forms", "SharedModel.Meta.Heroes.HeroForm[]", false },
		{ "SharedModel.Meta.Heroes.HeroForm", "Element", "SharedModel.Meta.Heroes.Element", false },
		{ "SharedModel.Meta.Skills.SkillType", "Id", "System.Int32", false },
		{ "SharedModel.Meta.Skills.SkillType", "Name", "SharedModel.Common.Localization.SharedLTextKey", false },
		{ "SharedModel.Meta.ClientStaticData", "StaticDataLocalization", "System.Collections.Generic.Dictionary`2<System.String, System.String>", false },
		{ "SharedModel.Meta.ClientStaticData", "ClientLocalization", "System.Collections.Generic.Dictionary`2<System.String, System.String>", false },
		{ "SharedModel.Common.Localization.SharedLTextKey", "Key", "System.String", false },
		{ "SharedModel.Common.Localization.SharedLTextKey", "DefaultValue", "System.String", false },
		// Resources, Shards and Items (#34), on Raid Toolkit's routes, which still hold at v39. Each wrapper's data is
		// declared on its read-only base class. Resources hold each amount as a double, not a Fixed.
		{ "Client.Model.Guard.UserWrapper", "Account", "Client.Model.Gameplay.Account.AccountWrapper", false },
		{ "Client.Model.Gameplay.Account.AccountWrapper", "AccountData", "SharedModel.Meta.Account.UserAccount", false },
		{ "SharedModel.Meta.Account.UserAccount", "Resources", "SharedModel.Meta.Account.Resources", false },
		{ "SharedModel.Meta.Account.Resources", "RawValues", "System.Collections.Generic.Dictionary`2<SharedModel.Meta.Account.ResourceTypeId, System.Double>", false },
		{ "Client.Model.Guard.UserWrapper", "Shards", "Client.Model.Gameplay.Shards.ShardWrapper", false },
		{ "Client.Model.Gameplay.Shards.ShardWrapper", "ShardData", "SharedModel.Meta.Shards.UserShardData", false },
		{ "SharedModel.Meta.Shards.UserShardData", "Shards", "System.Collections.Generic.List`1<SharedModel.Meta.Shards.Shard>", false },
		{ "SharedModel.Meta.Shards.Shard", "TypeId", "SharedModel.Meta.Shards.ShardTypeId", false },
		{ "SharedModel.Meta.Shards.Shard", "Count", "System.Int32", false },
		{ "Client.Model.Guard.UserWrapper", "BlackMarket", "Client.Model.Gameplay.BlackMarket.BlackMarketWrapper", false },
		{ "Client.Model.Gameplay.BlackMarket.BlackMarketWrapper", "BlackMarketData", "SharedModel.Meta.BlackMarket.UserBlackMarketData", false },
		{ "SharedModel.Meta.BlackMarket.UserBlackMarketData", "Items", "System.Collections.Generic.Dictionary`2<SharedModel.Meta.BlackMarket.BlackMarketItemId, SharedModel.Meta.BlackMarket.BmiBox>", false },
		{ "SharedModel.Meta.BlackMarket.BmiBox", "Count", "System.Int32", false },
		// The Great Hall (#35), on Raid Toolkit's route, which still holds at v39. VillageData is declared on
		// VillageWrapperReadOnly, and the levels on UpdatableVillageData's base class, UserVillageData. The static data
		// lists a bonus for each level of each stat the Great Hall raises, which bounds the levels.
		{ "Client.Model.Guard.UserWrapper", "Village", "Client.Model.Gameplay.Village.VillageWrapper", false },
		{ "Client.Model.Gameplay.Village.VillageWrapper", "VillageData", "Client.Model.Gameplay.Village.Data.UpdatableVillageData", false },
		{ "Client.Model.Gameplay.Village.Data.UpdatableVillageData", "CapitolBonusLevelByStatByElement", "System.Collections.Generic.Dictionary`2<SharedModel.Meta.Heroes.Element, System.Collections.Generic.Dictionary`2<SharedModel.Battle.Effects.StatKindId, System.Int32>>", false },
		{ "SharedModel.Meta.ClientStaticData", "VillageData", "SharedModel.Meta.Village.StaticVillageData", false },
		{ "SharedModel.Meta.Village.StaticVillageData", "CapitolBonusByStatKind", "System.Collections.Generic.Dictionary`2<SharedModel.Battle.Effects.StatKindId, System.Collections.Generic.List`1<SharedModel.Meta.Artifacts.BonusValue>>", false },
		// The Faction Guardians (#36), on Raid Toolkit's route, which still holds at v39, but with plain dictionaries where
		// Raid Toolkit read SlotsByFraction through a wrapper's UnderlyingDictionary. AcademyData is declared on
		// AcademyWrapperReadOnly, and Guardians on UpdatableUserAcademyData's base class, UserAcademyData.
		{ "Client.Model.Guard.UserWrapper", "Academy", "Client.Model.Gameplay.Academy.AcademyWrapper", false },
		{ "Client.Model.Gameplay.Academy.AcademyWrapper", "AcademyData", "Client.Model.Gameplay.Academy.Data.UpdatableUserAcademyData", false },
		{ "Client.Model.Gameplay.Academy.Data.UpdatableUserAcademyData", "Guardians", "SharedModel.Meta.Academy.User.UserAcademyGuardiansData", false },
		{ "SharedModel.Meta.Academy.User.UserAcademyGuardiansData", "SlotsByFraction", "System.Collections.Generic.Dictionary`2<SharedModel.Meta.Heroes.HeroFraction, SharedModel.Meta.Academy.User.AcademyGuardiansRaritySlots>", false },
		{ "SharedModel.Meta.Academy.User.AcademyGuardiansRaritySlots", "SlotsByRarity", "System.Collections.Generic.Dictionary`2<SharedModel.Meta.Heroes.HeroRarity, System.Collections.Generic.List`1<SharedModel.Meta.Academy.User.AcademyGuardiansSlot>>", false },
		{ "SharedModel.Meta.Academy.User.AcademyGuardiansSlot", "FirstHero", "System.Nullable`1<System.Int32>", false },
		{ "SharedModel.Meta.Academy.User.AcademyGuardiansSlot", "SecondHero", "System.Nullable`1<System.Int32>", false },
		// The Classic and Tag Arena (#37), on Raid Toolkit's routes, which still hold at v39. ArenaData is declared on
		// ArenaWrapperReadOnly and Data on Arena3x3WrapperReadOnly; the Tag Arena's UpdatableArena3x3Data derives from
		// UserArena3X3Data, which derives from the Classic Arena's UserArenaData, so both read the week from the same fields.
		{ "Client.Model.Guard.UserWrapper", "Arena", "Client.Model.Gameplay.Battle.ArenaWrapper", false },
		{ "Client.Model.Gameplay.Battle.ArenaWrapper", "ArenaData", "SharedModel.Meta.Battle.Arena.UserArenaData", false },
		{ "SharedModel.Meta.Battle.Arena.UserArenaData", "ArenaPoints", "System.Int64", false },
		{ "SharedModel.Meta.Battle.Arena.UserArenaData", "BattlesStartedThisWeek", "System.Int32", false },
		{ "SharedModel.Meta.Battle.Arena.UserArenaData", "VictoriesThisWeek", "System.Int32", false },
		{ "SharedModel.Meta.Battle.Arena.UserArenaData", "LossesThisWeek", "System.Int32", false },
		{ "SharedModel.Meta.Battle.Arena.UserArenaData", "ArenaDefenseTeamSetup", "SharedModel.Battle.Core.Setup.TeamSetup", false },
		{ "SharedModel.Battle.Core.Setup.TeamSetup", "ArenaLeagueId", "SharedModel.Meta.Battle.Arena.ArenaLeagueId", false },
		{ "SharedModel.Battle.Core.Setup.TeamSetup", "HeroSlotSetups", "System.Collections.Generic.List`1<SharedModel.Battle.Core.Setup.HeroSlotSetup>", false },
		{ "SharedModel.Battle.Core.Setup.HeroSlotSetup", "InventoryHeroId", "System.Int32", false },
		{ "Client.Model.Guard.UserWrapper", "Arena3x3", "Client.Model.Gameplay.Arena3x3.Arena3x3Wrapper", false },
		{ "Client.Model.Gameplay.Arena3x3.Arena3x3Wrapper", "Data", "Client.Model.Gameplay.Battle.Data.UpdatableArena3x3Data", false },
		{ "Client.Model.Gameplay.Battle.Data.UpdatableArena3x3Data", "LeagueId", "SharedModel.Meta.Battle.Arena.ArenaLeagueId", false },
		{ "Client.Model.Gameplay.Battle.Data.UpdatableArena3x3Data", "ArenaPoints", "System.Int64", false },
		{ "Client.Model.Gameplay.Battle.Data.UpdatableArena3x3Data", "Arena3X3DefenseTeamSetup", "System.Collections.Generic.List`1<SharedModel.Battle.Core.Setup.TeamSetup>", false },
		// The Classic Arena league's bonus (https://github.com/ANicholasson/raid-bridge/issues/20): it counts only while
		// DefenseHeroIds names a Champion, and each league's BattleBonuses are in the static data's LeagueInfos.
		{ "SharedModel.Meta.Battle.Arena.UserArenaData", "DefenseHeroIds", "System.Int32[]", false },
		{ "SharedModel.Meta.ClientStaticData", "ArenaData", "SharedModel.Meta.Battle.Arena.StaticStandardArenaData", false },
		{ "SharedModel.Meta.Battle.Arena.StaticStandardArenaData", "LeagueInfos", "System.Collections.Generic.List`1<SharedModel.Meta.Battle.Arena.ArenaLeagueInfo>", false },
		{ "SharedModel.Meta.Battle.Arena.ArenaLeagueInfo", "Id", "SharedModel.Meta.Battle.Arena.ArenaLeagueId", false },
		{ "SharedModel.Meta.Battle.Arena.ArenaLeagueInfo", "BattleBonuses", "SharedModel.Meta.Heroes.BattleStats", false },
		// Relics (https://github.com/ANicholasson/raid-bridge/issues/7, docs/research/2026-11-relics.md). RelicsData is
		// declared on RelicsWrapperReadonly, and the data on UpdatableUserRelicData's base class, UserRelicData. A Champion's
		// Relics are a list of ids by Champion id, not a field of Hero.
		{ "Client.Model.Guard.UserWrapper", "Relics", "Client.Model.Gameplay.Relics.RelicsWrapper", false },
		{ "Client.Model.Gameplay.Relics.RelicsWrapper", "RelicsData", "Client.Model.Gameplay.Relics.UpdatableUserRelicData", false },
		{ "Client.Model.Gameplay.Relics.UpdatableUserRelicData", "Relics", "System.Collections.Generic.List`1<SharedModel.Meta.Relics.UserData.Relic>", false },
		{ "Client.Model.Gameplay.Relics.UpdatableUserRelicData", "Stones", "System.Collections.Generic.List`1<SharedModel.Meta.Relics.UserData.RelicStone>", false },
		{ "Client.Model.Gameplay.Relics.UpdatableUserRelicData", "RelicDataByHeroId", "System.Collections.Generic.Dictionary`2<System.Int32, SharedModel.Meta.Relics.UserData.HeroRelicData>", false },
		{ "SharedModel.Meta.Relics.UserData.HeroRelicData", "RelicIds", "System.Collections.Generic.List`1<System.Int32>", false },
		{ "SharedModel.Meta.Relics.UserData.Relic", "Id", "System.Int32", false },
		{ "SharedModel.Meta.Relics.UserData.Relic", "TypeId", "System.Int32", false },
		{ "SharedModel.Meta.Relics.UserData.Relic", "Rank", "SharedModel.Meta.Relics.StaticData.RelicRank", false },
		{ "SharedModel.Meta.Relics.UserData.Relic", "Level", "System.Int32", false },
		{ "SharedModel.Meta.Relics.UserData.Relic", "IsActivated", "System.Nullable`1<System.Boolean>", false },
		{ "SharedModel.Meta.Relics.UserData.Relic", "Sockets", "System.Collections.Generic.List`1<SharedModel.Meta.Relics.UserData.RelicStoneSocket>", false },
		{ "SharedModel.Meta.Relics.UserData.RelicStoneSocket", "ShapeKindId", "SharedModel.Meta.Relics.StaticData.RelicStoneShapeKindId", false },
		{ "SharedModel.Meta.Relics.UserData.RelicStoneSocket", "StoneId", "System.Nullable`1<System.Int32>", false },
		{ "SharedModel.Meta.Relics.UserData.RelicStone", "Id", "System.Int32", false },
		{ "SharedModel.Meta.Relics.UserData.RelicStone", "TypeId", "System.Int32", false },
		// Relic static data: types by id, and the stat values each level gives. RelicData is declared on ClientStaticData's
		// base class, StaticData.
		{ "SharedModel.Meta.ClientStaticData", "RelicData", "SharedModel.Meta.Relics.StaticData.StaticRelicData", false },
		{ "SharedModel.Meta.Relics.StaticData.StaticRelicData", "_relicTypeById", "System.Collections.Generic.Dictionary`2<System.Int32, SharedModel.Meta.Relics.StaticData.RelicType>", false },
		{ "SharedModel.Meta.Relics.StaticData.StaticRelicData", "_relicStoneTypeById", "System.Collections.Generic.Dictionary`2<System.Int32, SharedModel.Meta.Relics.StaticData.RelicStoneType>", false },
		{ "SharedModel.Meta.Relics.StaticData.StaticRelicData", "StatValueByLevel", "System.Collections.Generic.Dictionary`2<SharedModel.Battle.Effects.StatKindId, System.Collections.Generic.List`1<SharedModel.Meta.Artifacts.BonusValue>>", false },
		{ "SharedModel.Meta.Relics.StaticData.StaticRelicData", "StatBonusOpeningLevels", "System.Collections.Generic.List`1<System.Int32>", false },
		{ "SharedModel.Meta.Relics.StaticData.StaticRelicData", "SkillLevelByRelicLevel", "System.Collections.Generic.Dictionary`2<System.Int32, System.Int32>", false },
		{ "SharedModel.Meta.Relics.StaticData.StaticRelicData", "HeroActivatedRelicsLimit", "System.Int32", false },
		{ "SharedModel.Meta.Relics.StaticData.RelicType", "Id", "System.Int32", false },
		{ "SharedModel.Meta.Relics.StaticData.RelicType", "Name", "SharedModel.Common.Localization.SharedLTextKey", false },
		{ "SharedModel.Meta.Relics.StaticData.RelicType", "Description", "SharedModel.Common.Localization.SharedLTextKey", false },
		{ "SharedModel.Meta.Relics.StaticData.RelicType", "Rarity", "SharedModel.Meta.ItemRarity", false },
		{ "SharedModel.Meta.Relics.StaticData.RelicType", "SkillIds", "System.Collections.Generic.List`1<System.Int32>", false },
		{ "SharedModel.Meta.Relics.StaticData.RelicType", "SkillIdsByFaction", "System.Collections.Generic.List`1<SharedModel.Meta.Relics.StaticData.RelicFactionSkillBonus>", false },
		{ "SharedModel.Meta.Relics.StaticData.RelicType", "StatsBonus", "System.Collections.Generic.List`1<SharedModel.Battle.Effects.StatKindId>", false },
		{ "SharedModel.Meta.Relics.StaticData.RelicType", "Sockets", "System.Collections.Generic.List`1<SharedModel.Meta.Relics.StaticData.RelicStoneShapeKindId>", false },
		{ "SharedModel.Meta.Relics.StaticData.RelicStoneType", "Id", "System.Int32", false },
		{ "SharedModel.Meta.Relics.StaticData.RelicStoneType", "Name", "SharedModel.Common.Localization.SharedLTextKey", false },
		{ "SharedModel.Meta.Relics.StaticData.RelicStoneType", "SkillIds", "System.Collections.Generic.List`1<System.Int32>", false },
		// What the Relics reader writes beside the route (https://github.com/ANicholasson/raid-bridge/issues/8): a stone's
		// rarity, shape and description, and the skills a type adds for its wearer's Faction, each entry with its own
		// description. A skill's own name is only placeholder text, so the descriptions stand in for it.
		{ "SharedModel.Meta.Relics.StaticData.RelicStoneType", "Description", "SharedModel.Common.Localization.SharedLTextKey", false },
		{ "SharedModel.Meta.Relics.StaticData.RelicStoneType", "Rarity", "SharedModel.Meta.ItemRarity", false },
		{ "SharedModel.Meta.Relics.StaticData.RelicStoneType", "Shape", "SharedModel.Meta.Relics.StaticData.RelicStoneShapeKindId", false },
		{ "SharedModel.Meta.Relics.StaticData.RelicFactionSkillBonus", "FractionId", "SharedModel.Meta.Heroes.HeroFraction", false },
		{ "SharedModel.Meta.Relics.StaticData.RelicFactionSkillBonus", "SkillIds", "System.Collections.Generic.List`1<System.Int32>", false },
		{ "SharedModel.Meta.Relics.StaticData.RelicFactionSkillBonus", "Description", "SharedModel.Common.Localization.SharedLTextKey", false },
		// Stat sources (https://github.com/ANicholasson/raid-bridge/issues/9, docs/research/2026-11-champion-stat-sources.md).
		// Base stats are on the first form of the Champion's own, possibly ascended, type, scaled by BattleStats' static
		// Multipliers, which the Game client holds as a double[,], and divided into fractions by its static ToPercent.
		{ "SharedModel.Meta.Heroes.HeroForm", "BaseStats", "SharedModel.Meta.Heroes.BattleStats", false },
		{ "SharedModel.Meta.Heroes.BattleStats", "Health", "Plarium.Common.Numerics.Fixed", false },
		{ "SharedModel.Meta.Heroes.BattleStats", "Attack", "Plarium.Common.Numerics.Fixed", false },
		{ "SharedModel.Meta.Heroes.BattleStats", "Defence", "Plarium.Common.Numerics.Fixed", false },
		{ "SharedModel.Meta.Heroes.BattleStats", "Speed", "Plarium.Common.Numerics.Fixed", false },
		{ "SharedModel.Meta.Heroes.BattleStats", "Resistance", "Plarium.Common.Numerics.Fixed", false },
		{ "SharedModel.Meta.Heroes.BattleStats", "Accuracy", "Plarium.Common.Numerics.Fixed", false },
		{ "SharedModel.Meta.Heroes.BattleStats", "CriticalChance", "Plarium.Common.Numerics.Fixed", false },
		{ "SharedModel.Meta.Heroes.BattleStats", "CriticalDamage", "Plarium.Common.Numerics.Fixed", false },
		{ "SharedModel.Meta.Heroes.BattleStats", "Multipliers", "System.Double[][]", true },
		{ "SharedModel.Meta.Heroes.BattleStats", "ToPercent", "Plarium.Common.Numerics.Fixed", true },
		// The static bonus tables: masteries, sets, Faction Guardians and Empowerment, and Awakening.
		{ "SharedModel.Meta.ClientStaticData", "MasteryData", "SharedModel.Meta.Masteries.StaticMasteryData", false },
		{ "SharedModel.Meta.Masteries.StaticMasteryData", "MasteryBonusById", "System.Collections.Generic.Dictionary`2<System.Int32, SharedModel.Meta.General.StatBonus>", false },
		// Every mastery, so an id that isn't one is refused (https://github.com/ANicholasson/raid-bridge/issues/12).
		{ "SharedModel.Meta.Masteries.StaticMasteryData", "MasteryById", "System.Collections.Generic.Dictionary`2<System.Int32, SharedModel.Meta.Masteries.MasteryType>", false },
		{ "SharedModel.Meta.General.StatBonus", "StatKindId", "SharedModel.Battle.Effects.StatKindId", false },
		{ "SharedModel.Meta.General.StatBonus", "Value", "Plarium.Common.Numerics.Fixed", false },
		{ "SharedModel.Meta.General.StatBonus", "IsAbsolute", "System.Boolean", false },
		{ "SharedModel.Meta.Artifacts.Sets.ArtifactSetInfo", "ArtifactCount", "System.Int32", false },
		{ "SharedModel.Meta.Artifacts.Sets.ArtifactSetInfo", "StatBonus", "SharedModel.Meta.General.StatBonus", false },
		{ "SharedModel.Meta.Artifacts.Sets.ArtifactSetInfo", "StatBonuses", "System.Collections.Generic.List`1<SharedModel.Meta.General.StatBonus>", false },
		// Sets that grow by the piece, and the second part of a Glyph (https://github.com/ANicholasson/raid-bridge/issues/11).
		{ "SharedModel.Meta.Artifacts.Sets.ArtifactSetInfo", "SubSetInfos", "System.Collections.Generic.List`1<SharedModel.Meta.Artifacts.Sets.ArtifactSubSetInfo>", false },
		{ "SharedModel.Meta.Artifacts.Sets.ArtifactSubSetInfo", "ArtifactCount", "System.Int32", false },
		{ "SharedModel.Meta.Artifacts.Sets.ArtifactSubSetInfo", "StatBonuses", "System.Collections.Generic.List`1<SharedModel.Meta.General.StatBonus>", false },
		{ "SharedModel.Meta.Artifacts.Bonuses.ArtifactBonus", "_rarityBasedPowerUpValue", "System.Nullable`1<Plarium.Common.Numerics.Fixed>", false },
		{ "SharedModel.Meta.ClientStaticData", "AcademyData", "SharedModel.Meta.Academy.Static.StaticAcademyData", false },
		{ "SharedModel.Meta.Academy.Static.StaticAcademyData", "Guardians", "SharedModel.Meta.Academy.Static.StaticAcademyGuardiansData", false },
		{ "SharedModel.Meta.Academy.Static.StaticAcademyGuardiansData", "BonusesByHeroRarity", "System.Collections.Generic.Dictionary`2<SharedModel.Meta.Heroes.HeroRarity, System.Collections.Generic.List`1<SharedModel.Meta.Academy.Static.StatBonuses>>", false },
		{ "SharedModel.Meta.Academy.Static.StaticAcademyData", "Empower", "SharedModel.Meta.Academy.Static.StaticAcademyEmpowerData", false },
		{ "SharedModel.Meta.Academy.Static.StaticAcademyEmpowerData", "Bonuses", "System.Collections.Generic.List`1<SharedModel.Meta.Academy.Static.StatBonuses>", false },
		{ "SharedModel.Meta.Academy.Static.StaticAcademyEmpowerData", "EpicBonuses", "System.Collections.Generic.List`1<SharedModel.Meta.Academy.Static.StatBonuses>", false },
		{ "SharedModel.Meta.Academy.Static.StatBonuses", "Bonuses", "System.Collections.Generic.Dictionary`2<SharedModel.Battle.Effects.StatKindId, SharedModel.Meta.Artifacts.BonusValue>", false },
		{ "SharedModel.Meta.ClientStaticData", "DoubleAscendData", "SharedModel.Meta.DoubleAscend.Static.StaticDoubleAscendData", false },
		{ "SharedModel.Meta.DoubleAscend.Static.StaticDoubleAscendData", "BlessingStatsByRarity", "System.Collections.Generic.Dictionary`2<SharedModel.Meta.Heroes.HeroRarity, System.Collections.Generic.List`1<SharedModel.Meta.DoubleAscend.Static.BlessingStats>>", false },
		{ "SharedModel.Meta.DoubleAscend.Static.BlessingStats", "Grade", "SharedModel.Meta.DoubleAscend.User.DoubleAscendGrade", false },
		{ "SharedModel.Meta.DoubleAscend.Static.BlessingStats", "StatBonusByKindId", "System.Collections.Generic.Dictionary`2<SharedModel.Battle.Effects.StatKindId, SharedModel.Meta.General.StatBonus>", false },
		// Each blessing names the stats each Awakening grade raises (https://github.com/ANicholasson/raid-bridge/issues/14).
		{ "SharedModel.Meta.DoubleAscend.Static.StaticDoubleAscendData", "BlessingById", "System.Collections.Generic.Dictionary`2<SharedModel.Meta.DoubleAscend.Static.BlessingTypeId, SharedModel.Meta.DoubleAscend.Static.BlessingType>", false },
		{ "SharedModel.Meta.DoubleAscend.Static.BlessingType", "Id", "SharedModel.Meta.DoubleAscend.Static.BlessingTypeId", false },
		{ "SharedModel.Meta.DoubleAscend.Static.BlessingType", "GradeBonuses", "System.Collections.Generic.Dictionary`2<SharedModel.Meta.DoubleAscend.User.DoubleAscendGrade, SharedModel.Meta.DoubleAscend.User.BlessingBonus>", false },
		{ "SharedModel.Meta.DoubleAscend.User.BlessingBonus", "StatKindIds", "System.Collections.Generic.List`1<SharedModel.Battle.Effects.StatKindId>", false },
		// The game's own bonus breakdown for a Champion in a battle setup, which the Arena defence teams are made of.
		{ "SharedModel.Battle.Core.Setup.HeroSlotSetup", "BuildingSetup", "SharedModel.Battle.Core.Setup.BuildingSetup", false },
		{ "SharedModel.Battle.Core.Setup.HeroSlotSetup", "AcademySetup", "SharedModel.Battle.Core.Setup.AcademySetup", false },
		{ "SharedModel.Battle.Core.Setup.HeroSlotSetup", "BlessingSetup", "SharedModel.Battle.Core.Setup.BlessingSetup", false },
		{ "SharedModel.Battle.Core.Setup.HeroSlotSetup", "RelicSetup", "SharedModel.Battle.Core.Setup.RelicSetup", false },
		{ "SharedModel.Battle.Core.Setup.HeroSlotSetup", "ArtifactSetups", "System.Collections.Generic.List`1<SharedModel.Battle.Core.Setup.ArtifactSetup>", false },
		{ "SharedModel.Battle.Core.Setup.AcademySetup", "PercentBonus", "SharedModel.Meta.Heroes.BattleStats", false },
		{ "SharedModel.Battle.Core.Setup.AcademySetup", "FlatBonus", "SharedModel.Meta.Heroes.BattleStats", false },
		// The class library's collections, read through the fields their runtime classes hold.
		{ "System.Collections.Generic.List`1", "_items", "T[]", false },
		{ "System.Collections.Generic.List`1", "_size", "System.Int32", false },
		{ "System.Collections.Generic.Dictionary`2", "_entries", "System.Collections.Generic.Dictionary`2+Entry<TKey, TValue>[]", false },
		{ "System.Collections.Generic.Dictionary`2", "_count", "System.Int32", false },
		{ "System.Collections.Generic.Dictionary`2", "_freeCount", "System.Int32", false },
		{ "System.Collections.Generic.Dictionary`2+Entry", "hashCode", "System.Int32", false },
		{ "System.Collections.Generic.Dictionary`2+Entry", "key", "TKey", false },
		{ "System.Collections.Generic.Dictionary`2+Entry", "value", "TValue", false },
		{ "System.Nullable`1", "hasValue", "System.Boolean", false },
		{ "System.Nullable`1", "value", "T", false },
	};

	/// <summary>
	/// A member of each enum a Snapshot is written with, and its value, as the installed game declares them at metadata
	/// v39: a Champion's and an Artifact's, the keys of the Resources, Shards, Items, the Great Hall and the Faction
	/// Guardians, the Arena's leagues, and a Relic's rank and its sockets' shapes
	/// (https://github.com/ANicholasson/raid-bridge/issues/8).
	/// </summary>
	public static TheoryData<string, string, long> SnapshotEnums => new()
	{
		{ "SharedModel.Meta.Heroes.HeroGrade", "Stars6", 6 },
		{ "SharedModel.Meta.Heroes.HeroRarity", "Legendary", 5 },
		{ "SharedModel.Meta.Heroes.HeroFraction", "DarkElves", 11 },
		{ "SharedModel.Meta.Heroes.Element", "Void", 4 },
		{ "SharedModel.Meta.Heroes.HeroMarker", "None", 0 },
		{ "SharedModel.Meta.DoubleAscend.User.DoubleAscendGrade", "Stars1", 1 },
		{ "SharedModel.Meta.DoubleAscend.Static.BlessingTypeId", "Necromancy", 1101 },
		{ "SharedModel.Meta.Artifacts.ArtifactKindId", "Weapon", 5 },
		{ "SharedModel.Meta.Artifacts.ArtifactRankId", "Six", 6 },
		{ "SharedModel.Meta.ItemRarity", "Legendary", 5 },
		{ "SharedModel.Meta.Artifacts.Sets.ArtifactSetKindId", "AttackSpeed", 4 },
		{ "SharedModel.Battle.Effects.StatKindId", "Speed", 4 },
		// HeroFraction's 0 is an Artifact with no Faction lock.
		{ "SharedModel.Meta.Heroes.HeroFraction", "Unknown", 0 },
		// ArtifactSetKindId's 0 is an Artifact in no set (#33).
		{ "SharedModel.Meta.Artifacts.Sets.ArtifactSetKindId", "None", 0 },
		// The keys of Resources, Shards and Items (#34). At v39 none of the three enums declares a member for 0.
		{ "SharedModel.Meta.Account.ResourceTypeId", "Energy", 1 },
		{ "SharedModel.Meta.Account.ResourceTypeId", "Silver", 2 },
		{ "SharedModel.Meta.Account.ResourceTypeId", "Gem", 4 },
		{ "SharedModel.Meta.Shards.ShardTypeId", "Mystery", 1 },
		{ "SharedModel.Meta.Shards.ShardTypeId", "Ancient", 3 },
		{ "SharedModel.Meta.Shards.ShardTypeId", "Legendary", 5 },
		{ "SharedModel.Meta.Shards.ShardTypeId", "Void", 6 },
		{ "SharedModel.Meta.Shards.ShardTypeId", "Mythical", 7 },
		{ "SharedModel.Meta.BlackMarket.BlackMarketItemId", "HeroXpBooster100P_1Day", 3001 },
		{ "SharedModel.Meta.BlackMarket.BlackMarketItemId", "SkillUpgradeItemEpic", 5002 },
		// The keys of the Great Hall (#35): each Affinity, and each stat the Great Hall raises. At v39 neither enum declares
		// a member for 0.
		{ "SharedModel.Meta.Heroes.Element", "Magic", 1 },
		{ "SharedModel.Meta.Heroes.Element", "Force", 2 },
		{ "SharedModel.Meta.Heroes.Element", "Spirit", 3 },
		{ "SharedModel.Battle.Effects.StatKindId", "Health", 1 },
		{ "SharedModel.Battle.Effects.StatKindId", "Attack", 2 },
		{ "SharedModel.Battle.Effects.StatKindId", "Defence", 3 },
		{ "SharedModel.Battle.Effects.StatKindId", "Resistance", 5 },
		{ "SharedModel.Battle.Effects.StatKindId", "Accuracy", 6 },
		{ "SharedModel.Battle.Effects.StatKindId", "CriticalDamage", 8 },
		// The keys of the Faction Guardians (#36): each Faction, and each rarity. HeroFraction's 0, Unknown, is pinned
		// above; HeroRarity declares no member for 0 at v39.
		{ "SharedModel.Meta.Heroes.HeroFraction", "BannerLords", 1 },
		{ "SharedModel.Meta.Heroes.HeroFraction", "Olympians", 17 },
		{ "SharedModel.Meta.Heroes.HeroRarity", "Common", 1 },
		{ "SharedModel.Meta.Heroes.HeroRarity", "Mythical", 6 },
		// The Arena's leagues (#37), both Arenas': None, written as no league, and the first and last of each tier.
		{ "SharedModel.Meta.Battle.Arena.ArenaLeagueId", "None", 0 },
		{ "SharedModel.Meta.Battle.Arena.ArenaLeagueId", "BronzeI", 1 },
		{ "SharedModel.Meta.Battle.Arena.ArenaLeagueId", "BronzeIV", 4 },
		{ "SharedModel.Meta.Battle.Arena.ArenaLeagueId", "SilverI", 11 },
		{ "SharedModel.Meta.Battle.Arena.ArenaLeagueId", "SilverIV", 14 },
		{ "SharedModel.Meta.Battle.Arena.ArenaLeagueId", "GoldI", 21 },
		{ "SharedModel.Meta.Battle.Arena.ArenaLeagueId", "GoldV", 25 },
		{ "SharedModel.Meta.Battle.Arena.ArenaLeagueId", "Platinum", 30 },
		// Relics (https://github.com/ANicholasson/raid-bridge/issues/7): the rank's first and last members, and the socket
		// shapes'. At v39 neither enum declares a member for 0.
		{ "SharedModel.Meta.Relics.StaticData.RelicRank", "Rank1", 1 },
		{ "SharedModel.Meta.Relics.StaticData.RelicRank", "Rank6", 6 },
		{ "SharedModel.Meta.Relics.StaticData.RelicStoneShapeKindId", "Circle", 1 },
		{ "SharedModel.Meta.Relics.StaticData.RelicStoneShapeKindId", "Pentagon", 5 },
	};

	/// <summary>Classes that aren't generic, whose runtime class the reader takes from their TypeInfo slot (#26).</summary>
	public static TheoryData<string> TypeInfoSlots => new()
	{
		"SharedModel.Meta.Artifacts.ArtifactStorage.ArtifactStorageResolver",
		"Plarium.Common.Numerics.Fixed",
		"SharedModel.Meta.Heroes.BattleStats",
	};

	/// <summary>Classes the path reaches through a base class or an interface-typed field, not a field of their own type.</summary>
	public static TheoryData<string, string> PathRelations => new()
	{
		{ "Client.Model.AppModel", "Client.App.SingleInstance`1<Client.Model.AppModel>" },
		{ "Client.Model.Gameplay.Artifacts.ExternalArtifactsStorage", "SharedModel.Meta.Artifacts.ArtifactStorage.IArtifactStorageAdapter" },
		{ "Client.Model.Gameplay.StaticData.ClientStaticDataManager", "Client.Model.Gameplay.StaticData.IClientStaticDataManager" },
		{ "Client.Model.Gameplay.Village.Data.UpdatableVillageData", "SharedModel.Meta.Village.UserVillageData" },
		{ "Client.Model.Gameplay.Academy.Data.UpdatableUserAcademyData", "SharedModel.Meta.Academy.User.UserAcademyData" },
		{ "Client.Model.Gameplay.Battle.Data.UpdatableArena3x3Data", "SharedModel.Meta.Battle.Arena3x3.UserArena3X3Data" },
		{ "SharedModel.Meta.Battle.Arena3x3.UserArena3X3Data", "SharedModel.Meta.Battle.Arena.UserArenaData" },
		{ "Client.Model.Gameplay.Relics.UpdatableUserRelicData", "SharedModel.Meta.Relics.UserData.UserRelicData" },
		{ "SharedModel.Meta.ClientStaticData", "SharedModel.Meta.StaticData" },
	};

	private static readonly Lazy<GameMetadata> Installed = new(() => GameMetadata.Load(GameFiles.In(GameDirectory())));

	/// <summary>Load refuses a parse LibCpp2IL warned about, so loading at all means a clean parse.</summary>
	[Fact]
	public void Parses_the_installed_files_at_the_supported_versions() =>
		Assert.Equal(new GameVersions(39, "6000.3.18f1"), InstalledMetadata().Versions);

	[Theory]
	[MemberData(nameof(AccountPath))]
	public void Resolves_each_field_on_the_path_to_the_account(string className, string fieldName, string type, bool isStatic)
	{
		var field = InstalledMetadata().Field(className, fieldName);

		Assert.Equal((type, isStatic), (field.Type, field.IsStatic));
	}

	[Theory]
	[MemberData(nameof(SnapshotEnums))]
	public void Resolves_each_enum_a_snapshot_is_written_with(string enumName, string memberName, long value)
	{
		var declared = InstalledMetadata().Enum(enumName);

		Assert.Equal("System.Int32", declared.UnderlyingType);
		Assert.Contains(new GameEnumMember(memberName, value), declared.Members);
	}

	[Theory]
	[MemberData(nameof(PathRelations))]
	public void Resolves_each_base_class_and_interface_on_the_path(string className, string baseTypeOrInterface)
	{
		var gameClass = InstalledMetadata().Class(className);

		Assert.Contains(baseTypeOrInterface, gameClass.Interfaces.Prepend(gameClass.BaseType));
	}

	/// <summary>The IL2CPP runtime writes the root's runtime class into its generic class record, so the record is in writable data.</summary>
	[Fact]
	public void Locates_the_generic_class_record_of_the_root_in_writable_data()
	{
		var rva = InstalledMetadata().GenericClassRva("Client.App.SingleInstance`1<Client.Model.AppModel>");

		Assert.True(WritableSection(rva), $"The record at 0x{rva:X} isn't in writable data.");
	}

	/// <summary>The IL2CPP runtime writes the class into its TypeInfo slot, so the slot is in writable data.</summary>
	[Theory]
	[MemberData(nameof(TypeInfoSlots))]
	public void Locates_the_type_info_slot_of_each_class_in_writable_data(string className)
	{
		var rva = InstalledMetadata().TypeInfoRva(className);

		Assert.True(WritableSection(rva), $"The TypeInfo slot of {className} at 0x{rva:X} isn't in writable data.");
	}

	/// <summary>
	/// A mastery bonus with this StatKindId raises set bonuses, as Lore of Steel does; it is -1, which no StatKindId
	/// names (https://github.com/ANicholasson/raid-bridge/issues/12).
	/// </summary>
	[Fact]
	public void Reads_the_marker_of_a_mastery_that_raises_set_bonuses()
	{
		var metadata = InstalledMetadata();

		Assert.Equal(-1, metadata.Constant("SharedModel.Meta.Masteries.StaticMasteryData", "BonusSourceArtifactSet"));
		Assert.DoesNotContain(metadata.Enum("SharedModel.Battle.Effects.StatKindId").Members, member => member.Value == -1);
	}

	/// <summary>Fixed is Q32.32: its scale, ONE, is 2^32 (#26).</summary>
	[Fact]
	public void Reads_the_scale_of_the_fixed_point_numbers() =>
		Assert.Equal(1L << 32, InstalledMetadata().Constant("Plarium.Common.Numerics.Fixed", "ONE"));

	private static bool WritableSection(ulong rva)
	{
		using var file = File.OpenRead(GameFiles.In(GameDirectory()).GameAssembly);
		using var pe = new PEReader(file);
		var section = pe.PEHeaders.SectionHeaders.Single(header => rva >= (ulong)header.VirtualAddress && rva < (ulong)(header.VirtualAddress + header.VirtualSize));
		return section.SectionCharacteristics.HasFlag(SectionCharacteristics.MemWrite);
	}

	private static GameMetadata InstalledMetadata()
	{
		Assert.SkipUnless(Environment.GetEnvironmentVariable("RAIDBRIDGE_LIVE") == "1", "Gated: set RAIDBRIDGE_LIVE=1 and RAIDBRIDGE_GAME_DIR to run.");
		return Installed.Value;
	}

	/// <summary>Opting in without the game is a failure, not a skip, so a gated run never passes by reading nothing.</summary>
	private static string GameDirectory() =>
		Environment.GetEnvironmentVariable("RAIDBRIDGE_GAME_DIR") is { Length: > 0 } directory
			? directory
			: throw new InvalidOperationException("RAIDBRIDGE_LIVE=1 needs RAIDBRIDGE_GAME_DIR, the folder holding Raid.exe.");
}
