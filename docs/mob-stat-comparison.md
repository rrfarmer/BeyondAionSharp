# Offline mob-stat comparison

`tools/Aion.NpcAudit` compares selected, source-pinned retail profiles with initialized
4.8 templates using the same C# source readers and normal-attack calculations as the
stat adapter. It does not bootstrap a server, publish a catalog or create actors.
Output must be outside the checkout; use a fresh directory.

```powershell
dotnet build tools/Aion.NpcAudit/Aion.NpcAudit.csproj
# Set these to your external package, its separately recorded SHA256, and a new output folder.
New-Item -ItemType Directory -Path $output
dotnet tools/Aion.NpcAudit/bin/Debug/net10.0/Aion.NpcAudit.dll --stats-package $manifest $manifestSha256 game-server/data/static_data/npcs/npc_templates.xml $output
```

Respect journey DLL ownership before building. This command reads the selected base,
normal/PvE and buff source evidence through the production parsers. Optional primary
skill, player-state and movement packages are outside this audit. Every selected
membership must match its declared 4.8 template identity. Hash or identity failures
stop the report; neither raw retail records nor output reports belong in commits.

`profiles.json` contains one row per map/NPC membership with source receipts, explicit
zero/default provenance, HP/attribute/defense comparisons, ordered attack entries and
calculated main/off-hand ranges. The 4.8 baseline is the initialized ordinary physical
attack stat and its actual 80/120 sample arithmetic. Source elements and dual attacks
remain distinct. `profiles.csv` contains comparable scalar differences. `calculation-receipt.json` pins
both outputs and the selected inputs/calculator for the coverage summarizer. Neither report
claims final HP loss, effects/AI overrides, ownership/difficulty scaling or runtime parity.

Source-bound skill IDs prove identity only. `primary_supported: null` means unassessed,
with `primary_skill_audit` explaining the scope. It must not be treated as supported or
as a discovered damage defect. Buff programs retain their own supported/compatibility
reasons. Missing required profile data remains a failure.

The C# `--request` bridge also serves the committed [Python extraction/index/batch
workflow](mob-stat-extraction.md). Optional equipment/curse experiments remain
separate from the committed mob-stat commands. To aggregate the export against an external
scope/census, use `tools/npc-audit/summarize_mob_stats.py --help`. That summary preserves
unassessed skills separately from confirmed exceptions and missing base evidence.
