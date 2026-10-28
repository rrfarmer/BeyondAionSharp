using System.Security.Cryptography;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Items.Enums;
using Aion.GameServer.Retail;
using System.Reflection;
using System.Collections.Immutable;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public class RetailNpcCurseAuthorizationHpSourceTests
{
    [Fact]
    public void RetailLevelRuleRetainsMissingRowsAndWrapsInt32()
    {
        var table=new RetailNpcCurseAuthorizationHpSource.Table(3,new Dictionary<int,RetailNpcCurseAuthorizationHpSource.Level>
        { [1]=new(25,true),[2]=new(0,false) },7,true);
        foreach(var (level,hp) in new[]{(0,0),(1,25),(2,0),(3,7),(4,14)})
        {Assert.True(table.TryHp(level,out int actual));Assert.Equal(hp,actual);}
        Assert.False(table.TryHp(-1,out _));Assert.False(table.TryHp(256,out _));
        var missing=table with { Levels=new Dictionary<int,RetailNpcCurseAuthorizationHpSource.Level>() };
        Assert.False(missing.TryHp(3,out _));
        var wrap=new RetailNpcCurseAuthorizationHpSource.Table(1,new Dictionary<int,RetailNpcCurseAuthorizationHpSource.Level>(),int.MaxValue,true);
        Assert.True(wrap.TryHp(2,out int result));Assert.Equal(-2,result);
    }

    [SkippableFact]
    public void ReviewedRetailSourceMatchesItemIdentityAndTempering()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_AUTHORIZATION_HP_FIXTURE");
        Skip.If(string.IsNullOrEmpty(path),"Requires reviewed authorization source");
        byte[] bytes=File.ReadAllBytes(path!);string hash=Convert.ToHexStringLower(SHA256.HashData(bytes));
        var source=RetailNpcCurseAuthorizationHpSource.Parse(bytes,hash);
        Assert.Equal(11249,source.ItemCount);Assert.Equal(23,source.TableCount);
        var necklace=new RetailNpcCurseOtherEquipmentHpSource.Candidate(121001358,"necklace_authorize_test_01","NECKLACE",0,0,false);
        Assert.Equal(RetailNpcCurseAuthorizationHpSource.Failure.None,source.Lookup(necklace,10,out int hp));Assert.Equal(30,hp);
        Assert.Equal(RetailNpcCurseAuthorizationHpSource.Failure.None,source.Lookup(necklace,11,out hp));Assert.Equal(30,hp);
        Assert.Equal(RetailNpcCurseAuthorizationHpSource.Failure.IdentityMismatch,source.Lookup(necklace with {Name="changed"},10,out _));
        Assert.Equal(RetailNpcCurseAuthorizationHpSource.Failure.UnknownItem,source.Lookup(necklace with {ItemId=1},10,out _));
        Assert.Equal(RetailNpcCurseAuthorizationHpSource.Failure.UnreviewedLevel,source.Lookup(necklace,256,out _));
        Assert.Throws<InvalidDataException>(()=>RetailNpcCurseAuthorizationHpSource.Parse(bytes,new string('0',64)));
    }

    [SkippableFact]
    public void OfflineObservationExposesAuthorizationButRemainsNotGameplayReady()
    {
        string? authPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_AUTHORIZATION_HP_FIXTURE");
        string? otherPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_OTHER_HP_FIXTURE");
        Skip.If(string.IsNullOrEmpty(authPath)||string.IsNullOrEmpty(otherPath),"Requires reviewed equipment sources");
        byte[] authBytes=File.ReadAllBytes(authPath!),otherBytes=File.ReadAllBytes(otherPath!);
        var auth=RetailNpcCurseAuthorizationHpSource.Parse(authBytes,Convert.ToHexStringLower(SHA256.HashData(authBytes)));
        var other=RetailNpcCurseOtherEquipmentHpSource.Parse(otherBytes,Convert.ToHexStringLower(SHA256.HashData(otherBytes)));
        var template=new ItemTemplate {itemId=121001358,retailSourceName="necklace_authorize_test_01",itemGroup=ItemGroup.NECKLACE,maxEnchant=0};
        var item=new Item(900001,template,1,true,ItemSlot.NECKLACE.GetSlotIdMask());item.SetTempering(10);
        var equipment=new Equipment(null!);
        var field=typeof(Equipment).GetField("equipment",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var slots=Assert.IsType<SortedDictionary<long,Item>>(field.GetValue(equipment));slots.Add(ItemSlot.NECKLACE.GetSlotIdMask(),item);
        var snapshot=RetailNpcCurseEquipmentObservation.Capture(equipment,1,33,null,otherHp:other,authorizationHp:auth);
        Assert.False(snapshot.GameplayReady);var observed=Assert.Single(snapshot.Items);
        Assert.Equal(RetailNpcCurseAuthorizationHpSource.Failure.None,observed.AuthorizationStatus);Assert.Equal(30,observed.AuthorizationHp);
        var missing=Assert.Single(RetailNpcCurseEquipmentObservation.Capture(equipment,1,33,null,otherHp:other).Items);
        Assert.Equal(RetailNpcCurseAuthorizationHpSource.Failure.SourceUnavailable,missing.AuthorizationStatus);Assert.Null(missing.AuthorizationHp);
    }

    [SkippableFact]
    public void OfflineCandidateCatalogInstallsAndRestoresThePinnedSource()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_AUTHORIZATION_HP_MANIFEST");
        Skip.If(string.IsNullOrEmpty(path),"Requires offline candidate manifest");
        byte[] bytes=File.ReadAllBytes(path!);string hash=Convert.ToHexStringLower(SHA256.HashData(bytes));
        var previous=RetailNpcStatCatalog.CurseAuthorizationHpSource;
        using(RetailNpcStatCatalog.InstallForTests(bytes,hash,Path.GetDirectoryName(path!)!))
        {
            Assert.Equal(11249,RetailNpcStatCatalog.CurseAuthorizationHpSource!.ItemCount);
            Assert.Equal(11249,RetailNpcStatCatalog.CurseOtherEquipmentHpSource!.ItemCount);
        }
        Assert.Same(previous,RetailNpcStatCatalog.CurseAuthorizationHpSource);
    }

    [SkippableFact]
    public void ExpandedSourceBindsArmorAndWeaponWithoutCrossFamilyFallback()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_AUTHORIZATION_EXPANDED_HP_FIXTURE");
        Skip.If(string.IsNullOrEmpty(path),"Requires expanded authorization source");
        byte[] bytes=File.ReadAllBytes(path!);var source=RetailNpcCurseAuthorizationHpSource.Parse(bytes,Convert.ToHexStringLower(SHA256.HashData(bytes)));
        Assert.Equal(54153,source.ItemCount);Assert.Equal(56,source.TableCount);
        var armor=new RetailNpcCurseArmorSource.Candidate(110100009,"rb_torso_n_c_01a",0,false,0,10,10,"rb_torso",ImmutableArray<byte>.Empty,0);
        var sword=new RetailNpcCurseWeaponHpSource.Candidate(100000001,"sword_circulous","SWORD","sword",15,false);
        Assert.Equal(RetailNpcCurseAuthorizationHpSource.Failure.None,source.Lookup(armor,11,out int hp));Assert.Equal(0,hp);
        Assert.Equal(RetailNpcCurseAuthorizationHpSource.Failure.None,source.Lookup(sword,255,out hp));Assert.Equal(0,hp);
        Assert.Equal(RetailNpcCurseAuthorizationHpSource.Failure.IdentityMismatch,source.Lookup(sword with {Name="changed"},1,out _));
        Assert.Equal(RetailNpcCurseAuthorizationHpSource.Failure.IdentityMismatch,source.Lookup(sword with {HostGroup="MACE"},1,out _));
        Assert.Equal(RetailNpcCurseAuthorizationHpSource.Failure.UnreviewedLevel,source.Lookup(armor,256,out _));
    }

    [SkippableFact]
    public void ExpandedObservationKeepsArmorAndMainHandSourceChecks()
    {
        string? authPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_AUTHORIZATION_EXPANDED_HP_FIXTURE");
        string? armorPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_ARMOR_FIXTURE");
        string? weaponPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_WEAPON_HP_FIXTURE");
        Skip.If(string.IsNullOrEmpty(authPath)||string.IsNullOrEmpty(armorPath)||string.IsNullOrEmpty(weaponPath),"Requires reviewed expansion sources");
        byte[] a=File.ReadAllBytes(authPath!),b=File.ReadAllBytes(armorPath!),c=File.ReadAllBytes(weaponPath!);
        string Hash(byte[] x)=>Convert.ToHexStringLower(SHA256.HashData(x));
        var auth=RetailNpcCurseAuthorizationHpSource.Parse(a,Hash(a));var armor=RetailNpcCurseArmorSource.Parse(b,Hash(b));
        var weapon=RetailNpcCurseWeaponHpSource.Parse(c,Hash(c));
        var torso=new Item(900001,new ItemTemplate {itemId=110100009,retailSourceName="rb_torso_n_c_01a",itemGroup=ItemGroup.RB_TORSO,maxEnchant=10},1,true,ItemSlot.TORSO.GetSlotIdMask());
        var sword=new Item(900002,new ItemTemplate {itemId=100000001,retailSourceName="sword_circulous",itemGroup=ItemGroup.SWORD,maxEnchant=15},1,true,ItemSlot.MAIN_HAND.GetSlotIdMask());
        torso.SetTempering(5);sword.SetTempering(11);var equipment=new Equipment(null!);
        var field=typeof(Equipment).GetField("equipment",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var slots=Assert.IsType<SortedDictionary<long,Item>>(field.GetValue(equipment));
        slots.Add(ItemSlot.TORSO.GetSlotIdMask(),torso);slots.Add(ItemSlot.MAIN_HAND.GetSlotIdMask(),sword);
        var snapshot=RetailNpcCurseEquipmentObservation.Capture(equipment,1,33,armor,weaponHp:weapon,authorizationHp:auth);
        Assert.False(snapshot.GameplayReady);Assert.Equal(2,snapshot.Items.Length);
        foreach(var entry in snapshot.Items){Assert.Equal(RetailNpcCurseAuthorizationHpSource.Failure.None,entry.AuthorizationStatus);Assert.Equal(0,entry.AuthorizationHp);}
    }

    [SkippableFact]
    public void ExpandedCandidateCatalogInstallsAndRestoresAllEquipmentSources()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_AUTHORIZATION_EXPANDED_HP_MANIFEST");
        Skip.If(string.IsNullOrEmpty(path),"Requires expanded candidate manifest");
        byte[] bytes=File.ReadAllBytes(path!);string hash=Convert.ToHexStringLower(SHA256.HashData(bytes));
        var previous=RetailNpcStatCatalog.CurseAuthorizationHpSource;
        using(RetailNpcStatCatalog.InstallForTests(bytes,hash,Path.GetDirectoryName(path!)!))
        {
            Assert.Equal(54153,RetailNpcStatCatalog.CurseAuthorizationHpSource!.ItemCount);
            Assert.Equal(25124,RetailNpcStatCatalog.CurseArmorSource!.ItemCount);
            Assert.Equal(17780,RetailNpcStatCatalog.CurseWeaponHpSource!.ItemCount);
            Assert.Equal(11249,RetailNpcStatCatalog.CurseOtherEquipmentHpSource!.ItemCount);
        }
        Assert.Same(previous,RetailNpcStatCatalog.CurseAuthorizationHpSource);
    }
}
