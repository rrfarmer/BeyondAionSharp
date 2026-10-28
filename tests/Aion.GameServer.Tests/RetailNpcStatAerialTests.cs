using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.SkillEngine.Condition;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcStatAerialTests
{
    [Theory]
    [InlineData(0,100)]
    [InlineData(49,100)]
    [InlineData(50,100)]
    [InlineData(51,1)]
    [InlineData(99,49)]
    [InlineData(100,50)]
    [InlineData(150,100)]
    [InlineData(65535,65485)]
    public void NativeCloseUsesAnUnsignedHitWordAndFallbackRatherThanAMinimumDelay(int word,int expected)
        =>Assert.Equal(expected,RetailNpcStatAerialCombat.CloseDelay(word));
    [Theory]
    [InlineData(-1)]
    [InlineData(65536)]
    public void AGuessedOrWrappedHitDelayCannotBeAdmitted(int word)
        =>Assert.Throws<ArgumentOutOfRangeException>(()=>RetailNpcStatAerialCombat.CloseDelay(word));
    [Theory]
    [InlineData(false,true,true,false,0)]
    [InlineData(true,true,false,false,1)]
    [InlineData(true,true,true,true,2)]
    [InlineData(true,false,false,true,1)]
    public void SecondaryConditionsUseNativeOrderAndInclusiveDraws(bool primary,bool checkStatus,bool aerial,bool expected,int expectedDraws)
    {
        var events=new List<string>();
        bool result=RetailNpcStatAerialCombat.CheckSecondary(primary,checkStatus,()=>{events.Add("status");return aerial;},(lo,hi)=>
        {Assert.Equal((0,100),(lo,hi));events.Add("draw");return 100;});
        Assert.Equal(expected,result);Assert.Equal(expectedDraws,events.Count(x=>x=="draw"));
        if(primary&&checkStatus)Assert.Equal(new[]{"draw","status"},events.Take(2));
        else Assert.DoesNotContain("status",events);
    }
    [Fact]
    public void AerialBitHasNoReferenceCounterAndIsIndependentOfPeriodicCounters()
    {
        var state=new RetailNpcStatTargetConditions();
        state.Set(AbnormalState.OPENAERIAL|AbnormalState.POISON);state.Set(AbnormalState.OPENAERIAL|AbnormalState.POISON);
        Assert.Equal(65537,state.Mask);
        state.Clear(AbnormalState.OPENAERIAL|AbnormalState.POISON);Assert.Equal(1,state.Mask);
        state.Clear(AbnormalState.POISON);Assert.Equal(0,state.Mask);
        state.Clear(AbnormalState.OPENAERIAL);state.Set(AbnormalState.OPENAERIAL);Assert.Equal(65536,state.Mask);
    }
    [Fact]
    public void OneLeaseRemovesCurrentOwnersAtCallbackTimeAndCannotScheduleTwice()
    {
        var lease=new RetailNpcStatAerialCombat.CloseLease();var queued=new List<(Action Run,int Delay)>();
        var active=new List<int>{111};
        void Schedule(Action action,int delay)=>queued.Add((action,delay));
        Assert.False(lease.TrySchedule(false,true,200,Schedule,active.Clear));
        Assert.False(lease.TrySchedule(true,false,200,Schedule,active.Clear));
        Assert.Empty(queued);
        Assert.True(lease.TrySchedule(true,true,200,Schedule,active.Clear));
        Assert.False(lease.TrySchedule(true,true,200,Schedule,active.Clear));
        Assert.Single(queued);Assert.Equal(150,queued[0].Delay);Assert.Single(active);
        active.Add(222);queued[0].Run();Assert.Empty(active);
    }

    private static RetailNpcStatAerialCombat.CloseProgram Program(bool status=true)=>new(SkillElement.WATER,status,60,60);
    private static CloseAerialEffect Host(bool status=true)=>new(){Position=2,NoResist=true,Element=SkillElement.WATER,
        PreEffects=[1],PreEffectProb=100,Hoptype=HopType.SKILLLV,HopA=60,HopB=60,
        EffectConditions=status?new Conditions {conditions=[new AbnormalStateCondition {Value=AbnormalState.OPENAERIAL}]}:null};
    [Fact]
    public void OnlyTheReviewedSecondaryHostCanShareThePrimaryProgram()
    {
        Assert.True(RetailNpcStatAerialCombat.HostMatches(Program(),Host()));
        Assert.True(RetailNpcStatAerialCombat.HostMatches(Program(false),Host(false)));
        var host=Host();host.NoResist=false;Assert.False(RetailNpcStatAerialCombat.HostMatches(Program(),host));
        host=Host();host.Element=SkillElement.FIRE;Assert.False(RetailNpcStatAerialCombat.HostMatches(Program(),host));
        host=Host();host.PreEffects=[2];Assert.False(RetailNpcStatAerialCombat.HostMatches(Program(),host));
        host=Host();host.PreEffectProb=99;Assert.False(RetailNpcStatAerialCombat.HostMatches(Program(),host));
        host=Host();host.EffectConditions=null;Assert.False(RetailNpcStatAerialCombat.HostMatches(Program(),host));
        host=Host();host.Duration2=100;Assert.False(RetailNpcStatAerialCombat.HostMatches(Program(),host));
        host=Host();host.HopA=59;Assert.False(RetailNpcStatAerialCombat.HostMatches(Program(),host));
    }

    // Skip motion/packet setup in this detached owner test; production EndEffect
    // and EffectController registration/removal still run unchanged.
    private sealed class AerialOwner:OpenAerialEffect
    {
        public override void StartEffect(Effect e)
        {e.SetAbnormal(AbnormalState.OPENAERIAL);e.GetEffected().GetEffectController().SetAbnormal(AbnormalState.OPENAERIAL);}
    }
    private static Effect AddOwner(Player player,int id)
    {
        var template=new AerialOwner {Position=1,Duration2=10000};
        var skill=new SkillTemplate {skillId=id,stack="aerial-"+id,effects=new Effects {effects=[template]}};
        var effect=new Effect(player,player,skill,1);effect.AddSuccessEffect(template);effect.ApplyEffect();return effect;
    }
    [Fact]
    public void NativeTypeRemovalEndsEveryAerialOwnerAcrossSkillIds()
    {
        using var h=BossAiHarness.For(220030000).Build();var player=h.SpawnPlayer();
        var a=AddOwner(player,910021);var b=AddOwner(player,910022);
        Assert.Contains(a,player.GetEffectController().GetAllEffects());Assert.Contains(b,player.GetEffectController().GetAllEffects());
        Assert.Equal(65536,player.GetEffectController().RetailNpcTargetConditionMask);
        player.GetEffectController().RemoveRetailNpcAerialEffects();
        Assert.DoesNotContain(a,player.GetEffectController().GetAllEffects());Assert.DoesNotContain(b,player.GetEffectController().GetAllEffects());
        Assert.Equal(0,player.GetEffectController().RetailNpcTargetConditionMask);Assert.Empty(h.Clock.Faults);
    }

    private sealed class TimedSkill(SkillTemplate template,Creature caster,Creature target):Skill(template,caster,1,target,null)
    {
        internal void SetWord(int word){SetClientHitTime(word);UpdateHitTime(false);}
    }
    [Fact]
    public void SelectedSecondarySchedulesOnceAndApplyCannotRemoveOwnersBeforeTheNativeDeadline()
    {
        using var h=BossAiHarness.For(220030000).Build();var player=h.SpawnPlayer();
        var caster=h.SpawnWithAi(210548,"general");
        var first=AddOwner(player,910021);var second=AddOwner(player,910022);
        var primary=new SkillAttackInstantEffect {Position=1};var close=Host();
        var template=new SkillTemplate {skillId=910023,stack="aerial-close",effects=new Effects {effects=[primary,close]}};
        var skill=new TimedSkill(template,caster,player);skill.SetWord(200);
        var effect=new Effect(skill,player);
        var source=new RetailNpcStatSkillCombat.Source(910023,"DetachedAerial",new string('0',64),
            RetailNpcStatSkillCombat.Kind.PhysicalInstant,60,SkillElement.NONE,false,true,3,100,null,
            AdditionalStatusMask:65536,AerialClose:Program());
        Assert.True(RetailNpcStatAerialCombat.Preflight(effect,source,out string reason),reason);
        RetailNpcStatAerialCombat.Prepare(effect,source);effect.AddSuccessEffect(primary);
        close.Calculate(effect);close.Calculate(effect);close.ApplyEffect(effect);
        Assert.True(effect.IsInSuccessEffects(2));Assert.Equal(SpellStatus.CLOSEAERIAL,effect.GetSpellStatus());
        Assert.Contains(first,player.GetEffectController().GetAllEffects());
        h.Clock.Advance(TimeSpan.FromMilliseconds(149));
        Assert.Contains(second,player.GetEffectController().GetAllEffects());
        h.Clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.DoesNotContain(first,player.GetEffectController().GetAllEffects());
        Assert.DoesNotContain(second,player.GetEffectController().GetAllEffects());
        Assert.Equal(0,player.GetEffectController().RetailNpcTargetConditionMask);Assert.Empty(h.Clock.Faults);
    }

    private static RetailNpcStatSkillCombat.Source Parse(XElement xml)
    {
        byte[] bytes=Encoding.Unicode.GetBytes(xml.ToString(SaveOptions.DisableFormatting));
        return RetailNpcStatSkillCombat.ParseSource(bytes,Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }
    [SkippableFact]
    public void EveryAerialSourceHasAnIndependentPeriodicConditionControlAndMatchingHost()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? pin=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        string? hostPath=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_HOST_SKILLS");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(pin)||string.IsNullOrEmpty(hostPath),"Requires complete pinned native and host sources");
        byte[] raw=File.ReadAllBytes(path!);Assert.Equal(pin,Convert.ToHexStringLower(SHA256.HashData(raw)));
        byte[] hostBytes=File.ReadAllBytes(hostPath!);
        Assert.Equal("e06ee05fa4f56bc4107c1be929dc0fecb8ec8a8dfbeaf26a235446461a14ace0",Convert.ToHexStringLower(SHA256.HashData(hostBytes)));
        var hosts=XDocument.Parse(Encoding.UTF8.GetString(hostBytes)).Root!.Elements().ToDictionary(x=>(int)x.Attribute("skill_id")!);
        using var manifest=JsonDocument.Parse(raw);var rows=new List<object>();
        foreach(var asset in manifest.RootElement.GetProperty("skill_sources").EnumerateArray())
        {
            byte[] bytes=File.ReadAllBytes(Path.GetFullPath(asset.GetProperty("path").GetString()!,Path.GetDirectoryName(path!)!));
            var xml=XElement.Parse(Encoding.Unicode.GetString(bytes));
            if(xml.Element("effect1_type")?.Value!="SkillATK_Instant"||!string.Equals(xml.Element("effect1_reserved16")?.Value,"OpenAerial",StringComparison.OrdinalIgnoreCase))continue;
            var source=RetailNpcStatSkillCombat.ParseSource(bytes,asset.GetProperty("sha256").GetString()!);
            // Canonicalize the collector's center only in the independent scalar
            // control. Neither operand program reads that center or a target count.
            if(xml.Element("target_range")?.Value=="Area"&&xml.Element("first_target")?.Value=="Me")
                xml.SetElementValue("first_target","Target");
            xml.SetElementValue("effect1_reserved16","Poison");xml.Elements().Where(x=>x.Name.LocalName.StartsWith("effect2_",StringComparison.Ordinal)).Remove();
            var control=Parse(xml);
            bool close=hosts[source.Id].Element("effects")?.Elements().Any(x=>x.Name=="closeaerial")==true;
            bool expected=control.Supported&&source.Id!=18616; //Actual sibling Stun, not CloseAerial.
            Assert.Equal(expected,source.Supported);
            bool hostMatch=false;
            if(source.Supported)
            {
                Assert.NotNull(source.AerialClose);Assert.True(close);
                Assert.Equal(control,source with {Sha256=control.Sha256,AdditionalStatusMask=1,AerialClose=null});
                using var reader=hosts[source.Id].CreateReader();
                var template=(SkillTemplate)new XmlSerializer(typeof(SkillTemplate),new XmlRootAttribute("skill_template")).Deserialize(reader)!;
                hostMatch=RetailNpcStatAerialCombat.HostMatches(source.AerialClose,template.GetEffectTemplate(2)!);
                Assert.True(hostMatch);Assert.True(RetailNpcStatSkillCombat.PhysicalHostMatches(source,template.GetEffectTemplate(1)!));
            }
            rows.Add(new {id=source.Id,supported=source.Supported,reason=source.UnsupportedReason,close=source.AerialClose,hostMatch,source=source.Sha256});
        }
        Assert.Equal(17,rows.Count);
        if(Environment.GetEnvironmentVariable("AION_RETAIL_STATS_AERIAL_OUTPUT") is {Length:>0} output)
        {using var stream=new FileStream(output,FileMode.CreateNew,FileAccess.Write);JsonSerializer.Serialize(stream,new {rows});}
    }
}
