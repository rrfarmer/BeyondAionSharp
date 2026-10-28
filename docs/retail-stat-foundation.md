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
