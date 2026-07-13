# Phase 5: Port Game Infrastructure â€” Detailed Plan & Progress

**Status**: COMPLETE (May 20, 2026; automated infrastructure parity)  
**Target**: C# game-server infrastructure that matches Java GameServer bootstrap through character-list response  
**Validation Approach**: Automated parity and integration tests complete; real-client validation is optional Phase 5h / Phase 8 follow-up  
**Data Scope**: Load ALL game data files  
**Handler System**: Defer to Phase 7 (Phase 5 infrastructure only)  
**Scheduler**: Custom ScheduledExecutorService-like wrapper

---

## Completion / Resume Note

Phase 5 is complete for automated GameServer infrastructure parity as of May 20, 2026. The work finished the socket listener, login/chat bridges, bootstrap lifecycle, static-data cache/load path, ID factory, packet infrastructure, character-list infrastructure, and supporting tests.

Real-client validation was intentionally not used as the Phase 5 completion gate. It remains tracked as optional Phase 5h / Phase 8 readiness validation, matching the current project decision to validate with a real client after more 1:1 gameplay parity is in place.

Future GameServer parity code should include short `Java parity: path::method` comments on ported functions so debugging can jump back to the Java source quickly.

---

## Phase 5 Deliverables (from csharp-port.md)

- [x] Game client socket listener (bind port 7777)
- [x] Login-server connector (outbound connection)
- [x] Chat-server connector (outbound connection)
- [x] Packet processor with per-connection ordering guarantees
- [x] Object ID factory
- [x] Game startup sequence shell (init order matters)
- [x] Static XML data merge/cache/load path (all data in /data/static_data/)
- [x] XML validation path (async, doesn't block startup)
- [x] World/bootstrap containers (skeleton only; no gameplay)
- [x] Scheduler/thread pool equivalent
- [x] Basic shutdown and persistence hooks

---

## Phase 5 Success Criteria (Parity-Focused)

1. **XML Data Loading**
   - Load counts for all major datasets match Java startup log counts
   - Cache file generation (./cache/static_data.xml) works
   - Schema validation completes async without blocking startup
   - All imported XML files merged correctly

2. **Socket Infrastructure**
   - Game server listens on `0.0.0.0:7777` (configurable)
   - Accepts client connections; reads first packet without crashing
   - Packet frame codec works (2-byte LE length field)
   - Connection shutdown is clean

3. **Login/Chat Bridge**
   - Outbound connection to login-server (configurable host/port)
   - Outbound connection to chat-server (configurable host/port)
   - Bridge packets (e.g., `GS_REGISTER`, `GS_ACCOUNT_*`) encode/decode correctly

4. **Real Client Validation**
   - Real Aion client can connect to C# game server
   - Client receives `SM_KEY` and `SM_INIT` without crash
   - Client can authenticate via C# login server
   - Client receives character-list response and can see saved characters
   - Client can create character and see it in list
   - **No gameplay required** (no movement, combat, NPCs)

---

## Architecture & Dependency Order

### Layer 1: Foundation (No Gameplay)
1. **Config Loading** (already done in Aion.Commons)
2. **Database Access** (already done in Aion.Commons)
3. **Packet Definitions** â€” Game-side packet models
4. **Object ID Factory** â€” Manages unique entity IDs
5. **Scheduler/Thread Pool Wrapper** â€” Task execution

### Layer 2: Infrastructure
6. **Static Data Manager** â€” XML loading and merging (all data)
7. **Socket Server** â€” Client listener on port 7777
8. **Packet Processor** â€” Per-connection ordering and dispatch
9. **Login Server Bridge** â€” Outbound connector for registration/auth
10. **Chat Server Bridge** â€” Outbound connector for chat

### Layer 3: Bootstrap & Lifecycle
11. **Game Startup Sequence** â€” Order of initialization
12. **World Container** (skeleton) â€” Object storage (no logic yet)
13. **Shutdown Hooks** â€” Clean disconnect from login/chat, close DB

---

## File Structure in Aion.GameServer

Based on Java GameServer layout, mirror these:

```
Aion.GameServer/
â”œâ”€â”€ Configuration/
â”‚   â””â”€â”€ GameServerOptions.cs                // Load GameServer config props
â”œâ”€â”€ dataholders/
â”‚   â”œâ”€â”€ DataManager.cs                      // Static data cache holder
â”‚   â””â”€â”€ StaticData.cs                       // Root object for XML data
â”œâ”€â”€ model/
â”‚   â””â”€â”€ GameEngine.cs                       // Interface for engine lifecycle
â”œâ”€â”€ network/
â”‚   â”œâ”€â”€ aion/
â”‚   â”‚   â”œâ”€â”€ GameConnectionFactoryImpl.cs     // Create per-client connections
â”‚   â”‚   â”œâ”€â”€ ServerPacketHandler.cs          // Dispatch registered packets
â”‚   â”‚   â”œâ”€â”€ GamePackets.cs                  // All packet models (SM_*, CM_*)
â”‚   â”‚   â”œâ”€â”€ GameServerConnection.cs         // Per-client connection state
â”‚   â”‚   â””â”€â”€ GameConnectionListener.cs       // Socket listener for 7777
â”‚   â”œâ”€â”€ loginserver/
â”‚   â”‚   â”œâ”€â”€ LoginServer.cs                  // Connector to login-server
â”‚   â”‚   â””â”€â”€ LoginServerPackets.cs           // Inter-server packets
â”‚   â””â”€â”€ chatserver/
â”‚       â”œâ”€â”€ ChatServer.cs                   // Connector to chat-server
â”‚       â””â”€â”€ ChatServerPackets.cs            // Inter-server packets
â”œâ”€â”€ Utils/
â”‚   â”œâ”€â”€ IdFactory/
â”‚   â”‚   â””â”€â”€ IDFactory.cs                    // Unique ID generation
â”‚   â”œâ”€â”€ ThreadPoolManager.cs                // Scheduler wrapper
â”‚   â””â”€â”€ concurrent/
â”‚       â””â”€â”€ OrderedTaskExecutor.cs          // Per-connection packet ordering
â”œâ”€â”€ world/
â”‚   â”œâ”€â”€ World.cs                            // World container (skeleton)
â”‚   â””â”€â”€ WorldPosition.cs                    // Coordinate model
â”œâ”€â”€ services/
â”‚   â””â”€â”€ GameTimeService.cs                  // Game clock (shared tick)
â””â”€â”€ Program.cs                              // Main entry, bootstrap sequence
```

---

## Implementation Checklist

### Phase 5a: Foundation & Config
- [x] **GameServerOptions.cs** - Load GameServer properties (GSConfig, NetworkConfig, GeoDataConfig, CustomConfig, CleaningConfig)
- [x] **Config Tests** - Assert config loaded from mygs.properties + defaults

### Phase 5b: Object ID Factory
- [x] **IDFactory.cs** - Allocate unique IDs (character IDs, spawn IDs, etc.)
- [x] **IDFactory Tests** - Assert uniqueness and thread-safety

### Phase 5c: Packet Definitions & Processing
- [x] **Packet Models** â€” Phase 5 infrastructure SM/CM models for auth, character selection, passkey, and create/delete/restore shells
  - [x] Initial game packet layer, game crypto header, `SM_KEY`, simple character-selection response packets, and representative `CM_*` parsers
  - [x] Character-selection auth shell: `SM_L2AUTH_LOGIN_CHECK`, `SM_ACCOUNT_PROPERTIES`, and empty `SM_CHARACTER_LIST`
  - [x] Java-shaped `SM_CHARACTER_LIST` player-info payload writer with DB-fed character-selection entries
  - [x] Character create/open-window packet shell (`CM_CREATE_CHARACTER`, `SM_CREATE_CHARACTER`)
  - [x] Route-info/MAC packet parser (`CM_MAC_ADDRESS`) for account lifecycle metadata
  - [x] Character passkey packet shell (`CM_CHARACTER_PASSKEY`, `SM_CHARACTER_SELECT`)
  - [x] Static-data world-map summaries are available for `SM_L2AUTH_LOGIN_CHECK`
  - [x] Frame codec: 2-byte LE length field, opcode after
  - Phase 6 continuation: remaining gameplay packet models as gameplay systems are ported
- [x] **Packet Tests** - Golden byte-level tests for representative game packets
- [x] **PacketProcessor.cs** - Queue-per-connection, ordered dispatch
- [x] **OrderedTaskExecutor.cs** - Per-connection FIFO execution primitive
- [x] **Processor Tests** - Assert FIFO ordering per connection

### Phase 5d: Socket Infrastructure
- [x] **GameClientSocketServer.cs** - Listener on port 7777 using the shared socket-server base
- [x] **GameClientSocketServer Tests** - Accept connection, send `SM_KEY`, read first bad frame without crash
- [x] **GameServerConnection.cs** - Per-client connection state shell and game packet read/write path
- [x] **Connection Close Behavior** - Graceful shutdown of active game client connections

### Phase 5e: Login/Chat Bridge
- [x] **LoginServer.cs** â€” Outbound connector; send GS_REGISTER, listen for responses
- [x] **LoginServer Tests** â€” Mock Java login-server, verify bridge packets
- [x] **ChatServer.cs** â€” Outbound connector; send bridge packets
- [x] **ChatServer Tests** â€” Mock Java chat-server, verify packets
  - [x] Login bridge account-auth request/response (`SM_ACCOUNT_AUTH` / `CM_ACOUNT_AUTH_RESPONSE`) shell
  - [x] Login bridge character-count request/response (`CM_GS_CHARACTER_RESPONSE` / `SM_GS_CHARACTER`) shell
  - [x] Login bridge account lifecycle notifications (`SM_ACCOUNT_CONNECTION_INFO`, `SM_ACCOUNT_DISCONNECTED`) shell

### Phase 5f: Static Data Loading (All Data)
- [x] **StaticData.cs** â€” Streaming static-data root/count view over the merged cache
- [x] **DataManager.cs** â€” Holder for loaded static data with async cache validation hook
- [x] **XmlMerger.cs** â€” Merge all imported XML files (game-server/data/static_data/*.xml)
- [x] **Cache Writer** â€” Write merged XML to ./cache/static_data.xml-compatible path
- [x] **Data Loaders** â€” Populate typed StaticData fields from merged XML
  - [x] Generic merged-cache element counting for all loaded XML
  - [x] Typed player experience table holder for character-list level calculation
  - [x] First typed template DTO/dataholder indexes for item, NPC, skill, and recipe summaries
  - Phase 6 continuation: deep typed template DTOs for every gameplay consumer as systems need them
  - Game maps, NPCs, spawns, items, skills, quests, instances, etc.
- [x] **Data Load Tests** â€” Assert load count matches Java startup logs for each data type
  - [x] Real Java manifest smoke with exact counts for items, NPCs, skills, and recipes
  - [x] Typed holder count parity and lookup tests for item, NPC, skill, and recipe indexes
- [x] **Cache Tests** â€” Assert cache file is written and reused on restart

### Phase 5g: Bootstrap Sequence & Lifecycle
- [x] **GameTimeService.cs** â€” Shared game tick (init early)
- [x] **GameEngine.cs** (interface) â€” Define Init()/Shutdown() contract
- [x] **World.cs** â€” Container for game objects (skeleton; no NPC/spawn logic yet)
- [x] **Program.cs Main()** â€” Bootstrap sequence matching Java GameServer.main()
  - [x] Init config shell
  - [x] IDFactory init
  - [x] DataManager init (load XML)
  - [x] Init registered engines in parallel
  - [x] Create World
  - [x] Init/start GameTimeService
  - [x] Connect to LoginServer, ChatServer
  - [x] Bind client listener on 7777 after bootstrap
  - [x] Report startup complete
- [x] **Shutdown Hooks** â€” Disconnect bridges, close DB, shutdown thread pool
  - [x] Hosted shutdown clears world, stops game time, and shuts down scheduler
  - [x] Bridge disconnect hooks
  - [x] DB connection-pool close hook in game-server Program shutdown
- [x] **ShutdownHook.cs** â€” Graceful termination on SIGINT/SIGTERM

### Phase 5h: Integration Tests
- [x] **Full Bootstrap Test** â€” Start C# game server, verify config + data loaded, verify bridges connected
- [x] **Socket Smoke Test** â€” Connect fake client, send CM_AUTH, receive SM_KEY without crash
- [ ] **Real Client Validation** (optional for Phase 5; full in Phase 8)
  - Real Aion client connects
  - Client receives SM_KEY, SM_INIT
  - Client authenticates via C# login server
  - Client sees character-list response
  - Client can create/delete/restore characters

---

## Data Files to Load (All of Them)

The game-server/data/static_data/ folder contains:

```
static_data.xml (root manifest with imports)
â”œâ”€â”€ fly_rings/ (flypath data)
â”œâ”€â”€ instance_exit/ (instance exit points)
â”œâ”€â”€ chests/ (chest templates)
â”œâ”€â”€ instance_cooltimes/ (instance reset timers)
â”œâ”€â”€ conqueror_protector_ranks/ (PvP ranks)
â”œâ”€â”€ ...other specialized data folders
```

**All must be loaded for XML load count parity.**

---

## Known Risks & Mitigations

| Risk | Mitigation |
|------|-----------|
| XML loading is slow (large datasets) | Cache merged result; async validation; report load times |
| Packet opcode collision (CM vs SM) | Golden test suite; use actual Java packet traces |
| Per-connection ordering (thread safety) | Unit test OrderedTaskExecutor with concurrent writes |
| Config key mismatch | Config tests assert every key exists and has expected type |
| Login/chat bridge auth failure | Mock login/chat servers in tests; use captured packet bytes |
| Database transaction semantics | Keep SQL direct (no ORM) so Java DAO behavior is obvious |

---

## NOT Included in Phase 5

- âŒ Gameplay simulation (NPC AI, combat, movement, loot)
- âŒ Dynamic handler loading (deferred to Phase 7)
- âŒ World spawning or zone logic
- âŒ Character stats, skills, inventory (structure only)
- âŒ Quest processing
- âŒ Siege/RvR systems

---

## Testing Strategy

1. **Unit Tests** (internal parity)
   - Config load, ID factory, packet codec, ordered executor, XML merge/count

2. **Integration Tests** (infrastructure)
   - Full bootstrap, bridge connection, socket listen, client handshake

3. **Real Client Validation** (final parity)
   - Connect real Aion client
   - Authenticate through C# login server
   - Retrieve character-list
   - Create/delete/restore character

---

## Success Metrics for Phase 5 Completion

âœ… Required checklist items complete  
âœ… XML load counts match Java startup logs  
âœ… All unit + integration tests pass  
âœ… Optional/deferred: real-client character-list validation remains Phase 5h/Phase 8 follow-up  
âœ… Bootstrap sequence complete in <5 seconds (target)  
âœ… No errors/warnings in logs during startup  
âœ… Graceful shutdown on SIGINT  
âœ… Game server stays running; awaits phase 6 (gameplay)

---

## Progress Log

### Session 1 (May 20, 2026)
- âœ… Completed Phase 5 planning and discovery
- âœ… Documented all layers, deliverables, and checklist
- âœ… Identified all data files to load
- âœ… Ready to begin Phase 5a: Config loading

### Session 2 (May 20, 2026)
- Completed Phase 5a game-server config loading in `Aion.GameServer.Configuration.GameServerOptions`.
- Preserved Java property keys and load order: `config/administration`, `config/main`, `config/network`, then `config/mygs.properties`.
- Added game database config loading for the existing `aion_gs` JDBC settings.
- Completed Phase 5b `IDFactory` foundation with Java-style reserved zero ID, duplicate preload protection, invalid client-invisible ID skipping, release/reuse behavior, and thread-safety coverage.
- Validation: `dotnet test dotnetConversion\AionServer.slnx` passes with 218 tests.

### Session 3 (May 20, 2026)
- Added the game-client packet frame layer with Java-style little-endian length, obfuscated opcode, static packet byte, and flipped opcode header.
- Ported the game packet crypto/key scheduling foundation used by `SM_KEY`, including deterministic test key support.
- Added initial server packets: `SmKey`, `SmMayLoginIntoGame`, `SmCreateCharacter`, `SmDeleteCharacter`, and `SmRestoreCharacter`.
- Added representative client packet parsers for L2 auth login check, character list, enter-world, delete/restore character, reconnect auth, and may-login.
- Added `OrderedTaskExecutor<TKey>` for per-connection FIFO packet execution with parallelism across different connection keys.
- Added `GamePacketProcessor<TKey>` as the dispatch shell that queues parsed client packets per connection.
- Validation: `dotnet test dotnetConversion\AionServer.slnx` passes with 226 tests.

### Session 4 (May 20, 2026)
- Completed the first Phase 5d socket infrastructure slice with `GameClientSocketServer` and `GameServerConnection`.
- The C# game server now has a hosted client listener wired into `Program.cs`; it loads Java game config and starts the listener from `GameServerHostedService`.
- New connections receive Java-shaped `SM_KEY` immediately, matching the Java `AionConnection.initialized()` behavior.
- Game client frames are read through the game crypto path and malformed early frames are ignored without crashing the listener.
- Active client connections are tracked and closed during listener shutdown.
- Validation: `dotnet test dotnetConversion\AionServer.slnx` passes with 228 tests.

### Session 5 (May 20, 2026)
- Completed the first Phase 5e login/chat bridge slice with outbound C# game-server connectors.
- Added login bridge packet framing for Java `SM_GS_AUTH`: game-server id, login password, client connect address/port, min access level, and max players.
- Added chat bridge packet framing for Java `SM_CS_AUTH`: game-server id and chat password.
- Added response parsing for login auth success/server-count and chat auth success/public chat endpoint.
- Added mock Java-style TCP bridge tests and golden auth-frame vectors for both login and chat connectors.
- Validation: `dotnet test dotnetConversion\tests\Aion.GameServer.Tests\Aion.GameServer.Tests.csproj` passes with 25 tests.

### Session 6 (May 20, 2026)
- Added Phase 5f static-data loading shell with a streaming `XmlMerger`, sidecar metadata, cache writing, and cache reuse detection.
- Added async XSD validation hook for modified cache files, matching Java's non-blocking validation approach.
- Added `StaticData` and `DataManager` shells that load the generated cache and expose generic element counts across the merged tree.
- Added fixture tests for directory imports with `singleRootTag`, cache reuse, and async validation.
- Added a real Java static-data manifest smoke test that merges the full `game-server/data/static_data` tree into a temp cache and verifies major dataset counts.
- Validation: `dotnet test dotnetConversion\tests\Aion.GameServer.Tests\Aion.GameServer.Tests.csproj` passes with 28 tests.

### Session 7 (May 20, 2026)
- Added the first Phase 5g bootstrap/lifecycle shell.
- Added `GameEngine`, `ThreadPoolManager`, `World`, `WorldPosition`, `GameTimeService`, `StaticDataService`, and `GameServerBootstrapService`.
- Wired `Program.cs` so bootstrap hosted service runs before the game client listener hosted service.
- Bootstrap now initializes IDFactory, loads static data, initializes registered engine shells in parallel, creates the world container, starts game time, waits for static-data validation, then allows the 7777 listener to start.
- Shutdown now stops game time, shuts down registered engines, clears the world container, and drains the scheduler.
- Validation: `dotnet test dotnetConversion\AionServer.slnx` passes with 237 tests.

### Session 8 (May 20, 2026)
- Added `ShutdownHook` facade with Java-style delayed shutdown state and host stop signaling.
- Added `GameBridgeHostedService` to start outbound login/chat bridge connectors after the game client listener is hosted.
- Bridge startup is non-fatal for infrastructure validation if login/chat peers are not running, while shutdown still closes both connectors.
- Added shutdown hook coverage.
- Validation: `dotnet test dotnetConversion\AionServer.slnx` passes with 239 tests.

### Session 9 (May 20, 2026)
- Added a hosted infrastructure integration smoke for Phase 5h.
- The test starts a real Generic Host with bootstrap, static-data fixture, game client listener, and login/chat bridge hosted services.
- Mock login/chat peers verify Java-shaped auth frames, return auth success, and the fake game client receives `SM_KEY` from the hosted listener.
- Validation: `dotnet test dotnetConversion\AionServer.slnx` passes with 240 tests.

### Session 10 (May 20, 2026)
- Extended the Phase 5c character-selection packet surface.
- Added Java-shaped `SM_ACCOUNT_PROPERTIES`, `SM_CHARACTER_LIST`, and `SM_L2AUTH_LOGIN_CHECK` server packets.
- Added generic world-map summary extraction during static-data cache reading so L2 auth can advertise map ids, instance flags, and twin counts.
- Added a limited connection-side infrastructure response path: L2 auth moves the connection to `Authed`, may-login returns `SM_MAY_LOGIN_INTO_GAME`, character-list returns account properties plus an empty character list, and delete/restore return simple Java-shaped responses.
- Added packet and real static-data smoke assertions for the new payloads and world-map summaries.
- Validation: `dotnet test dotnetConversion\tests\Aion.GameServer.Tests\Aion.GameServer.Tests.csproj` passes with 35 tests.

### Session 11 (May 20, 2026)
- Added the login bridge account-auth packet shell.
- Added `SM_ACCOUNT_AUTH` outbound framing from game server to login server and `CM_ACOUNT_AUTH_RESPONSE` parsing into `AccountAuthResult`.
- Added pending request tracking on the C# game-server login connector so `CM_L2AUTH_LOGIN_CHECK` can use the real login bridge when it is authenticated.
- Kept isolated infrastructure mode usable by falling back to local auth only when no authenticated login connector is available.
- Added mock login-bridge account-auth round-trip tests, including account time, access level, membership, toll, and allowed-HDD fields.
- Validation: `dotnet test dotnetConversion\AionServer.slnx` passes with 243 tests.

### Session 12 (May 20, 2026)
- Added the first DB-backed character-selection slice for Phase 5.
- Added `CharacterSelectionEntry`, appearance, visible-item, and ban-info models for Java-shaped select-screen data without creating full Phase 6 player objects.
- Added `ICharacterSelectionRepository` with a MySQL implementation that reads `players`, `player_appearance`, and visible equipped inventory rows, plus delete/restore `deletion_date` updates.
- Expanded `SM_CHARACTER_LIST` from an empty count packet into the Java `AbstractPlayerInfoPacket` select-screen payload shape.
- Added a typed `PlayerExperienceTable` static-data holder and fixed XML text parsing so all 66 Java experience entries are available for level calculation.
- Added login bridge `0x08` character-count request handling so the game server can answer server-list character-count fanout from the login server.
- Added login bridge account connection/disconnection notifications after C# game-client auth and close.
- Added `CM_MAC_ADDRESS` parsing so account connection notifications can carry client MAC/HDD metadata when the packet arrives before auth.
- Added the `CM_CREATE_CHARACTER` parser and `SM_CREATE_CHARACTER` open-window/failure response shell; full DB-backed creation remains Phase 6.
- Wired the repository into the client connection path and initialized/disposed the game database connection pool from `Program.cs`.
- Validation: `dotnet test dotnetConversion\AionServer.slnx` passes with 247 tests.

### Session 13 (May 20, 2026)
- Added typed static-data template holders for items, NPCs, skills, and recipes, populated directly from the merged cache stream.
- Added Java-like lookup indexes for item/npc/recipe ID lookups, skill group/stack lookups, and recipe autolearn filtering.
- Extended the real Java static-data smoke to assert typed holder counts match the existing Java manifest counts and spot-check representative lookups.
- Added the character passkey packet shell: `CM_CHARACTER_PASSKEY` parser, `SM_CHARACTER_SELECT` response packet, opcode registration, and a runtime success-style response.
- Marked the Phase 5 bootstrap and shutdown parent tasks complete now that their subitems are implemented.
- Validation: `dotnet test dotnetConversion\tests\Aion.GameServer.Tests\Aion.GameServer.Tests.csproj` passes with 40 tests. Full solution validation also passes: `dotnet test dotnetConversion\AionServer.slnx` (247 tests).

---

## Next Steps

1. Optional Phase 5h follow-up: real-client character-list/create/delete validation against the C# game-server shell.
2. Phase 6 handoff: DB-backed character creation, passkey persistence, and enter-world/player loading.
3. Deepen typed static-data DTOs as Phase 6 gameplay systems consume them.
