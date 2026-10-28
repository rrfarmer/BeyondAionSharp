using System.Security.Cryptography;
using System.Text.Json;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Retail;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcArmorDefenseSourceTests
{
    private const string CatalogSha="ebd610658fd1225d1ec262cbf2971ba5a9d4954c8004f7739fe0456422e6a7ba";

    [SkippableFact]
    public void MatchedArmorSourcesProvideExplicitDefenseAndRejectUnreviewedItems()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_17860_ARMOR_SOURCE");
        Skip.If(string.IsNullOrWhiteSpace(path),"Requires the pinned selected armor-defense source");
        byte[] bytes=File.ReadAllBytes(path!);var source=RetailNpcArmorDefenseSource.ParseForOfflineFixture(bytes,CatalogSha);
        Assert.Equal(25124,source.ItemCount);Assert.Equal(20,source.TableCount);
        Assert.Equal(RetailNpcArmorDefenseSource.Failure.None,source.Lookup(110100420,"RB_TORSO_N_U1_P_33A",15,out var armor));
        Assert.NotNull(armor);Assert.Equal((94,45,15,15),(armor.RawPhysicalDefense,armor.EnchantPhysicalDefense,armor.HostMaxEnchant,armor.RetailMaxEnchant));
        Assert.False(armor.EquipmentEligibilityVerified);
        Assert.Equal(RetailNpcArmorDefenseSource.Failure.IdentityMismatch,source.Lookup(110100420,"different",15,out _));
        Assert.Equal(RetailNpcArmorDefenseSource.Failure.UnknownItem,source.Lookup(-1,"unknown",0,out _));
        Assert.Equal(RetailNpcArmorDefenseSource.Failure.UnreviewedEnchant,source.Lookup(110100420,"rb_torso_n_u1_p_33a",16,out _));
        var changed=(byte[])bytes.Clone();changed[^1]^=1;
        Assert.Throws<InvalidDataException>(()=>RetailNpcArmorDefenseSource.ParseForOfflineFixture(changed,CatalogSha));
        int revised=RetailNpcArmorDefenseSource.RevisedPhysicalDefense(armor,33,23,true,RetailNpcArmorDefenseSource.Restriction.Computed);
        Assert.Equal(120,revised);
        Assert.Equal(1988,RetailNpcPlayerDefenseDrainProjection.PhysicalDefense(2000,(uint)revised,-10,0));
    }

    [SkippableFact]
    public void PhysicalArmorRevisionMatchesNativeFloatAndRestrictionCases()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_17860_ARMOR_PROOF");
        Skip.If(string.IsNullOrWhiteSpace(path),"Requires the pinned retail armor revision proof");
        byte[] bytes=File.ReadAllBytes(path!);
        Assert.Equal("3a8239562e794ace46ebcd0cb7f1b898c2cdbb44052f836db9d08608298dd3c8",Convert.ToHexStringLower(SHA256.HashData(bytes)));
        using var doc=JsonDocument.Parse(bytes);var rows=doc.RootElement.GetProperty("cases");Assert.Equal(100,rows.GetArrayLength());
        foreach(var row in rows.EnumerateArray())
        {
            var item=new RetailNpcArmorDefenseSource.Candidate(1,"explicit",row.GetProperty("raw").GetInt32(),
                row.GetProperty("enchant").GetInt32(),15,15,"explicit");
            var actual=RetailNpcArmorDefenseSource.RevisedPhysicalDefense(item,row.GetProperty("required").GetUInt16(),
                row.GetProperty("player").GetUInt16(),row.GetProperty("enabled").GetBoolean(),
                (RetailNpcArmorDefenseSource.Restriction)row.GetProperty("mode").GetInt32());
            Assert.Equal(row.GetProperty("result").GetInt32(),actual);
        }
        var example=new RetailNpcArmorDefenseSource.Candidate(1,"explicit",94,45,15,15,"explicit");
        Assert.Equal(139,RetailNpcArmorDefenseSource.RevisedPhysicalDefense(example,33,23,false,RetailNpcArmorDefenseSource.Restriction.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(()=>RetailNpcArmorDefenseSource.RevisedPhysicalDefense(example,33,23,true,(RetailNpcArmorDefenseSource.Restriction)99));
    }
}
