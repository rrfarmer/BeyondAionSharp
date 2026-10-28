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

public sealed class RetailNpcWeaponDefenseSourceTests
{
    [SkippableFact]
    public void MatchedWeaponsAreZeroOnlyForReviewedItemAndSlotStates()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_17860_WEAPON_DEFENSE_SOURCE");
        string? weaponPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_WEAPON_HP_FIXTURE");
        string? offPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_OFFHAND_FIXTURE");
        string? armorPath=Environment.GetEnvironmentVariable("AION_RETAIL_17860_ARMOR_SOURCE");
        Skip.If(new[]{path,weaponPath,offPath,armorPath}.Any(string.IsNullOrWhiteSpace),"Requires pinned retail sources");
        const string sha="ae8a1ebe3fa68fac4faf3b2b477d46e4e45a08fcf80e327a67483639477da11b";
        byte[] bytes=File.ReadAllBytes(path!);var source=RetailNpcWeaponDefenseSource.ParseForOfflineFixture(bytes,sha);Assert.Equal(17780,source.ItemCount);
        var hp=RetailNpcCurseWeaponHpSource.Parse(File.ReadAllBytes(weaponPath!),"09c3411e58241d3b51b82d676841e4f2b09d29fffeb2114eeae7e115c8f6a53a");
        var off=RetailNpcCurseOffhandSource.Parse(File.ReadAllBytes(offPath!),"029a5388abcad6c7f750f0771f7096eb0a846f41b7b453ea8a6becf6f80a959a");
        var armor=RetailNpcArmorDefenseSource.ParseForOfflineFixture(File.ReadAllBytes(armorPath!),"ebd610658fd1225d1ec262cbf2971ba5a9d4954c8004f7739fe0456422e6a7ba");
        var equipment=new Equipment(null!);
        var slots=Assert.IsType<SortedDictionary<long,Item>>(typeof(Equipment).GetField("equipment",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(equipment));
        var sword=new Item(900001,new ItemTemplate {itemId=100000001,retailSourceName="sword_circulous",itemGroup=ItemGroup.SWORD,maxEnchant=15},1,true,(long)ItemSlot.MAIN_HAND);
        sword.SetEnchantLevel(15);slots.Add((long)ItemSlot.MAIN_HAND,sword);
        var dagger=new Item(900002,new ItemTemplate {itemId=100200001,retailSourceName="dagger_polymorph_01a",itemGroup=ItemGroup.DAGGER,maxEnchant=10},1,true,(long)ItemSlot.SUB_HAND);
        dagger.SetEnchantLevel(10);slots.Add((long)ItemSlot.SUB_HAND,dagger);
        var input=new Projection.Context(0,null,true,RetailNpcArmorDefenseSource.Restriction.Computed);
        var snapshot=Observation.Capture(equipment,1,65,null,offhand:off,weaponHp:hp);
        var result=Projection.ProjectForOfflineFixture(snapshot,armor,_=>input,weapons:source);
        Assert.Empty(result.Issues);Assert.Equal(2,result.Entries.Length);Assert.Equal(0,result.RevisedArmorSubtotal);Assert.False(result.GameplayReady);
        Assert.All(result.Entries,x=>Assert.Equal(0,x.RevisedDefense));
        var weapon=Assert.Single(snapshot.Items,x=>x.ItemId==100000001).WeaponHp!;
        Assert.Equal(RetailNpcWeaponDefenseSource.Failure.None,source.Lookup(weapon,15));
        Assert.Equal(RetailNpcWeaponDefenseSource.Failure.UnreviewedEnchant,source.Lookup(weapon,16));
        Assert.Equal(RetailNpcWeaponDefenseSource.Failure.IdentityMismatch,source.Lookup(weapon with {Name="changed"},0));
        Assert.Equal(2,Projection.ProjectForOfflineFixture(snapshot,armor,_=>input).Issues.Length);
        var changed=(byte[])bytes.Clone();changed[^2]^=1;Assert.Throws<InvalidDataException>(()=>RetailNpcWeaponDefenseSource.ParseForOfflineFixture(changed,sha));
        slots.Clear();long two=(long)ItemSlot.MAIN_OR_SUB;
        var greatsword=new Item(900003,new ItemTemplate {itemId=100900001,retailSourceName="2hsword_test_01",itemGroup=ItemGroup.GREATSWORD,maxEnchant=10},1,true,two);
        greatsword.SetEnchantLevel(10);slots.Add((long)ItemSlot.MAIN_HAND,greatsword);slots.Add((long)ItemSlot.SUB_HAND,greatsword);
        var twoSnapshot=Observation.Capture(equipment,1,65,null,offhand:off,weaponHp:hp);
        var twoResult=Projection.ProjectForOfflineFixture(twoSnapshot,armor,_=>input,weapons:source);
        Assert.Empty(twoResult.Issues);Assert.Single(twoResult.Entries);Assert.Equal(0,twoResult.RevisedArmorSubtotal);
        var inconsistent=twoSnapshot with {Items=twoSnapshot.Items.Select(x=>x.OccupiedSlot==(long)ItemSlot.SUB_HAND?x with {EnchantLevel=9}:x).ToImmutableArray()};
        var bad=Projection.ProjectForOfflineFixture(inconsistent,armor,_=>input,weapons:source);
        Assert.Empty(bad.Entries);Assert.NotEmpty(bad.Issues);
        var fused=twoSnapshot with {Items=twoSnapshot.Items.Select(x=>x with {HasFusion=true}).ToImmutableArray()};
        Assert.Empty(Projection.ProjectForOfflineFixture(fused,armor,_=>input,weapons:source).Entries);
        Assert.NotEmpty(Projection.ProjectForOfflineFixture(fused,armor,_=>input,weapons:source).Issues);
    }
}
