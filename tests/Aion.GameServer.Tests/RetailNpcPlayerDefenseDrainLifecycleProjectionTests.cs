using System.Security.Cryptography;
using System.Text.Json;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Projection=Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcPlayerDefenseDrainLifecycleProjection;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcPlayerDefenseDrainLifecycleProjectionTests
{
    [SkippableFact]
    public void SourceBoundBeginEndAndResumeChangeTheSevenRetailChannels()
    {
        string? manifest=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? proofPath=Environment.GetEnvironmentVariable("AION_RETAIL_17860_LIFECYCLE_PROOF");
        Skip.If(new[]{manifest,proofPath}.Any(string.IsNullOrWhiteSpace),"Requires pinned 5.8 skill and instruction proof");
        byte[] proof=File.ReadAllBytes(proofPath!);
        Assert.Equal("dab5f38ef2b4a9c8ac6d5772750894396e344bea48544d757af84c3b4b56f56c",Convert.ToHexStringLower(SHA256.HashData(proof)));
        using var manifestDoc=JsonDocument.Parse(File.ReadAllBytes(manifest!));
        var asset=manifestDoc.RootElement.GetProperty("skill_sources").EnumerateArray().Single(x=>x.GetProperty("id").GetInt32()==17860);
        byte[] source=File.ReadAllBytes(asset.GetProperty("path").GetString()!);
        var original=new Projection.Vector(20,11,12,13,14,15,16,77);
        var begun=Projection.ApplyForOfflineFixture(source,original,Projection.Invocation.Begin);
        Assert.Equal(new Projection.Vector(10,1,2,3,4,5,6,77),begun);
        Assert.Equal(original,Projection.ApplyForOfflineFixture(source,begun,Projection.Invocation.End));
        Assert.Equal(begun,Projection.ApplyForOfflineFixture(source,original,Projection.Invocation.Resume));
        var zero=new Projection.Vector(0,0,0,0,0,0,0,0);
        var active=Projection.ApplyForOfflineFixture(source,zero,Projection.Invocation.Begin);
        var ended=Projection.ApplyForOfflineFixture(source,active,Projection.Invocation.End);
        Assert.Equal(723,RetailNpcPlayerDefenseDrainProjection.PhysicalDefense(595,395,active.Physical,167));
        Assert.Equal(762,RetailNpcPlayerDefenseDrainProjection.PhysicalDefense(595,395,ended.Physical,167));
        Assert.Equal((short)90,RetailNpcPlayerDefenseDrainProjection.ElementalDefense(100,active.Fire,0,-500,2000));
        Assert.Equal((short)100,RetailNpcPlayerDefenseDrainProjection.ElementalDefense(100,ended.Fire,0,-500,2000));
        var wrapped=original with {Physical=int.MinValue};
        Assert.Equal(unchecked(int.MinValue-10),Projection.ApplyForOfflineFixture(source,wrapped,Projection.Invocation.Begin).Physical);
        Assert.Throws<ArgumentOutOfRangeException>(()=>Projection.ApplyForOfflineFixture(source,original,(Projection.Invocation)999));
        var changed=(byte[])source.Clone();changed[^1]^=1;
        Assert.Throws<InvalidDataException>(()=>Projection.ApplyForOfflineFixture(changed,original,Projection.Invocation.Begin));
    }
}
