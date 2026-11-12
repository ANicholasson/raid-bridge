using RaidBridge.Domain.Artifacts;
using RaidBridge.Domain.Champions;
using RaidBridge.Domain.Relics;
using RaidBridge.Domain.Stats;
using RaidBridge.Game.Artifacts;
using RaidBridge.Game.Il2Cpp;
using RaidBridge.Game.Localization;
using RaidBridge.Game.Relics;

namespace RaidBridge.Game.Champions;

/// <summary>
/// Reads every Champion on the Account from the game's model: the heroes in <c>_userWrapper.Heroes.HeroData.HeroById</c>,
/// each with its hero type and skill types from the static data, by id, and the Artifacts it wears (spec #20). Only
/// fields are read, never the properties that run game code, such as <c>Hero.Type</c>.
/// </summary>
internal static class ChampionReader
{
	private const string Hero = "SharedModel.Meta.Heroes.Hero";
	private const string HeroType = "SharedModel.Meta.Heroes.HeroType";
	private const string HeroForm = "SharedModel.Meta.Heroes.HeroForm";
	private const string Skill = "SharedModel.Meta.Skills.Skill";
	private const string SkillType = "SharedModel.Meta.Skills.SkillType";

	/// <summary>Every Champion, each with the Artifacts <paramref name="equipped"/> and the Relics <paramref name="relics"/> say it wears, by the Champion's id.</summary>
	public static IReadOnlyList<Champion> Read(
		ManagedObject userWrapper,
		ManagedObject staticData,
		LocalizedNames names,
		IReadOnlyDictionary<int, IReadOnlyList<Artifact>> equipped,
		ArtifactSetBonuses sets,
		IReadOnlyDictionary<int, IReadOnlyList<Relic>> relics,
		AccountStatBonuses accountBonuses)
	{
		var heroTypes = staticData
			.Object("HeroData", "SharedModel.Meta.Heroes.StaticHeroData")
			.Dictionary("HeroTypeById", $"System.Collections.Generic.Dictionary`2<System.Int32, {HeroType}>")
			.ByInt32Key();
		var skillTypes = staticData
			.Object("SkillData", "SharedModel.Meta.Skills.StaticSkillData")
			.Dictionary("_skillTypeById", $"System.Collections.Generic.Dictionary`2<System.Int32, {SkillType}>")
			.ByInt32Key();

		var heroes = userWrapper
			.Object("Heroes", "Client.Model.Gameplay.Heroes.HeroesWrapper")
			.Object("HeroData", "Client.Model.Gameplay.Heroes.Data.UpdatableHeroData")
			.Dictionary("HeroById", $"System.Collections.Generic.Dictionary`2<System.Int32, {Hero}>")
			.ByInt32Key();
		// Read only once a Champion needs them, so an Account with none doesn't depend on them.
		var statReaders = new StatReaders(
			new Lazy<BaseStatsReader>(() => BaseStatsReader.Read(staticData.Runtime)),
			new Lazy<MasteryBonusReader>(() => MasteryBonusReader.Read(staticData)),
			new Lazy<AwakeningBonusReader>(() => AwakeningBonusReader.Read(staticData)),
			new Lazy<EmpowermentBonusReader>(() => EmpowermentBonusReader.Read(staticData)));
		var champions = heroes.Select(hero => Champion(
			hero.Key,
			hero.Value.Object(Hero),
			Worn(hero.Key, equipped.GetValueOrDefault(hero.Key, []), sets, relics.GetValueOrDefault(hero.Key, [])),
			heroTypes,
			skillTypes,
			names,
			statReaders,
			accountBonuses)).ToList();

		// Artifacts or Relics worn by a Champion the Account doesn't have would be dropped without a word.
		RefuseStrays(equipped, heroes.Keys, "SharedModel.Meta.Artifacts.UserArtifactData.ArtifactDataByHeroId gives Artifacts");
		RefuseStrays(relics, heroes.Keys, "SharedModel.Meta.Relics.UserData.UserRelicData.RelicDataByHeroId gives Relics");
		return champions;
	}

	private static void RefuseStrays<T>(IReadOnlyDictionary<int, IReadOnlyList<T>> worn, IEnumerable<int> heroIds, string gives)
	{
		var strays = worn.Where(champion => champion.Value.Count > 0).Select(champion => champion.Key).Except(heroIds).Order().ToList();
		if (strays.Count > 0)
		{
			throw new GameDataException($"{gives} to Champion {string.Join(" and ", strays)}, which HeroById doesn't hold.");
		}
	}

	private static Champion Champion(
		int key,
		ManagedObject hero,
		Wearing worn,
		IReadOnlyDictionary<int, ManagedDictionary.Entry> heroTypes,
		IReadOnlyDictionary<int, ManagedDictionary.Entry> skillTypes,
		LocalizedNames names,
		StatReaders statReaders,
		AccountStatBonuses accountBonuses)
	{
		var id = hero.Int32("Id").Where(value => value == key && value > 0, $"the positive id HeroById keeps it under, {key}");
		var typeId = hero.Int32("TypeId");
		var type = heroTypes.TryGetValue(typeId.Value, out var heroType)
			? heroType.Object(HeroType)
			: throw new GameDataException($"{typeId}, which isn't a type in the game's StaticHeroData.HeroTypeById.");
		// A corrupt entry or a torn read could key one type's data under another's id (CodeRabbit, PR #30).
		_ = type.Int32("Id").Where(value => value == typeId.Value, $"the id HeroTypeById keeps it under, {typeId.Value}");
		var doubleAscend = hero.ObjectOrNull("DoubleAscendData", "SharedModel.Meta.DoubleAscend.User.HeroDoubleAscendData");
		var masteries = hero.ObjectOrNull("MasteryData", "SharedModel.Meta.Masteries.HeroMasteryData");
		var rank = hero.EnumMember("Grade", "SharedModel.Meta.Heroes.HeroGrade");
		var level = hero.Int32("Level");
		var rarity = type.Enum("Rarity", "SharedModel.Meta.Heroes.HeroRarity").Value;
		var awakening = doubleAscend?.EnumMember("Grade", "SharedModel.Meta.DoubleAscend.User.DoubleAscendGrade").Value;
		var blessing = doubleAscend?.NullableEnum("BlessingId", "SharedModel.Meta.DoubleAscend.Static.BlessingTypeId").Value;
		var empowerment = hero.Int32("EmpowerLevel").Where(value => value >= 0, "an empowerment of 0 or more");
		var affinity = Affinity(type, typeId.Value);
		var faction = type.Enum("Fraction", "SharedModel.Meta.Heroes.HeroFraction").Value;
		// A Champion that never took a mastery has no mastery data at all.
		IReadOnlyList<int> masteryIds = masteries is null
			? []
			: [.. masteries.List("Masteries", "System.Collections.Generic.List`1<System.Int32>").Int32s().Select(mastery => mastery.Where(value => value > 0, "a positive mastery id"))];

		return new(
			Id: id,
			TypeId: typeId.Value,
			Name: names.Of(type, "Name", $"Champion type {typeId.Value}"),
			Rarity: rarity,
			Affinity: affinity,
			Faction: faction,
			Rank: rank.Value.Name,
			Level: level.Where(value => value > 0, "a positive level"),
			// A Champion that was never awakened has no awakening data at all.
			Awakening: awakening?.Name,
			Blessing: blessing,
			Empowerment: empowerment,
			Skills: [.. hero.List("Skills", $"System.Collections.Generic.List`1<{Skill}>").Objects(Skill).Select(skill => ChampionSkill(skill, skillTypes, names))],
			Masteries: masteryIds,
			Locked: hero.Boolean("Locked").Value,
			Marker: hero.Enum("Marker", "SharedModel.Meta.Heroes.HeroMarker").Value,
			Location: Location(hero, id),
			Artifacts: worn.Artifacts,
			Relics: worn.Relics,
			Stats: StatsFormula.For(
				statReaders.BaseStats.Value.Of(id, type, typeId.Value, rank, level),
				new ChampionBonuses(
					worn.ArtifactBonuses,
					worn.SetBonuses,
					masteryIds.Count == 0 ? MasteryBonuses.None : statReaders.Masteries.Value.For(id, masteryIds),
					accountBonuses.GreatHall.GetValueOrDefault(affinity, []),
					accountBonuses.FactionGuardians.GetValueOrDefault((faction, rarity), []),
					// A Champion with no blessing gets nothing from its Awakening, as the game's InitBlessing gives it nothing.
					awakening is null || blessing is null ? [] : statReaders.Awakening.Value.For(id, rarity, awakening.Value, blessing),
					empowerment == 0 ? [] : statReaders.Empowerment.Value.For(id, rarity, empowerment),
					worn.RelicBonuses,
					accountBonuses.ArenaLeague)));
	}

	/// <summary>
	/// What the Account gives every Champion of an Affinity, Faction and rarity: its Great Hall by Affinity, its
	/// Faction Guardians by Faction and rarity, and its Classic Arena league to every Champion.
	/// </summary>
	internal sealed record AccountStatBonuses(
		IReadOnlyDictionary<string, IReadOnlyList<StatBonus>> GreatHall,
		IReadOnlyDictionary<(string Faction, string Rarity), IReadOnlyList<StatBonus>> FactionGuardians,
		IReadOnlyList<StatBonus> ArenaLeague);

	/// <summary>The static data that scales Champions and gives their masteries' bonuses, each read once a Champion needs it.</summary>
	private sealed record StatReaders(
		Lazy<BaseStatsReader> BaseStats,
		Lazy<MasteryBonusReader> Masteries,
		Lazy<AwakeningBonusReader> Awakening,
		Lazy<EmpowermentBonusReader> Empowerment);

	/// <summary>
	/// What a Champion wears, its Artifacts in the game's order of slots and its Relics, and the bonuses its Artifacts,
	/// their sets and its Relics give it.
	/// </summary>
	private sealed record Wearing(IReadOnlyList<Artifact> Artifacts, IReadOnlyList<StatBonus> ArtifactBonuses, IReadOnlyList<StatBonus> SetBonuses, IReadOnlyList<Relic> Relics, IReadOnlyList<StatBonus> RelicBonuses);

	private static Wearing Worn(int championId, IReadOnlyList<Artifact> artifacts, ArtifactSetBonuses sets, IReadOnlyList<Relic> relics) =>
		new(artifacts, [.. artifacts.SelectMany(ArtifactSetBonuses.Of)], sets.For(championId, artifacts), relics, [.. relics.SelectMany(RelicStats.Of)]);

	/// <summary>A hero type keeps its Affinity on each of its forms, so they must agree.</summary>
	private static string Affinity(ManagedObject type, int typeId)
	{
		var forms = type.Array("Forms", $"{HeroForm}[]");
		var affinities = Enumerable.Range(0, forms.Length)
			.Select(index => forms.Object(index, HeroForm).Enum("Element", "SharedModel.Meta.Heroes.Element").Value)
			.Distinct(StringComparer.Ordinal)
			.ToList();
		return affinities.Count == 1
			? affinities[0]
			: throw new GameDataException(
				$"{forms.Where} of Champion type {typeId} points to forms at 0x{forms.Address:X} that give {(affinities.Count == 0 ? "no Affinity" : $"the Affinities {string.Join(" and ", affinities)}")}, where raid-bridge expects one.");
	}

	private static Skill ChampionSkill(ManagedObject skill, IReadOnlyDictionary<int, ManagedDictionary.Entry> skillTypes, LocalizedNames names)
	{
		var typeId = skill.Int32("TypeId");
		var type = skillTypes.TryGetValue(typeId.Value, out var skillType)
			? skillType.Object(SkillType)
			: throw new GameDataException($"{typeId}, which isn't a type in the game's StaticSkillData._skillTypeById.");
		_ = type.Int32("Id").Where(value => value == typeId.Value, $"the id _skillTypeById keeps it under, {typeId.Value}");
		return new(typeId.Value, names.Of(type, "Name", $"Skill type {typeId.Value}"), skill.Int32("Level").Where(level => level > 0, "a positive level"));
	}

	private static ChampionLocation Location(ManagedObject hero, int id)
	{
		var inVault = hero.Boolean("InStorage");
		var inDeepVault = hero.Boolean("InBathhouse");
		return (inVault.Value, inDeepVault.Value) switch
		{
			(false, false) => ChampionLocation.Roster,
			(true, false) => ChampionLocation.Vault,
			(false, true) => ChampionLocation.DeepVault,
			_ => throw new GameDataException($"Champion {id} is in both the Vault and the Deep Vault: {inVault}, and {inDeepVault}."),
		};
	}
}
