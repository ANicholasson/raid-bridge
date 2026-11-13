# Hexagonal projects with feature folders inside

raid-bridge is a few projects in a ports-and-adapters (hexagonal) shape, with code grouped by feature, not by kind, inside each project (decided 2026-11-01).
<!-- #22 added RaidBridge.Domain and RaidBridge.Game, #23 added RaidBridge.Export and RaidBridge.Cli, and #24 added RaidBridge.Game.Linux (PR #21). -->

- `RaidBridge.Domain` holds the Account model in the glossary's words (Accounts, Champions, Artifacts, Snapshots, and a folder for each later feature such as Resources, or Arena for the Classic and Tag Arena) <!-- (PR #56) --> and the one use case, `TakeSnapshot`, together with the ports it needs. It has no IO and no game types, and references nothing.
- `RaidBridge.Game` is the anti-corruption layer. It reads the Game client and translates the Game client's own model (hero, grade, fraction and the rest) into the Domain's. It keeps the same feature folders, with `Localization/` for the names they share (#25) and `Numerics/` for the Game client's fixed-point numbers (#26), plus a shared kernel for reading IL2CPP objects, the per-Unity-version layouts (ADR-0003), the metadata wrapper over the vendored LibCpp2IL, and the memory port (ADR-0002). The port's fake and contract suite are test code, so they live in `tests/RaidBridge.Game.Testing` with the synthetic Game client (#23).
- `RaidBridge.Game.Linux` is the Linux memory adapter and the only platform-specific project.
- `RaidBridge.Export` writes the Snapshot JSON and keeps the salt file.
- `RaidBridge.Cli` is the composition root: it wires the adapters to the use case and turns failures into one-line errors.

Dependencies point inward, toward Domain. A feature such as Champions lives in one folder on each side (`Domain/Champions`, `Game/Champions`), so a build ticket stays a vertical slice and a change stays in one place.

## Considered Options

- **One project per technical layer** (Snapshot, Metadata, Memory, Reader, Cli). Rejected: it cuts every feature across five projects, which is low cohesion and makes each ticket a horizontal slice.
- **One project per feature.** Rejected for now: the features share the IL2CPP kernel and the memory port, so splitting them into projects would add coupling through a shared project without a boundary that pays for itself.
- **A mediator, a DI container framework, generic repositories.** Left out: at this size they would be clever rather than obvious. The composition root wires things by hand.

## Consequences

- The Game model never leaks into Domain or Export: game names such as hero or grade appear only inside `RaidBridge.Game`.
- A new platform is a new `RaidBridge.Game.<Platform>` adapter that passes the memory contract suite, and nothing else changes.
- A new kind of Account data (battle results, other teams) is a new feature folder on each side.
