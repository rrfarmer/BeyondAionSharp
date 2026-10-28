using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;
using K=Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcStatStatusSources.StatusKind;
using S=Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcStatPhysicalStatuses;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcStatPhysicalForceTests
{
    private static (byte[] Raw,string Hash) Record(string fields)
    {
        byte[] bytes=Encoding.Unicode.GetBytes("<skill_base><id>910011</id><name>NativeStatusWitness</name>"+fields+"</skill_base>");
        return(bytes,Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }
    [Fact]
    public void PhysicalForceIsNotTheNoResistFlag()
    {
        var input=Record("<effect1_type>SkillATK_Instant</effect1_type><effect1_reserved2>100</effect1_reserved2><effect1_reserved11>255</effect1_reserved11>");
        var source=RetailNpcStatSkillCombat.ParseSource(input.Raw,input.Hash);
        Assert.True(source.Supported,source.UnsupportedReason);Assert.False(source.NoResist);Assert.Equal(255,source.ForceThreshold);
    }
    [Theory]
    [InlineData("AlwaysDodge",(int)K.Dodge)]
    [InlineData("AlwaysBlock",(int)K.Block)]
    [InlineData("AlwaysParry",(int)K.Parry)]
    public void SourceOwnerUsesR8R9ForChargesAndR13ForStrength(string type,int kind)
    {
        var input=Record($"<effect1_type>{type}</effect1_type><effect1_reserved9>12</effect1_reserved9><effect1_reserved13>3</effect1_reserved13>");
        var source=RetailNpcStatStatusSources.ParseDefinition(input.Raw,input.Hash);var part=Assert.Single(source.Parts);
        Assert.True(part.Supported);Assert.Equal((K)kind,part.Kind);Assert.Equal((0,12,3),(part.Rate,part.Constant,part.Strength));
        Assert.Equal(input.Hash,part.SourceSha256);
    }
    [Fact]
    public void MissingPriorityUsesTheNativePositiveConstructorAndVariableChargesStayVisible()
    {
        var input=Record("<effect1_type>AlwaysDodge</effect1_type><effect1_reserved8>1</effect1_reserved8><effect1_reserved9>2</effect1_reserved9>");
        var part=Assert.Single(RetailNpcStatStatusSources.ParseDefinition(input.Raw,input.Hash).Parts);
        Assert.Equal(1,part.Strength);Assert.False(part.Supported);
        Assert.Equal("unverified-level-dependent-defense-charge-owner",part.UnsupportedReason);
    }
    [Theory]
    [InlineData(0,40,65,40)]
    [InlineData(95,0,1,95)]
    [InlineData(95,0,2,100)]
    [InlineData(0,150,1,100)]
    [InlineData(65535,1,1,0)]
    [InlineData(65535,0,1,100)]
    [InlineData(0,0,1,0)]
    public void BlindChanceNarrowsBeforeCappingAndKeepsItsSeparateState(int rate,int constant,int level,int expected)
    {
        var input=Record($"<effect1_type>Blind</effect1_type><effect1_reserved1>{rate}</effect1_reserved1><effect1_reserved2>{constant}</effect1_reserved2>");
        var part=Assert.Single(RetailNpcStatStatusSources.ParseDefinition(input.Raw,input.Hash).Parts);
        Assert.True(part.Supported);Assert.Equal(expected,part.BlindChance(level));
        Assert.Throws<ArgumentOutOfRangeException>(()=>part.BlindChance(32768));
    }
    [Fact]
    public void RepeatedOwnerOperandsAndChangedBytesCannotBind()
    {
        var input=Record("<effect1_type>AlwaysDodge</effect1_type><effect1_reserved9>1</effect1_reserved9><effect1_reserved9>2</effect1_reserved9>");
        Assert.Throws<InvalidDataException>(()=>RetailNpcStatStatusSources.ParseDefinition(input.Raw,input.Hash));
        input=Record("<effect1_type>AlwaysDodge</effect1_type>");input.Raw[2]^=1;
        Assert.Throws<InvalidDataException>(()=>RetailNpcStatStatusSources.ParseDefinition(input.Raw,input.Hash));
    }
    private sealed class Counter(int value,int id=1923) : S.ICounter
    {
        internal int value=value;internal int EndCalls;internal Action? OnEnd;
        public int Count=>value;public int Decrement()=>unchecked(--value);
        public void EndOwner(){EndCalls++;OnEnd?.Invoke();}
        public int SkillId=>id;public int Position=>1;public string SourceSha256=>new('a',64);
    }
    [Theory]
    [InlineData(0,1,false,1)]
    [InlineData(1,1,true,3)]
    [InlineData(255,255,true,3)]
    [InlineData(255,256,false,1)]
    public void SignedForceComparisonAndConsumptionAreSeparate(int force,int strength,bool hit,int operations)
    {
        var state=new S.State();var counter=new Counter(2);state.Begin(K.Dodge,counter,strength,true);
        var result=state.Resolve(force);Assert.Equal(hit,result.Hit);Assert.Equal(1,counter.Count);
        Assert.Equal(operations,result.Consumptions.Count);Assert.False(result.NaturalDodge);
    }
    [Fact]
    public void LaterFamiliesUseFreshReadsAfterRemovalCallbacks()
    {
        var state=new S.State();var dodge=new Counter(1);var oldBlock=new Counter(99);var newBlock=new Counter(10);
        state.Begin(K.Dodge,dodge,2,true);state.Begin(K.Block,oldBlock,50,true);
        dodge.OnEnd=()=>{state.End(K.Dodge,dodge);state.Begin(K.Block,newBlock,5,true);};
        var result=state.Resolve(3);Assert.True(result.Hit);Assert.Equal(2,result.CachedDefense);
        Assert.Equal(99,oldBlock.Count);Assert.Equal(9,newBlock.Count);Assert.Equal(1,dodge.EndCalls);
        Assert.Equal(5,result.Consumptions[1].Before.Strength);
    }
    [Fact]
    public void ParryCanReplaceTheEarlierForcedBlockCache()
    {
        var state=new S.State();var block=new Counter(2);var parry=new Counter(2);
        state.Begin(K.Block,block,9,true);state.Begin(K.Parry,parry,1,true);
        var result=state.Resolve(0);Assert.True(result.Hit);Assert.Equal(1,result.CachedDefense);
        Assert.True(result.NaturalDodge);Assert.Equal((1,1),(block.Count,parry.Count));
    }
    [Fact]
    public void EndingAnOlderOwnerClearsThePairWithoutRestoringAnother()
    {
        var state=new S.State();var older=new Counter(1);var newer=new Counter(2,3195);
        state.Begin(K.Dodge,older,1,true);state.Begin(K.Dodge,newer,1,true);state.End(K.Dodge,older);
        Assert.Equal((0,0),(state.Read(K.Dodge).Strength,state.Read(K.Dodge).Count));
        Assert.True(state.Resolve(255).Hit);Assert.Equal(2,newer.Count);
    }
    [Fact]
    public void TheOuterBindingClearPreservesACallbackReplacementPair()
    {
        var state=new S.State();var older=new Counter(1);var newer=new Counter(2,3195);
        older.OnEnd=()=>{state.End(K.Dodge,older);state.Begin(K.Dodge,newer,9,true);};
        state.Begin(K.Dodge,older,1,true);Assert.True(state.Resolve(255).Hit);
        Assert.Equal((9,2),(state.Read(K.Dodge).Strength,state.Read(K.Dodge).Count));Assert.Null(state.Read(K.Dodge).OwnerSkill);
        Assert.False(state.Resolve(0).Hit);Assert.False(state.Resolve(0).Hit);
        Assert.Equal(0,newer.Count);Assert.Equal(0,newer.EndCalls);
        Assert.False(state.Resolve(0).Hit); // Zero count alone does not clear strength.
    }
    [Theory]
    [InlineData(0,0,false)]
    [InlineData(-1,-2,true)]
    [InlineData(int.MinValue,int.MaxValue,false)]
    public void CounterDecrementUsesNonzeroAndSignedWrappedExpiry(int before,int after,bool expired)
    {
        var state=new S.State();var counter=new Counter(before);state.Begin(K.Dodge,counter,1,true);
        var step=state.Resolve(255).Consumptions[0];Assert.Equal(after,counter.Count);
        Assert.Equal(before!=0,step.Decremented);Assert.Equal(expired,step.Expired);
    }
    [Fact]
    public void DisabledTableFamiliesAreNotQueriedOrConsumed()
    {
        var state=new S.State();var dodge=new Counter(2);var parry=new Counter(2);
        state.Begin(K.Dodge,dodge,1,true);state.Begin(K.Parry,parry,1,true);
        var result=state.Resolve(255,false,false);Assert.True(result.Hit);Assert.Equal(2,dodge.Count);Assert.Equal(2,parry.Count);
        Assert.Equal(K.Block,Assert.Single(result.Consumptions).Kind);
    }
    [Fact]
    public void UnmappedActiveOwnersRemainAVisiblePreflightBoundary()
    {
        var state=new S.State();var counter=new Counter(1);state.Begin(K.Dodge,counter,0,false,"unmapped-status-source");
        Assert.False(state.CanRead(out string reason));Assert.Equal("unmapped-status-source",reason);Assert.Equal(1,counter.Count);
        Assert.Throws<InvalidOperationException>(()=>state.Resolve(255));Assert.Equal(1,counter.Count);
        state.End(K.Dodge,counter);Assert.True(state.CanRead(out _));
    }
    [Theory]
    [InlineData((int)K.Dodge,AttackStatus.DODGE)]
    [InlineData((int)K.Block,AttackStatus.BLOCK)]
    [InlineData((int)K.Parry,AttackStatus.PARRY)]
    public void LegacyChecksStillConsumeTheirOwnObserverAndEndExactlyOnce(int kind,AttackStatus status)
    {
        using var h=BossAiHarness.For(220030000).Build();var player=h.SpawnPlayer();
        EffectTemplate template=(K)kind switch {K.Dodge=>new AlwaysDodgeEffect(),K.Block=>new AlwaysBlockEffect(),_=>new AlwaysParryEffect()};
        template.Value=2;template.Position=1;var effect=new Effect(player,player,new SkillTemplate {skillId=910011,stack="NativeStatusWitness"},1);
        effect.AddSuccessEffect(template);template.StartEffect(effect);
        Assert.False(player.GetObserveController().CheckAttackStatus(AttackStatus.RESIST));
        Assert.True(player.GetObserveController().CheckAttackStatus(status));Assert.True(player.GetObserveController().CheckAttackStatus(status));
        Assert.False(player.GetObserveController().CheckAttackStatus(status));
    }

    [SkippableFact]
    public void EverySelectedStatusDefinitionBindsTheExistingHostSlotAndChargeOwner()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_STATUS_CONTEXT");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_STATUS_CONTEXT_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash),"Requires the pinned external status source context");
        var library=RetailNpcStatStatusSources.Parse(File.ReadAllBytes(path!),hash!,Path.GetDirectoryName(path!)!,
            (asset,directory)=>File.ReadAllBytes(Path.GetFullPath(asset.GetProperty("path").GetString()!,directory)));
        using var h=BossAiHarness.For(220030000).Build();var player=h.SpawnPlayer();int defenses=0,blinds=0;
        foreach(var definition in library.Definitions)
        {
            var skill=DataManager.SKILL_DATA.GetSkillTemplate(definition.Id);Assert.NotNull(skill);
            foreach(var part in definition.Parts)
            {
                var template=skill.GetEffectTemplate(part.Position);Assert.NotNull(template);
                Assert.True(part.Kind switch {K.Dodge=>template is AlwaysDodgeEffect,K.Block=>template is AlwaysBlockEffect,
                    K.Parry=>template is AlwaysParryEffect,K.Blind=>template is BlindEffect,_=>false});
                var effect=new Effect(player,player,skill,1);
                Assert.True(library.TryGet(effect,template,part.Kind,out var selected,out string reason),definition.Id+"/"+part.Position+": "+reason);
                Assert.Same(part,selected);if(part.Kind==K.Blind)blinds++;else defenses++;
            }
        }
        Assert.Equal((34,79),(defenses,blinds));
    }

    [SkippableFact]
    public void ActualPhysicalForceSourcesChangeOnlyTheirFormerForceGate()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash),"Requires pinned external retail skill sources");
        byte[] raw=File.ReadAllBytes(path!);Assert.Equal(hash,Convert.ToHexStringLower(SHA256.HashData(raw)));
        using var manifest=JsonDocument.Parse(raw);var rows=new List<object>();int reviewed=0,eligible=0,physical=0,spell=0;
        foreach(var asset in manifest.RootElement.GetProperty("skill_sources").EnumerateArray())
        {
            byte[] bytes=File.ReadAllBytes(asset.GetProperty("path").GetString()!);
            var original=RetailNpcStatSkillCombat.ParseSource(bytes,asset.GetProperty("sha256").GetString()!);
            if(original.Supported){if(original.Family==RetailNpcStatSkillCombat.Kind.PhysicalInstant)physical++;
                else if(original.Family==RetailNpcStatSkillCombat.Kind.SpellInstant)spell++;}
            var xml=XElement.Parse(Encoding.Unicode.GetString(bytes));
            if(xml.Element("effect1_type")?.Value!="SkillATK_Instant")continue;
            int force=(int?)xml.Element("effect1_reserved11")??0;if(force==0)continue;
            reviewed++;xml.SetElementValue("effect1_reserved11",0);
            byte[] zeroBytes=Encoding.Unicode.GetBytes(xml.ToString(SaveOptions.DisableFormatting));
            var zero=RetailNpcStatSkillCombat.ParseSource(zeroBytes,Convert.ToHexStringLower(SHA256.HashData(zeroBytes)));
            Assert.Equal(force>=0&&zero.Supported,original.Supported);
            if(original.Supported){eligible++;Assert.Equal(force,original.ForceThreshold);}
            rows.Add(new {id=original.Id,force,supported=original.Supported,reason=original.UnsupportedReason});
        }
        Assert.Equal((133,114,RetailNpcStatReviewedSourceCensus.Physical,RetailNpcStatReviewedSourceCensus.Spell),(reviewed,eligible,physical,spell));
        string? output=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_FORCE_OUTPUT");
        if(!string.IsNullOrEmpty(output))
        {
            using var stream=new FileStream(output,FileMode.CreateNew,FileAccess.Write);
            JsonSerializer.Serialize(stream,new {reviewed,eligible,physical,spell,rows});
        }
    }
}
