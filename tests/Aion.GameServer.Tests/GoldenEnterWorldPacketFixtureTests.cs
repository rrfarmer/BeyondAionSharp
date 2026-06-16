using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Aion.Commons.Nio;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Stats;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

/// <summary>
/// INTEGRATION golden asserter for the deterministic EARLY ENTER-WORLD SM_* packets — the login send sequence in
/// PlayerEnterWorldService. Reads the SHARED fixtures produced by the Java harness
/// (game-server GoldenEnterWorldPacketFixtureGeneratorTest -> parity-artifacts/golden/packets/*.json) and asserts the
/// C# writers emit byte-for-byte identical payloads. Java is the oracle.
///
/// Each packet here reads only fixed scalars, an empty player-owned collection (titles/friends/blocks/motions/
/// emotions/quests), the active player's objectId, or a constant — nothing live (World/Knownlist/Legion/wall-clock).
/// Inputs are rebuilt structurally identically to the Java generator (same minimal Player + connection seam).
/// </summary>
[Xunit.Collection("GoldenDataManager")]
public sealed class GoldenEnterWorldPacketFixtureTests
{
    static GoldenEnterWorldPacketFixtureTests()
    {
        CustomConfig.BASE_FLYTIME = 60;
        EnsureDataManagerBridge();
        EnsureGameTimeSingleton();
        Aion.GameServer.Configs.Administration.AdminConfig.NAME_TAGS = new[] { "%s" };
    }

    [Theory]
    [InlineData("SM_AFTER_TIME_CHECK_4_7_5.json")]
    [InlineData("SM_ENTER_WORLD_CHECK.json")]
    [InlineData("SM_TITLE_INFO.json")]
    [InlineData("SM_EMOTION_LIST.json")]
    [InlineData("SM_MOTION.json")]
    [InlineData("SM_QUEST_LIST.json")]
    [InlineData("SM_CHANNEL_INFO.json")]
    [InlineData("SM_UI_SETTINGS.json")]
    [InlineData("SM_UNK_3_5_1.json")]
    [InlineData("SM_FRIEND_LIST.json")]
    [InlineData("SM_BLOCK_LIST.json")]
    public void CsharpEnterWorldPacketMatchesJavaGoldenFixture(string fixtureFile)
    {
        using var fixture = LoadFixture(fixtureFile);
        var packetName = fixture.RootElement.GetProperty("packet").GetString()!;

        foreach (var caseElement in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var caseName = caseElement.GetProperty("name").GetString()!;
            var expectedHex = caseElement.GetProperty("payloadHex").GetString()!;
            var inputs = caseElement.GetProperty("inputs");

            var (packet, con) = Reconstruct(packetName, inputs);
            var actual = CaptureWriteImplPayload(packet, con);
            var actualHex = Convert.ToHexString(actual);

            Assert.True(expectedHex == actualHex,
                $"{packetName}/{caseName}: C# payload diverged from Java golden.\n" +
                $"  Java : {expectedHex}\n  C#   : {actualHex}\n" +
                $"  firstDiffByte: {FirstDiffByte(expectedHex, actualHex)}");
        }
    }

    private static (AionServerPacket packet, AionConnection? con) Reconstruct(string packetName, JsonElement inputs)
    {
        switch (packetName)
        {
            case "SM_AFTER_TIME_CHECK_4_7_5":
                return (new SM_AFTER_TIME_CHECK_4_7_5(), null);
            case "SM_ENTER_WORLD_CHECK":
            {
                var msg = Enum.Parse<SM_ENTER_WORLD_CHECK.Msg>(inputs.GetProperty("msg").GetString()!);
                return (new SM_ENTER_WORLD_CHECK(msg), null);
            }
            case "SM_TITLE_INFO":
                return (ReconstructTitleInfo(inputs), null);
            case "SM_EMOTION_LIST":
                return (new SM_EMOTION_LIST((byte)inputs.GetProperty("action").GetInt32(),
                    new List<Aion.GameServer.Model.GameObjects.Players.Emotion.Emotion>()), null);
            case "SM_MOTION":
                return (new SM_MOTION(new List<Aion.GameServer.Model.GameObjects.Players.Motion.Motion>()), null);
            case "SM_QUEST_LIST":
                return (new SM_QUEST_LIST(new List<Aion.GameServer.QuestEngine.Model.QuestState>()), null);
            case "SM_CHANNEL_INFO":
                return (new SM_CHANNEL_INFO(null!), null);
            case "SM_UI_SETTINGS":
            {
                var data = inputs.GetProperty("data").EnumerateArray().Select(e => (byte)e.GetInt32()).ToArray();
                return (new SM_UI_SETTINGS(data, inputs.GetProperty("type").GetInt32()), null);
            }
            case "SM_UNK_3_5_1":
            {
                var active = NewPlayer(inputs.GetProperty("objectId").GetInt32(), Race.ELYOS);
                return (new SM_UNK_3_5_1(), NewConnectionWithActivePlayer(active));
            }
            case "SM_FRIEND_LIST":
            {
                var active = NewPlayer(780003, Race.ELYOS);
                active.SetFriendList(new FriendList(active, new List<Friend>()));
                return (new SM_FRIEND_LIST(), NewConnectionWithActivePlayer(active));
            }
            case "SM_BLOCK_LIST":
            {
                var active = NewPlayer(780004, Race.ELYOS);
                active.SetBlockList(new BlockList());
                return (new SM_BLOCK_LIST(), NewConnectionWithActivePlayer(active));
            }
            default:
                throw new NotSupportedException($"No C# reconstruction registered for {packetName}");
        }
    }

    private static SM_TITLE_INFO ReconstructTitleInfo(JsonElement inputs)
    {
        int action = inputs.GetProperty("action").GetInt32();
        switch (action)
        {
            case 0:
                return new SM_TITLE_INFO(NewPlayer(770001, Race.ELYOS));
            case 1:
                return new SM_TITLE_INFO(inputs.GetProperty("titleId").GetInt32());
            case 3:
            {
                var p = NewPlayer(inputs.GetProperty("playerObjId").GetInt32(), Race.ELYOS);
                return new SM_TITLE_INFO(p, inputs.GetProperty("titleId").GetInt32());
            }
            case 6:
                return new SM_TITLE_INFO(6, inputs.GetProperty("bonusTitleId").GetInt32());
            default:
                throw new NotSupportedException($"No SM_TITLE_INFO reconstruction for action {action}");
        }
    }

    // ---- minimal player (faithful base ctor builds an empty title list etc.) ----

    private static Player NewPlayer(int objectId, Race race)
    {
        var common = new PlayerCommonData(objectId);
        common.SetPlayerClass(PlayerClass.WARRIOR);
        common.SetRace(race);
        common.SetGender(Gender.MALE);
        common.SetName("Harness" + objectId);
        common.SetNote("");
        var accountData = new PlayerAccountData(common, new PlayerAppearance());
        return new Player(accountData, new Account(1));
    }

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

    // ---- integration seam (mirrors GoldenPlayerInfoFixtureTests) ----

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

    private static void EnsureDataManagerBridge()
    {
        bool hasExpTable = false;
        try { hasExpTable = DataManager.PLAYER_EXPERIENCE_TABLE is not null; }
        catch (InvalidOperationException) { }
        catch (NullReferenceException) { }
        if (hasExpTable)
            return;

        var expTable = new long[67];
        for (long i = 0; i < expTable.Length; i++)
            expTable[i] = 100L * i * i * i + 1000L * i;

        var staticData = (StaticData)RuntimeHelpers.GetUninitializedObject(typeof(StaticData));
        SetAutoProperty(staticData, nameof(StaticData.AbsoluteStatsDataDh), new AbsoluteStatsData());
        SetAutoProperty(staticData, nameof(StaticData.PlayerExperienceTable), new PlayerExperienceTable(expTable));

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

    // ---- capture ----

    private static byte[] CaptureWriteImplPayload(AionServerPacket packet, AionConnection? con)
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
            "Regenerate with: mvn -pl game-server -am test -Dtest=GoldenEnterWorldPacketFixtureGeneratorTest " +
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
}
