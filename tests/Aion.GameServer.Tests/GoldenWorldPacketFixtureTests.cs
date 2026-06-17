using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Aion.Commons.Nio;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.Templates.Stats;
using Aion.GameServer.Model.Templates.World;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

/// <summary>
/// INTEGRATION golden asserter for the World-reading SM_* family — increment 1 of the deferred integration-harness
/// sub-project (docs/next-slop-targets.md). This is the FIRST golden seam that drives a packet through the
/// <see cref="DataManager.WORLD_MAPS_DATA"/> world-map holder rather than only scalar/ctor state.
///
/// Reads the SHARED fixture produced by the Java harness
/// (game-server GoldenWorldPacketFixtureGeneratorTest -> parity-artifacts/golden/packets/SM_TELEPORT_LOC.json)
/// and asserts the C# SM_TELEPORT_LOC writer emits byte-for-byte identical payloads. Java is the oracle.
///
/// Seam: a minimal DataManager whose WorldMapsData holder carries exactly the two map templates this packet reads
/// (one non-instance, one instance), built IDENTICALLY on both sides — the C# side via an uninitialized StaticData
/// with the WorldMaps2 backing field set + the DataManager test ctor; the Java side via DataManager.WORLD_MAPS_DATA
/// reflectively populated with the structurally identical WorldMapTemplate set. SM_TELEPORT_LOC.writeImpl is pure
/// scalar; its ONLY non-ctor read is the ctor's DataManager.WORLD_MAPS_DATA.GetTemplate(mapId).IsInstance() branch,
/// which selects between writing the instanceId (instance map) or the mapId (regular map) for the channel field.
///
/// Joins the GoldenDataManager non-parallel collection: it calls DataManager.RegisterInstance (a global mutable
/// singleton), so it must run serially with the other DataManager-mutating golden classes.
/// </summary>
[Xunit.Collection("GoldenDataManager")]
public sealed class GoldenWorldPacketFixtureTests
{
    // The two map ids the SM_TELEPORT_LOC fixture exercises (identical on both sides). One regular (non-instance)
    // world and one instance world, so the ctor's WORLD_MAPS_DATA.GetTemplate(mapId).IsInstance() branch is covered
    // both ways. Morheim (220020000) is a regular field map; Draupnir Cave (320080000) is an instance.
    private const int RegularMapId = 220020000;
    private const int InstanceMapId = 320080000;

    private static readonly long[] ExpTable = BuildExpTable();

    private static long[] BuildExpTable()
    {
        var t = new long[67];
        for (long i = 0; i < t.Length; i++)
            t[i] = 100L * i * i * i + 1000L * i;
        return t;
    }

    static GoldenWorldPacketFixtureTests()
    {
        EnsureDataManagerBridgeWithWorldMaps();
    }

    [Theory]
    [InlineData("SM_TELEPORT_LOC.json")]
    public void CsharpWorldPacketMatchesJavaGoldenFixture(string fixtureFile)
    {
        using var fixture = LoadFixture(fixtureFile);
        var packetName = fixture.RootElement.GetProperty("packet").GetString()!;

        foreach (var caseElement in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var caseName = caseElement.GetProperty("name").GetString()!;
            var expectedHex = caseElement.GetProperty("payloadHex").GetString()!;
            var inputs = caseElement.GetProperty("inputs");

            var packet = packetName switch
            {
                "SM_TELEPORT_LOC" => new SM_TELEPORT_LOC(
                    inputs.GetProperty("mapId").GetInt32(),
                    inputs.GetProperty("instanceId").GetInt32(),
                    inputs.GetProperty("x").GetSingle(),
                    inputs.GetProperty("y").GetSingle(),
                    inputs.GetProperty("z").GetSingle(),
                    (byte)inputs.GetProperty("heading").GetInt32(),
                    (TeleportAnimation)inputs.GetProperty("portAnimation").GetInt32()),
                _ => throw new NotSupportedException($"No World reconstruction for {packetName}"),
            };

            var actual = CaptureWriteImplPayload(packet);
            var actualHex = Convert.ToHexString(actual);

            Assert.True(expectedHex == actualHex,
                $"{packetName}/{caseName}: C# payload diverged from Java golden.\n" +
                $"  Java : {expectedHex}\n  C#   : {actualHex}\n" +
                $"  firstDiffByte: {FirstDiffByte(expectedHex, actualHex)}");
        }
    }

    // ---- World/instance DataManager seam ------------------------------------------------------------------------

    /// <summary>
    /// Register a DataManager whose WorldMapsData holder carries the two map templates SM_TELEPORT_LOC reads. Also
    /// seeds the same AbsoluteStatsData + PLAYER_EXPERIENCE_TABLE the sibling golden classes register, so that whichever
    /// GoldenDataManager-collection class wins the (serial) registration the singleton is always fully populated.
    /// </summary>
    private static void EnsureDataManagerBridgeWithWorldMaps()
    {
        // If a DataManager is already registered with our world maps present, do nothing.
        try
        {
            if (DataManager.WORLD_MAPS_DATA?.GetTemplate(RegularMapId) is not null)
                return;
        }
        catch (InvalidOperationException) { }
        catch (NullReferenceException) { }

        var staticData = (StaticData)RuntimeHelpers.GetUninitializedObject(typeof(StaticData));
        SetAutoProperty(staticData, nameof(StaticData.AbsoluteStatsDataDh), new AbsoluteStatsData());
        SetAutoProperty(staticData, nameof(StaticData.PlayerExperienceTable), new PlayerExperienceTable(ExpTable));
        SetAutoProperty(staticData, nameof(StaticData.WorldMaps2), BuildWorldMaps());

        var dmCtor = typeof(DataManager).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic, binder: null, new[] { typeof(StaticData) }, modifiers: null)!;
        var dm = (DataManager)dmCtor.Invoke(new object[] { staticData });
        DataManager.RegisterInstance(dm);
    }

    /// <summary>Build a WorldMapsData with exactly one regular and one instance template (structurally == Java side).</summary>
    private static WorldMapsData BuildWorldMaps()
    {
        var regular = new WorldMapTemplate { MapId = RegularMapId, Name = "MORHEIM", Instance = false };
        var instance = new WorldMapTemplate { MapId = InstanceMapId, Name = "DRAUPNIR_CAVE", Instance = true };
        var holder = new WorldMapsData();
        holder.SetData(new List<WorldMapTemplate> { regular, instance });
        return holder;
    }

    private static void SetAutoProperty(object target, string propertyName, object value)
    {
        var field = target.GetType().GetField($"<{propertyName}>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(target.GetType().FullName, propertyName);
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
            "Regenerate with: mvn -pl game-server -am test -Dtest=GoldenWorldPacketFixtureGeneratorTest " +
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
