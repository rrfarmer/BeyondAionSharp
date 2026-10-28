using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Controllers.Effects;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcStatStumbleTests
{
    private const string Recipe="<skill_base><id>910001</id><name>SyntheticConditional</name>"
        +"<first_target>Target</first_target><target_range>OnlyOne</target_range><target_maxcount>1</target_maxcount><sub_type>Attack</sub_type>"
        +"<effect1_type>SkillATK_Instant</effect1_type><effect1_target_type>Target_Only</effect1_target_type><effect1_reserved1>0</effect1_reserved1><effect1_reserved2>60</effect1_reserved2>"
        +"<effect1_reserved6>100</effect1_reserved6><effect1_reserved7>1</effect1_reserved7><effect1_reserved8>3</effect1_reserved8>"
        +"<effect1_reserved10>71</effect1_reserved10><effect1_reserved16>stumble</effect1_reserved16></skill_base>";
    private static RetailNpcStatSkillCombat.Source Parse(string xml)
    {
        byte[] raw=Encoding.Unicode.GetBytes(xml);
        return RetailNpcStatSkillCombat.ParseSource(raw,Convert.ToHexStringLower(SHA256.HashData(raw)));
    }

    [Theory]
    [InlineData("stumble")]
    [InlineData("Stumble")]
    [InlineData("STUMBLE")]
    public void NativeNamedConditionIsOperandSelectedAndCaseInsensitive(string token)
    {
        var s=Parse(Recipe.Replace(">stumble<",$">{token}<"));
        Assert.True(s.Supported,s.UnsupportedReason);Assert.True(s.StumbleOnly);Assert.False(s.BackOnly);
        Assert.Equal(71,s.AdditionalFlat);Assert.Equal(60,s.Percent);
    }
    [Theory]
    [InlineData(">stumble<",">stagger<")]
    [InlineData(">stumble<",">stumble,poison<")]
    [InlineData("</skill_base>","<effect1_reserved11>-1</effect1_reserved11></skill_base>")]
    [InlineData("</skill_base>","<effect1_reserved14>Stumble</effect1_reserved14></skill_base>")]
    [InlineData("</skill_base>","<effect2_type>Stumble</effect2_type></skill_base>")]
    [InlineData("<target_range>OnlyOne</target_range>","<target_range>Point</target_range>")]
    public void UnprovedStateUnionsProcSelectorsAndTargetFramesStayVisible(string from,string to)
        =>Assert.False(Parse(Recipe.Replace(from,to)).Supported);

    [Theory]
    [InlineData(3)]
    [InlineData(24)]
    [InlineData(96)]
    [InlineData(120)]
    public void CleanAreaTargetsReuseThePerTargetProgramWithoutCountDamageScaling(int count)
    {
        string area=Recipe.Replace("<target_range>OnlyOne</target_range>","<target_range>Area</target_range>")
            .Replace("<target_maxcount>1</target_maxcount>",$"<target_maxcount>{count}</target_maxcount>");
        var source=Parse(area);Assert.True(source.Supported,source.UnsupportedReason);
        Assert.True(source.AreaConditional);Assert.True(source.StumbleOnly);Assert.False(source.BackOnly);
        Assert.Equal(60,source.ValueAtSkillLevel(11)); // Count never enters the numeric source scalar.
    }

    [Fact]
    public void AreaPhysicalRateUsesTheWrappedSignedLevelFormulaBeforeFloatConversion()
    {
        string area=Recipe.Replace("<target_range>OnlyOne</target_range>","<target_range>Area</target_range>")
            .Replace("<effect1_reserved1>0</effect1_reserved1>","<effect1_reserved1>10</effect1_reserved1>");
        var source=Parse(area);Assert.True(source.Supported,source.UnsupportedReason);
        Assert.Equal(10,source.LevelRate);Assert.Equal(170,source.ValueAtSkillLevel(11));
        Assert.Equal(60,source.ValueAtSkillLevel(0));
        Assert.False(Parse(area.Replace(">stumble<",">_Back<")).Supported);
        var centered=Parse(area.Replace("<first_target>Target</first_target>","<first_target>Me</first_target>"));
        Assert.True(centered.Supported,centered.UnsupportedReason);
        Assert.Equal(source,centered with {Sha256=source.Sha256}); // Collector center is not a numeric input.
        Assert.False(Parse(area.Replace("</skill_base>","<effect1_reserved19>1</effect1_reserved19></skill_base>")).Supported);
    }

    [SkippableFact]
    public void EveryReviewedActualAreaRecipeKeepsItsConstructedDefaultsAndLevelRate()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        string? reviewPath=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_AREA_REVIEW");
        string? reviewHash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_AREA_REVIEW_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash)||string.IsNullOrEmpty(reviewPath)||string.IsNullOrEmpty(reviewHash),
            "Requires the external source manifest and pinned native area contract");
        var bytes=File.ReadAllBytes(path!);Assert.Equal(hash,Convert.ToHexStringLower(SHA256.HashData(bytes)));
        using var manifest=System.Text.Json.JsonDocument.Parse(bytes);
        bytes=File.ReadAllBytes(reviewPath!);Assert.Equal(reviewHash,Convert.ToHexStringLower(SHA256.HashData(bytes)));
        using var review=System.Text.Json.JsonDocument.Parse(bytes);int count=0;
        foreach(var id in review.RootElement.GetProperty("clean_source_ids_for_inventory_only").EnumerateArray())
        {
            var asset=manifest.RootElement.GetProperty("skill_sources").EnumerateArray().Single(a=>a.GetProperty("id").GetInt32()==id.GetInt32());
            var source=RetailNpcStatSkillCombat.ParseSource(File.ReadAllBytes(asset.GetProperty("path").GetString()!),asset.GetProperty("sha256").GetString()!);
            Assert.True(source.Supported,source.UnsupportedReason);Assert.True(source.AreaConditional);Assert.True(source.StumbleOnly);
            Assert.Equal(id.GetInt32()==18747?10:0,source.LevelRate);count++;
        }
        Assert.Equal(9,count);
    }

    [Fact]
    public void ConditionalFlatUsesOneExtraDrawOnlyForTrueStateBeforeTruncation()
    {
        float amount=RetailNpcStatSkillCombat.StatisticsPhysicalBase(500,60,new(0,0),3,99);
        Assert.Equal(0x445bffffu,BitConverter.SingleToUInt32Bits(amount));
        Assert.Equal(amount,RetailNpcStatSkillCombat.ConditionalAdditional(amount,71,false,(_,_)=>throw new Exception("Must not draw"),out var absent));
        Assert.Null(absent);int calls=0;
        float with=RetailNpcStatSkillCombat.ConditionalAdditional(amount,71,true,(lo,hi)=>{Assert.Equal((1,100),(lo,hi));calls++;return 100;},out var word);
        Assert.Equal(1,calls);Assert.Equal(100,word);Assert.Equal(950,(int)with);
        Assert.Equal(amount+71f,with);
    }

    [Fact]
    public void RelatedStatesAndAnotherPlayerDoNotSetTheNativeStumblePredicate()
    {
        using var h=BossAiHarness.For(220030000).Build();var player=h.SpawnPlayer();var other=h.SpawnPlayer();
        var controller=player.GetEffectController();Assert.False(controller.RetailNpcStumbleActive);
        foreach(var state in new[]{AbnormalState.STAGGER,AbnormalState.OPENAERIAL,AbnormalState.SPIN,AbnormalState.PULLED})
        {
            controller.SetAbnormal(state);Assert.True(controller.IsAbnormalSet(state));Assert.False(controller.RetailNpcStumbleActive);
            controller.UnsetAbnormal(state);
        }
        controller.SetAbnormal(AbnormalState.STUMBLE);Assert.True(controller.RetailNpcStumbleActive);
        Assert.False(other.GetEffectController().RetailNpcStumbleActive);controller.UnsetAbnormal(AbnormalState.STUMBLE);
        Assert.False(controller.RetailNpcStumbleActive);
    }

    [Fact]
    public void NativeNpcStateClearsOnEndWithoutChangingLegacyPlayerOwnerRetention()
    {
        using var h=BossAiHarness.For(220030000).Build();var player=h.SpawnPlayer();var controller=player.GetEffectController();
        var map=(Dictionary<string,Effect>)typeof(EffectController).GetField("abnormalEffectMap",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(controller)!;
        var first=new Effect(player,player,new SkillTemplate {skillId=910001},1);
        var second=new Effect(player,player,new SkillTemplate {skillId=910002},1);
        first.SetAbnormal(AbnormalState.STUMBLE);second.SetAbnormal(AbnormalState.STUMBLE);map.Add("first",first);map.Add("second",second);
        controller.SetAbnormal(AbnormalState.STUMBLE);controller.SetAbnormal(AbnormalState.STUMBLE);
        controller.UnsetAbnormal(AbnormalState.STUMBLE);
        Assert.True(controller.IsAbnormalSet(AbnormalState.STUMBLE)); // Existing player/PvP union unchanged.
        Assert.False(controller.RetailNpcStumbleActive); // Native End always clears14.
        controller.SetAbnormal(AbnormalState.STUMBLE);Assert.True(controller.RetailNpcStumbleActive); // Resume/new Begin.
        map.Remove("first");controller.UnsetAbnormal(AbnormalState.STUMBLE);
        Assert.False(controller.RetailNpcStumbleActive);Assert.False(controller.IsAbnormalSet(AbnormalState.STUMBLE));map.Clear();
    }

    [Fact]
    public void ActualStumbleStartAndEndDriveTheNpcReadSide()
    {
        using var h=BossAiHarness.For(220030000).Build();var player=h.SpawnPlayer();
        player.SetFlyController(new Aion.GameServer.Controllers.FlyController(player));
        var effect=new Effect(player,player,new SkillTemplate {skillId=910003},1);
        effect.SetTargetLoc(player.GetX(),player.GetY(),player.GetZ());var template=new StumbleEffect();
        template.StartEffect(effect);Assert.True(player.GetEffectController().RetailNpcStumbleActive);
        template.EndEffect(effect);Assert.False(player.GetEffectController().RetailNpcStumbleActive);Assert.Empty(h.Clock.Faults);
    }
}
