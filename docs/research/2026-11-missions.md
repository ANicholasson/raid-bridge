# Where the game keeps missions and their progress (2026-11-13)

Findings for https://github.com/ANicholasson/raid-bridge/issues/33: where the Game client keeps Arbiter's missions and the Account's other quests, where a mission's text comes from, and what a build ticket would need.

Sources: the installed game's own files, `global-metadata.dat` and `GameAssembly.dll` at IL2CPP metadata v39 and Unity 6000.3.18f1, parsed with the vendored LibCpp2IL, and a disassembly of the methods that turn a quest into the progress the screen shows. Then a read-only probe of the running Game client through the Linux adapter, which confirmed each route and showed the values below. Nothing here is pinned by the gated installed-files check yet. One screenshot of the Missions screen was compared against them ("Confirmed on the screen" below).

## Words

The game's code says **quest** for every task the player can complete: daily, weekly and monthly quests, Advanced quests, Challenges, Achievements, the Champion pass, event and tournament quests, and the **Missions** screen. The Missions screen is what the code calls **recommended quests** (`RecommendedQuest*`; its view model is `Client.ViewModel.Contextes.RecommendedQuests`, and it shows the reward Champion through `MissionsHeroContext`). This note says "mission" for a recommended quest and "quest" for the general case.

## Arbiter's missions are the first section of the Missions screen

`SharedModel.Meta.Quests.RecommendedQuestSectionId` names three sections: `ArbiterSection` = 1, `RamantuSection` = 2 and `Missions3Section` = 3. The screen, titled "Progress Missions", shows them as tabs: Arbiter Missions, Ramantu Missions and Marius Missions. Each section is a run of groups (`RecommendedQuestGroupId`, `Group1` to `Group10`, with `Undefined` = 0). The static data says which:

- `ClientStaticData.QuestData` (declared on the base `SharedModel.Meta.StaticData`) is a `StaticQuestData`. Its `RecommendedQuestData` is a `RecommendedQuestData`:
  - `GroupsBySection`: `Dictionary<RecommendedQuestSectionId, List<RecommendedQuestGroupId>>`. Live: Arbiter is `Group1` to `Group4`, Ramantu `Group5` to `Group7`, the third section `Group8` to `Group10`.
  - `GroupInfos`: `List<RecommendedQuestGroupInfo>`, each a `GroupId` and a `Reward` (`UserPrize`). Live: 10, one per group.
  - `FirstGroupBySection` and `AvailableOpenSectionIdByPrevClosedSections`: which group opens a section, and which sections open after others close.

Each mission also names its own category, `Quest.RecommendedQuestCategoryId` (`Dungeons` = 1, `Campaign`, `Arena`, `Artifacts`, `Heroes`, `Clan`, `Arena3X3`, `DoomTower`, `Fraction`, `Forge`, `Tournament`, `Event`, `CursedCity`, `LiveArena`, `AltarOfSouls`, `Hydra` = 16, `Banner` = 20). Its group is `Quest.RecommendedQuestGroupId`. The screen draws the category as an icon, and its name is the client text `l10n:recommended-quest/recommended-quest-category?id={0}#label`.

## The route to the Account's quests

Like every other route, it starts from `Client.Model.AppModel._userWrapper` (a `Client.Model.Guard.UserWrapper`):

- `UserWrapper.Quests` is a `Client.Model.Gameplay.Quests.QuestWrapper`. On its base class `QuestWrapperReadOnly`:
  - `Quests`: `Dictionary<int, SharedModel.Meta.Quests.Data.Quest>`, **what each quest asks**, keyed by the quest's id on the Account. The server sends these at sign-in (`SignInResult.Quests`, `UserResponseDto.Quests`, both `List<Quest>`). They are not in the static data.
  - `QuestData`: `SharedModel.Meta.Quests.Data.UserQuestData`, **the Account's progress**.
- `UserWrapper.PeriodicQuests` (`PeriodicQuestWrapper`) and `UserWrapper.RecommendedQuests` (`RecommendedQuestsWrapper`) are views over the same `UserQuestData`. The second also holds `SignInRecommendedQuestData` (`QuestsCountByGroupId`, a `Dictionary<RecommendedQuestGroupId, int>`), which the probe didn't read.

`UserQuestData` holds:

| Field | Type | What it is |
| --- | --- | --- |
| `OpenedStates` | `List<QuestState>` | One state for each quest the Account has open, of every kind |
| `RecommendedQuestsData` | `UserRecommendedQuestData` | Missions: `ActiveGroups`, `CompletedGroups`, `CompletedPrototypeIds`, `LastQuestWeightsByGroupId`, `UserGroup`, `LastCompletedGroup`, `QuestsVersion` |
| `PeriodicData` | `UserQuestDataPeriodic` | Daily, weekly and monthly quests: `Groups`, each a `GroupId` (`Daily` = 1, `Weekly`, `Monthly`) and a `PeriodicQuestsData` list of `UserPeriodicQuestStateData` (`QuestId`, `QuestStateId`, `QuestState`) |
| `ChallengesData` | `UserQuestDataChallenges` | `CompletedPrototypeIds` only |
| `AchievementsData` | `UserQuestDataAchievements` | `CompletedQuestIds`, `SeenQuestIds` |
| `FluentData` | `UserQuestDataFluent` | `CompletedQuestIds`, `SoftWizardState` (the tutorial) |
| `AdvancedDailyData`, `BattlePassData`, `AllianceQuestsData`, `GlobalEventsData`, `NoviceTournamentData`, `InfluencerTournamentData`, `ReactivationQuestData` | | The other kinds' bookkeeping |
| `LastQuestId` | `int` | The last quest id the server gave out |

The issue's `UserQuestDataFeature` exists, but `UserQuestData` holds no field of that type.

## A quest

`SharedModel.Meta.Quests.Data.Quest` is what a quest asks and gives. The fields a build needs:

| Field | Type | Notes |
| --- | --- | --- |
| `Id` | `int` | The quest's id on the Account: the key in `Quests`, and the `QuestId` of its state |
| `PrototypeId` | `int` | **The stable id**, the same on every Account, and the id in its text keys |
| `CategoryId` | `QuestCategoryId` | Which kind: `Recommended` = 7 is a mission. The others are `Feature` = 1, `Wizard`, `Periodic`, `Challenge`, `Special`, `Achievement`, `Badges` = 8, and event, tournament and pass kinds up to `SeasonalCollection` = 31, with `None` = 0 |
| `Weight` | `int` | The order within a group. Live, missions run 22710, 22810, 22910 and so on |
| `RecommendedQuestCategoryId`, `RecommendedQuestGroupId` | `Nullable<…>` | Set on missions only |
| `Name`, `Description` | `SharedLTextKey` | The text. See below |
| `NameAlternative`, `CallToActionText`, `IntroText`, `OutroText`, `CountInNextGroup`, `TextSteps` | `SharedLTextKey` | Null on every mission read |
| `Prize`, `Prizes` | `UserPrize`, `List<UserPrize>` | The reward. Not read |
| `CompleteByOneOfTheCompletions` | `Nullable<bool>` | Whether one of several completions is enough |

## Its state and progress

`SharedModel.Meta.Quests.Data.QuestState` is the Account's progress on one quest:

- `QuestId` (the `Quest.Id`) and `PrototypeId`;
- `StateId`: `QuestStateId`, `Undefined` = 0, `New` = 10, `InProgress` = 20, `Completed` = 30;
- `PrizeGiven` (`bool`), and times (`TimeOpened`, `TimeCompleted`, `TimeDeadline` and others, `DateTime`, which the reader can't read yet);
- `RecommendedQuestGroupId`, `PeriodicQuestGroupId` and other kind tags;
- `Completions`: `List<QuestCompletion>`, what the quest counts.

A `QuestCompletion` has a `StateId` (`QuestCompletionStateId`: `InProgress` = 0, `Completed` = 9) and one field per kind of task, `ByBattle`, `ByHero`, `ByArtifact`, `ByMastery`, `ByCapitolBonus` (the Great Hall's Affinity bonuses), `ByArena`, `ByFuse`, `ByResourceSpent` and 23 more. All but the kinds in use are null. Most kinds hold `CountRequired` and `CountCollected`, both `int`, except `QuestCompletionByBattle`, where both are `long`: it counts damage too. `ByAreaBonus`, whose name suggests the Great Hall's Area Bonuses (not checked), has a type is spelled `QuestCompletionByByAreaBonus`.

### Stored counts and computed progress

**`CountCollected` isn't always the progress.** The screen's numbers come from `QuestWrapperReadOnly.CalcProgress(QuestState, QuestCompletionBy…, bool)`, one overload per kind, which returns a `QuestCompletionProgress` (`StepsCompleted`, `TotalSteps`). Disassembled, the overloads take one of two paths:

- **Completed quests:** each overload first checks whether the quest is `Completed`, and if so builds the progress from the stored `CountCollected` (at least 0) and `CountRequired`, without looking at the Account. A finished mission's -1 takes this path, not the computed one. <!-- (https://github.com/ANicholasson/raid-bridge/pull/37) -->
- **Stored kinds:** the kinds with no Account check of their own always build it that way (`ByResourceSpent`, `ByResourceGained`, `ByForge`, `ByPowerUp`, `ByRelic`, `ByRelicStone`, `BySiege`, `BySoloEvent`, `ByTournament`, `ByLiveArenaSeasons`, `ByLeaderboardSeason`, `ByFoggyForest`).
- **Kinds that can compute:** for an unfinished quest, the other kinds call the completion's own `CalcProgress` with the Account's current data. `ByHero` and `ByMastery` read the Champions; `ByCapitolBonus` and `ByAreaBonus` read the Great Hall (`UserVillageData`); `ByArena` reads the Classic Arena, the Champions, the Account and its stats; `ByArtifact` reads the Artifacts, Relics, Champions, Great Hall, Classic Arena and Faction Guardians; `ByBattle` reads stage progress and the Account's battle stats; `ByFuse`, `ByLoot`, `ByArena3X3`, `ByLiveArena`, `ByAllianceActivity` and `ByAction` read their own data.
- **Damage to a Clan Boss:** for a clan quest, `ByBattle` goes to `CalcProgressByDamageToAllianceBoss` instead, which reads the clan's boss list (`AllianceWrapper.Bosses`) and builds the progress from two `long`s. The issue's "49.7M of 100M" looks like this path, so for an unfinished one the screen's number may come from the clan's boss data, not from the stored count. A finished one keeps a stored count (below). Whether the stored count also stays current while the mission is open wasn't traced. <!-- (https://github.com/ANicholasson/raid-bridge/pull/37) -->

Inside the completion's own `CalcProgress`, the ones traced (`ByHero`, `ByMastery`, `ByCapitolBonus`, `ByAreaBonus` and `ByFuse`) first compare the stored `CountCollected` with 0. When it isn't negative they use the stored counts; when it is, they work the progress out from the Account. `ByBattle`, `ByArena`, `ByArtifact` and the rest weren't traced that far.

Live, this shows up as values: **for the traced kinds, a stored `CountCollected` of -1 on an unfinished mission means the game works the progress out from the Account.** `ByArena` and `ByArtifact` hold -1 too, and probably follow the same rule, but their own `CalcProgress` wasn't traced. <!-- (https://github.com/ANicholasson/raid-bridge/pull/37) --> It held -1 on every mission of `ByHero`, `ByArtifact`, `ByMastery`, `ByCapitolBonus` and `ByArena` kinds, finished or not (for example `ByHero` -1 of 5 on a `Completed` mission). Missions that count things held real counts: `ByBattle` 0 of 10 and 10 of 10, `ByFuse` 0 of 10, and 25,347,525 of 10,000,000 on a finished "deal 10,000,000 damage to the Demon Lord". A count can pass what's required.

So a Snapshot can write a stored count of 0 or more as the game holds it. For an unfinished mission whose count is -1 the game holds no number of its own, and for an unfinished Clan Boss damage mission the stored count may not be the screen's number. Writing the screen's number there would mean porting that kind's `CalcProgress`.

## Where the text comes from

A mission's `Name` and `Description` are `SharedLTextKey`s, the same as a Champion's name, keyed by the prototype id: `l10n:quest/name?id=<PrototypeId>#static` and `l10n:quest/description?id=<PrototypeId>#static`. Live, **all 60 missions' names and descriptions were in `ClientStaticData.StaticDataLocalization`**, the table `RaidBridge.Game.Localization.LocalizedNames` already reads. So the reader's existing lookup finds them with no new route.

- **The name is the line the player reads**, for example "Upgrade 5 Champions to Level 60", "Upgrade an Affinity Bonus to Level 7", "Clear Stage 14 or higher of the Ice Golem's Peak 10 times on Auto (on Normal)", or "Deal 10,000,000 damage to the Demon Lord on a single difficulty. Min. difficulty: Hard". The numbers are already in the text, so it needs no placeholders filled in.
- **The description is the longer help text**, several sentences, sometimes with the client's colour tags (`<color=#ffe87d>Classic Arena</color>`), the same tags the Relic descriptions have.
- **Default text isn't the screen's text.** A name's `DefaultValue` is a developer's note ("Have 5 heroes level 60"), and a description's is a placeholder ("310226 quest description"). The table lookup has to win, as `LocalizedNames` already makes it. But `LocalizedNames` falls back to the default text when no table holds the key, so reused for the kinds below it would quietly write developer notes. This is the trap behind https://github.com/ANicholasson/raid-bridge/issues/26.

Other kinds differ. The names of periodic, Challenge, Achievement, Champion pass and 15 of 16 Advanced quests were in the same table. Those of Badges (78), event quests (`CooperationEventMinionQuest` 35, `GlobalEventSolo` 4), tournaments (6), tutorial quests (4) and one Advanced quest were in neither static table, only a developer's default text such as "Have 50 Legendary". Their text probably comes from the server's text: `Localizer._serverLocalizationStorage` (a `ConcurrentDictionary<string, string>`, which the reader can't read yet) or `AppModel.GlobalEventsExternalLocalizationStorage`. That wasn't checked.

## Live check (2026-11-13)

A read-only probe read the running Game client through the Linux adapter, with each field's offset taken from its runtime class, as the reader does. Every route above resolved.

- `Quests` held 289 quests: 78 Badges, 60 Recommended, 36 Challenge, 35 event, 22 Champion pass, 21 periodic, 16 Advanced, 7 Achievement, 6 tournament, 4 event solo and 4 tutorial. `OpenedStates` held 275 states, every one naming a quest in `Quests`.
- Missions on the Account read: `ActiveGroups` is `[Group4]`, `CompletedGroups` `[Group1, Group2, Group3]`, 237 completed prototype ids, `QuestsVersion` `PostTestsEarlyMissions` and `UserGroup` `Undefined`. So this Account is on Arbiter's last group.
- **On this Account, only the active group's missions are held.** All 60 Recommended quests are in `Group4`. Earlier groups' missions are gone except as ids in `CompletedPrototypeIds`, and the later sections' missions aren't sent yet. `ActiveGroups` is a list, and no Account in another state was read, so whether two groups can be active at once is open. <!-- (https://github.com/ANicholasson/raid-bridge/pull/37) -->
- **Earlier missions' text is still there.** All 237 completed prototype ids have both a name and a description in `StaticDataLocalization`, for example 310200, "Defeat 100 Champions in Classic Arena". Their group, category and counts aren't held. <!-- (https://github.com/ANicholasson/raid-bridge/pull/37) -->
- Of the 60: 11 `Completed` with their prize given, 1 `InProgress` ("Upgrade an Affinity Bonus to Level 7") and 48 `New`. Every `New` one's completion is `InProgress`.
- Categories in `Group4`: 33 `Dungeons`, 10 `Heroes`, 7 `Arena`, 5 `Artifacts` and 5 `Clan`. Weights rise with the order.
- Periodic quests: 8 daily, 7 weekly and 6 monthly.
- Of 275 quests with a state, 267 have one completion, 1 has two, and 7 have none. In 5 completions two `By…` fields are set at once.

## Confirmed on the screen (2026-11-13)

The maintainer's screenshot of the Missions screen, taken with the Account read above:

- **The current mission** is "Upgrade an Affinity Bonus to Level 7", headed "Arena Mission" (its category is `Arena`), with **0/1**. It is the one `InProgress` mission, a `ByCapitolBonus` kind whose stored count is -1 of 1. So for this kind the screen's number is "done or not", worked out from the Great Hall, not the Affinity Bonus's level.
- **The next missions show a padlock**, each with 0/10: "Clear Stage 14 or higher of the Arcane Keep 10 times on Auto", "Mix 10 Superior Arcane Potions" (a `ByFuse`) and the Spirit Keep. In the screenshot, these `New` missions are locked, the one `InProgress` mission isn't, and the cards run in `Weight` order. So `New` probably means locked, but only the first few cards were seen, so how the rest open isn't established. The stored 0 of 10 is what these cards show. <!-- (https://github.com/ANicholasson/raid-bridge/pull/37) -->
- **The card's heading is the category**, "Arena Mission" or "Dungeon Mission", and the line under it is the quest's `Name`. The `Description` isn't on the card.
- **The bar along the bottom** reads Part 4/4, 237/286, and per Part 76/76, 75/75, 75/75 and 11/60. 76 + 75 + 75 + 11 = 237, the number of completed prototype ids, and 60 is the number of missions held for `Group4`. The last reward is a Champion.

## Open questions for the build

1. **What the screen shows** for damage to a Clan Boss that isn't finished, and for -1 kinds other than `ByCapitolBonus`; and whether every `New` mission is locked and they open one at a time in `Weight` order. The one screenshot settled the rest (above).
2. **Computed progress:** write `null` progress for those (only what the game holds), or port `CalcProgress` for some kinds. Many need only data the Snapshot already carries, such as Champion levels, masteries, Great Hall levels and the Classic Arena league.
3. **Scope:** missions only, as the issue asks first, or the other kinds too. Periodic quests resolve their text the same way, but some kinds' text isn't in the static tables.
4. **Section progress:** the screen counts missions done per group towards Arbiter (`RecommendedQuestsWrapperReadOnly.CompletedQuestsCountFor`, `CountFor`, and `SignInRecommendedQuestData.QuestsCountByGroupId`). On screen they read 76/76, 75/75, 75/75 and 11/60 for Parts 1 to 4, and 237/286 overall. Whether the Snapshot carries those counts, or a reader works them out from the missions, wasn't decided. Earlier groups' missions could be written as finished, by prototype id and text only.
5. **Rewards:** `Quest.Prize` and `RecommendedQuestGroupInfo.Reward` weren't read.
6. **Which text:** the recommendation is the `Name` as the mission's text, since it is the short line that says what to do, with the `Description` left out or optional. Still to decide.
7. **Words:** "Mission" for a recommended quest is a candidate for GLOSSARY.md (_Avoid_: recommended quest), still to settle.
