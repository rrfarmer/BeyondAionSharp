using System.Security.Cryptography;
using System.Text.Json;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Items.Enums;
using Aion.GameServer.Retail;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public class RetailNpcCurseOffhandSourceTests
{
    internal static byte[] Fixture() => JsonSerializer.SerializeToUtf8Bytes(new {
        version=1,purpose="retail-offhand-weapon-identity-v1",region="USA",
        provenance=new {retail_items_sha256=RetailNpcCurseOffhandSource.ReviewedWeaponsSha,
            host_items_sha256=RetailNpcCurseOffhandSource.ReviewedHostItemsSha,
            retail_selection="USA missing; base fallback"},
        items=new[]{new {id=100000001,name="sword_a",host_group="SWORD"}}
    });
    internal static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    internal static RetailNpcCurseOffhandSource Source(){byte[] bytes=Fixture();return RetailNpcCurseOffhandSource.Parse(bytes,Hash(bytes));}

    [Fact]
    public void LookupRequiresMatchingIdNameAndHostWeaponGroup()
    {
        var source=Source();var item=new ItemTemplate {itemId=100000001,retailSourceName="SWORD_A",itemGroup=ItemGroup.SWORD};
        Assert.Equal(RetailNpcCurseOffhandSource.Failure.None,source.Lookup(100000001,item));
        Assert.Equal(RetailNpcCurseOffhandSource.Failure.UnknownItem,source.Lookup(100000002,item));
        item.retailSourceName="other";Assert.Equal(RetailNpcCurseOffhandSource.Failure.IdentityMismatch,source.Lookup(100000001,item));
        item.retailSourceName="sword_a";item.itemGroup=ItemGroup.DAGGER;
        Assert.Equal(RetailNpcCurseOffhandSource.Failure.HostTemplateDrift,source.Lookup(100000001,item));
        item.itemGroup=ItemGroup.SWORD;item.itemId=100000002;
        Assert.Equal(RetailNpcCurseOffhandSource.Failure.HostTemplateDrift,source.Lookup(100000001,item));
    }

    [Fact]
    public void ParserRejectsChangedHashAndDuplicateId()
    {
        byte[] bytes=Fixture();Assert.Throws<InvalidDataException>(()=>RetailNpcCurseOffhandSource.Parse(bytes,new string('0',64)));
        string duplicate=System.Text.Encoding.UTF8.GetString(bytes).Replace(
            "\"items\":[{", "\"items\":[{\"id\":100000001,\"name\":\"sword_a\",\"host_group\":\"SWORD\"},{");
        byte[] changed=System.Text.Encoding.UTF8.GetBytes(duplicate);
        Assert.Throws<InvalidDataException>(()=>RetailNpcCurseOffhandSource.Parse(changed,Hash(changed)));
    }

    [SkippableFact]
    public void ReviewedRealSourceLoadsAllIdentities()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_OFFHAND_FIXTURE");
        Skip.If(string.IsNullOrEmpty(path),"Requires reviewed off-hand source fixture");
        byte[] bytes=File.ReadAllBytes(path!);
        Assert.Equal("029a5388abcad6c7f750f0771f7096eb0a846f41b7b453ea8a6becf6f80a959a",Hash(bytes));
        var source=RetailNpcCurseOffhandSource.Parse(bytes,Hash(bytes));Assert.Equal(5968,source.ItemCount);
    }

    [SkippableFact]
    public void CandidateManifestLoadsOptionalSourceAndRestoresCatalog()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_OFFHAND_MANIFEST");
        Skip.If(string.IsNullOrEmpty(path),"Requires offline candidate manifest");
        byte[] bytes=File.ReadAllBytes(path!);var previous=RetailNpcStatCatalog.CurseOffhandSource;
        using(RetailNpcStatCatalog.InstallForTests(bytes,Hash(bytes),Path.GetDirectoryName(path!)!))
            Assert.Equal(5968,RetailNpcStatCatalog.CurseOffhandSource!.ItemCount);
        Assert.Same(previous,RetailNpcStatCatalog.CurseOffhandSource);
    }
}
