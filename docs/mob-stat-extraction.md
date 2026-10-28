# Source-pinned mob-stat workflow

These tools extract and compare supported NPC stats offline. They do not run a
server, create actors, change game data or approve runtime activation. Generated
retail records and reports stay outside the checkout. Use fresh output paths.
The existing [C# comparison command](mob-stat-comparison.md) owns the calculations;
Python handles source selection, indexing, batching and package provenance.

## Extract and index

`tools/client-extract/extract_retail_npc_profiles.py --help` lists required input
paths: supplied MainServer/NPCServer/Script binaries, server XML root, independent
4.8 client NPC archive and the external NPC inventory. The source registry pins
the supplied binary identities and their seven-file NPC selection. The USA file
is selected when present; a malformed regional file fails instead of silently
falling back. The aggregate XML is a crosscheck, not a replacement source.

```powershell
python -B tools/client-extract/extract_retail_npc_profiles.py --xml-root $retailXml --inventory $inventory --client48-pak $clientNpcArchive --main-binary $mainBinary --npc-binary $npcBinary --script-binary $scriptBinary --output $newExtraction
```

The extractor preserves explicit zero, absent fields, ordered attack/skill lists,
original byte spans and source hashes. It rejects duplicate IDs and ambiguous
identities. The optional `--reviewed-abyss-bindings` accepts only the exact existing
90-row retained-ID review. It never guesses aliases or imports newer-content IDs.
Strict XML admission is not proof of the native reader's permissive behavior.
The selected historical generation predates the verified ASCII case-insensitive
skill-name join. Fresh extraction records its current tool hash and a new dataset
identity; retain old receipts and review a new generation explicitly before any
runtime admission. Do not relabel a new extraction with the old trust anchor.

Index an admitted extraction once, using independently recorded manifest and ratio
pins. The optional inventory enables historical map/family selection; it is not
proof of a current spawn. Supply the optional identity-review path and pin together
when comparing the already-reviewed retained-ID names.

```powershell
python -B tools/npc-audit/npc_audit.py index --dataset $newExtraction --manifest-sha256 $extractionManifestPin --index $newIndex --region USA --ratio $ratioXml --ratio-sha256 $ratioPin --ratio-relative pc_stat_ratio.xml --ratio-reason USA_missing_base_fallback --inventory $inventory
python -B tools/npc-audit/npc_audit.py info --index $newIndex
```

Index files have a separate pinned receipt. Source changes invalidate an index;
reindex to a new filename. Comparisons verify selected profile bytes again.

## Compare a supported mob or batch

```powershell
dotnet build tools/Aion.NpcAudit/Aion.NpcAudit.csproj
python -B tools/npc-audit/npc_audit.py compare --index $newIndex --npc 212761 --stats-only --no-build --out $newReport
```

Use repeatable/comma-separated `--npc` IDs, `--cohort three-keys`, `--map`, or
`--family`. Batches must explicitly fit 1..1024 NPCs; `--limit`/`--offset` provide
paging without silently dropping matches. `--stats-only` separates base-stat
admission from optional skill/behavior certification. `--no-build` records DLL
hashes but does not certify that the current source was compiled. Respect journey
DLL ownership even though this workflow must not start a journey.

Reports preserve blocked identities and direct AI stat-hook candidates. The AI
scan is static and direct only: inherited handlers, actual spawn AI, conditions,
scaling and effects are not proven. Damage endpoints are calculated base ranges,
not raw physical_attack/magical_attack or final target HP loss.

## Assemble an explicit reviewed stat package

`stats_manifest.py --review $review --out $newPackage` accepts a version1 USA JSON
review. Each cohort supplies a unique `id`, `map_id`, `npc_ids`, `review_basis` and
pinned `profiles`/`comparison` assets (`path`, `sha256`, optional byte length).
The profile artifact is the existing C# source bundle, including selected records,
ratio bytes and provenance; it is not a hand-written list of tuned stats. The
comparison must be an inactive `--stats-only` report from the context above.

Duplicate records, overlapping memberships, mismatched sources or missing
calculations fail. Relative asset paths resolve beside the review and become
absolute in the package. Known optional source contexts and skill records retain
their pins. XML skill assets are hashed as bytes, not decoded as JSON. Required
inputs are rehashed before atomic publication; an interrupted/failed build is not
published as a valid package. No source or existing package is overwritten.
Run the C# `--stats-package` audit on the result and reconcile its fields before
selecting any runtime configuration. Packaging performs no activation.

## Offline checks and private fixtures

The standard-library tests use synthetic source records. The retained identity
review is external, not a compressed payload in source. Set
`AION_NPC_IDENTITY_REVIEW_FIXTURE` to its file; SHA256 must equal the helper's
explicit trust anchor. Missing private input causes a visible skip; changed bytes
fail. The C# bridge integration tests additionally use `NPC_AUDIT_PRIVATE_INDEX`
and `NPC_AUDIT_EXPECTED` (the retained 25-mob acceptance fixture). These run offline.

```powershell
python -B -m unittest discover -s tools/client-extract -p 'test_retail_npc*.py'
python -B -m unittest discover -s tools/npc-audit -p test_npc_audit.py
python -B -m unittest discover -s tools/npc-audit -p test_stats_manifest.py
python -B -m unittest discover -s tools/npc-audit -p test_ai_stat_hooks.py
```
