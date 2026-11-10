using RaidBridge.Domain.Stats;
using RaidBridge.Game.Il2Cpp;

namespace RaidBridge.Game.Stats;

/// <summary>
/// The game's <c>StatKindId</c> names as the Domain's eight stats. The game names ten more, which the champion screen
/// doesn't show, so a bonus to one of them isn't part of a Champion's stats (ADR-0005). Any other name is refused, so a
/// stat a game update adds can't be dropped without a word (https://github.com/ANicholasson/raid-bridge/issues/11).
/// </summary>
internal static class StatNames
{
	private static readonly Dictionary<string, Stat> Shown = Enum.GetValues<Stat>().ToDictionary(stat => stat.ToString(), StringComparer.Ordinal);

	/// <summary>The rest of <c>StatKindId</c> at metadata v39: battle-only stats, and the share of damage dealt and taken.</summary>
	private static readonly HashSet<string> NotShown = new(StringComparer.Ordinal)
	{
		"CriticalHeal", "IgnoreDefence",
		"DealtDamagePve", "DealtDamagePvp", "DealtDamageBoss", "DealtDamageDungeons",
		"TakenDamagePve", "TakenDamagePvp", "TakenDamageBoss", "TakenDamageDungeons",
	};

	/// <summary>
	/// The stat a bonus raises, or null for one the champion screen doesn't show. <paramref name="what"/> names the bonus
	/// for the error a stat raid-bridge doesn't know raises.
	/// </summary>
	public static Stat? Of(string name, string what) =>
		Shown.TryGetValue(name, out var stat) ? stat
		: NotShown.Contains(name) ? null
		: throw new GameDataException($"{what} raises {name}, a stat raid-bridge doesn't know.");
}
