# Offline mob-stat coverage

`tools/npc-audit/summarize_mob_stats.py` summarizes an existing C# statistics-package
export against the reviewed Asmodian content envelope. It uses the standard library
and can run without game binaries, the server, or SIM. It does not calculate combat
damage, select a runtime package, or change game data.

First generate a fresh export with the existing `npc_audit.py stats-package` command
described in `docs/npc-audit.md`. Then run:

```powershell
python -B tools/npc-audit/summarize_mob_stats.py `
  --profiles '<external export>/profiles.json' `
  --receipt '<external export>/receipt.json' `
  --census '<reviewed raw-stat-census.json>' --census-sha256 '<recorded census hash>' `
  --scope '<reviewed scope.json>' --scope-sha256 '<recorded scope hash>' `
  --out '<new external report directory>'
```

The export receipt pins the calculated profile bytes, package and host templates.
The two explicit pins select the historical census and its map envelope. Changed
NPC source identities remain visible for reconciliation. Duplicate identities,
contradictory field statuses and inconsistent membership counts fail the report.
The tool does not independently re-open every original source asset or prove that
the export was calculated from today's checkout; regenerate the C# export first.

Outputs:

- `coverage.csv`: every expected or selected map/NPC pair, source identity, HP,
  attributes, defenses, ordered normal attacks, implementation status and blockers.
- `coverage.md`: one compact table for every supported map, including empty maps
  and the separately labeled global supplement.
- `summary.json`: map counts, exception counts and hashes of inputs, calculator,
  tool and CSV. Raw source-derived output stays outside Git.

The baseline is the initialized 4.8 template; the reference is the selected 5.8
fresh, unowned profile. Neither column is a running NPC observation. Current
static placements are separated from historical/conditional membership and global
supplements. Global package lookup follows the existing catalog fallback without
inventing a spawn. Owned units still need their own scaling/ownership evidence.

`selected_offline` means the production package reader calculated that profile.
It does not mean activation, deployed behavior or verified current effects.
Missing ordinary profiles and missing owned/scaled profiles have different
blockers. Special skill boundaries are reported separately and do not erase
working base HP/attribute/normal-attack coverage. A missing baseline is never a
match. Older exports omit a 4.8 normal-attack baseline and remain readable.

New exports provide the ordinary physical 4.8 base interval using the production
`Stat2`/cap path and `StatFunctions.CalculateNpcNormalSample`, shared with the
normal combat branch. The reference supplies each ordered 5.8 entry's main/off-hand
interval using the production dual split and normal modifier calculation. Both
exclude effects, AI overrides, status and target mitigation. The summary compares
those intervals, reports element and dual-record changes separately, and makes no
claim about final HP loss. A custom AI can change this ordinary baseline.

The report preserves the two authored damage endpoints and the ordered interval.
Six records in the reviewed corpus have reversed endpoints. Native
`FUN_140df1660` and the existing `RetailNpcNormalRange` order them before drawing;
this report normalization does not patch their source or alter gameplay.

Focused verification:

```powershell
python -B -W error::ResourceWarning -m unittest discover `
  -s tools/npc-audit -p test_summarize_mob_stats.py -v
```

Exit zero means the report was generated, not that stat rollout is complete.
