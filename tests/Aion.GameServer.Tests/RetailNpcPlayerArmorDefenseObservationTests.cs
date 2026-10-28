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

public sealed class RetailNpcPlayerArmorDefenseObservationTests
{
    [SkippableFact]
    public void ActualEquipmentObservationProducesOnlyReviewedArmorSubtotalAndVisibleIssues()
    {
        string? hpPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_ARMOR_FIXTURE");
        string? defensePath=Environment.GetEnvironmentVariable("AION_RETAIL_17860_ARMOR_SOURCE");
        Skip.If(string.IsNullOrWhiteSpace(hpPath)||string.IsNullOrWhiteSpace(defensePath),"Requires pinned retail armor sources");
        var hp=RetailNpcCurseArmorSource.Parse(File.ReadAllBytes(hpPath!),"72fa5bf580b1ace67426d6a15081531a75b7760962d601de59eed6fdb62a28ff");
        var defense=RetailNpcArmorDefenseSource.ParseForOfflineFixture(File.ReadAllBytes(defensePath!),"ebd610658fd1225d1ec262cbf2971ba5a9d4954c8004f7739fe0456422e6a7ba");
        var equipment=new Equipment(null!);
        var armorTemplate=new ItemTemplate {itemId=110100420,retailSourceName="rb_torso_n_u1_p_33a",itemGroup=ItemGroup.RB_TORSO,maxEnchant=15};
        var armor=new Item(900001,armorTemplate,1,true,ItemSlot.TORSO.GetSlotIdMask());armor.SetEnchantLevel(15);
        var unknown=new Item(900002,new ItemTemplate {itemId=199999999,retailSourceName="unknown",itemGroup=ItemGroup.SWORD},1,true,ItemSlot.MAIN_HAND.GetSlotIdMask());
        var slots=Assert.IsType<SortedDictionary<long,Item>>(typeof(Equipment).GetField("equipment",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(equipment));
        slots.Add(ItemSlot.TORSO.GetSlotIdMask(),armor);slots.Add(ItemSlot.MAIN_HAND.GetSlotIdMask(),unknown);
        var snapshot=Observation.Capture(equipment,1,23,hp);
        var inputs=new Projection.Context(0,null,true,RetailNpcArmorDefenseSource.Restriction.Computed);
        var report=Projection.ProjectForOfflineFixture(snapshot,defense,_=>inputs);
        var entry=Assert.Single(report.Entries);
        Assert.Equal((110100420,33,94,45,120),(entry.ItemId,entry.RequiredLevel,entry.RawDefense,entry.EnchantDefense,entry.RevisedDefense));
        Assert.Equal(120,report.RevisedArmorSubtotal);Assert.False(report.GameplayReady);Assert.Contains("other equipment",report.Limitation);
        var issue=Assert.Single(report.Issues);Assert.Equal((199999999,Projection.IssueKind.NotReviewedArmor),(issue.ItemId,issue.Kind));
        var row=Assert.Single(snapshot.Items,x=>x.ItemId==110100420);
        var onlyArmor=snapshot with {Items=ImmutableArray.Create(row)};
        var incomplete=Projection.ProjectForOfflineFixture(onlyArmor,defense,_=>null);
        Assert.Equal(Projection.IssueKind.MissingNativeContext,Assert.Single(incomplete.Issues).Kind);Assert.Empty(incomplete.Entries);
        var changedPermit=onlyArmor with {Items=ImmutableArray.Create(row with {RawClassPermit=42})};
        Assert.Equal(Projection.IssueKind.PermitMismatch,Assert.Single(Projection.ProjectForOfflineFixture(changedPermit,defense,_=>inputs).Issues).Kind);
        var socket=onlyArmor with {Items=ImmutableArray.Create(row with {HasSocketOrIdian=true})};
        Assert.Equal(Projection.IssueKind.UnsupportedItemState,Assert.Single(Projection.ProjectForOfflineFixture(socket,defense,_=>inputs).Issues).Kind);
        var duplicate=onlyArmor with {Items=onlyArmor.Items.Add(row)};
        var duplicateReport=Projection.ProjectForOfflineFixture(duplicate,defense,_=>inputs);
        Assert.Single(duplicateReport.Entries);Assert.Equal(Projection.IssueKind.InvalidItem,Assert.Single(duplicateReport.Issues).Kind);
        armor.SetEnchantLevel(14);
        var updated=Projection.ProjectForOfflineFixture(Observation.Capture(equipment,1,23,hp),defense,_=>inputs);
        Assert.Equal(117,updated.RevisedArmorSubtotal);Assert.Equal(120,report.RevisedArmorSubtotal);
    }
}
