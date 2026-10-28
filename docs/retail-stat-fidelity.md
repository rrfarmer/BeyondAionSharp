# Retail NPC stat fidelity

Maintainer authority: supplied 5.8 retail binaries and correctly selected data
win for matched NPC stats; Java is read for integration. Preserve 4.8 content,
protocol and unrelated player/PvP rules. Local scoped commits were authorized
by the 2026-10-28 mob-stats goal; pushes and deployments remain unauthorized.

## Ordinary construction and scalar projection

The Java `NpcData` loader fills several zero-valued stats from level/rating/rank
tables. Java's shared stat layer also clamps primary attributes to a player-style
minimum. Those rules remain the legacy baseline. The retail source projections
retain explicit zero, use the native constructor/table stages where the field is
absent, and evaluate the reviewed NPC scalar channels with their native widths
and caps. An unknown source/context remains unsupported.

| Area | Retail rule preserved | Evidence retained outside Git |
|---|---|---|
| Source selection | Exact selected record/build/region; ordered attacks and skills; absent versus explicit zero | Existing extractor manifest and source receipts |
| HP | Separate parser upper cap, initial HP copy and postparse lower bounds | `phase2/ordinary-seed-evidence`; source-step instruction anchors in `RetailNpcOrdinaryCoreSeedProjection` |
| Normal attack inputs | Native-width min/max endpoint copies; raw physical/magical attack fields are excluded from this damage basis | Same seed evidence and frozen 25-record vectors |
| Primary/defensive stats | Retain NPC zero copies and the reviewed integer modifier/cap order rather than silently applying player defaults | Existing ordinary scalar contracts and 25-record scalar vectors |

The pinned MainServer identity is
`da1ee5633d65a7ebb436b29d15ca24e46ab4f267449384d177bf20d2d26eb8f1`.
The ordinary seed evidence covers 10,289 byte-checked instructions in 30 cached
functions. Individual steps retain their function/address provenance in code.
These static calculations are source-backed reconstructions, not observations
of every runtime actor or special state.

The source reader/projections alone do not activate these differences. The
existing broader runtime adapter, package selection and combat integration must
be reviewed separately. Synthetic unit inputs are not retail data. The full
source fixtures and their hash-checked integration tests remain external.
