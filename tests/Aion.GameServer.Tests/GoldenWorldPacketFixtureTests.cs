using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Aion.Commons.Nio;
using Aion.GameServer.Controllers;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Model.Templates.Quest;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Items.Enums;
using Aion.GameServer.Model.Templates.Restriction;
using Aion.GameServer.Model.Templates.Stats;
using Aion.GameServer.Model.Templates.Tradelist;
using Aion.GameServer.Model.Templates.World;
using ItemUpdateType = Aion.GameServer.Services.Items.ItemPacketService.ItemUpdateType;
using ItemAddType = Aion.GameServer.Services.Items.ItemPacketService.ItemAddType;
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

    // SM_SELL_ITEM: the purchase-template buy-price-rate the fixture reads from TRADE_LIST_DATA (== Java side).
    private const int SellBuyPriceRate = 115;

    // SM_INVENTORY_UPDATE_ITEM item/ItemInfoBlob seam: the non-equippable Item/ItemTemplate scalars (== Java side).
    private const int ItemObjectId = 268500001;
    private const int ItemTemplateId = 161000001;
    private const int ItemMask = 0x1A2B;
    private const int ItemDescL10n = 350123;
    private const long ItemCount = 7L;
    private const string ItemCreator = "Daeva";

    // SM_INVENTORY_ADD_ITEM equippable-weapon item/ItemInfoBlob seam: the equippable Item/ItemTemplate scalars (== Java side).
    private const int EqItemObjectId = 268700001;
    private const int EqItemTemplateId = 100000855;
    private const int EqItemMask = 0x2C4D;
    private const int EqItemDescL10n = 350456;
    private const long EqItemCount = 1L;
    private const string EqItemCreator = "Smith";

    // SM_INVENTORY_ADD_ITEM more per-type variants (armor SLOTS_ARMOR / accessory SLOTS_ACCESSORY / dyed armor). Distinct
    // objectIds so they never clobber the weapon fixture. Mirror the Java EQ_ARMOR_*/EQ_ACCESSORY_*/EQ_DYED_* constants.
    private const int EqArmorObjectId = 268700002;
    private const int EqArmorTemplateId = 110000777;
    private const int EqArmorMask = 0x33AA;
    private const int EqArmorDescL10n = 350789;
    private const int EqAccessoryObjectId = 268700003;
    private const int EqAccessoryTemplateId = 115000333;
    private const int EqAccessoryMask = 0x44BB;
    private const int EqAccessoryDescL10n = 350790;
    private const int EqDyedArmorObjectId = 268700004;
    private const int EqDyedArmorColor = 0x3399CC;

    // SM_INVENTORY_ADD_ITEM_SUBOBJECT: ENCHANT_INFO sub-object writers on a 1H-sword base. Distinct objectIds so they
    // never clobber the weapon/armor/accessory/shield/wing/plume fixtures. Mirror the Java EQ_MS_*/EQ_GS_*/EQ_SUBOBJ_* constants.
    private const int EqMsObjectId = 268700201;
    private const int EqGsObjectId = 268700202;
    private const int EqMsGsObjectId = 268700203;
    private const int EqSubObjTemplateId = 100000855; // same 1H sword template id as the weapon seam
    private const int EqSubObjMask = 0x2C4D;
    private const int EqSubObjDescL10n = 350456;
    private const int EqMsSlot0ItemId = 167000001;
    private const int EqMsSlot2ItemId = 167000002;
    private const int EqGsItemId = 168000123;

    // SM_VIEW_PLAYER_DETAILS: the live-Player objectId the fixture reads (mirrors the Java side; the only live read).
    private const int ViewDetailsPlayerObjectId = 268900001;

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

    // ---- additional real-Npc-ctor packets (reuse the SM_NPC_INFO seam) ------------------------------------------

    /// <summary>
    /// SM_MOVE reuses the seam: WriteImpl reads objectId + X/Y/Z/Heading (the un-spawned Npc's WorldPosition == 0,
    /// identical both sides) + movementMask; NpcMoveController is a plain CreatureMoveController (pmc == null), so the
    /// POSITION|MANUAL branch writes GetTargetX2/Y2/Z2 (TargetDest* default 0). No glide/vehicle bits set.
    /// </summary>
    [Theory]
    [InlineData("SM_MOVE.json")]
    public void CsharpMoveMatchesJavaGoldenFixture(string fixtureFile)
    {
        using var fixture = LoadFixture(fixtureFile);
        var npc = BuildRealNpc(NpcInfoObjectId, CreatureType.PEACE);
        foreach (var caseElement in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var caseName = caseElement.GetProperty("name").GetString()!;
            var expectedHex = caseElement.GetProperty("payloadHex").GetString()!;
            var inputs = caseElement.GetProperty("inputs");
            Assert.Equal(NpcInfoObjectId, inputs.GetProperty("objectId").GetInt32());
            var movementMask = (byte)inputs.GetProperty("movementMask").GetInt32();

            var actualHex = Convert.ToHexString(CaptureWriteImplPayload(new SM_MOVE(npc, movementMask)));
            Assert.True(expectedHex == actualHex,
                $"SM_MOVE/{caseName}: C# payload diverged from Java golden.\n" +
                $"  Java : {expectedHex}\n  C#   : {actualHex}\n" +
                $"  firstDiffByte: {FirstDiffByte(expectedHex, actualHex)}");
        }
    }

    /// <summary>
    /// SM_SELL_ITEM reuses the seam: the ctor reads the vendor purchase TradeListTemplate (NORMAL type / buy-rate /
    /// trade tabs) from the bounded TRADE_LIST_DATA holder + the npc's CanSell/CanBuy/CanPurchase dialog flags. The npc
    /// template has NO talkInfo -> SupportsAction(..) is false -> showBuyTab/showSellTab == 0; the purchase template is
    /// present so tradeNpcType/buyPriceRate/tabs come from the template (PricesService config default not reached).
    /// </summary>
    [Theory]
    [InlineData("SM_SELL_ITEM.json")]
    public void CsharpSellItemMatchesJavaGoldenFixture(string fixtureFile)
    {
        using var fixture = LoadFixture(fixtureFile);
        var npc = BuildRealNpc(NpcInfoObjectId, CreatureType.PEACE);
        foreach (var caseElement in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var caseName = caseElement.GetProperty("name").GetString()!;
            var expectedHex = caseElement.GetProperty("payloadHex").GetString()!;
            Assert.Equal(NpcInfoObjectId, caseElement.GetProperty("inputs").GetProperty("objectId").GetInt32());

            var actualHex = Convert.ToHexString(CaptureWriteImplPayload(new SM_SELL_ITEM(npc)));
            Assert.True(expectedHex == actualHex,
                $"SM_SELL_ITEM/{caseName}: C# payload diverged from Java golden.\n" +
                $"  Java : {expectedHex}\n  C#   : {actualHex}\n" +
                $"  firstDiffByte: {FirstDiffByte(expectedHex, actualHex)}");
        }
    }

    // ---- item / ItemInfoBlob seam (SM_INVENTORY_UPDATE_ITEM) ----------------------------------------------------

    /// <summary>
    /// The FIRST golden seam that drives a packet through a live Item game-object + the ItemInfoBlob blob-writer family.
    /// SM_INVENTORY_UPDATE_ITEM(player, item) defaults to ItemUpdateType.DEC_ITEM_USE -> ItemInfoBlob.GetFullBlob. The item
    /// template is pinned to a NON-EQUIPPABLE group (ItemGroup.NONE -> GetValidEquipmentSlots()==0, IsWeapon/IsArmor/
    /// IsTwoHandWeapon false) with no fusion / packCount 0 / not STIGMA_SHARD, so GetFullBlob adds EXACTLY ONE blob entry:
    /// GENERAL_INFO. That entry reads ONLY Item + ItemTemplate scalars (GetItemMask()->template mask, GetItemCount(),
    /// GetItemCreator(), SecondsUntilExpiration() [expireTime 0 -> 0, no clock], GetTemporaryExchangeTimeRemaining() [0],
    /// GetItemId()) plus DataManager.ITEM_CLEAN_UP.HasAccountOrLegionWhStorabilityDisabled (empty bplist -> false). The host
    /// packet also writes item.GetObjectId() + template.GetL10n() (ChatUtil.L10n(desc), pure scalar) + the DEC_ITEM_USE mask.
    /// No live Player deref, no World/stones/enchant/godstone cascade, no DataManager beyond the bounded ITEM_CLEAN_UP holder.
    /// Built IDENTICALLY to the Java oracle side; Java is the oracle.
    /// </summary>
    [Theory]
    [InlineData("SM_INVENTORY_UPDATE_ITEM.json")]
    public void CsharpInventoryUpdateItemMatchesJavaGoldenFixture(string fixtureFile)
    {
        using var fixture = LoadFixture(fixtureFile);
        foreach (var caseElement in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var caseName = caseElement.GetProperty("name").GetString()!;
            var expectedHex = caseElement.GetProperty("payloadHex").GetString()!;
            var inputs = caseElement.GetProperty("inputs");

            Assert.Equal(ItemObjectId, inputs.GetProperty("objectId").GetInt32());
            var creatorJson = inputs.GetProperty("itemCreator");
            string? creator = creatorJson.ValueKind == JsonValueKind.Null ? null : creatorJson.GetString();

            var item = BuildSimpleItem(ItemObjectId, ItemTemplateId, ItemMask, ItemDescL10n, ItemCount, creator);
            // player arg null: GetFullBlob only stashes it as blob owner; GENERAL_INFO never dereferences it.
            var packet = new SM_INVENTORY_UPDATE_ITEM(null, item, ItemUpdateType.DEC_ITEM_USE);

            var actualHex = Convert.ToHexString(CaptureWriteImplPayload(packet));
            Assert.True(expectedHex == actualHex,
                $"SM_INVENTORY_UPDATE_ITEM/{caseName}: C# payload diverged from Java golden.\n" +
                $"  Java : {expectedHex}\n  C#   : {actualHex}\n" +
                $"  firstDiffByte: {FirstDiffByte(expectedHex, actualHex)}");
        }
    }

    /// <summary>
    /// Build a minimal non-equippable Item via the simple Item(objId, itemTemplate) ctor (== Java buildSimpleItem). The
    /// template is directly constructed with itemId/mask/desc set, itemGroup NONE (no equip slots) and maxTuneCount pinned
    /// to 0 (what AfterUnmarshal sets for a slot-0 item) so CanTune() is false identically on both sides. The objectId is
    /// passed to the ctor directly (no IDFactory).
    /// </summary>
    private static Item BuildSimpleItem(int objectId, int itemId, int mask, int desc, long itemCount, string? creator)
    {
        var template = new ItemTemplate();
        template.itemId = itemId;
        template.mask = mask;
        template.description = desc;
        template.itemGroup = ItemGroup.NONE;
        template.maxTuneCount = 0; // CanTune() == false (deterministic, identical both sides)
        var item = new Item(objectId, template);
        item.SetItemCount(itemCount);
        item.SetItemCreator(creator);
        return item;
    }

    // ---- equippable item / ItemInfoBlob seam (SM_INVENTORY_ADD_ITEM, weapon blob path) --------------------------

    /// <summary>
    /// Extends the item/ItemInfoBlob seam to the EQUIPPABLE-item blob path. SM_INVENTORY_ADD_ITEM writes per-item
    /// item.GetObjectId() + template.GetTemplateId() + template.GetL10n(), then ItemInfoBlob.GetFullBlob(player, item).WriteMe(),
    /// then (item.GetEquipmentSlot() &amp; 0xFFFF) + (template.IsCloth() ? 1 : 0). For an EQUIPPABLE item GetFullBlob adds, in order,
    /// EQUIPPED_SLOT + the per-type blob (SLOTS_WEAPON for a 1H sword) + ENCHANT_INFO + PREMIUM_OPTION + GENERAL_INFO. The item
    /// template is pinned to ItemGroup.SWORD (ONE_HAND weapon: IsWeapon true, IsTwoHandWeapon false, no fusion) with no
    /// stones/godstone/idian/dye/tempering, so every blob writer is deterministic on the bare simple-ctor Item. No live Player
    /// deref. Built IDENTICALLY to the Java oracle side; Java is the oracle.
    /// </summary>
    [Theory]
    [InlineData("SM_INVENTORY_ADD_ITEM.json")]
    public void CsharpInventoryAddItemEquippableMatchesJavaGoldenFixture(string fixtureFile)
    {
        using var fixture = LoadFixture(fixtureFile);
        foreach (var caseElement in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var caseName = caseElement.GetProperty("name").GetString()!;
            var expectedHex = caseElement.GetProperty("payloadHex").GetString()!;
            var inputs = caseElement.GetProperty("inputs");

            Assert.Equal(EqItemObjectId, inputs.GetProperty("objectId").GetInt32());
            var addType = Enum.Parse<ItemAddType>(inputs.GetProperty("addType").GetString()!);

            var item = BuildEquippableWeapon(EqItemObjectId, EqItemTemplateId, EqItemMask, EqItemDescL10n, EqItemCount, EqItemCreator);
            // player arg null: GetFullBlob only stashes it as blob owner; the weapon/equipped/enchant/premium/general writers never deref it.
            var packet = new SM_INVENTORY_ADD_ITEM(new List<Item> { item }, null, addType);

            var actualHex = Convert.ToHexString(CaptureWriteImplPayload(packet));
            Assert.True(expectedHex == actualHex,
                $"SM_INVENTORY_ADD_ITEM/{caseName}: C# payload diverged from Java golden.\n" +
                $"  Java : {expectedHex}\n  C#   : {actualHex}\n" +
                $"  firstDiffByte: {FirstDiffByte(expectedHex, actualHex)}");
        }
    }

    /// <summary>
    /// Build a minimal EQUIPPABLE 1H-sword Item via the simple Item(objId, itemTemplate) ctor (== Java buildEquippableWeapon).
    /// itemGroup SWORD (ONE_HAND weapon, valid equip slots) + maxTuneCount 0 (CanTune() false -> IsIdentified() true). Left
    /// unequipped with no stones/godstone/idian/dye/tempering/fusion so the equippable blob path is deterministic.
    /// </summary>
    private static Item BuildEquippableWeapon(int objectId, int itemId, int mask, int desc, long itemCount, string creator)
    {
        var template = new ItemTemplate();
        template.itemId = itemId;
        template.mask = mask;
        template.description = desc;
        template.itemGroup = ItemGroup.SWORD;
        template.maxTuneCount = 0; // CanTune() == false -> IsIdentified() == true (deterministic, identical both sides)
        var item = new Item(objectId, template);
        item.SetItemCount(itemCount);
        item.SetItemCreator(creator);
        return item;
    }

    // ---- equippable item / ItemInfoBlob seam: more per-type variants (SM_INVENTORY_ADD_ITEM) -------------------

    /// <summary>
    /// Reuses the equippable-item seam to cover the OTHER per-type blob writers selected by GetFullBlob's itemGroup branch,
    /// each a DISTINCT fixture case with a DISTINCT objectId (never clobbers the weapon fixture):
    ///   (a) ARMOR (itemGroup PL_TORSO, a PLATE torso): IsArmor() true, armorType != ACCESSORY -> SLOTS_ARMOR. Its writer is
    ///       WriteQ(GetSlotFor(GetItemSlot()).GetSlotIdMask()) [PL_TORSO -> ItemSlot.TORSO, single slot], WriteQ(0),
    ///       WriteDyeInfo(GetItemColor()). Two armor cases: UNDYED (itemColor null -> 4 zero bytes) and DYED (itemColor pinned,
    ///       colorExpireTime 0 so GetColorTimeLeft()==0 -> NO clock; the dye-populated branch fires in BOTH SLOTS_ARMOR AND
    ///       ENCHANT_INFO). IsCloth() true for a non-accessory armor -> the host packet's trailing IsCloth byte is 1.
    ///   (b) ACCESSORY (itemGroup RING): IsArmor() true, armorType == ACCESSORY -> SLOTS_ACCESSORY. Reads GetSlotsFor(GetItemSlot())
    ///       [RING -> RING_LEFT|RING_RIGHT, length 2] -> WriteQ(slots[0]) + WriteQ(slots[1]). IsCloth() false -> trailing byte 0.
    /// All other blob reads are identical to the weapon seam. Built IDENTICALLY to the Java oracle side; Java is the oracle.
    /// </summary>
    [Theory]
    [InlineData("SM_INVENTORY_ADD_ITEM_VARIANTS.json")]
    public void CsharpInventoryAddItemEquippableVariantsMatchesJavaGoldenFixture(string fixtureFile)
    {
        using var fixture = LoadFixture(fixtureFile);
        foreach (var caseElement in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var caseName = caseElement.GetProperty("name").GetString()!;
            var expectedHex = caseElement.GetProperty("payloadHex").GetString()!;
            var inputs = caseElement.GetProperty("inputs");

            var objectId = inputs.GetProperty("objectId").GetInt32();
            var itemId = inputs.GetProperty("itemId").GetInt32();
            var mask = inputs.GetProperty("mask").GetInt32();
            var desc = inputs.GetProperty("desc").GetInt32();
            var creator = inputs.GetProperty("itemCreator").GetString()!;
            var itemGroup = Enum.Parse<ItemGroup>(inputs.GetProperty("itemGroup").GetString()!);
            var colorJson = inputs.GetProperty("itemColor");
            int? itemColor = colorJson.ValueKind == JsonValueKind.Null ? null : colorJson.GetInt32();
            var addType = Enum.Parse<ItemAddType>(inputs.GetProperty("addType").GetString()!);

            var item = BuildEquippableVariant(objectId, itemId, mask, desc, creator, itemGroup, itemColor);
            // player arg null: GetFullBlob only stashes it as blob owner; the per-type/enchant/premium/general writers never deref it.
            var packet = new SM_INVENTORY_ADD_ITEM(new List<Item> { item }, null, addType);

            var actualHex = Convert.ToHexString(CaptureWriteImplPayload(packet));
            Assert.True(expectedHex == actualHex,
                $"SM_INVENTORY_ADD_ITEM_VARIANTS/{caseName}: C# payload diverged from Java golden.\n" +
                $"  Java : {expectedHex}\n  C#   : {actualHex}\n" +
                $"  firstDiffByte: {FirstDiffByte(expectedHex, actualHex)}");
        }
    }

    /// <summary>
    /// Build a minimal EQUIPPABLE non-weapon Item (armor or accessory) via the simple Item(objId, itemTemplate) ctor
    /// (== Java buildEquippableVariant). itemGroup selects the per-type blob (PL_TORSO -> SLOTS_ARMOR, RING -> SLOTS_ACCESSORY).
    /// maxTuneCount 0 (CanTune() false -> IsIdentified() true). Optionally dyed via SetItemColor (colorExpireTime stays 0 ->
    /// GetColorTimeLeft()==0, no clock). No stones/godstone/idian/tempering/fusion so the equippable blob path is deterministic.
    /// </summary>
    private static Item BuildEquippableVariant(int objectId, int itemId, int mask, int desc, string creator,
        ItemGroup itemGroup, int? itemColor)
    {
        var template = new ItemTemplate();
        template.itemId = itemId;
        template.mask = mask;
        template.description = desc;
        template.itemGroup = itemGroup;
        template.maxTuneCount = 0; // CanTune() == false -> IsIdentified() == true (deterministic)
        var item = new Item(objectId, template);
        item.SetItemCount(1L);
        item.SetItemCreator(creator);
        if (itemColor.HasValue)
            item.SetItemColor(itemColor.Value); // colorExpireTime stays 0 -> GetColorTimeLeft() == 0 (deterministic)
        return item;
    }

    // ---- equippable item / ItemInfoBlob seam: per-type SHIELD / WING / PLUME blob writers (SM_INVENTORY_ADD_ITEM) ----

    /// <summary>
    /// Reuses the equippable-item seam to cover the THREE per-type blob writers GetFullBlob selects BEFORE the IsArmor()/
    /// IsWeapon() branches (itemGroup == WING / SHIELD / PLUME), each a DISTINCT fixture case with a DISTINCT objectId in a
    /// NEW fixture file (never clobbers the weapon/armor/accessory fixtures):
    ///   (a) SHIELD: GetFullBlob -> EQUIPPED_SLOT + SLOTS_SHIELD + ENCHANT_INFO + PREMIUM_OPTION + GENERAL_INFO. ShieldInfoBlobEntry
    ///       = WriteQ(GetSlotFor(GetItemSlot()).GetSlotIdMask()) [SHIELD -> ItemSlot.SUB_HAND], WriteQ(0), WriteDyeInfo(GetItemColor())
    ///       [null -> 4 zero bytes]. SHIELD subType -> ArmorType.GENERAL -> IsArmor() true, not ACCESSORY/BELT -> IsCloth() true -> trailing byte 1.
    ///   (b) WING: -> SLOTS_WING = WriteQ(GetSlotFor(GetItemSlot()).GetSlotIdMask()) [WING -> ItemSlot.WINGS], WriteQ(0). IsCloth() true -> byte 1.
    ///   (c) PLUME (untempered): -> PLUME_INFO = WriteQ(GetSlotFor(GetItemSlot()).GetSlotIdMask()) [PLUME -> ItemSlot.PLUME],
    ///       WriteQ(0x100000), WriteD(0)x4. PLUME subType -> EquipType.PLUME (not armor) -> IsCloth() false -> trailing byte 0.
    /// Plus two TEMPERED-plume cases (tempering>0, itemGroup PLUME) exercising the ENCHANT_INFO plume branch: temperingName
    /// "TSHIRT_PHYSICAL" (-> PLUM_PHISICAL_ATTACK) and a non-match name (-> PLUM_BOOST_MAGICAL_SKILL), each with a pinned
    /// tempering level + rndPlumeBonusValue, so BOTH PlumStatEnum branches are covered. Built IDENTICALLY to the Java oracle
    /// side; Java is the oracle.
    /// </summary>
    [Theory]
    [InlineData("SM_INVENTORY_ADD_ITEM_PERTYPE.json")]
    public void CsharpInventoryAddItemPerTypeMatchesJavaGoldenFixture(string fixtureFile)
    {
        using var fixture = LoadFixture(fixtureFile);
        foreach (var caseElement in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var caseName = caseElement.GetProperty("name").GetString()!;
            var expectedHex = caseElement.GetProperty("payloadHex").GetString()!;
            var inputs = caseElement.GetProperty("inputs");

            var objectId = inputs.GetProperty("objectId").GetInt32();
            var itemId = inputs.GetProperty("itemId").GetInt32();
            var mask = inputs.GetProperty("mask").GetInt32();
            var desc = inputs.GetProperty("desc").GetInt32();
            var creator = inputs.GetProperty("itemCreator").GetString()!;
            var itemGroup = Enum.Parse<ItemGroup>(inputs.GetProperty("itemGroup").GetString()!);
            var addType = Enum.Parse<ItemAddType>(inputs.GetProperty("addType").GetString()!);

            Item item;
            if (inputs.TryGetProperty("tempering", out var temperingJson))
            {
                // Tempered plume: temperingName + tempering level + rndPlumeBonusValue (exercises ENCHANT_INFO plume branch).
                var temperingName = inputs.GetProperty("temperingName").GetString()!;
                var tempering = temperingJson.GetInt32();
                var rndPlumeBonus = inputs.GetProperty("rndPlumeBonusValue").GetInt32();
                item = BuildTemperedPlume(objectId, itemId, mask, desc, creator, temperingName, tempering, rndPlumeBonus);
            }
            else
            {
                var colorJson = inputs.GetProperty("itemColor");
                int? itemColor = colorJson.ValueKind == JsonValueKind.Null ? null : colorJson.GetInt32();
                item = BuildEquippableVariant(objectId, itemId, mask, desc, creator, itemGroup, itemColor);
            }

            // player arg null: GetFullBlob only stashes it as blob owner; the per-type/enchant/premium/general writers never deref it.
            var packet = new SM_INVENTORY_ADD_ITEM(new List<Item> { item }, null, addType);

            var actualHex = Convert.ToHexString(CaptureWriteImplPayload(packet));
            Assert.True(expectedHex == actualHex,
                $"SM_INVENTORY_ADD_ITEM_PERTYPE/{caseName}: C# payload diverged from Java golden.\n" +
                $"  Java : {expectedHex}\n  C#   : {actualHex}\n" +
                $"  firstDiffByte: {FirstDiffByte(expectedHex, actualHex)}");
        }
    }

    /// <summary>
    /// Build a minimal EQUIPPABLE TEMPERED plume Item via the simple Item(objId, itemTemplate) ctor (== Java buildTemperedPlume).
    /// itemGroup PLUME (PLUME slot, IsArmor false) + temperingName set (so GetTemperingName().Equals(..) doesn't NRE) + tempering
    /// level + a random plume bonus value, so the ENCHANT_INFO plume branch fires. maxTuneCount 0 (CanTune() false -> IsIdentified()
    /// true). No stones/godstone/idian/dye/conditioning/fusion. Mirrors the Java side exactly.
    /// </summary>
    private static Item BuildTemperedPlume(int objectId, int itemId, int mask, int desc, string creator,
        string temperingName, int tempering, int rndPlumeBonusValue)
    {
        var template = new ItemTemplate();
        template.itemId = itemId;
        template.mask = mask;
        template.description = desc;
        template.itemGroup = ItemGroup.PLUME;
        template.temperingName = temperingName;
        template.maxTuneCount = 0; // CanTune() == false -> IsIdentified() == true (deterministic)
        var item = new Item(objectId, template);
        item.SetItemCount(1L);
        item.SetItemCreator(creator);
        item.SetTempering(tempering);
        item.SetRndPlumeBonusValue(rndPlumeBonusValue);
        return item;
    }

    // ---- equippable item / ItemInfoBlob seam: ENCHANT_INFO SUB-OBJECT writers (socketed ManaStone / GodStone) ----

    /// <summary>
    /// Reuses the equippable-weapon seam (1H SWORD base -> SLOTS_WEAPON, already byte-validated) and POPULATES the
    /// ENCHANT_INFO sub-object slots the bare-item seam left null/empty, each a DISTINCT fixture case with a DISTINCT
    /// objectId in a NEW fixture file (never clobbers the weapon/armor/accessory/shield/wing/plume fixtures):
    ///   (a) socketedManastones: item.GetItemStones() carries two ManaStones (slot 0 + slot 2, distinct itemIds). The
    ///       ENCHANT_INFO writer's CreateManastoneMap builds a slot-&gt;stone map; the Item.MAX_BASIC_STONES (6) loop writes
    ///       stone.GetItemId() at the populated slots and 0 elsewhere. ManaStone(itemObjId, itemId, slot, NEW) ctor reads
    ///       DataManager.ITEM_DATA.GetItemTemplate(itemId) (empty holder -> null, tolerated) -> only the ItemStone scalars
    ///       (slot/itemId) are read by the writer.
    ///   (b) godStone: item.SetGodStone(new GodStone(item, 0, godStoneId, null, NEW)) -> GetGodStoneId() == godStoneId. The
    ///       GodStone ctor takes godstoneInfo directly (null OK; the writer only reads GetItemId()), DataManager-free.
    ///   (c) manastonesAndGodStone: BOTH branches populated on one item.
    /// All other ENCHANT_INFO/SLOTS_WEAPON/PREMIUM_OPTION/GENERAL_INFO reads are identical to the weapon seam (no idian/
    /// dye/tempering/fusion). Built IDENTICALLY to the Java oracle side; Java is the oracle.
    /// </summary>
    [Theory]
    [InlineData("SM_INVENTORY_ADD_ITEM_SUBOBJECT.json")]
    public void CsharpInventoryAddItemSubObjectMatchesJavaGoldenFixture(string fixtureFile)
    {
        using var fixture = LoadFixture(fixtureFile);
        foreach (var caseElement in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var caseName = caseElement.GetProperty("name").GetString()!;
            var expectedHex = caseElement.GetProperty("payloadHex").GetString()!;
            var inputs = caseElement.GetProperty("inputs");

            var objectId = inputs.GetProperty("objectId").GetInt32();
            var withManastones = inputs.GetProperty("withManastones").GetBoolean();
            var withGodStone = inputs.GetProperty("withGodStone").GetBoolean();
            var addType = Enum.Parse<ItemAddType>(inputs.GetProperty("addType").GetString()!);

            var item = BuildSubObjectWeapon(objectId, withManastones, withGodStone);
            // player arg null: GetFullBlob only stashes it as blob owner; the weapon/enchant/premium/general writers never deref it.
            var packet = new SM_INVENTORY_ADD_ITEM(new List<Item> { item }, null, addType);

            var actualHex = Convert.ToHexString(CaptureWriteImplPayload(packet));
            Assert.True(expectedHex == actualHex,
                $"SM_INVENTORY_ADD_ITEM_SUBOBJECT/{caseName}: C# payload diverged from Java golden.\n" +
                $"  Java : {expectedHex}\n  C#   : {actualHex}\n" +
                $"  firstDiffByte: {FirstDiffByte(expectedHex, actualHex)}");
        }
    }

    /// <summary>
    /// Build a 1H-sword Item (same base as BuildEquippableWeapon) and POPULATE the ENCHANT_INFO sub-objects: optional
    /// socketed manastones at slots 0/2 (via GetItemStones().Add(new ManaStone(..))) and/or a godstone (via
    /// SetGodStone(new GodStone(..))). The ManaStone ctor reads DataManager.ITEM_DATA.GetItemTemplate(itemId) (empty
    /// holder -> null, tolerated); the GodStone ctor takes godstoneInfo directly (null OK). Mirrors the Java side exactly.
    /// </summary>
    private static Item BuildSubObjectWeapon(int objectId, bool withManastones, bool withGodStone)
    {
        var template = new ItemTemplate();
        template.itemId = EqSubObjTemplateId;
        template.mask = EqSubObjMask;
        template.description = EqSubObjDescL10n;
        template.itemGroup = ItemGroup.SWORD;
        template.maxTuneCount = 0; // CanTune() == false -> IsIdentified() == true (deterministic)
        var item = new Item(objectId, template);
        item.SetItemCount(1L);
        item.SetItemCreator("Smith");
        if (withManastones)
        {
            item.GetItemStones().Add(new Aion.GameServer.Model.Items.ManaStone(objectId, EqMsSlot0ItemId, 0,
                Aion.GameServer.Model.GameObjects.IPersistable.PersistentState.NEW));
            item.GetItemStones().Add(new Aion.GameServer.Model.Items.ManaStone(objectId, EqMsSlot2ItemId, 2,
                Aion.GameServer.Model.GameObjects.IPersistable.PersistentState.NEW));
        }
        if (withGodStone)
            item.SetGodStone(new Aion.GameServer.Model.Items.GodStone(item, 0, EqGsItemId, null,
                Aion.GameServer.Model.GameObjects.IPersistable.PersistentState.NEW));
        return item;
    }

    // ---- SM_VIEW_PLAYER_DETAILS (reuse the equippable-item / ItemInfoBlob seam) ---------------------------------

    /// <summary>
    /// SM_VIEW_PLAYER_DETAILS(items, player) is bounded by the SAME seam as SM_INVENTORY_ADD_ITEM: the ctor reads ONLY
    /// player.GetObjectId() (a single AionObject scalar) + items.Count; WriteImpl writes targetObjId + the constant 11 +
    /// itemSize, then per item: WriteD(0) + template.GetTemplateId() + template.GetL10n() + GetFullBlob(player, item).WriteMe().
    /// The player is passed to GetFullBlob ONLY as the blob owner (never dereferenced for these deterministic items). So no
    /// live Player/Legion/appearance/equipment-iteration is needed: the live Player is allocated uninitialized (the
    /// established harness precedent) with ONLY the final AionObject._objectId pinned. The items reuse the seam's exact
    /// builders so the per-item bytes are byte-identical to the weapon/armor fixtures. Java is the oracle.
    /// </summary>
    [Theory]
    [InlineData("SM_VIEW_PLAYER_DETAILS.json")]
    public void CsharpViewPlayerDetailsMatchesJavaGoldenFixture(string fixtureFile)
    {
        using var fixture = LoadFixture(fixtureFile);
        foreach (var caseElement in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var caseName = caseElement.GetProperty("name").GetString()!;
            var expectedHex = caseElement.GetProperty("payloadHex").GetString()!;
            var inputs = caseElement.GetProperty("inputs");

            Assert.Equal(ViewDetailsPlayerObjectId, inputs.GetProperty("playerObjectId").GetInt32());

            var player = NewUninitializedPlayer(ViewDetailsPlayerObjectId);
            // Two-item view reusing the seam's exact builders (weapon 1H sword + plate-torso armor, undyed).
            var items = new List<Item>
            {
                BuildEquippableWeapon(EqItemObjectId, EqItemTemplateId, EqItemMask, EqItemDescL10n, EqItemCount, EqItemCreator),
                BuildEquippableVariant(EqArmorObjectId, EqArmorTemplateId, EqArmorMask, EqArmorDescL10n, "Armorsmith",
                    ItemGroup.PL_TORSO, null),
            };
            var packet = new SM_VIEW_PLAYER_DETAILS(items, player);

            var actualHex = Convert.ToHexString(CaptureWriteImplPayload(packet));
            Assert.True(expectedHex == actualHex,
                $"SM_VIEW_PLAYER_DETAILS/{caseName}: C# payload diverged from Java golden.\n" +
                $"  Java : {expectedHex}\n  C#   : {actualHex}\n" +
                $"  firstDiffByte: {FirstDiffByte(expectedHex, actualHex)}");
        }
    }

    /// <summary>
    /// Allocate a Player WITHOUT running any constructor (RuntimeHelpers.GetUninitializedObject — the established harness
    /// precedent, mirroring the Java Unsafe.allocateInstance side), then pin only the final AionObject._objectId.
    /// SM_VIEW_PLAYER_DETAILS reads nothing else from the live Player (GetFullBlob only stashes it as blob owner).
    /// </summary>
    private static Player NewUninitializedPlayer(int objectId)
    {
        var player = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
        var idField = typeof(AionObject).GetField("_objectId", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(AionObject).FullName, "_objectId");
        idField.SetValue(player, objectId);
        return player;
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
        // rating NORMAL -> GetCongenitalSeeState() == Normal (id 0); needed by Npc.GetSeeState() (SM_PLAYER_STATE),
        // harmless to SM_NPC_INFO (reads GetVisualState() only). Mirrors the Java side.
        SetField(t, "rating", NpcRating.NORMAL);
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
        // SM_SELL_ITEM seam: TRADE_LIST_DATA carries one purchase template under the npc id (== Java side).
        SetAutoProperty(staticData, nameof(StaticData.TradeListDataDh), BuildTradeListData());
        // SM_INVENTORY_UPDATE_ITEM seam: ITEM_CLEAN_UP with an empty (non-null) cleanup list -> GENERAL_INFO's
        // HasAccountOrLegionWhStorabilityDisabled streams an empty list -> false (mirrors the Java empty-bplist seam).
        SetAutoProperty(staticData, nameof(StaticData.ItemRestrictionCleanupDataDh), BuildItemCleanupData());
        // SM_INVENTORY_ADD_ITEM_SUBOBJECT seam: empty (non-null) ItemData so the ManaStone ctor's
        // DataManager.ITEM_DATA.GetItemTemplate(itemId) returns null gracefully (mirrors the Java empty ItemData seam).
        SetAutoProperty(staticData, nameof(StaticData.ItemDataDh), new ItemData());

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

    /// <summary>
    /// Build a TradeListData carrying one purchase template (npcId == NpcInfoNpcId, NORMAL type, SellBuyPriceRate, two
    /// trade tabs) in its private npcPurchaseTemplateData index — structurally == the Java side. The tradelist/trade-in
    /// indices stay empty (so CanSell/CanBuy with no talkInfo are false regardless).
    /// </summary>
    private static TradeListData BuildTradeListData()
    {
        var t = new TradeListTemplate();
        SetField(t, "npcId", NpcInfoNpcId);
        SetField(t, "tradeNpcType", TradeNpcType.NORMAL);
        SetField(t, "buyPriceRate", SellBuyPriceRate);
        var tabs = new List<TradeListTemplate.TradeTab>();
        foreach (var id in new[] { 7, 8 })
        {
            var tab = new TradeListTemplate.TradeTab();
            SetField(tab, "id", id);
            tabs.Add(tab);
        }
        SetField(t, "tradeTablist", tabs);

        var holder = new TradeListData();
        PutPrivateMapEntry(holder, "npcPurchaseTemplateData", NpcInfoNpcId, t);
        return holder;
    }

    /// <summary>Build an ItemRestrictionCleanupData with an empty (non-null) cleanup list (structurally == Java side).</summary>
    private static ItemRestrictionCleanupData BuildItemCleanupData()
    {
        return new ItemRestrictionCleanupData { bplist = new List<ItemCleanupTemplate>() };
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
