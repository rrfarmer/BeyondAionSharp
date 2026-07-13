# Phase 3: Port Login Server - Completion Notes

**Date**: May 19, 2026  
**Status**: Complete

## Completed In This Slice

- Added C# login protocol packet models for the Aion client boundary:
  - `SM_INIT`
  - `SM_AUTH_GG`
  - `SM_LOGIN_OK`
  - `SM_LOGIN_FAIL`
  - `SM_SERVER_LIST`
  - `SM_PLAY_OK`
  - `SM_PLAY_FAIL`
  - `SM_UPDATE_SESSION`
  - account banned/kick packets
- Added client packet parsers and state-based packet factory for:
  - `CM_AUTH_GG`
  - `CM_LOGIN`
  - `CM_SERVER_LIST`
  - `CM_PLAY`
  - `CM_UPDATE_SESSION`
- Audited the login-client parser read order against Java `CM_*` packets and added focused parser coverage for login, server-list, play, and update-session payload shapes.
- Added game-server bridge packet models for the first registration/auth slice:
  - `CM_GS_AUTH`
  - `CM_ACCOUNT_AUTH`
  - `CM_ACCOUNT_RECONNECT_KEY`
  - `CM_ACCOUNT_DISCONNECTED`
  - `CM_ACCOUNT_LIST`
  - `CM_GS_PONG`
  - `SM_GS_AUTH_RESPONSE`
  - `SM_ACCOUNT_AUTH_RESPONSE`
  - `SM_ACCOUNT_RECONNECT_KEY`
  - `SM_PING`
- Audited the game-server bridge parser read order against Java `CM_*` packets and added focused parser coverage for account auth/reconnect/disconnect/list, toll, bans, allowed-HDD, premium, player-transfer, ping/pong, and control packets.
- Aligned unknown authed game-server opcodes with Java by returning no packet instead of materializing and consuming an `UnknownGsClientPacket`.
- Added length-prefixed packet frame helpers matching the Java dispatcher framing rule: packet length includes the two-byte length field.
- Added hosted login client and game-server socket listener scaffolding under `Aion.LoginServer`.
- Added an in-memory game-server registry seam for tests and later DB-backed `GameServersDAO` parity.
- Corrected `PacketBuffer.WriteS` and `ReadS` to match Java `BaseServerPacket.writeS` / `BaseClientPacket.readS`: UTF-16 little-endian, null-terminated, not length-prefixed.
- Added focused packet parity tests for login and game-server auth packet shapes.
- Added a direct C# port of Java login `BlowfishCipher`.
- Added a C# `LoginCryptEngine` matching the Java login crypto state machine:
  - static first-packet key
  - delayed update to generated Blowfish key after first server packet
  - first-packet XOR pass
  - 8-byte padding behavior
  - checksum append/verify for later packets
- Added crypto tests for Blowfish reversibility, first-packet padding/key update, later packet decrypt, tamper rejection, and Java-generated encrypted golden vectors.
- Added a Java-generated encrypted `SM_INIT` frame vector covering the first real client-visible login packet.
- Added Java-generated packet payload vectors from the original Java packet classes for currently modeled login-client and game-server bridge server packets, including auth/login/play/session responses, account ban/kick packets, game-server auth/account/reconnect responses, ban/control/premium/transfer responses, ban-list packets, ping, character-count requests, and kick requests.
- Aligned login-client checksum verification with the Java server's live client-packet behavior using the captured `CM_AUTH_GG` payload from `dotnetConversion/DebugData/AionLoginChecksum.csv`; Blowfish decrypt produced the correct opcode, and the mismatch was the verifier rule, not decryption.
- Corrected C# `SM_GS_CHARACTER_RESPONSE` to opcode `0x08` after the Java-generated vector exposed the previous `0x04` mismatch.
- Added login RSA keypair generation with Java-compatible 1024-bit/F4 keys.
- Added RSA modulus scrambling matching Java `EncryptedRSAKeyPair.encryptModulus`.
- Added a Java-generated RSA modulus scrambling vector for deterministic `EncryptedRSAKeyPair` parity.
- Added raw RSA no-padding decrypt for `CM_LOGIN` credential blocks.
- Added a cached 10-key `LoginKeyGenerator` equivalent to Java `KeyGen`.
- Added normal-login credential decrypt tests for username, password, and OTP extraction.
- Fixed and covered the `-loginex` two-RSA-block credential compaction path so it matches Java's in-place block shifting behavior across the chunk boundary.
- Wired encrypted server packet serialization into `LoginClientConnection`.
- Wired encrypted client packet reads and checksum rejection into `LoginClientConnection`.
- Added Java-style `CM_LOGIN` session-id validation so mismatched login packets return `SM_LOGIN_FAIL(STR_L2AUTH_S_SYSTEM_ERROR)` before RSA decrypt/auth.
- Added direct MySQL repository ports for core login schema access:
  - `AccountRepository`
  - `AccountTimeRepository`
  - `BannedIpRepository`
  - `GameServersRepository`
  - `PremiumRepository`
- Aligned `AccountRepository.InsertAccountAsync` with Java `AccountDAO.insertAccount` by inserting only `account_data`, hardcoding inserted toll to `0`, and leaving the first `account_time` DB write to the normal login/account-time update path.
- Added Java-compatible SHA-1/Base64 password hashing via `AccountUtils.EncodePassword`.
- Aligned auto-created account defaults with Java `AccountController.createAccount`, including the implicit `last_server = 0` value from the Java model default.
- Added `LoginAuthService` for the first DB-backed `AccountController.login` slice:
  - banned IP check
  - auto-create account
  - password hash validation
  - activation check
  - expiration and penalty checks
  - forced IP mask check
  - account time update
  - last IP and membership update
- Wired successful `CM_LOGIN` auth to `SM_LOGIN_OK` session-key creation.
- Added a Docker helper script for a local login MySQL container: `dotnetConversion/scripts/start-login-db.ps1`.
- Added an opt-in MySQL integration test that initializes `login-server/sql/aion_ls.sql` and round-trips `AccountRepository`.
- Added in-memory auth service tests for fast development coverage.
- Added `LoginSessionRegistry` for Java-style `accountsOnLS` tracking.
- Added duplicate login behavior for accounts already on the login server:
  - existing login-server session receives `SM_ACCOUNT_KICK`
  - incoming login receives `SM_LOGIN_FAIL(STR_L2AUTH_S_ALREADY_LOGIN)`
- Added login disconnect cleanup for sessions that have not joined a game server yet:
  - remove account from login-server session registry
  - update account time on logout
- Added `CM_SERVER_LIST` handling for authenticated sessions:
  - session key validation
  - no-server-list failure
  - `SM_SERVER_LIST` response using registered game servers
- Added `CM_PLAY` handling for authenticated sessions:
  - session key validation
  - server-down failure
  - min-access-level failure
  - full-server failure
  - successful `SM_PLAY_OK` and joined-GS marker
- Added game-server registration IP mask enforcement in `GameServerRegistry`.
- Added unit tests for login session registration, duplicate login rejection, and game-server registration auth.
- Added startup loading for registered game servers via `GameServersRepository`.
- Added Java-style live game-server session tracking:
  - game-server disconnect marks the server offline and clears tracked accounts
  - duplicate game-server registration is rejected
  - `SM_REQUEST_KICK_ACCOUNT` can be sent to the owning game server
- Added `CM_ACCOUNT_AUTH` handling from game server to login server:
  - validates the full `SessionKey` against `accountsOnLS`
  - consumes the login-server session after successful game-server auth
  - adds the account to the selected game server
  - updates `account_data.last_server`
  - includes account time, membership, toll, access level, and allowed HDD serial in `SM_ACCOUNT_AUTH_RESPONSE`
- Added reconnect flow parity for the core handoff:
  - `ReconnectingAccount`
  - `CM_ACCOUNT_RECONNECT_KEY`
  - `SM_ACCOUNT_RECONNECT_KEY`
  - `CM_UPDATE_SESSION`
  - `SM_UPDATE_SESSION`
- Added `CM_ACCOUNT_DISCONNECTED` cleanup to remove accounts from the game-server registry and update account time.
- Added initial `CM_ACCOUNT_LIST` sync handling and corrected its count field to Java's 32-bit integer shape.
- Added `CM_GS_CHARACTER` and `SM_GS_CHARACTER_RESPONSE`.
- Updated `CM_SERVER_LIST` to request per-game-server character counts and send `SM_SERVER_LIST` only once every registered server has a count.
- Added packet and registry tests for account-list parsing, request-kick packets, reconnect state, and character-count fanout.
- Added Java-style game-server ping/pong lifecycle:
  - `SM_PING` every 5 seconds after GS auth
  - `CM_GS_PONG` resets the unanswered counter
  - connection closes after more than two unanswered pings
- Added account metadata/control bridge handling:
  - `CM_ACCOUNT_CONNECTION_INFO`
  - `CM_ACCOUNT_TOLL_INFO`
  - `CM_PREMIUM_CONTROL`
  - `CM_CHANGE_ALLOWED_HDD_SERIAL`
- Added premium response packet `SM_PREMIUM_RESPONSE`.
- Added DB-backed repositories and services for:
  - `AccountsLogDAO`
  - `BannedMacDAO`
  - `BannedHddDAO`
- Added MAC/HDD ban bridge handling:
  - `CM_MACBAN_CONTROL`
  - `CM_HDDBAN_CONTROL`
  - `SM_MACBAN_LIST`
  - `SM_HDDBAN_LIST`
- Aligned MAC/HDD ban lifecycle with Java:
  - startup deletes expired bans through the DAO
  - in-memory MAC/HDD ban maps are loaded lazily on first manager/service use
  - ban/unban controls load the current map before mutating it
- Updated `CM_ACCOUNT_LIST` to send MAC/HDD ban lists after account sync, matching Java's follow-up packet sequence.
- Aligned `PremiumDAO.getPoints` reward consumption with Java's single-row `rs.next()` behavior.
- Added packet tests for ping, account connection info, premium control/response, and ban-list payloads.
- Added account/IP ban bridge handling:
  - `CM_BAN`
  - `SM_BAN_RESPONSE`
  - account penalty time updates
  - banned IP insert/remove path
  - account kick after ban request
- Added login-server control handling:
  - `CM_LS_CONTROL`
  - `SM_LS_CONTROL_RESPONSE`
  - access-level and membership updates through `AccountRepository.UpdateAccountAsync`
- Added player-transfer bridge spine:
  - `CM_PTRANSFER_CONTROL`
  - `SM_PTRANSFER_RESPONSE`
  - `PlayerTransferRepository`
  - `PlayerTransferService`
  - scheduled new-task verification matching Java's 10-second initial delay and 7-minute interval
  - transfer request/error/ok/task-stop state transitions
- Added player-transfer packet tests for all newly modeled response shapes.
- Added external authentication success/failure handling:
  - posts Java-compatible JSON payload with `User-Agent: AionLS`
  - maps external `aionAuthResponseId` to `AionAuthResponse`
  - uses returned `accountId` as the DB account key
  - auto-creates external-auth accounts with an empty password hash
- Added brute-force escalation parity:
  - `loginserver.network.client.logintrybeforeban`
  - `loginserver.network.client.bantimeforbruteforcing`
  - Java-compatible counter timing, where the ban happens on the next failed attempt after the configured threshold is reached
  - localhost exemption
  - blocked-IP response plus connection close after an escalation ban
- Added auth tests for external auth success/failure/unavailable and brute-force ban timing.
- Added auth parity coverage for the remaining Java `AccountController.login` account-state branches:
  - missing account with auto-create disabled and empty account name return `STR_L2AUTH_S_ACCOUNT_LOAD_FAIL`
  - inactive accounts return `STR_L2AUTH_S_AGREE_GAME`
  - expired accounts return `STR_L2AUTH_S_TIME_EXHAUSTED`
  - active/permanent account penalties return the `SM_ACCOUNT_BANNED_2` signal path
  - forced-IP mismatches return `STR_L2AUTH_S_BLOCKED_IP`
  - previous-day successful login resets accumulated online/rest counters before persistence
- Ported Java `BannedIpController` semantics into `BannedIpService`:
  - startup clean/load into an in-memory mask-keyed ban list
  - login blocked-IP checks read the cached list instead of re-querying the DB
  - brute-force bans and game-server `CM_BAN` update the cached list and DB together
  - duplicate exact masks follow Java `HashSet<BannedIP>` behavior
- Fixed the opt-in MySQL integration test schema path lookup so it works from the .NET test output directory.
- Validated `AccountRepository` against the Java `aion_ls.sql` schema in a Dockerized MySQL 8.4 container.
- Added and smoke-validated a mixed-mode runbook plus Docker/script support for running the Java chat and game servers against the C# login server with Java SQL schemas initialized from the repo SQL files.
- Generated login crypto vectors from the repository's Java `BlowfishCipher` and `CryptEngine` sources in a Docker JDK container, then pinned C# tests to those bytes.
- Added Java-style server-list refresh fanout for logged-in, not-yet-joined clients after `CM_ACCOUNT_LIST` and game-server disconnect.
- Added Java `BaseClientPacket` malformed-read parity for live login/client and game-server bridge packet buffers:
  - network packet reads can return Java-style default zero/empty values on primitive underflow without closing the socket
  - structurally impossible packet reads are skipped instead of running their handler
  - short encrypted `CM_LOGIN` packets are ignored while keeping the client connection usable
  - short `CM_GS_AUTH` packets follow Java default-read behavior and close with `SM_GS_AUTH_RESPONSE(NOT_AUTHED)`
- Expanded the opt-in MySQL integration suite to round-trip the auxiliary login repositories against the Java schema:
  - game servers
  - banned IP/MAC/HDD entries
  - premium points and first unclaimed reward consumption
  - account login history
  - player transfer task load/update
- Re-ran the opt-in MySQL integration suite against the Dockerized MySQL 8.4 login schema after the broader auth/bridge parity additions; all 4 integration tests pass.
- Added login-server options coverage for the known Java config keys, including `loginserver.network.nio.threads`.
- Added Java `DatabaseConfig`/`DatabaseFactory.init()` startup parity:
  - `database.url`, `database.user`, `database.password`, `database.connectionpool.connections.max`, and `database.connectionpool.timeout` load from the same cascading `.properties` set as Java
  - JDBC MySQL URLs are parsed into `MySqlConnector` connection settings
  - login-server program startup now initializes `DatabaseFactory` before repository-backed hosted-service startup work
- Added Java-style send/close protection on login-client and game-server connections:
  - packet serialization and writes are guarded by a per-connection send lock
  - close waits for any in-flight send before tearing down the socket
  - packets requested after the connection is closed are ignored instead of racing a disposed stream
- Added listener shutdown tracking so login-client and game-server bridge sockets actively close child connections before waiting for the active connection count to drain.
- Added hosted-service startup coverage proving game-server DB registration, banned IP load, MAC/HDD expired-ban cleanup, and player-transfer scheduler startup complete before the login-client and game-server bridge listeners open their sockets.
- Aligned player-transfer scheduler lifecycle with Java `LoginServer` startup/shutdown ordering:
  - scheduler startup now happens after game-server load and ban cleanup but before login-client/game-server bridge sockets open
  - scheduler shutdown now completes before listener/network teardown begins
- Added player-transfer service integration coverage with fake DB/GS collaborators:
  - new waiting tasks become active and send perform-action packets to the source game server
  - source-account-online tasks are skipped without DB update or GS packet
  - source transfer requests disable both accounts and send transfer info to the target game server
  - target OK/error responses update task status, reactivate accounts, and notify the expected game server
- Added loopback socket smoke coverage for the hosted login listeners:
  - login-client listener sends an encrypted Java-sized `SM_INIT` frame, completes encrypted `CM_AUTH_GG` -> `SM_AUTH_GG`, completes encrypted RSA `CM_LOGIN` -> `SM_LOGIN_OK` with a fake auth service, and closes the active child socket during shutdown
  - login-client listener returns encrypted `SM_LOGIN_FAIL(STR_L2AUTH_S_SYSTEM_ERROR)` and closes when `CM_AUTH_GG` carries the wrong session id
  - login-client listener returns encrypted `SM_ACCOUNT_BANNED_2` and closes when account penalty auth returns Java's banned-account path
  - login-client listener kicks an existing login-server session with `SM_ACCOUNT_KICK(STR_L2AUTH_S_KICKED_DOUBLE_LOGIN)` and returns `SM_LOGIN_FAIL(STR_L2AUTH_S_ALREADY_LOGIN)` to the duplicate login
  - login-client listener returns encrypted `SM_LOGIN_FAIL(STR_L2AUTH_S_NO_SERVER_LIST)` and closes when a logged-in client requests a server list with no registered game servers
  - login-client listener returns encrypted `SM_LOGIN_FAIL(STR_L2AUTH_S_SYSTEM_ERROR)` and closes when `CM_SERVER_LIST` carries the wrong session key
  - login-client listener includes registered offline game servers in `SM_SERVER_LIST` with Java-style zero character counts
  - login-client listener accepts encrypted `CM_UPDATE_SESSION`, consumes Java-style reconnect keys, returns `SM_UPDATE_SESSION`, registers the restored login session, and closes without a packet when the reconnect key is wrong
  - login-client listener returns the Java `SM_PLAY_FAIL` branches for server-down, restricted-access, and full-server `CM_PLAY` requests, and closes with `SM_LOGIN_FAIL(STR_L2AUTH_S_SYSTEM_ERROR)` when the play session key is wrong
  - game-server bridge accepts a Java-framed `CM_GS_AUTH`, returns `SM_GS_AUTH_RESPONSE`, marks the registered server online, and marks it offline during shutdown
  - game-server bridge rejects unregistered IDs, wrong passwords, wrong source IPs, and duplicate registrations with the Java `SM_GS_AUTH_RESPONSE` failure codes and closes only the rejected socket
  - combined fake-client/fake-GS loopback flow routes `CM_SERVER_LIST` character-count requests through the game-server bridge, returns `SM_SERVER_LIST`, completes `CM_PLAY` -> `SM_PLAY_OK`, and completes GS `CM_ACCOUNT_AUTH` -> `SM_ACCOUNT_AUTH_RESPONSE` with last-server/toll/allowed-HDD side effects
- Added game-server bridge behavior parity coverage through the real hosted bridge socket for:
  - `CM_ACCOUNT_CONNECTION_INFO` updating last MAC/HDD serials and writing login history before a following response packet
  - `CM_ACCOUNT_RECONNECT_KEY` removing the account from the game-server account map, registering a reconnect key, and returning `SM_ACCOUNT_RECONNECT_KEY`
  - `CM_ACCOUNT_DISCONNECTED` removing the account from the game-server account map and applying logout/account-time update before the next response
  - `CM_ACCOUNT_TOLL_INFO` updating account toll points with Java `PremiumDAO.updatePoints(accountId, toll, 0)` semantics
  - `CM_CHANGE_ALLOWED_HDD_SERIAL` updating `account_data.allowed_hdd_serial`
  - `CM_LS_CONTROL` updating access level and returning `SM_LS_CONTROL_RESPONSE`
  - `CM_BAN` full account/IP bans, account-only permanent bans, and IP-only unbans using Java response/result semantics
  - `CM_PREMIUM_CONTROL` purchase, low-points, and toll-add result codes through the registered game-server session
  - `CM_ACCOUNT_LIST` loading new local accounts, requesting duplicate-account kicks, and sending MAC/HDD ban lists
  - `SM_PING`/`CM_GS_PONG` live ping loop behavior, including pong reset and close after the Java missed-pong threshold
  - `CM_MACBAN_CONTROL` and `CM_HDDBAN_CONTROL` applying Java manager-style ban/unban side effects before the next response packet
  - `CM_PTRANSFER_CONTROL` dispatching request, error, OK, and task-stop actions to the player-transfer service from the hosted bridge read path
- Added opt-in MySQL-backed encrypted login socket smoke that initializes the Java schema, authenticates through `LoginAuthService`/repositories, and verifies `last_ip` persistence.
- Added Java-shaped C# login-server file logging for `server_console.log`, `server_warnings.log`, and `server_errors.log` under `login-server/log`.

## Phase 3 Completion Summary

- No known Phase 3 login-server parity gaps remain before moving to Phase 4.
- `CM_LOGIN` reaches a DB-backed auth service and the known Java auth branches are ported; local encrypted socket smoke reaches `SM_LOGIN_OK` with fake auth and opt-in MySQL-backed auth.
- Mixed-mode validation passed with Java chat and game servers in Docker, the C# login server running locally, and the Java SQL schemas initialized from the repo SQL files.
- Real-client validation passed for login, server selection/play handoff, character creation through the Java game server, and logout.
- Specialized game-server bridge/control paths remain covered by packet, service, and hosted fake-GS tests. They should be revisited only if later full-client or Java GS testing exposes a behavioral mismatch.

## Parity Watch Notes

- Login-client checksum verification now matches the Java server's live client-packet path: after Blowfish decrypt, Java XORs all 4-byte words except the final ignored word and requires that XOR to be zero. Server-packet encryption still uses the Java append-checksum path.
- Host `javac` is not installed, but Java crypto golden vectors were produced through `eclipse-temurin:8-jdk` in Docker using `dotnetConversion/tools/java-login-crypto-vectors`.
- `LoginClientConnection` uses encrypted frames and was validated by a real client after the live-client checksum rule was aligned with Java.
- Dockerized MySQL integration now runs locally through `dotnetConversion/scripts/start-login-db.ps1`; the normal test suite keeps it dormant unless `AION_LOGIN_DB_INTEGRATION=1`.

## Phase 3 Parity Checklist

### 1. Login Client Crypto And Handshake

- Port Java `network/ncrypt/CryptEngine` exactly:
  - static first Blowfish key: `6B 60 CB 5B 82 CE 90 B1 CC 2B 6C 55 6C 6C 6C 6C` (ported)
  - first server packet special path: add checksum space, align to 8 bytes, XOR pass, encrypt with static key, then update to generated Blowfish key (ported)
  - later packet path: checksum append, 8-byte alignment, encrypt with current key (ported)
  - decrypt path: Blowfish decrypt plus Java live client checksum verification (ported and covered with captured `CM_AUTH_GG` payload)
- Port or prove byte parity for Java `BlowfishCipher` (direct port added; Java-generated vector covered).
- Port Java `KeyGen` behavior:
  - 10 cached RSA keypairs (ported)
  - 1024-bit RSA with public exponent F4 (ported)
  - generated 16-byte Blowfish keys (ported)
- Port Java `EncryptedRSAKeyPair.encryptModulus` scrambling exactly. (ported and covered with Java-generated vector)
- Wire encrypted frame read/write in `LoginClientConnection`. (ported and real-client validated)
- Add golden tests for encrypted `SM_INIT`, checksum verification, key update timing, and decrypt failure behavior. (covered with Java vectors for static Blowfish, encrypted `SM_INIT`, first server-packet encryption/key update, later checksum-packet encryption, captured live-client `CM_AUTH_GG` checksum shape, C# tamper rejection, and real-client login smoke)
- Add Java-generated packet vectors for common login and game-server bridge packets. (covered for the modeled login-client and game-server bridge server packet set)

### 2. Login Credential Authentication

- Port `CM_LOGIN` RSA no-padding credential decrypt in 128-byte blocks. (ported; covered by decrypt tests and encrypted loopback login smoke)
- Reject `CM_LOGIN` packets whose embedded session id does not match the connection session id before decrypting credentials. (ported and covered through encrypted loopback socket smoke)
- Preserve normal login and `-loginex` layout:
  - normal content offset 94, username 14 bytes, password 16 bytes
  - `-loginex` content offset 78, username 64 bytes, password 32 bytes
  - OTP from little-endian int immediately after username/password
- Keep Cp1252 string extraction and null termination behavior. (ported; normal and `-loginex` RSA layouts covered)
- Port account password hashing from Java `AccountUtils.encodePassword`. (ported)

### 3. Existing Database Schema Integration

- Add direct SQL DAO ports for:
  - `AccountDAO` (core load/insert/update fields ported; insert shape aligned with Java's account-data-only write)
  - `AccountTimeDAO` (ported)
  - `GameServersDAO` (ported)
  - `PremiumDAO` (ported)
  - `BannedIpDAO` (ported)
  - `BannedMacDAO` (ported)
  - `BannedHddDAO` (ported)
  - `AccountsLogDAO` (ported)
  - `PlayerTransferDAO` (ported)
- Use existing `account_data`, `account_time`, `gameservers`, and related login DB tables without schema migration. (ported for current DAO set; MySQL integration covers account, game-server, ban, premium, account-log, and player-transfer repositories)
- Preserve Java SQL strings and autocommit behavior unless a verified difference is documented.

### 4. AccountController Flow

- Port `AccountController.login` branch-for-branch:
  - banned IP check through startup-loaded `BannedIpController` cache semantics (ported)
  - optional external auth (ported)
  - account auto-create (ported, including Java default `last_server`)
  - password mismatch responses (ported)
  - activation check (ported)
  - account expiry and penalty checks (ported)
  - forced IP mask check (ported)
  - double-login behavior against LS and GS (ported for request-kick behavior)
  - `updateOnLogin`, last IP update, membership expiry update (ported)
  - brute-force ban escalation (ported)
- Port reconnect behavior:
  - `ReconnectingAccount` (ported)
  - `CM_UPDATE_SESSION` (ported)
  - `SM_UPDATE_SESSION` (ported)
- Port disconnect cleanup:
  - remove LS account if not joined GS (ported)
  - update account time on logout (ported)

### 5. Game-Server Bridge

- Load registered game servers from DB on startup. (ported)
- Enforce registered server ID, password, and IP mask in `CM_GS_AUTH`. (ported)
- Track online/offline game-server state and clear accounts on disconnect. (ported)
- Port ping/pong lifecycle:
  - send `SM_PING` every 5 seconds (ported)
  - close after more than 2 unanswered pings (ported)
- Port account bridge packets:
  - account auth response (ported)
  - account reconnect key (ported)
  - account disconnected (ported)
  - account list sync (ported)
  - account connection info (ported for DB update/log path)
  - GS character count response (ported)
- Port admin/control bridge packets:
  - LS control (ported)
  - ban control (ported)
  - mac ban control/list (ported)
  - HDD ban control/list (ported)
  - allowed HDD serial change (ported)
  - premium control (ported)
  - account toll info (ported)
  - player transfer control (ported; covered with parser tests, hosted bridge dispatch, and service-level DB/GS collaborator tests)
  - request kick account (ported)

### 6. Server List And Play Flow

- Preserve Java `CM_SERVER_LIST` behavior:
  - validate session key (ported)
  - close with no-server response when no GS exists (ported)
  - request per-GS character counts (ported)
  - send `SM_SERVER_LIST` only after all counts are known (ported; covered through fake-GS loopback socket smoke)
- Preserve Java `CM_PLAY` behavior:
  - validate session key (ported)
  - check GS online state (ported)
  - check min access level (ported)
  - check full server (ported)
  - mark client as joined GS (ported)
  - send exact `SM_PLAY_OK` / `SM_PLAY_FAIL` response (ported for core cases; `SM_PLAY_OK` covered through fake-GS loopback socket smoke)
- Update server lists for logged-in players when GS state changes. (ported for account-list sync and GS disconnect)

### 7. Startup, Shutdown, And Validation

- Match Java startup ordering: config, DB factory, game-server table, key generation, player-transfer scheduler, listener startup. (locally ported for Phase 3 responsibilities; Java database config initializes `DatabaseFactory` before repository-backed hosted-service startup; registered game servers and banned IP load before listeners; MAC/HDD expired-ban cleanup before listeners with lazy map load on first use; player-transfer scheduler starts before listeners; mixed Java GS / C# LS startup smoke validated)
- Load Java `.properties` from `config/main`, `config/network`, and `config/myls.properties` using identical keys. (ported and covered for current login and database options)
- Add graceful shutdown behavior equivalent to Java pending-close semantics where packet sends must complete before closing. (ported at connection send/close, player-transfer-before-network shutdown order, and listener shutdown level; local loopback smoke covered; real-client logout validated)
- Validate with:
  - packet golden tests for encrypted and unencrypted frames
  - DAO fixture tests against the current login schema
  - C# login server plus Java game server mixed mode
  - real client login, server list, server select/play handoff, character creation, and logout smoke test

## Verification

- `dotnet test AionServer.slnx`
- Result: all tests passing, 180 total.
- `AION_LOGIN_DB_INTEGRATION=1 dotnet test tests\Aion.LoginServer.Tests\Aion.LoginServer.Tests.csproj --filter LoginDatabaseIntegrationTests`
- Result: 4 tests passed against MySQL 8.4 on localhost:3307.

## Optional MySQL Integration Test

Start a local login DB container after Docker Desktop is running:

```powershell
cd dotnetConversion
powershell -ExecutionPolicy Bypass -File scripts\start-login-db.ps1
```

Then run the opt-in repository integration test:

```powershell
$env:AION_LOGIN_DB_INTEGRATION = "1"
$env:AION_LOGIN_DB_PORT = "3307"
$env:AION_LOGIN_DB_PASSWORD = "aion"
dotnet test tests\Aion.LoginServer.Tests\Aion.LoginServer.Tests.csproj --filter LoginDatabaseIntegrationTests
```
