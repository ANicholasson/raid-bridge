# The IL2CPP runtime's own layouts are kept per Unity version

The reader never hard-codes the game's classes or field offsets: the installed game's files locate the static roots and give the expected field names and types, and each object's field offsets come from its runtime class in memory (ADR-0001). <!-- (PR #21) --> The IL2CPP runtime's own structures are different: the object header, `Il2CppString`, `Il2CppArray`, `Il2CppClass` (name, namespace, fields, field count, static fields, generic, element and declaring class, element size), `FieldInfo`, the bits of `Il2CppType` that hold a field's type code and attributes and its data, and `Il2CppGenericClass`. They are laid out by the IL2CPP runtime of a Unity version, not by the game build, and no file the game ships holds them.

The reader can't reach a static field, such as the Account's root `SingleInstance<AppModel>._instance`, without `Il2CppClass`'s static fields. For a generic instance like that one, `GameAssembly.dll` holds an `Il2CppGenericClass` record in its data, which the installed files locate, and the runtime writes the instance's runtime class into the record's cached-class slot; the reader checks that the class leads back to the record (#23). A class that isn't generic, such as `ArtifactStorageResolver` or the value type `Fixed`, has no such record. Instead `GameAssembly.dll` keeps one TypeInfo slot per class its code uses, in its writable data, holding an encoded reference to the class (from metadata v27) until the runtime first uses the class and writes its runtime class there; the installed files locate the slot by that encoding, and the reader checks that the slot leads to the class it names (#26).

So these layouts live in one small table per Unity version, chosen from the Unity version the installed game reports. Tables are keyed by the exact Unity version. The first covers 6000.3.18f1, the version Raid ships (metadata v39, #11), and any other version is refused by name, even another 6000.3 patch, because Unity has changed IL2CPP formats within a minor stream before (metadata v31 arrived in the 2022.3.33 patch). A new patch version that keeps the layouts reuses an existing table only after a check against the running Game client (decided 2026-11-01). <!-- Exact version: (PR #21) -->

## Considered Options

- **Derive the layouts at run time** by scanning memory for known patterns. Rejected: a heuristic that guesses offsets can read the wrong field and produce a plausible but wrong Snapshot, which "parse, don't trust" forbids.
- **Take them from a decompiler's generated headers.** Rejected as a source of truth: the public tools lag new Unity versions, and the reader would depend on their output format. They can serve as a cross-check when a table is written.

## Consequences

- A Unity upgrade of the game stops the reader with a named error until a table for the new version is added and proven against the running Game client.
- Each table names its source, and is tested through the fake memory port like the rest of the reader. The synthetic Game client in the tests lays its structures out from its own copy of the offsets, so a changed table fails the tests. The 6000.3.18f1 table is from that version's libil2cpp headers, with offsets computed by `offsetof` for x86-64 (#23).
- AGENTS.md's rule against hard-coded offsets covers the game's own classes; these tables are its one exception.
