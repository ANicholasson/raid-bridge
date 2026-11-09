<!-- Copied from docs/review/2026-10-fork-review.md in a private fork of raid-toolkit-sdk. It is the record of why raid-bridge is a new reader rather than a fork. -->

> **Read with care.** The findings below stand. Section 4's recommendation was written before raid-bridge existed, and these points have moved on:
>
> - The pure libraries target `net10.0`, not `net8.0` (.NET 8 support ends in November 2026). See ADR-0002.
> - The memory reader is not Windows-only by design: it is one adapter per platform behind one port, Linux (the game under Proton) first (ADR-0002). `IMemorySource` was a working name; the port is `IGameMemory` in `RaidBridge.Game`'s `Memory/`.
> - LibCpp2IL's metadata v31 support is verified on paper: `Samboy063.LibCpp2IL` 2022.1.0-pre-release.21 reads metadata versions 23 to 106. raid-bridge vendors a newer LibCpp2IL (`third_party/LibCpp2IL/`), and the installed game ships metadata v39, which a gated check parses (ADR-0001).
> - "The research report" is the RAID Data Bridge report Claude wrote for the maintainer on 2026-10-27. It is not in this repo.

# Fork review: raid-toolkit-sdk (October 2026)

A review of `main` at `491d9b6` (1 February 2024). That commit is also the upstream `v2.8.22.23238` release tag, so this is the code the last public release was built from. The goal of the fork: get account data out of RAID, with **no injection, no memory writes and no automation**.

## 1. Headline: the core dependency injects into the game

RTK reads game data through Il2CppToolkit (NuGet `il2cpptoolkit.*` 2.0.134-alpha and `Il2CppToolkit.Injection.Client` 2.0.145-alpha, source at `IL2CPP-Toolkit/Core`). Decompiling those packages shows:

- `Il2CsRuntimeContext(Process)` always constructs an `InjectionClient`. Its `ProcessHook` calls the native `InjectHook(pid)` from `Il2CppToolkit.Injection.Host.dll`, which loads that DLL into `Raid.exe`.
- The injected DLL hosts a gRPC server on localhost inside the game process. It serves `GetTypeInfo`, `CallMethod`, `CreateObject`, `PinObject` and `FreeObject`. The runtime uses `GetTypeInfo` for every type lookup, so reading cannot work without the injected DLL.
- The native DLL contains a `" (Hooked)"` string. Upstream's 2.5 release notes say the game's window title gets "(Hooked)" appended. The injected code changes the game's window.
- The runtime also exposes `WriteMemory` / `WriteProcessMemory`. The process handle is opened with `PROCESS_VM_READ` (0x10) only, so writes through that path would fail, but the API surface exists.
- The client copies the native DLL to a hidden file `$Il2CppToolkit.Injection.Host.dll` next to itself before loading it.

Called from `src/Extensibility.Host/IPC/RuntimeManager.Server.cs:102` and `:107`, for every `Raid` process found.

**Consequence:** RTK as built is not a read-only reader. It doesn't meet the fork's no-injection rule, and it's riskier with Plarium than a pure memory reader. The game-side gRPC server also appears to have no authentication, so any local process could call game methods through it. This comes from string analysis of the native DLL and wasn't verified at runtime.

**Licensing:** the `IL2CPP-Toolkit/Core` repo and its NuGet packages declare no license. Without one, we can't legally fork and modify it. This repo (MIT) and `raid-toolkit/account-extension` (MIT) are fine.

## 2. Architecture as it stands

| Project | Target | Role |
| --- | --- | --- |
| `src/Common` | net472; net6.0 | Shared helpers, websocket/named-pipe IPC, reflection-based API server, GitHub updater, registry settings |
| `src/Loader` | net472; net6.0 | Locates the game install and builds `Raid.Interop.dll` via Il2CppToolkit's reverse compiler |
| `src/Extensibility` | net6.0-windows (WinForms) | Public extension SDK: `IExtensionPackage`, account/data-context interfaces |
| `src/Extensibility.Host` | net6.0-windows | Extension loading (`AssemblyLoadContext`), account manager, game instance discovery, runtime manager |
| `src/Application/Raid.Toolkit` | WinUI 3 exe | Tray app / service, update service, extension worker processes |
| `src/Application/Raid.Toolkit.ExtensionHost` | WinUI 3 exe | Per-extension host process |
| `src/Build.Tasks`, `src/Extensibility.Tasks` | MSBuild tasks | Generate interop and `.rtkx` packages at build time |
| `src/Template`, `src/Setup` | VSIX / installer | Extension template and installer |
| `src/Tests` | xUnit | One empty test plus a test extension |

The account data (heroes, artifacts, resources, arena, great hall, battle results) **isn't in this repo**. It lives in the separate `raid-toolkit/account-extension` repo (MIT), which maps game classes such as `AppModel._userWrapper.Shards` to JSON. That repo is the most valuable reference for us.

Observations:

- Everything sits on .NET 6, which went out of support in November 2024. WinUI 3 is pinned to Windows App SDK 1.4 and preview build tools.
- Most of the code is extension-platform plumbing (process-per-extension hosting, menus, windows, notifications, installer). A data exporter needs very little of it.
- `bin/v4.7.2/Raid.Interop.dll` and `bin/v6.0/Raid.Interop.dll` are checked in. They're generated binaries tied to an old game build.
- `.vscode/launch.json` ships an "Attach to Raid.exe" debugger configuration. Don't use it: attaching a debugger to the game is further from read-only than anything else here.

## 3. Code health

Beyond the injection, the code is out of support (.NET 6, WinUI 3 on preview build tools), has effectively no tests, and has safety problems of its own in its self-updater and extension host. Upstream has had no release since February 2024. raid-bridge shares no code with it, so this public copy of the review leaves those details out.

## 4. Recommendation

Keep this fork as a **reference**, not as the base to ship.

1. Build a new, small, MIT-licensed reader that uses **only `ReadProcessMemory`** with a read-only handle. Resolve types and offsets from `global-metadata.dat` and `GameAssembly.dll` on disk, using an MIT-licensed metadata parser (Cpp2IL's `LibCpp2IL` is the first candidate; its metadata v31 support needs verifying). If that parser handles RAID's current metadata, this also gets past the v31 breakage that stopped RTK.
2. Port the data mapping from `account-extension` (MIT) into a typed snapshot model.
3. Ship a headless exporter (CLI or tray) that writes the snapshot JSON described in the research report. Drop the extension platform, WinUI host, self-updater and websocket relay.
4. _(Superseded by ADR-0002: the libraries target `net10.0`, and the reader is one adapter per platform behind one port. Kept as written for the record.)_ Put the pure parts (metadata parsing, snapshot model, mapping) in `net8.0` libraries that build and test on Linux, so CI and coding agents can work on most of the code without Windows. Keep the Windows-only memory reader behind an interface (`IMemorySource`), with recorded memory fixtures for tests. Fixtures committed to the repo must be synthetic or scrubbed, with no real account IDs, names or other personal data. Real captures stay on the local machine.
