using RaidBridge.Domain.Stats;
using RaidBridge.Game.Il2Cpp;
using RaidBridge.Game.Stats;

namespace RaidBridge.Game.Champions;

/// <summary>
/// What a Champion's Empowerment gives it, from the static data's <c>AcademyData.Empower</c>, as the game's own
/// <c>BattleBonuses.InitEmpower</c> takes it at metadata v39 (https://github.com/ANicholasson/raid-bridge/issues/14):
/// the first <c>level</c> entries of <c>EpicBonuses</c> for an Epic Champion, when the game holds that list, or of
/// <c>Bonuses</c> otherwise, all of them added up, so a level gives its own bonuses and those of the levels below it.
/// </summary>
internal sealed class EmpowermentBonusReader(IReadOnlyList<ManagedObject> bonuses, IReadOnlyList<ManagedObject>? epicBonuses)
{
	private const string StatBonuses = "SharedModel.Meta.Academy.Static.StatBonuses";
	private const string StatKind = "SharedModel.Battle.Effects.StatKindId";
	private const string Where = "SharedModel.Meta.Academy.Static.StaticAcademyEmpowerData";

	public static EmpowermentBonusReader Read(ManagedObject staticData)
	{
		var empower = staticData
			.Object("AcademyData", "SharedModel.Meta.Academy.Static.StaticAcademyData")
			.Object("Empower", Where);
		var list = $"System.Collections.Generic.List`1<{StatBonuses}>";
		return new(empower.List("Bonuses", list).Objects(StatBonuses), empower.ListOrNull("EpicBonuses", list)?.Objects(StatBonuses));
	}

	/// <summary>What Champion <paramref name="championId"/>, of <paramref name="rarity"/>, empowered to <paramref name="level"/>, gets.</summary>
	public IReadOnlyList<StatBonus> For(int championId, string rarity, int level)
	{
		var (levels, name) = rarity == "Epic" && epicBonuses is not null ? (epicBonuses, "EpicBonuses") : (bonuses, "Bonuses");
		return level <= levels.Count
			? [.. levels.Take(level).SelectMany((entry, index) => entry
				.Dictionary("Bonuses", $"System.Collections.Generic.Dictionary`2<{StatKind}, SharedModel.Meta.Artifacts.BonusValue>")
				.ByEnumKey(StatKind)
				.OrderBy(bonus => bonus.Key.Value)
				.Select(bonus => StatNames.BonusOf(bonus.Key.Name, bonus.Value.Object("SharedModel.Meta.Artifacts.BonusValue"), $"{Where}.{name}[{index}]"))
				.OfType<StatBonus>())]
			: throw new GameDataException($"Champion {championId} is a {rarity} Champion empowered to level {level}, but {Where}.{name} holds {levels.Count} levels.");
	}
}
