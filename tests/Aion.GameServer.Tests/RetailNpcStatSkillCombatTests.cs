using System.Security.Cryptography;
using System.Text;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Model;

namespace Aion.GameServer.Tests;

/// <summary>Analytic instruction witnesses and source-admission boundaries, not
/// native observations. Real source records are separately pinned by the package.</summary>
public sealed class RetailNpcStatSkillCombatTests
{
    [Theory]
    [InlineData(541,811,true,true,540,810)]
    [InlineData(541,811,true,false,270,405)]
    [InlineData(1885,2495,true,true,1884,2494)]
    [InlineData(1885,2495,true,false,942,1247)]
    [InlineData(1461,2085,true,true,1460,2084)]
    [InlineData(1461,2085,true,false,730,1042)]
    [InlineData(16,24,true,true,16,24)]
    [InlineData(15,23,false,true,15,23)]
    [InlineData(0,0,true,true,0,0)]
    [InlineData(65535,65535,true,true,65534,65534)]
    public void DualSkillInputRangesFollowTheirOwnNativeConsumer(int min,int max,bool dual,bool physical,int low,int high)
    {
        int calls=0;
        var result=RetailNpcStatSkillCombat.DrawSkillWeapon(min,max,dual,physical,(a,b)=>{calls++;Assert.Equal(low,a);Assert.Equal(high,b);return a;});
        Assert.Equal(1,calls);Assert.Equal((low,high,low),(result.Minimum,result.Maximum,result.Sample));
    }

    [Fact]
    public void APhysicalDualDrawCanReturnAnOddInteriorSampleAndCopiesRoundedEffectRates()
    {
        int calls=0;
        var result=RetailNpcStatSkillCombat.DrawSkillWeapon(15,23,true,true,(a,b)=>{calls++;Assert.Equal((14,22),(a,b));return 15;});
        Assert.Equal(1,calls);Assert.Equal(15,result.Sample); // Not twice a7..11 draw.
        float amount=RetailNpcStatSkillCombat.StatisticsPhysicalBase(100,0,new(20,20),0,0,true);
        Assert.Equal(0x43340001u,BitConverter.SingleToUInt32Bits(amount)); // Ordered rate+1+rate, then40flat.
        Assert.Equal(-2f,RetailNpcStatSkillCombat.StatisticsPhysicalBase(0,0,new(0,int.MaxValue),0,0,true));
        Assert.Equal(140f,RetailNpcStatSkillCombat.StatisticsPhysicalBase(100,0,new(20,20),0,0));
    }
    [Theory]
    [InlineData(0, 234)]
    [InlineData(999, 234)]
    [InlineData(1000, 469)]
    public void NpcSpellBoostRetainsIntegerDivisionAndBinary32Quantization(int boost, int expected)
    {
        Assert.Equal(expected, RetailNpcStatSkillCombat.SpellBase(100, 135, boost, 1000, true));
    }

    [Fact]
    public void DisabledSourceBoostSkipsEvenAnOtherwiseUnresolvedHighBoost()
    {
        Assert.Equal(234, RetailNpcStatSkillCombat.SpellBase(100, 135, 9000, 1000, false));
        Assert.Throws<InvalidOperationException>(() => RetailNpcStatSkillCombat.SpellBase(100, 135, 2900, 1000, true));
    }

    [Theory]
    [InlineData(0, 1000, 3400, 234)]
    [InlineData(2999, 1000, 3400, 705)]
    [InlineData(3000, 1000, 3400, 939)]
    [InlineData(9000, 1000, 3400, 939)]
    [InlineData(3000, 1000, 2900, 705)]
    [InlineData(9000, 1000, 2900, 705)]
    [InlineData(9000, 2000, 3400, 469)]
    [InlineData(-9000, 1000, 3400, 234)]
    public void AnExplicitNativeCapPreservesBothClampsAndIntegerBoostDivision(int boost, uint ratio, int cap, int expected)
    {
        Assert.Equal(expected, RetailNpcStatSkillCombat.StatsSpellBase(100, 135, boost, ratio, true, cap));
    }

    [Theory]
    [InlineData(10000,1000,3400,3400)]
    [InlineData(10000,2000,3400,1700)]
    [InlineData(10000,2000,2900,1450)]
    [InlineData(3400,1500,3400,2266)]
    [InlineData(2999,1000,3400,2999)]
    [InlineData(-500,1000,3400,0)]
    public void IncomingAndOutgoingSpellsShareTheTwoNativeBoostClamps(int delta,uint ratio,int cap,int expected)
    {
        Assert.Equal(expected,RetailNpcStatSkillCombat.NormalizeSpellBoost(delta,ratio,cap));
    }

    [Theory]
    [InlineData(500f,100,1700,1350)]
    [InlineData(500f,100,3400,2200)]
    [InlineData(500f,80,0,400)]
    [InlineData(306f,120,0,367)]
    [InlineData(-100f,100,0,0)]
    public void PlayerCommonSpellScalarUsesTheNativeContinuousBranchAndEarlyIntegerBoundary(float raw,int knowledge,int boost,int expected)
    {
        Assert.Equal(expected,RetailNpcStatSkillCombat.PlayerSpellBase(raw,knowledge,boost));
    }

    [Fact]
    public void APlayerFractionalRecipeIsNotSilentlyAdmittedAsTheNativeAbsoluteBase()
    {
        Assert.Throws<ArgumentException>(()=>RetailNpcStatSkillCombat.PlayerSpellBase(306.9f,120,0));
    }

    [Theory]
    [InlineData("Me","OnlyOne","Target_Only",true)]
    [InlineData("me","onlyone","target_only",true)]
    [InlineData("Me","Area","Target_Only",false)]
    [InlineData("Target","OnlyOne","Target_Only",false)]
    public void ASourceSelfOnlyPrimaryIsDistinctFromAnEnemyDamageCase(string first,string range,string effectTarget,bool expected)
    {
        string xml=Spell("1").Replace("<id>16590</id>","<id>16590</id><first_target>"+first+"</first_target><target_range>"+range+
            "</target_range><effect1_target_type>"+effectTarget+"</effect1_target_type>",StringComparison.Ordinal);
        var source=Parse(xml);Assert.True(source.Supported);Assert.Equal(expected,source.SelfOnlyPrimary);
    }

    [Fact]
    public void StatisticsCapSupportDoesNotInventAnUnknownThirdMode()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RetailNpcStatSkillCombat.StatsSpellBase(100, 135, 9000, 1000, true, 3000));
        Assert.Equal(234, RetailNpcStatSkillCombat.StatsSpellBase(100, 135, 9000, 1000, false, 3400));
        // The finite historical pilot contract keeps its original rejection.
        Assert.Throws<InvalidOperationException>(() => RetailNpcStatSkillCombat.SpellBase(100, 135, 2900, 1000, true));
    }

    [Theory]
    [InlineData(0,1000,3400,true,500)]
    [InlineData(999,1000,3400,true,999)]
    [InlineData(1000,1000,3400,true,1000)]
    [InlineData(2999,1000,3400,true,1999)]
    [InlineData(3400,1000,3400,true,2200)]
    [InlineData(9000,1000,3400,true,2200)]
    [InlineData(9000,1000,2900,true,1950)]
    [InlineData(3400,2000,3400,true,1350)]
    [InlineData(-999,1000,3400,true,500)]
    [InlineData(9000,1000,3400,false,500)]
    public void AbsoluteNpcSpellsUseContinuousBoostWithoutTheWeaponPercentageBranch(int boost,uint ratio,int cap,bool enabled,int expected)
    {
        Assert.Equal(expected,RetailNpcStatSkillCombat.AbsoluteSpellBase(500,boost,ratio,enabled,cap));
    }

    [Theory]
    [InlineData(0,false)]
    [InlineData(1,true)]
    public void SourceR6SelectsTheSpellBaseAndR1RetainsLevelScaling(int mode,bool range)
    {
        string xml = Spell("1").Replace("<effect1_reserved6>1</effect1_reserved6>",
            "<effect1_reserved6>"+mode+"</effect1_reserved6>",StringComparison.Ordinal)
            .Replace("<effect1_reserved1>0</effect1_reserved1>","<effect1_reserved1>10</effect1_reserved1>",StringComparison.Ordinal);
        var source=Parse(xml);
        Assert.True(source.Supported); Assert.Equal(range,source.UsesWeaponRange);
        Assert.Equal(92,source.ValueAtSkillLevel(0)); Assert.Equal(122,source.ValueAtSkillLevel(3));
        Assert.Throws<ArgumentOutOfRangeException>(()=>source.ValueAtSkillLevel(65535));
    }

    [Fact]
    public void AbsentR6UsesTheProvedAbsoluteConstructorDefault()
    {
        var source=Parse(Spell("1").Replace("<effect1_reserved6>1</effect1_reserved6>","",StringComparison.Ordinal));
        Assert.True(source.Supported); Assert.False(source.UsesWeaponRange);
    }

    [Fact]
    public void UnknownR6AndUnprovedAdditionalConditionsRemainExplicitCompatibility()
    {
        Assert.False(Parse(Spell("1").Replace("<effect1_reserved6>1</effect1_reserved6>",
            "<effect1_reserved6>2</effect1_reserved6>",StringComparison.Ordinal)).Supported);
        Assert.False(Parse(Spell("1").Replace("<effect1_reserved6>1</effect1_reserved6>",
            "<effect1_reserved8>1</effect1_reserved8><effect1_reserved16>unproved</effect1_reserved16>",StringComparison.Ordinal)).Supported);
    }

    [Fact]
    public void ACallerChosenContextDigestDoesNotAuthorizeAGuessedCap()
    {
        byte[] guessed = Encoding.UTF8.GetBytes("{\"version\":1,\"special_server_type\":0,\"magical_boost_cap\":3400}");
        Assert.Throws<InvalidDataException>(() => RetailNpcStatSkillCombat.ParseBoostCapContext(guessed,
            Convert.ToHexStringLower(SHA256.HashData(guessed))));
    }

    [Theory]
    [InlineData(0, 99, 200)]
    [InlineData(3, 34, 180)]
    [InlineData(3, 35, 200)]
    [InlineData(3, 64, 200)]
    [InlineData(3, 65, 220)]
    [InlineData(4, 0, 200)]
    [InlineData(4, 99, 200)]
    public void PhysicalSourceRandomTablesPreserveTheirExactBins(int type, int word, int expected)
    {
        Assert.Equal((float)expected, RetailNpcStatSkillCombat.PhysicalBase(100, 100, new(0, 0), type, word));
    }

    [Theory]
    [InlineData(0,20,20,140)]
    [InlineData(100,20,20,240)]
    [InlineData(100,-20,-20,160)]
    public void StatisticsPhysicalSkillsKeepTheNativeEffectFlatIndependentOfPercent(int skillPercent,int effectPercent,int flat,int expected)
    {
        Assert.Equal((float)expected,RetailNpcStatSkillCombat.StatisticsPhysicalBase(100,skillPercent,
            new(effectPercent,flat),0,0));
    }

    [Theory]
    [InlineData(100,20,20,140)]
    [InlineData(500,20,20,620)]
    [InlineData(100,-200,20,-80)]
    [InlineData(50,int.MaxValue,0,-1073741824)]
    public void SpellBoostEffectsUseTheirOwnIndependentPairAfterTheIntegerSpellBase(int amount,int percent,int flat,int expected)
    {
        Assert.Equal(expected,RetailNpcStatSkillCombat.ApplySpellAttackModifiers(amount,new(percent,flat)));
    }

    [Theory]
    [InlineData("0", false)]
    [InlineData("1", true)]
    public void SourceParticipationFlagsRemainIndependentOfSkillFamily(string flag, bool enabled)
    {
        var source = Parse(Spell(flag));
        Assert.True(source.Supported); Assert.Equal(RetailNpcStatSkillCombat.Kind.SpellInstant, source.Family);
        Assert.Equal(16590, source.Id); Assert.Equal("NPEL_SpellELLong_Water", source.Name);
        Assert.Equal(SkillElement.WATER, source.Element);
        Assert.Equal(enabled, source.ApplyBoost); Assert.Equal(enabled, source.ApplyCritical);
    }

    [Fact]
    public void UnknownSkillFamiliesAreVisibleCompatibilityRows()
    {
        var source = Parse(Spell("1").Replace("SpellATK_Instant", "SpellATK", StringComparison.Ordinal));
        Assert.False(source.Supported); Assert.Contains("family", source.UnsupportedReason);
    }

    [Theory]
    [InlineData("<effect1_reserved2>92</effect1_reserved2><effect1_reserved2>93</effect1_reserved2>")]
    [InlineData("<effect1_unknown_damage>17</effect1_unknown_damage><effect1_reserved2>92</effect1_reserved2>")]
    [InlineData("<effect1_reserved2>92</effect1_reserved2><effect1_reserved8>3</effect1_reserved8><effect1_reserved16>unproved</effect1_reserved16>")]
    public void AnUnreviewedSpellProgramCannotEnterThePrimaryCalculator(string changed)
    {
        var source = Parse(Spell("1").Replace("<effect1_reserved2>92</effect1_reserved2>", changed, StringComparison.Ordinal));
        Assert.False(source.Supported); Assert.NotEmpty(source.UnsupportedReason!);
    }

    [Fact]
    public void HashMismatchIsAnIdentityFailureRatherThanACompatibilityFallback()
    {
        byte[] raw = Encoding.Unicode.GetBytes(Spell("1"));
        string pin = Convert.ToHexStringLower(SHA256.HashData(raw)); raw[^4] ^= 1;
        Assert.Throws<InvalidDataException>(() => RetailNpcStatSkillCombat.ParseSource(raw, pin));
    }

    [Fact]
    public void PhysicalConditionalDamageIsNotSilentlyDiscarded()
    {
        string xml = "<skill_base><id>16888</id><name>fixture</name><effect1_type>SkillATK_Instant</effect1_type>"
            + "<effect1_reserved1>0</effect1_reserved1><effect1_reserved2>68</effect1_reserved2>"
            + "<effect1_reserved10>71</effect1_reserved10></skill_base>";
        Assert.False(Parse(xml).Supported);
        var zero = Parse(xml.Replace("<effect1_reserved10>71</effect1_reserved10>", "", StringComparison.Ordinal));
        Assert.True(zero.Supported); Assert.Equal(RetailNpcStatSkillCombat.Kind.PhysicalInstant, zero.Family);
        Assert.Equal(0, zero.RandomType);
    }

    private static RetailNpcStatSkillCombat.Source Parse(string xml)
    {
        byte[] raw = Encoding.Unicode.GetBytes(xml);
        return RetailNpcStatSkillCombat.ParseSource(raw, Convert.ToHexStringLower(SHA256.HashData(raw)));
    }
    private static string Spell(string flag) => "<skill_base><id>16590</id><name>NPEL_SpellELLong_Water</name>"
        + "<sub_type>Debuff</sub_type><apply_magical_skill_boost_bonus>" + flag + "</apply_magical_skill_boost_bonus>"
        + "<apply_magical_critical>" + flag + "</apply_magical_critical><effect1_type>SpellATK_Instant</effect1_type>"
        + "<effect1_reserved1>0</effect1_reserved1><effect1_reserved2>92</effect1_reserved2>"
        + "<effect1_reserved6>1</effect1_reserved6><effect1_reserved10>Water</effect1_reserved10>"
        + "<effect1_reserved11>100</effect1_reserved11><effect1_critical_prob_mod2>100</effect1_critical_prob_mod2></skill_base>";
}
