namespace RaidBridge.Domain.Stats;

/// <summary>
/// What a Champion's masteries give it, by the game's static data: the stat bonuses of its stat masteries, each added
/// to its stat as it is, and the share of its set bonuses that a mastery such as Lore of Steel adds, such as 0.15.
/// </summary>
public sealed record MasteryBonuses(IReadOnlyList<StatBonus> Stats, IReadOnlyList<double> SetBonusShares)
{
	public static MasteryBonuses None { get; } = new([], []);
}
