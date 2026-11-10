namespace RaidBridge.Domain.Stats;

/// <summary>
/// The eight stats the champion screen shows, under the names the game's own <c>StatKindId</c> gives them and in the
/// order of its numbers (https://github.com/ANicholasson/raid-bridge/issues/5).
/// </summary>
public enum Stat
{
	Health,
	Attack,
	Defence,
	Speed,
	Resistance,
	Accuracy,
	CriticalChance,
	CriticalDamage,
}
