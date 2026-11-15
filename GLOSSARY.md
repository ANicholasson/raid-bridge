# raid-bridge

Reads a player's own RAID: Shadow Legends account out of the game client and writes it as JSON Snapshots. <!-- (https://github.com/ANicholasson/raid-bridge/pull/3) -->

## Language

**Account**:
One player's RAID: Shadow Legends account, as the game client holds it.
_Avoid_: profile, user

**Power**:
The Account's total power, the one number the game shows beside its level. The game's code calls it total power. <!-- (https://github.com/ANicholasson/raid-bridge/issues/27) -->

**Snapshot**:
Everything raid-bridge read from one Account at one moment, as one JSON document.
_Avoid_: dump, export, sync

**Champion**:
A collectible fighter on an Account. The game's code calls it a hero; anything a player reads says Champion.
_Avoid_: hero (outside game type names), unit

**Artifact**:
A piece of equipment a Champion wears, accessories included.
_Avoid_: gear, item

**Game client**:
The RAID: Shadow Legends program running on the player's computer.
_Avoid_: the game (when the program is meant), Plarium Play (that is the launcher)

**Rank**:
A Champion's star count, one to six. The game's code calls it a grade.
_Avoid_: grade, stars (in code)

**Affinity**:
A Champion's element: Magic, Force, Spirit or Void. The game's code calls it an element.
_Avoid_: element

**Faction**:
The group a Champion belongs to, such as Banner Lords or Dark Elves. The game's code calls it a fraction.
_Avoid_: fraction, race

**Awakening**:
A Champion's second progression, raised with Souls and capped by how far the Champion has ascended. It is separate from rank. The game's code calls it double ascend. <!-- (PR #17) -->
_Avoid_: double ascend

**Blessing**:
A power the player gives an awakened Champion, such as Necromancy. The game's code keeps it with the Champion's double ascend data.
_Avoid_: buff

**Empowerment**:
Further power a Champion gains, by level, 0 for none. The game's code calls it the empower level. <!-- No rank named: (https://github.com/ANicholasson/raid-bridge/pull/6) -->
_Avoid_: empower level

**Mastery**:
One of the bonuses a Champion learns from its mastery trees, each kept by its id.
_Avoid_: talent, perk

**Roster**:
The Champions on an Account that are in neither the Vault nor the Deep Vault: the ones the player uses.
_Avoid_: inventory

**Vault**:
Storage for Champions that are on the Account but not in the player's main roster. The game's code marks them as in storage.
_Avoid_: storage

**Deep Vault**:
A second, larger Champion store. The game's code calls it the bathhouse.
_Avoid_: bathhouse

**Substat**:
One of an Artifact's secondary bonuses, beside its main stat.
_Avoid_: secondary bonus, sub-stat

**Set**:
Artifacts of one kind that give a bonus when a Champion wears enough of them together, such as a Speed set. Most count once for each complete set worn; some grow by the piece, giving more for each piece worn.
_Avoid_: set bonus (for the set itself)

**Glyph**:
An extra value that the player adds onto an Artifact's substat. The game's code calls it a power-up.
_Avoid_: power-up

**Unworn Artifact**:
An Artifact on the Account that no Champion wears.
_Avoid_: vault Artifact (the Vault holds Champions), spare, inventory

**Relic**:
A piece of equipment a Champion wears beside its Artifacts, with bonuses of its own. The materials it is crafted from are Resources, and so are the Starstones that level it up; the Basalt that raises its Relic rank is Items.
_Avoid_: Artifact (a Relic isn't one), relic item

**Relic rank**:
A Relic's own rank, one to six, which caps how far it can be levelled. It is not a Champion's Rank.
_Avoid_: rank (alone, for a Relic), stars

**Starstone**:
What a Relic is levelled up with. The game's code keeps Starstones among the Resources as meteor.
_Avoid_: meteor (outside the game's code names)

**Basalt**:
What raises a Relic's Relic rank by one: Rank 1 Basalt takes a Relic of Relic rank 1 to Relic rank 2, and so on to Rank 5 Basalt. The game's code keeps it among the Items as carbon, one per Relic rank.
_Avoid_: carbon (outside the game's code names)

**Unworn Relic**:
A Relic on the Account that no Champion wears.
_Avoid_: spare Relic, inventory

**Relic stone**:
A stone the Account owns, with a shape. It goes in a Relic's socket of the same shape, or sits in no socket. It adds skills, not stats. <!-- (https://github.com/ANicholasson/raid-bridge/pull/16) -->
_Avoid_: gem, socket (the socket is the slot, not the stone)

**Resource**:
A currency or counter on the Account, such as silver, gems, energy, keys or tokens. The game's code keeps them as the account's resources.
_Avoid_: currency (for the whole set), wallet

**Shard**:
An item that summons a Champion, by type: Mystery, Ancient, Void, Sacred or Primal.
_Avoid_: summon, ticket

**Item**:
A consumable the Account holds a count of, such as a potion or a tome. The game's code calls it a black market item.
_Avoid_: black market item, consumable (in code)

**Great Hall**:
Account-wide stat bonuses for every Champion of one Affinity, bought by level for each stat. The game's code calls it the village's capitol bonus.
_Avoid_: village, capitol

**Area Bonus**:
A stat bonus the Great Hall gives every Champion while it fights in one area, such as a Dungeon or Demon Lord battles, bought by level for each stat with Crests. The game's code calls it the observatory bonus.
_Avoid_: observatory, location bonus

**Crest**:
A Live Arena medal, Bronze, Silver or Gold, won in Live Arena and spent on Area Bonuses. The game's code keeps Crests among the Resources as live arena medals.
_Avoid_: medal (when Classic Arena medals could be meant)

**Faction Guardian**:
A Champion placed in a Faction's guardian slot, which gives stat bonuses to every Champion of that Faction. The game's code keeps them in the academy.
_Avoid_: academy

**Classic Arena**:
The Arena where one team of up to four Champions fights another player's defence team. The game's code calls it the arena.
_Avoid_: arena (when Tag Arena could be meant)

**Tag Arena**:
The Arena where three teams of up to four Champions each face another player's three teams, in three battles. The game's code calls it the 3x3 arena. <!-- (PR #40) -->
_Avoid_: 3v3, arena3x3

**Live Arena**:
The Arena where two players pick Champions in turn and fight each other in real time.
_Avoid_: PvP (for Live Arena alone), live arena (in code names other than the game's own)

**Mission**:
A task on the game's Missions screen, such as Arbiter's, with a reward for finishing it. Missions come in groups the Account works through one after another, and each group is in a section of the screen, such as Arbiter's. The game's code calls a Mission a recommended quest; its other quests, such as daily quests, aren't Missions.
_Avoid_: recommended quest, quest (for a Mission)

**Base stats**:
A Champion's own stats at its rank, level and ascension, before any bonus: the white numbers on the champion screen. Two Champions of one type, rank and level can differ by ascension. <!-- (https://github.com/ANicholasson/raid-bridge/pull/6) -->
_Avoid_: raw stats, unscaled stats

**Stat source**:
One thing that adds to a Champion's stats beyond its Base stats, such as its Artifacts, its set bonuses, its masteries or the Great Hall. A Snapshot's `bySource` holds the Base stats under `base`, beside the Stat sources, so a Champion's stats are the sum of its parts.
_Avoid_: bonus (alone, since an Artifact's bonuses are one source)

**Total stats**:
A Champion's eight stats as the champion screen shows them: its Base stats plus every Stat source the screen counts. A bonus that applies only in battle, such as a leader skill's or a buff's, isn't part of them. A Snapshot writes them unrounded; the screen rounds each Stat source before it adds them. <!-- The Arena league isn't named either way: research settles it (https://github.com/ANicholasson/raid-bridge/pull/6). It settled that the screen counts it, while the Account has a Classic Arena defence team (https://github.com/ANicholasson/raid-bridge/issues/20). -->
_Avoid_: effective stats, battle stats
