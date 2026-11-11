# The Snapshot format

A Snapshot is everything raid-bridge read from one Account at one moment, as one JSON document. `raid-bridge snapshot` writes one per run, to a new file, and the player uses that file however they choose. This page is the format's reference: a test checks the writer's output against the example below, so the two can't drift apart.

A Snapshot holds the Account, its Champions with their Base stats, the Artifacts and Relics each Champion wears, the Artifacts and Relics no Champion wears, the Relic stones in no socket, the Account's Resources, Shards and Items, its Great Hall, its Faction Guardians, and its Classic and Tag Arena.

## Example

A synthetic Account: id `123456789`, hashed with a salt of the bytes `00 01 02 … 1f`, captured at 21:40 UTC on 31 October 2026, with three made-up Champions, one each in the roster, the Vault and the Deep Vault, whose Base stats come from made-up base stats and rank multipliers. The first wears three made-up Artifacts and a made-up Relic with a Relic stone in one of its two sockets. Two more Artifacts and a Relic are worn by nobody, and another stone is in no socket. The Account holds three Resources, two types of Shard and two Items, has bought levels in the Great Hall of two Affinities, has placed both its Rare Champions in one guardian slot and its Legendary alone in another, and has set all three Champions in its Classic Arena defence team, its Legendary in its first Tag Arena team and its Rare Champions in its second, leaving its third empty.

```json
{
  "schemaVersion": 1,
  "generator": {
    "name": "raid-bridge",
    "version": "0.1.0"
  },
  "capturedAt": "2026-10-31T21:40:00Z",
  "account": {
    "idHash": "hmac-sha256:a2fb4a2e5d7a21b17d1213d493a97fffd9c65f4f57dcd304c0b8bc58d310027f"
  },
  "champions": [
    {
      "id": 3,
      "typeId": 5240,
      "name": "Synthetic Knight",
      "rarity": "Legendary",
      "affinity": "Void",
      "faction": "DarkElves",
      "rank": "Stars6",
      "level": 60,
      "awakening": "Stars2",
      "blessing": "Necromancy",
      "empowerment": 1,
      "skills": [
        {
          "typeId": 52401,
          "name": "Synthetic Strike",
          "level": 4
        },
        {
          "typeId": 52402,
          "name": "Synthetic Guard",
          "level": 3
        }
      ],
      "masteries": [
        500111,
        500112
      ],
      "locked": true,
      "marker": "Favorite",
      "location": "Roster",
      "artifacts": {
        "Helmet": {
          "id": 102,
          "slot": "Helmet",
          "setId": "Hp",
          "setName": "Synthetic Life",
          "rank": "Five",
          "rarity": "Epic",
          "level": 12,
          "ascensionLevel": 2,
          "factionLock": null,
          "primary": {
            "stat": "Health",
            "value": 2120,
            "flat": true,
            "glyph": 0,
            "rolls": 0
          },
          "ascensionBonus": {
            "stat": "Defence",
            "value": 0.0400000000372529,
            "flat": false,
            "glyph": 0,
            "rolls": 0
          },
          "substats": []
        },
        "Weapon": {
          "id": 101,
          "slot": "Weapon",
          "setId": "AttackSpeed",
          "setName": "Synthetic Speed",
          "rank": "Six",
          "rarity": "Legendary",
          "level": 16,
          "ascensionLevel": null,
          "factionLock": null,
          "primary": {
            "stat": "Attack",
            "value": 265,
            "flat": true,
            "glyph": 0,
            "rolls": 0
          },
          "ascensionBonus": null,
          "substats": [
            {
              "stat": "Speed",
              "value": 12,
              "flat": true,
              "glyph": 3,
              "rolls": 2
            },
            {
              "stat": "CriticalChance",
              "value": 0.060000000055879354,
              "flat": false,
              "glyph": 0,
              "rolls": 1
            }
          ]
        },
        "Ring": {
          "id": 103,
          "slot": "Ring",
          "setId": "Hp",
          "setName": "Synthetic Life",
          "rank": "Six",
          "rarity": "Legendary",
          "level": 8,
          "ascensionLevel": null,
          "factionLock": "DarkElves",
          "primary": {
            "stat": "Health",
            "value": 1640,
            "flat": true,
            "glyph": 0,
            "rolls": 0
          },
          "ascensionBonus": null,
          "substats": [
            {
              "stat": "Attack",
              "value": 22,
              "flat": true,
              "glyph": 0,
              "rolls": 0
            }
          ]
        }
      },
      "relics": [
        {
          "id": 201,
          "typeId": 1,
          "name": "Synthetic Grail",
          "rarity": "Epic",
          "rank": "Rank5",
          "level": 15,
          "skillLevel": 5,
          "bonuses": [
            {
              "stat": "Attack",
              "value": 308,
              "flat": true
            },
            {
              "stat": "Health",
              "value": 5400,
              "flat": true
            },
            {
              "stat": "CriticalDamage",
              "value": 0.3600000001024455,
              "flat": false
            }
          ],
          "skills": [
            {
              "typeId": 3000000,
              "description": "Wearer deals more damage."
            }
          ],
          "factionSkills": [
            {
              "faction": "DarkElves",
              "typeId": 3200000,
              "description": "Dark Elves strike first."
            }
          ],
          "sockets": [
            {
              "shape": "Circle",
              "stone": {
                "id": 301,
                "typeId": 11,
                "name": "Synthetic Spark",
                "rarity": "Rare",
                "shape": "Circle",
                "skills": [
                  {
                    "typeId": 4000010,
                    "description": "Turn Meter effects are stronger."
                  }
                ]
              }
            },
            {
              "shape": "Diamond",
              "stone": null
            }
          ]
        }
      ],
      "stats": {
        "bySource": {
          "base": {
            "Health": 12375,
            "Attack": 660,
            "Defence": 742,
            "Speed": 101,
            "Resistance": 40,
            "Accuracy": 10,
            "CriticalChance": 0.14999999990686774,
            "CriticalDamage": 0.5
          },
          "artifacts": {
            "Health": 3760,
            "Attack": 287,
            "Defence": 29.680000027641654,
            "Speed": 15,
            "CriticalChance": 0.060000000055879354
          },
          "sets": {
            "Health": 1856.2499988474883
          }
        }
      }
    },
    {
      "id": 7,
      "typeId": 6010,
      "name": "Synthetic Archer",
      "rarity": "Rare",
      "affinity": "Force",
      "faction": "BannerLords",
      "rank": "Stars3",
      "level": 30,
      "awakening": null,
      "blessing": null,
      "empowerment": 0,
      "skills": [
        {
          "typeId": 60101,
          "name": "Synthetic Volley",
          "level": 1
        }
      ],
      "masteries": [],
      "locked": false,
      "marker": "None",
      "location": "Vault",
      "artifacts": {},
      "relics": [],
      "stats": {
        "bySource": {
          "base": {
            "Health": 3780,
            "Attack": 396,
            "Defence": 216,
            "Speed": 98,
            "Resistance": 30,
            "Accuracy": 0,
            "CriticalChance": 0.14999999990686774,
            "CriticalDamage": 0.5700000000651926
          }
        }
      }
    },
    {
      "id": 12,
      "typeId": 6010,
      "name": "Synthetic Archer",
      "rarity": "Rare",
      "affinity": "Force",
      "faction": "BannerLords",
      "rank": "Stars5",
      "level": 50,
      "awakening": "Stars1",
      "blessing": null,
      "empowerment": 0,
      "skills": [
        {
          "typeId": 60101,
          "name": "Synthetic Volley",
          "level": 2
        }
      ],
      "masteries": [],
      "locked": false,
      "marker": "None",
      "location": "DeepVault",
      "artifacts": {},
      "relics": [],
      "stats": {
        "bySource": {
          "base": {
            "Health": 6300,
            "Attack": 660,
            "Defence": 360,
            "Speed": 98,
            "Resistance": 30,
            "Accuracy": 0,
            "CriticalChance": 0.14999999990686774,
            "CriticalDamage": 0.5700000000651926
          }
        }
      }
    }
  ],
  "unwornArtifacts": [
    {
      "id": 104,
      "slot": "Boots",
      "setId": "AttackSpeed",
      "setName": "Synthetic Speed",
      "rank": "Six",
      "rarity": "Legendary",
      "level": 16,
      "ascensionLevel": null,
      "factionLock": null,
      "primary": {
        "stat": "Speed",
        "value": 45,
        "flat": true,
        "glyph": 0,
        "rolls": 0
      },
      "ascensionBonus": null,
      "substats": []
    },
    {
      "id": 105,
      "slot": "Cloak",
      "setId": "Hp",
      "setName": "Synthetic Life",
      "rank": "Five",
      "rarity": "Epic",
      "level": 4,
      "ascensionLevel": null,
      "factionLock": "HighElves",
      "primary": {
        "stat": "Attack",
        "value": 120,
        "flat": true,
        "glyph": 0,
        "rolls": 0
      },
      "ascensionBonus": null,
      "substats": [
        {
          "stat": "Speed",
          "value": 5,
          "flat": true,
          "glyph": 2,
          "rolls": 1
        }
      ]
    }
  ],
  "unwornRelics": [
    {
      "id": 203,
      "typeId": 2,
      "name": "Synthetic Harp",
      "rarity": "Legendary",
      "rank": "Rank3",
      "level": 0,
      "skillLevel": 1,
      "bonuses": [
        {
          "stat": "Health",
          "value": 900,
          "flat": true
        }
      ],
      "skills": [
        {
          "typeId": 3000001,
          "description": "Wearer gains Turn Meter."
        }
      ],
      "factionSkills": [],
      "sockets": [
        {
          "shape": "Pentagon",
          "stone": null
        }
      ]
    }
  ],
  "unsocketedRelicStones": [
    {
      "id": 303,
      "typeId": 11,
      "name": "Synthetic Spark",
      "rarity": "Rare",
      "shape": "Circle",
      "skills": [
        {
          "typeId": 4000010,
          "description": "Turn Meter effects are stronger."
        }
      ]
    }
  ],
  "resources": {
    "Energy": 130,
    "Silver": 1250000,
    "Gem": 42
  },
  "shards": {
    "Mystery": 14,
    "Ancient": 2
  },
  "items": {
    "HeroXpBooster100P_1Day": 3,
    "SkillUpgradeItemEpic": 1
  },
  "greatHall": {
    "Magic": {
      "Health": 3,
      "Attack": 2
    },
    "Void": {
      "CriticalDamage": 10
    }
  },
  "factionGuardians": {
    "BannerLords": {
      "Rare": [
        [
          7,
          12
        ]
      ]
    },
    "DarkElves": {
      "Legendary": [
        [
          3
        ]
      ]
    }
  },
  "classicArena": {
    "league": "SilverIII",
    "points": 1450,
    "battles": 12,
    "wins": 8,
    "losses": 3,
    "defenceTeam": [
      12,
      3,
      7
    ]
  },
  "tagArena": {
    "league": "BronzeIV",
    "points": 820,
    "battles": 6,
    "wins": 2,
    "losses": 4,
    "defenceTeams": [
      [
        3
      ],
      [
        7,
        12
      ],
      []
    ]
  }
}
```

## Fields

- `schemaVersion`: an integer, `1` for now. It changes only on a breaking change, so a reader that knows version 1 can read any Snapshot that says 1. An added field doesn't change it.
- `generator`: the raid-bridge build that wrote the Snapshot, as `name` and `version`. A build from a git checkout adds `+` and the commit it was built from to the version, such as `0.1.0+3ad5287c3e580145db0fc793165ce2dfd284ca24`.
- `capturedAt`: when the Account was read, in ISO 8601 UTC to the second, ending in `Z`.
- `account.idHash`: the Account id, hashed (below). The id itself never leaves the player's machine.
- `champions`: every Champion on the Account, sorted by `id`, wherever it is kept. Each has:
  - `id`: the Champion's id on the Account.
  - `typeId` and `name`: the Champion's type, and its name as the Game client shows it (below). Ascension isn't written, because the game holds it only as a rule applied to the type, not as a value.
  - `rarity`, `affinity`, `faction` and `rank`: such as `"Legendary"`, `"Void"`, `"DarkElves"` and `"Stars6"`.
  - `level` and `empowerment`: numbers, `empowerment` 0 for none.
  - `awakening` and `blessing`: such as `"Stars2"` and `"Necromancy"`, or `null` when the Champion has none.
  - `skills`: each skill's `typeId`, `name` and `level`, in the order the game keeps them.
  - `masteries`: the ids of the masteries the Champion took, in the order the game keeps them, or `[]`, without names: raid-bridge has found no mastery names in the game's data.
  - `locked`: whether the player locked the Champion.
  - `marker`: the marker the player set, such as `"Favorite"`, or `"None"`.
  - `location`: `"Roster"`, `"Vault"` or `"DeepVault"`.
  - `artifacts`: the Artifacts the Champion wears, each under the name of the slot it fills, such as `"Weapon"` or `"Ring"`, in the game's order of slots. An empty slot is left out, never written as `null`, so a Champion that wears nothing has `{}`. Each Artifact has:
    - `id`: the Artifact's id on the Account.
    - `slot`: the slot it fills, the same name it is filed under.
    - `setId` and `setName`: its set, such as `"AttackSpeed"`, and the set's name as the Game client shows it (below), or both `null` when the Artifact is in no set.
    - `rank` and `rarity`: such as `"Six"` and `"Legendary"`.
    - `level`: a number.
    - `ascensionLevel`: a number, or `null` when the Artifact was never ascended.
    - `factionLock`: the Faction an accessory is locked to, such as `"DarkElves"`, or `null` when the Artifact has none.
    - `primary`: its main stat, as a bonus (below).
    - `ascensionBonus`: the bonus ascension gave it, or `null` when it has none.
    - `substats`: its Substats, as bonuses, in the order the game keeps them, or `[]`.
  - `relics`: the Relics the Champion wears, in the order the game keeps them, or `[]` when it wears none. The game lets a Champion wear one Relic today, but keeps them as a list. Each Relic has:
    - `id`: the Relic's id on the Account.
    - `typeId` and `name`: its type, and its name as the Game client shows it (below).
    - `rarity` and `rank`: such as `"Legendary"` and `"Rank3"`. A Relic's rank, one to six, is its own, not a Champion's: it caps the Relic's level.
    - `level`: a number, from 0.
    - `skillLevel`: the level its passive effects are at, from 1, which the game's own table gives for the Relic's level: it rises at set levels (0, 6, 9, 12, 15 and 18 in the game's data today), so a Relic between two of them has the lower one's.
    - `bonuses`: the stats it raises, in its type's order, each with `stat`, `value` and `flat` as an Artifact's bonus has them, without `glyph` or `rolls`. A Relic's type raises three stats, which open one at a time as it levels (at levels 0, 9 and 15 in the game's data today): a stat that hasn't opened yet is left out.
    - `skills`: its own passive effects, each with its skill's `typeId` and the `description` the Game client shows for it (below).
    - `factionSkills`: the passive effects it adds only for a wearer of a given Faction, each with that `faction`, such as `"DarkElves"`, its `typeId` and its `description`, in the order its type lists them, or `[]`. Every Relic lists them, worn or not, so a reader can tell which Faction a spare Relic suits, and whether a worn Relic's wearer gets them. Few Relic types have any.
    - `sockets`: its sockets, in the order the game keeps them, each with its `shape`, such as `"Circle"`, and the Relic `stone` in it, or `null` when it is empty.
  - `stats`: the Champion's stats, worked out from what raid-bridge read (below). `bySource` holds the Champion's Base stats and what each Stat source adds to them, each stat under its name, such as `"Speed"`, in the order of the numbers the game's code gives the stats. All are in the units of an Artifact's bonuses: points, and fractions for `CriticalChance` and `CriticalDamage`. So far it holds:
    - `base`: the Champion's Base stats at its rank, level and ascension, the white numbers on its champion screen. It always has all eight stats: `Health`, `Attack`, `Defence`, `Speed`, `Resistance`, `Accuracy`, `CriticalChance` and `CriticalDamage`.
    - `artifacts`: what the Artifacts it wears add, each one's main stat, ascension bonus and Substats, each with its Glyph.
    - `sets`: what the sets of those Artifacts add.

    A Stat source holds only the stats it raises, and is left out when it raises none, so a Champion that wears nothing has only `base`. The other Stat sources and the Total stats are still to come.

  Each Relic stone has:
  - `id`: the stone's id on the Account.
  - `typeId` and `name`: its type, and its name as the Game client shows it.
  - `rarity` and `shape`: such as `"Rare"` and `"Circle"`. A stone fits only a socket of its shape.
  - `skills`: what it adds, in the shape of a Relic's `skills`. A stone adds skills, not stats.

  Each bonus has:
  - `stat`: the stat it raises, such as `"Speed"` or `"CriticalChance"`.
  - `value`: how much. When `flat` is `true` the value is a number of points, such as `265` Attack; when it is `false` the value is a fraction, so 6% is about `0.06`.
  - `glyph`: the value a Glyph adds to it, in the same unit, or `0`. The game can hold a Glyph's value in two parts, and this is both.
  - `rolls`: how many times the bonus was raised as the Artifact levelled, or `0`.
- `unwornArtifacts`: every Artifact on the Account that no Champion wears, sorted by `id`, or `[]` when every Artifact is worn. Each has exactly the fields of a worn Artifact, its `slot` being the slot it fits. An Artifact a Champion wears is under that Champion's `artifacts` and nowhere else, so every Artifact on the Account appears exactly once.
- `unwornRelics`: every Relic on the Account that no Champion wears, sorted by `id`, or `[]`. Each has exactly the fields of a worn Relic. A Relic a Champion wears is under that Champion's `relics` and nowhere else, so every Relic on the Account appears exactly once.
- `unsocketedRelicStones`: every Relic stone on the Account that is in no socket, sorted by `id`, or `[]`. A stone in a socket is under that socket's `stone` and nowhere else, so every stone on the Account appears exactly once.
- `resources`: every Resource the game keeps a count of on the Account, such as silver, gems, energy, keys and tokens, each under its name, such as `"Silver"`, with the amount the Account holds. The game holds each amount as a floating-point number, so it is written exactly as held and never rounded.
- `shards`: the Account's Shards, each type under its name, such as `"Ancient"`, with how many the Account holds.
- `items`: the Account's Items, such as potions, tomes and boosters, each under its name, such as `"SkillUpgradeItemEpic"`, with how many the Account holds.

  `resources`, `shards` and `items` hold only what the game keeps an entry for, so a name that isn't there means the game keeps no count of it, and a count of `0` is one the game keeps. Their names come in the order of the numbers the game's code gives them, not alphabetically. Each is always written, as `{}` when the Account holds nothing in it.
- `greatHall`: the Account's Great Hall, each Affinity under its name, such as `"Magic"`, holding the level of each stat bought for it under the stat's name, such as `"Health"`. A level is at least 1 and at most the highest the game offers for that stat, and a stat with no level bought isn't there. An Affinity with no levels is left out, so `greatHall` is `{}` when the player hasn't bought a level. The Affinities and the stats come in the order of the numbers the game's code gives them. Only levels are written: the bonus a level gives is the same for every Account, so it isn't the Account's to write.
- `factionGuardians`: the Account's Faction Guardians, each Faction under its name, such as `"DarkElves"`, holding its guardian slots of each rarity under the rarity's name, such as `"Legendary"`. Each slot is a list of the `id`s of the Champions in it, in the order of the slot's positions, and each `id` is a Champion in `champions` of that Faction and rarity, guarding in no other slot. A slot with no Champion in it is left out, so a slot's place in the list isn't its number in the game, and so is an empty position, so a half-filled slot is a list of one `id`. A rarity with no Champion in its slots is left out, and so is a Faction with none, so `factionGuardians` is `{}` when the player hasn't placed a Champion. The slots keep the game's order, and the Factions and rarities come in the order of the numbers the game's code gives them. Only the Champions are written: the bonus a slot gives is the same for every Account, so it isn't the Account's to write.
- `classicArena` and `tagArena`: the Account's Classic Arena and Tag Arena. Each has:
  - `league`: the league the Account is in, such as `"GoldII"`, or `null` when the game holds none, as it does for a Classic Arena with no defence team set.
  - `points`: the Account's Arena points.
  - `battles`, `wins` and `losses`: this week's counts, as of `capturedAt`: the game starts them again every week. Each is 0 or more. `battles` is the battles the player started, and `wins` and `losses` are the game's own counts of wins and losses, which can add up to more than `battles`: they seem to count battles fought on defence too.

  `classicArena`'s `defenceTeam` is the `id`s of the Champions in its defence team, in the game's order of positions, and `tagArena`'s `defenceTeams` is its three defence teams, in the game's order, each a list of `id`s the same way. Each `id` is a Champion in `champions`, defending only once in that Arena. An empty position is left out, so a Champion's place in the list isn't the number of its position, and a team with no Champion in it is `[]`, kept in its place among the teams. Both are always written: with no defence team set, `defenceTeam` is `[]` and `defenceTeams` is `[]`.

`rarity`, `affinity`, `faction`, `rank`, `awakening`, `blessing`, `marker`, `slot`, `setId`, `factionLock`, `stat` and `shape`, and the names in `resources`, `shards`, `items`, `greatHall` and `factionGuardians`, and each `league`, are written as the names the game's own code gives them, which aren't always the words the game shows: `rank` `"Stars6"` is six stars, `faction` `"DarkElves"` is Dark Elves, the set `"AttackSpeed"` is the Speed set, the Shards `"Legendary"` and `"Mythical"` are Sacred and Primal, and the Item `"SkillUpgradeItemEpic"` is an Epic Tome.

An Artifact's `value` and `glyph` are exactly what the game holds. The game keeps them in fixed point, as a whole number of steps of 2^-32, so a value the game shows as 6% is held as the nearest step, 0.0600000000558793…, and written as the shortest number that reads back as exactly that step: `0.060000000055879354`. Round it to show it, never to store it.

Keys are camelCase, apart from the game's own names, and always come in this order, indented by two spaces with `\n` line ends, so two Snapshots diff cleanly. Values are only what raid-bridge read or worked out from what it read: nothing is guessed or filled in. A run that can't read a value fails and writes no file. Two things are worked out:

- A Relic's `bonuses` and `skillLevel`. The game holds no stat values or skill level on a Relic, only its type and level, so each is the value the game's own static table gives at that level, read from the Game client like the names are. Nothing in the sum is raid-bridge's own.
- A Champion's `stats`. The game keeps no stats on a Champion, so raid-bridge works them out (ADR-0005). Every number in them is the Account's own or the game's static data, read from the Game client like the names are. Only the way they add up is raid-bridge's, and it follows the game's own code:
  - `base` is built the way the game builds a Champion's Base stats. `Health`, `Attack` and `Defence` scale by the Champion's rank and level and round to whole points as the game rounds them, and `Health` is multiplied by 15. `Speed`, `Resistance` and `Accuracy` are as the Champion's type holds them. `CriticalChance` and `CriticalDamage` turn from whole percents into fractions. raid-bridge works in the game's own fixed point here, so each value is the one the game itself holds, such as `0.14999999990686774` for the 15% the screen shows.
  - Every other Stat source adds up its bonuses the way the game does. A flat bonus adds its points. A percentage bonus adds that share of the Champion's Base stat, so 15% `Health` on 12,375 adds 1,856.25. Any bonus to `CriticalChance` or `CriticalDamage` adds its value as it is. A set counts once for each complete set of pieces worn. A set that grows by the piece counts the entry for the number of pieces worn. Bonuses to stats the screen doesn't show, such as a set's ignore-defence, are left out. These sums are unrounded.

  To show the stats as the champion screen does:
  - Round a Base stat's fraction to a whole percent. The points are whole already.
  - For a green number, round each Stat source to whole points or whole percents, a half going to the even one, then add them up. The screen does the same, but counts `artifacts` and `sets` as one source, so add those two together before rounding.

## Names

A Champion's, a skill's, an Artifact set's, a Relic's and a Relic stone's name, and a Relic skill's `description`, is the text the Game client shows for it, in the language the player set: raid-bridge looks its key up in the client's loaded localization tables, and takes the default text the game gives the key when neither table holds it. If there is neither, the run fails rather than write a made-up name.

A Relic's or a stone's skill has no name of its own in the game's data, only placeholder text such as "Skill 3000000 name", so it is written with the description the Relic's screen shows instead: the description of its Relic type, of the Faction's entry that adds it, or of its stone's type. The description belongs to that source, so if a source ever lists more than one skill, each carries the same text; in the game's data today each Relic type and each stone type lists one. The game holds a description with colour tags that only style it on screen, such as `<color=#1ee600>2%</color>` around a number shown in colour, and they are left out, so `"Wearer deals <color=#1ee600>2%</color> more damage"` is written `"Wearer deals 2% more damage"`.

## The Account id hash

`idHash` is `hmac-sha256:` followed by the lowercase hex of an HMAC-SHA256:

- the key is the player's salt, 32 bytes;
- the message is the Account id written as a decimal number with no sign, separator or padding, in UTF-8. The id `123456789` is the nine bytes of the text `123456789`.

The same Account always gets the same hash on the same machine, so anything reading the Snapshots can tell they belong together. Without the salt, nobody can work out the id from the hash or check whether a hash belongs to a given id.

## The salt

raid-bridge makes the salt on its first run: 32 random bytes in `$XDG_CONFIG_HOME/raid-bridge/salt`, or `~/.config/raid-bridge/salt` when `XDG_CONFIG_HOME` isn't set. The file is readable by the player only (mode 0600), and raid-bridge never prints or logs it.

Keep that file. If it is lost or deleted, the next run makes a new salt, and from then on every hash is different: the hashes no longer show that new Snapshots come from the same Account as the old ones. raid-bridge never replaces a salt file it finds, and refuses to run if the file isn't a salt. On Linux and macOS it also refuses to run if other users may read or change the salt: the file may grant nothing to its group or others, and its folder no write to them. Windows has no such modes, so there the check doesn't run. <!-- (CodeRabbit, PR #42) -->

## The file

By default a Snapshot is written to the current folder as `snapshot-<capturedAt>.json`, with the time in compact UTC form, such as `snapshot-20261031T214000Z.json`. `--out PATH` chooses the file instead. raid-bridge never overwrites a file: if the path exists, the run fails and writes nothing.
