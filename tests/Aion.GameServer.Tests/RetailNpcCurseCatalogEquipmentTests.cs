using System.Reflection;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Stats.Listeners;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcCurseCatalogEquipmentTests
{
    private sealed class UnknownStatOwner : IStatOwner { }
    private sealed class TemporaryEnchantTable : IDisposable
    {
        private readonly StaticData data;
        private readonly FieldInfo field;
        private readonly object? previous;
        internal TemporaryEnchantTable(string path)
        {
            var root=XDocument.Load(path).Root!;
            var groups=root.Elements("enchant_list").Select(group=>new EnchantGroupSummary(
                (string)group.Attribute("item_group")!,group.Elements("enchant_data").Select(level=>new EnchantLevelSummary(
                    (int)level.Attribute("level")!,level.Elements("enchant_stat").Select(stat=>new EnchantStatSummary(
                        (string)stat.Attribute("stat")!,(int)stat.Attribute("value")!)).ToArray())).ToArray())).ToArray();
            data=DataManager.GetRegisteredInstance()!.StaticData;
            field=typeof(StaticData).GetField("<EnchantTemplates>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic)!;
            previous=field.GetValue(data);field.SetValue(data,new EnchantTable(groups));
        }
        public void Dispose()=>field.SetValue(data,previous);
    }
    private sealed class TemporaryItemTable : IDisposable
    {
        private readonly StaticData data=DataManager.GetRegisteredInstance()!.StaticData;
        private readonly PropertyInfo property=typeof(StaticData).GetProperty(nameof(StaticData.ItemDataDh))!;
        private readonly object? previous;
        internal TemporaryItemTable(params ItemTemplate[] templates)
        {
            previous=property.GetValue(data);
            var items=new ItemData {its=[..templates]};items.AfterUnmarshal(null!);
            property.SetValue(data,items);
        }
        public void Dispose()=>property.SetValue(data,previous);
    }
    [Theory]
    [InlineData(1L,1L,false,true,0x10)]
    [InlineData(2L,2L,false,true,0x8)]
    [InlineData(8L,8L,false,true,0x20)]
    [InlineData(65536L,65536L,false,true,0x20)]
    [InlineData(1L,3L,true,true,0x100)]
    [InlineData(1L,3L,false,false,0)]
    [InlineData(2L,3L,true,false,0)]
    [InlineData(3L,3L,false,false,0)]
    [InlineData(131072L,131072L,false,false,0)]
    public void ReviewedWearSlotsSelectNativeBonusFlags(long occupied,long claimed,bool twoHanded,bool accepted,int expected)
    {
        Assert.Equal(accepted,RetailNpcCurseEquipmentObservation.TryOrdinaryWearBonusFlags(occupied,claimed,twoHanded,out int flags));
        Assert.Equal(expected,flags);
    }

    [SkippableFact]
    public void OriginalPackageDoesNotInventEquipmentContexts()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash),"Requires original retail package");
        using var active=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(path!),hash!,Path.GetDirectoryName(path!)!);
        Assert.Null(RetailNpcStatCatalog.CurseClassPermitSource);
        Assert.Null(RetailNpcStatCatalog.CurseArmorSource);
    }

    [SkippableTheory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    public void CatalogSourcesBuildCurseFromActualEquippedArmorAndHostMaxima(int equipmentCase)
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_EQUIPMENT_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_EQUIPMENT_SHA256");
        string? itemXml=Environment.GetEnvironmentVariable("AION_TEST_ITEM_XML");
        string? itemSets=Environment.GetEnvironmentVariable("AION_TEST_ITEMSET_XML");
        string? enchantXml=Environment.GetEnvironmentVariable("AION_TEST_ENCHANT_XML");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash)||string.IsNullOrEmpty(itemXml)||string.IsNullOrEmpty(itemSets),"Requires candidate equipment package and host item data");
        Skip.If(equipmentCase is 4 or 5 &&string.IsNullOrEmpty(enchantXml),"Requires host enchant data");
        byte[] bytes=File.ReadAllBytes(path!);using var manifest=JsonDocument.Parse(bytes);
        var source=manifest.RootElement.GetProperty("skill_sources").EnumerateArray().Single(x=>x.GetProperty("id").GetInt32()==16596);
        var candidate=RetailNpcVampirePrimaryProjection.ReadForOfflineFixture(16596,File.ReadAllBytes(source.GetProperty("path").GetString()!));
        using var active=RetailNpcStatCatalog.InstallForTests(bytes,hash!,Path.GetDirectoryName(path!)!);
        Assert.NotNull(RetailNpcStatCatalog.CurseClassPermitSource);
        Assert.NotNull(RetailNpcStatCatalog.CurseArmorSource);
        Assert.NotNull(RetailNpcStatCatalog.CurseOffhandSource);
        Assert.NotNull(RetailNpcStatCatalog.CurseWeaponHpSource);
        Assert.NotNull(RetailNpcStatCatalog.CurseOtherEquipmentHpSource);
        Assert.NotNull(RetailNpcStatCatalog.CurseAuthorizationHpSource);
        Assert.NotNull(RetailNpcStatCatalog.CurseItemBonusSource);
        using var h=BossAiHarness.For(220050000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        using var enchantTable=equipmentCase is 4 or 5?new TemporaryEnchantTable(enchantXml!):null;
        // This item has no set in the supplied host data. The offline harness
        // does not initialize the item-set holder, so provide the empty one.
        int armorId=equipmentCase==6?110101562:110100420;
        Assert.DoesNotContain(armorId.ToString(),File.ReadAllText(itemSets!));
        var emptySets=new ItemSetData {itemsetList=[]};emptySets.AfterUnmarshal(null!);
        typeof(StaticData).GetProperty(nameof(StaticData.ItemSetDataDh))!
            .SetValue(DataManager.GetRegisteredInstance()!.StaticData,emptySets);
        var npc=h.SpawnWithAi(214435,"retail_stat_base_fixture");var player=RetailNpcStatPeriodicTests.Player(h,220050000);
        player.GetCommonData().SetPlayerClass(equipmentCase==6?PlayerClass.SORCERER:PlayerClass.GLADIATOR);
        player.GetCommonData().SetLevel(equipmentCase>=11?npc.GetLevel():65);
        player.GetGameStats().UpdateStatsTemplate();PlayerStatFunctions.AddPredefinedStatFunctions(player);player.GetLifeStats().OnStatsChange(null!);
        int oldMpWithoutItem=player.GetLifeStats().GetMaxMp(),oldHpWithoutItem=player.GetLifeStats().GetMaxHp();
        ItemTemplate? template=null;
        using(var reader=XmlReader.Create(itemXml!,new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null}))
        {
            while(reader.Read())
                if(reader.NodeType==XmlNodeType.Element &&reader.Name=="item_template" &&reader.GetAttribute("id")==armorId.ToString())
                {
                    using var subtree=reader.ReadSubtree();
                    template=(ItemTemplate?)new XmlSerializer(typeof(ItemTemplate),new XmlRootAttribute("item_template")).Deserialize(subtree);
                    break;
                }
        }
        Assert.NotNull(template);template.AfterUnmarshal(new ItemData());
        Assert.Equal(equipmentCase==6?"rb_torso_n_l1_pc_65a":"rb_torso_n_u1_p_33a",template.GetRetailSourceName(),ignoreCase:true);
        long slot=ItemSlot.TORSO.GetSlotIdMask();var item=new Item(900001,template,1,true,slot);
        if(equipmentCase is 4 or 5)item.SetEnchantLevel(15);
        var slots=Assert.IsType<SortedDictionary<long,Item>>(typeof(Equipment).GetField("equipment",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(player.GetEquipment()));
        slots.Add(slot,item);ItemEquipmentListener.OnItemEquipment(item,player);player.GetLifeStats().OnStatsChange(null!);
        int hostHp=player.GetLifeStats().GetMaxHp(),hostMp=player.GetLifeStats().GetMaxMp();Assert.True(hostMp>oldMpWithoutItem);
        if(equipmentCase is 4 or 5)Assert.Equal(210,hostHp-oldHpWithoutItem);
        if(equipmentCase==6)Assert.Equal(232,hostHp-oldHpWithoutItem);
        player.GetLifeStats().SetCurrentHp(hostHp);player.GetLifeStats().SetCurrentMp(hostMp);
        npc.GetLifeStats().SetCurrentHp(100);npc.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_ACCURACY,10000)]);
        if(equipmentCase>=11)npc.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.CURSE_RESISTANCE_PENETRATION,1000),
            new StatSetFunction(StatEnum.MAGICAL_CRITICAL,0)]);
        var snapshot=RetailNpcCurseEquipmentObservation.Capture(player);Assert.Single(snapshot.Items);
        Assert.Equal(RetailNpcCurseEquipmentObservation.Status.ReviewedInputsOnly,snapshot.Items[0].Status);
        Assert.True(RetailNpcCurseEquipmentObservation.TryOrdinaryWearBonusFlags(snapshot.Items[0].OccupiedSlot,
            snapshot.Items[0].HostSlot,false,out int normalFlags));
        Assert.Equal(0x20,normalFlags);
        var normal=new RetailNpcCurseItemBonusSource.RevisionContext(0,true,1,false,normalFlags);
        var itemContext=new RetailNpcCurseEquipmentObservation.ArmorProjectionContext(0,null,false,false,normal,normal with {BonusFlags=0x200});
        var row=Assert.Single(snapshot.Items);var permits=RetailNpcStatCatalog.CurseClassPermitSource!;
        if(equipmentCase==6)
        {
            Assert.False(RetailNpcCurseResourcePlanBuilder.IsInvariantFlatArmor(row,snapshot.NativeClass,permits));
            Assert.True(RetailNpcCurseResourcePlanBuilder.IsOrdinaryArmor(row,snapshot.NativeClass,65,permits));
            Assert.False(RetailNpcCurseResourcePlanBuilder.IsOrdinaryArmor(row,snapshot.NativeClass,64,permits));
            Assert.False(RetailNpcCurseResourcePlanBuilder.IsOrdinaryArmor(row,2,65,permits));
        }
        else Assert.True(RetailNpcCurseResourcePlanBuilder.IsInvariantFlatArmor(row,snapshot.NativeClass,permits));
        Assert.False(RetailNpcCurseResourcePlanBuilder.IsInvariantFlatArmor(row with {Source=row.Source! with {RawMaxHp=1}},snapshot.NativeClass,permits));
        Assert.False(RetailNpcCurseResourcePlanBuilder.IsInvariantFlatArmor(row with {ItemId=row.ItemId+1},snapshot.NativeClass,permits));
        var percentBonus=row.ItemBonus! with {Hp=row.ItemBonus!.Hp with {
            Values=ImmutableArray.Create<RetailNpcCurseItemBonusSource.Operand?>(new(1,true,"bonus_attr1"),null,null)}};
        Assert.False(RetailNpcCurseResourcePlanBuilder.IsInvariantFlatArmor(row with {ItemBonus=percentBonus},snapshot.NativeClass,permits));
        Assert.False(RetailNpcCurseResourcePlanBuilder.IsOrdinaryArmor(row with {ItemBonus=percentBonus},snapshot.NativeClass,65,permits));
        var absentValues=row.ItemBonus! with {Hp=row.ItemBonus!.Hp with {Values=default}};
        Assert.False(RetailNpcCurseResourcePlanBuilder.IsInvariantFlatArmor(row with {ItemBonus=absentValues},snapshot.NativeClass,permits));
        Assert.False(RetailNpcCurseResourcePlanBuilder.IsOrdinaryArmor(row with {ItemBonus=absentValues},snapshot.NativeClass,65,permits));
        Assert.True(RetailNpcCurseEquipmentObservation.TryProjectEquipment(snapshot,RetailNpcStatCatalog.CurseClassPermitSource,
            _=>itemContext,out var equipment,out var equipmentFailure,out _));
        Assert.Equal(RetailNpcCurseEquipmentObservation.ProjectionFailure.None,equipmentFailure);
        Assert.Equal(equipmentCase==6?42:equipmentCase is 4 or 5?360:0,equipment.RevisedArmorHp);
        Assert.Equal(equipmentCase==6?758:572,equipment.MpBonus.Points);
        void Equip(int id,long occupied,bool twoHanded=false,int enchant=0)
        {
            Assert.DoesNotContain(id.ToString(),File.ReadAllText(itemSets!));
            ItemTemplate? selectedTemplate=null;
            using(var reader=XmlReader.Create(itemXml!,new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null}))
            {
                while(reader.Read())
                    if(reader.NodeType==XmlNodeType.Element &&reader.Name=="item_template" &&reader.GetAttribute("id")==id.ToString())
                    {
                        using var subtree=reader.ReadSubtree();
                        selectedTemplate=(ItemTemplate?)new XmlSerializer(typeof(ItemTemplate),new XmlRootAttribute("item_template")).Deserialize(subtree);
                        break;
                    }
            }
            Assert.NotNull(selectedTemplate);selectedTemplate.AfterUnmarshal(new ItemData());
            long sub=ItemSlot.SUB_HAND.GetSlotIdMask();long mask=twoHanded?occupied|sub:occupied;
            var selectedItem=new Item(910000+id%100000,selectedTemplate,1,true,mask);
            if(enchant>0)selectedItem.SetEnchantLevel(enchant);
            slots.Add(occupied,selectedItem);if(twoHanded)slots.Add(sub,selectedItem);
            ItemEquipmentListener.OnItemEquipment(selectedItem,player);
        }
        if(equipmentCase==1)
        {
            Equip(100000094,ItemSlot.MAIN_HAND.GetSlotIdMask());
            Equip(115000011,ItemSlot.SUB_HAND.GetSlotIdMask());
            Equip(122000010,ItemSlot.RING_RIGHT.GetSlotIdMask());
        }
        else if(equipmentCase==2)
        {
            Equip(100900038,ItemSlot.MAIN_HAND.GetSlotIdMask(),true);
            Equip(122000010,ItemSlot.RING_RIGHT.GetSlotIdMask());
        }
        else if(equipmentCase==3)
        {
            Equip(100000094,ItemSlot.MAIN_HAND.GetSlotIdMask());
            Equip(100000095,ItemSlot.SUB_HAND.GetSlotIdMask());
            Equip(122000010,ItemSlot.RING_RIGHT.GetSlotIdMask());
        }
        else if(equipmentCase==5)
        {
            Equip(100000094,ItemSlot.MAIN_HAND.GetSlotIdMask(),enchant:10);
            Equip(115000011,ItemSlot.SUB_HAND.GetSlotIdMask(),enchant:10);
            Equip(122000010,ItemSlot.RING_RIGHT.GetSlotIdMask());
        }
        ManaStone? socketed=null;TemporaryItemTable? stoneTable=null;
        if(equipmentCase is 7 or 8 or 9 or 10 or 11 or 12)
        {
            Assert.NotNull(RetailNpcStatCatalog.CurseManastoneSource);
            int stoneId=equipmentCase==7?167000226:equipmentCase==8?167000227:
                equipmentCase==11?167000578:equipmentCase==12?167000230:167000779;
            ItemTemplate? stoneTemplate=null;
            using(var reader=XmlReader.Create(itemXml!,new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null}))
            {
                while(reader.Read())
                    if(reader.NodeType==XmlNodeType.Element &&reader.Name=="item_template" &&reader.GetAttribute("id")==stoneId.ToString())
                    {
                        using var subtree=reader.ReadSubtree();
                        stoneTemplate=(ItemTemplate?)new XmlSerializer(typeof(ItemTemplate),new XmlRootAttribute("item_template")).Deserialize(subtree);
                        break;
                    }
            }
            Assert.NotNull(stoneTemplate);stoneTable=new TemporaryItemTable(template,stoneTemplate);
            socketed=new ManaStone(item.GetObjectId(),stoneId,0,IPersistable.PersistentState.NEW);
            Assert.True(item.GetItemStones().Add(socketed));
            Assert.True(RetailNpcStatCatalog.CurseManastoneSource!.Matches(socketed,item.GetObjectId()));
            Assert.False(RetailNpcStatCatalog.CurseManastoneSource.Matches(socketed,item.GetObjectId()+1));
            if(equipmentCase>=11)
            {
                var changed=socketed.GetModifiers()[0];int original=changed.Value;changed.Value++;
                Assert.False(RetailNpcStatCatalog.CurseManastoneSource.Matches(socketed,item.GetObjectId()));changed.Value=original;
            }
            if(equipmentCase==11)
            {
                var spec=manifest.RootElement.GetProperty("curse_manastone_context");
                var malformed=JsonNode.Parse(File.ReadAllText(spec.GetProperty("path").GetString()!))!.AsObject();
                var attributes=malformed["items"]!.AsArray()[0]!["attributes"]!.AsArray();
                attributes.Add(attributes[0]!.DeepClone());
                byte[] duplicate=System.Text.Encoding.UTF8.GetBytes(malformed.ToJsonString());
                Assert.Throws<InvalidDataException>(()=>RetailNpcCurseManastoneSource.Parse(duplicate,
                    Convert.ToHexStringLower(SHA256.HashData(duplicate))));
                attributes.RemoveAt(attributes.Count-1);
                attributes.Add(new JsonObject { ["stat"]="MAGICAL_DEFEND",["value"]=10 });
                byte[] unsupported=System.Text.Encoding.UTF8.GetBytes(malformed.ToJsonString());
                Assert.Throws<InvalidDataException>(()=>RetailNpcCurseManastoneSource.Parse(unsupported,
                    Convert.ToHexStringLower(SHA256.HashData(unsupported))));
            }
            ItemEquipmentListener.AddStoneStats(item,socketed,player.GetGameStats());
        }
        using var stoneTableLifetime=stoneTable;
        player.GetLifeStats().OnStatsChange(null!);
        if(equipmentCase==7)Assert.Equal(20,player.GetLifeStats().GetMaxHp()-hostHp);
        if(equipmentCase==8)Assert.Equal(20,player.GetLifeStats().GetMaxMp()-hostMp);
        if(equipmentCase is 9 or 10)
        {
            Assert.Equal(75,player.GetLifeStats().GetMaxHp()-hostHp);
            Assert.Equal(37,player.GetLifeStats().GetMaxMp()-hostMp);
        }
        if(equipmentCase==11)Assert.Equal(32,player.GetLifeStats().GetMaxHp()-hostHp);
        if(equipmentCase==12)Assert.Equal(hostHp,player.GetLifeStats().GetMaxHp());
        hostHp=player.GetLifeStats().GetMaxHp();hostMp=player.GetLifeStats().GetMaxMp();
        player.GetLifeStats().SetCurrentHp(hostHp);player.GetLifeStats().SetCurrentMp(hostMp);
        var equipped=RetailNpcCurseEquipmentObservation.Capture(player);
        Assert.Equal(equipmentCase is 0 or 4 or 6 or 7 or 8 or 9 or 10 or 11 or 12?1:4,equipped.Items.Length);
        Assert.True(RetailNpcCursePlayerPrimarySnapshot.TryCaptureRetailInitialForUnmodifiedHost(player,out var primary));Assert.NotNull(primary);
        BossAiHarness.MakeMutuallyKnown(npc,player);
        var skill=Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(npc,16596,38,player);Assert.NotNull(skill);
        skill.GetEffectedList().Add(player);
        Assert.True(RetailNpcStatCatalog.TryGetSkillTiming(npc,16596,38,out var timing));Assert.Equal((ushort)491,timing.Word);
        RetailNpcCurseResourceOwner.Plan? plan=null;
        RetailNpcCurseResourceOwner.Plan? Build(Player target,Effect effect)
        {
            if(equipmentCase is 4 or 5)
            {
                var enchant=item.GetEnchantEffect();Assert.NotNull(enchant);item.SetEnchantEffect(null!);
                Assert.False(RetailNpcCurseResourcePlanBuilder.TryBuildFlatEquipmentFromCatalog(target,effect,out _,out var stale));
                Assert.Equal(RetailNpcCurseResourcePlanBuilder.Failure.ModifierInputsUnavailable,stale);
                item.SetEnchantEffect(enchant);
                item.SetEnchantLevel(14); // The attached host effect is still the +15 effect.
                Assert.False(RetailNpcCurseResourcePlanBuilder.TryBuildFlatEquipmentFromCatalog(target,effect,out _,out var mismatch));
                Assert.Equal(RetailNpcCurseResourcePlanBuilder.Failure.ModifierInputsUnavailable,mismatch);
                item.SetEnchantLevel(15);
            }
            if(equipmentCase==5)
            {
                foreach(long hand in new[]{ItemSlot.MAIN_HAND.GetSlotIdMask(),ItemSlot.SUB_HAND.GetSlotIdMask()})
                {
                    var equippedItem=slots[hand];var enchant=equippedItem.GetEnchantEffect();Assert.NotNull(enchant);
                    equippedItem.SetEnchantEffect(null!);
                    Assert.False(RetailNpcCurseResourcePlanBuilder.TryBuildFlatEquipmentFromCatalog(target,effect,out _,out var missing));
                    Assert.Equal(RetailNpcCurseResourcePlanBuilder.Failure.ModifierInputsUnavailable,missing);
                    equippedItem.SetEnchantEffect(enchant);
                }
            }
            if(equipmentCase is 2 or 3)
            {
                long removedSlot=equipmentCase==2?ItemSlot.SUB_HAND.GetSlotIdMask():ItemSlot.MAIN_HAND.GetSlotIdMask();
                var removed=slots[removedSlot];slots.Remove(removedSlot);
                Assert.False(RetailNpcCurseResourcePlanBuilder.TryBuildFlatEquipmentFromCatalog(target,effect,out _,out var missing));
                Assert.Equal(RetailNpcCurseResourcePlanBuilder.Failure.EquipmentUnsupported,missing);
                slots.Add(removedSlot,removed);
            }
            if(socketed is not null)
            {
                socketed.SetSlot(5);
                Assert.False(RetailNpcCurseResourcePlanBuilder.TryBuildFlatEquipmentFromCatalog(target,effect,out _,out var wrongSlot));
                Assert.Equal(RetailNpcCurseResourcePlanBuilder.Failure.EquipmentUnsupported,wrongSlot);
                socketed.SetSlot(0);
                if(equipmentCase==9)
                {
                    var duplicate=new ManaStone(item.GetObjectId(),socketed.GetItemId(),1,IPersistable.PersistentState.NEW);
                    Assert.True(item.GetItemStones().Add(duplicate));duplicate.SetSlot(0);
                    Assert.False(RetailNpcCurseResourcePlanBuilder.TryBuildFlatEquipmentFromCatalog(target,effect,out _,out var duplicateSlot));
                    Assert.Equal(RetailNpcCurseResourcePlanBuilder.Failure.EquipmentUnsupported,duplicateSlot);
                    duplicate.SetSlot(1);Assert.True(item.GetItemStones().Remove(duplicate));
                }
                var changed=socketed.GetModifiers()[0];int originalValue=changed.Value;changed.Value++;
                Assert.False(RetailNpcCurseResourcePlanBuilder.TryBuildFlatEquipmentFromCatalog(target,effect,out _,out var alteredSource));
                Assert.Equal(RetailNpcCurseResourcePlanBuilder.Failure.EquipmentUnsupported,alteredSource);
                changed.Value=originalValue;
                target.GetGameStats().EndEffect(socketed);
                Assert.False(RetailNpcCurseResourcePlanBuilder.TryBuildFlatEquipmentFromCatalog(target,effect,out _,out var missingOwner));
                Assert.Equal(RetailNpcCurseResourcePlanBuilder.Failure.ModifierInputsUnavailable,missingOwner);
                ItemEquipmentListener.AddStoneStats(item,socketed,target.GetGameStats());
                target.GetLifeStats().OnStatsChange(null!);
            }
            var unknown=new UnknownStatOwner();
            target.GetGameStats().AddEffectOnly(unknown,[new StatRateFunction(StatEnum.MAXHP,10,true)]);
            Assert.False(RetailNpcCurseResourcePlanBuilder.TryBuildFlatEquipmentFromCatalog(target,effect,out _,out var denied));
            Assert.Equal(RetailNpcCurseResourcePlanBuilder.Failure.ModifierInputsUnavailable,denied);
            target.GetGameStats().EndEffect(unknown);
            Assert.True(RetailNpcCurseResourcePlanBuilder.TryBuildFlatEquipmentFromCatalog(target,effect,out plan,out var failure),failure.ToString());
            Assert.Equal(RetailNpcCurseResourcePlanBuilder.Failure.None,failure);
            if(socketed is not null)
            {
                Assert.True(plan!.SnapshotCurrent(target));
                socketed.SetSlot(1);
                Assert.False(plan.SnapshotCurrent(target));
                socketed.SetSlot(0);
                Assert.True(plan.SnapshotCurrent(target));
            }
            return plan;
        }
        RetailNpcVampirePlayerInputs? joined=null;
        if(equipmentCase==10)
        {
            var sources=Assert.IsType<RetailNpcVampirePlayerInputs.Sources>(RetailNpcStatCatalog.VampirePlayerSources);
            var otherSources=sources with { };
            Assert.NotSame(sources,otherSources);
            Assert.Null(new RetailNpcVampirePlayerInputs(otherSources).Capture(player));
            using var partial=JsonDocument.Parse("{\"absolute\":{},\"items\":{},\"buffs\":{}}");
            Assert.Throws<KeyNotFoundException>(()=>RetailNpcStatCatalog.LoadVampirePlayerSources(partial.RootElement,Path.GetDirectoryName(path!)!));
            var corrupted=JsonNode.Parse(manifest.RootElement.GetProperty("vampire_player_input_context").GetRawText())!.AsObject();
            corrupted["absolute"]!["sha256"]=new string('0',64);
            using var badHash=JsonDocument.Parse(corrupted.ToJsonString());
            Assert.Throws<InvalidDataException>(()=>RetailNpcStatCatalog.LoadVampirePlayerSources(badHash.RootElement,Path.GetDirectoryName(path!)!));
            joined=new(sources);
            RetailNpcVampireCastFixture.AttachReviewedInputsForOfflineFixture(skill,candidate,joined,(_,hi)=>hi);
        }
        else if(equipmentCase<11)RetailNpcVampireCastFixture.AttachFromCatalogForOfflineFixture(skill,candidate,Build,(_,hi)=>hi);
        int castMs=(int)MathF.Floor(skill.GetSkillTemplate().GetDuration()*(npc.GetGameStats().GetCastSpeed()/1000f)+0.5f);
        Assert.True(skill.UseWithoutPropSkill());Assert.Equal(491,skill.GetHitTime());
        if(equipmentCase>=11)Assert.True(RetailNpcVampireCastFixture.IsSelectedForOfflineFixture(skill));
        h.Clock.Advance(TimeSpan.FromMilliseconds(castMs));var effect=RetailNpcVampireCastFixture.ObservedEffectForOfflineFixture(skill);Assert.NotNull(effect);
        Assert.True(effect.IsInSuccessEffects(2));int damage=effect.GetReserveds(1).GetValue();
        if(joined is not null)Assert.Throws<InvalidOperationException>(()=>joined.Capture(player));
        Assert.InRange(damage,1,player.GetLifeStats().GetCurrentHp()-1);
        h.Clock.Advance(TimeSpan.FromMilliseconds(441));Assert.Equal(hostHp-damage,player.GetLifeStats().GetCurrentHp());
        if(joined is not null)
        {
            Assert.NotNull(socketed);socketed.SetSlot(5);
            Assert.Null(joined.Build(player,effect));socketed.SetSlot(0);
            var unsupported=new UnknownStatOwner();
            player.GetGameStats().AddEffectOnly(unsupported,[new StatAddFunction(StatEnum.CURSE_RESISTANCE,1,true)]);
            Assert.Null(joined.Build(player,effect));player.GetGameStats().EndEffect(unsupported);
        }
        h.Clock.Advance(TimeSpan.FromMilliseconds(50));
        if(joined is not null)plan=joined.PreparedPlan;
        int curseHp,curseMp;
        if(equipmentCase>=11)
        {
            curseHp=RetailNpcCurseStatMath.Maximum(RetailNpcCurseStatMath.Resource.Hp,primary.HpBasis+equipment.RevisedArmorHp,hostHp,-35);
            curseMp=RetailNpcCurseStatMath.Maximum(RetailNpcCurseStatMath.Resource.Mp,primary.MpBasis,hostMp,-35);
        }
        else
        {
            Assert.NotNull(plan);
            Assert.Equal(primary.HpBasis+equipment.RevisedArmorHp,plan.HpBasis);
            Assert.Equal(primary.MpBasis,plan.MpBasis);
            Assert.Equal(hostHp,RetailNpcCurseStatMath.Maximum(RetailNpcCurseStatMath.Resource.Hp,plan.HpBasis,plan.HpUnscaled,plan.HpOtherPercent));
            Assert.Equal(hostMp,RetailNpcCurseStatMath.Maximum(RetailNpcCurseStatMath.Resource.Mp,plan.MpBasis,plan.MpUnscaled,plan.MpOtherPercent));
            curseHp=RetailNpcCurseStatMath.Maximum(RetailNpcCurseStatMath.Resource.Hp,plan.HpBasis,plan.HpUnscaled,plan.HpOtherPercent-35);
            curseMp=RetailNpcCurseStatMath.Maximum(RetailNpcCurseStatMath.Resource.Mp,plan.MpBasis,plan.MpUnscaled,plan.MpOtherPercent-35);
        }
        Assert.Equal(curseHp,player.GetLifeStats().GetMaxHp());Assert.Equal(curseMp,player.GetLifeStats().GetMaxMp());
        effect.EndEffect();Assert.Equal(hostHp,player.GetLifeStats().GetMaxHp());Assert.Equal(hostMp,player.GetLifeStats().GetMaxMp());
        h.Clock.Advance(TimeSpan.FromMilliseconds(1000));
        Assert.Equal(Math.Min(npc.GetLifeStats().GetMaxHp(),100+RetailNpcStatSpellDrain.HealAmount(damage,100)),npc.GetLifeStats().GetCurrentHp());
        Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void RehashedUnreviewedPermitCatalogFailsClosed()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_EQUIPMENT_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_EQUIPMENT_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash),"Requires candidate equipment package");
        byte[] raw=File.ReadAllBytes(path!);Assert.Equal(hash,Convert.ToHexStringLower(SHA256.HashData(raw)));
        var node=JsonNode.Parse(raw)!;byte[] altered=System.Text.Encoding.UTF8.GetBytes("{}");string temp=Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(temp,altered);node["curse_class_permit_context"]!["path"]=temp;
            node["curse_class_permit_context"]!["sha256"]=Convert.ToHexStringLower(SHA256.HashData(altered));
            byte[] changed=System.Text.Encoding.UTF8.GetBytes(node.ToJsonString());
            Assert.Throws<InvalidDataException>(()=>RetailNpcStatCatalog.InstallForTests(changed,
                Convert.ToHexStringLower(SHA256.HashData(changed)),Path.GetDirectoryName(path!)!));
        }
        finally {File.Delete(temp);}
    }
}
