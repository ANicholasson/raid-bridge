# A Snapshot section nothing is worked out from is written as null when it can't be read

A Snapshot is read in one pass, and until now any value the reader refused stopped the run and wrote no file. That is right for a value other values are worked out from: a Champion's Total stats are wrong if one of the bonuses behind them is guessed or dropped. It is wrong for a section nothing reads from. One Mission whose text a game update left untranslated cost the player their Champions, Artifacts and Resources too, and an update that moved such a class would do it to every player at once (https://github.com/ANicholasson/raid-bridge/issues/47).

So a section nothing else in the Snapshot is worked out from is optional: when the reader refuses it, the section is written as `null`, whole, and the Snapshot's top-level `problems` lists it with the refusal's message, which names the type, the field and the address as a failed run's does. The file is still written, and the run ends with exit code 3, apart from success (0) and failure (1), so a script can tell the three apart. The optional sections are the Area Bonuses, the Live Arena, the Saved teams and the Missions (decided 2026-11-15; the Saved teams the same day, https://github.com/ANicholasson/raid-bridge/issues/57). Every section a Champion's stats count (the Champions, Artifacts and Relics, the Great Hall's Affinity bonuses, the Faction Guardians and the Classic Arena) still fails the run, and so, for now, do the Resources, Shards, Items and the Tag Arena. Only a refusal of the game's data or files counts: a bug in raid-bridge, or the Game client exiting during the read, still fails the run.

Nothing is relaxed. An optional section is refused by the same check with the same message as before; only what happens to the rest of the Snapshot changes. `schemaVersion` stays 1: `problems` is an added field, and a section is `null` only when `problems` lists it (the Live Arena was already `null` when the game holds none).

## Considered Options

- **Leave out only the entry that fails, such as one Mission.** Rejected: whatever reads the Snapshot couldn't tell a Mission left out from one the Account doesn't have, which is the silent skip "parse, don't trust" forbids.
- **Write an empty section instead of `null`.** Rejected for the same reason: `[]` or `{}` already means the Account has none.
- **Report the problem on stderr only.** Rejected: the Snapshot is passed on to other services, which never see stderr, so the file itself has to say that a section failed rather than was empty.
- **Make every section optional.** Rejected: a Snapshot whose Champions are `null` is no use, and a section other values are worked out from would make those values wrong without saying so.

## Consequences

- A new section is required unless it is added to the optional ones on purpose, with the reason that nothing is worked out from it. A section that later feeds another, such as one a Champion's stats come to count, leaves the optional ones.
- The reader reads each optional section on its own, after every required one, so one refused section costs only itself.
- A run that exits 3 still wrote a file; one that exits 1 wrote none.
