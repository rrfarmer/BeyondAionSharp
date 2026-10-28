using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Aion.GameServer.Controllers;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Controllers.Observer;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcStatSpellCriticalTests
{
    private static string Recipe(int coefficient,bool enabled=true)=>"<skill_base><id>910038</id><name>SpellCriticalCoefficient</name>"
        +"<sub_type>Attack</sub_type><first_target>Target</first_target><target_range>OnlyOne</target_range><target_maxcount>1</target_maxcount>"
        +"<apply_magical_skill_boost_bonus>1</apply_magical_skill_boost_bonus><apply_magical_critical>"+(enabled?1:0)+"</apply_magical_critical>"
        +"<effect1_type>SpellATK_Instant</effect1_type><effect1_target_type>Target_Only</effect1_target_type>"
        +"<effect1_reserved2>60</effect1_reserved2><effect1_reserved6>1</effect1_reserved6><effect1_reserved10>Fire</effect1_reserved10>"
        +"<effect1_critical_prob_mod2>"+coefficient+"</effect1_critical_prob_mod2></skill_base>";
    private static RetailNpcStatSkillCombat.Source Parse(string xml)
    {
        byte[] bytes=Encoding.Unicode.GetBytes(xml);
        return RetailNpcStatSkillCombat.ParseSource(bytes,Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(50)] [InlineData(100)]
    [InlineData(150)] [InlineData(200)] [InlineData(100000)]
    public void SourceCoefficientDoesNotDisableTheNativeCriticalBranch(int coefficient)
    {
        var source=Parse(Recipe(coefficient));Assert.True(source.Supported,source.UnsupportedReason);
        Assert.True(source.ApplyCritical);Assert.Equal(coefficient,source.CriticalPercent);
        Assert.Equal(60,source.Percent);
        var disabled=Parse(Recipe(coefficient,false));Assert.True(disabled.Supported,disabled.UnsupportedReason);
        Assert.False(disabled.ApplyCritical);Assert.Equal(coefficient,disabled.CriticalPercent);
    }

    [Theory]
    [InlineData(75,150,20,0,1000u,112,92)]
    [InlineData(75,200,20,0,1000u,150,130)]
    [InlineData(75,0,0,0,1000u,0,0)]
    [InlineData(75,0,20,5,1000u,0,480)]
    [InlineData(75,0,20,5,2000u,0,240)]
    [InlineData(65535,100000,1,0,1000u,65535000,500)]
    public void NativeCoefficientConversionPrecedesPoseResistanceRatioAndCap(int raw,int percent,int resistance,byte pose,uint ratio,int scaled,int threshold)
    {
        var input=RetailNpcStatSkillCombat.CriticalInput(raw,percent);
        Assert.Equal(scaled,input.Value);
        Assert.Equal(threshold,RetailNpcStatSkillCombat.CriticalThreshold(input.Value,resistance,pose,ratio));
        if(percent==150)Assert.Equal(112.5f,input.Scaled);
    }

    private sealed class CountingObserver():AttackerCriticalStatusObserver(AttackStatus.CRITICAL,2,50,true)
    {
        internal int Calls;
        public override AttackerCriticalStatus CheckAttackerCriticalStatus(AttackStatus status,bool skill)
        {Calls++;DecreaseCount();return new AttackerCriticalStatus(false);}
    }

    [Theory]
    [InlineData(0)] [InlineData(150)] [InlineData(200)]
    public void NewSpellCoefficientsRejectUnprovedOwnersBeforeConsumption(int coefficient)
    {
        var source=Parse(Recipe(coefficient));var observers=new ObserveController();var observer=new CountingObserver();
        Assert.True(RetailNpcStatSkillCombat.CriticalOwnerSupported(source,observers));
        observers.AddAttackCalcObserver(observer);
        Assert.False(RetailNpcStatSkillCombat.CriticalOwnerSupported(source,observers));
        Assert.Equal(0,observer.Calls);Assert.Equal(2,observer.GetCount());
        Assert.True(RetailNpcStatSkillCombat.CriticalOwnerSupported(Parse(Recipe(coefficient,false)),observers));
        observers.RemoveAttackCalcObserver(observer);
        Assert.True(RetailNpcStatSkillCombat.CriticalOwnerSupported(source,observers));Assert.Equal(0,observer.Calls);
    }

    [Theory]
    [InlineData(-1)] [InlineData(100001)]
    public void UnboundedCoefficientsRemainVisible(int coefficient)=>Assert.False(Parse(Recipe(coefficient)).Supported);

    [Theory]
    [InlineData("<effect1_critical_prob_mod1>1</effect1_critical_prob_mod1>")]
    [InlineData("<effect1_critical_add_dmg_mod2>1</effect1_critical_add_dmg_mod2>")]
    [InlineData("<effect1_reserved19>1</effect1_reserved19>")]
    public void AProvedCoefficientDoesNotAdmitOtherUnprovedOperands(string field)
        =>Assert.False(Parse(Recipe(0).Replace("</skill_base>",field+"</skill_base>")).Supported);

    [SkippableFact]
    public void EveryActualNondefaultSpellCoefficientEqualsItsHundredCoefficientControlExceptThatOperand()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? pin=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(pin),"Requires pinned complete source corpus");
        byte[] raw=File.ReadAllBytes(path!);Assert.Equal(pin,Convert.ToHexStringLower(SHA256.HashData(raw)));
        using var manifest=JsonDocument.Parse(raw);var rows=new List<object>();int admitted=0;
        foreach(var a in manifest.RootElement.GetProperty("skill_sources").EnumerateArray())
        {
            byte[] bytes=File.ReadAllBytes(a.GetProperty("path").GetString()!);
            var xml=XElement.Parse(Encoding.Unicode.GetString(bytes));
            if(xml.Element("effect1_type")?.Value!="SpellATK_Instant"||xml.Element("apply_magical_critical")?.Value!="1")continue;
            int coefficient=(int?)xml.Element("effect1_critical_prob_mod2")??100;if(coefficient==100)continue;
            var source=RetailNpcStatSkillCombat.ParseSource(bytes,a.GetProperty("sha256").GetString()!);
            xml.SetElementValue("effect1_critical_prob_mod2",100);var control=Parse(xml.ToString(SaveOptions.DisableFormatting));
            bool expected=coefficient is >=0 and <=100000&&control.Supported;
            Assert.Equal(expected,source.Supported);
            if(expected)
            {
                admitted++;Assert.True(source.ApplyCritical);
                Assert.Equal(control,source with {Sha256=control.Sha256,CriticalPercent=100});
            }
            rows.Add(new {id=source.Id,coefficient,expected,supported=source.Supported,reason=source.UnsupportedReason});
        }
        Assert.Equal((125,66),(rows.Count,admitted));
        if(Environment.GetEnvironmentVariable("AION_RETAIL_STATS_SPELL_CRITICAL_OUTPUT") is {Length:>0} output)
        {using var stream=new FileStream(output,FileMode.CreateNew,FileAccess.Write);JsonSerializer.Serialize(stream,new {reviewed=rows.Count,admitted,rows});}
    }
}
