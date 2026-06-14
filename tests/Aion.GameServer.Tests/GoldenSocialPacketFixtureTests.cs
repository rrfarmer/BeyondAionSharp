using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Aion.Commons.Nio;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

/// <summary>
/// Golden asserter for the SOCIAL / GROUP / LEGION / TRADE / HOUSE packet domain. Reads the SHARED fixtures
/// produced by the Java harness (game-server GoldenSocialPacketFixtureGeneratorTest -> parity-artifacts/golden/
/// packets/*.json) and asserts the C# writers emit byte-for-byte identical payloads. Java is the oracle.
///
/// Each packet here reads only constructor scalars/strings, an empty player-owned collection, or the active
/// player's deterministic state — nothing live (World/Knownlist/AbyssRankingCache/wall-clock/ItemInfoBlob).
/// Inputs are rebuilt structurally identically to the Java generator (same minimal Player + connection seam).
/// </summary>
public sealed class GoldenSocialPacketFixtureTests
{
    static GoldenSocialPacketFixtureTests()
    {
        CustomConfig.BASE_FLYTIME = 60;
        EnsureDataManagerBridge();
    }

    [Theory]
    [InlineData("SM_EXCHANGE_ADD_KINAH.json")]
    [InlineData("SM_EXCHANGE_CONFIRMATION.json")]
    [InlineData("SM_LEGION_LEAVE_MEMBER.json")]
    [InlineData("SM_ATREIAN_PASSPORT.json")]
    [InlineData("SM_MESSAGE.json")]
    [InlineData("SM_EXCHANGE_REQUEST.json")]
    [InlineData("SM_HOUSE_ACQUIRE.json")]
    [InlineData("SM_HOUSE_TELEPORT.json")]
    [InlineData("SM_HOUSE_PAY_RENT.json")]
    [InlineData("SM_LEGION_UPDATE_NICKNAME.json")]
    [InlineData("SM_LEGION_UPDATE_SELF_INTRO.json")]
    [InlineData("SM_LEGION_UPDATE_TITLE.json")]
    [InlineData("SM_FRIEND_STATUS.json")]
    [InlineData("SM_MARK_FRIENDLIST.json")]
    public void CsharpSocialPacketMatchesJavaGoldenFixture(string fixtureFile)
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
            case "SM_EXCHANGE_ADD_KINAH":
                return (new SM_EXCHANGE_ADD_KINAH(inputs.GetProperty("kinahCount").GetInt64(), inputs.GetProperty("action").GetInt32()), null);
            case "SM_EXCHANGE_CONFIRMATION":
                return (new SM_EXCHANGE_CONFIRMATION(inputs.GetProperty("action").GetInt32()), null);
            case "SM_LEGION_LEAVE_MEMBER":
            {
                var msgId = inputs.GetProperty("msgId").GetInt32();
                var objId = inputs.GetProperty("playerObjId").GetInt32();
                var name = inputs.GetProperty("name").GetString()!;
                var name1Prop = inputs.GetProperty("name1");
                // Java's single-name ctor leaves name1 == null; writeS(null) == writeS("") on the wire.
                if (name1Prop.ValueKind == JsonValueKind.Null)
                    return (new SM_LEGION_LEAVE_MEMBER(msgId, objId, name), null);
                return (new SM_LEGION_LEAVE_MEMBER(msgId, objId, name, name1Prop.GetString()!), null);
            }
            case "SM_ATREIAN_PASSPORT":
            {
                var date = new DateOnly(inputs.GetProperty("year").GetInt32(), inputs.GetProperty("month").GetInt32(), inputs.GetProperty("day").GetInt32());
                // Empty passport list -> only the header is written; no per-passport rows.
                return (new SM_ATREIAN_PASSPORT(new PassportsList(), inputs.GetProperty("stamps").GetInt32(), date), null);
            }
            case "SM_MESSAGE":
            {
                var active = NewPlayer(790000, Race.ELYOS); // non-staff (accessLevel 0) -> writeC(senderRace==0)
                var con = NewConnectionWithActivePlayer(active);
                var chatType = Enum.Parse<ChatType>(inputs.GetProperty("chatType").GetString()!);
                var packet = new SM_MESSAGE(
                    inputs.GetProperty("senderObjectId").GetInt32(),
                    inputs.GetProperty("senderName").GetString()!,
                    inputs.GetProperty("message").GetString()!,
                    chatType);
                return (packet, con);
            }
            case "SM_EXCHANGE_REQUEST":
                return (new SM_EXCHANGE_REQUEST(inputs.GetProperty("receiver").GetString()!), null);
            case "SM_HOUSE_ACQUIRE":
                return (new SM_HOUSE_ACQUIRE(inputs.GetProperty("playerId").GetInt32(), inputs.GetProperty("address").GetInt32(), inputs.GetProperty("acquire").GetBoolean()), null);
            case "SM_HOUSE_TELEPORT":
                return (new SM_HOUSE_TELEPORT(inputs.GetProperty("houseAddress").GetInt32(), inputs.GetProperty("playerId").GetInt32()), null);
            case "SM_HOUSE_PAY_RENT":
                return (new SM_HOUSE_PAY_RENT(inputs.GetProperty("weeksPaid").GetInt32()), null);
            case "SM_LEGION_UPDATE_NICKNAME":
                return (new SM_LEGION_UPDATE_NICKNAME(inputs.GetProperty("playerObjId").GetInt32(), inputs.GetProperty("newNickname").GetString()!), null);
            case "SM_LEGION_UPDATE_SELF_INTRO":
                return (new SM_LEGION_UPDATE_SELF_INTRO(inputs.GetProperty("playerObjId").GetInt32(), inputs.GetProperty("selfintro").GetString()!), null);
            case "SM_LEGION_UPDATE_TITLE":
            {
                var rank = Enum.Parse<Aion.GameServer.Model.Team.Legion.LegionRank>(inputs.GetProperty("rank").GetString()!);
                return (new SM_LEGION_UPDATE_TITLE(inputs.GetProperty("playerObjectId").GetInt32(), inputs.GetProperty("legionId").GetInt32(), inputs.GetProperty("legionName").GetString()!, rank), null);
            }
            case "SM_FRIEND_STATUS":
                return (new SM_FRIEND_STATUS(inputs.GetProperty("status").GetInt32()), null);
            case "SM_MARK_FRIENDLIST":
            {
                var active = NewPlayer(inputs.GetProperty("objectId").GetInt32(), Race.ELYOS);
                return (new SM_MARK_FRIENDLIST(), NewConnectionWithActivePlayer(active));
            }
            default:
                throw new NotSupportedException($"No C# reconstruction registered for {packetName}");
        }
    }

    // ---- minimal player (faithful base ctor; accessLevel 0 => non-staff) ----

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

    // ---- integration seam (mirrors GoldenEnterWorldPacketFixtureTests) ----

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
            "Regenerate with: mvn -pl game-server -am test -Dtest=GoldenSocialPacketFixtureGeneratorTest " +
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
