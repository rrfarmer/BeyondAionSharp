using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Model;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcStatPhysicalBonusTests
{
    private static RetailNpcStatSkillCombat.Source Parse(string operands)
    {
        string xml="<skill_base><id>910034</id><name>PhysicalBonusFixture</name><first_target>Target</first_target>"
            +"<sub_type>Attack</sub_type><target_range>OnlyOne</target_range><target_maxcount>1</target_maxcount>"
            +"<effect1_type>SkillATK_Instant</effect1_type><effect1_target_type>Target_Only</effect1_target_type>"
            +"<effect1_reserved2>60</effect1_reserved2><effect1_reserved6>100</effect1_reserved6>"+operands+"</skill_base>";
        byte[] bytes=Encoding.Unicode.GetBytes(xml);
        return RetailNpcStatSkillCombat.ParseSource(bytes,Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }

    [Theory]
    [InlineData(86,-86,86,-86)] [InlineData(-1,100,0,0)]
    [InlineData(1,-2,0,0)] [InlineData(int.MaxValue,1,0,0)]
    public void ConstructorResetsBothOperandsOnNativeWrappedValidationFailure(int rate,int constant,int r,int c)
        =>Assert.Equal((r,c),RetailNpcStatPhysicalBonus.Construct(rate,constant));

    [Theory]
    [InlineData(0,0)] [InlineData(1,0)] [InlineData(2,86)] [InlineData(32767,2817876)]
    public void LevelPairUsesSigned16MultiplyAddAndNonnegativeClamp(int level,int expected)
    {
        var p=new RetailNpcStatPhysicalBonus.Program(86,-86,false,0);
        Assert.Equal(expected,p.ValueAtLevel(level));
        Assert.Equal(0,(p with {Rate=100000,Constant=500}).ValueAtLevel(32767));
        Assert.Throws<ArgumentOutOfRangeException>(()=>p.ValueAtLevel(65535));
    }

    [Theory]
    [InlineData("")] [InlineData("<effect1_reserved7>0</effect1_reserved7>")]
    [InlineData("<effect1_reserved7>1</effect1_reserved7>")]
    public void PhysicalModeMetadataDoesNotTurnAFlatLevelBonusIntoAPercentage(string mode)
    {
        var source=Parse(mode+"<effect1_reserved9>86</effect1_reserved9><effect1_reserved10>-86</effect1_reserved10>");
        Assert.True(source.Supported,source.UnsupportedReason);var p=Assert.IsType<RetailNpcStatPhysicalBonus.Program>(source.PhysicalBonus);
        Assert.True(p.AppliesTo(PlayerClass.CLERIC,Race.ASMODIANS));
        int draws=0;
        int Draw(int low,int high){Assert.Equal((1,100),(low,high));draws++;return 100;}
        Assert.Equal(999.5f,RetailNpcStatSkillCombat.ConditionalAdditional(999.5f,p.ValueAtLevel(1),true,Draw));
        Assert.Equal(0,draws);
        Assert.Equal(1085.5f,RetailNpcStatSkillCombat.ConditionalAdditional(999.5f,p.ValueAtLevel(2),true,Draw));
        Assert.Equal(1,draws);
    }

    [Fact]
    public void ClassPredicateIsAnOrAndFalseSkipsTheAdditionalDraw()
    {
        var source=Parse("<effect1_reserved10>1000</effect1_reserved10><effect1_reserved16>_Class_Fighter,_Class_Knight</effect1_reserved16>");
        Assert.True(source.Supported,source.UnsupportedReason);var p=source.PhysicalBonus!;
        Assert.True(p.AppliesTo(PlayerClass.GLADIATOR,Race.ASMODIANS));Assert.True(p.AppliesTo(PlayerClass.TEMPLAR,Race.ELYOS));
        Assert.False(p.AppliesTo(PlayerClass.CLERIC,Race.ASMODIANS));
        Assert.Equal(200f,RetailNpcStatSkillCombat.ConditionalAdditional(200,p.ValueAtLevel(1),false,(_,_)=>throw new Exception("No draw expected")));
    }

    [Theory]
    [InlineData("Poison")] [InlineData("Bleed")] [InlineData("OpenAerial")] [InlineData("_Back")] [InlineData("Stumble")]
    public void AStatusOrGeometryPredicateCannotBecomeUnconditional(string token)
    {
        var source=Parse("<effect1_reserved10>200</effect1_reserved10><effect1_reserved16>"+token+"</effect1_reserved16>");
        Assert.True(source.Supported,source.UnsupportedReason);
        Assert.False(source.PhysicalBonus!.AppliesTo(PlayerClass.GLADIATOR,Race.ASMODIANS));
    }

    [Theory]
    [InlineData("Curse")] [InlineData("_Front")] [InlineData("_Race_Construct")]
    public void UnknownConditionsOrMissingRaceContextStayVisible(string token)
    {
        var source=Parse("<effect1_reserved10>200</effect1_reserved10><effect1_reserved16>"+token+"</effect1_reserved16>");
        Assert.False(source.Supported);Assert.NotEmpty(source.UnsupportedReason!);
    }

    [Fact]
    public void AuthoredNonPlayerRaceIsFalseRatherThanUnconditional()
    {
        var p=new RetailNpcStatPhysicalBonus.Program(0,3000000,true,0,0,"fixture");
        Assert.False(p.AppliesTo(PlayerClass.TEMPLAR,Race.ELYOS));
        Assert.False(p.AppliesTo(PlayerClass.TEMPLAR,Race.ASMODIANS));
        Assert.Throws<ArgumentOutOfRangeException>(()=>p.AppliesTo(PlayerClass.TEMPLAR,Race.NONE));
    }

    [SkippableFact]
    public void ActualSelectedLevelModeAndClassProgramsKeepTheirCompleteSourceBoundaries()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        Skip.If(string.IsNullOrEmpty(path),"Requires pinned external source corpus");
        using var m=JsonDocument.Parse(File.ReadAllBytes(path!));
        var selected=new Dictionary<int,bool>{{18186,true},{18198,true},{18471,true},{20742,false},{21543,true},
            {21544,false},{21545,true},{21546,true},{21547,false},{21548,true},{21549,false},{20178,false}};
        foreach(var a in m.RootElement.GetProperty("skill_sources").EnumerateArray())
            if(selected.TryGetValue(a.GetProperty("id").GetInt32(),out bool expected))
            {
                int id=a.GetProperty("id").GetInt32();
                var source=RetailNpcStatSkillCombat.ParseSource(File.ReadAllBytes(a.GetProperty("path").GetString()!),a.GetProperty("sha256").GetString()!);
                Assert.True(expected==source.Supported,$"Source{id}: {source.UnsupportedReason}");selected.Remove(id);
            }
        Assert.Empty(selected);
    }
}
