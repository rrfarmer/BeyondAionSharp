using System.Security.Cryptography;
using System.Text;
using Aion.GameServer.Controllers.Attack.RetailNpc;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcStatBackAttackTests
{
    private static readonly string Recipe="<skill_base><id>999999</id><name>NumericFixture</name><first_target>Target</first_target><target_range>OnlyOne</target_range><target_maxcount>1</target_maxcount>"
        +"<effect1_type>SkillATK_Instant</effect1_type><effect1_target_type>Target_Only</effect1_target_type>"
        +"<effect1_reserved2>60</effect1_reserved2><effect1_reserved6>100</effect1_reserved6><effect1_reserved7>1</effect1_reserved7><effect1_reserved8>3</effect1_reserved8>"
        +"<effect1_reserved10>21</effect1_reserved10><effect1_reserved16>_Back</effect1_reserved16></skill_base>";
    private static RetailNpcStatSkillCombat.Source Parse(string xml)
    {
        byte[] raw=Encoding.Unicode.GetBytes(xml);
        return RetailNpcStatSkillCombat.ParseSource(raw,Convert.ToHexStringLower(SHA256.HashData(raw)));
    }
    [Fact]
    public void NativeShapeIsSelectedByItsOperandsRatherThanASkillIdWhitelist()
    {
        var s=Parse(Recipe);Assert.True(s.Supported,s.UnsupportedReason);Assert.True(s.BackOnly);Assert.Equal(21,s.AdditionalFlat);
        Assert.Equal(60,s.Percent);Assert.Equal(3,s.RandomType);
    }
    [Theory]
    [InlineData("_Back","_Front")]
    [InlineData("<first_target>Target</first_target>","<first_target>Me</first_target>")]
    [InlineData("<effect1_reserved6>100</effect1_reserved6>","<effect1_reserved6>50</effect1_reserved6>")]
    [InlineData("<target_range>OnlyOne</target_range>","<target_range>Area</target_range>")]
    [InlineData("<target_maxcount>1</target_maxcount>","<target_maxcount>6</target_maxcount>")]
    [InlineData("<effect1_reserved8>3</effect1_reserved8>","<effect1_reserved8>11</effect1_reserved8>")]
    [InlineData("</skill_base>","<effect1_reserved14>Spin</effect1_reserved14></skill_base>")]
    [InlineData("</skill_base>","<effect2_type>StatDown</effect2_type></skill_base>")]
    [InlineData("</skill_base>","<effect9_type>StatDown</effect9_type></skill_base>")]
    [InlineData("</skill_base>","<effect1_reserved13>1</effect1_reserved13></skill_base>")]
    [InlineData("</skill_base>","<effect1_reserved20>1</effect1_reserved20></skill_base>")]
    [InlineData("</skill_base>","<effect1_reserved21>1</effect1_reserved21></skill_base>")]
    [InlineData("</skill_base>","<effect1_reserved5>Fire</effect1_reserved5></skill_base>")]
    [InlineData("</skill_base>","<effect1_reserved11>-1</effect1_reserved11></skill_base>")]
    public void UnprovedConditionsTargetsChildrenAndForceRemainExplicit(string from,string to)
    {var s=Parse(Recipe.Replace(from,to,StringComparison.Ordinal));Assert.False(s.Supported);Assert.NotEmpty(s.UnsupportedReason!);}
    [Fact]
    public void RawNegativeR5IsNotSubstitutedForNamedAccuracy()
    {var s=Parse(Recipe.Replace("</skill_base>","<effect1_reserved5>-15</effect1_reserved5></skill_base>"));Assert.True(s.Supported,s.UnsupportedReason);}
    [Theory]
    [InlineData(0,0,0,1,0,true)] [InlineData(0,0,0,-1,0,false)] [InlineData(0,0,0,0,1,false)]
    [InlineData(0,1,0,0,0,false)] [InlineData(30,0,0,0,1,true)] [InlineData(60,0,0,-1,0,true)]
    [InlineData(90,0,0,0,-1,true)]
    public void BackUsesTheNativeTableStrictPositiveDotAndActualOperandOrder(int heading,float sx,float sy,float tx,float ty,bool expected)
        =>Assert.Equal(expected,SkyTempleNpcPhysicalSkills.Behind(heading,sx,sy,tx,ty));
    [Theory]
    [InlineData(false,0x445bffffu,879,0)]
    [InlineData(true,0x44613fffu,900,1)]
    public void NativeFloatWordsAndConditionalDrawPrecedeTheAttenuationInteger(bool back,uint bits,int truncated,int count)
    {
        float before=RetailNpcStatSkillCombat.StatisticsPhysicalBase(500,60,new(0,0),3,99);
        Assert.Equal(0x445bffffu,BitConverter.SingleToUInt32Bits(before));
        int calls=0;float after=RetailNpcStatSkillCombat.BackAdditional(before,21,back,(lo,hi)=>{Assert.Equal((1,100),(lo,hi));calls++;return 100;});
        Assert.Equal(bits,BitConverter.SingleToUInt32Bits(after));Assert.Equal(truncated,(int)after);Assert.Equal(count,calls);
    }
    [Fact]
    public void AdditionalFlatIsNeitherAPercentageNorAnUnconditionalRandomDraw()
    {
        Assert.Equal(221f,RetailNpcStatSkillCombat.BackAdditional(200f,21,true,(_,_)=>1));
        Assert.Equal(200f,RetailNpcStatSkillCombat.BackAdditional(200f,0,true,(_,_)=>throw new Exception("Must not draw")));
        Assert.Equal(200f,RetailNpcStatSkillCombat.BackAdditional(200f,21,false,(_,_)=>throw new Exception("Must not draw")));
    }
}
