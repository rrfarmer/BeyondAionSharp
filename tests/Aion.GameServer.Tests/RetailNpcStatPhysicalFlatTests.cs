using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Aion.GameServer.Controllers.Attack.RetailNpc;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcStatPhysicalFlatTests
{
    private static RetailNpcStatSkillCombat.Source Parse(string xml)
    {
        byte[] bytes=Encoding.Unicode.GetBytes(xml);
        return RetailNpcStatSkillCombat.ParseSource(bytes,Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }
    private static string Recipe(int rate,int value,string extra="")
        =>"<skill_base><id>910010</id><name>PhysicalBaseHitWitness</name><first_target>Target</first_target>"
        +"<target_range>OnlyOne</target_range><target_maxcount>1</target_maxcount><sub_type>Attack</sub_type>"
        +"<effect1_type>SkillATK_Instant</effect1_type><effect1_target_type>Target_Only</effect1_target_type>"
        +$"<effect1_reserved3>{rate}</effect1_reserved3><effect1_reserved4>{value}</effect1_reserved4>"
        +"<effect1_reserved6>100</effect1_reserved6>"+extra+"</skill_base>";

    [Theory]
    [InlineData(0,100)]
    [InlineData(10,120)]
    [InlineData(1,37)]
    [InlineData(0,65500)]
    [InlineData(0,100000)]
    public void NativeBaseHitPairIsIndependentOfTheWeaponPercentage(int rate,int value)
    {
        var source=Parse(Recipe(rate,value));Assert.True(source.Supported,source.UnsupportedReason);
        Assert.Equal(rate,source.BaseHitLevelRate);Assert.Equal(value,source.BaseHitConstant);
        Assert.Equal(unchecked(rate*65+value),source.BaseHitAtSkillLevel(65));
    }

    [Theory]
    [InlineData(500,0,0,0,false,100,0,0,0x44160000u)]
    [InlineData(500,30,20,-13,false,71,3,0,0x4435ccccu)]
    [InlineData(500,30,20,-13,false,71,3,99,0x445e3334u)]
    [InlineData(499,30,-20,-13,true,71,2,70,0x44770cccu)]
    [InlineData(0,0,0,int.MaxValue,false,1,0,0,0xcf000000u)]
    [InlineData(500,0,0,0,false,100,2,0,0x43b40000u)]
    public void BaseHitCombinesAsInt32BeforeFloatRandomizationAndIsNotDoubled(int sample,int percent,int rate,int flat,
        bool dual,int baseHit,int randomType,int draw,uint expected)
        =>Assert.Equal(expected,BitConverter.SingleToUInt32Bits(RetailNpcStatSkillCombat.StatisticsPhysicalBase(
            sample,percent,new(rate,flat),randomType,draw,dual,baseHit)));

    [Fact]
    public void FlatLevelRetainsSignedLevelAndWrappedMultiplyBounds()
    {
        var source=Parse(Recipe(100000,100000));Assert.True(source.Supported,source.UnsupportedReason);
        Assert.Equal(100000,source.BaseHitAtSkillLevel(0));
        Assert.Equal(unchecked(32767*100000+100000),source.BaseHitAtSkillLevel(short.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(()=>source.BaseHitAtSkillLevel(-1));
        Assert.Throws<ArgumentOutOfRangeException>(()=>source.BaseHitAtSkillLevel(32768));
    }

    [Theory]
    [InlineData(-1,100)]
    [InlineData(0,-1)]
    [InlineData(100001,100)]
    [InlineData(0,100001)]
    public void InvalidOrUnboundedBaseHitRemainsVisible(int rate,int value)
        =>Assert.False(Parse(Recipe(rate,value)).Supported);

    [Theory]
    [InlineData("<effect1_reserved9>1</effect1_reserved9><effect1_reserved16>Curse</effect1_reserved16>")]
    [InlineData("<effect1_reserved11>-1</effect1_reserved11>")]
    [InlineData("<effect1_reserved19>1</effect1_reserved19>")]
    [InlineData("<effect1_acc_mod1>1</effect1_acc_mod1>")]
    public void BaseHitDoesNotAdmitOtherUnprovedOperands(string extra)
        =>Assert.False(Parse(Recipe(10,120,extra)).Supported);

    [SkippableFact]
    public void ActualBaseHitSourcesRetainEveryOtherAdmissionBoundary()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash),"Requires pinned external retail sources");
        byte[] raw=File.ReadAllBytes(path!);Assert.Equal(hash,Convert.ToHexStringLower(SHA256.HashData(raw)));
        using var manifest=JsonDocument.Parse(raw);var rows=new List<object>();int reviewed=0,eligible=0;
        foreach(var asset in manifest.RootElement.GetProperty("skill_sources").EnumerateArray())
        {
            byte[] bytes=File.ReadAllBytes(asset.GetProperty("path").GetString()!);
            var original=RetailNpcStatSkillCombat.ParseSource(bytes,asset.GetProperty("sha256").GetString()!);
            var xml=XElement.Parse(Encoding.Unicode.GetString(bytes));
            if(xml.Element("effect1_type")?.Value!="SkillATK_Instant")continue;
            int rate=(int?)xml.Element("effect1_reserved3")??0,value=(int?)xml.Element("effect1_reserved4")??0;
            if(rate==0&&value==0)continue;
            reviewed++;xml.SetElementValue("effect1_reserved3",0);xml.SetElementValue("effect1_reserved4",0);
            var zero=Parse(xml.ToString(SaveOptions.DisableFormatting));
            bool expected=rate is >=0 and <=100000&&value is >=0 and <=100000&&zero.Supported;
            Assert.Equal(expected,original.Supported);if(expected)eligible++;
            rows.Add(new {id=original.Id,rate,value,supported=original.Supported,reason=original.UnsupportedReason});
        }
        Assert.Equal((39,31),(reviewed,eligible)); // Includes20870's native flat baseHit alongside its irrelevant Earth R10 text.
        string? output=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_BASEHIT_OUTPUT");
        if(!string.IsNullOrEmpty(output))
        {
            using var stream=new FileStream(output,FileMode.CreateNew,FileAccess.Write);
            JsonSerializer.Serialize(stream,new {reviewed,eligible,rows});
        }
    }
}
