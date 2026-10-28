using System.Collections.Immutable;
using System.Reflection;
using System.Security.Cryptography;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Items.Enums;
using Aion.GameServer.Retail;
using Resource=Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcCurseStatMath.Resource;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public class RetailNpcCurseItemBonusSourceTests
{
    [Fact]
    public void RevisionSelectionUsesNativeServerPlayerAndBonusFlags()
    {
        Assert.False(RetailNpcCurseItemBonusSource.InitialHighLevelFlag(65,66));
        Assert.True(RetailNpcCurseItemBonusSource.InitialHighLevelFlag(66,66));
        Assert.True(RetailNpcCurseItemBonusSource.InitialHighLevelFlag(67,66));
        var ordinary=new RetailNpcCurseItemBonusSource.RevisionContext(0,true,1,false,0x20);
        Assert.False(RetailNpcCurseItemBonusSource.SelectRevision(ordinary));
        Assert.True(RetailNpcCurseItemBonusSource.SelectRevision(ordinary with {HighLevelFlag=true}));
        Assert.False(RetailNpcCurseItemBonusSource.SelectRevision(ordinary with {ActorPresent=false,ActorType=null,HighLevelFlag=null}));
        Assert.False(RetailNpcCurseItemBonusSource.SelectRevision(ordinary with {ActorType=2,HighLevelFlag=null}));
        Assert.Null(RetailNpcCurseItemBonusSource.SelectRevision(ordinary with {HighLevelFlag=null}));
        Assert.Null(RetailNpcCurseItemBonusSource.SelectRevision(ordinary with {SpecialServerType=null}));
        Assert.True(RetailNpcCurseItemBonusSource.SelectRevision(ordinary with {SpecialServerType=1,HighLevelFlag=null}));
        Assert.True(RetailNpcCurseItemBonusSource.SelectRevision(ordinary with {SpecialServerType=10,HighLevelFlag=null}));
        Assert.Null(RetailNpcCurseItemBonusSource.SelectRevision(ordinary with {SpecialServerType=9}));
        foreach(int flag in new[]{1,4,0x800,0x805})
            Assert.False(RetailNpcCurseItemBonusSource.SelectRevision(ordinary with {SpecialServerType=1,BonusFlags=flag}));
        Assert.Null(RetailNpcCurseItemBonusSource.SelectRevision(ordinary with {BonusFlags=null}));
    }

    [Fact]
    public void ChargeStagesAndRevisionAreAppliedSeparately()
    {
        var field=new RetailNpcCurseItemBonusSource.Field(ImmutableArray.CreateRange(new RetailNpcCurseItemBonusSource.Operand?[]{
            new(100,false,"bonus_attr1"),new(10,true,"bonus_attr_a1"),new(-10,false,"bonus_attr_b1")}),"alternate_retail_list");
        var candidate=new RetailNpcCurseItemBonusSource.Candidate(1,"item","SWORD","weapon","equip",field,field);
        foreach(var (points,flat,percent) in new[]{(0,100,0),(1,100,10),(500000,100,10),(500001,90,10),(1000000,90,10)})
        {
            Assert.Equal(RetailNpcCurseItemBonusSource.Failure.None,candidate.Resolve(Resource.Hp,points,33,23,false,false,out var result));
            Assert.Equal(new(flat,percent),result);
        }
        Assert.Equal(RetailNpcCurseItemBonusSource.Failure.None,candidate.Resolve(Resource.Mp,500001,33,23,true,true,out var revised));
        Assert.Equal(new(72,8),revised);
        Assert.Equal(RetailNpcCurseItemBonusSource.Failure.UnreviewedCharge,candidate.Resolve(Resource.Hp,-1,33,23,false,false,out _));
        Assert.Equal(RetailNpcCurseItemBonusSource.Failure.UnreviewedCharge,candidate.Resolve(Resource.Hp,1000001,33,23,false,false,out _));
        Assert.Equal(RetailNpcCurseItemBonusSource.Failure.UnreviewedApplication,(candidate with {Apply="inventory"}).Resolve(Resource.Hp,0,33,23,false,false,out _));
        var normal=new RetailNpcCurseItemBonusSource.RevisionContext(0,true,1,false,0x20);
        var charge=normal with {BonusFlags=0x10};
        Assert.Equal(RetailNpcCurseItemBonusSource.Failure.None,candidate.ResolveSelected(Resource.Hp,500001,33,23,normal,charge,out var unadjusted));
        Assert.Equal(new(90,10),unadjusted);
        var high=normal with {HighLevelFlag=true};
        Assert.Equal(RetailNpcCurseItemBonusSource.Failure.None,candidate.ResolveSelected(Resource.Hp,500001,33,23,high,high with {BonusFlags=0x10},out var selected));
        Assert.Equal(new(72,8),selected);
        Assert.Equal(RetailNpcCurseItemBonusSource.Failure.UnreviewedRevisionContext,
            candidate.ResolveSelected(Resource.Hp,500001,33,23,normal,charge with {HighLevelFlag=null},out _));
        Assert.Equal(RetailNpcCurseItemBonusSource.Failure.UnreviewedRevisionContext,
            candidate.ResolveSelected(Resource.Hp,0,33,23,normal with {HighLevelFlag=null},default,out _));
        Assert.Equal(RetailNpcCurseItemBonusSource.Failure.None,candidate.ResolveSelected(Resource.Hp,0,33,23,normal,default,out var noCharge));
        Assert.Equal(new(100,0),noCharge);
    }

    [Fact]
    public void FusedNormalAndChargeUseTheirOwnNativeLevelInputs()
    {
        var field=new RetailNpcCurseItemBonusSource.Field(ImmutableArray.CreateRange(new RetailNpcCurseItemBonusSource.Operand?[]{
            new(100,false,"bonus_attr1"),new(50,false,"bonus_attr_a1"),null}),"alternate_retail_list");
        var fused=new RetailNpcCurseItemBonusSource.Candidate(1,"fused","GREATSWORD","weapon","equip",field,field);
        var normal=new RetailNpcCurseItemBonusSource.RevisionContext(0,true,1,true,0x80);
        var charge=normal with {BonusFlags=0x200};
        var levels=new RetailNpcCurseItemBonusSource.FusionLevels(40,20,40,20);
        Assert.Equal(RetailNpcCurseItemBonusSource.Failure.None,fused.ResolveFusedSelected(Resource.Hp,1,10,levels,normal,charge,out var value));
        Assert.Equal(new(99,0),value); // Native float rounding makes the 50-point conditioning term 19.
        var fallback=new RetailNpcCurseItemBonusSource.FusionLevels(0,30,40,30);
        Assert.Equal(RetailNpcCurseItemBonusSource.Failure.None,fused.ResolveFusedSelected(Resource.Hp,1,10,fallback,normal,charge,out value));
        Assert.Equal(new(69,0),value); // Each branch falls back to its own raw level and uses retail float rounding.
        Assert.Equal(RetailNpcCurseItemBonusSource.Failure.None,fused.ResolveFusedSelected(Resource.Hp,0,10,levels,normal,default,out value));
        Assert.Equal(new(80,0),value);
        Assert.Equal(RetailNpcCurseItemBonusSource.Failure.UnreviewedRevisionContext,
            fused.ResolveFusedSelected(Resource.Hp,1,10,levels,normal with {BonusFlags=0x100},charge,out _));
        Assert.Equal(RetailNpcCurseItemBonusSource.Failure.UnreviewedRevisionContext,
            fused.ResolveFusedSelected(Resource.Hp,1,10,levels,normal,charge with {BonusFlags=2},out _));
        Assert.Equal(RetailNpcCurseItemBonusSource.Failure.UnreviewedCharge,
            fused.ResolveFusedSelected(Resource.Hp,-1,10,levels,normal,charge,out _));
    }

    [SkippableFact]
    public void PinnedTwoHandedFusionUsesRetailNormalAndChargeLevels()
    {
        string? bonusPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_ITEM_BONUS_FIXTURE");
        string? permitPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_CLASS_PERMIT_FIXTURE");
        Skip.If(string.IsNullOrEmpty(bonusPath)||string.IsNullOrEmpty(permitPath),"Requires pinned retail item sources");
        byte[] a=File.ReadAllBytes(bonusPath!),b=File.ReadAllBytes(permitPath!);
        var source=RetailNpcCurseItemBonusSource.Parse(a,Convert.ToHexStringLower(SHA256.HashData(a)));
        var permits=RetailNpcCurseClassPermitSource.Parse(b,Convert.ToHexStringLower(SHA256.HashData(b)));
        var main=new RetailNpcCurseWeaponHpSource.Candidate(100900898,"2hsword_n_l1_r_60a","GREATSWORD","2hsword",10,false);
        var secondary=new RetailNpcCurseWeaponHpSource.Candidate(100900855,"2hsword_n_e1_55c","GREATSWORD","2hsword",15,false);
        Assert.True(main.IsRetailTwoHanded);Assert.True(secondary.IsRetailTwoHanded);
        Assert.Equal(RetailNpcCurseItemBonusSource.Failure.None,source.Lookup(main,out var mainBonus));
        Assert.Equal(RetailNpcCurseItemBonusSource.Failure.None,source.Lookup(secondary,out var secondaryBonus));
        Assert.True(permits.TryLookup(mainBonus!,1,out byte mainLevel,out _));
        Assert.True(permits.TryLookup(secondaryBonus!,1,out byte secondaryLevel,out _));Assert.Equal((60,55),(mainLevel,secondaryLevel));
        var normal=new RetailNpcCurseItemBonusSource.RevisionContext(0,true,1,true,0x80);
        var charge=normal with {BonusFlags=0x200};
        var levels=new RetailNpcCurseItemBonusSource.FusionLevels(mainLevel,secondaryLevel,mainLevel,secondaryLevel);
        Assert.Equal(RetailNpcCurseItemBonusSource.Failure.None,secondaryBonus!.ResolveFusedSelected(Resource.Hp,1,50,levels,normal,charge,out var fused));
        Assert.Equal(new(363,0),fused); // 312 normal plus 51 conditioning, after native truncation.
        Assert.Equal(RetailNpcCurseItemBonusSource.Failure.None,mainBonus!.ResolveSelected(Resource.Hp,1,mainLevel,50,
            normal with {BonusFlags=0x100},charge,out var own));
        Assert.Equal(new(317,0),own);
    }

    [Fact]
    public void ArmorBonusUsesTheMatchingClassPermitAndExplicitAdjustment()
    {
        var field=new RetailNpcCurseItemBonusSource.Field(ImmutableArray.CreateRange(new RetailNpcCurseItemBonusSource.Operand?[]{
            new(100,false,"bonus_attr1"),new(10,true,"bonus_attr_a1"),null}),"alternate_retail_list");
        var bonus=new RetailNpcCurseItemBonusSource.Candidate(1,"robe","RB_TORSO","armor","equip",field,field);
        var permits=ImmutableArray.CreateRange(Enumerable.Range(0,17).Select(i=>(byte)(i==1?33:0)));
        var armor=new RetailNpcCurseArmorSource.Candidate(1,"robe",0,false,0,15,null,"rb_torso",permits,2);
        var normal=new RetailNpcCurseItemBonusSource.RevisionContext(0,true,1,true,0x20);
        var charge=normal with {BonusFlags=0x200};
        Assert.Equal(RetailNpcCurseItemBonusSource.Failure.None,
            bonus.ResolveArmorSelected(armor,1,0,null,Resource.Hp,1,23,normal,charge,out var full));
        Assert.Equal(new(80,8),full);
        Assert.Equal(RetailNpcCurseItemBonusSource.Failure.None,
            bonus.ResolveArmorSelected(armor,1,2,10,Resource.Hp,1,23,normal,charge,out var adjusted));
        Assert.Equal(new(84,8),adjusted);
        Assert.Equal(RetailNpcCurseItemBonusSource.Failure.UnreviewedRevisionContext,
            bonus.ResolveArmorSelected(armor,1,null,10,Resource.Hp,1,23,normal,charge,out _));
        Assert.Equal(RetailNpcCurseItemBonusSource.Failure.UnreviewedRevisionContext,
            bonus.ResolveArmorSelected(armor,1,2,null,Resource.Hp,1,23,normal,charge,out _));
        Assert.Equal(RetailNpcCurseItemBonusSource.Failure.UnreviewedRevisionContext,
            bonus.ResolveArmorSelected(armor,17,0,null,Resource.Hp,1,23,normal,charge,out _));
        Assert.Equal(RetailNpcCurseItemBonusSource.Failure.IdentityMismatch,
            bonus.ResolveArmorSelected(armor with {ItemId=2},1,0,null,Resource.Hp,1,23,normal,charge,out _));
        Assert.Equal(RetailNpcCurseItemBonusSource.Failure.UnreviewedRevisionContext,
            bonus.ResolveArmorSelected(armor,1,0,null,Resource.Hp,1,23,normal with {BonusFlags=2},charge,out _));
        Assert.Equal(RetailNpcCurseItemBonusSource.Failure.UnreviewedRevisionContext,
            bonus.ResolveArmorSelected(armor,1,0,null,Resource.Hp,1,23,normal,charge with {BonusFlags=2},out _));
        // An omitted permit is native zero and needs no adjustment or floor.
        Assert.Equal(RetailNpcCurseItemBonusSource.Failure.None,
            bonus.ResolveArmorSelected(armor,0,null,null,Resource.Hp,0,23,normal,default,out var zeroPermit));
        Assert.Equal(new(100,0),zeroPermit);
    }

    [SkippableFact]
    public void PinnedArmorPermitFeedsTheSelectedBonusRevision()
    {
        string? bonusPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_ITEM_BONUS_FIXTURE");
        string? armorPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_ARMOR_FIXTURE");
        Skip.If(string.IsNullOrEmpty(bonusPath)||string.IsNullOrEmpty(armorPath),"Requires pinned retail armor and bonus sources");
        byte[] a=File.ReadAllBytes(bonusPath!),b=File.ReadAllBytes(armorPath!);
        var bonusSource=RetailNpcCurseItemBonusSource.Parse(a,Convert.ToHexStringLower(SHA256.HashData(a)));
        var armorSource=RetailNpcCurseArmorSource.Parse(b,Convert.ToHexStringLower(SHA256.HashData(b)));
        Assert.Equal(RetailNpcCurseArmorSource.Failure.None,armorSource.Lookup(110100420,"rb_torso_n_u1_p_33a",0,out var armor));
        Assert.Equal(RetailNpcCurseItemBonusSource.Failure.None,bonusSource.Lookup(armor!,out var bonus));
        var context=new RetailNpcCurseItemBonusSource.RevisionContext(0,true,1,true,0x20);
        Assert.Equal(RetailNpcCurseItemBonusSource.Failure.None,
            bonus!.ResolveArmorSelected(armor!,1,0,null,Resource.Mp,0,23,context,default,out var value));
        Assert.Equal(new(457,0),value);
    }

    [SkippableFact]
    public void PinnedSourceUsesRetailDifferencesAndBothChargeLists()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_ITEM_BONUS_FIXTURE");
        Skip.If(string.IsNullOrEmpty(path),"Requires pinned retail item bonus fixture");
        byte[] bytes=File.ReadAllBytes(path!);string hash=Convert.ToHexStringLower(SHA256.HashData(bytes));
        var source=RetailNpcCurseItemBonusSource.Parse(bytes,hash);Assert.Equal(54153,source.ItemCount);
        var book=new RetailNpcCurseWeaponHpSource.Candidate(100601492,"world_book_n_e1_65b","SPELLBOOK","book",15,false);
        Assert.Equal(RetailNpcCurseItemBonusSource.Failure.None,source.Lookup(book,out var bookSource));Assert.NotNull(bookSource);
        Assert.Equal(RetailNpcCurseItemBonusSource.Failure.None,bookSource.Resolve(Resource.Mp,0,65,65,false,false,out var bookMp));
        Assert.Equal(new(828,0),bookMp);Assert.Equal("fields_differ",bookSource.Mp.ComparisonStatus);
        var sword=new RetailNpcCurseWeaponHpSource.Candidate(100001286,"sword_n_e1_pvp_55b","SWORD","sword",15,false);
        Assert.Equal(RetailNpcCurseItemBonusSource.Failure.None,source.Lookup(sword,out var swordSource));Assert.NotNull(swordSource);
        foreach(var (points,hp) in new[]{(0,283),(500000,337),(500001,364)})
        {Assert.Equal(RetailNpcCurseItemBonusSource.Failure.None,swordSource.Resolve(Resource.Hp,points,55,55,false,false,out var value));Assert.Equal(hp,value.Points);}
        var ordinary=new RetailNpcCurseItemBonusSource.RevisionContext(0,true,1,false,0x20);
        Assert.Equal(RetailNpcCurseItemBonusSource.Failure.None,swordSource.ResolveSelected(Resource.Hp,0,55,45,ordinary,default,out var unchanged));
        Assert.Equal(283,unchanged.Points);
        Assert.Equal(RetailNpcCurseItemBonusSource.Failure.None,swordSource.ResolveSelected(Resource.Hp,0,55,45,ordinary with {HighLevelFlag=true},default,out var highLevel));
        Assert.Equal(226,highLevel.Points);
        Assert.Equal(RetailNpcCurseItemBonusSource.Failure.IdentityMismatch,source.Lookup(sword with {Name="changed"},out _));
        Assert.Equal(RetailNpcCurseItemBonusSource.Failure.UnknownItem,source.Lookup(sword with {ItemId=1},out _));
        Assert.Throws<InvalidDataException>(()=>RetailNpcCurseItemBonusSource.Parse(bytes,new string('0',64)));
    }

    [SkippableFact]
    public void OfflineObservationCapturesChargeAndDoesNotClaimReadiness()
    {
        string? bonusPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_ITEM_BONUS_FIXTURE");
        string? weaponPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_WEAPON_HP_FIXTURE");
        Skip.If(string.IsNullOrEmpty(bonusPath)||string.IsNullOrEmpty(weaponPath),"Requires reviewed item sources");
        byte[] a=File.ReadAllBytes(bonusPath!),b=File.ReadAllBytes(weaponPath!);
        string Hash(byte[] x)=>Convert.ToHexStringLower(SHA256.HashData(x));
        var source=RetailNpcCurseItemBonusSource.Parse(a,Hash(a));var weapon=RetailNpcCurseWeaponHpSource.Parse(b,Hash(b));
        var item=new Item(900001,new ItemTemplate {itemId=100001286,retailSourceName="sword_n_e1_pvp_55b",itemGroup=ItemGroup.SWORD,maxEnchant=15},1,true,ItemSlot.MAIN_HAND.GetSlotIdMask());
        var chargeField=typeof(Item).GetField("conditioningInfo",BindingFlags.Instance|BindingFlags.NonPublic)!;
        chargeField.SetValue(item,new ChargeInfo(500001,item));var equipment=new Equipment(null!);
        var slotsField=typeof(Equipment).GetField("equipment",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var slots=Assert.IsType<SortedDictionary<long,Item>>(slotsField.GetValue(equipment));slots.Add(ItemSlot.MAIN_HAND.GetSlotIdMask(),item);
        var snapshot=RetailNpcCurseEquipmentObservation.Capture(equipment,1,55,null,weaponHp:weapon,itemBonus:source);
        Assert.False(snapshot.GameplayReady);var row=Assert.Single(snapshot.Items);Assert.Equal(500001,row.ChargePoints);
        Assert.False(row.HasFusion);Assert.False(row.HasRandomBonus);Assert.False(row.HasSocketOrIdian);
        Assert.Equal(RetailNpcCurseItemBonusSource.Failure.None,row.ItemBonusStatus);Assert.NotNull(row.ItemBonus);
        Assert.Equal(RetailNpcCurseItemBonusSource.Failure.None,row.ItemBonus.Resolve(Resource.Hp,row.ChargePoints,55,55,false,false,out var value));Assert.Equal(364,value.Points);
        var missing=Assert.Single(RetailNpcCurseEquipmentObservation.Capture(equipment,1,55,null,weaponHp:weapon).Items);
        Assert.Equal(RetailNpcCurseItemBonusSource.Failure.SourceUnavailable,missing.ItemBonusStatus);Assert.Null(missing.ItemBonus);
        var fusionField=typeof(Item).GetField("fusionedItemTemplate",BindingFlags.Instance|BindingFlags.NonPublic)!;
        fusionField.SetValue(item,new ItemTemplate());
        var fused=Assert.Single(RetailNpcCurseEquipmentObservation.Capture(equipment,1,55,null,weaponHp:weapon,itemBonus:source).Items);
        Assert.True(fused.HasFusion);Assert.Equal(RetailNpcCurseItemBonusSource.Failure.None,fused.ItemBonusStatus);Assert.NotNull(fused.ItemBonus);
        Assert.Equal(RetailNpcCurseWeaponHpSource.Failure.UnknownItem,fused.Fusion!.WeaponStatus);Assert.Null(fused.Fusion.Bonus);
        fusionField.SetValue(item,null);chargeField.SetValue(item,new ChargeInfo(-1,item));
        var invalid=Assert.Single(RetailNpcCurseEquipmentObservation.Capture(equipment,1,55,null,weaponHp:weapon,itemBonus:source).Items);
        Assert.Equal(RetailNpcCurseItemBonusSource.Failure.UnreviewedCharge,invalid.ItemBonusStatus);Assert.Null(invalid.ItemBonus);
    }

    [SkippableFact]
    public void OfflineCandidateCatalogInstallsAndRestoresPinnedBonusSource()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_ITEM_BONUS_MANIFEST");
        Skip.If(string.IsNullOrEmpty(path),"Requires offline bonus candidate manifest");
        byte[] bytes=File.ReadAllBytes(path!);string hash=Convert.ToHexStringLower(SHA256.HashData(bytes));
        var previous=RetailNpcStatCatalog.CurseItemBonusSource;
        using(RetailNpcStatCatalog.InstallForTests(bytes,hash,Path.GetDirectoryName(path!)!))
        {
            Assert.Equal(54153,RetailNpcStatCatalog.CurseItemBonusSource!.ItemCount);
            Assert.Equal(54153,RetailNpcStatCatalog.CurseAuthorizationHpSource!.ItemCount);
        }
        Assert.Same(previous,RetailNpcStatCatalog.CurseItemBonusSource);
    }
}
