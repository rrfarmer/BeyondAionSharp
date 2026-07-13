# Upstream port log

Baseline: Beyond Aion Java `4.8` at `f2f77fefef00aacbef4c10614c18b339bbdaa05a`.

| Upstream SHA | Date | Subject | Status | C# commit / PR | Notes |
|---|---|---|---|---|---|
| `1420a7057` | 2026-05-26 | Refactor stat caps and add dynamic limits (#138) | Ported | `codex/upstream-1420a7057-stat-caps` | Dynamic cap rules and focused player/staff/non-player tests. |
| `cf11ba99a` | 2026-05-26 | Add required dispel count and missing levels to skill_templates.xml (#139) | Ported | `main` | Exact XML/XSD carryover; C# metadata-driven dispel power and target-slot-level fix with focused tests. |
| `c84218bff` | 2026-05-29 | Legendary Symphony event | Ported | `main` | Exact event schedule and SQL carryover; C# reward table, generated syntax, and focused regression tests. |
| `f51eb98a5` | 2026-05-29 | Fixed #BA3735 | Ported | `main` | Cross-slot conflicts limited to matching shield, protect, or reflector effect types; focused conflict tests. |
| `635777e99` | 2026-06-01 | Normalize retail no-resist behavior for selected effect types (#140) | Ported | `main` | Exact 91-type no-resist normalization, cannot-miss integration, MPSHIELD conflicts, and focused tests. |
| `56d461d56` | 2026-06-01 | Fixed selling to certain NPCs | Ported | `main` | Exact NPC XML carryover; all 79 changed records verified through the functional-dialog parser. |
| `7bd02e9d2` | 2026-06-06 | Always resist nofly/fall effects with INVULNERABLE_WING (#141) | Ported | `main` | Calculation-time wing immunity for fall/no-fly effects with safe non-player application and focused tests. |
| `3b3a6b07d` | 2026-06-06 | Fixed GM bookmarks | Ported | `main` | Player-scoped bookmark DAO, client-native command/login flow, exact SQL/config carryover, and packet/access tests. |
| `796a72bd6` | 2026-06-06 | Shield mastery: damage reduce instead of block % (#142) | Ported | `main` | Exact six-skill XML carryover from block to damage reduction, with focused data parity coverage. |
| `6ee1e7b03` | 2026-06-07 | Removed unsupported in-game shop (closes #18) | Ported | `main` | End-to-end shop/toll/premium removal, compacted bridge opcodes and auth packet, plus exact SQL/config carryover; 765 tests pass. |
| `266a073fb` | 2026-06-08 | Load country code specific goodslists (#144) | Ported | `main` | Exact six-region goods-list data, country-aware cache/direct-holder selection with fallback, and client-native trade rejection; 775 tests pass. |
| `9ca6a3753` | 2026-06-13 | Fixed NPC states | Ported | `main` | Exact NPC/spawn state data, obsolete aggro-runner AI removal, and focused template/spawn regression coverage; 781 tests pass. |
| `edeb1a997` | 2026-06-14 | Implemented aerial_spawn attribute | Ported | `main` | Exact spawn/XSD data, runtime aerial flag propagation, retail state precedence, and focused model/controller tests; 786 tests pass. |
| `031a7eec6` | 2026-06-14 | Fixed issues with `//enemy` command | Ported | `main` | Centralized NPC disposition/aggro semantics, corrected command text, and covered all custom-state attackability cases; 794 tests pass. |
| `12851ab5a` | 2026-06-14 | `//moveto` improvements | Ported | `main` | Preserved active instance coordinates, added obstacle-ignoring forward movement, switched teleport correction to absolute position packets, and added focused math/encoding tests; 801 tests pass. |
| `37c93df63` | 2026-06-14 | Fixed XP for NPCs with custom HP via `modifyOwnerStat()` | Ported | `main` | XP now uses runtime MAXHP, with an isolated real-NPC/test-AI regression proving modified HP affects reward; 802 tests pass. |
| `9d32f6172` | 2026-06-16 | feat: instance scaling (#143) | Ported | `main` | Added configurable, grow-only instance NPC HP/damage scaling backed by weak instance state; shared config blob matches Java exactly, focused lifecycle/stat tests added, and all 809 tests pass. |
| `ee8cb3e40` | 2026-06-16 | Decrease the dispel counter only upon successful removal of a buff/debuff (#147) | Ported | `main` | Dispel target count now decreases only after power fully removes an effect in both affected paths; two ordered-candidate regressions added, and all 811 tests pass. |
| `302cd8ac7` | 2026-06-21 | Fix incorrect skill mapping logic for req_dispel attributes (#148) | Ported | `main` | Applied all 376 `req_dispel` metadata reassignments verbatim; XML blob matches Java exactly, representative loader assertions cover removed and corrected IDs, and all 811 tests pass. |
| `59f65a956` | 2026-06-21 | Fixed buff removal when unsocketing stigmas (#BA3708) | Pending | | |
