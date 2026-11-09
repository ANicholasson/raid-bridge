namespace RaidBridge.Domain.Artifacts;

/// <summary>
/// An Artifact on the Account, worn or not, accessories included, as the Game client holds it. The slot, set, rank,
/// rarity and Faction lock are the names the game's own enums give them, such as <c>Weapon</c> or <c>Six</c>; an
/// Unworn Artifact's slot is the one it fits. The ascension level and bonus are null when the Artifact was never
/// ascended, the set when the Artifact is in none, and the Faction lock when the Artifact has none.
/// </summary>
public sealed record Artifact(
	int Id,
	string Slot,
	string? SetId,
	string? SetName,
	string Rank,
	string Rarity,
	int Level,
	int? AscensionLevel,
	string? FactionLock,
	ArtifactBonus Primary,
	ArtifactBonus? AscensionBonus,
	IReadOnlyList<ArtifactBonus> Substats);
