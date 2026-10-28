using System.Collections.Immutable;
using System.Reflection;
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

public sealed class RetailNpcOtherEquipmentDefenseSourceTests
{
    [SkippableFact]
    public void SelectedHeadgearAndRingContributeAndOmittedFieldsStayDistinguishable()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_17860_OTHER_DEFENSE_SOURCE");
        string? hpPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_OTHER_HP_FIXTURE");
        string? armorPath=Environment.GetEnvironmentVariable("AION_RETAIL_17860_ARMOR_SOURCE");
        Skip.If(new[]{path,hpPath,armorPath}.Any(string.IsNullOrWhiteSpace),"Requires pinned retail sources");
        const string sha="53ebb035f0d1aa834638373b56ec991fc527cdf1ac1da56ab52533fd63eb3784";
        byte[] bytes=File.ReadAllBytes(path!);var source=RetailNpcOtherEquipmentDefenseSource.ParseForOfflineFixture(bytes,sha);
        Assert.Equal(9614,source.ItemCount);
        var hp=RetailNpcCurseOtherEquipmentHpSource.Parse(File.ReadAllBytes(hpPath!),"22ca15359baded4953cacf6a19ee6e61fd33e92ed05d0cc7f048072df8cd001e");
        var armor=RetailNpcArmorDefenseSource.ParseForOfflineFixture(File.ReadAllBytes(armorPath!),"ebd610658fd1225d1ec262cbf2971ba5a9d4954c8004f7739fe0456422e6a7ba");
        Assert.Equal(RetailNpcOtherEquipmentDefenseSource.Failure.None,source.Lookup(125003322,"rb_head_n_u1_n_65a",0,out var head));
        Assert.Equal(89,head!.RawPhysicalDefense);Assert.True(head.PhysicalDefenseExplicit);
        Assert.Equal(RetailNpcOtherEquipmentDefenseSource.Failure.None,source.Lookup(122001532,"ring_n_u1_q_silverlin_65a",0,out var ring));
        Assert.Equal(18,ring!.RawPhysicalDefense);
        Assert.Equal(RetailNpcOtherEquipmentDefenseSource.Failure.None,source.Lookup(122000010,"ring_n_c_21a",0,out var omitted));
        Assert.Equal(0,omitted!.RawPhysicalDefense);Assert.False(omitted.PhysicalDefenseExplicit);
        Assert.Equal(RetailNpcOtherEquipmentDefenseSource.Failure.UnreviewedEnchant,source.Lookup(head.Id,head.Name,1,out _));
        Assert.Equal(RetailNpcOtherEquipmentDefenseSource.Failure.IdentityMismatch,source.Lookup(head.Id,"different",0,out _));
        var altered=(byte[])bytes.Clone();altered[^2]^=1;
        Assert.Throws<InvalidDataException>(()=>RetailNpcOtherEquipmentDefenseSource.ParseForOfflineFixture(altered,sha));
        var equipment=new Equipment(null!);
        var slots=Assert.IsType<SortedDictionary<long,Item>>(typeof(Equipment).GetField("equipment",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(equipment));
        slots.Add(ItemSlot.HELMET.GetSlotIdMask(),new Item(900001,new ItemTemplate {itemId=head.Id,retailSourceName=head.Name,itemGroup=ItemGroup.HEAD,maxEnchant=0},1,true,ItemSlot.HELMET.GetSlotIdMask()));
        slots.Add(ItemSlot.RING_LEFT.GetSlotIdMask(),new Item(900002,new ItemTemplate {itemId=ring.Id,retailSourceName=ring.Name,itemGroup=ItemGroup.RING,maxEnchant=0},1,true,ItemSlot.RING_LEFT.GetSlotIdMask()));
        var snapshot=Observation.Capture(equipment,1,65,null,otherHp:hp);
        var input=new Projection.Context(0,null,true,RetailNpcArmorDefenseSource.Restriction.Computed);
        var result=Projection.ProjectForOfflineFixture(snapshot,armor,_=>input,otherEquipment:source);
        Assert.Empty(result.Issues);Assert.False(result.GameplayReady);Assert.Equal(107,result.RevisedArmorSubtotal);
        Assert.Equal(1990,RetailNpcPlayerDefenseDrainProjection.PhysicalDefense(2000,(uint)result.RevisedArmorSubtotal,-10,0));
        var without=Projection.ProjectForOfflineFixture(snapshot,armor,_=>input);
        Assert.Equal(2,without.Issues.Length);
        var wrongSlot=snapshot with {Items=snapshot.Items.Select(x=>x.ItemId==ring.Id
            ?x with {HostSlot=(long)ItemSlot.WAIST,OccupiedSlot=(long)ItemSlot.WAIST}:x).ToImmutableArray()};
        Assert.Equal(Projection.IssueKind.InvalidItem,Assert.Single(Projection.ProjectForOfflineFixture(wrongSlot,armor,_=>input,otherEquipment:source).Issues).Kind);
        var revised=Projection.ProjectForOfflineFixture(snapshot with {PlayerLevel=23},armor,_=>input,otherEquipment:source);
        Assert.Equal(16,revised.RevisedArmorSubtotal); // Explicit input level reduction, not an equip eligibility claim.
    }
}
