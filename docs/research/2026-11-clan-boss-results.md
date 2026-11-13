# What the game keeps about Clan Boss battles (2026-11-13)

Findings for https://github.com/ANicholasson/raid-bridge/issues/34: whether the Game client holds Clan Boss battle results in a form a Snapshot could read. The ask was a run's date, boss and difficulty, keys used, total damage, the boss's turns, and each Champion's damage dealt and taken, healing and turn of death, so that one run can be compared with another.

The class and field names below come from the installed game's own files (`global-metadata.dat` and `GameAssembly.dll` at IL2CPP metadata v39, Unity 6000.3.18f1), parsed with the vendored LibCpp2IL. The "Live check" section then reads the running Game client on the maintainer's Linux install, through the Linux adapter, logged in to the main Account. It reads only the Account's own entries: other clan members' entries were counted, never read.

## The short answer

- **Today's Clan Boss, per difficulty: yes.** The client holds the Account's damage, keys spent and team for each difficulty of the current Clan Boss day, and for each Champion in that team its damage dealt, damage taken and healing.
- **Best single attack per difficulty: yes.** A personal best per difficulty, and the previous day's total on the Account's profile.
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

- `MaxSingleDamageByBossId`: `Dictionary<AllianceBossTypeId, long>`, the best single attack per difficulty. Only difficulties the Account has fought appear.
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

**Still open:** whether the Account's `HeroInfos` after two attacks is one team with summed figures, the last team, or both teams. Pending the maintainer's two Clan Boss fights.

## What a build ticket would need

A build is feasible for **today's Clan Boss**, not for history or a battle-by-battle breakdown. It would need:

1. The shape, through `/grill-with-docs`: a `clanBoss` section with the day's start and end, and per difficulty the Account's damage, keys spent, best single attack, and the team's Champions with damage dealt, damage taken and healing; how a Champion is named when only its type is known; and how to write an Account with no clan, or one that hasn't attacked today.
2. The reader: the routes above, the Account's own key only, `AttackInfoByUserId` null treated as "nobody attacked".
3. Glossary terms for the Clan Boss, its difficulties and the Clan Boss day.
4. A live check against the Clan Boss score screen after a fight.

The player can compare runs across days by keeping a Snapshot from each day. Turn of death, the boss's turns, and each battle on its own are out of reach with the manual Snapshot flow.
