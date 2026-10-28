using System.Security.Cryptography;
using System.Text.Json;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcPlayerDefenseDrainProjectionTests
{
    [SkippableFact]
    public void DefenseChannelsMatchPinnedNativeInstructionCases()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_17860_DEFENSE_PROOF");
        Skip.If(string.IsNullOrWhiteSpace(path),"Requires pinned retail 17860 defense proof");
        byte[] raw=File.ReadAllBytes(path!);
        Assert.Equal("a3801f25c88486a73400510d8877857fcb9a0db6a6704d20badf8762ee9a784e",Convert.ToHexStringLower(SHA256.HashData(raw)));
        using var proof=JsonDocument.Parse(raw);var rows=proof.RootElement.GetProperty("cases");Assert.Equal(100,rows.GetArrayLength());
        foreach(var row in rows.EnumerateArray())
        {
            int percent=row.GetProperty("percent").GetInt32(),flat=row.GetProperty("flat").GetInt32();
            Assert.Equal(row.GetProperty("physical").GetInt32(),RetailNpcPlayerDefenseDrainProjection.PhysicalDefense(
                row.GetProperty("current").GetUInt32(),row.GetProperty("armor").GetUInt32(),percent,flat));
            Assert.Equal(row.GetProperty("elemental").GetInt16(),RetailNpcPlayerDefenseDrainProjection.ElementalDefense(
                row.GetProperty("element_base").GetInt16(),percent,flat,row.GetProperty("element_min").GetInt16(),row.GetProperty("element_max").GetInt16()));
        }
        Assert.Equal(1900,RetailNpcPlayerDefenseDrainProjection.PhysicalDefense(2000,1000,-10,0));
        Assert.Equal(0,RetailNpcPlayerDefenseDrainProjection.PhysicalDefense(uint.MaxValue,uint.MaxValue,int.MinValue,int.MinValue));
        Assert.Equal(100000,RetailNpcPlayerDefenseDrainProjection.PhysicalDefense(uint.MaxValue,uint.MaxValue,int.MaxValue,int.MaxValue));
        var host=new AdditionStat(StatEnum.PHYSICAL_DEFENSE,2000,null!);
        new StatRateFunction(StatEnum.PHYSICAL_DEFENSE,-10,true).Apply(host,new HashSet<CalculationType>());
        Assert.Equal(1800,host.GetCurrent()); // Explicit example, not a live player comparison.
        Assert.Equal((short)108,RetailNpcPlayerDefenseDrainProjection.ElementalDefense(120,-10,0,-500,2000));
        Assert.Throws<ArgumentException>(()=>RetailNpcPlayerDefenseDrainProjection.ElementalDefense(0,0,0,1,-1));
    }

    [SkippableFact]
    public void Real17860SourceBindsTheTenPercentCombinedDefenseReduction()
    {
        string? manifest=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        Skip.If(string.IsNullOrWhiteSpace(manifest),"Requires selected retail skill sources");
        using var doc=JsonDocument.Parse(File.ReadAllBytes(manifest!));
        var asset=doc.RootElement.GetProperty("skill_sources").EnumerateArray().Single(x=>x.GetProperty("id").GetInt32()==17860);
        byte[] source=File.ReadAllBytes(asset.GetProperty("path").GetString()!);
        Assert.Equal(-10,RetailNpcPlayerDefenseDrainProjection.Read17860PercentForOfflineFixture(source));
        var changed=(byte[])source.Clone();changed[^1]^=1;
        Assert.Throws<InvalidDataException>(()=>RetailNpcPlayerDefenseDrainProjection.Read17860PercentForOfflineFixture(changed));
    }
}
