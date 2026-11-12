using System.Globalization;
using System.Text.Json;
using RaidBridge.Domain.Arena;
using RaidBridge.Domain.Artifacts;
using RaidBridge.Domain.Champions;
using RaidBridge.Domain.Relics;
using RaidBridge.Domain.Snapshots;
using RaidBridge.Domain.Stats;

namespace RaidBridge.Export.Snapshots;

/// <summary>
/// A Snapshot as the JSON document docs/snapshot.md describes: camelCase keys in a fixed order, two-space indents and
/// "\n" line ends, so two Snapshots diff cleanly.
/// </summary>
internal static class SnapshotJson
{
	/// <summary>Bumped only by a breaking change to the format; an added field doesn't bump it.</summary>
	public const int SchemaVersion = 1;

	public static byte[] Write(Snapshot snapshot, Generator generator)
	{
		using var buffer = new MemoryStream();
		using (var json = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true, IndentSize = 2, NewLine = "\n" }))
		{
			json.WriteStartObject();
			json.WriteNumber("schemaVersion", SchemaVersion);

			json.WriteStartObject("generator");
			json.WriteString("name", generator.Name);
			json.WriteString("version", generator.Version);
			json.WriteEndObject();

			json.WriteString("capturedAt", snapshot.CapturedAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));

			json.WriteStartObject("account");
			json.WriteString("idHash", snapshot.AccountIdHash.Value);
			json.WriteEndObject();

			json.WriteStartArray("champions");
			foreach (var champion in snapshot.Champions)
			{
				WriteChampion(json, champion);
			}

			json.WriteEndArray();

			json.WriteStartArray("unwornArtifacts");
			foreach (var artifact in snapshot.UnwornArtifacts)
			{
				WriteArtifact(json, artifact);
			}

			json.WriteEndArray();

			json.WriteStartArray("unwornRelics");
			foreach (var relic in snapshot.UnwornRelics)
			{
				WriteRelic(json, relic);
			}

			json.WriteEndArray();

			json.WriteStartArray("unsocketedRelicStones");
			foreach (var stone in snapshot.UnsocketedRelicStones)
			{
				WriteRelicStone(json, stone);
			}

			json.WriteEndArray();

			json.WriteStartObject("resources");
			foreach (var resource in snapshot.Resources)
			{
				json.WriteNumber(resource.Name, resource.Amount);
			}

			json.WriteEndObject();

			json.WriteStartObject("shards");
			foreach (var shard in snapshot.Shards)
			{
				json.WriteNumber(shard.Type, shard.Count);
			}

			json.WriteEndObject();

			json.WriteStartObject("items");
			foreach (var item in snapshot.Items)
			{
				json.WriteNumber(item.Name, item.Count);
			}

			json.WriteEndObject();

			json.WriteStartObject("greatHall");
			foreach (var affinity in snapshot.GreatHall)
			{
				json.WriteStartObject(affinity.Affinity);
				foreach (var level in affinity.Levels)
				{
					json.WriteNumber(level.Stat, level.Level);
				}

				json.WriteEndObject();
			}

			json.WriteEndObject();

			json.WriteStartObject("factionGuardians");
			foreach (var faction in snapshot.FactionGuardians)
			{
				json.WriteStartObject(faction.Faction);
				foreach (var rarity in faction.Rarities)
				{
					json.WriteStartArray(rarity.Rarity);
					foreach (var slot in rarity.Slots)
					{
						json.WriteStartArray();
						foreach (var championId in slot.ChampionIds)
						{
							json.WriteNumberValue(championId);
						}

						json.WriteEndArray();
					}

					json.WriteEndArray();
				}

				json.WriteEndObject();
			}

			json.WriteEndObject();

			var classic = snapshot.ClassicArena;
			json.WriteStartObject("classicArena");
			WriteArenaStanding(json, classic.League, classic.Points, classic.Battles, classic.Wins, classic.Losses);
			json.WritePropertyName("defenceTeam");
			WriteArenaTeam(json, classic.DefenceTeam);
			json.WriteEndObject();

			var tag = snapshot.TagArena;
			json.WriteStartObject("tagArena");
			WriteArenaStanding(json, tag.League, tag.Points, tag.Battles, tag.Wins, tag.Losses);
			json.WriteStartArray("defenceTeams");
			foreach (var team in tag.DefenceTeams)
			{
				WriteArenaTeam(json, team);
			}

			json.WriteEndArray();
			json.WriteEndObject();

			json.WriteEndObject();
		}

		buffer.WriteByte((byte)'\n');
		return buffer.ToArray();
	}

	private static void WriteArenaStanding(Utf8JsonWriter json, string? league, long points, int battles, int wins, int losses)
	{
		json.WriteString("league", league);
		json.WriteNumber("points", points);
		json.WriteNumber("battles", battles);
		json.WriteNumber("wins", wins);
		json.WriteNumber("losses", losses);
	}

	private static void WriteArenaTeam(Utf8JsonWriter json, ArenaTeam team)
	{
		json.WriteStartArray();
		foreach (var championId in team.ChampionIds)
		{
			json.WriteNumberValue(championId);
		}

		json.WriteEndArray();
	}

	private static void WriteChampion(Utf8JsonWriter json, Champion champion)
	{
		json.WriteStartObject();
		json.WriteNumber("id", champion.Id);
		json.WriteNumber("typeId", champion.TypeId);
		json.WriteString("name", champion.Name);
		json.WriteString("rarity", champion.Rarity);
		json.WriteString("affinity", champion.Affinity);
		json.WriteString("faction", champion.Faction);
		json.WriteString("rank", champion.Rank);
		json.WriteNumber("level", champion.Level);
		json.WriteString("awakening", champion.Awakening);
		json.WriteString("blessing", champion.Blessing);
		json.WriteNumber("empowerment", champion.Empowerment);

		json.WriteStartArray("skills");
		foreach (var skill in champion.Skills)
		{
			json.WriteStartObject();
			json.WriteNumber("typeId", skill.TypeId);
			json.WriteString("name", skill.Name);
			json.WriteNumber("level", skill.Level);
			json.WriteEndObject();
		}

		json.WriteEndArray();

		json.WriteStartArray("masteries");
		foreach (var mastery in champion.Masteries)
		{
			json.WriteNumberValue(mastery);
		}

		json.WriteEndArray();

		json.WriteBoolean("locked", champion.Locked);
		json.WriteString("marker", champion.Marker);
		json.WriteString("location", champion.Location.ToString());

		json.WriteStartObject("artifacts");
		foreach (var artifact in champion.Artifacts)
		{
			json.WritePropertyName(artifact.Slot);
			WriteArtifact(json, artifact);
		}

		json.WriteEndObject();

		json.WriteStartArray("relics");
		foreach (var relic in champion.Relics)
		{
			WriteRelic(json, relic);
		}

		json.WriteEndArray();

		json.WriteStartObject("stats");
		WriteStats(json, "total", champion.Stats.Total);
		json.WriteStartObject("bySource");
		WriteStats(json, "base", champion.Stats.Base);
		// A Stat source that raises nothing is left out.
		foreach (var (source, stats) in new[] { (Name: "artifacts", Stats: champion.Stats.Artifacts), (Name: "sets", Stats: champion.Stats.Sets), (Name: "masteries", Stats: champion.Stats.Masteries), (Name: "greatHall", Stats: champion.Stats.GreatHall), (Name: "factionGuardians", Stats: champion.Stats.FactionGuardians), (Name: "awakening", Stats: champion.Stats.Awakening), (Name: "empowerment", Stats: champion.Stats.Empowerment), (Name: "relic", Stats: champion.Stats.Relics), (Name: "arenaLeague", Stats: champion.Stats.ArenaLeague) }.Where(source => source.Stats.Count > 0))
		{
			WriteStats(json, source, stats);
		}

		json.WriteEndObject();
		json.WriteEndObject();
		json.WriteEndObject();
	}

	/// <summary>The Total stats, or the stats one part of <c>bySource</c> gives, each under its name, in the game's order of stats.</summary>
	private static void WriteStats(Utf8JsonWriter json, string name, IReadOnlyDictionary<Stat, double> stats)
	{
		json.WriteStartObject(name);
		foreach (var (stat, value) in stats.OrderBy(entry => entry.Key))
		{
			json.WriteNumber(stat.ToString(), value);
		}

		json.WriteEndObject();
	}

	/// <summary>A Relic, worn or not, in the same shape.</summary>
	private static void WriteRelic(Utf8JsonWriter json, Relic relic)
	{
		json.WriteStartObject();
		json.WriteNumber("id", relic.Id);
		json.WriteNumber("typeId", relic.TypeId);
		json.WriteString("name", relic.Name);
		json.WriteString("rarity", relic.Rarity);
		json.WriteString("rank", relic.Rank);
		json.WriteNumber("level", relic.Level);
		json.WriteNumber("skillLevel", relic.SkillLevel);

		json.WriteStartArray("bonuses");
		foreach (var bonus in relic.Bonuses)
		{
			json.WriteStartObject();
			json.WriteString("stat", bonus.Stat);
			json.WriteNumber("value", bonus.Value);
			json.WriteBoolean("flat", bonus.Flat);
			json.WriteEndObject();
		}

		json.WriteEndArray();
		WriteRelicSkills(json, relic.Skills);

		json.WriteStartArray("factionSkills");
		foreach (var skill in relic.FactionSkills)
		{
			json.WriteStartObject();
			json.WriteString("faction", skill.Faction);
			json.WriteNumber("typeId", skill.TypeId);
			json.WriteString("description", skill.Description);
			json.WriteEndObject();
		}

		json.WriteEndArray();

		json.WriteStartArray("sockets");
		foreach (var socket in relic.Sockets)
		{
			json.WriteStartObject();
			json.WriteString("shape", socket.Shape);
			json.WritePropertyName("stone");
			if (socket.Stone is { } stone)
			{
				WriteRelicStone(json, stone);
			}
			else
			{
				json.WriteNullValue();
			}

			json.WriteEndObject();
		}

		json.WriteEndArray();
		json.WriteEndObject();
	}

	/// <summary>A Relic stone, in a socket or not, in the same shape.</summary>
	private static void WriteRelicStone(Utf8JsonWriter json, RelicStone stone)
	{
		json.WriteStartObject();
		json.WriteNumber("id", stone.Id);
		json.WriteNumber("typeId", stone.TypeId);
		json.WriteString("name", stone.Name);
		json.WriteString("rarity", stone.Rarity);
		json.WriteString("shape", stone.Shape);
		WriteRelicSkills(json, stone.Skills);
		json.WriteEndObject();
	}

	private static void WriteRelicSkills(Utf8JsonWriter json, IReadOnlyList<RelicSkill> skills)
	{
		json.WriteStartArray("skills");
		foreach (var skill in skills)
		{
			json.WriteStartObject();
			json.WriteNumber("typeId", skill.TypeId);
			json.WriteString("description", skill.Description);
			json.WriteEndObject();
		}

		json.WriteEndArray();
	}

	/// <summary>An Artifact, worn or not, in the same shape.</summary>
	private static void WriteArtifact(Utf8JsonWriter json, Artifact artifact)
	{
		json.WriteStartObject();
		json.WriteNumber("id", artifact.Id);
		json.WriteString("slot", artifact.Slot);
		json.WriteString("setId", artifact.SetId);
		json.WriteString("setName", artifact.SetName);
		json.WriteString("rank", artifact.Rank);
		json.WriteString("rarity", artifact.Rarity);
		json.WriteNumber("level", artifact.Level);
		if (artifact.AscensionLevel is { } ascensionLevel)
		{
			json.WriteNumber("ascensionLevel", ascensionLevel);
		}
		else
		{
			json.WriteNull("ascensionLevel");
		}

		json.WriteString("factionLock", artifact.FactionLock);
		json.WritePropertyName("primary");
		WriteBonus(json, artifact.Primary);
		json.WritePropertyName("ascensionBonus");
		if (artifact.AscensionBonus is { } ascensionBonus)
		{
			WriteBonus(json, ascensionBonus);
		}
		else
		{
			json.WriteNullValue();
		}

		json.WriteStartArray("substats");
		foreach (var substat in artifact.Substats)
		{
			WriteBonus(json, substat);
		}

		json.WriteEndArray();
		json.WriteEndObject();
	}

	/// <summary>A bonus, its value and Glyph as the shortest number that reads back as exactly the value the game holds.</summary>
	private static void WriteBonus(Utf8JsonWriter json, ArtifactBonus bonus)
	{
		json.WriteStartObject();
		json.WriteString("stat", bonus.Stat);
		json.WriteNumber("value", bonus.Value);
		json.WriteBoolean("flat", bonus.Flat);
		json.WriteNumber("glyph", bonus.Glyph);
		json.WriteNumber("rolls", bonus.Rolls);
		json.WriteEndObject();
	}
}
