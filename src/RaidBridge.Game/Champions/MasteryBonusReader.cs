using RaidBridge.Domain.Stats;
using RaidBridge.Game.Il2Cpp;
using RaidBridge.Game.Numerics;
using RaidBridge.Game.Stats;

namespace RaidBridge.Game.Champions;

/// <summary>
/// What a Champion's masteries give it, from the static data's <c>MasteryData</c>, as the game's own
/// <c>HeroExtensions.CalcMasteriesBonus</c> takes them at metadata v39
/// (https://github.com/ANicholasson/raid-bridge/issues/12):
/// <list type="bullet">
/// <item>A mastery with an entry in <c>MasteryBonusById</c> is a stat mastery. The game adds its value to its stat as
/// it is, whatever its <c>IsAbsolute</c> says.</item>
/// <item>An entry whose <c>StatKindId</c> is the constant <c>StaticMasteryData.BonusSourceArtifactSet</c> (-1 at v39) is
/// no stat: its value is the share of the Champion's set bonuses it adds, as Lore of Steel's 15% is.</item>
/// <item>Any other mastery in <c>MasteryById</c> raises no stat. A mastery id <c>MasteryById</c> doesn't hold is
/// refused.</item>
/// </list>
/// </summary>
internal sealed class MasteryBonusReader
{
	private const string StaticMasteryData = "SharedModel.Meta.Masteries.StaticMasteryData";
	private const string StatBonusClass = "SharedModel.Meta.General.StatBonus";
	private const string StatKind = "SharedModel.Battle.Effects.StatKindId";

	private const string MasteryType = "SharedModel.Meta.Masteries.MasteryType";

	private readonly IReadOnlyDictionary<int, ManagedDictionary.Entry> _masteries;
	private readonly IReadOnlyDictionary<int, ManagedDictionary.Entry> _bonuses;
	private readonly long _setBonusSource;

	private MasteryBonusReader(IReadOnlyDictionary<int, ManagedDictionary.Entry> masteries, IReadOnlyDictionary<int, ManagedDictionary.Entry> bonuses, long setBonusSource)
	{
		_masteries = masteries;
		_bonuses = bonuses;
		_setBonusSource = setBonusSource;
	}

	public static MasteryBonusReader Read(ManagedObject staticData)
	{
		var data = staticData.Object("MasteryData", StaticMasteryData);
		return new(
			data.Dictionary("MasteryById", $"System.Collections.Generic.Dictionary`2<System.Int32, {MasteryType}>").ByInt32Key(),
			data.Dictionary("MasteryBonusById", $"System.Collections.Generic.Dictionary`2<System.Int32, {StatBonusClass}>").ByInt32Key(),
			data.Runtime.Metadata.Constant(StaticMasteryData, "BonusSourceArtifactSet"));
	}

	/// <summary>What the masteries <paramref name="masteryIds"/> give Champion <paramref name="championId"/>.</summary>
	public MasteryBonuses For(int championId, IReadOnlyList<int> masteryIds)
	{
		var stats = new List<StatBonus>();
		var setBonusShares = new List<double>();
		foreach (var id in masteryIds)
		{
			if (!_masteries.TryGetValue(id, out var mastery))
			{
				throw new GameDataException($"Champion {championId} has mastery {id}, which isn't a mastery in the game's {StaticMasteryData}.MasteryById.");
			}

			// A corrupt entry or a torn read could key one mastery under another's id (CodeRabbit, PR #21).
			_ = mastery.Object(MasteryType).Int32("Id").Where(value => value == id, $"the id MasteryById keeps it under, {id}");

			if (!_bonuses.TryGetValue(id, out var entry))
			{
				continue;
			}

			var bonus = entry.Object(StatBonusClass);
			var kind = bonus.EnumMemberOrUnnamed("StatKindId", StatKind, _setBonusSource);
			var value = FixedPoint.Of(bonus, "Value");
			if (kind.Value is null)
			{
				setBonusShares.Add(value);
			}
			else if (StatNames.Of(kind.Value.Name, $"Mastery {id}") is { } stat)
			{
				// The game adds a stat mastery's value as it is, so it is flat whatever IsAbsolute says.
				stats.Add(new StatBonus(stat, value, Flat: true));
			}
		}

		return new(stats, setBonusShares);
	}
}
