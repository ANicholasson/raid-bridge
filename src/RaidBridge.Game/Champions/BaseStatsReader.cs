using RaidBridge.Domain.Stats;
using RaidBridge.Game.Il2Cpp;
using RaidBridge.Game.Metadata;
using RaidBridge.Game.Numerics;

namespace RaidBridge.Game.Champions;

/// <summary>
/// A Champion's Base stats, the white numbers on its champion screen, worked out as the game's own
/// <c>BattleStats.BuildFromBase(grade, level)</c> works them out at metadata v39, from the Champion type's default
/// form's <c>BaseStats</c> and the static <c>BattleStats.Multipliers</c> and <c>BattleStats.ToPercent</c>, in the
/// game's own fixed-point arithmetic (https://github.com/ANicholasson/raid-bridge/issues/10, ADR-0005):
/// <list type="bullet">
/// <item>Health, Attack and Defence scale with rank and level. Row <c>rank - 1</c> of <c>Multipliers</c> holds a start
/// and a growth: a stat is its base times the start, times the growth raised to how far the Champion is through its
/// rank's levels, rounded to a whole number, a half going to the even one. Health is that times 15.</item>
/// <item>Speed, Resistance and Accuracy are the base as it is.</item>
/// <item>CriticalChance and CriticalDamage are held as whole percents, so they are divided by <c>ToPercent</c> into
/// fractions, as an Artifact's are.</item>
/// </list>
/// An ascended Champion is of an ascended type, whose forms hold that ascension's base, so ascension needs nothing
/// here (https://github.com/ANicholasson/raid-bridge/issues/9).
/// </summary>
/// <remarks>
/// The 15 and the 10 levels a star gives a rank aren't in the game's static data: they are the game's own code's, in
/// <c>BuildFromBase</c> and <c>BattleStats.CalcValue</c>, so they are this formula's shape (asked of the maintainer,
/// 2026-11-10). <c>BuildFromBase</c> divides by a literal 100 too, which <c>ToPercent</c> also holds; raid-bridge
/// takes <c>ToPercent</c>, which the Game client keeps in memory.
/// </remarks>
internal sealed class BaseStatsReader
{
	private const string BattleStats = "SharedModel.Meta.Heroes.BattleStats";
	private const string HeroForm = "SharedModel.Meta.Heroes.HeroForm";
	private const string Fixed = "Plarium.Common.Numerics.Fixed";

	/// <summary>What <c>BuildFromBase</c> multiplies a scaled Health by.</summary>
	private const int HealthFactor = 15;

	/// <summary>How many levels <c>CalcValue</c> spreads a rank's growth over, for each star of the rank.</summary>
	private const int LevelsPerStar = 10;

	private readonly ManagedMatrix _multipliers;
	private readonly long _toPercent;

	private BaseStatsReader(ManagedMatrix multipliers, long toPercent)
	{
		_multipliers = multipliers;
		_toPercent = toPercent;
	}

	/// <summary>The static scaling the game builds Base stats with.</summary>
	public static BaseStatsReader Read(Il2CppRuntime game)
	{
		var where = $"{BattleStats}.Multipliers";
		var multipliers = new ManagedMatrix(
			game.StaticObjectOrNull(BattleStats, "Multipliers", "System.Double[][]", "System.Array")
				?? throw new GameDataException($"{where} is null, where raid-bridge expects the rank and level multipliers."),
			where);
		if (multipliers.Columns != 2)
		{
			throw new GameDataException($"{where} holds {multipliers.Columns} values for each rank, where raid-bridge expects a start and a growth.");
		}

		var toPercent = FixedPoint.RawOfValue(game.StaticValue(BattleStats, "ToPercent", Fixed), $"{BattleStats}.ToPercent");
		return new(multipliers, toPercent.Where(value => value > 0, "a positive number (a raw Fixed, in steps of 2^-32)"));
	}

	/// <summary>The Base stats of a Champion of <paramref name="heroType"/> at <paramref name="rank"/> and <paramref name="level"/>.</summary>
	public IReadOnlyDictionary<Stat, double> Of(int championId, ManagedObject heroType, int typeId, FieldValue<GameEnumMember> rank, FieldValue<int> level)
	{
		var stars = (int)rank.Value.Value;
		if (stars < 1 || stars > _multipliers.Rows)
		{
			throw new GameDataException($"Champion {championId} is at {rank.Value.Name}, but {_multipliers.Where} holds the multipliers of {_multipliers.Rows} ranks.");
		}

		var levels = LevelsPerStar * stars;
		_ = level.Where(value => value >= 1 && value <= levels, $"a level from 1 to {levels}, the levels of {rank.Value.Name}");

		var start = Multiplier(stars - 1, 0);
		var growth = Multiplier(stars - 1, 1);
		var scale = FixedPoint.FromDouble(Math.Pow(growth, (level.Value - 1.0) / (levels - 1)), $"{_multipliers.Where}[{stars - 1}, 1] to the power of the level");
		long Scaled(long value) => FixedPoint.RoundToEven(FixedPoint.Multiply(FixedPoint.Multiply(value, FixedPoint.FromDouble(start, $"{_multipliers.Where}[{stars - 1}, 0]")), scale));

		// HeroType.DefaultBaseStats is the first form's, which the champion screen shows.
		var forms = heroType.Array("Forms", $"{HeroForm}[]");
		if (forms.Length == 0)
		{
			throw new GameDataException($"{forms.Where} of Champion type {typeId} at 0x{forms.Address:X} holds no forms, so no Base stats.");
		}

		var stats = forms.Object(0, HeroForm).Object("BaseStats", BattleStats);
		long Base(string stat) => FixedPoint.RawOf(stats, stat).Where(value => value >= 0, "a stat of 0 or more (a raw Fixed, in steps of 2^-32)");

		return new Dictionary<Stat, double>
		{
			[Stat.Health] = FixedPoint.ToDouble(FixedPoint.Multiply(Scaled(Base("Health")), FixedPoint.FromInteger(HealthFactor))),
			[Stat.Attack] = FixedPoint.ToDouble(Scaled(Base("Attack"))),
			[Stat.Defence] = FixedPoint.ToDouble(Scaled(Base("Defence"))),
			[Stat.Speed] = FixedPoint.ToDouble(Base("Speed")),
			[Stat.Resistance] = FixedPoint.ToDouble(Base("Resistance")),
			[Stat.Accuracy] = FixedPoint.ToDouble(Base("Accuracy")),
			[Stat.CriticalChance] = FixedPoint.ToDouble(FixedPoint.Divide(Base("CriticalChance"), _toPercent)),
			[Stat.CriticalDamage] = FixedPoint.ToDouble(FixedPoint.Divide(Base("CriticalDamage"), _toPercent)),
		};
	}

	/// <summary>A multiplier, which must be a positive number.</summary>
	private double Multiplier(int row, int column)
	{
		var value = _multipliers.Double(row, column);
		return double.IsFinite(value) && value > 0
			? value
			: throw new GameDataException($"{_multipliers.Where}[{row}, {column}] is {value}, where raid-bridge expects a positive number.");
	}
}
