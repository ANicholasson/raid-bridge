namespace RaidBridge.Domain.SavedTeams;

/// <summary>
/// A Saved team: what it is for, as the game's own enum names it, such as <c>GeneralPve</c>; whether the player named
/// it, though never the name itself, which is text the player typed; and the ids of the Champions in it, in the order of
/// its positions, each a Champion on the Account and in the team once
/// (https://github.com/ANicholasson/raid-bridge/issues/57).
/// </summary>
public sealed record SavedTeam(string Purpose, bool Named, IReadOnlyList<int> ChampionIds);
