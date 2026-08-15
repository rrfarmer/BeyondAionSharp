# client-extract

Python tools for reading Aion game-client data, used to source retail behavior
facts that neither the C# port nor the Java reference carries.

Pure standard library — no dependencies, and no external binaries. (Tools such
as `AION-Encdec` wrap `pak2zip.exe` / `AIONdisasm.exe`; these scripts implement
both formats directly, so that toolchain is not needed.)

## Scripts

| Script | Purpose |
|---|---|
| `aionpak.py` | Read/extract a `.pak` archive. Every entry is CRC32-verified. |
| `bxml.py` | Decode the binary XML (magic `0x80`) most `.pak` members contain. |
| `index_paks.py` | Index every entry name across a client install, reading only archive directories. |
| `build_ai_binding.py` | Join the client's per-NPC `ai_name` against an NpcAIPatterns dump to produce the pattern → `npc_id` table. |
| `audit_missing_adds.py` | Report retail encounter adds our server never spawns. |
| `audit_missing_ai.py` | Report NPCs we spawn that have a real retail fight and no AI class at all. |
| `audit_dead_shouts.py` | Report NPCs left mute because their lines sit on a twin we never spawn. |
| `audit_hp_phases.py` | Report hand-written `HpPhases` thresholds that disagree with the retail pattern. |
| `audit_skill_index_reach.py` | Report which timer-driven bosses address skill indices beyond our own list, and so cannot be ported faithfully. |
| `triage_missing_adds.py` | Bucket the missing adds by how retail spawns them, which decides what each costs. |
| `summarize_pattern.py` | Print one pattern as a dense digest, branches in evaluation order. |
| `rotation_table.py` | Print a timer-driven boss's rotation as one row per branch, ready to transcribe. |

`aionpak.py` and `bxml.py` are importable libraries as well as CLIs.

## Usage

```bash
python aionpak.py "C:/Program Files (x86)/Beyond Aion/Data/Npcs/Npcs.pak" --list
python aionpak.py "C:/Program Files (x86)/Beyond Aion/Data/Npcs/Npcs.pak" ./out
python bxml.py ./out/client_npcs_monster.xml decoded.xml
python index_paks.py "C:/Program Files (x86)/Beyond Aion" pak_index.tsv
python build_ai_binding.py "C:/Program Files (x86)/Beyond Aion" "D:/path/to/5.8 AI Patterns" binding.tsv
python audit_missing_adds.py "C:/Program Files (x86)/Beyond Aion" "D:/path/to/5.8 AI Patterns" binding.tsv
python audit_dead_shouts.py  "C:/Program Files (x86)/Beyond Aion" "D:/path/to/5.8 AI Patterns" binding.tsv
python audit_hp_phases.py    "D:/path/to/5.8 AI Patterns" binding.tsv
python summarize_pattern.py  "D:/path/to/5.8 AI Patterns" IDYun_Nmd3
```

`summarize_pattern.py` is the one to start from when porting a boss: a fight
runs several hundred lines of XML that is mostly repeated placement fields, and
the digest collapses it to a screen with branches in evaluation order. Two
markers matter in its output -- `!` is a `set_flag_var` sitting in a branch's
*conditions*, which is a test-and-set and makes that branch fire once, and `?`
is an ordinary condition. A threshold branch without a `!` fires on every tick
below its value, not once. Pass `--raw` for the unabridged XML.

The three audits produce candidates for review, not auto-fixes; see
`docs/retail-ai-fidelity.md` for what to watch for when working their output.

`audit_missing_adds.py` cross-references each live NPC's retail pattern against
our spawn data and reports adds nothing ever brings into the world. Its
"can we spawn this?" check includes a regex sweep of handler code, which is an
approximation — spot-check a finding with a repo-wide grep before acting on it.
The sweep follows literal ids, ids reached through a named constant or an id
array, ids computed as `GetNpcId() + N` (resolved per NPC using that AI class),
and any method whose name contains `Spawn` (so `RndSpawnInRange` counts, not just
`Spawn`). It does **not** follow an id returned from a method, so a handler that
picks its add from a `List<int>` helper still reports as missing.

**Naming convention this depends on.** The handler sweep finds code-driven spawns
by looking for a call to a method whose name *contains* `Spawn`. A helper that
places an NPC should therefore be named `SpawnSomething`, not `OpenSomething` —
`UnstableYamennesAI.SpawnGate` was briefly called `OpenGate`, and three gates the
class demonstrably spawns went invisible to the audit until it was renamed.

**The total is not monotonic.** Teaching the sweep to resolve more spawns can
*raise* the missing-add count, because an NPC that becomes spawnable brings its
own pattern's adds into scope for the first time. A rise after a sweep fix is the
tool seeing more of the world, not a regression.

`build_ai_binding.py` reads `Npcs.pak` directly and accepts the AI-pattern dump
as shipped (UTF-16 or UTF-8) — no preprocessing step.

## Formats

**`.pak`** — a standard ZIP with two obfuscations:

1. The three 4-byte `PK` record signatures are XOR'd with `0xFF`
   (`50 4B 03 04` → `AF B4 FC FB`, and likewise for the central directory and
   end-of-central-directory records). All other header fields are plaintext;
   compression is raw deflate or store; data descriptors are never used.
2. Only the **first 32 bytes** of each entry's compressed payload are XOR'd
   against a fixed key table, at an offset derived from the compressed size —
   v2 (retail): `table[(csize & 0x3FF) + i]`; v1 (2008 open beta):
   `table[(csize & 0x1F) * 32 + i]`. Everything past byte 32 is untouched
   deflate, which is why a naive inflate fails instantly with
   "invalid code lengths set".

The version is detected per archive by trial-decrypting the first entry and
checking its CRC32. A few archives hold plain, unobfuscated ZIP entries
(third-party repacks); those are detected per entry and passed through.

The client's `Pub.key` is unrelated to any of this — it is the RSA-1024
login/patcher key.

**Binary XML** — `u8 0x80`, a LEB128 varint string-table size in bytes, a
UTF-16LE NUL-separated string table (a string *index* is a character offset, so
the byte offset is `index * 2`), then nodes of `varint name-index, u8 flags`
where bit 0 means a text value index follows, bit 1 an attribute count and
key/value index pairs, bit 2 a child count and child nodes. Some members
(`.txt`) are plain text, so branch on the magic via `bxml.is_binary_xml`.

## What the client does and does not have

`Data/Npcs/Npcs.pak` yields `client_npcs_monster.xml` (44,381 NPCs) and
`client_npcs_npc.xml` (18,906). Each record carries `<ai_name>` — the retail AI
pattern name — which is what makes the binding table possible: **49,134 NPCs
bind to 7,589 distinct patterns** in the 5.8 dump. Unbound `ai_name` values are
overwhelmingly engine built-ins (`NPC`, `NoAction`, `Summoned`, `Resurrect`)
rather than missing scripts.

The client does **not** carry per-NPC skill lists. An index of all 525,657
entries across all 3,332 archives found none: the client never needed them,
since the server dictates what an NPC casts. So `SKILLI_INDEX_N` references in
AI patterns cannot be resolved from client data and must be inferred per NPC
(distinctive skill names/effects, the `skill_no` attribute in `npc_shouts.xml`
which equals index + 1, and what the pattern does with the skill).

## `keys/`

`pak_table_v1.bin` (1024 bytes) and `pak_table_v2.bin` (1056 bytes) are the XOR
key tables, published in open-source extractors since 2008
(`davidsiaw/aiondb`, `zzsort/monono2`). They contain no NCSoft code.
