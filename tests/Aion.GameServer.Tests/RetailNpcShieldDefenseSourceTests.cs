using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Items.Enums;
using Aion.GameServer.Retail;
using Observation=Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcCurseEquipmentObservation;
using Projection=Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcPlayerArmorDefenseObservation;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcShieldDefenseSourceTests
{
    [SkippableFact]
    public void MatchedShieldAndArmorProduceOnlyAnExplicitPartialDefenseBasis()
    {
        string? shieldPath=Environment.GetEnvironmentVariable("AION_RETAIL_17860_SHIELD_SOURCE");
        string? hpPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_ARMOR_FIXTURE");
        string? otherPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_OTHER_HP_FIXTURE");
        string? armorPath=Environment.GetEnvironmentVariable("AION_RETAIL_17860_ARMOR_SOURCE");
        string? manifest=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        Skip.If(new[]{shieldPath,hpPath,otherPath,armorPath,manifest}.Any(string.IsNullOrWhiteSpace),"Requires pinned retail sources");
        byte[] bytes=File.ReadAllBytes(shieldPath!);
        const string sha="5d31e7d11491ec75d4c2804fe59632e4566ea0228b4bddf7e1b4320f7da69ae6";
        var shields=RetailNpcShieldDefenseSource.ParseForOfflineFixture(bytes,sha);Assert.Equal(1635,shields.ItemCount);
        var armorHp=RetailNpcCurseArmorSource.Parse(File.ReadAllBytes(hpPath!),"72fa5bf580b1ace67426d6a15081531a75b7760962d601de59eed6fdb62a28ff");
        var armor=RetailNpcArmorDefenseSource.ParseForOfflineFixture(File.ReadAllBytes(armorPath!),"ebd610658fd1225d1ec262cbf2971ba5a9d4954c8004f7739fe0456422e6a7ba");
        var other=RetailNpcCurseOtherEquipmentHpSource.Parse(File.ReadAllBytes(otherPath!),"22ca15359baded4953cacf6a19ee6e61fd33e92ed05d0cc7f048072df8cd001e");
        Assert.Equal(RetailNpcShieldDefenseSource.Failure.None,shields.Lookup(115001695,"npc_shield_GabSub01a_01",15,out var selected));
        Assert.Equal(5,selected!.RawPhysicalDefense);Assert.True(selected.PhysicalDefenseExplicit);
        Assert.Equal(RetailNpcShieldDefenseSource.Failure.None,shields.Lookup(115000011,"shield_n_c_03a",10,out var zeroShield));
        Assert.Equal(0,zeroShield!.RawPhysicalDefense);Assert.True(zeroShield.PhysicalDefenseExplicit);
        Assert.Equal(RetailNpcShieldDefenseSource.Failure.UnreviewedEnchant,shields.Lookup(115000011,zeroShield.Name,11,out _));
        Assert.Equal(RetailNpcShieldDefenseSource.Failure.None,shields.Lookup(115001364,"test_shield_n_u1_60a",0,out var defaulted));
        Assert.Equal(0,defaulted!.RawPhysicalDefense);Assert.False(defaulted.PhysicalDefenseExplicit);
        Assert.Equal(RetailNpcShieldDefenseSource.Failure.UnknownItem,shields.Lookup(115099999,"unknown",0,out _));
        Assert.Equal(RetailNpcShieldDefenseSource.Failure.IdentityMismatch,shields.Lookup(115001695,"different",0,out _));
        Assert.Equal(RetailNpcShieldDefenseSource.Failure.UnreviewedEnchant,shields.Lookup(115001695,selected.Name,16,out _));
        var altered=(byte[])bytes.Clone();altered[^2]^=1;
        Assert.Throws<InvalidDataException>(()=>RetailNpcShieldDefenseSource.ParseForOfflineFixture(altered,sha));
        var equipment=new Equipment(null!);
        var slots=Assert.IsType<SortedDictionary<long,Item>>(typeof(Equipment).GetField("equipment",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(equipment));
        var robe=new Item(900001,new ItemTemplate {itemId=110100420,retailSourceName="rb_torso_n_u1_p_33a",itemGroup=ItemGroup.RB_TORSO,maxEnchant=15},1,true,ItemSlot.TORSO.GetSlotIdMask());
        robe.SetEnchantLevel(15);slots.Add(ItemSlot.TORSO.GetSlotIdMask(),robe);
        var shieldItem=new Item(900002,new ItemTemplate {itemId=115001695,retailSourceName="npc_shield_GabSub01a_01",itemGroup=ItemGroup.SHIELD,maxEnchant=15},1,true,ItemSlot.SUB_HAND.GetSlotIdMask());
        shieldItem.SetEnchantLevel(15);slots.Add(ItemSlot.SUB_HAND.GetSlotIdMask(),shieldItem);
        var snapshot=Observation.Capture(equipment,1,23,armorHp,otherHp:other);
        var input=new Projection.Context(0,null,true,RetailNpcArmorDefenseSource.Restriction.Computed);
        var report=Projection.ProjectForOfflineFixture(snapshot,armor,_=>input,shields);
        Assert.False(report.GameplayReady);Assert.Empty(report.Issues);Assert.Equal(125,report.RevisedArmorSubtotal);
        Assert.Equal(5,Assert.Single(report.Entries,x=>x.ItemId==115001695).RevisedDefense);
        using var doc=JsonDocument.Parse(File.ReadAllBytes(manifest!));
        var asset=doc.RootElement.GetProperty("skill_sources").EnumerateArray().Single(x=>x.GetProperty("id").GetInt32()==17860);
        int percent=RetailNpcPlayerDefenseDrainProjection.Read17860PercentForOfflineFixture(File.ReadAllBytes(asset.GetProperty("path").GetString()!));
        Assert.Equal(1988,RetailNpcPlayerDefenseDrainProjection.PhysicalDefense(2000,(uint)report.RevisedArmorSubtotal,percent,0));
        var withoutShieldSource=Projection.ProjectForOfflineFixture(snapshot,armor,_=>input);
        Assert.Equal(Projection.IssueKind.NotReviewedArmor,Assert.Single(withoutShieldSource.Issues).Kind);
        var unsupported=snapshot with {Items=snapshot.Items.Select(x=>x.ItemId==115001695?x with {HasSocketOrIdian=true}:x).ToImmutableArray()};
        Assert.Equal(Projection.IssueKind.UnsupportedItemState,Assert.Single(Projection.ProjectForOfflineFixture(unsupported,armor,_=>input,shields).Issues).Kind);
        var wrongSlot=snapshot with {Items=snapshot.Items.Select(x=>x.ItemId==115001695
            ?x with {HostSlot=ItemSlot.TORSO.GetSlotIdMask(),OccupiedSlot=ItemSlot.TORSO.GetSlotIdMask()}:x).ToImmutableArray()};
        Assert.Equal(Projection.IssueKind.InvalidItem,Assert.Single(Projection.ProjectForOfflineFixture(wrongSlot,armor,_=>input,shields).Issues).Kind);
        slots[ItemSlot.SUB_HAND.GetSlotIdMask()]=new Item(900003,new ItemTemplate {itemId=115001364,
            retailSourceName="test_shield_n_u1_60a",itemGroup=ItemGroup.SHIELD,maxEnchant=15},1,true,ItemSlot.SUB_HAND.GetSlotIdMask());
        var withDefaulted=Observation.Capture(equipment,1,23,armorHp,otherHp:other);
        var defaultReport=Projection.ProjectForOfflineFixture(withDefaulted,armor,_=>input,shields);
        Assert.Empty(defaultReport.Issues);Assert.Equal(120,defaultReport.RevisedArmorSubtotal);
        Assert.Equal(0,Assert.Single(defaultReport.Entries,x=>x.ItemId==115001364).RevisedDefense);
    }
}
