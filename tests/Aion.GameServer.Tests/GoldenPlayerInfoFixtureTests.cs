using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Aion.Commons.Nio;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Model.Templates.Stats;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Tests;

/// <summary>
/// INTEGRATION golden asserter for SM_PLAYER_INFO — the enter-world visible-player definition packet (the single most
/// important and most complex player serialization).
///
/// Reads the SHARED fixture produced by the Java harness
/// (game-server GoldenPlayerInfoFixtureGeneratorTest -> parity-artifacts/golden/packets/SM_PLAYER_INFO.json)
/// and asserts the C# SM_PLAYER_INFO writer emits byte-for-byte identical payloads. Java is the oracle.
///
/// Extends the reusable SM_STATS_INFO integration seam (real faithful Player base ctor + DataManager bridge +
/// GameTime singleton) with the extra deterministic stubs SM_PLAYER_INFO needs, all built IDENTICALLY to the Java side:
///  - active player: an uninitialized AionConnection whose activePlayer field is a second same-race HarnessPlayer.
///  - isEnemy: HarnessPlayer overrides IsEnemy(Creature) -> false (raceId is the player's own race id).
///  - position: a fixed WorldPosition (mapId/x/y/z/heading); mapId is not a conqueror/protector world -> CP ranks 0.
///  - objectTemplate: HarnessPlayer.GetObjectTemplate returns a fixed stub (templateId == pcd.GetTemplateId()).
///  - abyssRank: a fixed AbyssRank pinned to GRADE9_SOLDIER (id 1) without its ctor (avoids ServerTime).
///  - playerSettings: default new PlayerSettings() (display 0, deny 0); houses: empty (GetActiveHouse == null);
///    pcd name/note/race/gender pinned; legion/store/target/flight/casting/mentor absent -> default branches.
/// </summary>
public sealed class GoldenPlayerInfoFixtureTests
{
    private static readonly long[] ExpTable = BuildExpTable();

    private static long[] BuildExpTable()
    {
        var t = new long[67];
        for (long i = 0; i < t.Length; i++)
            t[i] = 100L * i * i * i + 1000L * i;
        return t;
    }

    static GoldenPlayerInfoFixtureTests()
    {
        CustomConfig.BASE_FLYTIME = 60;
        EnsureDataManagerBridgeWithExpTable();
        EnsureGameTimeSingleton();
        // getName(true) reads AdminConfig.NAME_TAGS.Length; access level 0 -> index -1 out of range -> pcd name.
        // Both sides only require a non-empty array here; pin a fixed one identical to the Java generator.
        Aion.GameServer.Configs.Administration.AdminConfig.NAME_TAGS = new[] { "%s" };
    }

    [Fact]
    public void CsharpPlayerInfoMatchesJavaGoldenFixture()
    {
        using var fixture = LoadFixture("SM_PLAYER_INFO.json");
        var packetName = fixture.RootElement.GetProperty("packet").GetString()!;

        foreach (var caseElement in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var caseName = caseElement.GetProperty("name").GetString()!;
            var expectedHex = caseElement.GetProperty("payloadHex").GetString()!;
            var inputs = caseElement.GetProperty("inputs");

            var player = BuildPlayer(inputs);
            var activePlayer = BuildActivePlayer(inputs);
            var con = NewConnectionWithActivePlayer(activePlayer);

            var packet = new SM_PLAYER_INFO(player, false);
            var actual = CaptureWriteImplPayload(packet, con);
            var actualHex = Convert.ToHexString(actual);

            Assert.True(expectedHex == actualHex,
                $"{packetName}/{caseName}: C# payload diverged from Java golden.\n" +
                $"  Java : {expectedHex}\n  C#   : {actualHex}\n" +
                $"  firstDiffByte: {FirstDiffByte(expectedHex, actualHex)}");
        }
    }

    /// <summary>
    /// Player-only SCALAR SM_* packets whose writeImpl reads ONLY deterministic fixed HarnessPlayer fields
    /// (objectId, pinned robotId, null target -> 0, empty inventory -> no ticket). Mirrors the Java
    /// GoldenPlayerInfoFixtureGeneratorTest.generateGoldenPlayerScalarPacketFixtures. Java is the oracle.
    /// The fixed player fields are IDENTICAL to the Java scalarSpec() (objId 100777, level 10, Elyos warrior).
    /// </summary>
    [Theory]
    [InlineData("SM_PLAYER_STANCE.json")]
    [InlineData("SM_RIDE_ROBOT.json")]
    [InlineData("SM_PLASTIC_SURGERY.json")]
    [InlineData("SM_TARGET_UPDATE.json")]
    public void CsharpPlayerScalarPacketMatchesJavaGoldenFixture(string fixtureFile)
    {
        using var fixture = LoadFixture(fixtureFile);
        var packetName = fixture.RootElement.GetProperty("packet").GetString()!;

        foreach (var caseElement in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var caseName = caseElement.GetProperty("name").GetString()!;
            var expectedHex = caseElement.GetProperty("payloadHex").GetString()!;
            var inputs = caseElement.GetProperty("inputs");

            var player = BuildScalarPlayer();
            var packet = ReconstructScalar(packetName, player, inputs);
            // These scalar packet writeImpls never read con.
            var actual = CaptureWriteImplPayload(packet, null!);
            var actualHex = Convert.ToHexString(actual);

            Assert.True(expectedHex == actualHex,
                $"{packetName}/{caseName}: C# payload diverged from Java golden.\n" +
                $"  Java : {expectedHex}\n  C#   : {actualHex}\n" +
                $"  firstDiffByte: {FirstDiffByte(expectedHex, actualHex)}");
        }
    }

    /// <summary>The single deterministic Elyos warrior IDENTICAL to the Java scalarSpec() (objId 100777).</summary>
    private static HarnessPlayer BuildScalarPlayer()
    {
        var statMap = new Dictionary<StatEnum, int>
        {
            [StatEnum.MAXHP] = 1200,
            [StatEnum.MAXMP] = 800,
            [StatEnum.FLY_TIME] = 60,
            [StatEnum.MAXDP] = 4000,
        };
        var p = new HarnessPlayer(100777, 10, Race.ELYOS, Gender.MALE, PlayerClass.WARRIOR,
            "Scalarharness", "", statMap, 1200, 800, 60, 220020000, 100.5f, 200.25f, 300.75f, 30);
        PinCommonData(p.GetCommonData(), 0, 10);
        return p;
    }

    private static AionServerPacket ReconstructScalar(string packetName, HarnessPlayer player, JsonElement inputs) => packetName switch
    {
        "SM_PLAYER_STANCE" => new SM_PLAYER_STANCE(player, inputs.GetProperty("state").GetInt32()),
        "SM_RIDE_ROBOT" => new SM_RIDE_ROBOT(player, inputs.GetProperty("robotId").GetInt32()),
        "SM_PLASTIC_SURGERY" => new SM_PLASTIC_SURGERY(player, inputs.GetProperty("isGenderSwitch").GetBoolean()),
        "SM_TARGET_UPDATE" => new SM_TARGET_UPDATE(player),
        _ => throw new NotSupportedException($"No scalar Player reconstruction for {packetName}"),
    };

    private static HarnessPlayer BuildPlayer(JsonElement spec)
    {
        int objectId = spec.GetProperty("objectId").GetInt32();
        sbyte level = (sbyte)spec.GetProperty("level").GetInt32();
        Race race = Enum.Parse<Race>(spec.GetProperty("race").GetString()!);
        Gender gender = Enum.Parse<Gender>(spec.GetProperty("gender").GetString()!);
        PlayerClass playerClass = Enum.Parse<PlayerClass>(spec.GetProperty("playerClass").GetString()!);

        var statMap = new Dictionary<StatEnum, int>();
        foreach (var prop in spec.GetProperty("stats").EnumerateObject())
            statMap[Enum.Parse<StatEnum>(prop.Name)] = prop.Value.GetInt32();

        var p = new HarnessPlayer(objectId, level, race, gender, playerClass,
            spec.GetProperty("name").GetString()!, spec.GetProperty("note").GetString()!, statMap,
            spec.GetProperty("currentHp").GetInt32(),
            spec.GetProperty("currentMp").GetInt32(),
            spec.GetProperty("currentFp").GetInt32(),
            spec.GetProperty("mapId").GetInt32(),
            spec.GetProperty("x").GetSingle(),
            spec.GetProperty("y").GetSingle(),
            spec.GetProperty("z").GetSingle(),
            (byte)spec.GetProperty("heading").GetInt32());

        PinCommonData(p.GetCommonData(), spec.GetProperty("dp").GetInt32(), level);
        return p;
    }

    /// <summary>A deterministic same-race active player (distinct object id) used only as con.GetActivePlayer().</summary>
    private static HarnessPlayer BuildActivePlayer(JsonElement spec)
    {
        int objectId = spec.GetProperty("objectId").GetInt32() + 1;
        Race race = Enum.Parse<Race>(spec.GetProperty("race").GetString()!);
        int mapId = spec.GetProperty("mapId").GetInt32();

        var statMap = new Dictionary<StatEnum, int>
        {
            [StatEnum.MAXHP] = 100,
            [StatEnum.MAXMP] = 100,
            [StatEnum.FLY_TIME] = 60,
            [StatEnum.MAXDP] = 4000,
        };
        var p = new HarnessPlayer(objectId, 1, race, Gender.MALE, PlayerClass.WARRIOR,
            "ActiveViewer", "", statMap, 100, 100, 60, mapId, 0f, 0f, 0f, 0);
        PinCommonData(p.GetCommonData(), 0, 1);
        return p;
    }

    // ---- integration seam ---------------------------------------------------------------------------------------

    /// <summary>Allocate an uninitialized AionConnection (no socket) and pin its activePlayer field to the player.</summary>
    private static AionConnection NewConnectionWithActivePlayer(Player activePlayer)
    {
        var con = (AionConnection)RuntimeHelpers.GetUninitializedObject(typeof(AionConnection));
        var field = typeof(AionConnection).GetField("activePlayer",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(nameof(AionConnection), "activePlayer");
        field.SetValue(con, activePlayer);
        return con;
    }

    private static void EnsureGameTimeSingleton()
    {
        try { _ = GameTimeService.GetInstance(); }
        catch (InvalidOperationException)
        {
            var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<GameTimeService>.Instance;
            var tpm = (Aion.GameServer.Utils.ThreadPoolManager)RuntimeHelpers.GetUninitializedObject(
                typeof(Aion.GameServer.Utils.ThreadPoolManager));
            _ = new GameTimeService(logger, tpm);
        }
    }

    private static void EnsureDataManagerBridgeWithExpTable()
    {
        bool hasExpTable = false;
        try { hasExpTable = DataManager.PLAYER_EXPERIENCE_TABLE is not null; }
        catch (InvalidOperationException) { }
        catch (NullReferenceException) { }
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

    private static void PinCommonData(PlayerCommonData pcd, int dp, int level)
    {
        SetField(typeof(PlayerCommonData), pcd, "dp", dp);
        SetField(typeof(PlayerCommonData), pcd, "level", level);
    }

    private static void SetField(Type type, object target, string name, object value)
    {
        var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(type.FullName, name);
        field.SetValue(target, value);
    }

    /// <summary>
    /// Build an AbyssRank pinned to GRADE9_SOLDIER (id 1) WITHOUT running its ctor, whose DoUpdate() depends on the
    /// server time zone. The packet only reads GetRank().GetId(). Mirrors the Java harness's Unsafe-allocated AbyssRank.
    /// </summary>
    private static AbyssRank FixedAbyssRank()
    {
        var rank = (AbyssRank)RuntimeHelpers.GetUninitializedObject(typeof(AbyssRank));
        var rankField = typeof(AbyssRank).GetField("rank", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(nameof(AbyssRank), "rank");
        rankField.SetValue(rank, AbyssRankEnum.GRADE9_SOLDIER);
        return rank;
    }

    // ---- capture ------------------------------------------------------------------------------------------------

    private static byte[] CaptureWriteImplPayload(AionServerPacket packet, AionConnection con)
    {
        var buffer = ByteBuffer.Allocate(8192).Order(ByteOrder.LITTLE_ENDIAN);
        packet.SetBuf(buffer);
        var writeImpl = typeof(AionServerPacket).GetMethod("WriteImpl",
            BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(AionConnection) })!;
        writeImpl.Invoke(packet, new object?[] { con });
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
            "Regenerate with: mvn -pl game-server -am test -Dtest=GoldenPlayerInfoFixtureGeneratorTest " +
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

    // ---- harness types (mirror GoldenPlayerInfoFixtureGeneratorTest) --------------------------------------------

    internal sealed class HarnessPlayer : Player
    {
        private readonly sbyte _level;
        private readonly Race _race;
        private readonly VisibleObjectTemplate _stubTemplate;

        public HarnessPlayer(int objectId, sbyte level, Race race, Gender gender, PlayerClass playerClass,
            string name, string note, Dictionary<StatEnum, int> statMap, int currentHp, int currentMp, int currentFp,
            int mapId, float x, float y, float z, byte heading)
            : base(MinimalAccountData(objectId, gender, race, playerClass, name, note), new Account(1))
        {
            _level = level;
            _race = race;
            _stubTemplate = new StubTemplate(GetCommonData().GetTemplateId());
            SetGameStats(new HarnessStats(this, statMap));
            SetLifeStats(new HarnessLifeStats(this, currentHp, currentMp, currentFp));
            moveController = new PlayerMoveController(this); // movementMask defaults to 0
            SetPosition(new Aion.GameServer.World.WorldPosition(mapId, x, y, z, heading));
            SetAbyssRank(FixedAbyssRank()); // rank id 1 = GRADE9_SOLDIER (no DoUpdate -> no ServerTime dependency)
            SetPlayerSettings(new Aion.GameServer.Model.GameObjects.Players.PlayerSettings()); // display 0, deny 0
            PinEmptyHouses(this);
        }

        public override sbyte GetLevel() => _level;

        public override Race GetRace() => _race;

        public override VisibleObjectTemplate GetObjectTemplate() => _stubTemplate;

        // Deterministic relation: same-race viewer is never an enemy; avoids World/Knownlist/pvp-zone evaluation.
        public override bool IsEnemy(Creature creature) => false;

        private static PlayerAccountData MinimalAccountData(int objectId, Gender gender, Race race,
            PlayerClass playerClass, string name, string note)
        {
            var common = new PlayerCommonData(objectId);
            common.SetPlayerClass(playerClass);
            common.SetRace(race);
            common.SetGender(gender);
            common.SetName(name);
            common.SetNote(note);
            return new PlayerAccountData(common, new PlayerAppearance());
        }
    }

    /// <summary>Minimal fixed object template so transformModel.GetModelId() resolves a deterministic templateId.</summary>
    internal sealed class StubTemplate : VisibleObjectTemplate
    {
        private readonly int _templateId;

        public StubTemplate(int templateId) => _templateId = templateId;

        public override int GetTemplateId() => _templateId;
        public override string GetName() => "Harness";
        public override int GetL10nId() => 0;
    }

    private static void PinEmptyHouses(Player player)
    {
        var field = typeof(Player).GetField("houses", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(nameof(Player), "houses");
        field.SetValue(player, new List<Aion.GameServer.Model.House.House>());
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

    /// <summary>
    /// Mirrors the Java harness life stats. SM_PLAYER_INFO reads only getHpPercentage(), which in CreatureLifeStats
    /// reads the BASE currentHp field (not the getCurrentHp() override). The faithful PlayerLifeStats(owner) ctor sets
    /// that base field to gameStats.getMaxHp().getCurrent() (== the HarnessStats MAXHP), so the base field equals maxHp
    /// on both sides and hpPercentage is a deterministic 100 — exactly as the Java generator captures it. We therefore
    /// leave the base fields at their ctor values (do not pin spec currentHp/Mp/Fp; the packet never reads them).
    /// </summary>
    internal sealed class HarnessLifeStats : Aion.GameServer.Model.Stats.Container.PlayerLifeStats
    {
        public HarnessLifeStats(Player owner, int currentHp, int currentMp, int currentFp) : base(owner)
        {
        }
    }
}
