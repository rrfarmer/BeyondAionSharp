using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Aion.Commons.Nio;
using Aion.GameServer.Controllers;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Model.Templates.Quest;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Model.Templates.Stats;
using Aion.GameServer.Model.Templates.Tradelist;
using Aion.GameServer.Model.Templates.World;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using IDFactory = Aion.GameServer.Utils.IdFactory.IDFactory;

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

    // SM_SKILL_COOLDOWN: the one skill template the fixture reads from SKILL_DATA (id + raw cooldown; wire = cooldown*100).
    private const int CooldownSkillId = 1968;
    private const int CooldownRaw = 250;

    // SM_QUEST_ACTION: the two quest templates the fixture reads from QUEST_DATA (one NONE category, one extra category).
    private const int QuestIdNone = 1006;
    private const int QuestIdExtra = 1007;

    // SM_TRADE_IN_LIST: the live-Npc objectId the fixture reads (mirrors the Java side; the only live read).
    private const int TradeNpcObjectId = 700123;

    // SM_NPC_INFO real-Npc-ctor seam: the structurally-identical Npc/NpcTemplate/StatsTemplate scalars (== Java side).
    private const int NpcInfoObjectId = 740555;
    private const int NpcInfoNpcId = 215220;
    private const int NpcInfoWorldId = 220020000;
    private const int NpcInfoNameId = 350123;
    private const int NpcInfoTitleId = 4242;
    private const byte NpcInfoLevel = 55;
    private const int NpcInfoMaxHp = 123456;
    private const int NpcInfoAttackSpeed = 1500;
    private const float NpcInfoHeight = 1.75f;
    private const float NpcInfoBrFront = 1.25f;
    private const float NpcInfoBrSide = 0.95f;
    private const float NpcInfoBrUpper = 2.5f;
    private const float NpcInfoX = 1450.5f;
    private const float NpcInfoY = 1602.25f;
    private const float NpcInfoZ = 250.125f;
    private const byte NpcInfoHeading = 60;

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

    [Theory]
    [InlineData("SM_SKILL_COOLDOWN.json")]
    public void CsharpSkillCooldownMatchesJavaGoldenFixture(string fixtureFile)
    {
        using var fixture = LoadFixture(fixtureFile);
        foreach (var caseElement in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var caseName = caseElement.GetProperty("name").GetString()!;
            var expectedHex = caseElement.GetProperty("payloadHex").GetString()!;
            var inputs = caseElement.GetProperty("inputs");

            // Scalar ctor (skillId, expirationTimeMillis); expiration 0 -> deterministic (no UtcNow read).
            var packet = new SM_SKILL_COOLDOWN(
                inputs.GetProperty("skillId").GetInt32(),
                inputs.GetProperty("expirationTimeMillis").GetInt64());

            var actualHex = Convert.ToHexString(CaptureWriteImplPayload(packet));
            Assert.True(expectedHex == actualHex,
                $"SM_SKILL_COOLDOWN/{caseName}: C# payload diverged from Java golden.\n" +
                $"  Java : {expectedHex}\n  C#   : {actualHex}\n" +
                $"  firstDiffByte: {FirstDiffByte(expectedHex, actualHex)}");
        }
    }

    [Theory]
    [InlineData("SM_QUEST_ACTION.json")]
    public void CsharpQuestActionMatchesJavaGoldenFixture(string fixtureFile)
    {
        using var fixture = LoadFixture(fixtureFile);
        foreach (var caseElement in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var caseName = caseElement.GetProperty("name").GetString()!;
            var expectedHex = caseElement.GetProperty("payloadHex").GetString()!;
            var inputs = caseElement.GetProperty("inputs");

            var ctor = inputs.GetProperty("ctor").GetString()!;
            var questId = inputs.GetProperty("questId").GetInt32();
            SM_QUEST_ACTION packet = ctor switch
            {
                "unk" => new SM_QUEST_ACTION(questId),
                "timer" => new SM_QUEST_ACTION(questId, inputs.GetProperty("timer").GetInt32()),
                "share" => new SM_QUEST_ACTION(questId, inputs.GetProperty("sharerId").GetInt32(),
                    inputs.GetProperty("shareInAlliance").GetBoolean()),
                _ => throw new NotSupportedException($"Unknown SM_QUEST_ACTION ctor: {ctor}"),
            };

            var actualHex = Convert.ToHexString(CaptureWriteImplPayload(packet));
            Assert.True(expectedHex == actualHex,
                $"SM_QUEST_ACTION/{caseName}: C# payload diverged from Java golden.\n" +
                $"  Java : {expectedHex}\n  C#   : {actualHex}\n" +
                $"  firstDiffByte: {FirstDiffByte(expectedHex, actualHex)}");
        }
    }

    // ---- live-Npc object seam (SM_TRADE_IN_LIST) ----------------------------------------------------------------

    /// <summary>
    /// FIRST golden seam that drives a packet through a live Npc game-object. SM_TRADE_IN_LIST.WriteImpl reads ONLY
    /// npc.GetObjectId() from the live object (no template/stats/AI/World), plus the directly-constructed
    /// TradeListTemplate scalars — so the bounded live Npc is allocated uninitialized (RuntimeHelpers.GetUninitializedObject,
    /// the established harness precedent, mirroring the Java Unsafe.allocateInstance side) with only the final
    /// AionObject._objectId pinned. Java is the oracle.
    /// </summary>
    [Theory]
    [InlineData("SM_TRADE_IN_LIST.json")]
    public void CsharpTradeInListMatchesJavaGoldenFixture(string fixtureFile)
    {
        using var fixture = LoadFixture(fixtureFile);
        var npc = NewUninitializedNpc(TradeNpcObjectId);

        foreach (var caseElement in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var caseName = caseElement.GetProperty("name").GetString()!;
            var expectedHex = caseElement.GetProperty("payloadHex").GetString()!;
            var inputs = caseElement.GetProperty("inputs");

            Assert.Equal(TradeNpcObjectId, inputs.GetProperty("objectId").GetInt32());
            var npcId = inputs.GetProperty("npcId").GetInt32();
            var npcType = Enum.Parse<TradeNpcType>(inputs.GetProperty("npcType").GetString()!);
            var buyPriceModifier = inputs.GetProperty("buyPriceModifier").GetInt32();
            var tabIds = inputs.GetProperty("tabIds").EnumerateArray().Select(e => e.GetInt32()).ToArray();

            var tlist = BuildTradeListTemplate(npcId, npcType, tabIds);
            var packet = new SM_TRADE_IN_LIST(npc, tlist, buyPriceModifier);

            var actualHex = Convert.ToHexString(CaptureWriteImplPayload(packet));
            Assert.True(expectedHex == actualHex,
                $"SM_TRADE_IN_LIST/{caseName}: C# payload diverged from Java golden.\n" +
                $"  Java : {expectedHex}\n  C#   : {actualHex}\n" +
                $"  firstDiffByte: {FirstDiffByte(expectedHex, actualHex)}");
        }
    }

    /// <summary>Build a TradeListTemplate carrying the structurally-identical scalars + trade tabs (== Java side).</summary>
    private static TradeListTemplate BuildTradeListTemplate(int npcId, TradeNpcType npcType, int[] tabIds)
    {
        var t = new TradeListTemplate();
        SetField(t, "npcId", npcId);
        SetField(t, "tradeNpcType", npcType);
        var tabs = new List<TradeListTemplate.TradeTab>();
        foreach (var id in tabIds)
        {
            var tab = new TradeListTemplate.TradeTab();
            SetField(tab, "id", id);
            tabs.Add(tab);
        }
        SetField(t, "tradeTablist", tabs);
        return t;
    }

    /// <summary>
    /// Allocate an Npc WITHOUT running any constructor (mirrors the Java Unsafe.allocateInstance seam), then pin only
    /// the final AionObject._objectId. SM_TRADE_IN_LIST reads nothing else from the live object.
    /// </summary>
    private static Npc NewUninitializedNpc(int objectId)
    {
        var npc = (Npc)RuntimeHelpers.GetUninitializedObject(typeof(Npc));
        var idField = typeof(AionObject).GetField("_objectId", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(AionObject).FullName, "_objectId");
        idField.SetValue(npc, objectId);
        return npc;
    }

    // ---- real-Npc-ctor object seam (SM_NPC_INFO) ----------------------------------------------------------------

    /// <summary>
    /// The real-Npc-ctor golden seam. SM_NPC_INFO is the maximal Npc-reading packet: its WriteImpl reads the live Npc's
    /// stat containers (GetLifeStats().GetHpPercentage(), GetGameStats().GetMaxHp(), GetMovementSpeedFloat()), the move
    /// controller (GetTargetX2/Y2/Z2/GetMovementMask), the NpcTemplate, and TownService. So this drives a REAL
    /// Npc(controller, spawn, template) ctor through SetupStatContainers -> NpcGameStats/NpcLifeStats (built from a
    /// populated StatsTemplate) with a DummyAI (NpcTemplate.ai == null + SpawnTemplate.aiName == null) — no
    /// World/Knownlist/SkillEngine/DataManager-cascade. Built IDENTICALLY to the Java oracle side; Java is the oracle.
    /// </summary>
    [Theory]
    [InlineData("SM_NPC_INFO.json")]
    public void CsharpNpcInfoMatchesJavaGoldenFixture(string fixtureFile)
    {
        using var fixture = LoadFixture(fixtureFile);

        foreach (var caseElement in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var caseName = caseElement.GetProperty("name").GetString()!;
            var expectedHex = caseElement.GetProperty("payloadHex").GetString()!;
            var inputs = caseElement.GetProperty("inputs");

            Assert.Equal(NpcInfoObjectId, inputs.GetProperty("objectId").GetInt32());
            var creatureType = Enum.Parse<CreatureType>(inputs.GetProperty("creatureType").GetString()!);

            var npc = BuildRealNpc(NpcInfoObjectId, creatureType);
            // player arg null: npc.type is pinned so GetType_(player) short-circuits without dereferencing the player.
            var packet = new SM_NPC_INFO(npc, null);

            var actualHex = Convert.ToHexString(CaptureWriteImplPayload(packet));
            Assert.True(expectedHex == actualHex,
                $"SM_NPC_INFO/{caseName}: C# payload diverged from Java golden.\n" +
                $"  Java : {expectedHex}\n  C#   : {actualHex}\n" +
                $"  firstDiffByte: {FirstDiffByte(expectedHex, actualHex)}");
        }
    }

    /// <summary>
    /// Build a REAL Npc through the full Npc(controller, spawn, template) ctor (== Java buildRealNpc), then make it
    /// deterministic: overwrite the IDFactory-assigned objectId with a pinned value and pin the npc.type field (so
    /// GetType_(player) short-circuits). The template carries a populated StatsTemplate (maxHp) + FLAG type.
    /// </summary>
    private static Npc BuildRealNpc(int objectId, CreatureType type)
    {
        var template = BuildNpcTemplate();
        var spawnGroup = new SpawnGroup(NpcInfoWorldId, NpcInfoNpcId, 0, null);
        // staticId 0 -> SM_NPC_INFO writes GetSpawn().GetStaticId() == 0.
        var spawn = new SpawnTemplate(spawnGroup, NpcInfoX, NpcInfoY, NpcInfoZ, NpcInfoHeading, 0, null, 0);
        var controller = new NpcController();
        var npc = new Npc(controller, spawn, template);

        // Pin objectId (IDFactory-assigned is non-deterministic).
        var idField = typeof(AionObject).GetField("_objectId", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(AionObject).FullName, "_objectId");
        idField.SetValue(npc, objectId);

        // Pin the npc.type field so GetType_(player) short-circuits (player can be null).
        var typeField = typeof(Npc).GetField("type", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(Npc).FullName, "type");
        typeField.SetValue(npc, type);
        return npc;
    }

    private static NpcTemplate BuildNpcTemplate()
    {
        var t = new NpcTemplate();
        SetField(t, "npcId", NpcInfoNpcId);
        SetField(t, "nameId", NpcInfoNameId);
        SetField(t, "titleId", NpcInfoTitleId);
        SetField(t, "level", NpcInfoLevel);
        SetField(t, "height", NpcInfoHeight);
        SetField(t, "attackSpeed", NpcInfoAttackSpeed);
        SetField(t, "npcTemplateType", (NpcTemplateType?)NpcTemplateType.FLAG); // IsFlag() == true -> deterministic
        t.boundRadius = new BoundRadius(NpcInfoBrFront, NpcInfoBrSide, NpcInfoBrUpper);
        t.statsTemplate = BuildStatsTemplate();
        // ai left null -> DummyAI (no AIEngine registration needed).
        return t;
    }

    private static StatsTemplate BuildStatsTemplate()
    {
        var s = new StatsTemplate { MaxHp = NpcInfoMaxHp };
        // Speeds left null -> GetRunSpeed() == 0 -> GetMovementSpeedFloat() == 0.0f (deterministic).
        return s;
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
        SetAutoProperty(staticData, nameof(StaticData.SkillDataDh), BuildSkillData());
        SetAutoProperty(staticData, nameof(StaticData.Quests), BuildQuestsData());
        // SM_NPC_INFO real-Npc seam: NpcSkillList(owner) reads NPC_SKILL_DATA; TownService ctor reads HOUSE_DATA.GetLands().
        // The uninitialized StaticData skips field initializers, so seed both with empty holders (NpcSkillData/HouseData
        // default-ctor empty -> getNpcSkillList(npcId) null + getLands() empty). Mirrors the Java HOUSE_DATA/NPC_SKILL_DATA seam.
        SetAutoProperty(staticData, nameof(StaticData.NpcSkillDataDh), new NpcSkillData());
        SetAutoProperty(staticData, nameof(StaticData.HouseDataDh), new HouseData());

        var dmCtor = typeof(DataManager).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic, binder: null, new[] { typeof(StaticData) }, modifiers: null)!;
        var dm = (DataManager)dmCtor.Invoke(new object[] { staticData });
        DataManager.RegisterInstance(dm);

        // IDFactory singleton bridge: the real Npc(controller,spawn,template) ctor calls IDFactory.GetInstance().NextId().
        // Register a fresh empty IDFactory if none is bound (mirrors the Java lazy SingletonHolder; the assigned objectId
        // is overwritten with a pinned value, so its value is irrelevant).
        try { _ = IDFactory.GetInstance(); }
        catch (InvalidOperationException) { IDFactory.RegisterInstance(new IDFactory()); }
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

    /// <summary>Build a SkillData carrying exactly the one skill template SM_SKILL_COOLDOWN reads (== Java side).</summary>
    private static SkillData BuildSkillData()
    {
        var t = new SkillTemplate();
        SetField(t, "skillId", CooldownSkillId);
        SetField(t, "cooldown", CooldownRaw);
        var holder = new SkillData();
        PutPrivateMapEntry(holder, "skillTemplateById", CooldownSkillId, t);
        return holder;
    }

    /// <summary>Build a QuestsData carrying the NONE + extra-category quest templates SM_QUEST_ACTION reads (== Java side).</summary>
    private static QuestsData BuildQuestsData()
    {
        var none = new QuestTemplate();
        SetField(none, "id", QuestIdNone);
        SetField(none, "extraCategory", QuestExtraCategory.NONE);
        var extra = new QuestTemplate();
        SetField(extra, "id", QuestIdExtra);
        SetField(extra, "extraCategory", QuestExtraCategory.COIN_QUEST);
        var holder = new QuestsData();
        PutPrivateMapEntry(holder, "questTemplates", QuestIdNone, none);
        PutPrivateMapEntry(holder, "questTemplates", QuestIdExtra, extra);
        return holder;
    }

    private static void SetField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new MissingFieldException(target.GetType().FullName, fieldName);
        field.SetValue(target, value);
    }

    /// <summary>Mirror the Java reflective-index populate: put one entry into a holder's private id->template map.</summary>
    private static void PutPrivateMapEntry<TKey, TValue>(object holder, string mapFieldName, TKey key, TValue value)
    {
        var field = holder.GetType().GetField(mapFieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(holder.GetType().FullName, mapFieldName);
        var map = (IDictionary<TKey, TValue>)field.GetValue(holder)!;
        map[key] = value;
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
