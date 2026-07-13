# Aion Server Commons Project - Detailed Architecture Breakdown

## 1. MODULE ORGANIZATION

### Directory Structure
```
commons/
├── src/com/aionemu/commons/
│   ├── configs/              # Configuration classes with @Property annotations
│   ├── configuration/        # Config binding system (JAXB-like property processing)
│   ├── database/             # Connection pooling & SQL utilities
│   ├── logging/              # Logging initialization & Discord webhooks
│   ├── network/              # NIO-based socket/packet handling infrastructure
│   ├── options/              # Build-time assertions
│   ├── scripting/            # Dynamic Java script compilation & loading
│   └── utils/                # Utility functions (networking, randomization, threading)
└── test/                     # Unit tests for ConfigurableProcessor
```

### Package Breakdown

#### **1.1 configs/** - Configuration Holder Classes
- **CommonsConfig.java** - Global commons settings (@Property annotations)
  - `RUNNABLESTATS_ENABLE`: Debug flag for execution statistics
  - `SCRIPT_COMPILER_CACHING`: Enable/disable script compilation cache
- **DatabaseConfig.java** - Database connection settings
  - Used by HikariCP pool initialization
- Pattern: Static fields with @Property annotation, populated by ConfigurableProcessor

#### **1.2 configuration/** - Property Binding Framework
**Core Classes:**
- **Property.java** - Runtime annotation marking fields for property binding
  - `key` - Property name in config files
  - `defaultValue` - Fallback if not found (or special value `DEFAULT_VALUE` to skip)
  - Example: `@Property(key = "commons.runnablestats.enable", defaultValue = "false")`

- **ConfigurableProcessor.java** - Main reflection-based config loader
  - Recursively processes annotated fields (static + instance)
  - Walks class hierarchy (interfaces, superclasses)
  - Resolves property placeholders `${propertyName}`
  - Returns unused properties set for validation
  - **Usage**: `ConfigurableProcessor.process(Properties props, Object... objectsOrClasses)`

- **Properties.java** - Alternative annotation supporting regex key patterns

- **TransformationTypeInfo.java** - Type metadata for transformation
  - Generic type extraction for List<T>, Set<T>
  - Factory for creating transformer instances

- **TransformationException.java** - Config binding errors

**Transformers Directory (13 classes):**
- **PropertyTransformer.java** - Interface defining type converters
- **PropertyTransformers.java** - Registry pattern, lists supported types:
  - Number (int, long, double, float, etc.)
  - Boolean, Character, String
  - Enum types
  - Arrays, Collections (List, Set)
  - File, InetSocketAddress, Pattern
  - TimeZone, ZoneId
  - Class (string → Class<?> lookup)
  - MapTransformer, CommaSeparatedValueTransformer

**Key Pattern**: Extensible transformer registry - can register custom transformers
```java
PropertyTransformers.register(new CustomTransformer());
```

#### **1.3 database/** - SQL Connection Management
- **DatabaseFactory.java** - Singleton HikariCP pool
  - `init()` - Initialize with DatabaseConfig properties
  - `getConnection()` - Acquire connection from pool
  - Enforces auto-commit mode

- **DB.java** - Static facade for SQL operations
  - `select(String query, ReadStH reader, [errMsg])` - SELECT queries
  - `insertUpdate(String query, IUStH handler, [errMsg])` - INSERT/UPDATE
  - `callFunction(String func, CallReadStH handler)` - Callable statements
  - Automatic connection recycling

- **Statement Handler Interfaces:**
  - **ReadStH** - Process ResultSet from SELECT
  - **ParamReadStH** - Extends ReadStH, set PreparedStatement parameters
  - **IUStH** - Handle INSERT/UPDATE (must call `executeBatch()` or `executeUpdate()`)
  - **CallReadStH** - Process CallableStatement results

- **Transaction.java** - Not fully used; represents transactional ops

#### **1.4 logging/** - Logging System with Discord Integration
- **Logging.java** - Initialization & log archival
  - `init()` - **MUST call before any logger instantiation**
    - Sets `ClassicConstants.CONFIG_FILE_PROPERTY` → `config/logback.xml`
  - `archiveLogs()` - Automatically zips old logs between runs
    - Uses server start time marker file
    - Creates timestamped archives in `log/archived/`
    - Cleans up old .log files

- **DiscordChannelAppender.java** - Custom Logback appender
  - Sends ERROR/WARN messages to Discord webhook
  - Handles 2000-char Discord limit by splitting messages
  - Preserves code blocks (```...``` markers)
  - Async appender wrapper prevents blocking on webhook calls
  - Configuration:
    ```xml
    <appender name="app_status_discord" class="com.aionemu.commons.logging.DiscordChannelAppender">
      <encoder><pattern>%msg</pattern></encoder>
      <webhookUrl>${discord.webhook.url}</webhookUrl>
      <userName_avatarUrl_msg_separator>\|</userName_avatarUrl_msg_separator>
    </appender>
    ```

- **OnConsoleWarningStatusListener.java** - Logs Logback internal warnings to console

#### **1.5 network/** - NIO Socket & Packet Pipeline
**Architecture**: Selector-based NIO with separate accept/read-write threads

**Core Classes:**

- **NioServer.java** - Main server bootstrap
  - `NioServer(int readWriteThreads, ServerCfg... cfgs)` - Constructor
  - `connect(Executor dcExecutor)` - Bind and start accepting
  - Manages multiple dispatcher threads:
    - **1 Accept Dispatcher** - Accepts new connections
    - **N Read/Write Dispatchers** - Process I/O (load-balanced)
    - Special case: if `readWriteThreads < 1`, uses single AcceptReadWriteDispatcherImpl
  - Configuration via **ServerCfg**: address, port, connection factory, client description

- **Dispatcher.java** - Abstract thread managing SelectionKey dispatch
  - Extends Thread, runs selector event loop
  - Abstract methods: `dispatch()`, `closeConnection(AConnection)`
  - Thread-safe registration via `gate` object synchronization
  - Handles Selector.select() with optional timeout

- **AcceptReadWriteDispatcherImpl.java** - Combined accept + read/write dispatcher
  - Processes selected keys by operation type:
    - OP_ACCEPT → call accept(key)
    - OP_READ → call read(key)
    - OP_WRITE → call write(key)
    - OP_READ|OP_WRITE → read then write if still valid
  - Maintains `pendingClose` list - connections marked for closure
  - Graceful close: waits until send queue empty (max 2 seconds)

- **AcceptDispatcherImpl.java** - Accept-only dispatcher (separate thread)

- **Acceptor.java** - Handles new socket connections
  - `accept(SelectionKey key)` - Called when ServerSocketChannel ready
  - Creates AConnection via ConnectionFactory
  - Registers socket to read-write dispatcher
  - Sets TCP_NODELAY = true, SO_LINGER = 10s for graceful shutdown

- **AConnection<T extends BaseServerPacket>** - Abstract connection handler
  - **Buffers**:
    - `writeBuffer` (LITTLE_ENDIAN) - Outgoing packet data
    - `readBuffer` (LITTLE_ENDIAN) - Incoming packet data
  - **Packet Flow**:
    - `sendPacket(T serverPacket)` - Add to queue, enable OP_WRITE
    - `read()` called by dispatcher → fills readBuffer → `processData(ByteBuffer)`
    - `write()` called by dispatcher → `writeData(ByteBuffer)` → flushes writeBuffer
  - **Lifecycle**:
    - `initialized()` - Hook after registration (send welcome packet)
    - `onDisconnect()` - Called on disconnect (cleanup)
    - `onServerClose()` - Called on server shutdown
  - **Synchronization**:
    - `guard` object for packet queue, close state
    - `locked` flag for PacketProcessor synchronization (try-lock mechanism)
    - `pendingCloseUntilMillis` - Grace period for close

- **ConnectionFactory.java** - Interface for creating AConnection instances
  - Implementations create game-server, chat-server, login-server connections
  - Single method: `AConnection create(SocketChannel, Dispatcher)`

- **ServerCfg.java** - Configuration record
  - Bind address, client type description, factory reference

- **PacketProcessor.java** - Queues & dispatches client packets to execution threads

#### **1.6 network/packet/** - Packet Base Classes

- **BasePacket.java** - Abstract root for all packets
  - `opCode` - Packet operation code (type identifier)
  - `getPacketName()` - Simple class name
  - `toFormattedPacketNameString()` - Format "[OPC] PacketName"

- **BaseClientPacket<T extends AConnection>** - Packets from client
  - Extends BasePacket, implements Runnable
  - `read()` - Parse data from ByteBuffer (deserialize)
    - Catches BufferUnderflowException, logs partially-read packets
  - `run()` - Execute after deserialization (dispatch to business logic)
  - Tracks partially-read packets (ConcurrentHashMap) to avoid re-logging same error

- **BaseServerPacket** - Packets to client
  - Abstract: `writeImpl(ByteBuffer buf)` - Serialize to buffer
  - Called by write pipeline

#### **1.7 options/** - Build-Time Assertions
- **Assertion.java** - Compile-time constant flags
  - `NetworkAssertion` - Enables network layer runtime checks
  - If false, all `assert` statements are removed by javac

#### **1.8 utils/** - Utility Classes

**Concurrent (Threading/Scheduling):**
- **PriorityThreadFactory.java** - Creates named thread groups with priority
  - Example: "AcceptDispatcher-1", "ReadWrite-Dispatcher-2"
  - Useful for thread monitoring/debugging

- **DeadLockDetector.java** - Periodic deadlock detection
  - Uses ThreadMXBean to find deadlocked threads
  - Logs full stack traces of all threads on deadlock
  - Calls action callback (typically logs and exits)

- **ExecuteWrapper.java** - Executor with execution time tracking
  - Logs warning if task takes longer than threshold
  - Optionally catches/logs throwables
  - Integrates with RunnableStatsManager for metrics

- **RunnableWrapper.java** - Simple wrapper delegating to ExecuteWrapper

- **RunnableStatsManager.java** - Aggregates execution statistics
  - Per-class execution times, counts
  - Useful for performance profiling

- **UncaughtExceptionHandler.java** - Global thread exception handler

- **AionRejectedExecutionHandler.java** - ThreadPoolExecutor rejection policy
  - Called when queue full and no threads available

**Other Utils:**
- **Rnd.java** - Random number generation (used by crypto)
- **PropertiesUtils.java** - .properties file loading
- **NetworkUtils.java** - IP/socket utilities
- **GenericValidator.java** - Input validation
- **ClassUtils.java** - Class reflection utilities
- **VersionInfo.java** - Version/build metadata
- **SystemInfo.java** - JVM/OS information
- **ExitCode.java** - Enum of exit codes

#### **1.9 scripting/** - Dynamic Java Compilation & Hotloading

- **ScriptManager.java** - Manages script contexts (load/reload/unload)
  - Loads .java files from directories recursively
  - Supports global ClassListener for post-compilation hooks

- **ScriptContext.java** - Interface defining script environment
  - `init()` - Compile and load all scripts
  - `reload()` - Recompile and hotload
  - `shutdown()` - Cleanup

- **ScriptCompiler.java** - Interface for compilation
  - Returns CompilationResult with error details

- **ScriptCompilerImpl.java** - Java Compiler API implementation
  - Uses javax.tools.JavaCompiler
  - Supports caching via ScriptCompilerCache

- **ScriptClassLoader.java** - Custom ClassLoader
  - VirtualClassURLStreamHandler/Connection for runtime-generated bytecode

- **ClassListener/OnClassLoadUnloadListener.java** - Hooks for @OnClassLoad, @OnClassUnload annotations
  - Allows scripts to register initialization/cleanup methods

---

## 2. LOGGING SYSTEM

### Initialization Flow
```
1. Logging.init() called early in server startup
2. Sets ClassicConstants.CONFIG_FILE_PROPERTY = "config/logback.xml"
3. Logback reads XML and initializes appenders
4. archiveLogs() zips previous run's logs
```

### Configuration (logback.xml)
**Appenders:**
1. **ConsoleAppender** (`out_console`)
   - Format: `HH:mm:ss %-5level [thread] - message`
   - Color highlighting (SLF4J %highlight)

2. **FileAppender** (`app_console`)
   - File: `log/server_console.log`
   - Format: Full ISO timestamp, level, thread, logger name, message

3. **Separate Error/Warn Files**
   - `server_errors.log` - ERROR level only
   - `server_warnings.log` - WARN level only
   - Using LevelFilter (onMatch=ACCEPT, onMismatch=DENY)

4. **Discord Webhook** (`app_status_discord`)
   - Custom DiscordChannelAppender
   - Webhook URL from property file
   - Async wrapper prevents blocking
   - Filters: Threshold >= WARN

### Log Levels
- Controlled via logback.xml root logger level
- Per-logger levels can override (e.g., specific package verbosity)

### Log Archival
```java
Logging.archiveLogs() {
  1. Create marker file: log/[server_start_marker]
  2. Find all .log files modified since last run
  3. Create timestamped zip: log/archived/2026-05-18 10.30 to 2026-05-18 11.45.zip
  4. Delete old .log files
}
```

**Pros:**
- Keeps `log/` directory clean
- Preserves history compressed
- Automatic, no manual cleanup needed

**Cons:**
- Only zips files from last run
- Depends on start-time marker file

### Discord Webhook Integration
```xml
<encoder>
  <pattern>%logger{0} [%thread]|${chatserver.log.status.discord.avatar_url}|%msg</pattern>
</encoder>
<userName_avatarUrl_msg_separator>\|</userName_avatarUrl_msg_separator>
```
- Splits message by separator: `logger|avatar_url|message`
- Posts to Discord with custom username & avatar
- Async to prevent network I/O blocking

---

## 3. SOCKET/NETWORK SERVER BASE

### Architecture Overview
```
┌─────────────────────────────────────────────────┐
│           NioServer (Main)                       │
│  ┌──────────────────────────────────────────┐   │
│  │  Dispatcher (Accept) Thread              │   │
│  │  ├─ Selector for ServerSocketChannels    │   │
│  │  └─ Calls Acceptor.accept(key)           │   │
│  └──────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────┐   │
│  │  Dispatcher[] (ReadWrite) N Threads      │   │
│  │  ├─ Selector for SocketChannels          │   │
│  │  └─ Handles OP_READ, OP_WRITE            │   │
│  └──────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────┐   │
│  │  Executor (Disconnect Callback)          │   │
│  │  └─ Runs onDisconnect() async            │   │
│  └──────────────────────────────────────────┘   │
└─────────────────────────────────────────────────┘
```

### Connection Lifecycle

**1. Acceptance Phase**
```
ServerSocketChannel (listening)
  ↓ SelectionKey.OP_ACCEPT ready
Acceptor.accept(SelectionKey key)
  ├─ serverSocketChannel.accept() → SocketChannel
  ├─ socketChannel.configureBlocking(false)
  ├─ Set TCP_NODELAY=true, SO_LINGER=10s
  ├─ factory.create(socketChannel, dispatcher) → AConnection
  └─ dispatcher.register(socketChannel, OP_READ, connection)
```

**2. I/O Phase**
```
Dispatcher loop (per read-write thread):
  while (true) {
    selector.select() // block until ready
    for (SelectionKey key : selectedKeys) {
      switch (key.readyOps()) {
        case OP_READ:
          socketChannel.read(readBuffer)
          processData(readBuffer)  // deserialize packets
        case OP_WRITE:
          writeData(writeBuffer)
          socketChannel.write(writeBuffer)
        case OP_READ | OP_WRITE:
          read() then write()
      }
    }
    processPendingClose() // close grace-period connections
  }
```

**3. Graceful Close**
```
connection.close([closePacket])
  ├─ Set pendingCloseUntilMillis = now + 2000ms
  ├─ Queue closePacket (if provided)
  ├─ Enable OP_WRITE
  └─ dispatcher.closeConnection(connection)

Dispatcher.processPendingClose():
  for each pendingClose connection:
    if (sendQueue.isEmpty() OR !connected OR timeout) {
      closeConnectionImpl(connection)
      disconnect(executor)  // async onDisconnect()
    }
```

### Connection Handler (AConnection<T>)

**Read Pipeline:**
```
Dispatcher thread calls: connection.read()
  ↓
readBuffer.remaining() bytes from socket
  ↓
processData(readBuffer) - abstract, implemented by subclasses
  ├─ Deserialize BaseClientPacket
  ├─ Validate packet structure
  └─ Queue for business logic thread pool
```

**Write Pipeline:**
```
Application thread calls: connection.sendPacket(serverPacket)
  ├─ synchronized(guard) {
  │   if (not pending close && connected) {
  │     sendQueue.add(serverPacket)
  │     enableOP_WRITE()
  │   }
  ├─ }
  ↓
Dispatcher thread calls: connection.write()
  ↓
writeData(writeBuffer) - serialize packet
  ├─ Convert BaseServerPacket to bytes
  ├─ Apply encryption if needed
  └─ Return true if more data to write
  ↓
socketChannel.write(writeBuffer)
```

### Synchronization Strategy

**Per-Connection Synchronization:**
- `guard` object - protects: send queue, close state, connection status
- `locked` flag - PacketProcessor prevents concurrent packet processing
- Method pattern: `tryLockConnection()` / `unlockConnection()`

**Thread Safety:**
- Read/write operations always on same dispatcher thread
- `sendPacket()` is thread-safe (synchronized)
- No global locks - each connection independent

### Packet Buffers

**Read Buffer:**
- Allocated once per connection in AConnection constructor
- ByteBuffer.allocate(rbSize), order = LITTLE_ENDIAN
- Reused across multiple packets

**Write Buffer:**
- Similar to read buffer
- Initially flipped (position=0, limit=0)
- Packets written, then flipped for reading by write()

---

## 4. CRYPTOGRAPHY

**Note**: Crypto is NOT in commons - it's game-server specific. Included here for completeness.

### Crypt.java (game-server/network/)

**Packet Encryption/Decryption:**

**Initialization:**
```java
public int enableKey() {
  int key = Rnd.nextInt()  // Random session key
  packetKey = new EncryptionKeyPair(key)
  return (key ^ 0xCD92E4DF) + 0x3FF2CCCF  // Obfuscated key sent to client
}
```

**Encryption (Server → Client):**
```java
public void encrypt(ByteBuffer buf) {
  if (!isEnabled) {
    isEnabled = true  // First packet (SM_KEY) is NOT encrypted
    return
  }
  packetKey.encrypt(buf)
}
```

**Decryption (Client → Server):**
```java
public boolean decrypt(ByteBuffer buf) {
  return packetKey.decrypt(buf)
}
```

### EncryptionKeyPair.java (game-server/network/)

**Key Generation:**
```java
public EncryptionKeyPair(int baseKey) {
  // Server key = 8 bytes derived from baseKey + static constants
  keys[SERVER] = [
    baseKey & 0xFF,
    (baseKey >> 8) & 0xFF,
    (baseKey >> 16) & 0xFF,
    (baseKey >> 24) & 0xFF,
    0xA1, 0x6C, 0x54, 0x87
  ]
  
  // Client key initially same as server
  keys[CLIENT] = copy of keys[SERVER]
}
```

**Static XOR Key:**
```
"nKO/WctQ0AVLbpzfBkS6NevDYT8ourG5CRlmdjyJ72aswx4EPq1UgZhFMXH?3iI9"
```

**Decryption Algorithm (Client Packets):**
```
1. Validate packet: 
   - buf.getShort(0) == ~buf.getShort(3)
   - buf.get(2) == 0x65 (static client code)

2. XOR loop:
   for i = 0 to size:
     data[i] ^= staticKey[i & 0x3F] ^ clientKey[i & 0x07] ^ previous_byte

3. Update key:
   clientKey += packet_size (as 64-bit value, byte-by-byte)
```

**Encryption Algorithm (Server Packets):**
```
Similar XOR loop, using serverKey
Updates serverKey after each packet
```

### Packet Opcode Obfuscation

**Server Packets:**
```java
int encodeServerPacketOpcode(int opcode) {
  return (opcode + SM_VERSION_CHECK.INTERNAL_VERSION) ^ 0xDF
}
```

**Client Packets:**
```java
int decodeClientPacketOpcode(int opcode) {
  return ((opcode ^ 0xEF) - 0xC ^ 0xEF) - SM_VERSION_CHECK.INTERNAL_VERSION
}
```

### Design Notes

**Strengths:**
- Simple, fast XOR-based cipher (suitable for online games)
- Per-packet key updates prevent replay attacks
- Session-based keys (unique per connection)

**Weaknesses:**
- XOR is cryptographically weak (pattern analysis possible)
- Static key embedded in code (not a secret)
- Not suitable for sensitive data (passwords, credit cards)

**Replication Level:** Must replicate exactly for client compatibility

---

## 5. THREADING & SCHEDULING

### Thread Model

**1. Dispatcher Threads (Network I/O)**
```
Accept Dispatcher:
  - 1 thread handling ServerSocketChannel.accept()
  - Selector-based loop

Read/Write Dispatchers:
  - N threads (configurable)
  - Each has own Selector for SocketChannels
  - Load-balanced via round-robin in NioServer.getReadWriteDispatcher()
```

**2. Executor/Thread Pools**
```
- Executor passed to NioServer.connect(executor)
- Used for:
  - Packet processing (ClientPacket.run())
  - Disconnect callbacks (AConnection.onDisconnect())
```

### Runnable Wrapping

**RunnableWrapper.java:**
```java
public class RunnableWrapper implements Runnable {
  Runnable runnable
  long maxRuntimeMsWithoutWarning
  boolean catchAndLogThrowables
  
  @Override
  public void run() {
    ExecuteWrapper.execute(runnable, maxRuntimeMs, catchLog)
  }
}
```

**ExecuteWrapper.java:**
```java
public static void execute(Runnable r, long maxRuntimeMs, boolean catchLog) {
  long begin = System.nanoTime()
  try {
    r.run()
    long durationMs = (System.nanoTime() - begin) / 1_000_000
    
    if (RUNNABLESTATS_ENABLE)
      RunnableStatsManager.handleStats(r.getClass(), durationNanos)
    
    if (durationMs > maxRuntimeMs)
      log.warn(r.getClass().getSimpleName() + " took " + durationMs + "ms")
  } catch (Throwable t) {
    if (catchLog)
      log.error("Exception in Runnable", t)
    else
      throw t
  }
}
```

### Performance Monitoring

**RunnableStatsManager.java:**
- Tracks per-class execution statistics
- Enabled via `CommonsConfig.RUNNABLESTATS_ENABLE`
- Aggregates:
  - Min/max/avg execution times
  - Call count
  - Total time

### Deadlock Detection

**DeadLockDetector.java:**
```java
public DeadLockDetector(Duration checkInterval, Runnable actionOnDeadlock)

@Override
public void run() {
  while (!detectDeadlock()) {
    Thread.sleep(checkInterval)
  }
  actionOnDeadlock.run()  // Usually: log + System.exit()
}

private boolean detectDeadlock() {
  long[] ids = threadMXBean.findDeadlockedThreads()
  if (ids != null) {
    // Log all thread info, locked synchronizers, stack traces
    return true
  }
  return false
}
```

**Usage Example:**
```java
new Thread(new DeadLockDetector(
  Duration.ofSeconds(10),
  () -> System.exit(1)
)).start()
```

### Thread Factory

**PriorityThreadFactory.java:**
```java
public Thread newThread(Runnable r) {
  Thread t = new Thread(threadGroup, r)
  t.setName(namePattern + "-" + atomicCounter.incrementAndGet())
  t.setPriority(priority)  // Thread.MIN/NORM/MAX_PRIORITY
  return t
}
```

Creates named thread groups for easy monitoring:
- `AcceptDispatcher-1`, `AcceptDispatcher-2`, ...
- `ReadWriteDispatcher-1`, `ReadWriteDispatcher-2`, ...

### Thread Pool Rejection Policy

**AionRejectedExecutionHandler.java:**
```java
public void rejectedExecution(Runnable r, ThreadPoolExecutor executor) {
  // Called when queue full && max threads reached
  // Custom handling (log, execute caller, drop, etc.)
}
```

### Synchronization Patterns

**1. Per-Connection (AConnection)**
```java
synchronized (guard) {
  // Protect: sendQueue, closed, pendingCloseUntilMillis
}

// Try-lock for packet processing
if (tryLockConnection()) {
  try {
    processPacket()
  } finally {
    unlockConnection()
  }
}
```

**2. Per-Dispatcher (Selector)**
```java
synchronized (gate) {
  // Synchronizes between register() and selector.select()
}
```

**3. Atomic Values**
```java
// DeadLockDetector uses AtomicReference
AtomicReference<FileTime> lastStopTime = new AtomicReference<>()
```

### Volatile Fields

```java
public class CommonsConfig {
  @Property(key = "commons.script_compiler.caching.enable")
  public static volatile boolean SCRIPT_COMPILER_CACHING
}
```
- Used for config values read from multiple threads
- Ensures visibility without explicit locking

---

## 6. XML & CONFIG BINDING

### Property Annotation System

**Goal**: Map .properties files to Java fields automatically

**Flow:**
```
1. Read .properties file(s)
2. Create Properties object
3. Call ConfigurableProcessor.process(props, Class)
4. Process walks reflection tree finding @Property annotations
5. Transform string values to target types
6. Inject into fields
```

### Example Usage

```java
// CommonsConfig.java
public class CommonsConfig {
  @Property(key = "commons.runnablestats.enable", defaultValue = "false")
  public static boolean RUNNABLESTATS_ENABLE;
  
  @Property(key = "commons.script_compiler.caching.enable")
  public static volatile boolean SCRIPT_COMPILER_CACHING;
}

// In main:
Properties props = new Properties();
props.load(new FileReader("config/main/commons.properties"));
Set<String> unused = ConfigurableProcessor.process(props, CommonsConfig.class);
if (!unused.isEmpty())
  System.out.println("Unused properties: " + unused);
```

### ConfigurableProcessor Details

**Recursive Field Processing:**
```java
static void process(Class<?> clazz, Object obj, Properties props, Set<String> unused) {
  // Process declared fields
  for (Field f : clazz.getDeclaredFields()) {
    if (hasAnnotation(f, @Property)) {
      processField(f, obj, props, unused)
    }
  }
  
  // If processing class (static fields), recurse to interfaces
  if (obj == null) {
    for (Class<?> iface : clazz.getInterfaces()) {
      process(iface, null, props, unused)
    }
  }
  
  // Always recurse to superclass
  if (clazz.getSuperclass() != null) {
    process(clazz.getSuperclass(), obj, props, unused)
  }
}
```

**Field Value Transformation:**
```java
static void processField(Field f, Object obj, Properties props, Set<String> unused) {
  Property annotation = f.getAnnotation(Property.class)
  String key = annotation.key()
  String defaultValue = annotation.defaultValue()
  
  String value = props.getProperty(key, defaultValue)
  if (value == null && defaultValue.equals(DEFAULT_VALUE))
    return  // Skip if no value found and no default
  
  unused.remove(key)
  
  // Transform value
  PropertyTransformer<?> transformer = PropertyTransformers.get(f.getType())
  Object transformed = transformer.transform(value)
  
  f.setAccessible(true)
  f.set(obj, transformed)
}
```

### Property Placeholders

```java
// In processField
String value = props.getProperty(key)
if (value.contains("${")) {
  // Resolve nested properties
  Pattern regex = \\$\\{([^}]+)\\}
  Matcher m = regex.matcher(value)
  while (m.find()) {
    String placeholderKey = m.group(1)
    String placeholderValue = props.getProperty(placeholderKey)
    value = value.replace("${" + placeholderKey + "}", placeholderValue)
  }
}
```

### Type Transformation Registry

**Builtin Transformers:**

| Type | Transformer | Example |
|------|-------------|---------|
| int, long, double | NumberTransformer | "42" → 42 |
| boolean | BooleanTransformer | "true", "1", "yes" → true |
| char | CharTransformer | "A" → 'A' |
| String | StringTransformer | Passthrough |
| Enum<E> | EnumTransformer | "RUNNING" → MyEnum.RUNNING |
| int[] | ArrayTransformer | "1,2,3" → [1,2,3] |
| List<T> | CollectionTransformer | "a,b,c" → ["a","b","c"] |
| File | FileTransformer | "/path/to/file" → File obj |
| InetSocketAddress | InetSocketAddressTransformer | "localhost:8080" → addr |
| Pattern | PatternTransformer | "\\d+" → Pattern |
| Class | ClassTransformer | "java.lang.String" → String.class |
| TimeZone | TimeZoneTransformer | "UTC" → UTC timezone |
| ZoneId | ZoneIdTransformer | "America/New_York" → ZoneId |
| Map<K,V> | MapTransformer | "key1=val1,key2=val2" → Map |
| CSV | CommaSeparatedValueTransformer | "a,b,c" → List |

**Custom Transformers:**
```java
class IPTransformer implements PropertyTransformer<InetAddress> {
  @Override
  public boolean matches(Class<?> targetType) {
    return targetType == InetAddress.class
  }
  
  @Override
  public InetAddress transform(String value) {
    return InetAddress.getByName(value)
  }
}

PropertyTransformers.register(new IPTransformer())
```

### JAXB Alternative (Not Used Here)

The commons project does NOT use JAXB (XML marshaling). Instead:
- Pure .properties file based
- Reflection + annotation processing
- No XML schema validation
- No post-unmarshal hooks (but could add similar concept)

### Validation & Error Handling

**TransformationException:**
- Thrown if transformation fails
- Contains: target type, value, cause exception

**Unused Properties Detection:**
```java
Set<String> unused = ConfigurableProcessor.process(props, MyConfig.class)
// Log warning for unrecognized keys
for (String key : unused) {
  log.warn("Unused property: " + key)
}
```

---

## KEY PATTERNS TO REPLICATE VS SIMPLIFY

### Must Replicate 1:1

| Component | Reason |
|-----------|--------|
| **Dispatcher architecture** | Client-server protocol depends on exact NIO behavior |
| **Encryption/decryption** | Clients won't connect without exact crypto match |
| **Packet framing** | Opcode parsing, buffer management must be identical |
| **Logging.init()** | Called before logger creation, affects all log output |
| **Database connection pooling** | HikariCP settings affect query performance/reliability |

### Can Simplify

| Component | Simplification |
|-----------|----------------|
| **Config transformers** | Start with just: int, bool, String. Add others later. |
| **Property @annotation system** | Could use JAXB/Jackson instead for initial port |
| **Log archival** | Simple: just delete old logs, don't zip |
| **Discord appender** | Skip in initial version, add later |
| **Scripting system** | Not needed for core server, can defer |
| **Deadlock detector** | Optional; replace with simpler monitoring |
| **Runnable stats** | Optional performance debugging tool |

### Equivalent Modern Patterns

| Java Pattern | Modern Alternative |
|---------|----------------|
| NIO Selector | Project Reactor, Netty, Vert.x |
| @Property annotation + reflection | Spring @Configuration, Guice, Dagger |
| Manual thread pools | ExecutorService, ForkJoinPool, virtual threads (Java 21+) |
| ByteBuffer read/write | ProtoBuf, FlatBuffers, Cap'n Proto |
| Custom ThreadFactory | CommonPool in ForkJoinPool |

---

## FILE STRUCTURE SUMMARY

```
commons/
├── pom.xml                                           (Maven config)
├── src/com/aionemu/commons/
│   ├── configs/
│   │   ├── CommonsConfig.java                       (Global settings)
│   │   └── DatabaseConfig.java                      (Database config)
│   ├── configuration/
│   │   ├── Property.java                            (Annotation)
│   │   ├── Properties.java                          (Alt annotation)
│   │   ├── ConfigurableProcessor.java               (Main processor - 170 lines)
│   │   ├── TransformationTypeInfo.java
│   │   ├── TransformationException.java
│   │   └── transformers/                            (13 transformer classes)
│   │       ├── PropertyTransformer.java             (Interface)
│   │       ├── PropertyTransformers.java            (Registry)
│   │       ├── NumberTransformer.java
│   │       ├── BooleanTransformer.java
│   │       ├── [... 10 more ...]
│   │       └── MapTransformer.java
│   ├── database/
│   │   ├── DB.java                                  (Static SQL facade)
│   │   ├── DatabaseFactory.java                     (HikariCP pool)
│   │   ├── ReadStH.java                             (Interface)
│   │   ├── ParamReadStH.java                        (Interface)
│   │   ├── IUStH.java                               (Interface)
│   │   ├── CallReadStH.java                         (Interface)
│   │   └── Transaction.java
│   ├── logging/
│   │   ├── Logging.java                             (Init + archival - 80 lines)
│   │   ├── DiscordChannelAppender.java              (Logback appender)
│   │   └── OnConsoleWarningStatusListener.java
│   ├── network/
│   │   ├── NioServer.java                           (Main server - 120 lines)
│   │   ├── Dispatcher.java                          (Abstract base - 90 lines)
│   │   ├── AcceptReadWriteDispatcherImpl.java        (Combined dispatcher)
│   │   ├── AcceptDispatcherImpl.java                 (Accept-only)
│   │   ├── Acceptor.java                            (Accept handler)
│   │   ├── AConnection.java                         (Base connection - 400 lines)
│   │   ├── ConnectionFactory.java                   (Interface)
│   │   ├── ServerCfg.java                           (Record)
│   │   ├── PacketProcessor.java
│   │   └── packet/
│   │       ├── BasePacket.java                      (Root class)
│   │       ├── BaseClientPacket.java                (Client packets)
│   │       └── BaseServerPacket.java                (Server packets)
│   ├── options/
│   │   └── Assertion.java                           (Compile-time flags)
│   ├── scripting/
│   │   ├── ScriptManager.java                       (Main manager)
│   │   ├── ScriptContext.java                       (Interface)
│   │   ├── ScriptCompiler.java                      (Interface)
│   │   ├── ScriptClassLoader.java
│   │   ├── CompilationResult.java
│   │   ├── impl/
│   │   │   ├── ScriptContextImpl.java
│   │   │   └── javacompiler/
│   │   │       ├── ScriptCompilerImpl.java           (Java Compiler API)
│   │   │       ├── ScriptClassLoaderImpl.java
│   │   │       ├── [... 5 more utility classes ...]
│   │   └── classlistener/
│   │       ├── ClassListener.java                   (Interface)
│   │       ├── AggregatedClassListener.java
│   │       ├── OnClassLoadUnloadListener.java
│   │       └── metadata/
│   │           ├── @OnClassLoad
│   │           └── @OnClassUnload
│   └── utils/
│       ├── Rnd.java                                 (Random)
│       ├── PropertiesUtils.java
│       ├── NetworkUtils.java
│       ├── GenericValidator.java
│       ├── ClassUtils.java
│       ├── ExitCode.java
│       ├── info/
│       │   ├── VersionInfo.java
│       │   └── SystemInfo.java
│       └── concurrent/
│           ├── PriorityThreadFactory.java
│           ├── DeadLockDetector.java                (120 lines)
│           ├── ExecuteWrapper.java                  (Executor wrapper)
│           ├── RunnableWrapper.java
│           ├── RunnableStatsManager.java
│           ├── UncaughtExceptionHandler.java
│           └── AionRejectedExecutionHandler.java
├── test/com/aionemu/commons/
│   └── [ConfigurableProcessor tests]
└── config/
    └── logback.xml                                  (Logging configuration)
```

---

## QUICK REFERENCE

### Imports to Know
```java
// Logging
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

// NIO
import java.nio.channels.*;
import java.nio.ByteBuffer;
import java.nio.ByteOrder;

// Database
import java.sql.*;
import com.zaxxer.hikari.HikariDataSource;

// Config
import com.aionemu.commons.configuration.Property;
import com.aionemu.commons.configuration.ConfigurableProcessor;

// Threading
import java.util.concurrent.Executor;
import java.util.concurrent.ThreadPoolExecutor;
```

### Common Entry Points
```java
// Initialize commons
Logging.init()
DatabaseFactory.init()
ConfigurableProcessor.process(props, MyConfig.class)

// Create NIO server
NioServer server = new NioServer(4, new ServerCfg(...))
server.connect(executor)

// Send packet
connection.sendPacket(new MyServerPacket())

// Receive packet (implement in subclass)
@Override
protected boolean processData(ByteBuffer data) { ... }
```

### Design Principles
- **Non-blocking I/O**: Selector-based, no thread-per-connection
- **Reflection-based Config**: Automatic property binding with type transformation
- **Graceful Shutdown**: Connections close only after queues drain
- **Low-allocation**: Reuse buffers, pre-sized collections
- **Observable**: Logging, stats, deadlock detection built-in
