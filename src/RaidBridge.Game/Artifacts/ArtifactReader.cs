using RaidBridge.Domain.Artifacts;
using RaidBridge.Game.Il2Cpp;
using RaidBridge.Game.Localization;
using RaidBridge.Game.Numerics;

namespace RaidBridge.Game.Artifacts;

/// <summary>
/// Reads the Account's Artifacts from the game's model: the slots each Champion fills in
/// <c>_userWrapper.Artifacts.ArtifactData.ArtifactDataByHeroId[championId].ArtifactIdByKind</c>, where 0 is an empty
/// slot, and the Artifacts themselves from where the Account keeps them (spec #20, #26). Every Artifact kept there that
/// no Champion wears is an Unworn Artifact (#33). Only fields are read, never the properties that run game code, such
/// as <c>Artifact.SetInfo</c> and <c>Artifact.SetKindId</c>.
/// </summary>
internal static class ArtifactReader
{
	private const string Artifact = "SharedModel.Meta.Artifacts.Artifact";
	private const string Bonus = "SharedModel.Meta.Artifacts.Bonuses.ArtifactBonus";
	private const string HeroArtifactData = "SharedModel.Meta.Artifacts.HeroArtifactData";
	private const string Kind = "SharedModel.Meta.Artifacts.ArtifactKindId";
	private const string SetInfo = "SharedModel.Meta.Artifacts.Sets.ArtifactSetInfo";
	private const string SetKind = "SharedModel.Meta.Artifacts.Sets.ArtifactSetKindId";
	private const string Resolver = "SharedModel.Meta.Artifacts.ArtifactStorage.ArtifactStorageResolver";
	private const string ExternalStorage = "Client.Model.Gameplay.Artifacts.ExternalArtifactsStorage";

	public static AccountArtifacts Read(ManagedObject userWrapper, ManagedObject staticData, LocalizedNames names)
	{
		var artifactData = userWrapper
			.Object("Artifacts", "Client.Model.Gameplay.Artifacts.EquipmentWrapper")
			.Object("ArtifactData", "SharedModel.Meta.Artifacts.UserArtifactData");
		var stored = Stored(artifactData);
		var sets = Sets(staticData);

		var wornBy = artifactData
			.Dictionary("ArtifactDataByHeroId", $"System.Collections.Generic.Dictionary`2<System.Int32, {HeroArtifactData}>")
			.ByInt32Key()
			.ToDictionary(hero => hero.Key, hero => Equipped(hero.Key, hero.Value.Object(HeroArtifactData), stored, sets, names));

		// One Artifact worn by two Champions would be written under both, and the Account would seem to hold it twice.
		var wearers = new Dictionary<int, int>();
		foreach (var (championId, artifacts) in wornBy.OrderBy(champion => champion.Key))
		{
			foreach (var artifact in artifacts)
			{
				if (!wearers.TryAdd(artifact.Id, championId))
				{
					throw new GameDataException(
						$"SharedModel.Meta.Artifacts.UserArtifactData.ArtifactDataByHeroId gives Artifact {artifact.Id} to both Champion {wearers[artifact.Id]} and Champion {championId}.");
				}
			}
		}

		return new(
			wornBy,
			[.. stored.ById.Where(entry => !wearers.ContainsKey(entry.Key)).OrderBy(entry => entry.Key).Select(entry => Unworn(entry.Value, sets, names))]);
	}

	/// <summary>
	/// The Account's Artifacts by id, and where they are kept, for errors. The storage resolver's implementation says
	/// where: none, and they are in the Account's own list; the external storage after storage migration, and they
	/// are in its cache. Any other class is refused by name (spec #20).
	/// </summary>
	private static (IReadOnlyDictionary<int, ManagedObject> ById, string Where) Stored(ManagedObject artifactData)
	{
		var storage = artifactData.Runtime.StaticObjectOrNull(
			Resolver, "_implementation", "SharedModel.Meta.Artifacts.ArtifactStorage.IArtifactStorageAdapter", "System.Object");
		switch (storage?.Class.FullName)
		{
			case null:
				var byId = new Dictionary<int, ManagedObject>();
				foreach (var artifact in artifactData.List("Artifacts", $"System.Collections.Generic.List`1<{Artifact}>").Objects(Artifact))
				{
					var id = artifact.Int32("_id");
					if (!byId.TryAdd(id.Where(value => value > 0, "a positive Artifact id"), artifact))
					{
						throw new GameDataException($"{id}, an id SharedModel.Meta.Artifacts.UserArtifactData.Artifacts holds twice.");
					}
				}

				return (byId, "SharedModel.Meta.Artifacts.UserArtifactData.Artifacts");

			case ExternalStorage:
				var cached = storage!.As(ExternalStorage)
					.Object("_cachedArtifacts", "Client.Model.Gameplay.Artifacts.CachedArtifacts")
					.Dictionary("_artifacts", $"System.Collections.Generic.Dictionary`2<System.Int32, {Artifact}>")
					.ByInt32Key()
					.ToDictionary(
						entry => entry.Key,
						entry =>
						{
							var artifact = entry.Value.Object(Artifact);
							_ = artifact.Int32("_id").Where(value => value == entry.Key && value > 0, $"the positive id _artifacts keeps it under, {entry.Key}");
							return artifact;
						});
				return (cached, "Client.Model.Gameplay.Artifacts.CachedArtifacts._artifacts");

			default:
				throw new GameDataException(
					$"{Resolver}._implementation points to a {storage!.Class.FullName} at 0x{storage.Address:X}, which raid-bridge doesn't know as a place the game keeps Artifacts.");
		}
	}

	/// <summary>Each Artifact set the static data describes, by its set kind's name.</summary>
	private static Dictionary<string, ManagedObject> Sets(ManagedObject staticData)
	{
		var sets = new Dictionary<string, ManagedObject>(StringComparer.Ordinal);
		foreach (var set in staticData
			.Object("ArtifactData", "SharedModel.Meta.Artifacts.StaticArtifactData")
			.List("SetInfos", $"System.Collections.Generic.List`1<{SetInfo}>")
			.Objects(SetInfo))
		{
			var kind = set.Enum("ArtifactSetKindId", SetKind);
			if (!sets.TryAdd(kind.Value, set))
			{
				throw new GameDataException($"{kind}, a set the game's StaticArtifactData.SetInfos describes twice.");
			}
		}

		return sets;
	}

	private static IReadOnlyList<Artifact> Equipped(
		int championId,
		ManagedObject heroArtifactData,
		(IReadOnlyDictionary<int, ManagedObject> ById, string Where) stored,
		Dictionary<string, ManagedObject> sets,
		LocalizedNames names)
	{
		var artifacts = new List<Artifact>();
		foreach (var (slot, entry) in heroArtifactData
			.Dictionary("ArtifactIdByKind", $"System.Collections.Generic.Dictionary`2<{Kind}, System.Int32>")
			.ByEnumKey(Kind)
			.OrderBy(slot => slot.Key.Value))
		{
			var id = entry.Int32();
			if (id.Where(value => value >= 0, "an Artifact id, or 0 for an empty slot") == 0)
			{
				continue;
			}

			artifacts.Add(stored.ById.TryGetValue(id.Value, out var artifact)
				? Equipped(championId, slot.Name, artifact, sets, names)
				: throw new GameDataException($"Champion {championId} wears Artifact {id.Value} in its {slot.Name} slot, but {stored.Where} has no Artifact {id.Value}."));
		}

		return [.. artifacts];
	}

	private static Artifact Equipped(int championId, string slot, ManagedObject artifact, Dictionary<string, ManagedObject> sets, LocalizedNames names) =>
		ArtifactIn(artifact.Enum("_kindId", Kind).Where(kind => kind == slot, $"the {slot} slot Champion {championId} wears it in"), artifact, sets, names);

	/// <summary>An Artifact no Champion wears, in the slot it fits.</summary>
	private static Artifact Unworn(ManagedObject artifact, Dictionary<string, ManagedObject> sets, LocalizedNames names) =>
		ArtifactIn(artifact.Enum("_kindId", Kind).Value, artifact, sets, names);

	/// <summary>An Artifact in the slot it fills or fits, read by the same code whether a Champion wears it or not.</summary>
	private static Artifact ArtifactIn(string slot, ManagedObject artifact, Dictionary<string, ManagedObject> sets, LocalizedNames names)
	{
		var id = artifact.Int32("_id").Value;
		// ArtifactSetKindId's 0, None, is no set: the secondary Account holds an unworn Ring in none (live check, #33).
		var setKind = artifact.EnumMember("_setKindId", SetKind);
		string? setName = null;
		if (setKind.Value.Value != 0)
		{
			var set = sets.TryGetValue(setKind.Value.Name, out var setInfo)
				? setInfo
				: throw new GameDataException(
					$"{setKind.Field} at 0x{setKind.Address:X} is {setKind.Value.Name}, which isn't a set in the game's StaticArtifactData.SetInfos.");
			setName = names.Of(set, "Name", $"Artifact set {setKind.Value.Name}");
		}

		// HeroFraction's 0, Unknown, is no Faction: only an accessory is locked to one.
		var faction = artifact.EnumMember("_requiredFraction", "SharedModel.Meta.Heroes.HeroFraction").Value;
		var ascensionBonus = artifact.ObjectOrNull("_ascendBonus", Bonus);

		return new(
			Id: id,
			Slot: slot,
			SetId: setKind.Value.Value == 0 ? null : setKind.Value.Name,
			SetName: setName,
			Rank: artifact.Enum("_rankId", "SharedModel.Meta.Artifacts.ArtifactRankId").Value,
			Rarity: artifact.Enum("_rarityId", "SharedModel.Meta.ItemRarity").Value,
			Level: artifact.Int32("_level").Where(level => level >= 0, "a level of 0 or more"),
			AscensionLevel: artifact.NullableInt32("_ascendLevel").Where(level => level is null or >= 0, "an ascension level of 0 or more, or none"),
			FactionLock: faction.Value == 0 ? null : faction.Name,
			Primary: ArtifactBonus(artifact.Object("_primaryBonus", Bonus)),
			AscensionBonus: ascensionBonus is null ? null : ArtifactBonus(ascensionBonus),
			// An Artifact that never rolled a Substat may have no list at all.
			Substats: [.. artifact.ListOrNull("_secondaryBonuses", $"System.Collections.Generic.List`1<{Bonus}>")?.Objects(Bonus).Select(ArtifactBonus) ?? []]);
	}

	/// <summary>A bonus: its stat, its value and whether that is flat or a percentage, its Glyph, and how many times it was raised.</summary>
	private static ArtifactBonus ArtifactBonus(ManagedObject bonus)
	{
		var value = bonus.Object("_value", "SharedModel.Meta.Artifacts.BonusValue");
		return new(
			Stat: bonus.Enum("_kindId", "SharedModel.Battle.Effects.StatKindId").Value,
			Value: FixedPoint.Of(value, "_value"),
			Flat: value.Boolean("_isAbsolute").Value,
			Glyph: FixedPoint.Of(bonus, "_powerUpValue"),
			Rolls: bonus.Int32("_level").Where(level => level >= 0, "a roll count of 0 or more"));
	}
}

/// <summary>The Account's Artifacts: those each Champion wears, by the Champion's id, in the game's order of slots, and those no Champion wears, by id.</summary>
internal sealed record AccountArtifacts(IReadOnlyDictionary<int, IReadOnlyList<Artifact>> WornBy, IReadOnlyList<Artifact> Unworn);
