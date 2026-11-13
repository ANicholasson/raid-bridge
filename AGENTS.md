# AGENTS.md: raid-bridge

## What this project is

raid-bridge reads the player's own RAID: Shadow Legends Account (Champions, Artifacts, Relics, Resources, its Arena defence teams and standings; other teams and battle results later) out of the Windows game client and writes it as JSON Snapshots for the player to use as they choose. Data out only: raid-bridge never plays for them. C# on .NET 10.

Status: `raid-bridge snapshot` writes a Snapshot holding the Account, its Champions with their Total stats, their Base stats and what their Artifacts, sets, masteries, the Great Hall, Faction Guardians, Awakening, Empowerment, Relics and the Classic Arena league add, the Artifacts and Relics they wear, the Artifacts and Relics nobody wears, the Relic stones in no socket, its Resources, Shards and Items, its Great Hall and Area Bonuses, its Faction Guardians, its Classic, Tag and Live Arena, and its Missions (#23, #25, #26, #33, #34, #35, #36, #37, https://github.com/ANicholasson/raid-bridge/issues/8, https://github.com/ANicholasson/raid-bridge/issues/10, https://github.com/ANicholasson/raid-bridge/issues/11, https://github.com/ANicholasson/raid-bridge/issues/12, https://github.com/ANicholasson/raid-bridge/issues/13, https://github.com/ANicholasson/raid-bridge/issues/14, https://github.com/ANicholasson/raid-bridge/issues/15, https://github.com/ANicholasson/raid-bridge/issues/20, https://github.com/ANicholasson/raid-bridge/issues/32, https://github.com/ANicholasson/raid-bridge/issues/33), read from the running Game client under Proton by the Linux memory adapter (`RaidBridge.Game.Linux`, #24); there is no Windows adapter yet. The reader parses the installed game's files (`RaidBridge.Game`'s `Metadata/`, #22). The format is in `docs/snapshot.md`.

## Agent skills

The slash commands named here (`/grill-with-docs`, `/wayfinder`, `/to-spec`, `/to-tickets`, `/code-review`) are skills from [mattpocock/skills](https://github.com/mattpocock/skills), enabled for Claude Code in `.claude/settings.json`. Where they aren't installed, read `skills/<area>/<name>/SKILL.md` in that repo and follow it.

### Issue tracker

Issues and specs live in GitHub Issues on `ANicholasson/raid-bridge`, via the `gh` CLI. See `docs/agents/issue-tracker.md`.

The `#NN` and `PR #NN` references already in the tree when the public repository began point to the earlier private tracker, which isn't public. They stay as provenance. From then on, cite an issue or PR by its link, so the two numberings can't be confused.

### Triage labels

The five canonical roles under their default names (`needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`), applied as GitHub labels. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: `GLOSSARY.md` and `docs/adr/` at the repo root. See `docs/agents/domain.md`.

## Where to start

1. `GLOSSARY.md`. Use its terms, and avoid the ones it lists under _Avoid_.
2. `docs/adr/`, ADR-0001 first.
3. `docs/research/2026-10-raid-toolkit-sdk-review.md`: why this is a new reader and not a fork of Raid Toolkit.
4. The open issues.

## Standing constraints

Fair play. These define the product: a violation is a bug even when a task seems to need it, so stop and ask the maintainer instead.

- Read only (ADR-0001). On Linux the game is read with `process_vm_readv` and nothing else, and raid-bridge refuses to run as root or with any capability (#13); on Windows the game process is opened with `PROCESS_VM_READ`, plus `PROCESS_QUERY_LIMITED_INFORMATION` if needed, and nothing more. No code runs inside `Raid.exe`: no injection, hooks, debugger or remote threads, and nothing writes to its memory. `tests/RaidBridge.Guardrails.Tests` fails `verify` on the Windows and Linux APIs, access rights and packages that would break this, on any write in a file that names `/proc`, and on any native import outside its allowlist (`process_vm_readv`, `getuid`, `geteuid`). Extend its lists, never trim them: when a harmless name matches, rename the code.
- No automation. Nothing sends input to the game or calls game methods.
- No credentials. Nothing asks for, stores or sends Plarium login details.
- The only network destination is the endpoint the user configures. No telemetry, no update check, no third-party service. `tests/RaidBridge.Guardrails.Tests` fails `verify` on any .NET network API; when sending to that endpoint lands, it lets through only that feature's folder.
- Account ids leave the machine only as hashes. Fixtures committed here are synthetic or scrubbed, with no real account id, player name or clan name. Real captures and Snapshots stay on the machine that made them (`captures/` and `snapshot-*.json` are gitignored).
- No personal names, in files, commit messages, PR text or review replies: the repo may be published, and its history with it. Say the maintainer or the player, and date a decision rather than sign it. The public repository starts from one scrubbed commit of an earlier private one, because a GitHub PR page keeps its commits after a history rewrite (decided 2026-10-27).

Platform:

- Only the memory adapter is platform-specific (ADR-0002), one adapter per platform behind one port, Linux (the game under Proton) first. Everything else targets `net10.0` with no platform API, so it builds and tests on Windows, Linux and macOS. CI runs `verify.sh` on Windows and Linux, with macOS off until raid-bridge supports it (2026-11-13), and agents run on Linux.
- Raid Toolkit (`raid-toolkit/raid-toolkit-sdk`, `raid-toolkit/account-extension`, both MIT) is a reference for WHAT to read, never a dependency. Code ported from it keeps its MIT notice in a comment at the site, naming the source file. Its core, `IL2CPP-Toolkit/Core`, has no license: never copy from it.
- Game updates move classes and offsets. The installed game's files locate the static roots and give the expected field names and types; each object's field offsets come from its runtime class in memory and are checked against them (ADR-0001). Nothing hard-codes an offset or a field index of the game's own classes. <!-- (CodeRabbit, PR #21) --> The IL2CPP runtime's own structures are the one exception: one layout table per Unity version, and any other version is refused (ADR-0003).

Code:

- Domain-driven design, in the glossary's words: high cohesion, low coupling, deep modules behind small interfaces, obvious over clever, and well-established patterns over invented ones (the maintainer's standing preference, 2026-11-01).
- Hexagonal projects with feature folders inside (ADR-0004). `RaidBridge.Domain` references nothing. `RaidBridge.Game` (the anti-corruption layer over the Game client) and `RaidBridge.Export` reference Domain, and `RaidBridge.Game.Linux` is the only platform-specific project. `RaidBridge.Cli` is the composition root. Inside a project, code is grouped by feature (`Accounts/`, `Champions/`, `Artifacts/`, `Snapshots/`), never by kind: no `Models/`, `Services/` or `Helpers/`. Tests mirror the same folders.
- Boundaries are project references: a project that must not see another does not reference it, and whatever other projects don't need is `internal`.
- Test support the test projects share lives in `tests/RaidBridge.Game.Testing`: the fake memory port, the contract suite every memory adapter passes, and the synthetic Game client the reader is tested through. Nothing in `src/` references it.
- Parse game data, don't trust it. A value read from memory is checked into a typed model, and a mismatch throws an error naming the type, the field and the address. Never a default, never a silent skip, never a made-up number.
- Tests never need the game, Windows or the network. Whatever does is a gated check that skips without the game: the installed-files check in `RaidBridge.Game.Tests`, and the running Game client's checks in the memory adapter's own test project. <!-- (CodeRabbit, PR #27) -->
- Package versions live in `Directory.Packages.props`, and restores are locked (`packages.lock.json`; refresh with `dotnet restore --force-evaluate`). A new package is justified in the PR body. Dependabot opens at most one grouped NuGet PR and one grouped GitHub Actions PR a month (`.github/dependabot.yml`), and security updates may come separately (CodeRabbit, PR #60). A NuGet PR leaves the lock files stale, so refresh them on its branch before it can pass.
- Third-party code must keep working, not merely be pinned. When a package is unmaintained or lags a fix we need, patch it in our tree (keeping its license notice and naming the upstream commit) or re-implement the part we use; never ship around a known bug (decided 2026-11-01).
- Vendored third-party code lives in `third_party/<name>/`, never in `src/`, one folder per upstream (decided 2026-11-01):
  - The folder keeps upstream's own license file and an `UPSTREAM.md` naming the source URL, the pinned commit, what was dropped, and every patch with its reason.
  - It builds as its own project, referenced only by the module that wraps it, and no other project's API exposes its types.
  - Upstream's code keeps upstream's style: our format check and style analyzers skip `third_party/`, but the guardrails, the locked restore and the build still cover it.
  - The first commit is the untouched copy at the pinned commit. Each patch is its own small commit, marked at the site with `// raid-bridge patch: <why>`. Moving to a newer upstream commit replaces the copy in one commit and re-applies the patches.
- Commits: conventional, subject ≤50 chars, body only when the why isn't obvious, NO `Co-Authored-By` trailers.

## Checks

`./scripts/verify.sh` is the one gate, and CI runs exactly it. Capture its own exit code before any commit: never pipe it through `tail` or `grep` and chain a commit on the pipe.

Gated checks skip unless `RAIDBRIDGE_LIVE=1`. The installed-files check also needs `RAIDBRIDGE_GAME_DIR`, the folder holding `Raid.exe`, and fails rather than skips when it's missing, so an opted-in run never passes by reading nothing.

Green tests are not acceptance for reader code. A reader change is proven against the running Game client on its adapter's platform (the maintainer's Linux install, the game under Proton, for the Linux adapter), and the PR says plainly what has only compiled (CodeRabbit, PR #2).

## Working agreement

- **Interrogate**: `/grill-with-docs` per vertical before building, one decision per question, each with a recommendation. Work too big for one session goes on a `/wayfinder` map.
- **Capture**: vocabulary lands in `GLOSSARY.md` as it resolves (a glossary only, never a spec or a scratchpad); ADRs at the bar hard-to-reverse ∧ surprising ∧ real trade-off; research in `docs/research/`.
- **Breakdown**: `/to-spec`, then `/to-tickets` into tracer-bullet slices filed as GitHub issues.
- **Implement**: one slice per PR. Every changed file earns its place: no drive-by refactors. Flag ambiguity as an open question; never invent behaviour or settle a decision the grill hasn't.
- **Review**: `/code-review` locally before the first push, and once per iteration after it, never per fix. Then the review bots: triage EVERY finding (fix it with a regression test, or rebut it with the mechanism), reply per thread, resolve, and repeat until a round returns zero findings. A line that exists because of a review finding carries provenance at the site, `(CodeRabbit, PR #3)` style. NEVER merge: the maintainer merges.

## Doc-sync rule

Any change that alters a pattern documented here, in an ADR or in any instruction file updates that doc **in the same change**. Stale instruction files are worse than none: they are injected into every agent's context and teach the old pattern.

## Known agent failure modes

Real incidents. Take them personally:

- Trusting a dependency's description over its code. Raid Toolkit was first reported here as "reads memory, no injection" from its README and architecture; decompiling Il2CppToolkit showed that every type lookup goes through a DLL it injects into `Raid.exe` (2026-10-27). Before stating what any dependency does to the game process, read its source or decompile it.
