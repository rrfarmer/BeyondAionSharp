# CLAUDE.md

This is a **Java → C# port** of the Aion server (the `aionemu` codebase). The Java
source is the reference implementation; the C# port exists to match its behavior 1:1.

## Golden rule: the Java source is the spec

- **Before fixing or porting anything, read the corresponding Java implementation first**
  and mirror it. Do not infer intended behavior from the C# alone — the C# may be a
  faithful port of a Java quirk (keep it) or "reworked" code that diverged (fix it to
  match Java). The Java tree is the source of truth.
- When Java and C# disagree, **Java wins** — except pure infrastructure
  (DI, lifecycle, threading, sockets), where idiomatic C# is acceptable.

**One sanctioned exception:** NPC AI behavior sourced from NCSoft's own retail AI
pattern data outranks aionemu, whose version is an approximation. Those changes are
logged in `docs/retail-ai-fidelity.md` — read it before "correcting" NPC skill,
summon, or shout data back toward Java, and add to it when making such a change.

**The retail dump is 5.8; this port is 4.8.** The exception above is about *behavior*,
not about content: retail names npcs, skills, routes and mechanics that 4.8 does not
have, and those are **boundaries to record, not gaps to close**. Never add a 4.8
template, spawn or skill so that a 5.8 pattern will fit. Every extractor under
`tools/client-extract/` refuses what this port does not have and prints the count —
keep it that way, and see section E of `docs/retail-ai-backlog.md` for what the
boundary currently costs.

## Always watch for Java ↔ C# semantic gaps

These differ in ways that silently change behavior. Check them on every port/fix:

- **Null vs throw** — Java `ResultSet.getString()`, `Map.get()`, etc. return `null`;
  the C# equivalents (`GetString()`, dictionary indexer) **throw**. A throw mid-loop can
  abort an entire multi-row load.
- **enum ordinal** — Java `ordinal()` ≠ C# `(int)enum` when the C# enum has explicit /
  non-sequential values. Use `Array.IndexOf(Enum.GetValues(typeof(E)), e)`.
- **String hash on the wire** — the client expects Java `String.hashCode()` (`31*h + c`),
  not C# `string.GetHashCode()`.
- **DateTime Kind** — Java local/UTC handling vs C# `DateTimeKind`; mismatches corrupt
  timestamps.
- **Nullable enums from XML** — a missing `@XmlAttribute` enum defaults to ordinal-0 in
  C#, not `null`.
- **Numbers/char/division/overflow** — verify semantics whenever the value goes over the
  wire or into a calculation.

The Java checkout is expected at `../aion-server` by default. Set `BEYOND_AION_JAVA_ROOT`
when it lives elsewhere.

## Where things live

| What | Path |
|---|---|
| **Java reference (the spec)** | separate sibling `../aion-server` checkout, branch `4.8` |
| **C# port** | repository root (solution `AionServer.slnx`, target `net10.0`) |
| Authoritative parity backlog | `docs/Full-Parity-Backlog.md` — read before doing parity work |
| Upstream update queue | `docs/upstream-port-log.md` and `docs/upstream-porting.md` |
| Retail AI: what is left | `docs/retail-ai-backlog.md` — **read this first** |
| Retail AI log (why each decision was made) | `docs/retail-ai-fidelity.md` — a running log, not a to-do list |
| Retail AI data (generated, do not hand-edit) | `game-server/data/static_data/pattern_tables/*.xml`, `.../guard_answers/` |
| Retail AI extractors and emitters | `tools/client-extract/` — `regen_check.py` runs the whole pipeline |
| Run / DB / setup guide | `RUNNING.md` |

## Build & test (C#)

```bash
dotnet build AionServer.slnx
dotnet test  AionServer.slnx        # golden/parity suite + unit tests
```

For upstream fixes, port one Java commit at a time and include an
`Upstream-Java-SHA` trailer. Never merge or cherry-pick Java history into `main`.

Run the stack (separate terminals, in order — details in `RUNNING.md`):

```bash
dotnet run --project src/Aion.LoginServer
dotnet run --project src/Aion.ChatServer
dotnet run --project src/Aion.GameServer
```
