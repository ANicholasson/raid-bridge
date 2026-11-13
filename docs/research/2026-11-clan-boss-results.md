# What the game keeps about Clan Boss battles (2026-11-13)

Findings for https://github.com/ANicholasson/raid-bridge/issues/34: whether the Game client holds Clan Boss battle results in a form a Snapshot could read. The ask was a run's date, boss and difficulty, keys used, total damage, the boss's turns, and each Champion's damage dealt and taken, healing and turn of death, so that one run can be compared with another.

The class and field names below come from the installed game's own files (`global-metadata.dat` and `GameAssembly.dll` at IL2CPP metadata v39, Unity 6000.3.18f1), parsed with the vendored LibCpp2IL. The "Live check" section then reads the running Game client on the maintainer's Linux install, through the Linux adapter, logged in to the main Account. It reads the Account's own entries. Of other clan members' entries it read only their damage, summed, to check it against the boss's total.

## The short answer

- **Today's Clan Boss, per difficulty: partly.** The client holds the Account's total damage and keys spent on each difficulty of the current Clan Boss day, and the **last** team used there, as the game's Clan Boss score list shows it. Not each battle's damage, and not the team behind the best one.
- **Each Champion's damage dealt, damage taken and healing: no.** The fields exist, but they hold 0 for Clan Boss, and the score list doesn't show them either (live check).
- **Best single attack per difficulty: not dependably.** A personal best per difficulty exists, but it didn't change after a day's fights (live check). The Account's profile holds the previous day's total.
- **History of past days: no.** Each new Clan Boss day replaces the previous one. A Snapshot taken after each day's fights is the only history, kept by the player.
- **Each battle on its own, the boss's turns, turn of death, battle time: no**, except while that battle's result screen is open. The full battle record lives only as long as the battle and its result screen, and in a small on-disk queue until the server accepts the result.

## Today's Clan Boss

The clan's Clan Boss state is under the Account's root like every other route, through the clan wrapper:

- `Client.Model.AppModel._userWrapper.Alliance` is a `Client.Model.Gameplay.Alliance.AllianceWrapper`.
- Its `<Bosses>k__BackingField` (declared on `AllianceWrapperReadonly`) is an `AllianceBossesWrapperReadOnly`, whose `Alliance` is a `Client.Model.Gameplay.Alliance.Data.Alliance`.
- `Alliance.GameData` (`UpdatableAllianceGameData` : `SharedModel.Meta.Alliances.AllianceGameData`) `.BossData` is a `SharedModel.Meta.Alliances.Boss.AllianceBossData`:

| Field | Type | Notes |
| --- | --- | --- |
| `StartTime` | `DateTime` | When the current Clan Boss day began |
| `NextRefreshTime` | `Nullable<DateTime>` | When it ends |
| `MaxUnlockedId` | `AllianceBossTypeId` | The clan's highest difficulty |
| `Bosses` | `List<AllianceBoss>` | One per difficulty |
| `BossRewardInfoByUserId` | `Dictionary<long, UserAllianceBossRewardInfo>` | Chests by member |

`AllianceBossTypeId` names `Easy`, `Normal`, `Hard`, `Brutal`, `Nightmare` and `UltraNightmare`.

`SharedModel.Meta.Alliances.Boss.AllianceBoss`, one difficulty:

| Field | Type | Notes |
| --- | --- | --- |
| `Id` | `AllianceBossTypeId` | The difficulty |
| `HealthLeft` | `int` | |
| `DamageTaken` | `long` | The whole clan's damage today |
| `Element` | `Element` | The boss's Affinity today |
| `Revision` | `long` | The Clan Boss day's number, the same on every difficulty |
| `AttackInfoByUserId` | `Dictionary<long, AllianceAttackShortInfo>` | Each member's attacks today, by Account id; null while nobody has attacked |

`SharedModel.Meta.Alliances.AllianceAttackShortInfo`, one member's attacks on one difficulty today:

| Field | Type |
| --- | --- |
| `GivenDamage` | `long` |
| `SpentKeys` | `int` |
| `TotalPower` | `int` |
| `HeroInfos` | `List<ShortHeroInfo>` |
| `TurnsBeforeCompleteByChallengeId` | `Dictionary<int, int>` (shared with the Hydra and Chimera; Clan Boss has no challenges) |

`SharedModel.Meta.Heroes.ShortHeroInfo`, one Champion in that team:

| Field | Type |
| --- | --- |
| `HeroTypeId` | `int` |
| `Grade` | `HeroGrade` (stars) |
| `Level` | `int` |
| `EmpowerLevel` | `int` |
| `DoubleAscendData` | `ShortDoubleAscendData` (Awakening grade and blessing) |
| `EquippedSets` | `List<ArtifactSetKindId>` |
| `DamageDealt` | `long` |
| `DamageTaken` | `long` |
| `RestoredHp` | `long` (healing) |

`ShortHeroInfo` names the Champion's type, not the Account's Champion id, so the Snapshot could only match it to a Champion by type, which is ambiguous when the Account holds two of one type.

This is the clan's scoreboard, the one the game shows under the Clan Boss's scores, and the server sends it with the rest of the clan's data (`GetAllianceCmd`, refreshed on a timer). It holds every member's entry, so a reader takes only the Account's own key and must not write the others: their Account ids, teams and damage aren't the player's data.

## The Account's own Clan Boss records

`_userWrapper.Alliance.UserAllianceData` (`UpdatableUserAllianceData` : `SharedModel.Meta.Alliances.UserAllianceData`) holds the Account's side:

- `MaxSingleDamageByBossId`: `Dictionary<AllianceBossTypeId, long>`, by its name the best single attack per difficulty. It held 2 difficulties, and didn't gain the one fought during the live check.
- `CurrentBossAttack`: a `UserBossAttackInfo` (`Id`, `StartTime`, `InitialHp`), the last attack the Account started.
- `ReceivedRewards` and `RewardInfo`: the chests per difficulty, by damage range.

`_userWrapper.Account.AccountData` (`SharedModel.Meta.Account.UserAccount`) `.Activities.AllianceBossData` is a `SharedModel.Meta.Account.Profile.AllianceBossActivitiesData`, the Clan Boss line on the Account's profile: `BossRevision`, `CurrentRevisionTotalDamage`, `TypeId` (a difficulty) and `MaxDamage`.

## Each battle: only while it lasts

A battle's full result is a `SharedModel.Battle.Core.Result.BattleResult`. Its `Statistics.StatisticsByHero` holds a `HeroStatistics` for every Champion and the boss, and each of those keeps, per round, every turn's damage, healing and effects (`TurnStatistics`), deaths (`HeroDeathStatistics`) and a `TurnsCount`. Everything issue #34 asks for is there, turn of death and the boss's turns included.

Nothing in the client keeps it after the battle:

- **On screen.** `BattleFinishDialogState` holds the `BattleResult`, `GivenDamageToAllianceBoss` and `Turns` while the result screen is open. The screen's per-Champion bars (`HeroBattleStatsContext`) are built from it. Closing the screen drops it. Reading it would mean taking a Snapshot with that screen open, which the manual flow doesn't do, and only the last battle would be there.
- **On disk.** `AppModel.<BattleResultsCache>k__BackingField` is a `MessagePackBattleResultsCache` (`Load`, `Save`, `Add`, `Delete`, `Clear`). It writes `battle-results/battleResults` in the game's `LocalLow` folder, and the sign-in request carries its contents (`SignInDto.CachedBattleResults`). It is a queue of results the server hasn't accepted yet, so a crash doesn't lose a battle, not a history. On the maintainer's install, with the game running, the file was 11 bytes: an LZ4-compressed MessagePack empty list.
- **Elsewhere.** No class in the model keeps a list of past battles. `ClearLastBattleDataCmd` sends a battle id to the server for auto battles. The local database `raidV2.db` holds an Account id key, analytics events and migrations, nothing about battles.

The server's own history isn't reachable: raid-bridge never talks to it (ADR-0001, no network).

## Live check (2026-11-13)

A read-only probe read the running Game client on the main Account, through the Linux adapter, with each field's offset taken from its runtime class as the reader does. Every route above resolved.

- `BossData` had 6 bosses, one per difficulty, all with the same `Revision` and `Element`. `StartTime` and `NextRefreshTime` were 24 hours apart, less a few minutes.
- `AttackInfoByUserId` was null on the two difficulties nobody had attacked. On the others, the members' `GivenDamage` added up to the boss's `DamageTaken` exactly, so each entry is one member's whole day on that difficulty, not one battle.
- The Account hadn't attacked the new day's boss yet, so it had no entry in any `AttackInfoByUserId`.
- `MaxSingleDamageByBossId` held 2 difficulties. `Activities.AllianceBossData` named the previous day's revision (one less than the bosses'), with that day's total and the best on the profile.

**After two fights.** The maintainer then fought Hard twice with two different teams, the stronger team first and a weaker one second. Read again:

- The Account's entry on Hard held `GivenDamage` 41,498,308 and `SpentKeys` 2: both battles, added up. Hard's `DamageTaken` went up by the same amount.
- `HeroInfos` held 5 Champions: the **second** team only, with their stars, level and Awakening. `TotalPower` was that team's power. The stronger first team was gone.
- Every `ShortHeroInfo.DamageDealt`, `DamageTaken` and `RestoredHp` was 0, and every `EquippedSets` was empty.
- The game's Clan Boss score list (clan screen, on the maintainer's screenshot) showed the same thing: 41.49M with 2 keys, the second team, team power 105.76K, and no per-Champion figures. Reading again after opening that list (which also refreshed another difficulty's figures) changed nothing on the Account's entry.
- `MaxSingleDamageByBossId` still held only Brutal and Nightmare, with no entry for Hard. `Activities.AllianceBossData` still named the previous day. `CurrentBossAttack.StartTime` was the second battle's start.
- Only Hard, the difficulty fought, changed its `Element` (to Force); the others still said Void. Which is right isn't settled: the unfought difficulties may lag behind until the next clan refresh.

## What a build ticket would need

A build is feasible but small: **today's Clan Boss totals**. That's per difficulty the Account's damage and keys spent for the day, and the last team used, as the score list shows. Neither a run-by-run comparison nor per-Champion figures are possible. It would need:

1. The shape: a `clanBoss` section with the day's start and end, and per difficulty the Account's damage, keys spent, last team and the boss's Affinity (`AllianceBoss.Element`); whether to write `MaxSingleDamageByBossId` at all, given it didn't move; how a Champion is named when only its type is known; and how to write an Account with no clan, or one that hasn't attacked today.
2. The reader: the routes above, the Account's own key only, `AttackInfoByUserId` null treated as "nobody attacked".
3. Glossary terms for the Clan Boss, its difficulties and the Clan Boss day.
4. A live check against the Clan Boss score list after a fight, including the Affinity: on the live read, Hard showed Force after the fights and the other difficulties Void, and so did the score list's icons. Whether the difficulties really differ, or the unfought ones were stale, has to be settled first.

The player can compare days by keeping a Snapshot from each day. Within a day, a run's damage and team can be had by taking a Snapshot right after each key and subtracting the total in the Snapshot before it. Each Champion's damage, turn of death and the boss's turns are out of reach with the manual Snapshot flow. Per-Champion figures stay with the result screen's screenshot.

**Decision (2026-11-13):** no separate ticket for now. The maintainer's priority is missions, then Area Bonuses and crests, then Starstones and Basalt, then this small Clan Boss version (day total, keys, last team and Affinity per difficulty) if it's cheap.
