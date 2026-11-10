# Where a Champion's stats come from (2026-11-10)

Findings for https://github.com/ANicholasson/raid-bridge/issues/9, part of the Total stats spec (https://github.com/ANicholasson/raid-bridge/issues/5) and ADR-0005.

Everything here comes from the installed game's files: `global-metadata.dat` and `GameAssembly.dll` at IL2CPP metadata v39 and Unity 6000.3.18f1, dated 2026-11-06, parsed with the vendored LibCpp2IL. No game memory and no Account were read, so field **values** (the tables' contents, the multipliers) are still unseen. The gated installed-files check pins the routes the stats work will start from: Base stats and `BattleStats`, the mastery, set, Faction Guardian, Empowerment and Awakening tables, and `HeroSlotSetup`'s setups, with `AcademySetup`'s pair of bonuses as the sample. The rest, such as the ascension fields and the other setups' bonus pairs, is from the dump only, and each build ticket pins what its reader takes. The ticket's live and on-screen parts are listed at the end, still open.

## Does the client keep Total stats for every Champion?

No. `SharedModel.Meta.Heroes.Hero` declares these fields:

- `_type`, `Id` and `TypeId`;
- `Grade` and `Level`;
- `Experience` and `FullExperience`;
- `EmpowerLevel`;
- `Locked`, `InStorage`, `InBathhouse` and `Marker`;
- `RecentBattleTime` and `RecentLiveArenaBattleTime`;
- `Skills`, `MasteryData` and `DoubleAscendData`;
- `Power` (`Nullable<double>`);
- `KnownHeroRevision`, `ClaimDate` and `HeroTypeVersionReceived`.

`Power` is the only computed number, and it isn't a stat. No other user-data class found holds per-Champion stats. ADR-0005 stands: raid-bridge computes Total stats.

### The game's own breakdown, in battle setups

`SharedModel.Battle.Core.Setup.HeroSlotSetup` is the setup of one Champion in one team. It carries the game's own per-source bonuses, each as a `PercentBonus` and a `FlatBonus` (both `BattleStats`):

| Field | Holds |
| --- | --- |
| `BuildingSetup` | The Great Hall |
| `AcademySetup` | Faction Guardians, and probably Empowerment |
| `BlessingSetup` | The blessing, with its `BlessingId` and `Skill` |
| `RelicSetup` | The Relics, with their skills |
| `ArtifactSetups[]` | Each Artifact, with its `AscendBonus` |
| `SiegeBonusSetups[]` | Siege, which is battle-only |

It also carries:

- `ArtifactSetSetups`, the sets;
- `MasteryIds`;
- `Grade`, `Level`, `DoubleAscendGrade` and `EmpowerLevel`;
- `ModifiersSetup` (`BattleStatsModifierSetup`), where stage and battle modifiers go;
- `Power`.

The Classic and Tag Arena defence teams that the Snapshot already reads (`TeamSetup.HeroSlotSetups`) are made of this class. If the client fills these bonus fields for the defence teams, the Arena defence Champions carry the game's own breakdown in memory. That would be an exact oracle for the formula, though it covers only those Champions, not every one. Whether the fields are filled is a live check.

## The static-data route to each Stat source

Every route below starts at `ClientStaticData` (the fields are declared on its base class, `SharedModel.Meta.StaticData`), which the reader already reaches for names. Bonuses come in two shapes:

- `StatBonus` (`SharedModel.Meta.General`): `StatKindId`, `Value` (`Fixed`) and `IsAbsolute`;
- `BonusValue` (`SharedModel.Meta.Artifacts`): `_value` (`Fixed`) and `_isAbsolute`, keyed by a `StatKindId` outside it.

### Base stats

- A Champion type's forms carry its Base stats: `HeroType.Forms[i].BaseStats`, a `SharedModel.Meta.Heroes.BattleStats`.
- `BattleStats` has ten `Fixed` fields: `Health`, `Attack`, `Defence`, `Speed`, `Resistance`, `Accuracy`, `CriticalChance`, `CriticalDamage`, `CriticalHeal` and `IgnoreDefence`.
- **Rank and level.** `BattleStats` has a static `Multipliers` (`double[][]`) and a static `ToPercent` (`Fixed`). `Multipliers` is very likely the rank-by-level scaling that Raid Toolkit ships as a JSON file. It is a static field of the game's own class, so it is in the Game client's memory and can be read, with no table hard-coded (ADR-0005). The live check confirms its shape.
- **Ascension.** An ascended Champion is a different `HeroType`: `HeroType` has `AscendLevel`, `UnAscendedHeroId`, `BaseId` and `_maxAscendedType`. So a Champion's `TypeId` already names its ascended type, whose forms carry that ascension's Base stats. This is why the Snapshot writes no ascension field.
- **Awakening.** `StaticDoubleAscendData.BlessingStatsByRarity` is a `Dictionary<HeroRarity, List<BlessingStats>>`. Each `BlessingStats` has a `Grade` (`DoubleAscendGrade`) and a `StatBonusByKindId` (`Dictionary<StatKindId, StatBonus>`). Despite the name, this is keyed by the Champion's rarity and Awakening level, not by blessing. So it looks like the Awakening bonus every awakened Champion gets.

### Artifacts and sets

- The Artifacts' own bonuses are what the Snapshot already writes. `ArtifactSetup` in battle carries `AscendBonus` separately from the primary and Substats, which confirms that ascension is a bonus of its own.
- The set bonuses are in `StaticArtifactData.SetInfos` (`List<ArtifactSetInfo>`), already read for names. Each `ArtifactSetInfo` has:
  - `ArtifactCount`: the pieces for one set;
  - `StatBonus` and `StatBonuses`: a `StatBonus` and a `List<StatBonus>`;
  - `SkillBonus`;
  - `SubSetInfos` (`List<ArtifactSubSetInfo>`) and `_maxArtifactCount`: probably the sets that grow by the piece.

### Masteries

- `StaticMasteryData.MasteryBonusById` is a `Dictionary<int, StatBonus>`, keyed by the same ids as `Hero.MasteryData.Masteries`.
- `StaticMasteryData` also has a constant `BonusSourceArtifactSet` (an `int`). It is probably the mastery id or the bonus source of the mastery that raises set bonuses (Lore of Steel), and the live check settles it.
- `MasteryById` (`Dictionary<int, MasteryType>`) holds the rest of each mastery.

### Great Hall

`StaticVillageData.CapitolBonusByStatKind` (`Dictionary<StatKindId, List<BonusValue>>`) is already read to check levels. Level _n_ is entry _n_ − 1.

### Faction Guardians

`StaticAcademyData.Guardians.BonusesByHeroRarity` is a `Dictionary<HeroRarity, List<StatBonuses>>`. Each `StatBonuses` holds `Bonuses`, a `Dictionary<StatKindId, BonusValue>`. The list is presumably indexed by the number of filled slots or by slot. The live check settles it.

### Blessings

`StaticDoubleAscendData.BlessingById` (`Dictionary<BlessingTypeId, BlessingType>`). Each `BlessingType` has `GradeBonuses`, a `Dictionary<DoubleAscendGrade, BlessingBonus>`, beside its `Name` and `Description`. `BlessingBonus` wasn't dumped. Whether a blessing adds stats, or only a skill, is open. Battle's `BlessingSetup` has stat bonuses, which suggests some blessings add stats.

### Empowerment

`StaticAcademyData.Empower` holds `Bonuses` and `EpicBonuses`, each a `List<StatBonuses>`. The list is presumably indexed by Empowerment level, with one table for Legendary and Mythical and another for Epic.

### Relics

`StaticRelicData.StatValueByLevel` (`Dictionary<StatKindId, List<BonusValue>>`), applied to the stats a Relic's type raises. See `2026-11-relics.md`.

### StatKindId

`Health` = 1, `Attack`, `Defence`, `Speed`, `Resistance`, `Accuracy`, `CriticalChance`, `CriticalDamage`, `CriticalHeal`, `IgnoreDefence` = 10, then `DealtDamagePve`, `DealtDamagePvp`, `DealtDamageBoss` and `DealtDamageDungeons` (11 to 14), and `TakenDamagePve`, `TakenDamagePvp`, `TakenDamageBoss` and `TakenDamageDungeons` (15 to 18). There is no 0 member. The Total stats are the first eight.

## The Arena league and clan bonuses

No user or static field found in this dump names a league or clan stat bonus for a Champion. In battle setups, stage and battle modifiers go through `HeroSlotSetup.ModifiersSetup`. Whether the champion screen counts the Arena league or clan bonuses can only be settled on the screen.

## Still open (needs the running Game client and the maintainer's screen)

1. **The table values and shapes:** `BattleStats.Multipliers`, how a level or a count indexes each list, and whether each source is flat or a percentage.
2. **The Arena defence breakdown:** are `HeroSlotSetup`'s `BuildingSetup`, `AcademySetup`, `BlessingSetup`, `RelicSetup` and `ArtifactSetups` filled for the defence-team Champions? If so, they check the formula source by source.
3. **What the screen counts:** whether the champion screen counts the Arena league, clan bonuses, the Great Hall, Faction Guardians and Empowerment.
4. **How the screen rounds** each stat.

Items 1 and 2 need a read of the running Game client's memory. Items 3 and 4 need the maintainer at the champion screen.
