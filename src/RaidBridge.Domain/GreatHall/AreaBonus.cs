namespace RaidBridge.Domain.GreatHall;

/// <summary>
/// The Area Bonuses of one area: the area, as the game's own enum names it, such as <c>IceGolemCave</c>, and the level
/// of each stat bought for it, in the order the game's enum gives the stats. Only levels: the bonus a level gives is
/// the game's static data, not the Account's.
/// </summary>
public sealed record AreaBonus(string Area, IReadOnlyList<GreatHallLevel> Levels);
