namespace RaidBridge.Domain.Stats;

/// <summary>
/// Every bonus a Champion's Stat sources give it, by the game's static data, before they add to its Base stats: the
/// bonuses of the Artifacts it wears, each with its Glyph, what their sets give, what its masteries give, what the
/// Account's Great Hall and Faction Guardians give a Champion of its Affinity, Faction and rarity, and what its
/// Awakening, through its blessing, its Empowerment and the Relics it wears give it, and what the Account's Classic Arena
/// league gives every Champion.
/// </summary>
public sealed record ChampionBonuses(
	IReadOnlyList<StatBonus> Artifacts,
	IReadOnlyList<StatBonus> Sets,
	MasteryBonuses Masteries,
	IReadOnlyList<StatBonus> GreatHall,
	IReadOnlyList<StatBonus> FactionGuardians,
	IReadOnlyList<StatBonus> Awakening,
	IReadOnlyList<StatBonus> Empowerment,
	IReadOnlyList<StatBonus> Relics,
	IReadOnlyList<StatBonus> ArenaLeague)
{
	public static ChampionBonuses None { get; } = new([], [], MasteryBonuses.None, [], [], [], [], [], []);
}
