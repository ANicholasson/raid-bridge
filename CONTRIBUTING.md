# Contributing

Thank you for considering it. raid-bridge is small and has hard rules, so this guide is short and points to the rest.

## Before you start

- Read the standing constraints in [AGENTS.md](AGENTS.md#standing-constraints). raid-bridge only reads: a change that writes to the game, runs code in it, sends it input, asks for a login or sends anything anywhere but the one endpoint the player configures won't be merged, however useful it is. <!-- (CodeRabbit, PR #65) -->
- A new feature, such as another part of the Account to read: open an issue first and say what you want in a Snapshot and what you would do with it.
- A bug: a pull request with a failing test is welcome straight away. If raid-bridge stopped with an error or a Snapshot is wrong, the bug-report form asks for what we need.

## Working on it

```sh
./scripts/verify.sh
```

It needs only the .NET 10 SDK and runs everything CI runs: the locked restore, the build with warnings as errors, the format check and every test, the guardrails included. No test needs the game. [AGENTS.md](AGENTS.md) says how the code is shaped, and [GLOSSARY.md](GLOSSARY.md) gives the words.

## Opening a pull request

- Conventional commits, with a subject of 50 characters or fewer.
- One change per pull request, with no drive-by refactors.
- Say what you verified and how. A change to a reader is proven against the running game; if you could only build and test it, say so plainly.
- Never attach a Snapshot, a memory capture or a screenshot that shows an account id, player name or clan name. Test fixtures are synthetic.

Reviews are kind and concrete. If something is unclear, ask in the pull request.
