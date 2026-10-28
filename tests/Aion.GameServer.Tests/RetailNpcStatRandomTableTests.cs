using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Aion.GameServer.Controllers.Attack.RetailNpc;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcStatRandomTableTests
{
    private const string Recipe="<skill_base><id>910002</id><name>SyntheticRandomTwo</name>"
        +"<first_target>Target</first_target><target_range>Area</target_range><target_maxcount>12</target_maxcount><sub_type>Attack</sub_type>"
        +"<effect1_type>SkillATK_Instant</effect1_type><effect1_target_type>Target_Only</effect1_target_type>"
        +"<effect1_reserved2>30</effect1_reserved2><effect1_reserved6>100</effect1_reserved6>"
        +"<effect1_reserved7>1</effect1_reserved7><effect1_reserved8>2</effect1_reserved8>"
        +"<effect1_reserved10>20</effect1_reserved10><effect1_reserved16>Stumble</effect1_reserved16></skill_base>";

    private static RetailNpcStatSkillCombat.Source Parse(string xml)
    {
        byte[] raw=Encoding.Unicode.GetBytes(xml);
        return RetailNpcStatSkillCombat.ParseSource(raw,Convert.ToHexStringLower(SHA256.HashData(raw)));
    }

    [Fact]
    public void EveryDrawUsesTheSelectedTwentyBucketRowAndAddsConditionalFlatAfterwards()
    {
        // Native row2: fourteen60s/six200s, indexed by draw/5. These bits
        // come from the independent instruction-derived binary32 model.
        int low=0,high=0;
        for(int draw=0;draw<100;draw++)
        {
            float amount=RetailNpcStatSkillCombat.StatisticsPhysicalBase(500,30,new(0,0),2,draw);
            uint expected=draw<70?0x43c30001u:0x44a28000u;
            Assert.Equal(expected,BitConverter.SingleToUInt32Bits(amount));
            float withFlat=RetailNpcStatSkillCombat.ConditionalAdditional(amount,20,true,(_,_)=>100);
            Assert.Equal(draw<70?0x43cd0001u:0x44a50000u,BitConverter.SingleToUInt32Bits(withFlat));
            Assert.Equal(amount,RetailNpcStatSkillCombat.ConditionalAdditional(amount,20,false,(_,_)=>throw new Exception("False condition must not draw")));
            if(draw<70)low++;else high++;
        }
        Assert.Equal((70,30),(low,high));
    }

    [Theory]
    [InlineData(65535,100000,99,2147483600,false,0,0x4e9e4bf9u)]
    [InlineData(65535,100000,99,2147483600,false,99,0x4f83e9fau)]
    [InlineData(499,30,-20,-13,true,69,0x437ddc29u)]
    [InlineData(499,30,-20,-13,true,70,0x44538cccu)]
    public void RandomRowPreservesModifierAndDualArithmeticOrder(int sample,int percent,int rate,int flat,bool dual,int draw,uint expected)
        =>Assert.Equal(expected,BitConverter.SingleToUInt32Bits(
            RetailNpcStatSkillCombat.StatisticsPhysicalBase(sample,percent,new(rate,flat),2,draw,dual)));

    [Fact]
    public void CleanRandomTwoPredicatesKeepExistingProcMaskAndTargetBoundaries()
    {
        var area=Parse(Recipe);Assert.True(area.Supported,area.UnsupportedReason);
        Assert.True(area.AreaConditional);Assert.True(area.StumbleOnly);Assert.Equal(2,area.RandomType);
        var back=Parse(Recipe.Replace("<target_range>Area</target_range>","<target_range>OnlyOne</target_range>")
            .Replace("<target_maxcount>12</target_maxcount>","<target_maxcount>1</target_maxcount>").Replace(">Stumble<",">_Back<"));
        Assert.True(back.Supported,back.UnsupportedReason);Assert.True(back.BackOnly);Assert.False(back.AreaConditional);
        foreach(string field in new[]{"<effect1_reserved14>Stumble</effect1_reserved14>","<effect1_reserved19>1</effect1_reserved19>",
            "<effect1_reserved12>AllDMG</effect1_reserved12>","<effect2_type>Slow</effect2_type>","<effect1_critical_prob_mod2>100001</effect1_critical_prob_mod2>"})
            Assert.False(Parse(Recipe.Replace("</skill_base>",field+"</skill_base>")).Supported);
        Assert.False(Parse(Recipe.Replace(">Stumble<",">_Back<")).Supported);
        Assert.False(Parse(Recipe.Replace("<effect1_reserved8>2</effect1_reserved8>","<effect1_reserved8>11</effect1_reserved8>")).Supported);
    }

    [Fact]
    public void NativeParserBoundsAndZeroModeStayExplicit()
    {
        for(int mode=0;mode<=10;mode++)
            Assert.True(Parse(Recipe.Replace("<effect1_reserved8>2</effect1_reserved8>",$"<effect1_reserved8>{mode}</effect1_reserved8>")).Supported);
        foreach(int mode in new[]{-1,11,int.MaxValue})
        {
            Assert.False(Parse(Recipe.Replace("<effect1_reserved8>2</effect1_reserved8>",$"<effect1_reserved8>{mode}</effect1_reserved8>")).Supported);
            Assert.Throws<ArgumentOutOfRangeException>(()=>RetailNpcStatSkillCombat.StatisticsPhysicalBase(500,30,new(0,0),mode,0));
        }
        Assert.Equal(650f,RetailNpcStatSkillCombat.StatisticsPhysicalBase(500,30,new(0,0),0,99));
        Assert.Equal(0x3f666667u,BitConverter.SingleToUInt32Bits(RetailNpcStatSkillCombat.StatisticsPhysicalBase(1,50,new(0,0),2,0)));
    }

    [SkippableFact]
    public void EveryPositiveNativeTableRowMatchesEveryDrawAgainstIndependentFloatVectors()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_RANDOM_TABLE_SOURCE");
        Skip.If(string.IsNullOrEmpty(path),"Requires the selected native random table source");
        byte[] raw=File.ReadAllBytes(path!);
        Assert.Equal("1901847166c70e03258072a31737d3e5d43ec7bf0eb39634acfabb2e5d53eacb",Convert.ToHexStringLower(SHA256.HashData(raw)));
        var expectedBits=new Dictionary<int,uint>{{50,0x43a28000u},{60,0x43c30001u},{90,0x44124000u},
            {100,0x44228000u},{110,0x4432c000u},{150,0x4473c000u},{200,0x44a28000u}};
        var rows=XDocument.Parse(Encoding.Unicode.GetString(raw).TrimStart('\ufeff')).Root!.Elements().ToArray();
        Assert.Equal(10,rows.Length);
        foreach(var row in rows)
        {
            int mode=int.Parse(row.Attribute("random_type")!.Value);
            Assert.Equal(20,row.Elements().Count());
            for(int draw=0;draw<100;draw++)
            {
                int factor=int.Parse(row.Element("range"+(draw/5))!.Value);
                float actual=RetailNpcStatSkillCombat.StatisticsPhysicalBase(500,30,new(0,0),mode,draw);
                Assert.Equal(expectedBits[factor],BitConverter.SingleToUInt32Bits(actual));
            }
        }
    }

    [SkippableFact]
    public void ActualSelectedRandomTwoRecipesKeepTheirSourceAndConditionalIdentity()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash),"Requires external pinned retail stat manifest");
        byte[] bytes=File.ReadAllBytes(path!);Assert.Equal(hash,Convert.ToHexStringLower(SHA256.HashData(bytes)));
        using var doc=JsonDocument.Parse(bytes);
        foreach(int id in new[]{17581,17703,20245})
        {
            var asset=doc.RootElement.GetProperty("skill_sources").EnumerateArray().Single(x=>x.GetProperty("id").GetInt32()==id);
            var source=RetailNpcStatSkillCombat.ParseSource(File.ReadAllBytes(asset.GetProperty("path").GetString()!),asset.GetProperty("sha256").GetString()!);
            if(id==20245){Assert.False(source.Supported);continue;}
            Assert.True(source.Supported,source.UnsupportedReason);Assert.Equal(2,source.RandomType);Assert.Equal(20,source.AdditionalFlat);
            Assert.Equal(id==17703,source.AreaConditional);Assert.Equal(id==17703,source.StumbleOnly);Assert.Equal(id==17581,source.BackOnly);
        }
        int acceptedPhysical=0;
        foreach(var asset in doc.RootElement.GetProperty("skill_sources").EnumerateArray())
        {
            var source=RetailNpcStatSkillCombat.ParseSource(File.ReadAllBytes(asset.GetProperty("path").GetString()!),asset.GetProperty("sha256").GetString()!);
            if(source.Supported&&source.Family==RetailNpcStatSkillCombat.Kind.PhysicalInstant)acceptedPhysical++;
        }
        Assert.Equal(RetailNpcStatReviewedSourceCensus.Physical,acceptedPhysical);
    }
}
