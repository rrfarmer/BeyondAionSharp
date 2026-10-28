using System.Security.Cryptography;
using System.Text.Json;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Items.Enums;
using Aion.GameServer.Retail;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public class RetailNpcCurseOtherEquipmentHpSourceTests
{
    internal static byte[] Fixture()=>JsonSerializer.SerializeToUtf8Bytes(new {
        version=1,purpose="retail-other-equipment-hp-base-v1",region="USA",
        provenance=new {retail_armors_sha256=RetailNpcCurseArmorSource.ReviewedRetailItemsSha,
            retail_accessories_sha256=RetailNpcCurseOtherEquipmentHpSource.ReviewedAccessoriesSha,
            retail_enchant_sha256=RetailNpcCurseArmorSource.ReviewedRetailEnchantsSha,
            host_items_sha256=RetailNpcCurseArmorSource.ReviewedHostItemsSha,
            armor_selection="USA missing; base fallback",accessory_selection="USA missing; base fallback",enchant_selection="USA missing; base fallback"},
        items=new[]{new {id=115000001,name="shield_a",host_group="SHIELD",host_max_enchant=15,max_enchant_level=15,max_hp_explicit=false},
            new {id=122000001,name="ring_a",host_group="RING",host_max_enchant=0,max_enchant_level=0,max_hp_explicit=true}}
    });
    internal static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    internal static RetailNpcCurseOtherEquipmentHpSource Source(){byte[] b=Fixture();return RetailNpcCurseOtherEquipmentHpSource.Parse(b,Hash(b));}

    [Fact]
    public void ShieldsAndRingsRequireCorrectIdentitySlotAndEnchantState()
    {
        var source=Source();long sub=(long)ItemSlot.SUB_HAND,left=(long)ItemSlot.RING_LEFT,right=(long)ItemSlot.RING_RIGHT;
        var shield=new ItemTemplate {itemId=115000001,retailSourceName="SHIELD_A",itemGroup=ItemGroup.SHIELD,maxEnchant=15};
        Assert.Equal(RetailNpcCurseOtherEquipmentHpSource.Failure.None,source.Lookup(115000001,shield,15,sub,out var row));
        Assert.Equal(0,row!.RawMaxHp);Assert.Equal(0,row.EnchantMaxHp);Assert.False(row.MaxHpExplicit);
        Assert.Equal(RetailNpcCurseOtherEquipmentHpSource.Failure.SlotMismatch,source.Lookup(115000001,shield,0,(long)ItemSlot.MAIN_HAND,out _));
        Assert.Equal(RetailNpcCurseOtherEquipmentHpSource.Failure.UnreviewedEnchantLevel,source.Lookup(115000001,shield,16,sub,out _));
        shield.retailSourceName="changed";Assert.Equal(RetailNpcCurseOtherEquipmentHpSource.Failure.IdentityMismatch,source.Lookup(115000001,shield,0,sub,out _));
        shield.retailSourceName="shield_a";shield.enchantName="custom";
        Assert.Equal(RetailNpcCurseOtherEquipmentHpSource.Failure.HostTemplateDrift,source.Lookup(115000001,shield,0,sub,out _));
        var ring=new ItemTemplate {itemId=122000001,retailSourceName="ring_a",itemGroup=ItemGroup.RING,maxEnchant=0};
        Assert.Equal(RetailNpcCurseOtherEquipmentHpSource.Failure.None,source.Lookup(122000001,ring,0,left,out row));Assert.True(row!.MaxHpExplicit);
        Assert.Equal(RetailNpcCurseOtherEquipmentHpSource.Failure.None,source.Lookup(122000001,ring,0,right,out _));
        Assert.Equal(RetailNpcCurseOtherEquipmentHpSource.Failure.UnreviewedEnchantLevel,source.Lookup(122000001,ring,1,left,out _));
        Assert.Equal(RetailNpcCurseOtherEquipmentHpSource.Failure.UnknownItem,source.Lookup(122000002,ring,0,left,out _));
    }

    [Fact]
    public void ParserRejectsHashAndUnsupportedRange()
    {
        byte[] b=Fixture();Assert.Throws<InvalidDataException>(()=>RetailNpcCurseOtherEquipmentHpSource.Parse(b,new string('0',64)));
        string wrong=System.Text.Encoding.UTF8.GetString(b).Replace("\"max_enchant_level\":15","\"max_enchant_level\":16");
        byte[] changed=System.Text.Encoding.UTF8.GetBytes(wrong);
        Assert.Throws<InvalidDataException>(()=>RetailNpcCurseOtherEquipmentHpSource.Parse(changed,Hash(changed)));
    }

    [SkippableFact]
    public void SelectedOtherEquipmentSourceContainsReviewedRows()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_OTHER_HP_FIXTURE");
        Skip.If(string.IsNullOrEmpty(path),"Requires reviewed equipment HP fixture");byte[] b=File.ReadAllBytes(path!);
        Assert.Equal("22ca15359baded4953cacf6a19ee6e61fd33e92ed05d0cc7f048072df8cd001e",Hash(b));
        Assert.Equal(11249,RetailNpcCurseOtherEquipmentHpSource.Parse(b,Hash(b)).ItemCount);
    }

    [SkippableFact]
    public void CombinedCandidateManifestLoadsAndRestoresSources()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_OTHER_HP_MANIFEST");
        Skip.If(string.IsNullOrEmpty(path),"Requires offline candidate manifest");byte[] b=File.ReadAllBytes(path!);
        var previous=RetailNpcStatCatalog.CurseOtherEquipmentHpSource;
        using(RetailNpcStatCatalog.InstallForTests(b,Hash(b),Path.GetDirectoryName(path!)!))
        {
            Assert.Equal(11249,RetailNpcStatCatalog.CurseOtherEquipmentHpSource!.ItemCount);
            Assert.Equal(17780,RetailNpcStatCatalog.CurseWeaponHpSource!.ItemCount);
            Assert.Equal(5968,RetailNpcStatCatalog.CurseOffhandSource!.ItemCount);
            Assert.Equal(25124,RetailNpcStatCatalog.CurseArmorSource!.ItemCount);
        }
        Assert.Same(previous,RetailNpcStatCatalog.CurseOtherEquipmentHpSource);
    }
}
