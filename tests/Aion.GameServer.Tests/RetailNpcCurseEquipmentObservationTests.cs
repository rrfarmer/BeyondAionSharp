using System.Collections.Immutable;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Items.Enums;
using Aion.GameServer.Retail;
using Observation=Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcCurseEquipmentObservation;

namespace Aion.GameServer.Tests;

public class RetailNpcCurseEquipmentObservationTests
{
    [Fact]
    public void EquipmentCaptureWaitsForNormalEquipMonitor()
    {
        var equipment=new Equipment(null!);
        using var started=new ManualResetEventSlim();
        using var finished=new ManualResetEventSlim();
        Observation.Snapshot? result=null;
        Exception? failure=null;
        var worker=new Thread(()=>
        {
            started.Set();
            try { result=Observation.Capture(equipment,1,33,null); }
            catch(Exception ex) { failure=ex; }
            finally { finished.Set(); }
        });
        lock(equipment)
        {
            worker.Start();
            Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
            Assert.False(finished.Wait(TimeSpan.FromMilliseconds(150)));
        }
        Assert.True(finished.Wait(TimeSpan.FromSeconds(5)));
        Assert.Null(failure);
        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.False(result.GameplayReady);
    }

    private static RetailNpcCurseArmorSource Source()
    {
        byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(new {
            version=2,purpose="default-armor-hp-and-class-v2",region="USA",
            provenance=new {bindings_sha256=RetailNpcCurseArmorSource.ReviewedBindingsSha,
                retail_items_sha256=RetailNpcCurseArmorSource.ReviewedRetailItemsSha,
                retail_enchant_sha256=RetailNpcCurseArmorSource.ReviewedRetailEnchantsSha,
                host_items_sha256=RetailNpcCurseArmorSource.ReviewedHostItemsSha,
                retail_items_selection="USA missing; base fallback",retail_enchant_selection="USA missing; base fallback"},
            tables=new[]{new {name="rb_torso",threshold=16,hp_levels_1_to_15=Enumerable.Range(1,15).Select(x=>x*24).ToArray()}},
            items=new[]{new {id=110100420,name="robe",table="rb_torso",raw_max_hp=0,max_hp_explicit=false,
                host_max_enchant=15,retail_max_enchant=(int?)15,
                class_permits=Enumerable.Range(0,17).Select(x=>x==1?33:0).ToArray(),class_present_mask=2}}
        });
        return RetailNpcCurseArmorSource.Parse(bytes,Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }

    [Fact]
    public void ItemTemplateRetainsSourceNameWithoutChangingVisibleName()
    {
        var serializer=new XmlSerializer(typeof(ItemTemplate),new XmlRootAttribute("item_template"));
        using var input=new StringReader("<item_template id=\"110100420\" name=\"Visible robe\" cName=\"rb_torso_n_u1_p_33a\" />");
        var template=Assert.IsType<ItemTemplate>(serializer.Deserialize(input));
        Assert.Equal("Visible robe",template.GetName());Assert.Equal("rb_torso_n_u1_p_33a",template.GetRetailSourceName());
        using var missing=new StringReader("<item_template id=\"110100420\" name=\"Visible robe\" />");
        Assert.Null(Assert.IsType<ItemTemplate>(serializer.Deserialize(missing)).GetRetailSourceName());
    }

    [SkippableFact]
    public void ActualHostItemXmlRetainsTheReviewedIdentity()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_HOST_ITEMS");
        Skip.If(string.IsNullOrEmpty(path),"Requires the reviewed host item source");
        string Hash(){using var stream=File.OpenRead(path!);return Convert.ToHexStringLower(SHA256.HashData(stream));}
        Assert.Equal(RetailNpcCurseArmorSource.ReviewedHostItemsSha,Hash());
        using var reader=XmlReader.Create(path!,new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null});
        XElement? selected=null;
        while(reader.Read())
            if(reader.NodeType==XmlNodeType.Element&&reader.LocalName=="item_template"&&reader.GetAttribute("id")=="110100420")
            { using var subtree=reader.ReadSubtree();selected=XElement.Load(subtree);break; }
        Assert.NotNull(selected);
        var serializer=new XmlSerializer(typeof(ItemTemplate),new XmlRootAttribute("item_template"));
        using var itemReader=selected.CreateReader();var item=Assert.IsType<ItemTemplate>(serializer.Deserialize(itemReader));
        item.AfterUnmarshal(null!);
        Assert.Equal(110100420,item.GetTemplateId());Assert.Equal("rb_torso_n_u1_p_33a",item.GetRetailSourceName());
        Assert.Equal(ItemGroup.RB_TORSO,item.GetItemGroup());Assert.Equal(15,item.GetMaxEnchantLevel());
        Assert.Equal(RetailNpcCurseArmorSource.ReviewedHostItemsSha,Hash());
    }

    [Fact]
    public void EquipmentObservationRetainsKnownAndUnknownItemsWithoutClaimingReadiness()
    {
        var source=Source();var equipment=new Equipment(null!);
        var template=new ItemTemplate {itemId=110100420,name="Visible robe",retailSourceName="robe",
            itemGroup=ItemGroup.RB_TORSO,maxEnchant=15};
        var item=new Item(900001,template,1,true,ItemSlot.TORSO.GetSlotIdMask());item.SetEnchantLevel(15);
        var unknown=new Item(900002,new ItemTemplate {itemId=100000001,name="Sword",itemGroup=ItemGroup.SWORD},
            1,true,ItemSlot.MAIN_HAND.GetSlotIdMask());
        var field=typeof(Equipment).GetField("equipment",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var slots=Assert.IsType<SortedDictionary<long,Item>>(field.GetValue(equipment));
        slots.Add(ItemSlot.TORSO.GetSlotIdMask(),item);slots.Add(ItemSlot.MAIN_HAND.GetSlotIdMask(),unknown);
        var snapshot=Observation.Capture(equipment,1,33,source);
        Assert.False(snapshot.GameplayReady);Assert.Equal(2,snapshot.Items.Length);
        var armor=Assert.Single(snapshot.Items,x=>x.ItemId==110100420);
        Assert.Equal(Observation.Status.ReviewedInputsOnly,armor.Status);Assert.Equal(360,armor.Source!.EnchantMaxHp);
        Assert.Equal((byte)33,armor.RawClassPermit);Assert.True(armor.ClassPermitPresent);Assert.Equal(15,armor.EnchantLevel);
        Assert.Equal(Observation.Status.UnknownItem,Assert.Single(snapshot.Items,x=>x.ItemId==100000001).Status);
        Assert.Equal(Observation.Status.SourceUnavailable,Assert.Single(Observation.Capture(equipment,1,33,null).Items,x=>x.ItemId==110100420).Status);
        item.SetEnchantLevel(16);
        Assert.Equal(15,armor.EnchantLevel);Assert.Equal(Observation.Status.UnreviewedEnchantLevel,
            Assert.Single(Observation.Capture(equipment,1,33,source).Items,x=>x.ItemId==110100420).Status);
        item.SetEnchantLevel(15);template.retailSourceName="other";
        Assert.Equal(Observation.Status.IdentityMismatch,Assert.Single(Observation.Capture(equipment,1,33,source).Items,x=>x.ItemId==110100420).Status);
        template.retailSourceName="robe";template.maxEnchant=14;
        var drift=Assert.Single(Observation.Capture(equipment,1,33,source).Items,x=>x.ItemId==110100420);
        Assert.Equal(Observation.Status.HostTemplateDrift,drift.Status);Assert.Null(drift.Source);Assert.Null(drift.RawClassPermit);
        template.maxEnchant=15;template.enchantName="rb_torso";
        Assert.Equal(Observation.Status.HostTemplateDrift,Assert.Single(Observation.Capture(equipment,1,33,source).Items,x=>x.ItemId==110100420).Status);
        template.enchantName=null!;item.SetEquipmentSlot(ItemSlot.GLOVES.GetSlotIdMask());
        Assert.Equal(Observation.Status.SlotMismatch,Assert.Single(Observation.Capture(equipment,1,33,source).Items,x=>x.ItemId==110100420).Status);
        item.SetEquipped(false);
        Assert.Equal(Observation.Status.ItemNotEquipped,Assert.Single(Observation.Capture(equipment,1,33,source).Items,x=>x.ItemId==110100420).Status);
    }

    [SkippableFact]
    public void SelectedArmorObservationCombinesRetailBaseAuthorizationAndBonuses()
    {
        string? armorPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_ARMOR_FIXTURE");
        string? authPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_AUTHORIZATION_EXPANDED_HP_FIXTURE");
        string? bonusPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_ITEM_BONUS_FIXTURE");
        Skip.If(string.IsNullOrEmpty(armorPath)||string.IsNullOrEmpty(authPath)||string.IsNullOrEmpty(bonusPath),"Requires pinned retail item sources");
        byte[] a=File.ReadAllBytes(armorPath!),b=File.ReadAllBytes(authPath!),c=File.ReadAllBytes(bonusPath!);
        static string Hash(byte[] x)=>Convert.ToHexStringLower(SHA256.HashData(x));
        var armor=RetailNpcCurseArmorSource.Parse(a,Hash(a));
        var auth=RetailNpcCurseAuthorizationHpSource.Parse(b,Hash(b));
        var bonus=RetailNpcCurseItemBonusSource.Parse(c,Hash(c));
        var equipment=new Equipment(null!);
        var template=new ItemTemplate {itemId=110100420,retailSourceName="rb_torso_n_u1_p_33a",itemGroup=ItemGroup.RB_TORSO,maxEnchant=15};
        var item=new Item(900001,template,1,true,ItemSlot.TORSO.GetSlotIdMask());item.SetEnchantLevel(15);
        var slots=Assert.IsType<SortedDictionary<long,Item>>(typeof(Equipment).GetField("equipment",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(equipment));
        slots.Add(ItemSlot.TORSO.GetSlotIdMask(),item);
        var snapshot=Observation.Capture(equipment,1,33,armor,authorizationHp:auth,itemBonus:bonus);
        var normal=new RetailNpcCurseItemBonusSource.RevisionContext(0,true,1,false,0x20);
        var context=new Observation.ArmorProjectionContext(0,null,false,false,normal,default);
        Assert.True(Observation.TryProjectArmor(snapshot,_=>context,out var result,out var reason,out int failed));
        Assert.Equal(Observation.ProjectionFailure.None,reason);Assert.Equal(0,failed);
        Assert.Equal(1,result.ItemCount);Assert.Equal(360,result.RevisedArmorHp);
        Assert.Equal(new(0,0),result.HpBonus);Assert.Equal(new(572,0),result.MpBonus);
        Assert.False(snapshot.GameplayReady);
        var row=Assert.Single(snapshot.Items);
        Assert.False(Observation.TryProjectArmor(snapshot,_=>context with {LevelAdjustment=null},out _,out reason,out failed));
        Assert.Equal((Observation.ProjectionFailure.MissingNativeContext,110100420),(reason,failed));
        var socket=snapshot with {Items=snapshot.Items.SetItem(0,row with {HasSocketOrIdian=true})};
        Assert.False(Observation.TryProjectArmor(socket,_=>context,out _,out reason,out _));
        Assert.Equal(Observation.ProjectionFailure.UnsupportedItemState,reason);
        var missing=snapshot with {Items=snapshot.Items.SetItem(0,row with {AuthorizationHp=null})};
        Assert.False(Observation.TryProjectArmor(missing,_=>context,out _,out reason,out _));
        Assert.Equal(Observation.ProjectionFailure.MissingItemSource,reason);
        var duplicate=snapshot with {Items=snapshot.Items.Add(row)};
        Assert.False(Observation.TryProjectArmor(duplicate,_=>context,out _,out reason,out _));
        Assert.Equal(Observation.ProjectionFailure.UnsupportedItemState,reason);
        var two=snapshot with {Items=snapshot.Items.Add(row with {ObjectId=900002})};
        Assert.False(Observation.TryProjectArmor(two,x=>x.ObjectId==900001?context:context with {Normal=normal with {HighLevelFlag=true}},out _,out reason,out _));
        Assert.Equal(Observation.ProjectionFailure.MissingNativeContext,reason);
        var itemSet=context with {Normal=normal with {BonusFlags=2}};
        Assert.False(Observation.TryProjectArmor(snapshot,_=>itemSet,out _,out reason,out _));
        Assert.Equal(Observation.ProjectionFailure.InvalidBonus,reason);
    }

    [SkippableFact]
    public void SelectedMixedEquipmentUsesEachRetailPermitAndBonus()
    {
        string? armorPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_ARMOR_FIXTURE");
        string? authPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_AUTHORIZATION_EXPANDED_HP_FIXTURE");
        string? bonusPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_ITEM_BONUS_FIXTURE");
        string? permitPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_CLASS_PERMIT_FIXTURE");
        string? weaponPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_WEAPON_HP_FIXTURE");
        string? otherPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_OTHER_HP_FIXTURE");
        Skip.If(new[]{armorPath,authPath,bonusPath,permitPath,weaponPath,otherPath}.Any(string.IsNullOrEmpty),"Requires pinned equipment sources");
        static byte[] Load(string path)=>File.ReadAllBytes(path);
        static string Hash(byte[] x)=>Convert.ToHexStringLower(SHA256.HashData(x));
        byte[] a=Load(armorPath!),b=Load(authPath!),c=Load(bonusPath!),d=Load(permitPath!),e=Load(weaponPath!),f=Load(otherPath!);
        var armor=RetailNpcCurseArmorSource.Parse(a,Hash(a));var auth=RetailNpcCurseAuthorizationHpSource.Parse(b,Hash(b));
        var bonus=RetailNpcCurseItemBonusSource.Parse(c,Hash(c));var permits=RetailNpcCurseClassPermitSource.Parse(d,Hash(d));
        var weapon=RetailNpcCurseWeaponHpSource.Parse(e,Hash(e));var other=RetailNpcCurseOtherEquipmentHpSource.Parse(f,Hash(f));
        var equipment=new Equipment(null!);
        var slots=Assert.IsType<SortedDictionary<long,Item>>(typeof(Equipment).GetField("equipment",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(equipment));
        var robe=new Item(900001,new ItemTemplate {itemId=110100420,retailSourceName="rb_torso_n_u1_p_33a",itemGroup=ItemGroup.RB_TORSO,maxEnchant=15},1,true,ItemSlot.TORSO.GetSlotIdMask());robe.SetEnchantLevel(15);
        var sword=new Item(900002,new ItemTemplate {itemId=100001286,retailSourceName="sword_n_e1_pvp_55b",itemGroup=ItemGroup.SWORD,maxEnchant=15},1,true,ItemSlot.MAIN_HAND.GetSlotIdMask());
        typeof(Item).GetField("conditioningInfo",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(sword,new ChargeInfo(500001,sword));
        var neck=new Item(900003,new ItemTemplate {itemId=121001358,retailSourceName="necklace_authorize_test_01",itemGroup=ItemGroup.NECKLACE,maxEnchant=0},1,true,ItemSlot.NECKLACE.GetSlotIdMask());neck.SetTempering(10);
        var shield=new Item(900004,new ItemTemplate {itemId=115000011,retailSourceName="shield_n_c_03a",itemGroup=ItemGroup.SHIELD,maxEnchant=10},1,true,ItemSlot.SUB_HAND.GetSlotIdMask());
        slots.Add(ItemSlot.TORSO.GetSlotIdMask(),robe);slots.Add(ItemSlot.MAIN_HAND.GetSlotIdMask(),sword);
        slots.Add(ItemSlot.NECKLACE.GetSlotIdMask(),neck);slots.Add(ItemSlot.SUB_HAND.GetSlotIdMask(),shield);
        var snapshot=Observation.Capture(equipment,1,65,armor,weaponHp:weapon,otherHp:other,authorizationHp:auth,itemBonus:bonus);
        var normal=new RetailNpcCurseItemBonusSource.RevisionContext(0,true,1,false,0x20);
        var context=new Observation.ArmorProjectionContext(0,null,false,false,normal,normal with {BonusFlags=0x200});
        Assert.True(Observation.TryProjectEquipment(snapshot,permits,_=>context,out var result,out var reason,out int failed));
        Assert.Equal(Observation.ProjectionFailure.None,reason);Assert.Equal(0,failed);Assert.Equal(4,result.ItemCount);
        Assert.Equal(390,result.RevisedArmorHp);Assert.Equal(new(742,0),result.HpBonus);Assert.Equal(new(572,0),result.MpBonus);
        Assert.False(snapshot.GameplayReady);
        Assert.False(Observation.TryProjectEquipment(snapshot,null,_=>context,out _,out reason,out _));
        Assert.Equal(Observation.ProjectionFailure.MissingItemSource,reason);
        var special=context with {Normal=normal with {SpecialServerType=1}};
        Assert.False(Observation.TryProjectEquipment(snapshot,permits,_=>special,out _,out reason,out _));
        Assert.Equal(Observation.ProjectionFailure.MissingNativeContext,reason);
    }

    [SkippableFact]
    public void SeparateOffhandWeaponContributesItsOwnRetailBonuses()
    {
        string? offPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_OFFHAND_FIXTURE");
        string? authPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_AUTHORIZATION_EXPANDED_HP_FIXTURE");
        string? bonusPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_ITEM_BONUS_FIXTURE");
        string? permitPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_CLASS_PERMIT_FIXTURE");
        string? weaponPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_WEAPON_HP_FIXTURE");
        Skip.If(new[]{offPath,authPath,bonusPath,permitPath,weaponPath}.Any(string.IsNullOrEmpty),"Requires pinned weapon sources");
        static byte[] Load(string path)=>File.ReadAllBytes(path);
        static string Hash(byte[] x)=>Convert.ToHexStringLower(SHA256.HashData(x));
        byte[] a=Load(offPath!),b=Load(authPath!),c=Load(bonusPath!),d=Load(permitPath!),e=Load(weaponPath!);
        var off=RetailNpcCurseOffhandSource.Parse(a,Hash(a));var auth=RetailNpcCurseAuthorizationHpSource.Parse(b,Hash(b));
        var bonus=RetailNpcCurseItemBonusSource.Parse(c,Hash(c));var permits=RetailNpcCurseClassPermitSource.Parse(d,Hash(d));
        var weapon=RetailNpcCurseWeaponHpSource.Parse(e,Hash(e));var equipment=new Equipment(null!);
        var slots=Assert.IsType<SortedDictionary<long,Item>>(typeof(Equipment).GetField("equipment",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(equipment));
        var template=new ItemTemplate {itemId=100001286,retailSourceName="sword_n_e1_pvp_55b",itemGroup=ItemGroup.SWORD,maxEnchant=15};
        var main=new Item(900001,template,1,true,ItemSlot.MAIN_HAND.GetSlotIdMask());
        var sub=new Item(900002,template,1,true,ItemSlot.SUB_HAND.GetSlotIdMask());
        typeof(Item).GetField("conditioningInfo",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(main,new ChargeInfo(500001,main));
        slots.Add(ItemSlot.MAIN_HAND.GetSlotIdMask(),main);slots.Add(ItemSlot.SUB_HAND.GetSlotIdMask(),sub);
        Observation.Snapshot Capture()=>Observation.Capture(equipment,1,65,null,off,weapon,authorizationHp:auth,itemBonus:bonus);
        var snapshot=Capture();var subRow=Assert.Single(snapshot.Items,x=>x.ObjectId==900002);
        Assert.Equal(Observation.Status.SkippedRetailOffhandWeapon,subRow.Status);Assert.NotNull(subRow.WeaponHp);Assert.NotNull(subRow.ItemBonus);
        var normal=new RetailNpcCurseItemBonusSource.RevisionContext(0,true,1,false,0x10);
        Observation.ArmorProjectionContext Context(Observation.ItemObservation x)=>new(0,null,false,false,
            normal with {BonusFlags=x.OccupiedSlot==ItemSlot.SUB_HAND.GetSlotIdMask()?8:0x10},normal with {BonusFlags=0x200});
        Assert.True(Observation.TryProjectEquipment(snapshot,permits,x=>Context(x),out var result,out var reason,out int failed));
        Assert.Equal(Observation.ProjectionFailure.None,reason);Assert.Equal(0,failed);Assert.Equal(2,result.ItemCount);
        Assert.Equal(0,result.RevisedArmorHp);Assert.Equal(new(647,0),result.HpBonus);
        var missingOffhand=Observation.Capture(equipment,1,65,null,weaponHp:weapon,authorizationHp:auth,itemBonus:bonus);
        Assert.False(Observation.TryProjectEquipment(missingOffhand,permits,x=>Context(x),out _,out reason,out _));
        Assert.Equal(Observation.ProjectionFailure.UnsupportedItem,reason);
        Assert.False(Observation.TryProjectEquipment(snapshot,permits,x=>Context(x) with {Normal=normal},out _,out reason,out _));
        Assert.Equal(Observation.ProjectionFailure.InvalidBonus,reason);
        var doubled=snapshot with {Items=snapshot.Items.SetItem(snapshot.Items.IndexOf(subRow),
            subRow with {HostSlot=ItemSlot.MAIN_HAND.GetSlotIdMask()|ItemSlot.SUB_HAND.GetSlotIdMask()})};
        Assert.False(Observation.TryProjectEquipment(doubled,permits,x=>Context(x),out _,out reason,out _));
        Assert.Equal(Observation.ProjectionFailure.UnsupportedItemState,reason);
        sub.SetEnchantLevel(1);
        Assert.False(Observation.TryProjectEquipment(Capture(),permits,x=>Context(x),out _,out reason,out _));
        Assert.Equal(Observation.ProjectionFailure.UnsupportedItemState,reason);sub.SetEnchantLevel(0);
        sub.SetTempering(1);
        Assert.False(Observation.TryProjectEquipment(Capture(),permits,x=>Context(x),out _,out reason,out _));
        Assert.Equal(Observation.ProjectionFailure.UnsupportedItemState,reason);sub.SetTempering(0);
        slots.Remove(ItemSlot.MAIN_HAND.GetSlotIdMask());
        Assert.False(Observation.TryProjectEquipment(Capture(),permits,x=>Context(x),out _,out reason,out _));
        Assert.Equal(Observation.ProjectionFailure.UnsupportedItemState,reason);
    }

    [SkippableFact]
    public void MatchedTwoHandedWeaponIsCountedOnceAcrossBothSlots()
    {
        string? authPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_AUTHORIZATION_EXPANDED_HP_FIXTURE");
        string? bonusPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_ITEM_BONUS_FIXTURE");
        string? permitPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_CLASS_PERMIT_FIXTURE");
        string? weaponPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_WEAPON_HP_FIXTURE");
        Skip.If(new[]{authPath,bonusPath,permitPath,weaponPath}.Any(string.IsNullOrEmpty),"Requires pinned weapon sources");
        static byte[] Load(string path)=>File.ReadAllBytes(path);
        static string Hash(byte[] x)=>Convert.ToHexStringLower(SHA256.HashData(x));
        byte[] a=Load(authPath!),b=Load(bonusPath!),c=Load(permitPath!),d=Load(weaponPath!);
        var auth=RetailNpcCurseAuthorizationHpSource.Parse(a,Hash(a));var bonus=RetailNpcCurseItemBonusSource.Parse(b,Hash(b));
        var permits=RetailNpcCurseClassPermitSource.Parse(c,Hash(c));var weapon=RetailNpcCurseWeaponHpSource.Parse(d,Hash(d));
        var equipment=new Equipment(null!);long main=ItemSlot.MAIN_HAND.GetSlotIdMask(),sub=ItemSlot.SUB_HAND.GetSlotIdMask();
        var slots=Assert.IsType<SortedDictionary<long,Item>>(typeof(Equipment).GetField("equipment",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(equipment));
        var template=new ItemTemplate {itemId=100900062,retailSourceName="2hsword_n_c2_12a",itemGroup=ItemGroup.GREATSWORD,maxEnchant=10};
        var item=new Item(900001,template,1,true,main|sub);slots.Add(main,item);slots.Add(sub,item);
        Observation.Snapshot Capture()=>Observation.Capture(equipment,1,65,null,weaponHp:weapon,authorizationHp:auth,itemBonus:bonus);
        var snapshot=Capture();Assert.Equal(2,snapshot.Items.Length);
        Assert.True(Assert.Single(snapshot.Items,x=>x.OccupiedSlot==main).WeaponHp!.IsRetailTwoHanded);
        var normal=new RetailNpcCurseItemBonusSource.RevisionContext(0,true,1,false,0x100);
        var context=new Observation.ArmorProjectionContext(0,null,false,false,normal,default);
        Assert.True(Observation.TryProjectEquipment(snapshot,permits,_=>context,out var result,out var reason,out _));
        Assert.Equal(Observation.ProjectionFailure.None,reason);Assert.Equal(1,result.ItemCount);
        Assert.Equal(0,result.RevisedArmorHp);Assert.Equal(new(48,0),result.HpBonus);
        Assert.False(Observation.TryProjectEquipment(snapshot,permits,_=>context with {Normal=normal with {BonusFlags=0x20}},out _,out reason,out _));
        Assert.Equal(Observation.ProjectionFailure.InvalidBonus,reason);
        var secondary=Assert.Single(snapshot.Items,x=>x.OccupiedSlot==sub);
        var changed=snapshot with {Items=snapshot.Items.SetItem(snapshot.Items.IndexOf(secondary),secondary with {ChargePoints=1})};
        Assert.False(Observation.TryProjectEquipment(changed,permits,_=>context,out _,out reason,out _));
        Assert.Equal(Observation.ProjectionFailure.UnsupportedItemState,reason);
        var repeated=snapshot with {Items=snapshot.Items.Add(secondary)};
        Assert.False(Observation.TryProjectEquipment(repeated,permits,_=>context,out _,out reason,out _));
        Assert.Equal(Observation.ProjectionFailure.UnsupportedItemState,reason);
        slots.Remove(sub);
        Assert.False(Observation.TryProjectEquipment(Capture(),permits,_=>context,out _,out reason,out _));
        Assert.Equal(Observation.ProjectionFailure.UnsupportedItemState,reason);
    }

    [SkippableFact]
    public void FusedEquipmentProjectionRequiresMatchedSecondaryAndExplicitNativeState()
    {
        string? authPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_AUTHORIZATION_EXPANDED_HP_FIXTURE");
        string? bonusPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_ITEM_BONUS_FIXTURE");
        string? permitPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_CLASS_PERMIT_FIXTURE");
        string? weaponPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_WEAPON_HP_FIXTURE");
        Skip.If(new[]{authPath,bonusPath,permitPath,weaponPath}.Any(string.IsNullOrEmpty),"Requires pinned weapon sources");
        static byte[] Load(string path)=>File.ReadAllBytes(path);
        static string Hash(byte[] x)=>Convert.ToHexStringLower(SHA256.HashData(x));
        byte[] a=Load(authPath!),b=Load(bonusPath!),c=Load(permitPath!),d=Load(weaponPath!);
        var auth=RetailNpcCurseAuthorizationHpSource.Parse(a,Hash(a));var bonus=RetailNpcCurseItemBonusSource.Parse(b,Hash(b));
        var permits=RetailNpcCurseClassPermitSource.Parse(c,Hash(c));var weapon=RetailNpcCurseWeaponHpSource.Parse(d,Hash(d));
        var equipment=new Equipment(null!);long main=ItemSlot.MAIN_HAND.GetSlotIdMask(),sub=ItemSlot.SUB_HAND.GetSlotIdMask();
        var slots=Assert.IsType<SortedDictionary<long,Item>>(typeof(Equipment).GetField("equipment",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(equipment));
        var mainTemplate=new ItemTemplate {itemId=100900898,retailSourceName="2hsword_n_l1_r_60a",itemGroup=ItemGroup.GREATSWORD,maxEnchant=10};
        var fusedTemplate=new ItemTemplate {itemId=100900855,retailSourceName="2hsword_n_e1_55c",itemGroup=ItemGroup.GREATSWORD,maxEnchant=15};
        var item=new Item(900001,mainTemplate,1,true,main|sub);
        var field=typeof(Item).GetField("fusionedItemTemplate",BindingFlags.Instance|BindingFlags.NonPublic)!;
        field.SetValue(item,fusedTemplate);slots.Add(main,item);slots.Add(sub,item);
        Observation.Snapshot Capture()=>Observation.Capture(equipment,1,50,null,weaponHp:weapon,authorizationHp:auth,itemBonus:bonus);
        var snapshot=Capture();Assert.False(snapshot.GameplayReady);
        var primary=Assert.Single(snapshot.Items,x=>x.OccupiedSlot==main);
        Assert.Equal(100900855,primary.Fusion!.ItemId);
        Assert.Equal(RetailNpcCurseWeaponHpSource.Failure.None,primary.Fusion.WeaponStatus);
        Assert.Equal(RetailNpcCurseItemBonusSource.Failure.None,primary.Fusion.BonusStatus);
        var normal=new RetailNpcCurseItemBonusSource.RevisionContext(0,true,1,true,0x100);
        var context=new Observation.ArmorProjectionContext(0,null,false,false,normal,default);
        var fusedNormal=normal with {BonusFlags=0x80};
        var fusion=new Observation.FusionProjectionContext(true,true,true,1,0,null,fusedNormal,fusedNormal with {BonusFlags=0x200});
        Assert.False(Observation.TryProjectEquipment(snapshot,permits,_=>context,out _,out var reason,out _));
        Assert.Equal(Observation.ProjectionFailure.MissingNativeContext,reason);
        Assert.True(Observation.TryProjectEquipment(snapshot,permits,_=>context,out var result,out reason,out _,_=>fusion));
        Assert.Equal(1,result.ItemCount);Assert.Equal(new(680,0),result.HpBonus);
        Assert.False(Observation.TryProjectEquipment(snapshot,permits,_=>context,out _,out reason,out _,_=>fusion with {LinkedItemActive=null}));
        Assert.Equal(Observation.ProjectionFailure.MissingNativeContext,reason);
        Assert.False(Observation.TryProjectEquipment(snapshot,permits,_=>context,out _,out reason,out _,_=>fusion with {SecondaryChargePoints=null}));
        Assert.Equal(Observation.ProjectionFailure.MissingNativeContext,reason);
        Assert.False(Observation.TryProjectEquipment(snapshot,permits,_=>context,out _,out reason,out _,_=>fusion with {StandardBonusPath=false}));
        Assert.Equal(Observation.ProjectionFailure.MissingNativeContext,reason);
        Assert.False(Observation.TryProjectEquipment(snapshot,permits,_=>context,out _,out reason,out _,_=>fusion with {AdditionalEffectsAbsent=null}));
        Assert.Equal(Observation.ProjectionFailure.MissingNativeContext,reason);
        Assert.False(Observation.TryProjectEquipment(snapshot,permits,_=>context,out _,out reason,out _,_=>fusion with {Normal=fusedNormal with {HighLevelFlag=false}}));
        Assert.Equal(Observation.ProjectionFailure.MissingNativeContext,reason);
        Assert.False(Observation.TryProjectEquipment(snapshot,permits,_=>context,out _,out reason,out _,_=>fusion with {Charge=fusedNormal}));
        Assert.Equal(Observation.ProjectionFailure.InvalidBonus,reason);
        var second=Assert.Single(snapshot.Items,x=>x.OccupiedSlot==sub);
        var inconsistent=snapshot with {Items=snapshot.Items.SetItem(snapshot.Items.IndexOf(second),second with {Fusion=null})};
        Assert.False(Observation.TryProjectEquipment(inconsistent,permits,_=>context,out _,out reason,out _,_=>fusion));
        Assert.Equal(Observation.ProjectionFailure.UnsupportedItemState,reason);
        var alteredFusion=primary.Fusion with {ItemId=123};
        var forged=snapshot with {Items=snapshot.Items.Select(x=>x with {Fusion=alteredFusion}).ToImmutableArray()};
        Assert.False(Observation.TryProjectEquipment(forged,permits,_=>context,out _,out reason,out _,_=>fusion));
        Assert.Equal(Observation.ProjectionFailure.MissingItemSource,reason);
        fusedTemplate.retailSourceName="mismatched";
        Assert.False(Observation.TryProjectEquipment(Capture(),permits,_=>context,out _,out reason,out _,_=>fusion));
        Assert.Equal(Observation.ProjectionFailure.MissingItemSource,reason);
        field.SetValue(item,null);Assert.Null(Assert.Single(Capture().Items,x=>x.OccupiedSlot==main).Fusion);
    }

    [Fact]
    public void RetailSlotObservationPreservesTwoHandEntriesAndExcludesOnlyProvenSlots()
    {
        var equipment=new Equipment(null!);
        var slots=Assert.IsType<SortedDictionary<long,Item>>(
            typeof(Equipment).GetField("equipment",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(equipment));
        long main=ItemSlot.MAIN_HAND.GetSlotIdMask(),sub=ItemSlot.SUB_HAND.GetSlotIdMask();
        long alternate=ItemSlot.MAIN_OFF_HAND.GetSlotIdMask(),stigma=ItemSlot.STIGMA1.GetSlotIdMask();
        var weapon=new Item(1,new ItemTemplate {itemId=100000001,name="Weapon",itemGroup=ItemGroup.SWORD},1,true,main|sub);
        var inactive=new Item(2,new ItemTemplate {itemId=100000002,name="Inactive",itemGroup=ItemGroup.SWORD},1,true,alternate);
        var stone=new Item(3,new ItemTemplate {itemId=100000003,name="Stigma"},1,true,stigma);
        var last=new Item(4,new ItemTemplate {itemId=100000004,name="Last scanned slot"},1,true,1L<<20);
        var after=new Item(5,new ItemTemplate {itemId=100000005,name="Following slot"},1,true,1L<<21);
        slots.Add(main,weapon);slots.Add(sub,weapon);slots.Add(alternate,inactive);slots.Add(stigma,stone);
        slots.Add(1L<<20,last);slots.Add(1L<<21,after);

        var snapshot=Observation.Capture(equipment,1,33,Source());
        Assert.False(snapshot.GameplayReady);Assert.Equal(6,snapshot.Items.Length);
        var entries=snapshot.Items.Where(x=>x.ObjectId==1).OrderBy(x=>x.OccupiedSlot).ToArray();
        Assert.Equal(new[]{main,sub},entries.Select(x=>x.OccupiedSlot));
        Assert.All(entries,x=>Assert.Equal(Observation.Status.UnknownItem,x.Status));
        Assert.Equal(Observation.Status.OutsideRetailStatLoop,Assert.Single(snapshot.Items,x=>x.ObjectId==2).Status);
        Assert.Equal(Observation.Status.OutsideRetailStatLoop,Assert.Single(snapshot.Items,x=>x.ObjectId==3).Status);
        Assert.Equal(Observation.Status.UnknownItem,Assert.Single(snapshot.Items,x=>x.ObjectId==4).Status);
        Assert.Equal(Observation.Status.OutsideRetailStatLoop,Assert.Single(snapshot.Items,x=>x.ObjectId==5).Status);
        Assert.Equal(Observation.Status.OutsideRetailStatLoop,
            Assert.Single(Observation.Capture(equipment,1,33,null).Items,x=>x.ObjectId==2).Status);

        inactive.SetEquipmentSlot(main);
        Assert.Equal(Observation.Status.SlotMismatch,Assert.Single(Observation.Capture(equipment,1,33,Source()).Items,x=>x.ObjectId==2).Status);
    }

    [Fact]
    public void OffhandWeaponGateRequiresReviewedIdentityAndPreservesMainHand()
    {
        var equipment=new Equipment(null!);long main=ItemSlot.MAIN_HAND.GetSlotIdMask(),sub=ItemSlot.SUB_HAND.GetSlotIdMask();
        var slots=Assert.IsType<SortedDictionary<long,Item>>(
            typeof(Equipment).GetField("equipment",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(equipment));
        var template=new ItemTemplate {itemId=100000001,retailSourceName="sword_a",itemGroup=ItemGroup.SWORD};
        var item=new Item(100,template,1,true,main|sub);slots.Add(main,item);slots.Add(sub,item);
        var offhand=RetailNpcCurseOffhandSourceTests.Source();
        var result=Observation.Capture(equipment,1,33,Source(),offhand);
        Assert.False(result.GameplayReady);
        Assert.Equal(Observation.Status.UnknownItem,Assert.Single(result.Items,x=>x.OccupiedSlot==main).Status);
        Assert.Equal(Observation.Status.SkippedRetailOffhandWeapon,Assert.Single(result.Items,x=>x.OccupiedSlot==sub).Status);
        Assert.Equal(Observation.Status.UnknownItem,Assert.Single(Observation.Capture(equipment,1,33,Source()).Items,x=>x.OccupiedSlot==sub).Status);
        template.retailSourceName="drift";
        Assert.Equal(Observation.Status.IdentityMismatch,Assert.Single(Observation.Capture(equipment,1,33,Source(),offhand).Items,x=>x.OccupiedSlot==sub).Status);
        template.retailSourceName="sword_a";template.itemGroup=ItemGroup.DAGGER;
        Assert.Equal(Observation.Status.HostTemplateDrift,Assert.Single(Observation.Capture(equipment,1,33,Source(),offhand).Items,x=>x.OccupiedSlot==sub).Status);
        item.SetEquipped(false);
        Assert.Equal(Observation.Status.ItemNotEquipped,Assert.Single(Observation.Capture(equipment,1,33,Source(),offhand).Items,x=>x.OccupiedSlot==sub).Status);
    }

    [Fact]
    public void MainHandWeaponBaseNeedsTheReviewedSourceAndStaysSeparateFromOtherBonuses()
    {
        var equipment=new Equipment(null!);long main=ItemSlot.MAIN_HAND.GetSlotIdMask();
        var slots=Assert.IsType<SortedDictionary<long,Item>>(
            typeof(Equipment).GetField("equipment",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(equipment));
        var template=new ItemTemplate {itemId=100000001,retailSourceName="sword_a",itemGroup=ItemGroup.SWORD,maxEnchant=15};
        var item=new Item(101,template,1,true,main);item.SetEnchantLevel(15);slots.Add(main,item);
        var source=RetailNpcCurseWeaponHpSourceTests.Source();
        var snapshot=Observation.Capture(equipment,1,33,Source(),null,source);
        var entry=Assert.Single(snapshot.Items);Assert.False(snapshot.GameplayReady);
        Assert.Equal(Observation.Status.ReviewedWeaponHpOnly,entry.Status);Assert.Equal(0,entry.WeaponHp!.RawMaxHp);
        Assert.Equal(0,entry.WeaponHp.EnchantMaxHp);Assert.Null(entry.Source);
        item.SetEnchantLevel(16);entry=Assert.Single(Observation.Capture(equipment,1,33,Source(),null,source).Items);
        Assert.Equal(Observation.Status.UnreviewedEnchantLevel,entry.Status);Assert.Null(entry.WeaponHp);
        item.SetEnchantLevel(15);template.enchantName="special";
        Assert.Equal(Observation.Status.HostTemplateDrift,Assert.Single(Observation.Capture(equipment,1,33,Source(),null,source).Items).Status);
    }

    [Fact]
    public void ShieldIsNotMistakenForOffhandWeaponAndTemperingRemainsVisible()
    {
        var equipment=new Equipment(null!);long sub=ItemSlot.SUB_HAND.GetSlotIdMask();
        var slots=Assert.IsType<SortedDictionary<long,Item>>(
            typeof(Equipment).GetField("equipment",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(equipment));
        var template=new ItemTemplate {itemId=115000001,retailSourceName="shield_a",itemGroup=ItemGroup.SHIELD,maxEnchant=15};
        var item=new Item(102,template,1,true,sub);item.SetEnchantLevel(15);item.SetTempering(3);slots.Add(sub,item);
        var other=RetailNpcCurseOtherEquipmentHpSourceTests.Source();
        var snap=Observation.Capture(equipment,1,33,Source(),RetailNpcCurseOffhandSourceTests.Source(),null,other);
        var entry=Assert.Single(snap.Items);Assert.False(snap.GameplayReady);Assert.Equal(3,entry.Tempering);
        Assert.Equal(Observation.Status.ReviewedOtherEquipmentHpOnly,entry.Status);Assert.Equal(0,entry.OtherHp!.RawMaxHp);
        Assert.Equal(0,entry.OtherHp.EnchantMaxHp);Assert.Null(entry.WeaponHp);
        item.SetEnchantLevel(16);entry=Assert.Single(Observation.Capture(equipment,1,33,Source(),null,null,other).Items);
        Assert.Equal(Observation.Status.UnreviewedEnchantLevel,entry.Status);Assert.Null(entry.OtherHp);
    }
}
