using System.Collections.Immutable;
using System.Reflection;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Items.Enums;
using Aion.GameServer.Retail;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcItemDefenseBonusSourceTests
{
    [SkippableFact]
    public void EquipmentObservationKeepsBonusesAndMissingContextsVisible()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_17860_ITEM_DEFENSE_BONUS_SOURCE");
        string? bonusPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_ITEM_BONUS_FIXTURE");
        string? armorPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_ARMOR_FIXTURE");
        string? permitsPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_CLASS_PERMIT_FIXTURE");
        Skip.If(new[]{path,bonusPath,armorPath,permitsPath}.Any(string.IsNullOrWhiteSpace),"Requires pinned retail item sources");
        var source=RetailNpcItemDefenseBonusSource.ParseForOfflineFixture(File.ReadAllBytes(path!),"0bc098ba47c2f8d063b0afb47e1d4451d28abc594c6a4a2f5f510ade769ebcdd");
        var bonus=RetailNpcCurseItemBonusSource.Parse(File.ReadAllBytes(bonusPath!),RetailNpcCurseItemBonusSource.ReviewedSourceSha);
        var armor=RetailNpcCurseArmorSource.Parse(File.ReadAllBytes(armorPath!),"72fa5bf580b1ace67426d6a15081531a75b7760962d601de59eed6fdb62a28ff");
        var permits=RetailNpcCurseClassPermitSource.Parse(File.ReadAllBytes(permitsPath!),RetailNpcCurseClassPermitSource.ReviewedSha);
        var equipment=new Equipment(null!);
        var slots=Assert.IsType<SortedDictionary<long,Item>>(typeof(Equipment).GetField("equipment",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(equipment));
        slots.Add((long)ItemSlot.TORSO,new Item(900001,new ItemTemplate {itemId=110301465,retailSourceName="lt_torso_a_m2_65a",itemGroup=ItemGroup.LT_TORSO,maxEnchant=0},1,true,(long)ItemSlot.TORSO));
        var snapshot=RetailNpcCurseEquipmentObservation.Capture(equipment,4,65,armor,itemBonus:bonus);
        var normal=new RetailNpcCurseItemBonusSource.RevisionContext(0,true,1,true,0);
        var charge=new RetailNpcCurseItemBonusSource.RevisionContext(0,true,1,true,0x200);
        var inputs=new RetailNpcPlayerItemDefenseBonusObservation.Context(0,null,normal,charge);
        var projected=RetailNpcPlayerItemDefenseBonusObservation.ProjectForOfflineFixture(snapshot,source,permits,_=>inputs);
        Assert.False(projected.GameplayReady);Assert.Empty(projected.Issues);Assert.Equal(167,projected.Points);
        var row=Assert.Single(snapshot.Items);var withCharge=snapshot with {PlayerLevel=55,Items=ImmutableArray.Create(row with {ChargePoints=1})};
        Assert.Equal(148,RetailNpcPlayerItemDefenseBonusObservation.ProjectForOfflineFixture(withCharge,source,permits,_=>inputs).Points);
        Assert.Equal(RetailNpcPlayerItemDefenseBonusObservation.IssueKind.MissingNativeContext,
            Assert.Single(RetailNpcPlayerItemDefenseBonusObservation.ProjectForOfflineFixture(snapshot,source,permits,_=>null).Issues).Kind);
        var mutated=snapshot with {Items=ImmutableArray.Create(row with {ItemId=1})};
        Assert.Equal(RetailNpcPlayerItemDefenseBonusObservation.IssueKind.MissingSource,
            Assert.Single(RetailNpcPlayerItemDefenseBonusObservation.ProjectForOfflineFixture(mutated,source,permits,_=>inputs).Issues).Kind);
    }

    [SkippableFact]
    public void PhysicalDefenseBonusesUsePinnedListsAndRetailLevelRevision()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_17860_ITEM_DEFENSE_BONUS_SOURCE");
        string? bonusPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_ITEM_BONUS_FIXTURE");
        string? armorPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_ARMOR_FIXTURE");
        Skip.If(new[]{path,bonusPath,armorPath}.Any(string.IsNullOrWhiteSpace),"Requires pinned retail item sources");
        const string sha="0bc098ba47c2f8d063b0afb47e1d4451d28abc594c6a4a2f5f510ade769ebcdd";
        byte[] raw=File.ReadAllBytes(path!);var source=RetailNpcItemDefenseBonusSource.ParseForOfflineFixture(raw,sha);
        Assert.Equal(54153,source.ItemCount);
        var bonus=RetailNpcCurseItemBonusSource.Parse(File.ReadAllBytes(bonusPath!),RetailNpcCurseItemBonusSource.ReviewedSourceSha);
        var armor=RetailNpcCurseArmorSource.Parse(File.ReadAllBytes(armorPath!),"72fa5bf580b1ace67426d6a15081531a75b7760962d601de59eed6fdb62a28ff");
        Assert.Equal(RetailNpcCurseArmorSource.Failure.None,armor.Lookup(110301465,"lt_torso_a_m2_65a",0,out var item));
        Assert.Equal(RetailNpcCurseItemBonusSource.Failure.None,bonus.Lookup(item!,out var identity));
        Assert.Equal(RetailNpcItemDefenseBonusSource.Failure.None,source.Lookup(identity!,out var row));
        Assert.Equal(167,row!.Values[0]!.Value);Assert.Equal(19,row.Values[1]!.Value);Assert.Null(row.Values[2]);
        var normal=new RetailNpcCurseItemBonusSource.RevisionContext(0,true,1,true,0);
        var charge=new RetailNpcCurseItemBonusSource.RevisionContext(0,true,1,true,0x200);
        Assert.Equal(RetailNpcItemDefenseBonusSource.Failure.None,row.Resolve(identity!,0,65,65,normal,charge,out int points));Assert.Equal(167,points);
        Assert.Equal(RetailNpcItemDefenseBonusSource.Failure.None,row.Resolve(identity!,1,65,65,normal,charge,out points));Assert.Equal(186,points);
        Assert.Equal(RetailNpcItemDefenseBonusSource.Failure.None,row.Resolve(identity!,500001,65,65,normal,charge,out points));Assert.Equal(186,points);
        Assert.Equal(RetailNpcItemDefenseBonusSource.Failure.None,row.Resolve(identity!,1,65,55,normal,charge,out points));Assert.Equal(148,points);
        Assert.Equal(RetailNpcItemDefenseBonusSource.Failure.UnreviewedCharge,row.Resolve(identity!,1000001,65,65,normal,charge,out _));
        Assert.Equal(RetailNpcItemDefenseBonusSource.Failure.UnreviewedRevision,row.Resolve(identity!,1,65,65,normal,default,out _));
        Assert.Equal(RetailNpcItemDefenseBonusSource.Failure.IdentityMismatch,source.Lookup(identity! with {Name="changed"},out _));
        Assert.Equal(2067,RetailNpcPlayerDefenseDrainProjection.PhysicalDefense(2000,1000,-10,167));
        var altered=(byte[])raw.Clone();altered[^2]^=1;
        Assert.Throws<InvalidDataException>(()=>RetailNpcItemDefenseBonusSource.ParseForOfflineFixture(altered,sha));
    }
}
