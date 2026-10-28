using System.Security.Cryptography;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aion.GameServer.Retail;

namespace Aion.GameServer.Tests;

public class RetailNpcCurseArmorSourceTests
{
    private static byte[] Fixture()
    {
        return JsonSerializer.SerializeToUtf8Bytes(new {
            version=2,purpose="default-armor-hp-and-class-v2",region="USA",
            provenance=new {bindings_sha256=RetailNpcCurseArmorSource.ReviewedBindingsSha,
                retail_items_sha256=RetailNpcCurseArmorSource.ReviewedRetailItemsSha,
                retail_enchant_sha256=RetailNpcCurseArmorSource.ReviewedRetailEnchantsSha,
                host_items_sha256=RetailNpcCurseArmorSource.ReviewedHostItemsSha,
                retail_items_selection="USA missing; base fallback",retail_enchant_selection="USA override"},
            tables=new[]{new {name="rb_torso",threshold=16,hp_levels_1_to_15=Enumerable.Range(1,15).Select(x=>x*24).ToArray()}},
            items=new[]{new {id=110100420,name="robe",table="rb_torso",raw_max_hp=0,max_hp_explicit=false,
                host_max_enchant=15,retail_max_enchant=(int?)null,
                class_permits=Enumerable.Range(0,17).Select(x=>x==1?33:0).ToArray(),class_present_mask=2}}
        });
    }
    private static RetailNpcCurseArmorSource Parse(byte[] bytes)
        =>RetailNpcCurseArmorSource.Parse(bytes,Convert.ToHexStringLower(SHA256.HashData(bytes)));

    [Fact]
    public void LookupReturnsExplicitSourceInputsWithoutClaimingEquipEligibility()
    {
        var source=Parse(Fixture());Assert.Equal(1,source.ItemCount);Assert.Equal(1,source.TableCount);
        Assert.Equal(RetailNpcCurseArmorSource.Failure.None,source.Lookup(110100420,"ROBE",15,out var row));
        Assert.NotNull(row);Assert.Equal(360,row.EnchantMaxHp);Assert.Equal(0,row.RawMaxHp);
        Assert.False(row.EquipmentEligibilityVerified);Assert.False(row.ItemRevisionApplied);
        Assert.True(row.TryGetClassPermit(1,out byte level,out bool present));Assert.Equal(33,level);Assert.True(present);
        Assert.True(row.TryGetClassPermit(0,out level,out present));Assert.Equal(0,level);Assert.False(present);
        Assert.False(row.TryGetClassPermit(17,out _,out _));
        Assert.False((row with {ClassPermits=default}).TryGetClassPermit(0,out _,out _));
        Assert.False(row.MaxHpExplicit);Assert.Null(row.RetailMaxEnchant);Assert.Equal(15,row.HostMaxEnchant);
        Assert.Equal(RetailNpcCurseArmorSource.Failure.None,source.Lookup(110100420,"robe",0,out row));Assert.Equal(0,row!.EnchantMaxHp);
        Assert.Equal(RetailNpcCurseArmorSource.Failure.IdentityMismatch,source.Lookup(110100420,"other",1,out row));Assert.Null(row);
        Assert.Equal(RetailNpcCurseArmorSource.Failure.IdentityMismatch,source.Lookup(110100420,"robé",1,out row));Assert.Null(row);
        Assert.Equal(RetailNpcCurseArmorSource.Failure.UnreviewedEnchantLevel,source.Lookup(110100420,"robe",16,out row));Assert.Null(row);
        Assert.Equal(RetailNpcCurseArmorSource.Failure.UnknownItem,source.Lookup(123,"robe",1,out row));Assert.Null(row);
    }

    [Fact]
    public void RejectsWrongHashDuplicateIdentityAndMissingTable()
    {
        byte[] bytes=Fixture();Assert.Throws<InvalidDataException>(()=>RetailNpcCurseArmorSource.Parse(bytes,new string('0',64)));
        var root=JsonNode.Parse(bytes)!.AsObject();var items=root["items"]!.AsArray();items.Add(items[0]!.DeepClone());
        Assert.Throws<InvalidDataException>(()=>Parse(JsonSerializer.SerializeToUtf8Bytes(root)));
        root=JsonNode.Parse(bytes)!.AsObject();root["items"]![0]!["table"]="other";
        Assert.Throws<InvalidDataException>(()=>Parse(JsonSerializer.SerializeToUtf8Bytes(root)));
        root=JsonNode.Parse(bytes)!.AsObject();root["items"]![0]!["raw_max_hp"]=1;
        Assert.Throws<InvalidDataException>(()=>Parse(JsonSerializer.SerializeToUtf8Bytes(root)));
        root=JsonNode.Parse(bytes)!.AsObject();root["provenance"]!["retail_items_sha256"]=new string('0',64);
        Assert.Throws<InvalidDataException>(()=>Parse(JsonSerializer.SerializeToUtf8Bytes(root)));
        root=JsonNode.Parse(bytes)!.AsObject();root["items"]![0]!["class_present_mask"]=0;
        Assert.Throws<InvalidDataException>(()=>Parse(JsonSerializer.SerializeToUtf8Bytes(root)));
        root=JsonNode.Parse(bytes)!.AsObject();root["items"]![0]!["class_permits"]![1]=100;
        Assert.Throws<InvalidDataException>(()=>Parse(JsonSerializer.SerializeToUtf8Bytes(root)));
    }

    [Fact]
    public void ReviewedRevisionReducesOnlyRawHpAndKeepsEnchantHp()
    {
        var row=new RetailNpcCurseArmorSource.Candidate(1,"robe",100,true,24,15,15,"rb_torso",ImmutableArray.CreateRange(new byte[17]),0);
        Assert.Equal(124,RetailNpcCurseArmorSource.RevisedHp(row,30,20,false,false));
        Assert.Equal(124,RetailNpcCurseArmorSource.RevisedHp(row,30,20,true,true));
        Assert.Equal(124,RetailNpcCurseArmorSource.RevisedHp(row,20,30,true,false));
        Assert.Equal(104,RetailNpcCurseArmorSource.RevisedHp(row,30,20,true,false));
        Assert.Equal(26,RetailNpcCurseArmorSource.RevisedHp(row,100,0,true,false));
        Assert.Equal(122,RetailNpcCurseArmorSource.RevisedHp(row with {RawMaxHp=101},21,20,true,false));
        Assert.Equal(134,RetailNpcCurseArmorSource.RevisedHpWithAuthorization(row,30,30,20,true,false));
        Assert.Equal(154,RetailNpcCurseArmorSource.RevisedHpWithAuthorization(row,30,30,20,false,false));
        Assert.Equal(unchecked(104+int.MaxValue),RetailNpcCurseArmorSource.RevisedHpWithAuthorization(row,int.MaxValue,30,20,true,false));
        Assert.Throws<ArgumentOutOfRangeException>(()=>RetailNpcCurseArmorSource.RevisedHp(row,-1,1,true,false));
        Assert.False(row.EquipmentEligibilityVerified);Assert.False(row.ItemRevisionApplied);
    }

    [Theory]
    [InlineData(0,2,10,0)]
    [InlineData(33,0,10,33)]
    [InlineData(33,2,10,31)]
    [InlineData(33,100,10,10)]
    [InlineData(8,1,10,10)]
    [InlineData(33,-3,10,36)]
    public void ClassPermitUsesExplicitNativeLevelAdjustment(byte permit,sbyte adjustment,ushort floor,int expected)
        =>Assert.Equal(expected,RetailNpcCurseArmorSource.AdjustClassPermit(permit,adjustment,floor));

    [SkippableFact]
    public void SelectedSourceLoadsAllReviewedItemsAndPreservesMissingAndExplicitHp()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_ARMOR_FIXTURE");
        Skip.If(string.IsNullOrEmpty(path),"Requires selected offline retail armor source");
        byte[] bytes=File.ReadAllBytes(path!);
        Assert.Equal("72fa5bf580b1ace67426d6a15081531a75b7760962d601de59eed6fdb62a28ff",Convert.ToHexStringLower(SHA256.HashData(bytes)));
        var source=Parse(bytes);Assert.Equal(25124,source.ItemCount);Assert.Equal(20,source.TableCount);
        Assert.Equal(RetailNpcCurseArmorSource.Failure.None,source.Lookup(110100420,"rb_torso_n_u1_p_33a",15,out var row));
        Assert.Equal(0,row!.RawMaxHp);Assert.False(row.MaxHpExplicit);Assert.Equal(360,row.EnchantMaxHp);
        Assert.True(row.TryGetClassPermit(1,out byte permit,out bool present));Assert.Equal(33,permit);Assert.True(present);
        Assert.Equal(RetailNpcCurseArmorSource.Failure.None,source.Lookup(110101524,"rb_torso_n_u1_cc_65a",1,out row));
        Assert.Equal(42,row!.RawMaxHp);Assert.True(row.MaxHpExplicit);Assert.Equal(24,row.EnchantMaxHp);Assert.Equal(5,row.RetailMaxEnchant);
    }
}
