# raid-bridge

Reads a player's own RAID: Shadow Legends account out of the game client and exports it for an AI companion that helps them understand it.

## Language

**Account**:
One player's RAID: Shadow Legends account, as the game client holds it.
_Avoid_: profile, user

**Snapshot**:
Everything raid-bridge read from one Account at one moment, as one JSON document.
_Avoid_: dump, export, sync

**Champion**:
A collectible fighter on an Account. The game's code calls it a hero; anything a player reads says Champion.
_Avoid_: hero (outside game type names), unit

**Artifact**:
A piece of equipment a Champion wears, accessories included.
_Avoid_: gear, item

**Companion**:
The AI agent that reads Snapshots to help the player understand their Account. It never plays.
_Avoid_: bot, assistant

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
A Champion's second progression, raised with Souls and capped by how far the Champion has ascended. It is separate from rank. The game's code calls it double ascend. <!-- (Codex, PR #17) -->
_Avoid_: double ascend

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

**Glyph**:
An extra value that the player adds onto an Artifact's substat. The game's code calls it a power-up.
_Avoid_: power-up

**Unworn Artifact**:
An Artifact on the Account that no Champion wears.
_Avoid_: vault Artifact (the Vault holds Champions), spare, inventory

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

**Faction Guardian**:
A Champion placed in a Faction's guardian slot, which gives stat bonuses to every Champion of that Faction. The game's code keeps them in the academy.
_Avoid_: academy

**Classic Arena**:
The Arena where one team of up to four Champions fights another player's defence team. The game's code calls it the arena.
_Avoid_: arena (when Tag Arena could be meant)

**Tag Arena**:
The Arena where three teams of up to four Champions each face another player's three teams, in three battles. The game's code calls it the 3x3 arena. <!-- (CodeRabbit, PR #40) -->
_Avoid_: 3v3, arena3x3
