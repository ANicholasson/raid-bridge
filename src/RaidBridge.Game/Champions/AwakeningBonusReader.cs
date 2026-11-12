using RaidBridge.Domain.Stats;
using RaidBridge.Game.Il2Cpp;
using RaidBridge.Game.Numerics;
using RaidBridge.Game.Stats;

namespace RaidBridge.Game.Champions;

/// <summary>
/// What a Champion's Awakening gives it, from the static data's <c>DoubleAscendData</c>, as the game's own
/// <c>BattleBonuses.InitBlessing</c> and <c>DoubleAscendExtensions.GetStatBonusesBy</c> take it at metadata v39
/// (https://github.com/ANicholasson/raid-bridge/issues/14):
/// <list type="bullet">
/// <item>A Champion with no blessing gets nothing, however far it is awakened.</item>
/// <item>Its blessing's <c>GradeBonuses</c> name, for each Awakening grade up to the Champion's, the stats that grade
/// raises in its <c>StatKindIds</c>.</item>
/// <item>The value of each comes from <c>BlessingStatsByRarity</c> for the Champion's rarity: the entry of that grade,
/// its <c>StatBonusByKindId</c> of that stat.</item>
/// </list>
/// </summary>
internal sealed class AwakeningBonusReader
{
	private const string Grade = "SharedModel.Meta.DoubleAscend.User.DoubleAscendGrade";
	private const string BlessingTypeId = "SharedModel.Meta.DoubleAscend.Static.BlessingTypeId";
	private const string BlessingType = "SharedModel.Meta.DoubleAscend.Static.BlessingType";
	private const string BlessingStats = "SharedModel.Meta.DoubleAscend.Static.BlessingStats";
	private const string BlessingBonus = "SharedModel.Meta.DoubleAscend.User.BlessingBonus";
	private const string StatKind = "SharedModel.Battle.Effects.StatKindId";
	private const string StatBonusClass = "SharedModel.Meta.General.StatBonus";
	private const string Rarity = "SharedModel.Meta.Heroes.HeroRarity";
	private const string Where = "SharedModel.Meta.DoubleAscend.Static.StaticDoubleAscendData";

	private readonly IReadOnlyDictionary<string, ManagedDictionary.Entry> _blessings;
	private readonly IReadOnlyDictionary<string, ManagedDictionary.Entry> _statsByRarity;

	private AwakeningBonusReader(IReadOnlyDictionary<string, ManagedDictionary.Entry> blessings, IReadOnlyDictionary<string, ManagedDictionary.Entry> statsByRarity)
	{
		_blessings = blessings;
		_statsByRarity = statsByRarity;
	}

	public static AwakeningBonusReader Read(ManagedObject staticData)
	{
		var data = staticData.Object("DoubleAscendData", Where);
		return new(
			data.Dictionary("BlessingById", $"System.Collections.Generic.Dictionary`2<{BlessingTypeId}, {BlessingType}>").ByEnumKey(BlessingTypeId).ToDictionary(entry => entry.Key.Name, entry => entry.Value, StringComparer.Ordinal),
			data.Dictionary("BlessingStatsByRarity", $"System.Collections.Generic.Dictionary`2<{Rarity}, System.Collections.Generic.List`1<{BlessingStats}>>").ByEnumKey(Rarity).ToDictionary(entry => entry.Key.Name, entry => entry.Value, StringComparer.Ordinal));
	}

	/// <summary>What Champion <paramref name="championId"/>, of <paramref name="rarity"/>, awakened to <paramref name="grade"/> with <paramref name="blessing"/>, gets.</summary>
	public IReadOnlyList<StatBonus> For(int championId, string rarity, long grade, string blessing)
	{
		var type = _blessings.TryGetValue(blessing, out var entry)
			? entry.Object(BlessingType)
			: throw new GameDataException($"Champion {championId} has the blessing {blessing}, which isn't a blessing in the game's {Where}.BlessingById.");
		_ = type.EnumMember("Id", BlessingTypeId).Where(id => id.Name == blessing, $"the blessing BlessingById keeps it under, {blessing}");

		var bonuses = new List<StatBonus>();
		foreach (var (gradeKey, gradeBonus) in type.Dictionary("GradeBonuses", $"System.Collections.Generic.Dictionary`2<{Grade}, {BlessingBonus}>").ByEnumKey(Grade).OrderBy(entry => entry.Key.Value))
		{
			var stats = gradeBonus.Object(BlessingBonus).ListOrNull("StatKindIds", $"System.Collections.Generic.List`1<{StatKind}>")?.EnumMembers(StatKind) ?? [];
			if (gradeKey.Value > grade || stats.Count == 0)
			{
				continue;
			}

			var values = GradeStats(championId, rarity, gradeKey.Name);
			foreach (var stat in stats)
			{
				var bonus = values.TryGetValue(stat.Value, out var value)
					? value.Object(StatBonusClass)
					: throw new GameDataException(
						$"The blessing {blessing} raises {stat.Value.Name} at {gradeKey.Name}, but {Where}.BlessingStatsByRarity has no {stat.Value.Name} for a {rarity} Champion at {gradeKey.Name}.");
				if (StatNames.Of(stat.Value.Name, $"The blessing {blessing} at {gradeKey.Name}") is { } shown)
				{
					bonuses.Add(new StatBonus(shown, FixedPoint.Of(bonus, "Value"), bonus.Boolean("IsAbsolute").Value));
				}
			}
		}

		return bonuses;
	}

	/// <summary>The stat bonuses <c>BlessingStatsByRarity</c> gives a Champion of <paramref name="rarity"/> at <paramref name="grade"/>.</summary>
	private IReadOnlyDictionary<Metadata.GameEnumMember, ManagedDictionary.Entry> GradeStats(int championId, string rarity, string grade)
	{
		var forRarity = _statsByRarity.TryGetValue(rarity, out var entry)
			? entry.List().Objects(BlessingStats)
			: throw new GameDataException($"Champion {championId} is a {rarity} Champion, which {Where}.BlessingStatsByRarity holds no stats for.");
		var forGrade = forRarity.Where(stats => stats.Enum("Grade", Grade).Value == grade).ToList();
		return forGrade.Count == 1
			? forGrade[0].Dictionary("StatBonusByKindId", $"System.Collections.Generic.Dictionary`2<{StatKind}, {StatBonusClass}>").ByEnumKey(StatKind)
			: throw new GameDataException($"{Where}.BlessingStatsByRarity holds {forGrade.Count} entries for a {rarity} Champion at {grade}, where raid-bridge expects one.");
	}
}
