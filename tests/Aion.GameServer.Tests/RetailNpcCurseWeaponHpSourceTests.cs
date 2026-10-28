using System.Security.Cryptography;
using System.Text.Json;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Items.Enums;
using Aion.GameServer.Retail;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public class RetailNpcCurseWeaponHpSourceTests
{
    internal static byte[] Fixture()=>JsonSerializer.SerializeToUtf8Bytes(new {
        version=1,purpose="retail-default-weapon-hp-base-v1",region="USA",
        provenance=new {retail_items_sha256=RetailNpcCurseOffhandSource.ReviewedWeaponsSha,
            retail_enchant_sha256=RetailNpcCurseArmorSource.ReviewedRetailEnchantsSha,
            host_items_sha256=RetailNpcCurseArmorSource.ReviewedHostItemsSha,
            retail_selection="USA missing; base fallback",enchant_selection="USA missing; base fallback"},
        items=new[]{new {id=100000001,name="sword_a",host_group="SWORD",table="sword",host_max_enchant=15,max_hp_explicit=false}}
    });
    internal static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    internal static RetailNpcCurseWeaponHpSource Source(){byte[] b=Fixture();return RetailNpcCurseWeaponHpSource.Parse(b,Hash(b));}

    [Fact]
    public void ZeroInputsRequireMatchingIdentityDefaultTableAndReviewedLevel()
    {
        var source=Source();var item=new ItemTemplate {itemId=100000001,retailSourceName="SWORD_A",itemGroup=ItemGroup.SWORD,maxEnchant=15};
        Assert.Equal(RetailNpcCurseWeaponHpSource.Failure.None,source.Lookup(100000001,item,15,out var row));
        Assert.Equal(0,row!.RawMaxHp);Assert.Equal(0,row.EnchantMaxHp);Assert.False(row.MaxHpExplicit);
        Assert.Equal(RetailNpcCurseWeaponHpSource.Failure.UnknownItem,source.Lookup(100000002,item,0,out _));
        Assert.Equal(RetailNpcCurseWeaponHpSource.Failure.UnreviewedEnchantLevel,source.Lookup(100000001,item,16,out _));
        item.retailSourceName="other";Assert.Equal(RetailNpcCurseWeaponHpSource.Failure.IdentityMismatch,source.Lookup(100000001,item,0,out _));
        item.retailSourceName="sword_a";item.enchantName="custom";
        Assert.Equal(RetailNpcCurseWeaponHpSource.Failure.HostTemplateDrift,source.Lookup(100000001,item,0,out _));
        item.enchantName=null!;item.itemGroup=ItemGroup.DAGGER;
        Assert.Equal(RetailNpcCurseWeaponHpSource.Failure.HostTemplateDrift,source.Lookup(100000001,item,0,out _));
    }

    [Fact]
    public void SourceParserRejectsChangedHashAndWrongTable()
    {
        byte[] b=Fixture();Assert.Throws<InvalidDataException>(()=>RetailNpcCurseWeaponHpSource.Parse(b,new string('0',64)));
        string wrong=System.Text.Encoding.UTF8.GetString(b).Replace("\"table\":\"sword\"","\"table\":\"dagger\"");
        byte[] changed=System.Text.Encoding.UTF8.GetBytes(wrong);
        Assert.Throws<InvalidDataException>(()=>RetailNpcCurseWeaponHpSource.Parse(changed,Hash(changed)));
    }

    [SkippableFact]
    public void SelectedWeaponSourceContainsAllReviewedRows()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_WEAPON_HP_FIXTURE");
        Skip.If(string.IsNullOrEmpty(path),"Requires reviewed weapon HP fixture");byte[] b=File.ReadAllBytes(path!);
        Assert.Equal("09c3411e58241d3b51b82d676841e4f2b09d29fffeb2114eeae7e115c8f6a53a",Hash(b));
        Assert.Equal(17780,RetailNpcCurseWeaponHpSource.Parse(b,Hash(b)).ItemCount);
    }

    [SkippableFact]
    public void OptionalManifestLoadsAndRestoresAllCurseSources()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_WEAPON_HP_MANIFEST");
        Skip.If(string.IsNullOrEmpty(path),"Requires offline candidate manifest");byte[] b=File.ReadAllBytes(path!);
        var previous=RetailNpcStatCatalog.CurseWeaponHpSource;
        using(RetailNpcStatCatalog.InstallForTests(b,Hash(b),Path.GetDirectoryName(path!)!))
        {
            Assert.Equal(17780,RetailNpcStatCatalog.CurseWeaponHpSource!.ItemCount);
            Assert.Equal(5968,RetailNpcStatCatalog.CurseOffhandSource!.ItemCount);
            Assert.Equal(25124,RetailNpcStatCatalog.CurseArmorSource!.ItemCount);
        }
        Assert.Same(previous,RetailNpcStatCatalog.CurseWeaponHpSource);
    }
}
