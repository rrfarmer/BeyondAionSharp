using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcStatPeriodicConditionTests
{
    private static RetailNpcStatSkillCombat.Source Parse(string token,string range="OnlyOne")
    {
        string xml=$"<skill_base><id>910007</id><name>ConditionalPeriodicState</name>"
            +$"<first_target>Target</first_target><target_range>{range}</target_range><target_maxcount>1</target_maxcount><sub_type>Attack</sub_type>"
            +"<effect1_type>SkillATK_Instant</effect1_type><effect1_target_type>Target_Only</effect1_target_type>"
            +"<effect1_reserved2>60</effect1_reserved2><effect1_reserved6>100</effect1_reserved6><effect1_reserved7>1</effect1_reserved7>"
            +$"<effect1_reserved8>3</effect1_reserved8><effect1_reserved10>71</effect1_reserved10><effect1_reserved16>{token}</effect1_reserved16></skill_base>";
        byte[] bytes=Encoding.Unicode.GetBytes(xml);
        return RetailNpcStatSkillCombat.ParseSource(bytes,Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }

    [Theory]
    [InlineData("Poison","OnlyOne")]
    [InlineData("poison","Area")]
    [InlineData("POISON","OnlyOne")]
    [InlineData("Bleed","OnlyOne")]
    [InlineData("bleed","Area")]
    [InlineData("BLEED","OnlyOne")]
    public void PlainNativePeriodicStatusAdmitsItsFlatAdditionalProgram(string token,string range)
    {
        var source=Parse(token,range);
        Assert.True(source.Supported,source.UnsupportedReason);
        Assert.Equal(71,source.AdditionalFlat);Assert.False(source.BackOnly);Assert.False(source.StumbleOnly);
        Assert.Equal(range=="Area",source.AreaConditional);
        Assert.Equal(token.Equals("poison",StringComparison.OrdinalIgnoreCase)?1:2,source.AdditionalStatusMask);
    }

    [Theory]
    [InlineData("Poison,Bleed")]
    [InlineData("_Poison")]
    [InlineData("Poison_Stumble")]
    public void MixedOrUnprovedConditionShapesStayOutsideTheNumericProgram(string token)
        =>Assert.False(Parse(token).Supported);

    [Theory]
    [InlineData(AbnormalState.POISON,1)]
    [InlineData(AbnormalState.BLEED,2)]
    public void NativePeriodicStateRetainsOverlappingBeginsUntilTheFinalEnd(AbnormalState state,int mask)
    {
        var native=new RetailNpcStatTargetConditions();Assert.Equal(0,native.Mask);
        native.Set(state);native.Set(state);Assert.Equal(mask,native.Mask);
        native.Clear(state);Assert.Equal(mask,native.Mask);
        native.Clear(state);Assert.Equal(0,native.Mask);
    }

    [Fact]
    public void ParalyzeReadSideFollowsNativeUncountedSetAndClear()
    {
        using var h=BossAiHarness.For(220030000).Build();var player=h.SpawnPlayer();var other=h.SpawnPlayer();
        player.SetFlyController(new Aion.GameServer.Controllers.FlyController(player));
        var first=new Effect(player,player,new SkillTemplate {skillId=910017},1);
        var second=new Effect(player,player,new SkillTemplate {skillId=910018},1);
        var paralyze=new ParalyzeEffect();
        Assert.Equal(0,player.GetEffectController().RetailNpcTargetConditionMask);
        paralyze.StartEffect(first);paralyze.StartEffect(second);
        Assert.Equal(RetailNpcStatTargetConditions.ParalyzeMask,player.GetEffectController().RetailNpcTargetConditionMask);
        Assert.Equal(0,other.GetEffectController().RetailNpcTargetConditionMask);
        paralyze.EndEffect(first);
        Assert.Equal(0,player.GetEffectController().RetailNpcTargetConditionMask);
        paralyze.EndEffect(second);
        Assert.Equal(0,player.GetEffectController().RetailNpcTargetConditionMask);
        Assert.Empty(h.Clock.Faults);
    }

    [Fact]
    public void StateBitsAreSeparateFromSignedCountersAndOtherStatuses()
    {
        var native=new RetailNpcStatTargetConditions();
        native.Set(AbnormalState.STUMBLE|AbnormalState.SLEEP);Assert.Equal(0,native.Mask);
        // Native Clear does not clamp a zero counter. The following Set writes
        // its bit even though the counter has returned to zero.
        native.Clear(AbnormalState.POISON);Assert.Equal(0,native.Mask);
        native.Set(AbnormalState.POISON);Assert.Equal(1,native.Mask);
        native.Clear(AbnormalState.POISON);Assert.Equal(1,native.Mask);
        var separate=new RetailNpcStatTargetConditions();separate.Set(AbnormalState.POISON|AbnormalState.BLEED);
        separate.Clear(AbnormalState.POISON);Assert.Equal(2,separate.Mask);
        separate.Clear(AbnormalState.BLEED);Assert.Equal(0,separate.Mask);
    }

    [Fact]
    public void ConcurrentBeginsAndEndsKeepEachCounterBitEventSerialized()
    {
        var native=new RetailNpcStatTargetConditions();
        Parallel.For(0,128,_=>native.Set(AbnormalState.POISON|AbnormalState.BLEED));Assert.Equal(3,native.Mask);
        Parallel.For(0,127,_=>native.Clear(AbnormalState.POISON|AbnormalState.BLEED));Assert.Equal(3,native.Mask);
        native.Clear(AbnormalState.POISON|AbnormalState.BLEED);Assert.Equal(0,native.Mask);
    }

    [Theory]
    [InlineData(AbnormalState.POISON,1)]
    [InlineData(AbnormalState.BLEED,2)]
    public void ExistingOverTimeStartEndHooksDriveOnlyTheNpcConsumerReadSide(AbnormalState state,int mask)
    {
        using var h=BossAiHarness.For(220030000).Build();var player=h.SpawnPlayer();var other=h.SpawnPlayer();
        var first=new Effect(player,player,new SkillTemplate {skillId=910007},1);
        var second=new Effect(player,player,new SkillTemplate {skillId=910008},1);
        AbstractOverTimeEffect template=state==AbnormalState.POISON?new PoisonEffect():new BleedEffect();
        template.StartEffect(first,state);template.StartEffect(second,state);
        Assert.Equal(mask,player.GetEffectController().RetailNpcPoisonBleedMask);
        Assert.Equal(0,other.GetEffectController().RetailNpcPoisonBleedMask);
        template.EndEffect(first,state);
        Assert.Equal(mask,player.GetEffectController().RetailNpcPoisonBleedMask);
        // Original4.8 union still uses its original effect-map ownership rule.
        Assert.False(player.GetEffectController().IsAbnormalSet(state));
        template.EndEffect(second,state);Assert.Equal(0,player.GetEffectController().RetailNpcPoisonBleedMask);
        Assert.False(player.GetEffectController().RetailNpcStumbleActive);Assert.Empty(h.Clock.Faults);
    }

    [Fact]
    public void TrueStatusAddsFlatBeforeTruncationAndFalseStatusConsumesNoAdditionalDraw()
    {
        int draws=0;
        int Draw(int low,int high){Assert.Equal((1,100),(low,high));draws++;return 100;}
        float before=BitConverter.UInt32BitsToSingle(0x445bffff);
        Assert.Equal(before,RetailNpcStatSkillCombat.ConditionalAdditional(before,71,false,Draw,out var absent));
        Assert.Null(absent);Assert.Equal(0,draws);
        float after=RetailNpcStatSkillCombat.ConditionalAdditional(before,71,true,Draw,out var consumed);
        Assert.Equal(100,consumed);Assert.Equal(1,draws);Assert.Equal(950,(int)after);
        Assert.NotEqual((int)(before*1.71f),(int)after);
    }

    [SkippableFact]
    public void EveryCurrentRawPeriodicConditionProgramMatchesTheReviewedAdmissionBoundary()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_PERIODIC_CONDITION_REVIEW");
        Skip.If(string.IsNullOrWhiteSpace(path),"Requires pinned external source census");
        using var review=JsonDocument.Parse(File.ReadAllBytes(path!));int accepted=0,blocked=0;
        foreach(var p in review.RootElement.GetProperty("programs").EnumerateArray())
        {
            var source=RetailNpcStatSkillCombat.ParseSource(File.ReadAllBytes(p.GetProperty("source_path").GetString()!),p.GetProperty("source_sha256").GetString()!);
            bool expected=p.GetProperty("candidate_clean_shape").GetBoolean()||source.Id==18119;
            Assert.Equal(expected,source.Supported); // Native self-centered area uses the same per-target program.
            if(expected){accepted++;Assert.Equal(p.GetProperty("condition").GetString()=="poison"?1:2,source.AdditionalStatusMask);}
            else blocked++;
        }
        Assert.Equal((10,7),(accepted,blocked));
    }
}
