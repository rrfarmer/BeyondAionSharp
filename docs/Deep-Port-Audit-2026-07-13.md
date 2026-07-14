# BeyondAionSharp Java-to-C# Deep Port Audit

**Audit date:** 2026-07-13

**C# baseline:** `main` at `c677d77c7122b13126420840117ddeaf33fafa2e`

**Java specification:** `4.8` at `59f65a9561bfa655eb24134da88ba3121c66ee8a`

**Java checkout used:** `C:\Users\ryanf\Documents\GitHub\aion-server`

## Remediation update

The detailed findings below preserve the state of the audited C# baseline. Same-day remediation then implemented every scoped code fix and added regression coverage. The current working tree has **13 Verified findings** and **five Code-complete findings**; no finding remains Not started, In progress, Blocked, or Accepted.

| Current status | Findings | Meaning |
|---|---|---|
| Verified (13) | BA-004, BA-007 through BA-018 | Scoped implementation, focused evidence, warning/build gate, and full solution tests pass |
| Code complete (5) | BA-001, BA-002, BA-003, BA-005, BA-006 | Implementation and automated/live-data evidence pass; a multi-process or in-world release journey still requires operator QA |

Final automated and live-data evidence:

- `dotnet test AionServer.slnx --no-build --no-restore -v:minimal`: **1,002 passed, 0 failed, 0 skipped** (Commons 84, Chat 35, Login 132, Game 751).
- Warning ratchet: **4,318 unique warning sites across 21 codes**, unchanged on its verification rebuild. A conventional clean rebuild reports **4,325 warnings, 0 errors**, down 34 from the audited baseline; `CS0184`, `CS0472`, and `CS8605` are enforced at zero. Local and CI builds are pinned to .NET SDK `10.0.301` so compiler drift cannot silently invalidate the inventory.
- `scripts/parity/check_fidelity.py`: passed with zero new slop and the one existing approved god-class exception.
- Isolated MySQL 8 validation under `America/New_York` passed winter/summer Login and production Game DAO timestamp round-trips, the schema-valid NULL cases, and a complete fresh Game schema import. The isolated container and port 3307 listener were removed afterward.
- BA-010/BA-018 Java-semantic suites pass **77/77**. All 83 textual enum parse sites were triaged; all 97 shipped `counter_skill` values are pinned (67 exact names, 30 Java-null comma values).
- A final adversarial protocol review removed peer-sized MAC/HDD ban-list allocations and rejects positive counts that cannot fit in the authenticated frame. Valid entries remain iterative and negative counts retain Java's empty-loop behavior; the production-lenient inbound protocol suite passes **9/9**.
- Final source/diff checks found no raw command/downstream primitive parsers, no instant-valued `GetDateTime` use (the sole remaining call reads SQL `DATE`), no broken local document links, and no whitespace errors.

Production promotion should still wait for these operator-owned journeys:

1. A two-Game-Server transfer plus live duplicate-login, kick, reconnect, grant, ban, and full MAC/HDD synchronization/restart flow.
2. A retail-client Chat login covering success, gag replay, disconnect/timeout, and duplicate-request behavior.
3. One in-world siege gate repair and one automatic fortress assault using the restored nested indexes.

The evidence-by-finding checklist is maintained in [Deep-Port-Audit-Remediation-Tracker.md](Deep-Port-Audit-Remediation-Tracker.md).

## Executive assessment

The port is broad and substantially implemented, but the audited baseline was **not production-ready**. This audit found three release-blocking cross-server defects, five high-risk subsystem/data/setup/mechanic defects, and several medium-risk Java/C# semantic mismatches. The most important issues are not missing game classes; they are narrow semantic and lifecycle differences that the existing parity tests did not exercise.

The highest-risk confirmed defects are:

1. Character-transfer requests omit the GS-to-LS opcode and are decoded as unrelated commands, which can mutate the wrong login-server state.
2. The production Chat authentication path never registers a response callback, so the Chat token is discarded and the client never receives `SM_CHAT_INIT`.
3. The Game Server drops most Login Server response opcodes. Kick/duplicate-login enforcement, fast reconnect, access changes, bans, persisted MAC/HDD bans, and player transfers are incomplete or ineffective.
4. Siege XML child callbacks do not run. Gate-repair lookup tables and Balaur fortress-assault wave tables remain empty even though those mechanics are enabled in the shipped configuration.
5. The LS and Chat bridges do not retry/reconnect, and a single packet-handler exception can terminate the shared bridge until the Game Server is restarted.
6. Database URL timezone options are discarded, while individual persistence paths disagree about `DateTimeKind`. On the current non-UTC deployment timezone this shifts player timestamps by four or five hours and would similarly shift hardware-ban timestamps once BA-003's missing ban-list synchronization is restored.
7. The shipped Game Server schema has invalid SQL in the `bookmark` table, so a fresh `aion_gs` database cannot be initialized at all.
8. .NET enum parsing treats comma-separated names as a bitwise combination even for a non-flags enum. Thirty shipped combat-skill templates consequently become `RESIST` counters where Java JAXB leaves the invalid single-enum field null.

Recommendation after remediation: the confirmed code defects and schema blocker are corrected, but keep the production block until the three operator-owned runtime journeys above pass.

## Results at a glance

| ID | Severity | Confidence | Area | Result |
|---|---|---:|---|---|
| BA-001 | Critical | Confirmed | Player transfer | Transfer packet omits opcode `0x0D` and is misrouted by LS |
| BA-002 | Critical | Confirmed | Chat authentication | Chat response callback is absent on the real client path |
| BA-003 | Critical | Confirmed / partly known | GS↔LS protocol | GS drops kick, reconnect, control, ban, MAC/HDD, and transfer responses; account resync is absent |
| BA-004 | High | Confirmed | Cross-server lifecycle | No initial retry/reconnect; one handler exception can permanently kill a bridge |
| BA-005 | High | Confirmed | Siege mechanics / XML | Nested siege callbacks and a child XML field binding are missing |
| BA-006 | High | Confirmed on non-UTC hosts; ban impact masked by BA-003 | Persistence / time | JDBC options are dropped and UTC/local interpretations disagree |
| BA-007 | Medium | Confirmed conditional/current-data mismatch | Seasonal events | Missing XML theme becomes `NONE`, suppressing a themed event during overlap |
| BA-008 | Medium | Confirmed for schema-valid NULLs | Persistence | Typed getters throw where JDBC returns primitive defaults, truncating loads |
| BA-009 | Medium | Confirmed | Configuration | Bootstrap and runtime parse the same Java properties differently |
| BA-010 | Medium | Confirmed | Commands / packet builder | Java numeric grammar, overflow, and enum-name semantics were not preserved |
| BA-011 | Medium | Confirmed semantic mismatch | Rewards / calculations | `Math.Round` uses banker's rounding, unlike Java `Math.round` |
| BA-012 | Medium | Confirmed mismatch; overlap-dependent impact | Zones | Zone ordering uses randomized C# string hashes instead of Java hashes |
| BA-013 | Medium | Confirmed resilience gap | Static data | Holder-specific load errors silently replace a subsystem with empty data |
| BA-014 | Medium | Confirmed | Cross-server fidelity | Several smaller auth/name/result/framing divergences remain |
| BA-015 | Low | Confirmed edge paths | Nullable values | Two nullable Java results are force-unwrapped in C# |
| BA-016 | Improvement | Confirmed | Quality gates | 4,359 compiler warnings and shallow boundary tests hide high-signal defects |
| BA-017 | High | Confirmed by live MySQL import | Database setup | `aion_gs.sql` is syntactically invalid at the `bookmark` foreign key |
| BA-018 | High | Confirmed in shipped data and Java JAXB oracle | Combat skills / enum boundaries | 30 counter skills are misread as `RESIST`; other name-only enum boundaries accept numeric values |

Severity means:

- **Critical:** can corrupt cross-server state, bypass enforcement, or break a primary player flow in normal operation.
- **High:** disables a major subsystem or causes persistent incorrect state under a realistic deployment condition.
- **Medium:** produces incorrect behavior under a specific mechanic, input, schema-valid row, or configuration.
- **Low:** defensive parity issue that needs malformed or inconsistent state to trigger.

## Scope and method

The Java `4.8` implementation was treated as the specification. Every finding below was checked against the paired Java implementation; C#-only oddities that also exist in Java were not reported as port defects.

The audit covered:

- Aion client packet registration and packet read/write shapes.
- Game Server ↔ Login Server and Game Server ↔ Chat Server bridges.
- Static XML loading, nested JAXB callbacks, and nullable XML attributes.
- Siege, event, arena, command, and zone mechanics.
- MySQL/JDBC null and timestamp behavior.
- Fresh Login/Game database schema initialization.
- Configuration parsing and database option propagation.
- Exact-name enum conversion at configuration, XML, command, and transfer boundaries.
- Existing parity backlog and automated test coverage.

Baseline checks:

- `dotnet test AionServer.slnx --no-build`: **812 passed, 0 failed** (Commons 55, Chat 31, Login 120, Game 606).
- Clean `dotnet build AionServer.slnx -t:Rebuild -v:minimal`: **0 errors, 4,359 warnings**.
- `scripts/parity/check_fidelity.py`: baseline passed with zero unapproved slop and one already-known god-class exception.
- Static comparison found exact parity for the main Aion client opcode/state table (195/195), active server opcode table (236/236), canonical client packet set (189/189), and the audited packet primitive read/write sequences.

This was a code/data audit plus automated-test run. It did not run a live retail client through a multi-process, multi-GS database environment. The critical findings do not depend on such an environment to establish the divergence, but the proposed end-to-end tests should be run as release gates after fixes.

## Detailed findings

### BA-001 — Character-transfer packets omit opcode `0x0D`

**Severity:** Critical · **Status:** Confirmed current defect; newly identified

The Java packet constructors call `super(13)`, and `LsServerPacket` writes that opcode before the transfer action. The C# packet begins directly with `_type`; neither its base class nor its frame codec inserts a command opcode.

Evidence:

- C#: [`SM_PTRANSFER_CONTROL.cs`](../src/Aion.GameServer/Network/LoginServer/ServerPackets/SM_PTRANSFER_CONTROL.cs#L64) begins the payload with `buffer.WriteC(_type)`.
- C#: [`LoginServerPacket.cs`](../src/Aion.GameServer/Network/LoginServer/LoginServerPacket.cs#L8) and [`ServerPacketFrameCodec.cs`](../src/Aion.GameServer/Network/ServerPacketFrameCodec.cs#L8) do not prepend an opcode.
- C#: [`GsClientPacketFactory.cs`](../src/Aion.LoginServer/Network/GameServer/GsClientPacketFactory.cs#L10) expects transfer control at opcode `13`.
- Java: `game-server/src/com/aionemu/gameserver/network/loginserver/serverpackets/SM_PTRANSFER_CONTROL.java:52-80` calls `super(13)` in every constructor.
- Java: `game-server/src/com/aionemu/gameserver/network/loginserver/LsServerPacket.java:30-37` writes the opcode.

Impact:

Transfer action IDs `1..9` are treated as top-level GS-to-LS packet opcodes. For example, action `2` looks like reconnect-key, `3` like account-disconnected, `4` like account-list, and `6` like ban. A transfer can therefore do more than fail: it can mutate unrelated LS account/ban state or be rejected/misparsed while the factory reads the wrong packet shape.

Required fix and validation:

- Encode `0x0D` before the transfer action in every constructor path.
- Add byte-golden tests asserting `[0D, action, ...]`.
- Round-trip every constructor through `GsClientPacketFactory` and assert `CmPlayerTransferControl` is selected.
- Run a real two-GS transfer through the Java-reachable control flow (GS actions `1..4`, LS responses `20..23`) and parser tests for Java's dormant GS response cases `24..28`. Java 4.8's LS does not accept GS actions `5..9` or emit responses `24..28`; do not invent a C#-only extension without an explicit upstream decision.

The authoritative backlog currently describes this transfer packet as audited; that claim should be corrected.

### BA-002 — The production Chat authentication response is discarded

**Severity:** Critical · **Status:** Confirmed current defect; newly identified

The actual Aion client handler invokes a synchronous-looking Chat request method that only sends the request. It never registers the callback consumed by the inbound Chat authentication response. A separate async method does register the callback, but it has no production caller.

Evidence:

- C#: [`CM_CHAT_AUTH.cs`](../src/Aion.GameServer/Network/Aion/ClientPackets/CM_CHAT_AUTH.cs#L22) calls `SendPlayerLoginRequest`.
- C#: [`ChatServer.cs`](../src/Aion.GameServer/Network/ChatServer/ChatServer.cs#L55) sends `SmPlayerAuth` without adding `_playerAuthCallbacks`.
- C#: [`ChatServer.cs`](../src/Aion.GameServer/Network/ChatServer/ChatServer.cs#L157) registers the callback only in the unused `SendPlayerLoginRequestAsync` path.
- C#: [`ChatServer.cs`](../src/Aion.GameServer/Network/ChatServer/ChatServer.cs#L222) only delivers the token when that callback exists.
- Java: `game-server/src/com/aionemu/gameserver/network/chatserver/clientpackets/CM_CS_PLAYER_AUTH_RESPONSE.java:32-47` finds the player, sends `SM_CHAT_INIT`, and propagates gag state directly.

Impact:

The Chat Server can authenticate the player and return a valid token, but the Game Server drops it. The game client never receives `SM_CHAT_INIT`; Chat initialization and gag propagation do not complete.

Required fix and validation:

- Make the client path register a response continuation or mirror Java's direct player lookup/response handling.
- Inject a real `0x01` Chat response after `CM_CHAT_AUTH` and assert the client receives the exact `SM_CHAT_INIT` bytes.
- Cover both normal and gagged players, callback timeout, disconnect, and duplicate requests.

### BA-003 — The GS↔LS response surface is still incomplete

**Severity:** Critical · **Status:** Confirmed; the broad gap is known in backlog §I1, but current details are stale

The C# Game Server dispatch currently handles only opcodes `0x00`, `0x01`, `0x08`, and `0x0B`; the paired Java factory maps the complete active response set. The Login Server already emits several packets the Game Server drops.

Core evidence:

- C#: [`LoginServer.cs`](../src/Aion.GameServer/Network/LoginServer/LoginServer.cs#L349) warns and drops every unrecognized LS opcode.
- Java: `game-server/src/com/aionemu/gameserver/network/loginserver/LsClientPacketFactory.java:26-38` maps the active response set.

Missing flows:

| Opcode | Java behavior | Current C# impact |
|---:|---|---|
| `0x02` | Kick an account, optionally with duplicate-login notification | Online bans and duplicate-login eviction do not remove the existing GS session |
| `0x03` | Complete fast reconnect and send the client reconnect key | LS mutates reconnect state, but the client never receives the key |
| `0x04` | Apply access/membership control response and notify participants | DB state can change while an online account remains stale; no result message |
| `0x05` | Deliver ban success/failure | Admin receives no result; online target may remain connected |
| `0x09` | Load persisted MAC bans | Fresh GS starts with an empty local manager, allowing persisted bans to be bypassed |
| `0x0A` | Load persisted HDD bans | Same enforcement bypass for HDD bans |
| `0x0C` | Parse transfer response cases `20..28` | Existing `PlayerTransferService` methods are unreachable from the bridge; Java's LS currently emits only `20..23` |

Account resynchronization is also absent. After successful bridge auth, Java immediately sends logged-in account IDs with opcode `0x04`; the C# Game Server only changes its state and logs success. The C# Login Server already has the account-list, MAC-ban, and HDD-ban response logic, so the backlog statement that those LS facilities are absent is no longer accurate.

Additional evidence:

- C# LS emits kick packets from [`GameServerRegistry.cs`](../src/Aion.LoginServer/Services/GameServerRegistry.cs#L91) and ban handling in [`GameServerConnection.cs`](../src/Aion.LoginServer/Network/GameServerConnection.cs#L330).
- C# LS emits reconnect, control, ban, and ban-list responses from [`GameServerConnection.cs`](../src/Aion.LoginServer/Network/GameServerConnection.cs#L218).
- C# LS emits transfer responses from [`SmPlayerTransferResponse.cs`](../src/Aion.LoginServer/Network/GameServer/ServerPackets/SmPlayerTransferResponse.cs#L43).
- C# has transfer operations in [`PlayerTransferService.cs`](../src/Aion.GameServer/Services/Transfers/PlayerTransferService.cs#L43), but no inbound dispatch reaches them.
- Java sends the account list after auth in `CM_GS_AUTH_RESPONSE.java:33-39` and defines its layout in `SM_ACCOUNT_LIST.java:24-34`.

Required fix and validation:

- Port the missing LS packet classes/factory dispatch and the post-auth account-list send as one coherent protocol slice.
- Add factory state/opcode tests for every active opcode, including rejection in illegal states.
- Add loopback integration tests for kick, duplicate login, reconnect, grant, ban, MAC/HDD enforcement, and the Java-reachable two-GS transfer flow. Cover dormant response cases `24..28` at the parser/dispatcher level.
- Update `docs/Full-Parity-Backlog.md` §I1 to the current `0..13` maps and LS implementation status.

### BA-004 — Cross-server links do not recover and lack packet fault isolation

**Severity:** High · **Status:** Confirmed current defect; newly identified

Both outbound bridges make a one-shot connection attempt. Once the reader task ends, the singleton cannot be restarted cleanly. In addition, exceptions are contained at the connection/read-loop boundary instead of around one packet as in Java.

Evidence:

- C#: [`OutboundLinkHostedService.cs`](../src/Aion.GameServer/Services/OutboundLinkHostedService.cs#L48) attempts each link once and only logs failure.
- C#: [`LoginServer.cs`](../src/Aion.GameServer/Network/LoginServer/LoginServer.cs#L277) will not start again while `_readerTask` remains assigned; its loop-level catch/close is at lines 317-346 and 457-478.
- C#: [`ChatServer.cs`](../src/Aion.GameServer/Network/ChatServer/ChatServer.cs#L85) has the same one-shot lifecycle.
- C#: [`GameServerConnection.cs`](../src/Aion.LoginServer/Network/GameServerConnection.cs#L101) has no per-packet handler containment; [`BaseSocketServer.cs`](../src/Aion.Commons/Network/Server/BaseSocketServer.cs#L237) closes at the connection-loop boundary.
- Java LS connection management retries initial connection and reconnects after loss in `game-server/.../network/loginserver/LoginServer.java:60-105`.
- Java catches individual handler failures in `login-server/.../network/gameserver/GsClientPacket.java:27-33` and `game-server/.../network/loginserver/LsClientPacket.java:25-31`.

Impact:

- Starting LS/Chat after GS, or restarting either service, leaves authentication or Chat unavailable until GS is restarted.
- Pending `_loginRequests` are not closed and cleared on LS loss.
- A transient database/service exception in one control or ban packet can take down the shared bridge. Combined with no reconnect, the outage persists.

Required fix and validation:

- Implement a cancellation-aware retry/reconnect state machine with bounded backoff, reauthentication, and post-auth account resync.
- Clear/fail pending requests on disconnect.
- Catch/log failures around one decoded packet while reserving connection teardown for framing/protocol/transport failure.
- Failure-injection test: start GS before LS/Chat, bring dependencies online, restart them, and throw once inside a packet handler; subsequent valid packets must still work.

### BA-005 — Siege child JAXB callbacks never run

**Severity:** High · **Status:** Confirmed current-data defect; newly identified

The generic C# XML loader invokes `AfterUnmarshal(object)` only on the top-level holder. JAXB invokes callbacks throughout the object graph. Other converted holders explicitly cascade child callbacks, but `SiegeLocationData` does not call either `DoorRepairData.AfterUnmarshal` or `AssaultData.AfterUnmarshal`.

There is a second binding defect: `DoorRepairStone.staticId` is `internal`, while `XmlSerializer` only binds public members. Even after adding the callback cascade, repair stones would be indexed under the default ID unless this field is exposed through a serializable public member/proxy.

Evidence:

- C#: [`JaxbHolderLoader.cs`](../src/Aion.GameServer/Dataholders/LoadingUtils/JaxbHolderLoader.cs#L12) documents the top-level callback and public-member restriction.
- C#: [`SiegeLocationData.cs`](../src/Aion.GameServer/Dataholders/SiegeLocationData.cs#L20) builds location indexes without cascading the two nested callbacks.
- C#: [`DoorRepairData.cs`](../src/Aion.GameServer/Model/Templates/Siegelocation/DoorRepairData.cs#L19) builds its repair-stone dictionary only in `AfterUnmarshal`.
- C#: [`DoorRepairStone.cs`](../src/Aion.GameServer/Model/Templates/Siegelocation/DoorRepairStone.cs#L10) binds `static_id` to an internal field.
- C#: [`AssaultData.cs`](../src/Aion.GameServer/Model/Templates/Siegelocation/AssaultData.cs#L21) builds `processedAssaulters` only in `AfterUnmarshal`.
- Real data: [`siege_locations.xml`](../game-server/data/static_data/siege/siege_locations.xml#L103) contains assault waves, and lines 143-146 contain repair stones `199` and `200`.
- Shipped config: [`siege.properties`](../game-server/config/main/siege.properties#L8) enables siege and Balaur auto-assault.
- Java: `DoorRepairData.java:26-31` and `AssaultData.java:33-47` rely on JAXB child callbacks, which do run.

Impact:

- [`GateRepairAI.cs`](../src/Aion.GameServer/Handlers/AI/GateRepairAI.cs#L55) cannot find a repair stone and returns without healing the gate.
- Gate-death cleanup sees an empty repair-stone collection.
- [`FortressAssault.cs`](../src/Aion.GameServer/Services/Siege/FortressAssault.cs#L31) receives an empty processed map. Teleport/commander/wave lists are missing; the first teleport-wave selection can dereference a null list, while commander accounting can divide by zero.

The existing static-data tests check only the top-level fortress ID/world/duration, so they pass while both child mechanics are empty.

Required fix and validation:

- Explicitly invoke `DoorRepairData.AfterUnmarshal` and `AssaultData.AfterUnmarshal` for each template before `SiegeLocationData.AfterUnmarshal` builds its location indexes, preserving JAXB child-before-parent order.
- Make `static_id` serializable without changing its public domain API (for example, a public XML proxy).
- Extend the real-data test to assert fortress `1131` maps repair stone `199` to door `53` and that a known assault location has non-empty `TELEPORT`, `COMMANDER`, and combat lists.
- Exercise one gate repair and one full automatic fortress assault in QA.

### BA-006 — Database timezone options are discarded and timestamp kinds disagree

**Severity:** High · **Status:** Confirmed on non-UTC hosts; newly identified

This is one boundary problem with three observable forms.

#### A. JDBC URL semantics are dropped

The Java server passes the complete JDBC URL to Hikari. C# parses only host, port, and database, discarding `uri.Query`, then rebuilds a MySqlConnector string without timezone, charset, SSL, or other session options.

- C#: [`DatabaseOptions.cs`](../src/Aion.Commons/Configuration/DatabaseOptions.cs#L46) returns only `(Server, Port, Database)`.
- C#: [`GameServerOptions.cs`](../src/Aion.GameServer/Configuration/GameServerOptions.cs#L367) consumes only those values.
- C#: [`DatabaseFactory.cs`](../src/Aion.Commons/Database/DatabaseFactory.cs#L20) builds a new connection string without translated query options.
- Config: [`database.properties`](../game-server/config/network/database.properties#L6) explicitly says timezone is required for DST correctness and supplies `serverTimezone=${gameserver.timezone}&characterEncoding=UTF-8`.
- Java: `commons/src/com/aionemu/commons/database/DatabaseFactory.java:28-40` preserves the full URL.

#### B. `players.last_online` is interpreted both as UTC and Local

- C#: [`PlayerLeaveWorldService.cs`](../src/Aion.GameServer/Services/Player/PlayerLeaveWorldService.cs#L119) writes a UTC-kind value.
- C#: [`PlayerDAO.cs`](../src/Aion.GameServer/Dao/PlayerDAO.cs#L178) tags the DB value UTC; [`PlayerCommonData.cs`](../src/Aion.GameServer/Model/GameObjects/Player/PlayerCommonData.cs#L451) converts it as UTC.
- C#: [`CharacterSelectionRepository.cs`](../src/Aion.GameServer/Data/CharacterSelectionRepository.cs#L364) force-tags the same column as Local before converting to epoch.
- Java uses `Timestamp.getTime()` consistently in `PlayerCommonData.java:396-404`.

The same row can therefore produce different epochs depending on the code path. On `America/New_York`, the selection packet is shifted by four hours during daylight time and five during standard time.

#### C. MAC/HDD ban epochs would change after a DB reload once synchronization is restored

Inbound control timestamps are UTC, but repositories reload `DateTime` with unspecified kind. `new DateTimeOffset(unspecifiedDateTime)` treats it as host-local when serializing back to GS. BA-003 currently prevents those lists from reaching GS at all, so this component is a confirmed latent defect in the synchronization path rather than a second currently active enforcement change.

- C#: [`BannedMacRepository.cs`](../src/Aion.LoginServer/Data/BannedMacRepository.cs#L20) and [`BannedHddRepository.cs`](../src/Aion.LoginServer/Data/BannedHddRepository.cs#L19) use `GetDateTime` without normalizing kind.
- C#: [`SmMacBanList.cs`](../src/Aion.LoginServer/Network/GameServer/ServerPackets/SmMacBanList.cs#L15) and [`SmHddBanList.cs`](../src/Aion.LoginServer/Network/GameServer/ServerPackets/SmHddBanList.cs#L14) construct `DateTimeOffset` from those values.
- Java preserves epoch values through JDBC `Timestamp` in the paired DAOs and `SM_MACBAN_LIST` / `SM_HDDBAN_LIST`.

#### D. Millisecond connection timeouts are silently shortened

Java passes `database.connectionpool.timeout` to Hikari in milliseconds. C# divides by `1000` and assigns the truncated result to MySqlConnector's whole-second `ConnectionTimeout`. A configured `1500` ms therefore expires after one second, while `1..999` becomes the connector's special zero value instead of the requested finite deadline.

- C#: [`DatabaseFactory.cs`](../src/Aion.Commons/Database/DatabaseFactory.cs#L77) floors the configured milliseconds.
- Java: `commons/src/com/aionemu/commons/database/DatabaseFactory.java:31-39` passes the millisecond value unchanged.

Impact:

Player last-online displays/logic can disagree now. After BA-003's ban-list sync is implemented, hardware-ban expiration/enforcement would also change after an LS restart unless the timestamp kind is fixed at the same time.

Required fix and validation:

- Define one DB temporal contract (prefer UTC instants) and normalize every reader/writer at the repository boundary.
- Translate supported JDBC query semantics explicitly to MySqlConnector option names; reject unsupported options instead of silently discarding them.
- Document and test a whole-second translation that never expires before Java's deadline, or reject non-integral-second values visibly.
- Under `America/New_York`, round-trip fixed winter and summer timestamps through both player repositories and both ban repositories; assert the same Java epoch before and after restart.

### BA-007 — Missing event themes become `NONE` instead of Java `null`

**Severity:** Medium · **Status:** Confirmed conditional/current-data semantic mismatch; newly identified

Java's optional XML enum is nullable. The C# `EventTheme` property is a non-nullable value type, so an absent `theme` attribute becomes ordinal zero (`NONE`). `EventService` retains Java's null guard, but the compiler correctly reports that the condition is always true; the loop breaks on the first active event whether or not it is themed.

Evidence:

- C#: [`EventTemplate.cs`](../src/Aion.GameServer/Model/Templates/Event/EventTemplate.cs#L32) declares non-nullable `EventTheme`.
- C#: [`EventService.cs`](../src/Aion.GameServer/Services/Event/EventService.cs#L255) checks `GetTheme() != null` and immediately breaks.
- Java: `EventTemplate.java:36-37,79-81` stores nullable `EventTheme`; `EventService.java:207-218` skips null themes.
- Real data has 75 event elements but only 10 theme attributes. [`custom_events.xml`](../game-server/data/static_data/events/timed_events/custom_events.xml#L3) contains an always-active, theme-less “Beyond Aion Server Buffs” event.

No shipped themed event was active on the audit date, so the visible seasonal symptom is conditional on a themed overlap; the deserialized state mismatch itself is present in the current data/model.

Impact:

When a seasonal themed event overlaps the always-active unthemed event, the selected theme can remain `NONE`; city decorations and the `SM_VERSION_CHECK` theme state are wrong. Hash-set iteration makes which active event wins unsuitable as an implicit priority rule.

Required fix and validation:

- Preserve missing XML state with a nullable string/enum proxy, as already done in other converted models.
- Test one always-active theme-less event plus one active Christmas/Valentine/Halloween event and assert the themed value wins.
- As an improvement beyond strict Java parity, define deterministic precedence if more than one themed event can overlap.

### BA-008 — Schema-valid SQL NULL values throw and can truncate loads

**Severity:** Medium · **Status:** Confirmed conditional defects; newly identified

Java `ResultSet.getFloat/getBoolean/getInt/getLong` return primitive zero/false for SQL NULL (with `wasNull()` available separately). MySqlConnector typed getters throw on `DBNull`. Three loaders copied the Java call shape without copying its null semantics, and their exception scope is outside the row loop or account load.

Confirmed cases:

| Loader | Schema-valid nullable fields | C# behavior | Java behavior / impact |
|---|---|---|---|
| [`CustomInstancePlayerModelEntryDAO.cs`](../src/Aion.GameServer/Dao/CustomInstancePlayerModelEntryDAO.cs#L32) | Target metric columns in `custom_instance_records` | `GetFloat/GetBoolean/GetInt32` throws; outer catch returns only the prefix | Java primitive getters yield `0/false`; later valid rows should still load |
| [`AccountTimeRepository.cs`](../src/Aion.LoginServer/Data/AccountTimeRepository.cs#L16) | `session_duration`, `accumulated_online`, `accumulated_rest` | `GetInt64` throws and can abort account-time load/login | Java `getLong` yields zero for a legacy/schema-valid null row |
| [`PlayerRegisteredItemsDAO.cs`](../src/Aion.GameServer/Dao/PlayerRegisteredItemsDAO.cs#L93) | `h`, `expire_time` | typed getters throw; outer catch truncates the housing item result set | Java primitive getters yield zero and continue |

Schema evidence is in `game-server/sql/aion_gs.sql:198-227` and `811-832`, and `login-server/sql/aion_ls.sql:30-40`.

Required fix and validation:

- Add explicit `IsDBNull` handling that mirrors the exact Java getter default for these primitive fields.
- Keep per-row failures from discarding unrelated valid rows where Java would continue.
- For the two collection loaders, test `valid row → nullable row → valid row`; assert all rows load and the nullable values equal Java's primitive defaults. For `AccountTimeRepository`, load one nullable account row and assert zero defaults without an exception.

### BA-009 — Bootstrap and runtime use different Java-properties semantics

**Severity:** Medium · **Status:** Confirmed configuration defect; newly identified

The DI/bootstrap `GameServerOptions` path uses a simple parser that recognizes trimmed `key=value` lines and `bool.TryParse`. The later static configuration framework uses the more faithful `JavaProperties` implementation. Java accepts `true/false/1/0` and full Java Properties escaping/continuation behavior.

Evidence:

- C#: [`ConfigLoader.cs`](../src/Aion.Commons/Configuration/ConfigLoader.cs#L81) is the naive bootstrap parser.
- C#: [`GameServerOptions.cs`](../src/Aion.GameServer/Configuration/GameServerOptions.cs#L443) uses `bool.TryParse`, so Java-valid `1` and `0` fall back rather than parse.
- C#: [`JavaProperties.cs`](../src/Aion.Commons/Configuration/JavaProperties.cs#L9) implements the faithful semantics, and [`Config.cs`](../src/Aion.GameServer/Configs/Config.cs#L113) uses it later.
- Java: `commons/src/com/aionemu/commons/configuration/transformers/BooleanTransformer.java:14-28` accepts `true/false/1/0` and rejects other values.

Impact:

The same property file can produce one value during DI/bootstrap and another in static config holders. For example, `gameserver.chatserver.enable=1` is true in Java but can become the C# bootstrap default false, changing whether a bridge/service starts.

A related transformer gap exists for enum-valued properties. Java's `EnumTransformer` delegates to case-sensitive `Enum.valueOf`, which accepts only a declared name. .NET `Enum.Parse` also accepts decimal underlying values and comma-separated name combinations. A value such as `1` can therefore select an enum member in C# even though Java rejects the property.

Required fix and validation:

- Use one Java-properties parser and one transformer set for bootstrap and runtime.
- Test `1`, `0`, invalid booleans, escaped separators, unicode escapes, continuation lines, and name/numeric/comma enum inputs through the actual transformer and `Program` option construction—not just the parser in isolation.

### BA-010 — Java numeric and enum command parsing diverges from .NET

**Severity:** Medium · **Status:** Confirmed current defect; newly identified

Java catches `IllegalArgumentException`, whose subclasses include `NumberFormatException`. C# catches only `ArgumentException`; `int.Parse` and `long.Parse` throw `FormatException` or `OverflowException`, neither of which derives from `ArgumentException`. The formatter even tests `e is FormatException` while `e` is statically `ArgumentException`, producing compiler warning CS0184 because that branch is impossible.

There is a second, smaller mismatch after making those exceptions reachable: .NET's parse exceptions do not reliably retain the offending token, whereas Java reports `Invalid number: "<input>"`. Framework parsing can also accept culture-dependent syntax that Java's decimal parsers reject. The command boundary therefore needs Java-compatible lexical/overflow handling, not only a wider catch clause.

Evidence:

- C#: [`ChatCommand.cs`](../src/Aion.GameServer/Utils/ChatHandlers/ChatCommand.cs#L40) catches only `ArgumentException` around command execution.
- C#: [`ChatCommand.cs`](../src/Aion.GameServer/Utils/ChatHandlers/ChatCommand.cs#L149) accepts only `ArgumentException`; its FormatException branch is unreachable at line 171.
- Java: `game-server/src/com/aionemu/gameserver/utils/chathandlers/ChatCommand.java:50-59,155-189` catches `IllegalArgumentException` and formats `NumberFormatException`.
- The initial scan found 57 `int.Parse` or `long.Parse` occurrences across 31 admin/console command files; the expanded sweep found additional numeric conversions in player commands, command helpers, and the `//fsc` custom-packet builder. Some commands catch locally, while many rely on the base class. .NET enum parsing is also too permissive for Java `Enum.valueOf`: numeric values and comma-separated names may parse successfully instead of reaching the error path.

Impact:

Malformed or overflowing numeric command arguments skip Java's friendly syntax/error response, reach the broad exception logger, and return as an unexpected command failure. This creates noisy logs and inconsistent operator behavior during QA/admin use.

Required fix and validation:

- Introduce Java-compatible integer/long/byte/float/double/decode and Commons-number parsers, and route command/downstream call sites through them.
- Catch the dedicated Java-number failure alongside domain `ArgumentException` without swallowing unrelated framework exceptions.
- Require exact declared enum names where Java calls `Enum.valueOf`; reject numeric, comma-combined, wrong-case, and undefined values.
- Add Java-oracle tests for malformed text, overflow, radix/decode, Unicode digits, floating-point/hex syntax, culture edges, exact enum names, and a command-thrown domain `ArgumentException`.

### BA-011 — Several Java `Math.round` ports use banker's rounding

**Severity:** Medium · **Status:** Confirmed semantic mismatch; impact occurs on exact half values

Java `Math.round(x)` follows `floor(x + 0.5)`. C# `Math.Round(x)` defaults to midpoint-to-even. The repository already uses `Math.Floor(x + 0.5)` in many correctly ported locations, but four semantic sites across three handler classes still use `Math.Round` (six calls because the arena group split rounds three reward components).

Evidence:

- C#: [`GateRepairAI.cs`](../src/Aion.GameServer/Handlers/AI/GateRepairAI.cs#L62) vs Java `game-server/data/handlers/ai/siege/GateRepairAI.java:90`.
- C#: [`DarkPoetaInstance.cs`](../src/Aion.GameServer/Handlers/Instance/DarkPoetaInstance.cs#L255) vs Java `game-server/data/handlers/instance/DarkPoetaInstance.java:239`.
- C#: [`PvPArenaInstance.cs`](../src/Aion.GameServer/Handlers/Instance/PvPArenaInstance.cs#L132) and group reward split at lines 642-647 vs Java `PvPArenaInstance.java:138,586-588`.

Impact:

Exact `.5` results can be one point/item lower in C#. The concrete recurring risk is an odd Arena of Harmony AP/GP/insignia component divided among two players; gate repair at the configured 1% can also hit a half-value. The Dark Poeta and kill-point occurrences are latent with current constants but remain incorrect primitives.

Required fix and validation:

- Centralize Java-compatible `JRound(float/double)` helpers and replace only Java `Math.round` ports, not display-only rounding.
- Pin positive and negative midpoint vectors plus representative arena/gate inputs in the formula parity suite.

### BA-012 — Zone tie-breaking uses C# string hashes

**Severity:** Medium · **Status:** Confirmed semantic mismatch; player-visible impact depends on an overlapping equal-priority zone pair

`ZoneName.Id()` uses `string.GetHashCode()`, despite the source comment acknowledging it is not Java's hash. Modern .NET string hashes are process-randomized. `MapRegion` uses that value as the final comparator key after zone type and priority; revalidation accepts the first entered nonzero-priority zone of each type.

Evidence:

- C#: [`ZoneName.cs`](../src/Aion.GameServer/World/Zone/ZoneName.cs#L34) returns `_name.GetHashCode()`.
- C#: [`MapRegion.cs`](../src/Aion.GameServer/World/MapRegion.cs#L20) uses `ZoneName.Id()` as the final tie-breaker.
- C#: [`MapRegion.cs`](../src/Aion.GameServer/World/MapRegion.cs#L191) makes ordering observable by accepting the first matching priority zone per type.
- Java: `world/zone/ZoneName.java:33-35` uses Java `String.hashCode()`; `world/MapRegion.java:25-27` uses it in the same comparator.

Impact:

For overlapping zones of the same type and priority, the selected handler can differ from Java and can change after a process restart. This is not the client-wire hash—the audited `SM_PLAYER_REGION` path already uses the correct Java hash—but it can still change gameplay zone entry/exit behavior.

Required fix and validation:

- Use the existing Java string-hash implementation (or centralize one) for `ZoneName.Id()`.
- Add known hash vectors and an overlap test with equal type/priority zones whose Java and C# hash order differs.

### BA-013 — Static-holder failures silently empty gameplay subsystems

**Severity:** Medium · **Status:** Confirmed resilience/parity gap; no current malformed source file was found

Java's main XML graph loader throws `GameServerError` on load failure. C# validates/loads the main graph, then loads many feature holders individually; holder-specific binding or callback exceptions are caught and replaced with a new/empty holder. This includes critical NPC, item, skill, quest, AI, and other gameplay data paths.

Evidence:

- C#: [`DataManager.cs`](../src/Aion.GameServer/Dataholders/DataManager.cs#L163) explicitly describes guarded leaf loading.
- C#: [`StaticData.cs`](../src/Aion.GameServer/Dataholders/StaticData.cs#L691) catches merged-holder failures and returns `new T()`; [`StaticData.cs`](../src/Aion.GameServer/Dataholders/StaticData.cs#L757) returns the fallback for a single holder.
- Java: `game-server/src/com/aionemu/gameserver/dataholders/loadingutils/XmlDataLoader.java:38-56` promotes XML failure to `GameServerError`; `DataManager.java:130-145` uses it.

Impact:

A well-formed file that triggers a C#-specific binding or invoked-callback exception can let boot or an admin reload appear successful while a whole data subsystem is empty. The server then fails later and far from the root cause. BA-005 demonstrates a separate silent callback/binding divergence; a throwing top-level callback or explicitly cascaded child callback is what exercises this caught-exception fallback.

Required fix and validation:

- Classify holders as required vs optional. Fail boot/reload for required data and enforce minimum/integrity invariants; only optional content should fail open.
- Preserve the previous known-good holder on reload failure rather than replacing it with empty state.
- Test a well-formed source whose top-level callback (or an explicitly cascaded child callback) deliberately throws, and assert boot/reload fails atomically.

### BA-014 — Smaller cross-server fidelity issues remain

**Severity:** Medium · **Status:** Confirmed; some are masked by BA-002/BA-003

These should be fixed while the bridge code is already being changed:

| Issue | C# evidence | Java behavior | Risk |
|---|---|---|---|
| Chat registration sends raw name | [`ChatServer.cs`](../src/Aion.GameServer/Network/ChatServer/ChatServer.cs#L55) sends `player.Name` | `SM_CS_PLAYER_AUTH.java:18-24` uses `player.getName(true)` | Staff with configured name tags can fail Chat's exact nickname match |
| Reconnect checks account ID, not the registered connection | [`LoginServer.cs`](../src/Aion.GameServer/Network/LoginServer/LoginServer.cs#L196) | Java also requires the requesting connection to equal the map entry | A stale second connection can disturb the active account once reconnect response is ported |
| Send reports success before LS authentication and fire-and-forgets writes | [`LoginServer.cs`](../src/Aion.GameServer/Network/LoginServer/LoginServer.cs#L59) | Java sends only in `AUTHED` state | Callers receive success for an illegal/pre-auth or failed write |
| LS-down auth closes without protocol failure | [`LoginServer.cs`](../src/Aion.GameServer/Network/LoginServer/LoginServer.cs#L97) | Java sends `SM_L2AUTH_LOGIN_CHECK(false, null)` before close | Client sees an abrupt disconnect instead of the defined failure |
| Chat frames accept the full unsigned-short range | [`ClientChannelHandler.cs`](../src/Aion.ChatServer/Network/Handlers/ClientChannelHandler.cs#L48) and bridge readers | Java `PacketFrameDecoder` caps frames at 16 KiB | Avoidable allocation/amplification hardening gap |

Add these cases to the bridge integration suite rather than relying on isolated unit tests.

### BA-015 — Nullable Java results are force-unwrapped on two edge paths

**Severity:** Low · **Status:** Confirmed edge-path parity defects

Two call sites correctly model a Java-returned enum as nullable, then immediately use `.Value` where Java safely handles null:

- [`DialogService.cs`](../src/Aion.GameServer/Services/DialogService.cs#L269) force-unwraps `GetProfessionByNpc(npc)`. Java passes null to `RelinquishCraftStatus`, whose null check returns false. The C# edge case requires NPC data to advertise `GIVEUP_CRAFT_*` for an NPC absent from `professionByNpc`; ordinary unsupported client actions are rejected earlier.
- [`HousingBidService.cs`](../src/Aion.GameServer/Services/HousingBidService.cs#L433) force-unwraps `AuctionResultExtensions.GetResultFromId`. Java comparisons simply do nothing for an unknown auction-mail result. A well-formed title containing an unknown numeric result ID can abort housing mail handling on player login; nonnumeric or structurally malformed titles can throw in Java too and are not a port difference.

Fix by preserving nullable control flow through the consumer. Test an NPC that advertises relinquish without a profession mapping and a well-formed auction title with an unknown numeric result ID. Do not replace missing values with enum ordinal zero unless Java does so.

### BA-016 — Warning and boundary-test debt is masking real port defects

**Severity:** Improvement · **Status:** Confirmed

The clean solution build succeeds with **4,359 warnings**. Most are nullable-flow warnings, but several are direct defect signals:

- CS0472 identifies impossible value-type null checks, including BA-007 and the craft path in BA-015.
- CS0184 identifies the unreachable `FormatException` branch in BA-010.
- Nullable dereference/conversion warnings cluster at persistence and XML boundaries where Java and C# differ most.

The 812 passing tests are valuable, but current real-data tests mostly assert top-level counts/IDs. The siege test, for example, proves fortress `1011` exists but does not prove nested repair/assault maps were initialized. Cross-server tests prove many packet shapes in isolation but not a request/response journey across two processes.

Recommended quality controls:

- Establish the current warning file as a baseline and reject new warnings immediately.
- Promote high-signal codes (`CS0184`, `CS0472`, and selected nullable boundary warnings) to errors first; then burn down the broader nullable debt.
- Add boot-time static-data invariants for required holders and nested indexes.
- Add loopback protocol journeys, DB NULL/timezone round-trips, and Java semantic primitive tests (`JRound`, Java hash, enum ordinal, nullable XML proxy).

Remediation outcome: CI now performs the warning-ratchet rebuild, full solution tests, and fidelity check. The baseline records 4,318 unique sites across 21 codes; increases or new codes fail, while `CS0184`, `CS0472`, and `CS8605` are build errors held at zero. The deterministic boundary, bridge, static-data, and database suites run in the normal solution command; externally provisioned MySQL tests remain explicitly environment-gated.

### BA-017 — Fresh Game Server schema import fails at `bookmark`

**Severity:** High · **Status:** Confirmed during live MySQL validation; also present in the Java reference

The `bookmark` table's composite primary-key declaration is immediately followed by a foreign-key constraint without a separating comma. MySQL stops the schema import with error 1064, leaving a fresh `aion_gs` database incomplete and preventing every DB-backed Game Server test or first-time installation from starting.

Evidence:

- C#: at the audited baseline, [`aion_gs.sql`](../game-server/sql/aion_gs.sql#L108) omitted the comma after the composite `PRIMARY KEY` declaration.
- Java: `game-server/sql/aion_gs.sql:108-116` contains the same invalid statement.
- A live MySQL 8 schema import fails at this statement before any Game Server temporal round-trip can run.

This is an infrastructure exception to the Java-is-spec rule: preserving an upstream syntax error would make the supplied setup artifact unusable. Add the comma in the C# distribution, pin the statement with a schema sanity test, and require a complete fresh-schema import in DB integration QA.

Remediation outcome: the comma is restored, a schema-sanity regression test pins the foreign-key boundary, and a fresh isolated MySQL 8 import of the complete Game Server schema succeeds.

### BA-018 — Name-only Java enums are parsed as numbers or flag combinations

**Severity:** High · **Status:** Confirmed current-data mechanic defect plus defensive trust-boundary defects; newly identified during remediation review

Java `Enum.valueOf` accepts one exact, case-sensitive declared name. JAXB uses the same single-enum conversion rule for an enum-typed `@XmlAttribute`: a non-name token does not become a combined value. .NET `Enum.Parse`/`Enum.TryParse`, by contrast, accepts decimal underlying values and comma-separated names; the latter are bitwise-ORed even when the enum has no `[Flags]` attribute.

The shipped `skill_templates.xml` contains 30 multi-condition `counter_skill` attributes: 22 `BLOCK,RESIST`, six `RESIST,PARRY`, and two `RESIST,DODGE`. `AttackStatus` uses values `DODGE=0`, `PARRY=2`, `BLOCK=4`, and `RESIST=6`, so every combination currently parses to `RESIST` in C#. The affected groups are Spinning Fire (`1982..1983`), Backlash (`2498..2503`), Courageous Shield (`3048`), Avenging Blow (`3085..3093`), and Shield Counter (`3094..3105`). In Java these invalid single-enum attributes remain null, so the C# port incorrectly enables those skills after a resisted attack.

Evidence:

- C#: [`SkillTemplate.cs`](../src/Aion.GameServer/SkillEngine/Model/SkillTemplate.cs#L138) uses `Enum.TryParse<AttackStatus>` on the `xs:string` proxy.
- Data: [`skill_templates.xml`](../game-server/data/static_data/skills/skill_templates.xml#L30453) begins the affected stock entries; all 30 were enumerated during the audit.
- Java: `game-server/src/com/aionemu/gameserver/skillengine/model/SkillTemplate.java:96-97` binds one nullable `AttackStatus` directly.
- Oracle: a JAXB 2.3.9 probe matching the Java dependency unmarshalled `BLOCK,RESIST` as null and `RESIST` as `RESIST`.

The same permissive primitive appears at lower-frequency boundaries: transfer-wire NPC-faction and quest states use `Enum.valueOf` in Java but `Enum.Parse` in C#, so malformed peer input such as `999` becomes an undefined enum instead of failing. Untyped XML proxies for housing size, item gender, and runtime signet selection similarly need exact-name behavior. Most other XML enum sites are constrained by enum/list XSD types and were excluded from this finding after a bounded scan.

Required fix and validation:

- Use one exact-name Java enum helper for command, transfer, runtime, and untyped XML-proxy boundaries.
- Preserve each Java boundary's failure/null behavior: invalid nullable JAXB attributes become null; `Enum.valueOf` call sites throw.
- Pin all 30 shipped counter-skill templates to null while proving the 67 valid single-name templates still bind correctly.
- Reject defined numeric, undefined numeric, comma-combined, wrong-case, and unknown tokens at representative transfer/runtime boundaries.

Remediation outcome: one shared exact-name helper now covers the command, transfer, housing, item-gender, counter-skill, and signet boundaries. Six BA-018 tests and 71 command/parser tests pass; the stock-data test verifies all 97 counter-skill attributes, and the bounded 83-site textual enum scan found no remaining confirmed defect.

## Prioritized remediation and QA plan

### Release gate 1 — Repair primary cross-server flows

1. Fix BA-001's transfer opcode.
2. Implement the complete BA-003 LS response factory plus post-auth account list.
3. Fix BA-002's Chat response ownership and `SM_CHAT_INIT` delivery.
4. Add BA-004 retry/reconnect, pending-request cleanup, and per-packet fault containment.
5. Run loopback journeys for normal login, duplicate login, kick, fast reconnect, grant, ban, MAC/HDD reload, Chat login, LS/Chat restart, and two-GS transfer.

### Release gate 2 — Restore enabled gameplay/data behavior

1. Fix BA-005 callback cascade and `static_id` binding.
2. Add real-data invariants for all repair and assault entries.
3. Fix BA-006 time/connection-option contract and run DST/restart round-trips.
4. Fix BA-007 event theme nullability before the next seasonal event.
5. Fix BA-017 and prove a complete fresh Game Server schema import before running DB-backed gameplay tests.

### Hardening gate

1. Fix BA-008 schema-valid NULL handling.
2. Unify configuration parsing for BA-009.
3. Correct BA-010 through BA-012 semantic primitives.
4. Make required static-data loads atomic/fail-fast (BA-013).
5. Fold BA-014/BA-015 cases into the integration suite.
6. Ratchet compiler warnings and add boundary-focused tests (BA-016).
7. Replace permissive enum parsing at the BA-018 name-only boundaries and pin the shipped counter-skill data.

## Suggested automated test matrix

| Layer | Test | Expected parity assertion |
|---|---|---|
| Packet bytes | Every `SM_PTRANSFER_CONTROL` constructor | First byte is `0x0D`, second byte is action |
| Packet factory | All active GS↔LS opcodes and legal states | Same class/state selection as Java factories |
| Protocol journey | Login → account list → MAC/HDD response | Existing accounts resynced; persisted hardware bans enforced |
| Protocol journey | `CM_CHAT_AUTH` → Chat response | Exact `SM_CHAT_INIT` token and gag propagation |
| Failure injection | LS/Chat late start, restart, one handler throw | Automatic recovery; next valid packet succeeds |
| Static real data | Fortress `1131` repair map | Static ID `199` resolves to door `53` |
| Static real data | Known fortress assault data | Required assaulter types are non-empty |
| Events | Theme-less always-on + themed overlap | The real theme wins deterministically |
| Time | Same DB timestamp through both player readers | Identical epoch in winter/summer and to Java |
| Time | MAC/HDD ban before/after DB restart | Identical expiration epoch |
| SQL NULL collections | Valid → NULL → valid rows | All rows load; NULL uses Java primitive default |
| SQL NULL account time | One account row with nullable numeric fields | Account loads; nullable values use Java zero defaults |
| Schema bootstrap | Import `game-server/sql/aion_gs.sql` into an empty MySQL database | Full import succeeds, including `bookmark` and its foreign key |
| Properties | `1/0`, escapes, continuations | Bootstrap and static config return the same Java value |
| Commands | Invalid/overflow numeric input and domain `ArgumentException` | Friendly Java-equivalent error; no broad exception log |
| Enum boundaries | Stock counter skills plus numeric/comma/wrong-case transfer/XML tokens | Invalid JAXB single-enum values remain null; `valueOf` boundaries reject |
| Formula | Midpoints around `n + 0.5` | Exact Java `Math.round` results |
| Zones | Equal type/priority overlap | Deterministic Java hash ordering |

## Backlog corrections

Update [`Full-Parity-Backlog.md`](Full-Parity-Backlog.md) while implementing this report:

- The transfer packet should not be marked fully audited until BA-001 and the end-to-end response flow are covered.
- §I1 should list the current active `0..13` bridge opcodes and distinguish LS-side implementations from missing GS consumers.
- The MAC/HDD subsystem is present on LS; the open work is the GS post-auth request, inbound handlers, lifecycle resync, and timestamp correctness.
- Add explicit lifecycle/fault-isolation work from BA-004 rather than treating sockets as complete because they connect once.
- Add a JAXB child-callback/public-member audit item for every nested static-data model, with BA-005 as the first regression fixture.

## Verified parity and excluded false positives

To keep this report focused, the audit also checked and excluded several tempting but incorrect findings:

- Main Aion opcode/state tables and canonical client packet sets matched the Java reference exactly in the automated comparison.
- Packet primitive sequences in the canonical audited files matched; one unused `SM_MOVE` overload difference was benign.
- The wire value in `SM_PLAYER_REGION` already uses Java `String.hashCode`; BA-012 is a separate internal zone-ordering call site.
- Audited protocol enum ordinal uses were safe for the enums involved.
- Login logout-duration update ordering mirrors Java's `AccountTimeController` and was not reported.
- Several impossible-null warnings are faithful because Java fields are primitive or initialized (for example current ranking, hit-type, and required skill properties); they were not promoted to findings without an observable difference.
- Most XML enum attributes are constrained by enum/list XSD types. The bounded untyped-string audit is tracked in BA-018; it found the current-data `counter_skill` defect rather than incorrectly clearing all XML enum proxies.
- The removed premium/in-game-shop bridge should not be reintroduced; current Java and C# both omit that unsupported flow.

## Completion criteria

The **code/automation remediation is complete**: every finding has an implementation, focused coverage, a green full solution, and a reconciled backlog; 13 findings meet every scoped acceptance item and are Verified. Five remain Code complete because their final evidence is inherently a live multi-process or in-world journey:

- BA-001/BA-003: two-GS transfer and the complete Login bridge operator journey.
- BA-002: retail-client Chat success/failure/duplicate lifecycle.
- BA-005: one live gate repair and automatic fortress assault.
- BA-006: full MAC/HDD LS↔GS synchronization across a database reload/restart.

Production parity sign-off occurs when those journeys pass and the separate live-client backlog gate succeeds. Until then, “Code complete” must not be reported as “Verified.”
