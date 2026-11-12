# Where a Champion's stats come from (2026-11-10)

Findings for https://github.com/ANicholasson/raid-bridge/issues/9, part of the Total stats spec (https://github.com/ANicholasson/raid-bridge/issues/5) and ADR-0005.

Everything above the "Live check" section comes from the installed game's files: `global-metadata.dat` and `GameAssembly.dll` at IL2CPP metadata v39 and Unity 6000.3.18f1, dated 2026-11-06, parsed with the vendored LibCpp2IL. That part read no game memory and no Account, so it gives no field **values**. The "Live check" section then reads the running Game client and the maintainer's Account for the tables' contents, the multipliers and the Arena defence breakdown. The gated installed-files check pins the routes the stats work will start from: Base stats and `BattleStats`, the mastery, set, Faction Guardian, Empowerment and Awakening tables, and `HeroSlotSetup`'s setups, with `AcademySetup`'s pair of bonuses as the sample. The rest, such as the ascension fields and the other setups' bonus pairs, is from the dump only, and each build ticket pins what its reader takes. What is still open, all of it on the screen, is listed at the end.

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

`StaticAcademyData.Guardians.BonusesByHeroRarity` is a `Dictionary<HeroRarity, List<StatBonuses>>`. Each `StatBonuses` holds `Bonuses`, a `Dictionary<StatKindId, BonusValue>`. Entry _n_ is the bonus of the Faction's slot _n_ + 1 of that rarity, counted only while the slots are full: see "The Great Hall and Faction Guardians" below.

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

## Live check (2026-11-10)

A read-only probe read the running Game client on the maintainer's Linux install through the Linux adapter, with each field's offset taken from its runtime class as the reader does. Every route above resolved. `ArtifactSetInfo` and `ArtifactSubSetInfo` are in `SharedModel.Meta.Artifacts.Sets`.

### Units

`Fixed` values are raw Q32.32, as the reader already handles. Percentages are fractions (0.10 for 10%), with one exception: a set's IgnoreDefence is held as 15, not 0.15. `BattleStats.ToPercent` is 100.

### `BattleStats.Multipliers`

**The metadata declares it `double[][]`, but in memory it is a `double[6,2]`**, a two-dimensional array, which IL2CPP lays out differently from an array of arrays. The reader has to support that shape and check it against the declared type. Its rows, one per rank: [1, 2], [1.6, 1.9], [2.432, 1.8], [3.50208, 1.7], [4.7628288, 1.7], [6.477447168, 1.7]. The first values grow by 1.6, 1.52, 1.44, 1.36 and 1.36 from rank to rank. How a level within a rank applies them is settled in "How the game builds Base stats" below. <!-- (CodeRabbit, https://github.com/ANicholasson/raid-bridge/pull/18) -->

### The tables

- **Great Hall:** 6 stats with 10 levels each, entry _n_ − 1 for level _n_. HP, ATK and DEF give 2, 3, 4, 6, 8, 10, 12, 14, 17 and 20%. C.DMG gives 2% to 25%. ACC and RES give 5, 10, 15, 20, 30 and up to 80, flat.
- **Faction Guardians:** 5 bonuses per rarity, one per slot: HP 10%, ATK 10%, DEF 10%, ACC and RES flat, and SPD flat. The flat ones grow with rarity: ACC and RES +7, +15, +30 and +45, and SPD +3, +6, +10 and +15, for Rare, Epic, Legendary and Mythical.
- **Empowerment:** `Bonuses` and `EpicBonuses` hold 4 levels each. The levels add up: see "Awakening, blessing and Empowerment" below. For example, level 4 of `Bonuses` holds SPD +5, C.RATE 10% and C.DMG 30%, beside HP, ATK and DEF 10% and ACC and RES +10.
- **Awakening:** `BlessingStatsByRarity` has Stars1 to Stars6 for each rarity, and the grades add up. For Legendary they are HP +7500, ATK +750, DEF +600, HP +1000 with C.DMG 38%, ACC and RES +75, and SPD +15.
- **Masteries:** 13 entries in `MasteryBonusById`. One has `StatKindId` −1, which is no member of the enum, and is worth 15%. The constant `BonusSourceArtifactSet` is also −1, so that entry is the mastery that raises set bonuses (Lore of Steel). The reader has to treat −1 as that marker, not as a bad stat.
- **Sets:** 71. `_maxArtifactCount` is 0 on all of them. A set with one stat uses `StatBonus`, and a set with two uses `StatBonuses` and leaves `StatBonus` null. Speed is 2 pieces for 12%, and Accuracy is 2 pieces for +40. The 13 sets with `ArtifactCount` 0 have 9 `SubSetInfos`, one for each piece count from 1 to 9: these are the sets that grow by the piece.

### The Arena defence breakdown

The breakdown is filled. On the Account read, across 4 defence teams and 16 places:

- `BuildingSetup` (the Great Hall) is on all 16;
- `ArtifactSetups` is on 15, each Artifact with its own percent and flat bonus;
- `BlessingSetup` is on 5, and carries the Awakening bonus: a Stars5 Legendary showed flat HP +8500 (7500 + 1000), ATK +750, DEF +600, ACC +75 and RES +75, and C.DMG 38% as a percentage. One awakened Champion has none, which is still open;
- `AcademySetup` (Faction Guardians) is on 1;
- `RelicSetup` is on none, since no defence Champion wears a Relic;
- `ModifiersSetup` is null everywhere.

So the defence Champions give an exact oracle, source by source, for the Great Hall, Artifacts and Awakening, and for Faction Guardians on one Champion.

## How the game builds Base stats (settled 2026-11-10)

For https://github.com/ANicholasson/raid-bridge/issues/10. The installed v39 `GameAssembly.dll` was disassembled at the methods LibCpp2IL locates, read from the file on disk, never from the Game client. `BattleStats.BuildFromBase(HeroGrade grade, int level)` builds the white numbers from a form's `BaseStats`:

- `Health`, `Attack` and `Defence` each go through `BattleStats.CalcValue(baseStat, grade, level)`, which is `FixedMath.Round(baseStat * (Fixed)Multipliers[grade - 1, 0] * (Fixed)Math.Pow(Multipliers[grade - 1, 1], (level - 1.0) / (10 * grade - 1)), MidpointRounding.ToEven)`. So the first value of a rank's row is its start, the second its growth over the rank's levels, and the result is a whole number.
- `Health` is then multiplied by 15.
- `Speed`, `Resistance` and `Accuracy` are copied as they are.
- `CriticalChance`, `CriticalDamage`, `CriticalHeal` and `IgnoreDefence` are divided by 100. `BaseStats` holds them as whole percents, such as 15, and the result is a fraction.

`HeroType.DefaultBaseStats` is `Forms[0].BaseStats`. A Champion's level runs from 1 to 10 times its stars.

The arithmetic is `Plarium.Common.Numerics.Fixed`'s, in Q32.32:

- `(Fixed)double` truncates `value * 2^32`;
- a product is rounded down to a step;
- a quotient is computed to one bit past a step, plus one, then shifted down, so it rounds to the nearest step, a half step rounding up;
- `FixedMath.Round` with `ToEven` rounds a half to the even whole number.

So the base CriticalChance of 15 is held as 644245094 / 2^32, 0.14999999990686774.

Three numbers in the formula are the code's, not static data: the 15, the 10 levels per star, and the 100. The 100 is also `BattleStats.ToPercent`, a static the Game client holds, so the reader takes that. The maintainer agreed on 2026-11-10 to treat the 15 and the 10 as the formula's shape.

Two notes on the sections above. `BattleStats.Multipliers` isn't a table by level like the file Raid Toolkit ships: each row is a rank's start and growth, and the levels between come from the formula. And at v39 `HeroType` has no `AscendLevel` field, only a getter, so ascension is part of the type, as said.

With this formula, the reader gave the screen's sample below exactly: an Epic at rank 6 and level 60 with HP 18,990, ATK 749, DEF 1,288, SPD 102, C.RATE 15%, C.DMG 50%, RES 30 and ACC 0. It is Demytha, whose base is HP 115, ATK 68 and DEF 117.

For the sources still to come, `SharedModel.Meta.Heroes.Extensions.BattleStatsExtensions` has one method per source, each taking the Base stats: `CalcArtifactsBonus`, `CalcRelicsBonus`, `CalcBuildingsBonus` (the Great Hall), `CalcBlessingBonus`, `CalcArenaBonus` (by `ArenaLeagueId`), `CalcAcademyBonus` (Faction Guardians) and `CalcEmpowerBonus`. `BattleStats.UpdateWithBonuses` takes a percent and a flat part from each. They are the map for the later tickets, read the same way.

## How the champion screen adds its Stat sources (settled 2026-11-10)

For https://github.com/ANicholasson/raid-bridge/issues/11, read the same way from the installed v39 `GameAssembly.dll`.

### What the screen counts, and how it rounds

The champion screen's green numbers come from `Client.ViewModel.Utils.HeroExtensions.CalcBattleBonusStats`. It calls `SharedModel.Meta.Heroes.HeroExtensions.CalcHeroStatBonusesRounded` with:
- the Account's Great Hall (`VillageWrapperReadOnly.MakeHeroBuildingSetup`);
- its Faction Guardians (`AcademyWrapperReadOnly.Setup`);
- its Classic Arena league, but only when it has a defence team (`ArenaWrapperReadOnly.HasDefence`);
- the Champion's Artifacts and Relics.

`CalcHeroStatBonusesRounded` works out each Stat source on its own from the Base stats, through `BattleStatsExtensions`:
- the Artifacts and their sets, as one source;
- Relics;
- the Arena league;
- the Great Hall;
- masteries;
- the blessing;
- Faction Guardians;
- Empowerment.

It then adds the sources up with `OperationType.AddRoundValues`, which rounds each source to a whole number with `Math.Round`, a half going to the even one, before it adds it. `CriticalChance`, `CriticalDamage`, `CriticalHeal` and `IgnoreDefence` are first multiplied by `ToPercent`, so they round to whole percents.

So the screen counts the Arena league, the Great Hall, Faction Guardians and Empowerment. No clan bonus is among them.

Each source's bonus is `BattleStats.UpdateWithBonuses` of that source alone, less the Base stats:
- `Health`, `Attack`, `Defence`, `Speed`, `Resistance` and `Accuracy` use `CalcAbsoluteStats`: base × (1 + the source's percentages) + its flat bonuses.
- The critical stats use `CalcPercentageStats`: base + every bonus, whether flat or not.

### The Arena league

`StaticData.ArenaData.LeagueInfos`, each an `ArenaLeagueInfo` with `Id` and `BattleBonuses` (a `BattleStats` of percentages). From BronzeI to Platinum it gives 1, 2, 3, 4, 6, 8, 10, 12, 14, 16, 18, 20, 22 and 25% to `Health`, `Attack` and `Defence`. Spec https://github.com/ANicholasson/raid-bridge/issues/5 didn't name it as a Stat source, so it was added on 2026-11-11 as `arenaLeague`, with a ticket of its own (https://github.com/ANicholasson/raid-bridge/issues/20).

### Artifacts and sets

- `ArtifactSetup.FromArtifact` takes every bonus of an Artifact: its primary, its Substats and its ascension bonus. Each is the bonus's value, plus its `_powerUpValue`, plus its `_rarityBasedPowerUpValue` when it has one. So a Glyph can be held in two parts. A bonus goes to the percentages unless it is absolute or its stat is a relative one (`HeroStatKindIdExtensions.IsRelative`), such as the critical stats.
- `ArtifactExtensions.GetArtifactSetBonuses` counts the worn pieces of each set kind, then looks the set up in two places, `StaticArtifactData.GetSetInfo` and `GetSeriesSetInfo`:
  - For a set that grows by the piece, `GetSeriesSetInfo` finds the `ArtifactSubSetInfo` whose `ArtifactCount` equals the number worn. That entry's `StatBonuses` count once, beside the set's own empty ones.
  - Any other set gives its `StatBonus`, or its `StatBonuses` when it has no single one, once for each `ArtifactCount` pieces worn.
- `BattleBonuses.InitArtifact` puts a set's absolute bonuses with the flat bonuses and the rest with the percentages.

Live, on 2026-11-10, the defence teams' `ArtifactSetup`s held exactly those sums for the Artifacts worn, ascension bonuses included. With the formula, two screenshots matched every green number exactly once the other sources were added: Jizoh (a complete Attack set, the Great Hall, a Faction Guardian slot, SilverIV) and Vallaryn the Equalizer (no set, the Great Hall, SilverIV). For example, Vallaryn's HP is round(3,992.75) + round(10,695 × 12%) = 3,993 + 1,283 = 5,276. No Artifact on the Account had a Glyph then, so Glyphs are proven by the code and the tests only.

## Masteries (settled 2026-11-11)

For https://github.com/ANicholasson/raid-bridge/issues/12, read the same way. `HeroExtensions.CalcMasteriesBonus(hero, baseStats, artifactSetSetups)` works through `Hero.MasteryData.Masteries`. For each id it looks up `StaticMasteryData.MasteryBonusById`, and a mastery without an entry raises no stat. For an entry:
- If its `StatKindId` isn't `BonusSourceArtifactSet` (-1), the bonus's `Value` is added to that stat of a copy of the Base stats, as it is, whatever `IsAbsolute` says.
- If it is, `ChangeStatsWithArtifactSetMasteryBonus` multiplies each of the Champion's set bonuses by the mastery's `Value`. It sums the products by stat, flat or not. `Resistance`, `Accuracy` and the critical stats get the sum added as it is. Every other stat gets `OperationType.Scale`: the stat times (1 + sum), rounded to a whole number, a half going to the even one.

Each mastery's part is its copy less the Base stats, and the parts add up.

Live, on 2026-11-11, `MasteryBonusById` held 13 entries, all of them flat or critical fractions:
- Attack +75;
- Defence +75 and +200;
- Health +810 and +3,000;
- Resistance +10 and +50;
- Accuracy +10 and +50;
- CriticalChance +5%;
- CriticalDamage +10% and +20%;
- Lore of Steel (500343): 15% of set bonuses.

With the formula, two screenshots matched on 2026-11-11, once the Arena league, the Great Hall and the Artifacts and sets were added:
- Kael, with Accuracy, CriticalChance and CriticalDamage masteries: every green number.
- Uugo, with Lore of Steel and complete sets: every green number but HP, including Lore of Steel's +3 Resistance and +6 Accuracy. HP was 4,500 higher, from his Awakening, which no reader counts yet.

## The Great Hall and Faction Guardians (settled 2026-11-11)

For https://github.com/ANicholasson/raid-bridge/issues/13, read the same way.

- **Great Hall.** `VillageWrapperReadOnly.MakeHeroBuildingSetup(heroType)` calls `BuildingSetup.FromVillageData`. That takes the levels of the hero type's `DefaultElement`, its first form's Affinity, from `CapitolBonusLevelByStatByElement`. `BuildSetupByUserBonuses` then takes level _n_ of each stat as entry _n_ − 1 of `StaticVillageData.CapitolBonusByStatKind`, a `BonusValue` that goes to the flat bonuses if absolute and to the percentages if not. An observatory location adds a second table, but the champion screen passes none.
- **Faction Guardians.** `AcademyWrapperReadOnly.Setup(heroType)` calls `AcademySetup.Create(guardians, heroType)`:
  - It takes the slots of the hero type's own Faction and rarity (`UserAcademyGuardiansData.Slots`).
  - It counts them in order while `AcademyGuardiansSlot.IsFull()`, both positions filled.
  - For each slot counted, _n_ from 0, it adds `StaticAcademyGuardiansData.Bonuses(rarity, n)`, which is `BonusesByHeroRarity[rarity][n]`. That is a `StatBonuses` of `BonusValue`s by stat, split into flat and percentages the same way.
  - A Champion doesn't need to guard to get the bonus. With no static entry for a counted slot, the game itself would fail.

Both add as an Artifact's bonuses do. Live on 2026-11-11, the Snapshot gave Jizoh 813 Health from his Faction's one full Epic slot, and the Magic and Spirit Champions 20 Accuracy from level 4. Both are already in the green numbers that matched on the Jizoh, Vallaryn, Kael and Uugo screenshots above.

## Awakening, blessing and Empowerment (settled 2026-11-12)

For https://github.com/ANicholasson/raid-bridge/issues/14, read the same way.

- **Awakening.** `BattleBonuses.InitBlessing(HeroDoubleAscendData, rarity)` gives nothing unless the Champion has a blessing (`BlessingId` with a value). With one, it takes the blessing's `BlessingType.GradeBonuses` for every Awakening grade up to the Champion's. `DoubleAscendExtensions.GetStatBonusesBy` then:
  - reads each grade's `BlessingBonus.StatKindIds`;
  - looks up the `BlessingStats` of that grade in `BlessingStatsByRarity[rarity]`;
  - adds its `StatBonusByKindId` of each stat named, flat when absolute and as a percentage when not.

  So the blessing chooses which stats rise, and the rarity how much. An awakened Champion without a blessing gets nothing.
- **Empowerment.** `BattleBonuses.InitEmpower(level, heroType)` takes `AcademyData.Empower.EpicBonuses` for an Epic Champion, when the list exists, and `Bonuses` otherwise. It keeps the first `level` entries (`TakeWhile((entry, index) => index < level)`) and adds all of them. So the levels add up, which settles the question from the live check.

Live, on 2026-11-12, the Snapshot gave Uugo, an Epic at Stars1 with a blessing, 4,500 Health. That is exactly the gap left on his screenshot of 2026-11-11. Vallaryn the Equalizer, awakened to Stars4 with no blessing, got nothing, as her screenshot of 2026-11-10 showed. Dorothy Gale, a Legendary at Stars5, got Health +8,500, Attack +750, Defence +600, Resistance and Accuracy +75, and CriticalDamage +38%. Those are the values of the game's own `BlessingSetup` on her defence team, recorded above. No Champion on the Account was empowered, so Empowerment is proven by the code and the tests only.

## Still open (needs the maintainer's screen)

1. **Base stats:** settled above. For https://github.com/ANicholasson/raid-bridge/issues/10 the reader also matched the white numbers of three more Champions exactly on 2026-11-10: a Legendary at rank 5 and level 50 of an ascended type, an Epic at rank 4 and level 40, and a Rare at rank 3 and level 2.
2. **Empowerment:** settled above. Its levels add up.
3. **The missing blessing setup** on one awakened defence Champion: settled above. A Champion with no blessing gets no Awakening bonus.
4. **What the screen counts:** settled above, from the game's code. The Arena league, the Great Hall, Faction Guardians and Empowerment count, and no clan bonus does.
5. **How the screen rounds** each stat: settled above. Each Stat source rounds to a whole number or a whole percent, a half to the even one, before they are added.
