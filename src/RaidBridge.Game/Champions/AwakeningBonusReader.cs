using RaidBridge.Domain.Stats;
using RaidBridge.Game.Il2Cpp;
using RaidBridge.Game.Stats;

namespace RaidBridge.Game.Champions;

/// <summary>
/// What a Champion's Awakening gives it, from the static data's <c>DoubleAscendData</c>, as the game's own
/// <c>BattleBonuses.InitBlessing</c> and <c>DoubleAscendExtensions.GetStatBonusesBy</c> take it at metadata v39
/// (https://github.com/ANicholasson/raid-bridge/issues/14):
/// <list type="bullet">
/// <item>A Champion with no blessing gets nothing, however far it is awakened.</item>
/// <item>Its blessing's <c>GradeBonuses</c> name, for each star of Awakening up to the Champion's, the stats that star
/// raises in its <c>StatKindIds</c>.</item>
/// <item>The value of each comes from <c>BlessingStatsByRarity</c> for the Champion's rarity: the entry of that star,
/// its <c>StatBonusByKindId</c> of that stat.</item>
/// </list>
/// </summary>
internal sealed class AwakeningBonusReader
{
	private const string DoubleAscendGrade = "SharedModel.Meta.DoubleAscend.User.DoubleAscendGrade";
	private const string BlessingTypeId = "SharedModel.Meta.DoubleAscend.Static.BlessingTypeId";
	private const string BlessingType = "SharedModel.Meta.DoubleAscend.Static.BlessingType";
	private const string BlessingStats = "SharedModel.Meta.DoubleAscend.Static.BlessingStats";
	private const string BlessingBonus = "SharedModel.Meta.DoubleAscend.User.BlessingBonus";
	private const string StatKind = "SharedModel.Battle.Effects.StatKindId";
	private const string StatBonusClass = "SharedModel.Meta.General.StatBonus";
	private const string Rarity = "SharedModel.Meta.Heroes.HeroRarity";
	private const string StaticDoubleAscendData = "SharedModel.Meta.DoubleAscend.Static.StaticDoubleAscendData";

	private readonly IReadOnlyDictionary<string, ManagedDictionary.Entry> _blessings;
	private readonly IReadOnlyDictionary<string, ManagedDictionary.Entry> _statsByRarity;

	private AwakeningBonusReader(IReadOnlyDictionary<string, ManagedDictionary.Entry> blessings, IReadOnlyDictionary<string, ManagedDictionary.Entry> statsByRarity)
	{
		_blessings = blessings;
		_statsByRarity = statsByRarity;
	}

	public static AwakeningBonusReader Read(ManagedObject staticData)
	{
		var data = staticData.Object("DoubleAscendData", StaticDoubleAscendData);
		return new(
			data.Dictionary("BlessingById", $"System.Collections.Generic.Dictionary`2<{BlessingTypeId}, {BlessingType}>").ByEnumKey(BlessingTypeId).ToDictionary(entry => entry.Key.Name, entry => entry.Value, StringComparer.Ordinal),
			data.Dictionary("BlessingStatsByRarity", $"System.Collections.Generic.Dictionary`2<{Rarity}, System.Collections.Generic.List`1<{BlessingStats}>>").ByEnumKey(Rarity).ToDictionary(entry => entry.Key.Name, entry => entry.Value, StringComparer.Ordinal));
	}

	/// <summary>
	/// What Champion <paramref name="championId"/>, of <paramref name="rarity"/>, awakened as far as the game's
	/// DoubleAscendGrade numbered <paramref name="awakening"/> with <paramref name="blessing"/>, gets.
	/// </summary>
	public IReadOnlyList<StatBonus> For(int championId, string rarity, long awakening, string blessing)
	{
		var type = _blessings.TryGetValue(blessing, out var entry)
			? entry.Object(BlessingType)
			: throw new GameDataException($"Champion {championId} has the blessing {blessing}, which isn't a blessing in the game's {StaticDoubleAscendData}.BlessingById.");
		_ = type.EnumMember("Id", BlessingTypeId).Where(id => id.Name == blessing, $"the blessing BlessingById keeps it under, {blessing}");

		var bonuses = new List<StatBonus>();
		foreach (var (star, starBonus) in type.Dictionary("GradeBonuses", $"System.Collections.Generic.Dictionary`2<{DoubleAscendGrade}, {BlessingBonus}>").ByEnumKey(DoubleAscendGrade).OrderBy(entry => entry.Key.Value))
		{
			// GetStatBonusesBy skips an entry whose StatKindIds is null or empty, so it raises nothing.
			var stats = starBonus.Object(BlessingBonus).ListOrNull("StatKindIds", $"System.Collections.Generic.List`1<{StatKind}>")?.EnumMembers(StatKind) ?? [];
			if (star.Value > awakening || stats.Count == 0)
			{
				continue;
			}

			var values = AwakeningStats(championId, rarity, star.Name);
			foreach (var stat in stats)
			{
				var bonus = values.TryGetValue(stat.Value, out var value)
					? value.Object(StatBonusClass)
					: throw new GameDataException(
						$"The blessing {blessing} raises {stat.Value.Name} at {star.Name}, but {StaticDoubleAscendData}.BlessingStatsByRarity has no {stat.Value.Name} for a {rarity} Champion at {star.Name}.");
				if (StatNames.BonusOfStatBonus(stat.Value.Name, bonus, $"The blessing {blessing} at {star.Name}") is { } shown)
				{
					bonuses.Add(shown);
				}
			}
		}

		return bonuses;
	}

	/// <summary>The stat bonuses <c>BlessingStatsByRarity</c> gives a Champion of <paramref name="rarity"/> awakened to <paramref name="star"/>.</summary>
	private IReadOnlyDictionary<Metadata.GameEnumMember, ManagedDictionary.Entry> AwakeningStats(int championId, string rarity, string star)
	{
		var forRarity = _statsByRarity.TryGetValue(rarity, out var entry)
			? entry.List().Objects(BlessingStats)
			: throw new GameDataException($"Champion {championId} is a {rarity} Champion, which {StaticDoubleAscendData}.BlessingStatsByRarity holds no stats for.");
		var forStar = forRarity.Where(stats => stats.Enum("Grade", DoubleAscendGrade).Value == star).ToList();
		return forStar.Count == 1
			? forStar[0].Dictionary("StatBonusByKindId", $"System.Collections.Generic.Dictionary`2<{StatKind}, {StatBonusClass}>").ByEnumKey(StatKind)
			: throw new GameDataException($"{StaticDoubleAscendData}.BlessingStatsByRarity holds {forStar.Count} entries for a {rarity} Champion at {star}, where raid-bridge expects one.");
	}
}
