# Read the game client's memory, never run code in it

raid-bridge gets Account data by reading the Game client's memory from outside, with read access only (each platform's adapter says how, ADR-0002), and works out where things live from the game's own metadata, on disk and as the runtime holds it (amended 2026-11-01). It never loads code into the Game client, hooks it, debugs it, writes to it or drives it. The point is a Companion that helps a player understand their own Account: injection or automation would make it a cheat and ruin the game for the player it serves (decided 2026-10-27). Reading memory is still a grey area under Plarium's terms of service; injecting into the client is not.
<!-- Read access per adapter: (CodeRabbit, PR #2) -->

## Considered Options

- **Fork Raid Toolkit (`raid-toolkit/raid-toolkit-sdk`).** Rejected. Its Il2CppToolkit core injects a DLL into `Raid.exe` that hosts a gRPC server, and the runtime sends every type lookup through it, so it can't be made read-only without replacing the core. That core also has no license. See `docs/research/2026-10-raid-toolkit-sdk-review.md`.
- **Screenshots and OCR.** Kept as a fallback only. It touches nothing, but it's slow, lossy and breaks with every UI change.

## Consequences

- The reader resolves classes and field offsets itself, and has to keep doing so across game updates and IL2CPP metadata versions. `global-metadata.dat` and `GameAssembly.dll`, parsed with LibCpp2IL, locate the static roots and give the expected field names and types. Each object's field offsets come from its runtime class in memory, which is exact for generic instances, and are checked against the files (amended 2026-11-01). LibCpp2IL reads the installed build's metadata v39: a gated check parses the installed files, and the reader runs on it against the live Game client (amended 2026-11-09). <!-- (CodeRabbit, PR #21) --> The IL2CPP runtime structures this needs are laid out per Unity version (ADR-0003).
- `tests/RaidBridge.Guardrails.Tests` fails `verify` (and CI) on the Windows and Linux APIs, access rights, native imports and packages that would break this decision. It is a test, not an analyzer: a plain `dotnet build` doesn't run it.
