namespace RaidBridge.Game.Testing.SavedTeams;

/// <summary>
/// A Saved team, as SharedModel.Meta.Heroes.HeroesAiPreset holds it: its Id, which says what it is for, its Position in
/// the player's order, whether the player named it, and each SkillPrioritiesSetup's HeroId. A null HeroId is a null
/// entry, an empty position, as the game can hold one; null HeroIds hold no list
/// (https://github.com/ANicholasson/raid-bridge/issues/57).
/// </summary>
public sealed record SyntheticSavedTeam(int Id, int Position, bool NameIsNotDefault, IReadOnlyList<int?>? HeroIds);
