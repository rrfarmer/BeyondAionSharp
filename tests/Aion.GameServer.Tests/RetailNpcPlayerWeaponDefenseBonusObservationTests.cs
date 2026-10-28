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
using Projection=Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcPlayerItemDefenseBonusObservation;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcPlayerWeaponDefenseBonusObservationTests
{
    [SkippableFact]
    public void OffhandAndTwoHandedBonusesRequireReviewedFlagsAndSlotOwnership()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_17860_ITEM_DEFENSE_BONUS_SOURCE");
        string? bonusPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_ITEM_BONUS_FIXTURE");
        string? permitPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_CLASS_PERMIT_FIXTURE");
        string? weaponPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_WEAPON_HP_FIXTURE");
        string? offPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_OFFHAND_FIXTURE");
        Skip.If(new[]{path,bonusPath,permitPath,weaponPath,offPath}.Any(string.IsNullOrWhiteSpace),"Requires pinned retail item sources");
        var source=RetailNpcItemDefenseBonusSource.ParseForOfflineFixture(File.ReadAllBytes(path!),"0bc098ba47c2f8d063b0afb47e1d4451d28abc594c6a4a2f5f510ade769ebcdd");
        var bonus=RetailNpcCurseItemBonusSource.Parse(File.ReadAllBytes(bonusPath!),RetailNpcCurseItemBonusSource.ReviewedSourceSha);
        var permits=RetailNpcCurseClassPermitSource.Parse(File.ReadAllBytes(permitPath!),RetailNpcCurseClassPermitSource.ReviewedSha);
        var weapons=RetailNpcCurseWeaponHpSource.Parse(File.ReadAllBytes(weaponPath!),"09c3411e58241d3b51b82d676841e4f2b09d29fffeb2114eeae7e115c8f6a53a");
        var off=RetailNpcCurseOffhandSource.Parse(File.ReadAllBytes(offPath!),"029a5388abcad6c7f750f0771f7096eb0a846f41b7b453ea8a6becf6f80a959a");
        var equipment=new Equipment(null!);
        var slots=Assert.IsType<SortedDictionary<long,Item>>(typeof(Equipment).GetField("equipment",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(equipment));
        slots.Add((long)ItemSlot.MAIN_HAND,new Item(900001,new ItemTemplate {itemId=100000001,retailSourceName="sword_circulous",itemGroup=ItemGroup.SWORD,maxEnchant=15},1,true,(long)ItemSlot.MAIN_HAND));
        slots.Add((long)ItemSlot.SUB_HAND,new Item(900002,new ItemTemplate {itemId=100200001,retailSourceName="dagger_polymorph_01a",itemGroup=ItemGroup.DAGGER,maxEnchant=10},1,true,(long)ItemSlot.SUB_HAND));
        var snapshot=Observation.Capture(equipment,1,65,null,offhand:off,weaponHp:weapons,itemBonus:bonus);
        Projection.Context Inputs(int flags)=>new(0,null,new(0,true,1,true,flags),new(0,true,1,true,0x200));
        var result=Projection.ProjectForOfflineFixture(snapshot,source,permits,x=>Inputs(x.OccupiedSlot==(long)ItemSlot.SUB_HAND?8:0));
        Assert.Empty(result.Issues);Assert.Equal(2,result.Entries.Length);Assert.Equal(0,result.Points);Assert.False(result.GameplayReady);
        Assert.Equal(Projection.IssueKind.MissingNativeContext,Assert.Single(Projection.ProjectForOfflineFixture(snapshot,source,permits,_=>Inputs(0)).Issues).Kind);
        var offhand=Assert.Single(snapshot.Items,x=>x.OccupiedSlot==(long)ItemSlot.SUB_HAND);
        var ench=snapshot with {Items=snapshot.Items.Select(x=>x==offhand?x with {EnchantLevel=1}:x).ToImmutableArray()};
        Assert.Equal(Projection.IssueKind.UnsupportedItemState,Assert.Single(Projection.ProjectForOfflineFixture(ench,source,permits,x=>Inputs(x.OccupiedSlot==(long)ItemSlot.SUB_HAND?8:0)).Issues).Kind);
        slots.Clear();long both=(long)ItemSlot.MAIN_OR_SUB;
        var greatsword=new Item(900003,new ItemTemplate {itemId=100900001,retailSourceName="2hsword_test_01",itemGroup=ItemGroup.GREATSWORD,maxEnchant=10},1,true,both);
        slots.Add((long)ItemSlot.MAIN_HAND,greatsword);slots.Add((long)ItemSlot.SUB_HAND,greatsword);
        var two=Observation.Capture(equipment,1,65,null,offhand:off,weaponHp:weapons,itemBonus:bonus);
        var twoResult=Projection.ProjectForOfflineFixture(two,source,permits,_=>Inputs(0x100));
        Assert.Empty(twoResult.Issues);Assert.Single(twoResult.Entries);Assert.Equal(0,twoResult.Points);
        Assert.Equal(Projection.IssueKind.MissingNativeContext,Assert.Single(Projection.ProjectForOfflineFixture(two,source,permits,_=>Inputs(0)).Issues).Kind);
        var inconsistent=two with {Items=two.Items.Select(x=>x.OccupiedSlot==(long)ItemSlot.SUB_HAND?x with {ChargePoints=1}:x).ToImmutableArray()};
        Assert.Empty(Projection.ProjectForOfflineFixture(inconsistent,source,permits,_=>Inputs(0x100)).Entries);
    }
}
