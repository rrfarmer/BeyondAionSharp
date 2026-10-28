using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aion.GameServer.Controllers;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Controllers.Observer;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcStatCriticalTests
{
    private const string Recipe="<skill_base><id>910004</id><name>SyntheticCritical</name>"
        +"<first_target>Target</first_target><target_range>OnlyOne</target_range><target_maxcount>1</target_maxcount><sub_type>Attack</sub_type>"
        +"<effect1_type>SkillATK_Instant</effect1_type><effect1_target_type>Target_Only</effect1_target_type>"
        +"<effect1_reserved2>60</effect1_reserved2><effect1_reserved6>100</effect1_reserved6>"
        +"<effect1_critical_prob_mod2>120</effect1_critical_prob_mod2></skill_base>";
    private static RetailNpcStatSkillCombat.Source Parse(string xml)
    {
        byte[] bytes=Encoding.Unicode.GetBytes(xml);
        return RetailNpcStatSkillCombat.ParseSource(bytes,Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(50)] [InlineData(75)]
    [InlineData(100)] [InlineData(120)] [InlineData(200)] [InlineData(100000)]
    public void FlatCriticalRecipeKeepsNativeCoefficientIndependentOfAttackPercentage(int percent)
    {
        var s=Parse(Recipe.Replace(">120<",$">{percent}<"));
        Assert.True(s.Supported,s.UnsupportedReason);Assert.Equal(percent,s.CriticalPercent);Assert.Equal(60,s.Percent);
    }

    [Theory]
    [InlineData("<effect1_critical_prob_mod1>1</effect1_critical_prob_mod1>")]
    [InlineData("<effect1_critical_add_dmg_mod1>1</effect1_critical_add_dmg_mod1>")]
    [InlineData("<effect1_critical_add_dmg_mod2>1</effect1_critical_add_dmg_mod2>")]
    [InlineData("<effect1_reserved18>90</effect1_reserved18>")]
    public void UnreviewedLevelAddDamageAndBeginProbabilityStayFenced(string field)
        =>Assert.False(Parse(Recipe.Replace("</skill_base>",field+"</skill_base>")).Supported);

    [Theory]
    [InlineData(112,20,0,1000u,92)]
    [InlineData(112,20,0,2000u,46)]
    [InlineData(112,113,0,1000u,-1)]
    [InlineData(0,0,5,1000u,500)]
    [InlineData(3000,0,0,1000u,500)]
    [InlineData(int.MinValue,0,5,65000u,-33038202)]
    public void NativeCriticalThresholdPreservesEarlyConversionSignedDeltaRatioAndCap(int scaled,int resistance,byte pose,uint ratio,int expected)
        =>Assert.Equal(expected,RetailNpcStatSkillCombat.CriticalThreshold(scaled,resistance,pose,ratio));

    private sealed class CountingCriticalObserver() : AttackerCriticalStatusObserver(AttackStatus.CRITICAL,2,50,true)
    {
        public int Calls;
        public override AttackerCriticalStatus CheckAttackerCriticalStatus(AttackStatus status,bool skill)
        {Calls++;DecreaseCount();return new AttackerCriticalStatus(false);}
    }

    [Fact]
    public void NewCoefficientDomainChecksOwnerBeforeAnyConsumingObserverQuery()
    {
        var controller=new ObserveController();var observer=new CountingCriticalObserver();var source=Parse(Recipe);
        Assert.True(RetailNpcStatSkillCombat.CriticalOwnerSupported(source,controller));
        controller.AddAttackCalcObserver(observer);Assert.True(controller.HasSkillCriticalObserver);
        Assert.False(RetailNpcStatSkillCombat.CriticalOwnerSupported(source,controller));
        Assert.Equal(0,observer.Calls);Assert.Equal(2,observer.GetCount());
        Assert.True(RetailNpcStatSkillCombat.CriticalOwnerSupported(Parse(Recipe.Replace(">120<",">100<")),controller));
        controller.RemoveAttackCalcObserver(observer);Assert.True(RetailNpcStatSkillCombat.CriticalOwnerSupported(source,controller));
        Assert.Equal(0,observer.Calls);Assert.Equal(2,observer.GetCount());
    }

    [SkippableFact]
    public void EveryActualNewSourceParsesWithItsExistingGraphGuards()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash),"Requires pinned external full09 source manifest");
        byte[] raw=File.ReadAllBytes(path!);Assert.Equal(hash,Convert.ToHexStringLower(SHA256.HashData(raw)));
        using var doc=JsonDocument.Parse(raw);
        int[] ids=[17302,17303,17536,18336,18337,18338,19009,19411,19415,19416,19724,19725,19726,19727,21913];
        int count=0;
        foreach(var asset in doc.RootElement.GetProperty("skill_sources").EnumerateArray())
        {
            int id=asset.GetProperty("id").GetInt32();if(!ids.Contains(id))continue;
            var s=RetailNpcStatSkillCombat.ParseSource(File.ReadAllBytes(asset.GetProperty("path").GetString()!),asset.GetProperty("sha256").GetString()!);
            Assert.True(s.Supported,s.UnsupportedReason);
            if(id is 19411 or 19415 or 19416){Assert.False(s.ApplyCritical);Assert.Equal(0,s.CriticalPercent);}
            count++;
        }
        Assert.Equal(15,count);
    }
}
