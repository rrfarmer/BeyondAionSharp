using System.Security.Cryptography;
using System.Text;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Model.Stats.Container;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcStatBuffTests
{
    [Fact]
    public void GeneralResistanceDescriptorRequiresThePlayerResistanceParser()
    {
        byte[] raw=Encoding.Unicode.GetBytes("<skill_base><id>1</id><name>Fixture</name><effect1_type>StatUp</effect1_type>"
            +"<effect1_reserved13>arAll</effect1_reserved13><effect1_reserved2>100</effect1_reserved2></skill_base>");
        string hash=Convert.ToHexStringLower(SHA256.HashData(raw));
        Assert.False(Assert.Single(RetailNpcStatBuffs.Parse(raw,hash).Programs).Supported);
        var program=Assert.Single(RetailNpcStatBuffs.ParsePlayerResistance(raw,hash).Programs);
        Assert.True(program.Supported);
        var part=Assert.Single(program.Parts);
        Assert.Equal(StatEnum.ABNORMAL_RESISTANCE_ALL,part.Stat);
        Assert.False(part.Percent);Assert.Equal(100,RetailNpcStatBuffs.Value(part,1));
    }

    [SkippableFact]
    public void PinnedRetailPlayerBuffProvidesGeneralCurseResistanceOperand()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_PLAYER_BUFF_FIXTURE");
        Skip.If(string.IsNullOrWhiteSpace(path),"Requires pinned 5.8 player buff record");
        const string hash="0155bee5a323c47ad0e012e98d85fb4f2dcb195c9ad9cf206cef60dc1376f1bd";
        var source=RetailNpcStatBuffs.ParsePlayerResistance(File.ReadAllBytes(path!),hash);
        Assert.Equal(2767,source.Id);Assert.Equal("RI_SummonArmor_G1",source.Name);
        var program=Assert.Single(source.Programs,p=>p.Position==3);
        Assert.True(program.Supported);
        var general=Assert.Single(program.Parts,p=>p.Stat==StatEnum.ABNORMAL_RESISTANCE_ALL);
        Assert.False(general.Percent);int value=RetailNpcStatBuffs.Value(general,1);
        Assert.Equal(100,value);
        Assert.Equal(100,RetailNpcStatSpellDrain.CalculateCurseResistance(new(0,default,new(0,value),default,default)));
    }

    private static RetailNpcStatBuffs.Program Parse(string descriptors)
    {
        byte[] raw=Encoding.Unicode.GetBytes("<skill_base><id>16405</id><name>Fixture</name><effect1_type>StatUp</effect1_type>"+descriptors+"</skill_base>");
        return Assert.Single(RetailNpcStatBuffs.Parse(raw,Convert.ToHexStringLower(SHA256.HashData(raw))).Programs);
    }

    [Fact]
    public void NativeCombinedDefenseIncludesSixElementsAndDoesNotAddMagicHitResistance()
    {
        var p=Parse("<effect1_reserved13>PMDefend</effect1_reserved13><effect1_reserved2>50</effect1_reserved2><effect1_reserved6>1</effect1_reserved6>");
        Assert.True(p.Supported);Assert.Equal(7,p.Parts.Length);
        Assert.Contains(p.Parts,x=>x.Stat==StatEnum.PHYSICAL_DEFENSE);
        Assert.All(RetailNpcStatBuffs.Elements,e=>Assert.Contains(p.Parts,x=>x.Stat==e));
        Assert.DoesNotContain(p.Parts,x=>x.Stat==StatEnum.MAGICAL_RESIST);
        Assert.All(p.Parts,x=>{Assert.True(x.Percent);Assert.Equal(50,RetailNpcStatBuffs.Value(x,25));});
    }

    [Fact]
    public void NativeLastTwoDescriptorsAndSignedLevelArithmeticRemainOrdered()
    {
        var p=Parse("<effect1_reserved18>Dodge</effect1_reserved18><effect1_reserved15>10</effect1_reserved15><effect1_reserved16>70</effect1_reserved16><effect1_reserved17>1</effect1_reserved17>"
            +"<effect1_reserved22>ElementalDefendDark</effect1_reserved22><effect1_reserved20>8000</effect1_reserved20>");
        Assert.True(p.Supported);Assert.Equal(2,p.Parts.Length);
        Assert.Equal(StatEnum.EVASION,p.Parts[0].Stat);Assert.Equal(360,RetailNpcStatBuffs.Value(p.Parts[0],29));
        Assert.Equal(StatEnum.DARK_RESISTANCE,p.Parts[1].Stat);Assert.False(p.Parts[1].Percent);
        Assert.Equal(8000,RetailNpcStatBuffs.Value(p.Parts[1],29));
    }

    [Fact]
    public void UnknownDescriptorRejectsTheWholeEffectInsteadOfKeepingAPartialBuff()
    {
        var p=Parse("<effect1_reserved13>physicalDefend</effect1_reserved13><effect1_reserved2>200</effect1_reserved2>"
            +"<effect1_reserved14>Speed</effect1_reserved14><effect1_reserved4>50</effect1_reserved4>");
        Assert.False(p.Supported);Assert.Empty(p.Parts);Assert.Contains("Speed",p.UnsupportedReason);
    }

    // Independent native integer/cap witnesses, including the already-capped-seed trap.
    [Theory]
    [InlineData(1000000,-50,0,0,500000,500000)]
    [InlineData(123,50,0,0,500000,184)]
    [InlineData(123,-50,0,0,500000,62)]
    [InlineData(500,0,10000000,0,500000,500000)]
    [InlineData(700,0,65535,0,9999,9999)]
    [InlineData(0,0,8000,-1300,1300,1300)]
    [InlineData(-101,50,-10,-1300,1300,-161)]
    [InlineData(0,50,0,-1300,1300,0)]
    public void NativeDefensesTruncateBeforeClamping(long basis,int percent,int flat,long min,long max,long expected)
        =>Assert.Equal(expected,RetailNpcStatDefenses.Calculate(basis,percent,flat,min,max));
}
