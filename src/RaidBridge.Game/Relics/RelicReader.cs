using RaidBridge.Domain.Champions;
using RaidBridge.Domain.Relics;
using RaidBridge.Game.Il2Cpp;
using RaidBridge.Game.Localization;
using RaidBridge.Game.Metadata;
using RaidBridge.Game.Numerics;

namespace RaidBridge.Game.Relics;

/// <summary>
/// Reads the Account's Relics and Relic stones from the game's model
/// (https://github.com/ANicholasson/raid-bridge/issues/8, docs/research/2026-11-relics.md):
/// <c>_userWrapper.Relics.RelicsData</c>'s <c>Relics</c> and <c>Stones</c>, every one the Account owns, and its
/// <c>RelicDataByHeroId[championId].RelicIds</c>, the Relics each Champion wears. Every Relic no Champion wears is an
/// Unworn Relic, and every stone no socket names is in no socket. A Relic holds no stat values of its own: each
/// stat its type raises whose opening level it has reached gives the static table's value at the Relic's level. The
/// cached <c>_type</c> fields are never read: types come from the static data, by id.
/// </summary>
internal static class RelicReader
{
	private const string Relic = "SharedModel.Meta.Relics.UserData.Relic";
	private const string Stone = "SharedModel.Meta.Relics.UserData.RelicStone";
	private const string StoneSocket = "SharedModel.Meta.Relics.UserData.RelicStoneSocket";
	private const string HeroRelicData = "SharedModel.Meta.Relics.UserData.HeroRelicData";
	private const string StaticRelicData = "SharedModel.Meta.Relics.StaticData.StaticRelicData";
	private const string RelicType = "SharedModel.Meta.Relics.StaticData.RelicType";
	private const string StoneType = "SharedModel.Meta.Relics.StaticData.RelicStoneType";
	private const string FactionSkills = "SharedModel.Meta.Relics.StaticData.RelicFactionSkillBonus";
	private const string Rank = "SharedModel.Meta.Relics.StaticData.RelicRank";
	private const string Shape = "SharedModel.Meta.Relics.StaticData.RelicStoneShapeKindId";
	private const string StatKind = "SharedModel.Battle.Effects.StatKindId";
	private const string BonusValue = "SharedModel.Meta.Artifacts.BonusValue";
	private const string Rarity = "SharedModel.Meta.ItemRarity";
	private const string Ints = "System.Collections.Generic.List`1<System.Int32>";

	/// <summary>
	/// <paramref name="champions"/> with the Relics each wears, the Relics no Champion wears, and the Relic stones in no
	/// socket, each sorted by id. A worn Relic's Faction skills are its wearer's Faction's, so this reader takes the
	/// Champions already read and gives each its Relics, where the Artifact reader hands its own to the Champion reader.
	/// </summary>
	public static AccountRelics Read(ManagedObject userWrapper, ManagedObject staticData, LocalizedNames names, IReadOnlyList<Champion> champions)
	{
		var relicData = userWrapper
			.Object("Relics", "Client.Model.Gameplay.Relics.RelicsWrapper")
			.Object("RelicsData", "Client.Model.Gameplay.Relics.UpdatableUserRelicData");
		var tables = new Lazy<Tables>(() => new(staticData.Object("RelicData", StaticRelicData)));

		var stones = ById(relicData.List("Stones", $"System.Collections.Generic.List`1<{Stone}>").Objects(Stone), "Relic stone", "Stones");
		var relics = ById(relicData.List("Relics", $"System.Collections.Generic.List`1<{Relic}>").Objects(Relic), "Relic", "Relics");
		var wornBy = WornBy(relicData, relics, champions);

		var socketed = new Dictionary<int, int>();
		Relic Build(int id, string? wearerFaction) => RelicFrom(relics[id], wearerFaction, stones, socketed, tables, names);

		var factions = champions.ToDictionary(champion => champion.Id, champion => champion.Faction);
		var worn = wornBy.ToDictionary(
			champion => champion.Key,
			champion => (IReadOnlyList<Relic>)[.. champion.Value.Select(id => Build(id, factions[champion.Key]))]);
		var wornIds = wornBy.SelectMany(champion => champion.Value).ToHashSet();
		IReadOnlyList<Relic> unworn = [.. relics.Keys.Where(id => !wornIds.Contains(id)).Order().Select(id => Build(id, wearerFaction: null))];

		return new(
			[.. champions.Select(champion => champion with { Relics = worn.GetValueOrDefault(champion.Id, []) })],
			unworn,
			[.. stones.Keys.Where(id => !socketed.ContainsKey(id)).Order().Select(id => StoneFrom(stones[id], tables, names))]);
	}

	/// <summary>The Account's Relics or Relic stones by id, each id positive and held once.</summary>
	private static Dictionary<int, ManagedObject> ById(IReadOnlyList<ManagedObject> owned, string what, string field)
	{
		var byId = new Dictionary<int, ManagedObject>();
		foreach (var item in owned)
		{
			var id = item.Int32("Id");
			if (!byId.TryAdd(id.Where(value => value > 0, $"a positive {what} id"), item))
			{
				throw new GameDataException($"{id}, an id SharedModel.Meta.Relics.UserData.UserRelicData.{field} holds twice.");
			}
		}

		return byId;
	}

	/// <summary>
	/// The ids of the Relics each Champion wears, in the game's order. Each must be a Relic the Account owns, worn by one
	/// Champion only, and each Champion one the Account holds, or a torn read could hide a Relic or show it twice.
	/// </summary>
	private static Dictionary<int, IReadOnlyList<int>> WornBy(ManagedObject relicData, Dictionary<int, ManagedObject> relics, IReadOnlyList<Champion> champions)
	{
		const string Where = "SharedModel.Meta.Relics.UserData.UserRelicData.RelicDataByHeroId";
		var championIds = champions.Select(champion => champion.Id).ToHashSet();
		var wearers = new Dictionary<int, int>();
		var wornBy = new Dictionary<int, IReadOnlyList<int>>();
		foreach (var (championId, entry) in relicData
			.Dictionary("RelicDataByHeroId", $"System.Collections.Generic.Dictionary`2<System.Int32, {HeroRelicData}>")
			.ByInt32Key()
			.OrderBy(champion => champion.Key))
		{
			var ids = new List<int>();
			foreach (var id in entry.Object(HeroRelicData).List("RelicIds", Ints).Int32s())
			{
				if (!relics.ContainsKey(id.Value))
				{
					throw new GameDataException($"{Where} gives Champion {championId} Relic {id.Value}, but UserRelicData.Relics has no Relic {id.Value}: {id}.");
				}

				if (!wearers.TryAdd(id.Value, championId))
				{
					throw new GameDataException($"{Where} gives Relic {id.Value} to both Champion {wearers[id.Value]} and Champion {championId}.");
				}

				ids.Add(id.Value);
			}

			if (ids.Count > 0 && !championIds.Contains(championId))
			{
				throw new GameDataException($"{Where} gives Relics to Champion {championId}, which HeroById doesn't hold.");
			}

			wornBy[championId] = ids;
		}

		return wornBy;
	}

	private static Relic RelicFrom(
		ManagedObject relic,
		string? wearerFaction,
		Dictionary<int, ManagedObject> stones,
		Dictionary<int, int> socketed,
		Lazy<Tables> tables,
		LocalizedNames names)
	{
		var id = relic.Int32("Id").Value;
		var typeId = relic.Int32("TypeId");
		var type = tables.Value.RelicTypeOf(typeId);
		var level = relic.Int32("Level");
		_ = level.Where(value => value >= 0, "a level of 0 or more");

		return new(
			Id: id,
			TypeId: typeId.Value,
			Name: names.Of(type, "Name", $"Relic type {typeId.Value}"),
			Rarity: type.Enum("Rarity", Rarity).Value,
			Rank: relic.Enum("Rank", Rank).Value,
			Level: level.Value,
			Bonuses: Bonuses(type, typeId.Value, level, tables.Value),
			Skills: Skills(type, typeId.Value, wearerFaction, names),
			Sockets: [.. relic.List("Sockets", $"System.Collections.Generic.List`1<{StoneSocket}>").Objects(StoneSocket).Select(socket => SocketFrom(socket, id, stones, socketed, tables, names))]);
	}

	/// <summary>
	/// The bonus of each stat the type raises whose opening level the Relic has reached. The type's first stat opens at
	/// the static data's first opening level, its second at the second, and so on; the value is the stat's static list
	/// at the Relic's level, which is its index (live check, docs/research/2026-11-relics.md).
	/// </summary>
	private static List<RelicBonus> Bonuses(ManagedObject type, int typeId, FieldValue<int> level, Tables tables)
	{
		var stats = type.List("StatsBonus", $"System.Collections.Generic.List`1<{StatKind}>").EnumMembers(StatKind);
		if (stats.Count > tables.OpeningLevels.Count)
		{
			throw new GameDataException(
				$"Relic type {typeId} raises {stats.Count} stats, but {StaticRelicData}.StatBonusOpeningLevels gives an opening level for only {tables.OpeningLevels.Count}.");
		}

		var bonuses = new List<RelicBonus>();
		for (var index = 0; index < stats.Count; index++)
		{
			if (level.Value < tables.OpeningLevels[index])
			{
				continue;
			}

			var stat = stats[index].Value;
			var values = tables.ValuesOf(stat, stats[index], typeId);
			if (level.Value >= values.Count)
			{
				throw new GameDataException(
					$"{level}, but {StaticRelicData}.StatValueByLevel[{stat.Name}], which its type {typeId} raises, holds values for levels 0 to {values.Count - 1}.");
			}

			var value = values[level.Value];
			bonuses.Add(new(stat.Name, FixedPoint.Of(value, "_value"), value.Boolean("_isAbsolute").Value));
		}

		return bonuses;
	}

	/// <summary>
	/// The type's own skills, each described by the type's description, then the skills the wearer's Faction adds, each
	/// described by that Faction's entry. An unworn Relic has no wearer, so only its own. Most types hold no Faction
	/// list at all (live check, https://github.com/ANicholasson/raid-bridge/issues/8).
	/// </summary>
	private static List<RelicSkill> Skills(ManagedObject type, int typeId, string? wearerFaction, LocalizedNames names)
	{
		var skills = SkillsOf(type, $"Relic type {typeId}", names).ToList();
		if (wearerFaction is null)
		{
			return skills;
		}

		var factionSkills = type
			.ListOrNull("SkillIdsByFaction", $"System.Collections.Generic.List`1<{FactionSkills}>")?
			.Objects(FactionSkills)
			.Where(entry => entry.Enum("FractionId", "SharedModel.Meta.Heroes.HeroFraction").Value == wearerFaction)
			.ToList() ?? [];
		return factionSkills.Count switch
		{
			0 => skills,
			1 => [.. skills, .. SkillsOf(factionSkills[0], $"Relic type {typeId}'s {wearerFaction} skills", names)],
			_ => throw new GameDataException($"Relic type {typeId}'s SkillIdsByFaction lists {wearerFaction} {factionSkills.Count} times."),
		};
	}

	/// <summary>The skills a Relic type, a Faction's entry or a stone type lists, each with the description it gives.</summary>
	private static IEnumerable<RelicSkill> SkillsOf(ManagedObject owner, string named, LocalizedNames names)
	{
		var ids = owner.List("SkillIds", Ints).Int32s();
		if (ids.Count == 0)
		{
			return [];
		}

		var description = names.Of(owner, "Description", named);
		return [.. ids.Select(id => new RelicSkill(id.Where(value => value > 0, "a positive skill type id"), description))];
	}

	/// <summary>A socket, and the stone it names, which must be one the Account owns, in no other socket, of the socket's shape.</summary>
	private static RelicSocket SocketFrom(
		ManagedObject socket,
		int relicId,
		Dictionary<int, ManagedObject> stones,
		Dictionary<int, int> socketed,
		Lazy<Tables> tables,
		LocalizedNames names)
	{
		var shape = socket.Enum("ShapeKindId", Shape).Value;
		var stoneId = socket.NullableInt32("StoneId");
		if (stoneId.Value is not { } id)
		{
			return new(shape, null);
		}

		if (!stones.TryGetValue(id, out var stoneObject))
		{
			throw new GameDataException($"A socket of Relic {relicId} holds Relic stone {id}, but UserRelicData.Stones has no Relic stone {id}: {stoneId}.");
		}

		if (!socketed.TryAdd(id, relicId))
		{
			throw new GameDataException($"Relic stone {id} is in a socket of Relic {socketed[id]} and one of Relic {relicId}: {stoneId}.");
		}

		var stone = StoneFrom(stoneObject, tables, names);
		return stone.Shape == shape
			? new(shape, stone)
			: throw new GameDataException($"A {shape} socket of Relic {relicId} holds Relic stone {id}, which is a {stone.Shape}: {stoneId}.");
	}

	private static RelicStone StoneFrom(ManagedObject stone, Lazy<Tables> tables, LocalizedNames names)
	{
		var typeId = stone.Int32("TypeId");
		var type = tables.Value.StoneTypeOf(typeId);
		return new(
			Id: stone.Int32("Id").Value,
			TypeId: typeId.Value,
			Name: names.Of(type, "Name", $"Relic stone type {typeId.Value}"),
			Rarity: type.Enum("Rarity", Rarity).Value,
			Shape: type.Enum("Shape", Shape).Value,
			Skills: [.. SkillsOf(type, $"Relic stone type {typeId.Value}", names)]);
	}

	/// <summary>The static Relic data a Relic is worked out from, each table read once, when it is first needed.</summary>
	private sealed class Tables(ManagedObject relicData)
	{
		private readonly Lazy<IReadOnlyDictionary<int, ManagedDictionary.Entry>> _relicTypes = new(() =>
			relicData.Dictionary("_relicTypeById", $"System.Collections.Generic.Dictionary`2<System.Int32, {RelicType}>").ByInt32Key());

		private readonly Lazy<IReadOnlyDictionary<int, ManagedDictionary.Entry>> _stoneTypes = new(() =>
			relicData.Dictionary("_relicStoneTypeById", $"System.Collections.Generic.Dictionary`2<System.Int32, {StoneType}>").ByInt32Key());

		private readonly Lazy<IReadOnlyDictionary<GameEnumMember, ManagedDictionary.Entry>> _values = new(() =>
			relicData.Dictionary("StatValueByLevel", $"System.Collections.Generic.Dictionary`2<{StatKind}, System.Collections.Generic.List`1<{BonusValue}>>").ByEnumKey(StatKind));

		private readonly Dictionary<GameEnumMember, IReadOnlyList<ManagedObject>> _valuesByStat = [];

		private readonly Lazy<IReadOnlyList<int>> _openingLevels = new(() =>
			[.. relicData.List("StatBonusOpeningLevels", Ints).Int32s().Select(level => level.Where(value => value >= 0, "an opening level of 0 or more"))]);

		public IReadOnlyList<int> OpeningLevels => _openingLevels.Value;

		/// <summary>A Relic type by its id, which must be the id the static data keeps it under.</summary>
		public ManagedObject RelicTypeOf(FieldValue<int> typeId) => Type(_relicTypes.Value, typeId, RelicType, "_relicTypeById");

		/// <summary>A Relic stone type by its id, which must be the id the static data keeps it under.</summary>
		public ManagedObject StoneTypeOf(FieldValue<int> typeId) => Type(_stoneTypes.Value, typeId, StoneType, "_relicStoneTypeById");

		/// <summary>The value each level of a stat gives, from level 0, or an error naming the type's stat when the table has none.</summary>
		public IReadOnlyList<ManagedObject> ValuesOf(GameEnumMember stat, FieldValue<GameEnumMember> raisedBy, int typeId)
		{
			if (!_valuesByStat.TryGetValue(stat, out var values))
			{
				values = _values.Value.TryGetValue(stat, out var entry)
					? entry.List().Objects(BonusValue)
					: throw new GameDataException(
						$"Relic type {typeId}'s StatsBonus raises {stat.Name}, which {StaticRelicData}.StatValueByLevel holds no values for: {raisedBy.Field} at 0x{raisedBy.Address:X}.");
				_valuesByStat[stat] = values;
			}

			return values;
		}

		private static ManagedObject Type(IReadOnlyDictionary<int, ManagedDictionary.Entry> types, FieldValue<int> typeId, string className, string field)
		{
			var type = types.TryGetValue(typeId.Value, out var entry)
				? entry.Object(className)
				: throw new GameDataException($"{typeId}, which isn't a type in the game's {StaticRelicData}.{field}.");
			// A corrupt entry or a torn read could key one type's data under another's id (CodeRabbit, PR #30).
			_ = type.Int32("Id").Where(value => value == typeId.Value, $"the id {field} keeps it under, {typeId.Value}");
			return type;
		}
	}
}

/// <summary>The Account's Champions, each with the Relics it wears, the Unworn Relics, and the Relic stones in no socket, both by id.</summary>
internal sealed record AccountRelics(IReadOnlyList<Champion> Champions, IReadOnlyList<Relic> Unworn, IReadOnlyList<RelicStone> Unsocketed);
