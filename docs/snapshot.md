# The Snapshot format

A Snapshot is everything raid-bridge read from one Account at one moment, as one JSON document. `raid-bridge snapshot` writes one per run, to a new file, and the player uses that file however they choose. This page is the format's reference: a test checks the writer's output against the example below, so the two can't drift apart.

A Snapshot holds the Account, its level, experience and power, its Champions with their Base stats, the Artifacts and Relics each Champion wears, the Artifacts and Relics no Champion wears, the Relic stones in no socket, the Account's Resources, Shards and Items, its Great Hall and its Area Bonuses, its Faction Guardians, its Classic, Tag and Live Arena, and its Missions.

## Example

A synthetic Account: id `123456789`, hashed with a salt of the bytes `00 01 02 … 1f`, captured at 21:40 UTC on 31 October 2026, at level 54 with a made-up power, with three made-up Champions, one each in the roster, the Vault and the Deep Vault, whose Base stats come from made-up base stats and rank multipliers. The first wears three made-up Artifacts and a made-up Relic with a Relic stone in one of its two sockets. Two more Artifacts and a Relic are worn by nobody, and another stone is in no socket. The Account holds three Resources, two types of Shard and two Items, has bought levels in the Great Hall of two Affinities and Area Bonuses in two areas, has placed both its Rare Champions in one guardian slot and its Legendary alone in another, and has set all three Champions in its Classic Arena defence team, its Legendary in its first Tag Arena team and its Rare Champions in its second, leaving its third empty, and is in SilverII in the Live Arena. It has finished the first group of Arbiter's Missions and is on the second, with one Mission finished, one under way and one not started.

```json
{
  "schemaVersion": 1,
  "generator": {
    "name": "raid-bridge",
    "version": "0.1.0"
  },
  "capturedAt": "2026-10-31T21:40:00Z",
  "account": {
    "idHash": "hmac-sha256:a2fb4a2e5d7a21b17d1213d493a97fffd9c65f4f57dcd304c0b8bc58d310027f",
    "level": 54,
    "experience": 31250,
    "power": 1234567.891
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
        "total": {
          "Health": 32406.75,
          "Attack": 2146.0000000614673,
          "Defence": 845.8800000967458,
          "Speed": 116,
          "Resistance": 50,
          "Accuracy": 20,
          "CriticalChance": 0.2099999999627471,
          "CriticalDamage": 1.1100000001024455
        },
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
          },
          "masteries": {
            "Health": 278,
            "Attack": 75
          },
          "greatHall": {
            "CriticalDamage": 0.25
          },
          "awakening": {
            "Health": 7500,
            "Attack": 750
          },
          "empowerment": {
            "Resistance": 10,
            "Accuracy": 10
          },
          "relic": {
            "Health": 5400,
            "Attack": 308,
            "CriticalDamage": 0.3600000001024455
          },
          "arenaLeague": {
            "Health": 1237.5000011525117,
            "Attack": 66.00000006146729,
            "Defence": 74.20000006910414
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
        "total": {
          "Health": 4536.00000070408,
          "Attack": 435.6000000368804,
          "Defence": 237.60000002011657,
          "Speed": 98,
          "Resistance": 30,
          "Accuracy": 0,
          "CriticalChance": 0.14999999990686774,
          "CriticalDamage": 0.5700000000651926
        },
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
          },
          "factionGuardians": {
            "Health": 378.00000035203993
          },
          "arenaLeague": {
            "Health": 378.00000035203993,
            "Attack": 39.600000036880374,
            "Defence": 21.600000020116568
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
        "total": {
          "Health": 7560.000001173466,
          "Attack": 726.0000000614673,
          "Defence": 396.0000000335276,
          "Speed": 98,
          "Resistance": 30,
          "Accuracy": 0,
          "CriticalChance": 0.14999999990686774,
          "CriticalDamage": 0.5700000000651926
        },
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
          },
          "factionGuardians": {
            "Health": 630.0000005867332
          },
          "arenaLeague": {
            "Health": 630.0000005867332,
            "Attack": 66.00000006146729,
            "Defence": 36.00000003352761
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
  "areaBonuses": {
    "IceGolemCave": {
      "Speed": 2,
      "Accuracy": 1
    },
    "AllianceBoss": {
      "Attack": 4
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
  },
  "liveArena": {
    "league": "SilverII",
    "points": 1520,
    "wins": 14,
    "losses": 9
  },
  "missions": {
    "completedGroups": [
      "Group1"
    ],
    "openGroups": [
      {
        "group": "Group2",
        "section": "ArbiterSection",
        "missions": [
          {
            "prototypeId": 9101,
            "category": "Heroes",
            "state": "Completed",
            "name": "Upgrade 2 Champions to Level 30",
            "collected": null,
            "required": 2
          },
          {
            "prototypeId": 9102,
            "category": "Dungeons",
            "state": "InProgress",
            "name": "Clear Stage 10 of a Synthetic Keep 10 times",
            "collected": 4,
            "required": 10
          },
          {
            "prototypeId": 9103,
            "category": "Clan",
            "state": "New",
            "name": "Deal 2,000,000 damage to the Synthetic Boss",
            "collected": 0,
            "required": 2000000
          }
        ]
      }
    ]
  }
}
```

## Fields

- `schemaVersion`: an integer, `1` for now. It changes only on a breaking change, so a reader that knows version 1 can read any Snapshot that says 1. An added field doesn't change it.
- `generator`: the raid-bridge build that wrote the Snapshot, as `name` and `version`. A build from a git checkout adds `+` and the commit it was built from to the version, such as `0.1.0+3ad5287c3e580145db0fc793165ce2dfd284ca24`.
- `capturedAt`: when the Account was read, in ISO 8601 UTC to the second, ending in `Z`.
- `account.idHash`: the Account id, hashed (below). The id itself never leaves the player's machine.
- `account.level`: the Account's level, 1 or more.
- `account.experience`: the Account's experience as the game holds it, 0 or more.
- `account.power`: the Account's total power, exactly as the game holds it, a number that need not be whole: round it to show it, never to store it.
- `champions`: every Champion on the Account, sorted by `id`, wherever it is kept. Each has:
  - `id`: the Champion's id on the Account.
  - `typeId` and `name`: the Champion's type, and its name as the Game client shows it (below). Ascension isn't written, because the game holds it only as a rule applied to the type, not as a value.
  - `rarity`, `affinity`, `faction` and `rank`: such as `"Legendary"`, `"Void"`, `"DarkElves"` and `"Stars6"`.
  - `level` and `empowerment`: numbers, `empowerment` 0 for none.
  - `awakening` and `blessing`: such as `"Stars2"` and `"Necromancy"`, or `null` when the Champion has none.
  - `skills`: each skill the champion screen shows, with its `typeId`, `name` and `level`, in the order the game keeps them. The game gives some Champions hidden skills too, such as a passive that works behind one the screen shows, and the screen leaves those out; so does the Snapshot, since the game holds no name for them that the player ever sees, only placeholder text such as "Skill 107305 name".
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
  - `stats`: the Champion's stats, worked out from what raid-bridge read (below). `total` holds its Total stats, the eight stats its champion screen shows, unrounded: each is the Champion's Base stat plus what every Stat source in `bySource` adds to it. `bySource` holds the Champion's Base stats and what each Stat source adds to them. Each stat is under its name, such as `"Speed"`, in the order of the numbers the game's code gives the stats. All are in the units of an Artifact's bonuses: points, and fractions for `CriticalChance` and `CriticalDamage`. `bySource` holds:
    - `base`: the Champion's Base stats at its rank, level and ascension, the white numbers on its champion screen. It always has all eight stats: `Health`, `Attack`, `Defence`, `Speed`, `Resistance`, `Accuracy`, `CriticalChance` and `CriticalDamage`.
    - `artifacts`: what the Artifacts it wears add, each one's main stat, ascension bonus and Substats, each with its Glyph.
    - `sets`: what the sets of those Artifacts add.
    - `masteries`: what the Champion's masteries add. A mastery that raises set bonuses, such as Lore of Steel, adds its share of `sets` here.
    - `greatHall`: what the Account's Great Hall adds for the Champion's Affinity, from the levels in `greatHall` below.
    - `factionGuardians`: what the Account's Faction Guardians add for the Champion's Faction and rarity. The game counts that Faction's guardian slots of that rarity in order, only while both places of a slot are filled, so a slot after one that isn't full adds nothing even when full. The Champion doesn't need to be a Guardian itself.
    - `awakening`: what the Champion's Awakening adds. Only an awakened Champion with a blessing gets it: for each star of Awakening up to its own, its blessing names the stats that star raises, worth what the game gives a Champion of its rarity at that star.
    - `empowerment`: what the Champion's Empowerment adds. The levels add up, so level 3 gives the bonuses of levels 1, 2 and 3. An Epic Champion has levels of its own.
    - `relic`: what the Relics the Champion wears add, the `bonuses` of each in `relics` above. A Relic stone adds skills, not stats.
    - `arenaLeague`: what the Account's Classic Arena league adds, a share of `Health`, `Attack` and `Defence` that rises with the league, from 1% in BronzeI to 25% in Platinum in the game's data today. Every Champion gets it, but only while the Account has a Classic Arena defence, as the game counts it: while the game's list of defence Champions, which it keeps beside the team written in `classicArena.defenceTeam`, names any. With none, the game gives nothing, whatever `classicArena`'s league. The two lists have agreed on every Account read so far.

    A Stat source holds only the stats it raises, and is left out when it raises none, so a Champion that wears nothing, has no mastery that raises a stat, gets nothing from the Great Hall or Faction Guardians, and has neither a blessing nor Empowerment, on an Account with no Classic Arena defence team, has only `base`, and a `total` equal to it. <!-- (https://github.com/ANicholasson/raid-bridge/pull/21) -->

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

  A Relic is levelled up and its rank raised with materials of its own, which are here under the game's code names. Starstones, which level a Relic up, are the Resource `"Meteor"`. Basalt, which raises a Relic's rank by one, comes as the Items `"Carbon_Rank1"` to `"Carbon_Rank5"`: `"Carbon_Rank1"` is Rank 1 Basalt, which takes a Relic of rank `"Rank1"` to `"Rank2"`, and so on to `"Carbon_Rank5"`, Rank 5 Basalt, which takes one of rank `"Rank5"` to `"Rank6"`, the highest. In the game's data today every Relic level is priced in `"Meteor"` alone, and each rank in the `"Carbon_Rank"` Item of that rank. The materials a Relic is made from are the Resources whose names start `"RelicCraftMaterial_"`.
- `greatHall`: the Account's Great Hall, each Affinity under its name, such as `"Magic"`, holding the level of each stat bought for it under the stat's name, such as `"Health"`. A level is at least 1 and at most the highest the game offers for that stat, and a stat with no level bought isn't there. An Affinity with no levels is left out, so `greatHall` is `{}` when the player hasn't bought a level. The Affinities and the stats come in the order of the numbers the game's code gives them. Only levels are written: the bonus a level gives is the same for every Account, so it isn't the Account's to write.
- `areaBonuses`: the Account's Area Bonuses, the part of the Great Hall that raises the stats of every Champion fighting in one area, such as a Dungeon, Faction Wars or the Demon Lord, written as `greatHall` is. Each area is under its name, such as `"IceGolemCave"`, holding the level of each stat bought for it under the stat's name, such as `"Speed"`. A level is at least 1 and at most the highest the game offers for that stat in that area, and a stat with no level bought isn't there. An area with no levels is left out, so `areaBonuses` is `{}` when the player hasn't bought one. The areas and the stats come in the order of the numbers the game's code gives them. Only levels are written: the bonus a level gives is the same for every Account. In the game's data today each level adds 2% `Health`, `Attack`, `Defence` and `IgnoreDefence`, 3% `CriticalDamage`, 2 `Speed`, and 8 `Resistance` and `Accuracy`, the same in every area, up to level 10. An Area Bonus counts only in battles in its area, so it isn't in any Champion's `stats`, which are what the champion screen shows. The areas' names are the game's own: `"IceGolemCave"` is Ice Golem's Peak, `"FireGolemCave"` Fire Knight's Castle, `"SpiderCave"` Spider's Den, `"DoubleAscendDungeon"` Iron Twins Fortress, `"ArtifactAscendDungeon"` Sand Devil's Necropolis, `"AccessoryAscendDungeon"` Phantom Shogun's Grove, `"AllianceBoss"` the Demon Lord and `"FactionWar"` Faction Wars.

  Area Bonuses are bought with Crests, won in the Live Arena. They are among `resources` as `"LiveArenaMedal_Bronze"`, `"LiveArenaMedal_Silver"` and `"LiveArenaMedal_Gold"`, the game's names for Bronze, Silver and Gold Crests, and are there only once the game keeps a count of them.
- `factionGuardians`: the Account's Faction Guardians, each Faction under its name, such as `"DarkElves"`, holding its guardian slots of each rarity under the rarity's name, such as `"Legendary"`. Each slot is a list of the `id`s of the Champions in it, in the order of the slot's positions, and each `id` is a Champion in `champions` of that Faction and rarity, guarding in no other slot. A slot with no Champion in it is left out, so a slot's place in the list isn't its number in the game, and so is an empty position, so a half-filled slot is a list of one `id`. A rarity with no Champion in its slots is left out, and so is a Faction with none, so `factionGuardians` is `{}` when the player hasn't placed a Champion. The slots keep the game's order, and the Factions and rarities come in the order of the numbers the game's code gives them. Only the Champions are written: the bonus a slot gives is the same for every Account, so it isn't the Account's to write.
- `classicArena` and `tagArena`: the Account's Classic Arena and Tag Arena. Each has:
  - `league`: the league the Account is in, such as `"GoldII"`, or `null` when the game holds none, as it does for a Classic Arena with no defence team set.
  - `points`: the Account's Arena points.
  - `battles`, `wins` and `losses`: this week's counts, as of `capturedAt`: the game starts them again every week. Each is 0 or more. `battles` is the battles the player started, and `wins` and `losses` are the game's own counts of wins and losses, which can add up to more than `battles`: they seem to count battles fought on defence too.

  `classicArena`'s `defenceTeam` is the `id`s of the Champions in its defence team, in the game's order of positions, and `tagArena`'s `defenceTeams` is its defence teams, as many as the game holds, in the game's order, each a list of `id`s the same way. Each `id` is a Champion in `champions`, defending only once in that Arena. An empty position is left out, so a Champion's place in the list isn't the number of its position, and a team with no Champion in it is `[]`, kept in its place among the teams while another team has a Champion. Both are always written: with no defence team set, `defenceTeam` is `[]` and `defenceTeams` is `[]`. `defenceTeams` is `[]` too when the game holds teams with no Champion in any of them, as it can for an Account that has set no Tag Arena defence.

- `liveArena`: the Account's Live Arena, or `null` when the game holds no Live Arena data for the Account, as it may before the Account reaches the level that opens the Live Arena. It has:
  - `league`: the league the Account is in, such as `"SilverII"`. The game holds no league for the Live Arena: it works the league out from the points each time, as the highest whose points the Account has reached, and `"BronzeI"` below them all, and so does raid-bridge, from the game's own table. An Account that has never fought there is in `"BronzeI"`, with the 900 points it starts with.
  - `points`: the Account's Live Arena points.
  - `wins` and `losses`: the game's counts of the Account's Live Arena wins and losses, each 0 or more.

- `missions`: the Account's Missions, the tasks on the game's Missions screen, such as Arbiter's. The game gives them out in groups, a section of the screen holding several, and holds the Missions of the groups the Account has open, and only the names of those it has finished. So `missions` has:
  - `completedGroups`: the groups the Account has finished, such as `"Group1"`, in the game's order, or `[]`.
  - `openGroups`: each group the Account has open, in the game's order, or `[]`. Each has its `group`, such as `"Group4"`; the `section` of the Missions screen it is in, such as `"ArbiterSection"`; and its `missions`, in the order the game gives them. Each Mission has:
    - `prototypeId`: the Mission's id, the same on every Account.
    - `category`: the kind of task, such as `"Dungeons"`, `"Heroes"` or `"Clan"`, which the screen shows as an icon.
    - `state`: `"New"`, `"InProgress"` or `"Completed"`, as the game holds it. The Missions screen lists a group's Missions in this order from the first one not finished: the one `"InProgress"` open, and each `"New"` one after it locked.
    - `name`: the line the Missions screen shows for it, such as `"Upgrade 5 Champions to Level 60"`, in the language the player set, with its numbers already in it.
    - `collected` and `required`: the Mission's progress, the count the game holds towards the count it requires, such as `4` of `10` battles or `25347525` of `10000000` damage. A count can pass what is required. `collected` is `null` when the game holds no count the screen shows. The game marks a Mission that counts from the Account, such as "Upgrade 5 Champions to Level 60", as holding no count, and while it is unfinished the screen works its progress out from the Account each time it opens; raid-bridge doesn't, so the Champions in `champions` say how far it is. It keeps that mark once the Mission is finished, when `state` says it is done. An unfinished battle Mission of a clan's, outside the Clan Boss's own battles, is `null` too: the screen shows its progress from the clan's Clan Boss data, not from the count the Mission holds. Damage to the Demon Lord is counted on the Mission itself.

`rarity`, `affinity`, `faction`, `rank`, `awakening`, `blessing`, `marker`, `slot`, `setId`, `factionLock`, `stat` and `shape`, and the names in `resources`, `shards`, `items`, `greatHall`, `areaBonuses` and `factionGuardians`, each `league`, and a Mission's `group`, `section`, `category` and `state`, are written as the names the game's own code gives them, which aren't always the words the game shows: `rank` `"Stars6"` is six stars, `faction` `"DarkElves"` is Dark Elves, the set `"AttackSpeed"` is the Speed set, the Shards `"Legendary"` and `"Mythical"` are Sacred and Primal, the Resource `"LiveArenaMedal_Gold"` is a Gold Crest and `"Meteor"` Starstones, and the Items `"SkillUpgradeItemEpic"` and `"Carbon_Rank2"` are an Epic Tome and Rank 2 Basalt.

An Artifact's `value` and `glyph` are exactly what the game holds. The game keeps them in fixed point, as a whole number of steps of 2^-32, so a value the game shows as 6% is held as the nearest step, 0.0600000000558793…, and written as the shortest number that reads back as exactly that step: `0.060000000055879354`. Round it to show it, never to store it.

Keys are camelCase, apart from the game's own names, and always come in this order, indented by two spaces with `\n` line ends, so two Snapshots diff cleanly. Values are only what raid-bridge read or worked out from what it read: nothing is guessed or filled in. A run that can't read a value fails and writes no file. Two things are worked out:

- A Relic's `bonuses` and `skillLevel`. The game holds no stat values or skill level on a Relic, only its type and level, so each is the value the game's own static table gives at that level, read from the Game client like the names are. Nothing in the sum is raid-bridge's own.
- A Champion's `stats`. The game keeps no stats on a Champion, so raid-bridge works them out (ADR-0005). Every number in them is the Account's own or the game's static data, read from the Game client like the names are. Only the way they add up is raid-bridge's, and it follows the game's own code:
  - `base` is built the way the game builds a Champion's Base stats. `Health`, `Attack` and `Defence` scale by the Champion's rank and level and round to whole points as the game rounds them, and `Health` is multiplied by 15. `Speed`, `Resistance` and `Accuracy` are as the Champion's type holds them. `CriticalChance` and `CriticalDamage` turn from whole percents into fractions. raid-bridge works in the game's own fixed point here, so each value is the one the game itself holds, such as `0.14999999990686774` for the 15% the screen shows.
  - Every other Stat source adds up its bonuses the way the game does. A flat bonus adds its points. A percentage bonus adds that share of the Champion's Base stat, so 15% `Health` on 12,375 adds 1,856.25. Any bonus to `CriticalChance` or `CriticalDamage` adds its value as it is. A set counts once for each complete set of pieces worn. A set that grows by the piece counts the entry for the number of pieces worn. The Great Hall, Faction Guardians, Awakening, Empowerment, Relics and the Arena league add as an Artifact's bonuses do. A stat mastery adds its value as it is. A mastery that raises set bonuses adds its share of each set bonus: as it is for `Resistance`, `Accuracy` and the critical stats, and for the other stats by scaling the Base stat, rounded to whole points as the game rounds it. So Lore of Steel's 15% of a 15% `Health` set raises 12,375 `Health` to 12,653. Bonuses to stats the screen doesn't show, such as a set's ignore-defence, are left out. These sums are otherwise unrounded.
  - `total` adds them up unrounded. The screen rounds each Stat source before it adds them, so rounding `total` can miss the screen's number by a few points, one for every source rounded the other way. To match the screen, round by source as below.

  To show the stats as the champion screen does:
  - Round a Base stat's fraction to a whole percent. The points are whole already. That is the white number.
  - For the green number, round each Stat source to whole points or whole percents, a half going to the even one, then add them up. The screen counts `artifacts` and `sets` as one source, so add those two together before rounding.
  - The screen's total is the white number plus the green one.

## Names

A Champion's, a skill's, an Artifact set's, a Relic's and a Relic stone's name, and a Relic skill's `description`, is the text the Game client shows for it, in the language the player set: raid-bridge looks its key up in the client's loaded localization tables, and takes the default text the game gives the key when neither table holds it. If there is neither, the run fails rather than write a made-up name.

A Mission's `name` comes from those tables alone. The default text the game gives a Mission's key is a developer's note, such as "Have 5 heroes level 60", not what the screen shows, so a Mission whose key neither table holds fails the run instead.

A Relic's or a stone's skill has no name of its own in the game's data, only placeholder text such as "Skill 3000000 name", so it is written with the description the Relic's screen shows instead: the description of its Relic type, of the Faction's entry that adds it, or of its stone's type. The description belongs to that source, so if a source ever lists more than one skill, each carries the same text; in the game's data today each Relic type and each stone type lists one. The game holds a description with colour tags that only style it on screen, such as `<color=#1ee600>2%</color>` around a number shown in colour, and they are left out, so `"Wearer deals <color=#1ee600>2%</color> more damage"` is written `"Wearer deals 2% more damage"`.

## The Account id hash

`idHash` is `hmac-sha256:` followed by the lowercase hex of an HMAC-SHA256:

- the key is the player's salt, 32 bytes;
- the message is the Account id written as a decimal number with no sign, separator or padding, in UTF-8. The id `123456789` is the nine bytes of the text `123456789`.

The same Account always gets the same hash on the same machine, so anything reading the Snapshots can tell they belong together. Without the salt, nobody can work out the id from the hash or check whether a hash belongs to a given id.

## The salt

raid-bridge makes the salt on its first run: 32 random bytes in `$XDG_CONFIG_HOME/raid-bridge/salt`, or `~/.config/raid-bridge/salt` when `XDG_CONFIG_HOME` isn't set. The file is readable by the player only (mode 0600), and raid-bridge never prints or logs it.

Keep that file. If it is lost or deleted, the next run makes a new salt, and from then on every hash is different: the hashes no longer show that new Snapshots come from the same Account as the old ones. raid-bridge never replaces a salt file it finds, and refuses to run if the file isn't a salt. On Linux and macOS it also refuses to run if other users may read or change the salt: the file may grant nothing to its group or others, and its folder no write to them. Windows has no such modes, so there the check doesn't run. <!-- (PR #42) -->

## The file

By default a Snapshot is written to the current folder as `snapshot-<capturedAt>.json`, with the time in compact UTC form, such as `snapshot-20261031T214000Z.json`. `--out PATH` chooses the file instead. raid-bridge never overwrites a file: if the path exists, the run fails and writes nothing.
