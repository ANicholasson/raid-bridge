# raid-bridge

[![verify](https://github.com/ANicholasson/raid-bridge/actions/workflows/verify.yml/badge.svg)](https://github.com/ANicholasson/raid-bridge/actions/workflows/verify.yml)

Reads your own RAID: Shadow Legends account data (champions, artifacts, relics, resources, Great Hall and Area Bonuses, Faction Guardians, Arena defence teams and standings, Live Arena standing, and missions) from the game client running on your computer and exports it as JSON snapshots.

Data out only. raid-bridge never injects code into the game, never writes to its memory and never automates play.

Status: early. `raid-bridge snapshot` writes a Snapshot holding the account, its champions, the artifacts and relics they wear, the artifacts and relics nobody wears, the relic stones in no socket, its resources, shards and items, its Great Hall and Area Bonuses, its Faction Guardians, its Classic, Tag and Live Arena, and its missions. It reads the running game on Linux, where the Windows game runs under Steam's Proton, with no setup and no root. There is no Windows reader yet. The format is in [docs/snapshot.md](docs/snapshot.md).

## Fair play and risk

raid-bridge reads the game's memory from outside the game, with your own user's rights and read access only, and works out where things live from the game's own files on disk ([ADR-0001](docs/adr/0001-read-only-memory-reader.md)). It refuses to run as root or with any Linux capability. It never asks for, stores or sends your Plarium login, and it sends nothing over the network: it writes a file, and you decide where that file goes.

Reading a game's memory is still a grey area under Plarium's terms of service, and Plarium can close an account at its discretion. Use raid-bridge on your own account and at your own risk.

raid-bridge is a fan project. It is not made, endorsed or supported by Plarium. RAID: Shadow Legends and Plarium are trademarks of their owners.

## Supported platforms

| | Status |
| --- | --- |
| Linux on x86-64, with the game run through Plarium Play under Steam's Proton | Supported |
| Windows | Not yet: the reader is designed for a Windows adapter ([ADR-0002](docs/adr/0002-platform-specific-memory-adapter.md)), but none exists |
| macOS | Not supported |

raid-bridge reads only the build RAID ships today: IL2CPP metadata version 39 and Unity 6000.3.18f1. When a game update changes either, raid-bridge stops with an error naming the version it doesn't know, the metadata version first, until support is added ([ADR-0003](docs/adr/0003-il2cpp-runtime-layouts-per-unity-version.md)). <!-- (CodeRabbit, PR #59) --> Building and running the tests works on Linux and Windows, which CI checks; macOS isn't checked.

## Install

You need Linux, the game running through Plarium Play under Steam's Proton, and the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). From a clone of this repository:

```sh
dotnet publish src/RaidBridge.Cli/RaidBridge.Cli.csproj -c Release -o ~/.local/share/raid-bridge
mkdir -p ~/.local/bin
ln -sf ~/.local/share/raid-bridge/raid-bridge ~/.local/bin/raid-bridge
```

To update, pull a newer version and run the same commands again. <!-- mkdir: (CodeRabbit, PR #56) -->

## Use

Start the game, log in, then run as the user who plays it:

```sh
raid-bridge snapshot
```

It writes `snapshot-<UTC time as yyyyMMddTHHmmssZ>.json` into the current folder, never overwriting a file, and prints its path. `--out PATH` writes somewhere else; inside a git clone, only `snapshot-*.json` names are ignored, so keep other names outside it. <!-- (CodeRabbit, PR #56) --> raid-bridge finds the game's files from the running game; `--game-dir DIR` names the folder holding `Raid.exe` instead.

A Snapshot carries no account id, player name or clan name. The account is identified only by a keyed hash of its id (`account.idHash`), so whatever reads your Snapshots can tell your accounts apart without learning their ids. The key is a random salt made on the first run at `$XDG_CONFIG_HOME/raid-bridge/salt` (by default `~/.config/raid-bridge/salt`). Keep it: a new salt gives the same account a new hash.

## Developing

Start with [AGENTS.md](AGENTS.md), which humans and coding agents share, then [GLOSSARY.md](GLOSSARY.md) and the [decision records](docs/adr/). `./scripts/verify.sh` runs every check CI runs and needs only the .NET 10 SDK. No test needs the game: checks against the running game are opt-in (see AGENTS.md, Checks). [CONTRIBUTING.md](CONTRIBUTING.md) says how to propose a change, and [SECURITY.md](SECURITY.md) how to report a vulnerability privately.

## License

MIT. See [LICENSE](LICENSE). The vendored [LibCpp2IL](third_party/LibCpp2IL/) keeps its own MIT license.
