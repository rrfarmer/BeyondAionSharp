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
| `3b3a6b07d` | 2026-06-06 | Fixed GM bookmarks | Pending | | |
| `796a72bd6` | 2026-06-06 | Shield mastery: damage reduce instead of block % (#142) | Pending | | |
| `6ee1e7b03` | 2026-06-07 | Removed unsupported in-game shop (closes #18) | Pending | | |
| `266a073fb` | 2026-06-08 | Load country code specific goodslists (#144) | Pending | | |
| `9ca6a3753` | 2026-06-13 | Fixed NPC states | Pending | | |
| `edeb1a997` | 2026-06-14 | Implemented aerial_spawn attribute | Pending | | |
| `031a7eec6` | 2026-06-14 | Fixed issues with `//enemy` command | Pending | | |
| `12851ab5a` | 2026-06-14 | `//moveto` improvements | Pending | | |
| `37c93df63` | 2026-06-14 | Fixed XP for NPCs with custom HP via `modifyOwnerStat()` | Pending | | |
| `9d32f6172` | 2026-06-16 | feat: instance scaling (#143) | Pending | | |
| `ee8cb3e40` | 2026-06-16 | Decrease the dispel counter only upon successful removal of a buff/debuff (#147) | Pending | | |
| `302cd8ac7` | 2026-06-21 | Fix incorrect skill mapping logic for req_dispel attributes (#148) | Pending | | |
| `59f65a956` | 2026-06-21 | Fixed buff removal when unsocketing stigmas (#BA3708) | Pending | | |
