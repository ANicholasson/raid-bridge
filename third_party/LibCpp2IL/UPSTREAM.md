# LibCpp2IL

Reads IL2CPP's `global-metadata.dat` and the game binary. `RaidBridge.Game`'s `Metadata/` folder wraps it, and nothing else references it (spec #20).

- Source: <https://github.com/SamboyCoding/Cpp2IL>, folder `LibCpp2IL/`, and the repository's `LICENSE` (MIT).
- Pinned commit: `b5ad444b82267cb1e4b88b8b373c008105bdea52` on `development` (2026-09-12).
- Why vendored, not the nuget.org package: the package (2022.1.0-pre-release.21, from `58fc404`) lags `development` by eight months and misses later fixes. AGENTS.md's vendoring rule (decided 2026-11-01).

## Dropped

The Game client ships a Windows PE binary, so the other binary formats are dropped (spec #20):

- WebAssembly: `Wasm/`, the `WasmDisassembler` project reference and `LibCpp2IlBinaryRegistry.WasmRemappedDynCallFunctions`.
- ELF: `Elf/`, `PE/ElfProgramHeaderFlags.cs` (an ELF type kept under `PE/` upstream), and the ARM helpers only ELF used, `ArmUtils.cs` and `MiniArm64Decompiler.cs`.
- Nintendo Switch: `NintendoSwitch/` and `Lz4DecodeStream.cs`, which only NSO decompression used.
- Mach-O: `MachO/`, universal binaries included.

## Patches

Each patch is its own commit, marked at the site with `raid-bridge patch: <why>`.

- `LibCpp2IlBinaryRegistry.cs`, `BinarySearcher.cs` and `LibCpp2IL.csproj`: the references to the dropped formats are removed.
- `LibCpp2IL.csproj`: one target, the repo's `net10.0`, so PolySharp and System.Memory, which only served the older targets, are gone.
- `LibCpp2IL.csproj`: AssetRipper.Primitives takes its version from `Directory.Packages.props`, as the repo's central package management requires.
- `LibCpp2IL.csproj`: never packed, since it's built only as a project reference.
- `LibCpp2IL.csproj`: `IsTrimmable` and `IsAotCompatible` are dropped. They add a `Microsoft.NET.ILLink.Tasks` reference pinned to the installed SDK's patch version, which breaks the locked restore on any other SDK, and raid-bridge is neither trimmed nor AOT-compiled.

Outside this folder, `third_party/Directory.Build.props` turns off the repo's style analyzers for vendored code, and `scripts/verify.sh`'s format check skips `third_party/`.
