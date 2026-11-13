# Where the game keeps saved teams and presets (2026-11-13)

Findings for https://github.com/ANicholasson/raid-bridge/issues/31: whether the Game client keeps the player's saved teams, its Artifact presets and the team it last attacked with in the Classic Arena, and where, so a later ticket can add them to the Snapshot.

Everything above the "Live check" section comes from the installed game's own files: `global-metadata.dat` and `GameAssembly.dll` at IL2CPP metadata v39 and Unity 6000.3.18f1, dated 2026-11-06, parsed with the vendored LibCpp2IL, and a few methods disassembled from `GameAssembly.dll` (their addresses are in the text). That part read no game memory and no Account. The "Live check" section then reads the running Game client with one of the maintainer's Accounts logged in. Nothing here is pinned by a gated check yet.

## In short

The saved teams and the team last attacked with in the Classic Arena are in the Game client and hold Champions today. Artifact presets are held too, once the Account has any (see the Live check).

| What the player sees | Where the game keeps it | Live |
| --- | --- | --- |
| Saved teams (the "presets" on the team screen) | `UserHeroData.HeroesAiPresets` | 2 saved teams |
| The team each place remembers, the Classic and Tag Arena's among them | `UserHeroData.BattlePresets`, by a key made from the place | 64 places |
| The Tag Arena defence teams as saved teams | `UserArenaData.DefensePresets` of the Tag Arena | 3 teams, the same Champions as its defence teams |
| Artifact presets, per Champion | `ExternalArtifactsStorage._presets`, and `UserArtifactData.UpdatedPresetsByHero` for presets made since the game started | none at first; one after the maintainer saved one |

The Classic Arena attack team is not a field of `UserArenaData`. It is the team the Game client remembers for the Classic Arena as a place, `BattlePresets[3014]`, which follows each attack (live check), the same way it remembers the last team for each dungeon, Faction Wars crypt or Clan Boss.

## Saved teams

The route starts from the Account's root, `Client.Model.AppModel._userWrapper`, like every other route. `UserWrapper.Heroes.HeroData` is the `UpdatableHeroData` the Champion reader already reads `HeroById` from. Its base class `SharedModel.Meta.Heroes.UserHeroData` declares:

- `HeroesAiPresets`: `List<HeroesAiPreset>`, every saved team on the Account;
- `BattlePresets` and `PresetIdsByRegionKey`: both `Dictionary<int, int[]>`, below.

`SharedModel.Meta.Heroes.HeroesAiPreset`:

| Field | Type | Notes |
| --- | --- | --- |
| `Id` | `int` | Also says what the team is for (below) |
| `Name` | `string` | The name shown. Live, every team still had the game's default name |
| `Position` | `int` | Its place in the list the player sorts; 1-based for the saved teams read live |
| `NameIsNotDefault` | `bool` | Whether the player named it |
| `SkillPrioritiesSetups` | `List<SkillPrioritiesSetup>` | One per position. **An empty position can be a null entry** (live) |
| `TypeRanges` | static `(int, HeroesAiPresetType)[]` | The id ranges, not data of the team |

A team has no list of Champion ids of its own. Each position's `SharedModel.Meta.Heroes.SkillPrioritiesSetup` names its Champion in `HeroId`, an id in `HeroById`, and adds the AI's skill priorities for that Champion: `StarterSkillId` (`Nullable<int>`), `PriorityBySkillId` (`Dictionary<int, SkillPriorityType>`, null on every team read live) and `Sequences` (`List<HeroAiPresetSequence>`, one per round or phase, each with `StarterSkillId`, `StarterSkillIds` and its own `PriorityBySkillId`). `SkillPriorityType` is `None` = -1, `Default` = 0, `First` = 1 to `Third` = 3, `NotUsed` = 4.

What a team is for comes from its id, not a field: `HeroesAiPreset.get_Type` (RVA 0x2F471F0) looks the id up in `TypeRanges`. The ranges are the constants of `SharedModel.Meta.Heroes.HeroesAiPresetIds`, a block of ids for each use, most of them a hundred wide. The Classic Arena defence (301) and the Tag Arena defence (601 to 603) are ids of their own, with no `HeroesAiPresetType` member, and no constant starts at 501. These ids are not `RegionTypeId` values, though some numbers are the same (301, 601, 701):

| Ids from | Type (`HeroesAiPresetType`) | Max Champions, max teams (live) |
| --- | --- | --- |
| 1 | `GeneralPve` = 1 | 5, 15 |
| 101 | `Pvp` = 2, attack teams for the Classic Arena, and perhaps for each Tag Arena team (not traced) | 4, 15 |
| 201 | `Fractions` = 4, Faction Wars | 5, 15 |
| 301 | the Classic Arena defence (`ArenaDefence`, one id) | |
| 401 | `DoomTower` = 5 | 5, 15 |
| 601 to 603 | the Tag Arena's three defence teams | |
| 701 | `Hydra` = 3 | 6, 15 |
| 801 | `Chimera` = 6 | 5, 15 |
| 901 | `CooperationEventWorldBoss` = 7 | 5, 10 |
| 1001 | `Siege` = 8 | 4, 15 |
| 1101 | `IronTwins` = 9 | 5, 15 |
| 1201 | `PotionKeeps` = 10 | 5, 15 |
| 1301 | `ArtifactAscendAndMasteries` = 11 | 5, 15 |
| 1401 | `EventDungeon` = 12 | 5, 15 |
| 1501 | `ArtifactDungeons` = 13 | 5, 15 |

The limits are static data: `ClientStaticData.HeroData` (`StaticHeroData`) `.PresetInfoByType`, a `Dictionary<HeroesAiPresetType, HeroesAiPresetInfo>` of `MaxHeroesCount`, `MaxPresetsCount` and `SequencesCount`.

## The team each place remembers

`UserHeroData.BattlePresets` is the team the team screen offers again the next time the player goes to a place: one `int[]` of Champion ids per place. `HeroExtensions.GetBattlePreset` and `UpdateBattlePresets` read and write it, and `GetBattlePresetKey` makes the key:

- For most places, `GetBattlePresetKey(RegionTypeId, slotsCount)` (RVA 0x2F2ABE0) is `(int)region * 10 + slotsCount`, where `slotsCount` is how many Champions one team there takes. The Classic Arena (`RegionTypeId.Arena` = 301, four Champions) is key **3014**, the Tag Arena (`Arena3x3` = 601) is **6014**, the Clan Boss (`AllianceBoss` = 401, five) is 4015, the Hydra (`AllianceHydraSeason5` = 805, six) 8056.
- For a stage, `GetBattlePresetKey(Stage, slotsCount)` (RVA 0x2F2ACD0) uses the stage's own id instead for the Foggy Forest, the co-operation event's world boss and secret chambers, the stage id times 100 plus a value from a lookup for the Cursed City, and its own branch for the Doom Tower. The live keys of seven and eight digits (7011010, 15019001) are of this kind. Decoding these needs more disassembly; a build can leave them out or write them by stage id.

`RegionTypeId` runs from `Region1` = 101 to `Region12` = 112 (the campaign), 201 to 219 (keeps, dungeons, Champion Journey), `Arena` = 301, `AllianceBoss` = 401, 501 to 517 (one per Faction), `Arena3x3` = 601, `DoomTower1` to `3` = 701 to 703, `AllianceHydraSeason1` to `6` = 801 to 806, `LiveArena` = 901, 1001 to 1005 (Cursed City), 1101 to 1103 (Siege), 1301 to 1304 (Chimera), 1401 to 1404 (Foggy Forest), 1501, and `InDevelopment` = -1.

The array holds only the Champions chosen, with no slot for an empty position (live: the campaign's keys hold two to four ids). The Tag Arena's array holds its three teams one after another, twelve ids. The array doesn't say which Champion leads: in the Classic Arena's, the leader came first for one team and second for another. Its order was the order the battle's result screen lists the team in, for the one team checked.

`PresetIdsByRegionKey`, by the same kind of key, names the saved teams last picked for a place (`HeroesAiPresetExtensions.UpdatePresetIdsByRegionKey`). It was empty live.

## The Arena's own teams

`SharedModel.Meta.Battle.Arena.UserArenaData`, which the Arena reader already reads, also declares `DefensePresets`: `List<HeroesAiPreset>`, the defence teams as saved teams with their AI settings. Live, the Classic Arena's was null and the Tag Arena's held three teams with ids 601 to 603, the same Champions as its `Arena3X3DefenseTeamSetup`. The Snapshot already writes both Arenas' defence teams, so this adds only their skill priorities.

Nothing in `UserArenaData` holds the player's own attack team. `CurrentOpponent`, `Opponents` and `Logs` hold the other players' teams (`ArenaLog.OpponentSetup`), not the player's.

## Artifact presets

An Artifact preset is a set of Artifacts saved for one Champion. They are kept beside the Account's Artifacts in the external storage the Artifact reader already reads from (`ArtifactStorageResolver._implementation`, an `ExternalArtifactsStorage`):

- `ExternalArtifactsStorage._presets` is a `Client.Model.Gameplay.Artifacts.Presets.ArtifactPresets`; its `_presets` is a `Dictionary<int, ArtifactsPresetsOfType>` by Champion id (`ArtifactPresets.GetPresetsOfType(data, heroId)`), and `<Revision>k__BackingField` a `long`.
- `SharedModel.Meta.Artifacts.Presets.ArtifactsPresetsOfType` holds `_artifactPresets` and `_accessoryPresets`, each a `List<ArtifactPreset>`, plus `_revision` and `_lastUpdateTime`.
- `SharedModel.Meta.Artifacts.Presets.ArtifactPreset` is `_id` (`long`), `_name` (`string`) and `_artifactIds` (`List<int>`, Artifact ids).
- Changes not yet sent are in `UserArtifactData.UpdatedPresetsByHero` and `DeletedPresetHeroIds`; `ArtifactPresets.GetPresets` merges them in.
- The limits are `ArtifactSettings.PresetSettings` (`ArtifactPresetSettings`: `MaxPresetsGlobal`, `MaxPresetsByHero`, `MaxItemsCountForArtifactPreset`, `MaxItemsCountForAccessoryPreset`, name lengths, `AvailableHeroLocations`). Where the client reaches `ArtifactSettings` wasn't traced.

## What isn't kept, or not for long

- `UserBattleData.LastBattleDatas` (`UserWrapper.Battle.BattleData`) holds a `BattleSetup`, with the player's `FirstTeam`, per battle. It was empty live, and `ClearLastBattleDataCmd` suggests it holds only battles not yet settled with the server. It is no source for a remembered team.
- The client's battle journal (`AppModel.Journals`) writes battles to files; reading files the game writes is outside the memory adapter, and not needed.
- The auto-battle report (`AutoBattleReport.LastBattleHeroIds`, `PresetId`) lives only while a multi-battle runs.

## Live check

Read on 2026-11-13 with a throwaway gated test, deleted before commit, from the running Game client with one of the maintainer's Accounts logged in (197 Champions):

- `HeroesAiPresets`: 2 saved teams, ids 1 and 2 (`GeneralPve`), positions 1 and 2, default names. One held two entries, both Champions; the other held four, one Champion and three null entries. So the list isn't always as long as the team can be. Every position's `PriorityBySkillId` was null and `Sequences` held 3, `GeneralPve`'s `SequencesCount`.
- `BattlePresets`: 64 places. Key 3014, the Classic Arena, held four Champions, the same four as its defence team in another order. Key 6014, the Tag Arena, held twelve, a different split from its three defence teams. Keys matched the formula for the campaign (1012 to 1124), dungeons (2015 to 2185), the Clan Boss (4015), Faction Wars (5015 to 5165), a Doom Tower region (7035), the Hydra (8056) and Chimera rotations (13015 to 13045), and stage keys of seven digits for the Doom Tower (7011010 to 7031020) and eight for the co-operation event (15019001, 15019002).
- `PresetIdsByRegionKey`: empty. `LastBattleDatas`: empty.
- Classic `DefensePresets`: null. Tag `DefensePresets`: 3 teams, ids 601 to 603, four Champions each, `Position` 0, each position's `Sequences` 1, which matches `Pvp`'s `SequencesCount` (which type the game counts 601 to 603 as wasn't traced).
- Artifact presets: `ExternalArtifactsStorage._presets._presets` empty, revision -1; `UserArtifactData.NextPresetId` 1, `UpdatedPresetsByHero` empty. `DeletedPresetHeroIds` held over a thousand Champion ids, most no longer on the Account: it looks like a log of Champions whose presets were cleared when they left, and was empty after a restart. It isn't needed.

Every Champion id read was a Champion in `HeroById`.

Then the maintainer fought one Classic Arena battle with a different team, and a second read found key 3014 changed to exactly that team, in the order the result screen listed it, with the leader second. The maintainer's screenshots showed the earlier team as the one the Arena's team screen offered, with its leader first. So key 3014 is the team last attacked with. The Clan Boss's key 4015 had changed between the reads too.

Artifact presets, checked the same day:

- **Where a saved Artifact preset ends up.** The Account had none at the first read. The maintainer then saved one for one Champion, and a third read found it not in `ExternalArtifactsStorage._presets` (still empty, revision -1) but in `UserArtifactData.UpdatedPresetsByHero`, under that Champion's id: one Artifact preset of six Artifacts and one accessory preset of three, ids 2 and 3, `_revision` 1, and `NextPresetId` up from 1 to 2. So a preset made in this session is held as a change not yet sent to the storage.
- **After a restart.** The maintainer then restarted the game, and a fourth read found the preset in `ExternalArtifactsStorage._presets` (revision 1, the same ids and Artifacts), `UpdatedPresetsByHero` empty and `DeletedPresetHeroIds` empty. Presets saved earlier are loaded with the Account when the game starts; the maintainer's report doesn't say whether that Champion's presets were opened first, so that one detail is unchecked. Everything else read (saved teams, `BattlePresets` with the new Classic Arena attack team) was the same as before the restart, so all of it outlives a session.

## What a build ticket needs

To decide before building:

1. **Which teams to write.** Saved teams (`HeroesAiPresets`) are what the player made on purpose, and the clearest to write. The remembered teams (`BattlePresets`) are richer, the Classic and Tag Arena attack teams among them, but are an inference from use. The issue asks for saved teams and the Classic Arena attack team, so a small first slice is saved teams plus key 3014, now that the live check settled it. A remembered team doesn't say which Champion leads, so the Snapshot can't either. The Tag Arena's key 6014, the other places, the Tag Arena's `DefensePresets` and Artifact presets go beyond the issue, and are extras to take or leave.
2. **The place, as a term.** The glossary has no word for what a team is for (the game's `HeroesAiPresetType`) nor for where it was used (`RegionTypeId`). Writing both by the game's member names, as Affinities and leagues are written, fits the format, but needs glossary terms such as "Saved team" and "Remembered team".
3. **Team names.** A saved team's name is text the player typed. Snapshots stay with the player, but they feed it to other services; decide whether to write the name, or only whether it was named.
4. **Skill priorities.** Each position carries the AI's skill priorities per round. They name skills by id and are useful to an advisor, but double the shape. Recommend leaving them out of the first slice.
5. **The Tag Arena's twelve ids.** `BattlePresets[6014]` holds three teams in one array. Splitting it into three teams of four assumes every team is full, which an Arena with an empty position would break; check the team screen with an empty position before splitting.
6. **Stage keys.** Leave out keys that aren't `region * 10 + slots`, or decode `GetBattlePresetKey(Stage, …)` further first.
7. **Artifact presets.** Read them as the game does, `ArtifactPresets.GetPresets`: the storage's `_presets` with `UpdatedPresetsByHero` laid over it and `DeletedPresetHeroIds` taken out. Presets saved in earlier sessions were loaded after a restart, so they can be written as the game holds them.

Parse, don't trust: a team names Champions by id, and each must be a Champion in `HeroById`, as the Arena reader checks today. An empty position can be a null entry in a saved team but is simply absent in a remembered one, and both shapes need a test.
