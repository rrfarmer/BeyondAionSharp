# Retail NPC stat foundation

The source reader and ordinary stat projections under
`src/Aion.GameServer/Dataholders/RetailNpc` are the data foundation for the
5.8-to-4.8 mob-stat adapter. They preserve selected source identities, ordered
entries, omitted fields and explicit zero. They do not start a server, publish
a gameplay catalog, or change NPC templates merely by being present.

The maintainer's retail mob authority applies: read Java's `NpcData` and
`StatsTemplate` for integration, then use verified 5.8 semantics for matched
NPCs. The intentional differences are recorded in `retail-stat-fidelity.md`.

Included calculations cover fresh, unowned ordinary NPC construction and the
85 scalar outputs, with explicit source and modifier inputs. HP and damage
endpoints retain their distinct parser, constructor and cap stages. Attribute,
accuracy, critical, defense and resistance calculations keep their integer
widths, modifier slots and caps. Raw retail `physical_attack` and
`magical_attack` are not treated as normal-attack damage.

Source identity and calculation admission are separate. Dataset/region/build
and source hashes must match; duplicate or malformed fields do not get a generic
replacement. A calculated immutable profile is not proof of a current actor,
active buffs, owner scaling, a selected runtime package or final HP loss.
Owned/scaled units and complex skill mechanics remain explicit exceptions.
This foundation commit does not include routing, regeneration or lifecycle code.

## Offline verification

Regular tests use generated synthetic records and a synthetic 100-row ratio
table. They need no proprietary data:

```powershell
dotnet test tests/Aion.GameServer.Tests/Aion.GameServer.Tests.csproj `
  --filter 'FullyQualifiedName~RetailNpcDatasetReaderTests|FullyQualifiedName~RetailNpcOrdinaryCoreSeedProjectionTests|FullyQualifiedName~RetailNpcOrdinaryScalarEvaluationTests'
```

Two additional integration tests use the original 25 Three Keys records,
selected table and independently calculated seed/scalar vectors. These inputs
are stored outside the checkout in `ordinary-stat-fixtures.json`. The loader
requires its recorded SHA256; setting a path does not approve changed data.

```powershell
$env:AION_RETAIL_NPC_STAT_FIXTURES = '<external private-fixtures directory>'
# Run the same command above.
```

Without that variable, only those two integration tests skip. With it configured,
a missing or altered fixture fails. Keep the fixture, source snapshots and raw
extraction outside Git. The external discovery status records its location and
the actual verification results.

Before building/testing, honor the repository's journey DLL ownership rule.
No SIM, bot journey, client or game server is required by this workflow.

## Ordinary normal damage

The normal-attack scope, range, damage stages, dual endpoint split and modifier
calculation live under `Controllers/Attack/RetailNpc`. The pure range/math files
compile independently of the game server and native-memory reconstruction code.
The companion modifier reader uses the existing NPC stat-function owners and
conditions. Neither part selects a runtime cohort by itself.

```powershell
dotnet test tests/Aion.GameServer.Tests/Aion.GameServer.Tests.csproj `
  --filter 'FullyQualifiedName~RetailNpcCombatDamageStagesTests|FullyQualifiedName~RetailNpcNormalBaseMathTests|FullyQualifiedName~RetailNpcDualNormalRangeTests|FullyQualifiedName~RetailNpcStatNormalInputsTests'
```

These tests cover independent arithmetic witnesses and real host modifier
functions. A passing arithmetic test is not an enabled-mob or final-HP-loss test.

## Selected profile package and template conversion

`RetailNpcStatCatalog.Profiles.cs` contains the shared base-profile admission
used by the full parser. Its offline `ReadProfileSourceForAudit` entry point
checks the manifest, selected assets, region/build identity, reviewed NPC identity
changes, map membership and all requested source records. It returns immutable
membership metadata and calculated profiles. It does not publish runtime state,
validate optional skill packages, or verify a loaded host merely from its declared
template hash. The full runtime parser retains those responsibilities.

The shared value object retains the historical `SkyTempleNpcProfile` type name.
Its data conversion replaces HP, primaries, accuracy, critical and defense fields
with the selected calculations. In statistics-only mode it requires the original
4.8 template and retains that template's MP, legacy skill attack fields and speeds.
Missing compatibility input now fails explicitly. Each conversion owns a fresh
stats object; the shared source and original template are not mutated.

Ordered attack entries preserve explicit zero and the original element token.
A partial entry receives only the verified constructor defaults; an absent list
stays empty. The existing source-body metadata calculation is included to represent
the profile's attack inputs. This data path applies no world body, terrain or
movement behavior. Full-cohort skill-list creation is in a separate partial file
outside this base-profile commit.

The package integration test accepts four external fixture variables:
`AION_RETAIL_STATS_TEST_MANIFEST`, `AION_RETAIL_STATS_TEST_SHA256`,
`AION_RETAIL_STATS_PROFILE_EXPECTED`, and
`AION_RETAIL_STATS_PROFILE_EXPECTED_SHA256`. The expected file is the pinned
pre-refactor C# export; it is not regenerated by the test. The current external
status records its identity and full-membership result.
