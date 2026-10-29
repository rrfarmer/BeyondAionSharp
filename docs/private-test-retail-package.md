# Private test server retail package

The separate local test hosts select **package 13** through
[`scripts/private-test/retail-package.json`](../scripts/private-test/retail-package.json).
The manifest path resolves relative to the repository root, into the external
discovery folder. Its SHA256 is pinned separately:

`7721c21ba8fe010830630395b9f1133a4862f0ce5ba9eb95080c73d7f092713c`.

The local `Aion-NPC-Discovery/implementation/pilot25/Start-LocalTestHost.py`
launcher now calls `scripts/private-test/retail_package.py` for its child
environment. The game host receives only the production statistics manifest
and hash. Login/Chat receive no retail package. Inherited pilot navigation,
cohort, experimental retail and SIM selectors are removed from these children.
The parent environment, user/machine environment, live Docker configuration and
ordinary `dotnet run` defaults are unchanged. No routing implementation changed.

## Start the separate local hosts

Use the existing prepared private database/configuration receipts under
`Aion-NPC-Discovery/implementation/pilot25/local-play-20261023`. The launcher
requires exact private override hashes and refuses other owners' edits. It does
not build, clone databases, migrate schemas or start Docker services.

1. Check DLL ownership with `scripts/sim/sim-run-marker.ps1`. Never build while
   a private host or SIM journey holds the assemblies.
2. Run the existing `Switch-LocalTestConfig.py inspect`, then `private` only
   when all three files are original. Never replace an `other-owner-change`.
3. Set `AION_PRIVATE_TEST_RUN_DIR` in the launching process to a fresh external
   evidence directory. Start `Start-LocalTestHost.py login`, then `chat`, then
   `game`. Each host records its PID, assembly hash, log and DLL ownership mark;
   the game receipt also records the selected manifest/hash.
4. Require full bootstrap completion, login-server authentication, the game
   listener on `127.0.0.1:17777`, and the expected retail membership count.
   Login/Chat use private ports12106/11241 and bridges19014/19021. Database
   connections must use the prepared `aion_tk25_20261023_*` schemas.
5. After the owned hosts stop, restore original overrides with
   `Switch-LocalTestConfig.py original`. Retain logs, receipts and old packages.
   Stale DLL marks are handled by the existing marker script's PID/start check.

The external launcher backup and configuration-switch receipts are retained
under `implementation/private-package13-20261028-04`. The selection file and
helper can be committed; the native assets, database copies and private launcher
receipts stay outside Git. This setup is separate from `docker/RestartAll.ps1`
and the live stack. Selecting this package does not authorize a live deployment.

## Startup verification (2026-10-28)

The real private Game host loaded **11,517 map/NPC memberships**, exactly the
manifest's unique membership count, completed bootstrap, authenticated with
private Login and Chat, and listened on `127.0.0.1:17777`. No fatal startup error
was logged. The older Sky Temple navigation pilot was disabled. The manifest's
referenced assets and identity reviews passed the production loader's checks.
This verifies package loading, not an additional gameplay or all-map smoke run.

All three hosts used their prepared private database configurations. Active LS
and GS connections were observed in the private schemas; idle Chat had no active
pooled database connection. The copied GS had zero online players. Validation
hosts were stopped afterward and the original ignored override bytes restored.
Live containers and their configuration were unchanged. The selected package
persists for the next explicit private-host launch.

On 2026-10-29, focused controlled SIM checks used this same package selection
for Haramel, NTC, Fire Temple, Three Keys, Ishalgen and Altgard. All 502 HP/profile
cases and 103 buff cases passed; all 489 authored normal-attack profiles caused
final HP loss. Thirteen profiles have no authored normal. No server fix was
needed. This is separate from the socket-host startup check above; see
[the smoke results and limits](retail-stat-smoke.md#package13-smoke-of-six-requested-areas-2026-10-29).

## Verify or roll back selection

`python scripts/private-test/retail_package.py` checks the selected manifest's
mode, size and exact hash without launching anything. The server additionally
validates referenced assets and host bindings before atomic publication.

Change both manifest and SHA256 in the private selection file to choose a
different reviewed package. Stop and restart only the private hosts; publication
cannot reload in place. Reverting this selection commit restores the prior
repository state, but the backed-up external launcher must also be reconciled
before using its older pilot mode. Never silently reactivate that navigation
pilot as a statistics rollback. No schema migration is involved.
