using RaidBridge.Domain.Artifacts;
using RaidBridge.Domain.Stats;
using RaidBridge.Game.Il2Cpp;
using RaidBridge.Game.Numerics;
using RaidBridge.Game.Stats;

namespace RaidBridge.Game.Artifacts;

/// <summary>
/// What the Artifact sets a Champion wears give it, from the static data's <c>ArtifactSetInfo</c>s, counted as the
/// game's own <c>ArtifactExtensions.GetArtifactSetBonuses</c> counts them at metadata v39
/// (https://github.com/ANicholasson/raid-bridge/issues/11):
/// <list type="bullet">
/// <item>A set that grows by the piece has an <c>ArtifactSubSetInfo</c> for each number of pieces. The one for the
/// number the Champion wears gives its <c>StatBonuses</c>, beside the set's own, which such sets leave empty.</item>
/// <item>Any other set gives its <c>StatBonus</c>, or its <c>StatBonuses</c> when it has no single one, once for
/// each <c>ArtifactCount</c> pieces worn, so a set one piece short gives nothing.</item>
/// </list>
/// A bonus to a stat outside the eight the champion screen shows, such as a set's IgnoreDefence, isn't part of the
/// Champion's stats, and its skill bonuses aren't stats either. Each Artifact's own bonuses are here too, so both
/// Stat sources of what a Champion wears come from one place.
/// </summary>
internal sealed class ArtifactSetBonuses(IReadOnlyDictionary<string, ManagedObject> sets)
{
	private const string StatBonusClass = "SharedModel.Meta.General.StatBonus";
	private const string SubSetInfo = "SharedModel.Meta.Artifacts.Sets.ArtifactSubSetInfo";
	private const string StatKind = "SharedModel.Battle.Effects.StatKindId";

	/// <summary>
	/// An Artifact's bonuses as <c>ArtifactSetup.FromArtifact</c> takes them: its main stat, its ascension bonus and its
	/// Substats, each with its Glyph added on.
	/// </summary>
	public static IEnumerable<StatBonus> Of(Artifact artifact) =>
		new[] { artifact.Primary }
			.Concat(artifact.AscensionBonus is { } ascension ? [ascension] : [])
			.Concat(artifact.Substats)
			.Select(bonus => (Stat: StatNames.Of(bonus.Stat, $"A bonus of Artifact {artifact.Id}"), Bonus: bonus))
			.Where(bonus => bonus.Stat is not null)
			.Select(bonus => new StatBonus(bonus.Stat!.Value, bonus.Bonus.Value + bonus.Bonus.Glyph, bonus.Bonus.Flat));

	/// <summary>The bonuses the sets of the Artifacts <paramref name="worn"/> give Champion <paramref name="championId"/>.</summary>
	public IReadOnlyList<StatBonus> For(int championId, IReadOnlyList<Artifact> worn) =>
		[.. worn
			.Where(artifact => artifact.SetId is not null)
			.GroupBy(artifact => artifact.SetId!, StringComparer.Ordinal)
			.SelectMany(set => Bonuses(championId, set.Key, set.Count()))];

	private IEnumerable<StatBonus> Bonuses(int championId, string setId, int pieces)
	{
		// The Artifact reader has refused a set the static data doesn't describe.
		var set = sets[setId];
		var subSets = set.ListOrNull("SubSetInfos", $"System.Collections.Generic.List`1<{SubSetInfo}>")?.Objects(SubSetInfo) ?? [];
		if (subSets.Count > 0)
		{
			var forPieces = subSets.Where(subSet => subSet.Int32("ArtifactCount").Value == pieces).ToList();
			// With no entry for the pieces worn, the game itself would divide by the set's ArtifactCount of 0.
			return forPieces.Count == 1
				? OwnBonuses(set, setId).Concat(ListedBonuses(forPieces[0], setId))
				: throw new GameDataException(
					$"Champion {championId} wears {pieces} pieces of the set {setId}, whose {forPieces.Count} SharedModel.Meta.Artifacts.Sets.ArtifactSetInfo.SubSetInfos for that many pieces aren't one.");
		}

		var perSet = set.Int32("ArtifactCount").Where(count => count > 0, $"the positive number of pieces a set of {setId} takes, since it has no SubSetInfos");
		return Enumerable.Repeat(OwnBonuses(set, setId), pieces / perSet).SelectMany(bonuses => bonuses);
	}

	/// <summary>A set's own bonuses: its single StatBonus, or else its list of them.</summary>
	private static IReadOnlyList<StatBonus> OwnBonuses(ManagedObject set, string setId) =>
		set.ObjectOrNull("StatBonus", StatBonusClass) is { } single ? [.. Translated([single], setId)] : ListedBonuses(set, setId);

	/// <summary>The bonuses in a set's, or one of its entries', StatBonuses.</summary>
	private static IReadOnlyList<StatBonus> ListedBonuses(ManagedObject owner, string setId) =>
		[.. Translated(owner.ListOrNull("StatBonuses", $"System.Collections.Generic.List`1<{StatBonusClass}>")?.Objects(StatBonusClass) ?? [], setId)];

	private static IEnumerable<StatBonus> Translated(IEnumerable<ManagedObject> bonuses, string setId) =>
		bonuses
			.Select(bonus => (Stat: StatNames.Of(bonus.Enum("StatKindId", StatKind).Value, $"A bonus of the set {setId}"), Bonus: bonus))
			.Where(bonus => bonus.Stat is not null)
			.Select(bonus => new StatBonus(bonus.Stat!.Value, FixedPoint.Of(bonus.Bonus, "Value"), bonus.Bonus.Boolean("IsAbsolute").Value));
}
