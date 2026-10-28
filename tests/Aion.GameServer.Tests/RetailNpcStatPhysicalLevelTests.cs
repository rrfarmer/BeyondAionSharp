using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Aion.GameServer.Controllers.Attack.RetailNpc;

namespace Aion.GameServer.Tests;

/// <summary>Native hitMult construction and signed-level arithmetic apply to
/// physical primaries independently of the target collector or additional state.</summary>
public sealed class RetailNpcStatPhysicalLevelTests
{
    private static RetailNpcStatSkillCombat.Source Parse(string xml)
    {
        byte[] bytes=Encoding.Unicode.GetBytes(xml);
        return RetailNpcStatSkillCombat.ParseSource(bytes,Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }

    private static string Recipe(int rate,int value,string range="OnlyOne",string extra="")
        =>"<skill_base><id>910009</id><name>PhysicalLevelWitness</name><first_target>Target</first_target>"
        +$"<target_range>{range}</target_range><target_maxcount>1</target_maxcount><sub_type>Attack</sub_type>"
        +"<effect1_type>SkillATK_Instant</effect1_type><effect1_target_type>Target_Only</effect1_target_type>"
        +$"<effect1_reserved1>{rate}</effect1_reserved1><effect1_reserved2>{value}</effect1_reserved2>"
        +"<effect1_reserved6>100</effect1_reserved6>"+extra+"</skill_base>";

    [Theory]
    [InlineData(10,90,0,90,0x446d8000u)]
    [InlineData(10,90,1,100,0x447a0000u)]
    [InlineData(10,90,9,180,0x44af0000u)]
    [InlineData(20,20,65,1320,0x45dde000u)]
    [InlineData(1,120,65,185,0x44b22000u)]
    public void PhysicalRateUsesTheNativeCoefficientBeforeFloatConversion(int rate,int value,int level,int effective,uint bits)
    {
        // Independent ordered binary32 witnesses, not rounded decimal damage.
        var source=Parse(Recipe(rate,value));Assert.True(source.Supported,source.UnsupportedReason);
        Assert.Equal(rate,source.LevelRate);Assert.Equal(effective,source.ValueAtSkillLevel(level));
        Assert.Equal(bits,BitConverter.SingleToUInt32Bits(RetailNpcStatSkillCombat.StatisticsPhysicalBase(
            500,source.ValueAtSkillLevel(level),new(0,0),0,0)));
    }

    [Theory]
    [InlineData("OnlyOne","")]
    [InlineData("Area","")]
    [InlineData("OnlyOne","_Back")]
    [InlineData("OnlyOne","Stumble")]
    [InlineData("OnlyOne","Poison")]
    [InlineData("OnlyOne","Bleed")]
    [InlineData("Area","Stumble")]
    [InlineData("Area","Poison")]
    [InlineData("Area","Bleed")]
    public void ACollectorOrConditionDoesNotSelectADifferentLevelFormula(string range,string condition)
    {
        string extra=condition.Length==0?"":"<effect1_reserved7>1</effect1_reserved7><effect1_reserved10>71</effect1_reserved10>"
            +$"<effect1_reserved16>{condition}</effect1_reserved16>";
        var source=Parse(Recipe(10,60,range,extra));Assert.True(source.Supported,source.UnsupportedReason);
        Assert.Equal(710,source.ValueAtSkillLevel(65));
    }

    [Theory]
    [InlineData("<effect1_reserved11>-1</effect1_reserved11>")]
    [InlineData("<effect1_reserved19>1</effect1_reserved19>")]
    [InlineData("<effect1_reserved9>1</effect1_reserved9><effect1_reserved16>Curse</effect1_reserved16>")]
    [InlineData("<effect1_acc_mod1>1</effect1_acc_mod1>")]
    [InlineData("<effect1_reserved7>1</effect1_reserved7><effect1_reserved10>71</effect1_reserved10><effect1_reserved16>Curse</effect1_reserved16>")]
    public void LevelSupportDoesNotAdmitUnprovedNumericOperands(string extra)
        =>Assert.False(Parse(Recipe(10,90,extra:extra)).Supported);

    [Theory]
    [InlineData(-1,90)]
    [InlineData(100001,90)]
    [InlineData(10,-11)]
    public void InvalidOrUnboundedPairsRemainVisible(int rate,int value)
        =>Assert.False(Parse(Recipe(rate,value)).Supported);

    [Fact]
    public void SignedLevelAndWrappedMultiplyKeepTheirExistingBounds()
    {
        var source=Parse(Recipe(100000,100000));Assert.True(source.Supported,source.UnsupportedReason);
        Assert.Equal(unchecked(32767*100000+100000),source.ValueAtSkillLevel(short.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(()=>source.ValueAtSkillLevel(-1));
        Assert.Throws<ArgumentOutOfRangeException>(()=>source.ValueAtSkillLevel(32768));
        // The production adapter separately rejects effective values outside0..100000.
    }

    [SkippableFact]
    public void SelectedPhysicalSourcesChangeOnlyThePreviouslyRejectedRateGate()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash),"Requires the external pinned source manifest");
        byte[] raw=File.ReadAllBytes(path!);Assert.Equal(hash,Convert.ToHexStringLower(SHA256.HashData(raw)));
        using var manifest=JsonDocument.Parse(raw);var rows=new List<object>();int reviewed=0,eligible=0;
        foreach(var asset in manifest.RootElement.GetProperty("skill_sources").EnumerateArray())
        {
            byte[] bytes=File.ReadAllBytes(asset.GetProperty("path").GetString()!);
            var original=RetailNpcStatSkillCombat.ParseSource(bytes,asset.GetProperty("sha256").GetString()!);
            var xml=XElement.Parse(Encoding.Unicode.GetString(bytes));
            if(xml.Element("effect1_type")?.Value!="SkillATK_Instant")continue;
            var rateNode=xml.Element("effect1_reserved1");int rate=rateNode is null?0:int.Parse(rateNode.Value);
            if(rate==0)continue;
            reviewed++;rateNode!.Value="0";var zeroRate=Parse(xml.ToString(SaveOptions.DisableFormatting));
            bool expected=rate is >=0 and <=100000&&zeroRate.Supported;
            // The zero-rate control preserves every other source operand and boundary.
            Assert.Equal(expected,original.Supported);
            if(expected){eligible++;Assert.Equal(rate,original.LevelRate);}
            rows.Add(new {id=original.Id,rate,supported=original.Supported,reason=original.UnsupportedReason,
                additional=original.AdditionalStatusMask,stumble=original.StumbleOnly,back=original.BackOnly});
        }
        Assert.Equal((69,57),(reviewed,eligible)); // Includes two newly proved All-mask level programs.
        string? output=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_LEVEL_RATE_OUTPUT");
        if(!string.IsNullOrEmpty(output))
        {
            using var stream=new FileStream(output,FileMode.CreateNew,FileAccess.Write);
            JsonSerializer.Serialize(stream,new {reviewed,eligible,rows});
        }
    }
}
