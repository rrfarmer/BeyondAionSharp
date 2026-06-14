using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Aion.Commons.Nio;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Templates.Stats;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Tests;

/// <summary>
/// INTEGRATION golden asserter for SM_STATS_INFO — the full-Player enter-world stat sheet.
///
/// Reads the SHARED fixture produced by the Java harness
/// (game-server GoldenStatsInfoFixtureGeneratorTest -> parity-artifacts/golden/packets/SM_STATS_INFO.json)
/// and asserts the C# SM_STATS_INFO writer emits byte-for-byte identical payloads. Java is the oracle.
///
/// The integration seam mirrors the Java generator exactly:
///  - GameTime: a DI GameTimeService constructed with the default 0 game-minutes (registers as the singleton),
///    so GameTimeService.GetInstance().GetGameTime().GetTime() == 0 (Java's stub resolves time 0 too).
///  - PLAYER_EXPERIENCE_TABLE: an identical minimal exp table injected via the DataManager test bridge.
///  - Stats: HarnessStats fixed-map (current == base per stat).
///  - exp/level/dp/repose/salvation pinned via reflection on PlayerCommonData (identical lowercase fields both sides).
///  - currentHp/currentMp/currentFp pinned via reflection on the life-stats.
/// </summary>
public sealed class GoldenStatsInfoFixtureTests
{
    // exp[i] = 100*i^3 + 1000*i — identical to the Java generator's buildExpTable() (67 entries).
    private static readonly long[] ExpTable = BuildExpTable();

    private static long[] BuildExpTable()
    {
        var t = new long[67];
        for (long i = 0; i < t.Length; i++)
            t[i] = 100L * i * i * i + 1000L * i;
        return t;
    }

    static GoldenStatsInfoFixtureTests()
    {
        CustomConfig.BASE_FLYTIME = 60; // match the Java @Property default the generator pins
        EnsureDataManagerBridgeWithExpTable();
        EnsureGameTimeSingleton();
    }

    [Fact]
    public void CsharpStatsInfoMatchesJavaGoldenFixture()
    {
        using var fixture = LoadFixture("SM_STATS_INFO.json");
        var packetName = fixture.RootElement.GetProperty("packet").GetString()!;

        foreach (var caseElement in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var caseName = caseElement.GetProperty("name").GetString()!;
            var expectedHex = caseElement.GetProperty("payloadHex").GetString()!;
            var inputs = caseElement.GetProperty("inputs");

            var player = BuildPlayer(inputs);
            var packet = new SM_STATS_INFO(player);
            var actual = CaptureWriteImplPayload(packet);
            var actualHex = Convert.ToHexString(actual);

            Assert.True(expectedHex == actualHex,
                $"{packetName}/{caseName}: C# payload diverged from Java golden.\n" +
                $"  Java : {expectedHex}\n  C#   : {actualHex}\n" +
                $"  firstDiffByte: {FirstDiffByte(expectedHex, actualHex)}");
        }
    }

    private static HarnessPlayer BuildPlayer(JsonElement spec)
    {
        int objectId = spec.GetProperty("objectId").GetInt32();
        sbyte level = (sbyte)spec.GetProperty("level").GetInt32();
        Race race = Enum.Parse<Race>(spec.GetProperty("race").GetString()!);
        PlayerClass playerClass = Enum.Parse<PlayerClass>(spec.GetProperty("playerClass").GetString()!);

        var statMap = new Dictionary<StatEnum, int>();
        foreach (var prop in spec.GetProperty("stats").EnumerateObject())
            statMap[Enum.Parse<StatEnum>(prop.Name)] = prop.Value.GetInt32();

        var p = new HarnessPlayer(objectId, level, race, playerClass, statMap,
            spec.GetProperty("currentHp").GetInt32(),
            spec.GetProperty("currentMp").GetInt32(),
            spec.GetProperty("currentFp").GetInt32());

        PinCommonData(p.GetCommonData(),
            spec.GetProperty("exp").GetInt64(),
            spec.GetProperty("expRecoverable").GetInt64(),
            spec.GetProperty("dp").GetInt32(),
            spec.GetProperty("reposeCurrent").GetInt64(),
            spec.GetProperty("reposeMax").GetInt64(),
            spec.GetProperty("salvationPoint").GetInt64(),
            level);
        return p;
    }

    // ---- integration seam ---------------------------------------------------------------------------------------

    private static void EnsureGameTimeSingleton()
    {
        try { _ = GameTimeService.GetInstance(); }
        catch (InvalidOperationException)
        {
            // Construct a DI GameTimeService; the ctor registers it as the singleton with 0 game-minutes.
            var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<GameTimeService>.Instance;
            var tpm = (Aion.GameServer.Utils.ThreadPoolManager)RuntimeHelpers.GetUninitializedObject(
                typeof(Aion.GameServer.Utils.ThreadPoolManager));
            _ = new GameTimeService(logger, tpm); // ctor sets _instance; _gameMinutes defaults to 0 -> time 0
        }
    }

    // The faithful Player ctor reads DataManager.ABSOLUTE_STATS_DATA; SM_STATS_INFO additionally reads
    // DataManager.PLAYER_EXPERIENCE_TABLE. Register a minimal StaticData exposing BOTH (uninitialized StaticData with
    // only the two backing properties set). Always (re)register so a prior bridge lacking the exp table is replaced.
    private static void EnsureDataManagerBridgeWithExpTable()
    {
        bool hasExpTable = false;
        try { hasExpTable = DataManager.PLAYER_EXPERIENCE_TABLE is not null; }
        catch (InvalidOperationException) { /* not registered */ }
        catch (NullReferenceException) { /* registered but exp table null */ }
        if (hasExpTable)
            return;

        var staticData = (StaticData)RuntimeHelpers.GetUninitializedObject(typeof(StaticData));
        SetAutoProperty(staticData, nameof(StaticData.AbsoluteStatsDataDh), new AbsoluteStatsData());
        SetAutoProperty(staticData, nameof(StaticData.PlayerExperienceTable),
            new PlayerExperienceTable(ExpTable));

        var dmCtor = typeof(DataManager).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic, binder: null, new[] { typeof(StaticData) }, modifiers: null)!;
        var dm = (DataManager)dmCtor.Invoke(new object[] { staticData });
        DataManager.RegisterInstance(dm);
    }

    private static void SetAutoProperty(object target, string propertyName, object value)
    {
        var field = target.GetType().GetField($"<{propertyName}>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(target.GetType().FullName, propertyName);
        field.SetValue(target, value);
    }

    // ---- field pinning ------------------------------------------------------------------------------------------

    private static void PinCommonData(PlayerCommonData pcd, long exp, long expRecoverable, int dp,
        long reposeCurrent, long reposeMax, long salvationPoint, int level)
    {
        SetField(typeof(PlayerCommonData), pcd, "exp", exp);
        SetField(typeof(PlayerCommonData), pcd, "expRecoverable", expRecoverable);
        SetField(typeof(PlayerCommonData), pcd, "dp", dp);
        SetField(typeof(PlayerCommonData), pcd, "reposeCurrent", reposeCurrent);
        SetField(typeof(PlayerCommonData), pcd, "reposeMax", reposeMax);
        SetField(typeof(PlayerCommonData), pcd, "salvationPoint", salvationPoint);
        SetField(typeof(PlayerCommonData), pcd, "level", level);
    }

    private static void SetField(Type type, object target, string name, object value)
    {
        var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(type.FullName, name);
        field.SetValue(target, value);
    }

    // ---- capture ------------------------------------------------------------------------------------------------

    private static byte[] CaptureWriteImplPayload(AionServerPacket packet)
    {
        var buffer = ByteBuffer.Allocate(8192).Order(ByteOrder.LITTLE_ENDIAN);
        packet.SetBuf(buffer);
        var writeImpl = typeof(AionServerPacket).GetMethod("WriteImpl",
            BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(AionConnection) })!;
        writeImpl.Invoke(packet, new object?[] { null });
        var length = buffer.Position();
        var payload = new byte[length];
        buffer.Flip();
        buffer.Get(payload);
        return payload;
    }

    private static string FirstDiffByte(string a, string b)
    {
        int n = Math.Min(a.Length, b.Length);
        for (int i = 0; i + 1 < n; i += 2)
            if (a[i] != b[i] || a[i + 1] != b[i + 1])
                return $"byte#{i / 2} java={a.Substring(i, 2)} csharp={b.Substring(i, 2)}";
        return a.Length == b.Length ? "none (equal prefix)" : $"length java={a.Length / 2} csharp={b.Length / 2}";
    }

    private static JsonDocument LoadFixture(string fileName)
    {
        var path = Path.Combine(FixtureRoot(), fileName);
        Assert.True(File.Exists(path), $"Missing Java golden fixture: {path}. " +
            "Regenerate with: mvn -pl game-server -am test -Dtest=GoldenStatsInfoFixtureGeneratorTest " +
            "-Dmaven.test.skip=false -Dsurefire.failIfNoSpecifiedTests=false");
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static string FixtureRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "parity-artifacts", "golden", "packets");
            if (Directory.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException(
            "Could not locate parity-artifacts/golden/packets above " + AppContext.BaseDirectory);
    }

    // ---- harness types (mirror GoldenStatsInfoFixtureGeneratorTest) ---------------------------------------------

    internal sealed class HarnessPlayer : Player
    {
        private sbyte _level;
        private Race _race;

        public HarnessPlayer(int objectId, sbyte level, Race race, PlayerClass playerClass,
            Dictionary<StatEnum, int> statMap, int currentHp, int currentMp, int currentFp)
            : base(MinimalAccountData(objectId, playerClass), new Account(1))
        {
            _level = level;
            _race = race;
            SetGameStats(new HarnessStats(this, statMap));
            SetLifeStats(new HarnessLifeStats(this, currentHp, currentMp, currentFp));
            moveController = new PlayerMoveController(this); // movementMask defaults to 0
        }

        public override sbyte GetLevel() => _level;

        public override Race GetRace() => _race;

        private static PlayerAccountData MinimalAccountData(int objectId, PlayerClass playerClass)
        {
            var common = new PlayerCommonData(objectId);
            common.SetPlayerClass(playerClass);
            return new PlayerAccountData(common, new PlayerAppearance());
        }
    }

    internal sealed class HarnessStats : PlayerGameStats
    {
        private readonly Dictionary<StatEnum, int> _statMap;

        public HarnessStats(Player owner, Dictionary<StatEnum, int> statMap) : base(owner)
        {
            _statMap = statMap;
        }

        public override Stat2 GetStat(StatEnum statEnum, float baseValue, params CalculationType[] calculationTypes)
        {
            float resolved = _statMap.TryGetValue(statEnum, out int v) ? v : baseValue;
            return new AdditionStat(statEnum, resolved, owner);
        }

        public override StatsTemplate GetStatsTemplate() => new StatsTemplate();
        public override Stat2 GetAttackSpeed() => new AdditionStat(StatEnum.ATTACK_SPEED, 1000, owner);
        public override Stat2 GetMovementSpeed() => new AdditionStat(StatEnum.SPEED, 6000, owner);
        public override Stat2 GetAttackRange() => new AdditionStat(StatEnum.ATTACK_RANGE, 1500, owner);
        public override Stat2 GetHpRegenRate() => new AdditionStat(StatEnum.REGEN_HP, 1, owner);
        public override Stat2 GetMpRegenRate() => new AdditionStat(StatEnum.REGEN_MP, 1, owner);
    }

    // Fixed currentHp/currentMp/currentFp pinned on the backing fields (GetCurrentHp/Mp non-virtual; GetCurrentFp
    // virtual but we pin the field anyway so all three are deterministic regardless of the maxHp/maxMp/flyTime ctor seed).
    internal sealed class HarnessLifeStats : PlayerLifeStats
    {
        private static readonly FieldInfo CurrentHpField =
            typeof(CreatureLifeStats).GetField("currentHp", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private static readonly FieldInfo CurrentMpField =
            typeof(CreatureLifeStats).GetField("currentMp", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private static readonly FieldInfo CurrentFpField =
            typeof(PlayerLifeStats).GetField("currentFp", BindingFlags.Instance | BindingFlags.NonPublic)!;

        public HarnessLifeStats(Player owner, int currentHp, int currentMp, int currentFp) : base(owner)
        {
            CurrentHpField.SetValue(this, currentHp);
            CurrentMpField.SetValue(this, currentMp);
            CurrentFpField.SetValue(this, currentFp);
        }
    }
}
