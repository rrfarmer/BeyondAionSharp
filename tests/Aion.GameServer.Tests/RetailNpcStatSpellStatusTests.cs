using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Model;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcStatSpellStatusTests
{
    private static RetailNpcStatSpellBonus.Program Program(string condition)
    {
        Assert.True(RetailNpcStatSpellBonus.TrySpellCondition(condition,null,out uint c,out int? r,out int s));
        return new(0,4000,false,c,r,StatusMask:s);
    }

    [Theory]
    [InlineData("Poison",1)] [InlineData("pOISON",1)] [InlineData("_dmg_Poison",1)]
    [InlineData("Bleed",2)] [InlineData("_dmg_bleed",2)]
    [InlineData("OpenAerial",65536)] [InlineData("_dmg_OpenAerial",65536)]
    public void RecognizedNativeStatusesRequireTheirEventBit(string condition,int mask)
    {
        var p=Program(condition);Assert.Equal(mask,p.StatusMask);
        Assert.False(p.AppliesTo(PlayerClass.WARRIOR,Race.ASMODIANS,0));
        Assert.True(p.AppliesTo(PlayerClass.WARRIOR,Race.ASMODIANS,mask));
        Assert.False(p.AppliesTo(PlayerClass.WARRIOR,Race.ASMODIANS,mask==1?2:1));
    }

    [Fact]
    public void StatusesAndClassesAreNativeOrPredicatesRatherThanAnUnconditionalBonus()
    {
        var p=Program("Poison,\t_Class_priest _dmg_Bleed");
        Assert.Equal(3,p.StatusMask);
        Assert.True(p.AppliesTo(PlayerClass.CLERIC,Race.ASMODIANS,0));
        Assert.True(p.AppliesTo(PlayerClass.PRIEST,Race.ASMODIANS,2));
        Assert.False(p.AppliesTo(PlayerClass.PRIEST,Race.ASMODIANS,0));
    }

    [SkippableFact]
    public void NonPlayerRaceAndStatusRemainAnOrWithNoUnconditionalFallback()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_SPELL_CONDITION_CONTEXT");
        Skip.If(string.IsNullOrEmpty(path),"Requires native RaceDB projection");
        var context=RetailNpcStatSpellConditions.Parse(File.ReadAllBytes(path!),RetailNpcStatSpellConditions.ContextSha256);
        Assert.True(RetailNpcStatSpellBonus.TrySpellCondition("_Race_Type_C,Poison",context,out uint c,out int? r,out int s));
        Assert.Equal(0,r);Assert.Equal(1,s);
        var p=new RetailNpcStatSpellBonus.Program(0,4000,false,c,r,StatusMask:s);
        Assert.False(p.AppliesTo(PlayerClass.WARRIOR,Race.ASMODIANS,0));
        Assert.False(p.AppliesTo(PlayerClass.WARRIOR,Race.ELYOS,0));
        Assert.True(p.AppliesTo(PlayerClass.WARRIOR,Race.ASMODIANS,1));
    }

    [Theory]
    [InlineData("Curse")] [InlineData("Snare")] [InlineData("Stunlike")]
    [InlineData("_Back")] [InlineData("Bleed,Paralyze")] [InlineData("Poison\n")]
    public void UnprovedStatusesPoseAndTokenShapesRemainVisible(string text)
        =>Assert.False(RetailNpcStatSpellBonus.TrySpellCondition(text,null,out _,out _,out _));

    [Fact]
    public void ExistingPoisonOwnersDriveSpellBonusIndependentlyOfTheLegacyUnion()
    {
        using var h=BossAiHarness.For(220030000).Build();var player=h.SpawnPlayer();
        var first=new Effect(player,player,new SkillTemplate {skillId=910036},1);
        var second=new Effect(player,player,new SkillTemplate {skillId=910037},1);
        var effect=new PoisonEffect();var p=Program("Poison");int draws=0;
        int Damage()=>RetailNpcStatSpellBonus.Apply(50,4000,false,
            p.AppliesTo(player.GetPlayerClass(),player.GetRace(),player.GetEffectController().RetailNpcTargetConditionMask),
            1,2,(lo,hi)=>{draws++;return hi;}).Damage;
        Assert.Equal(50,Damage());Assert.Equal(0,draws);
        effect.StartEffect(first,AbnormalState.POISON);effect.StartEffect(second,AbnormalState.POISON);
        Assert.Equal(6050,Damage());Assert.Equal(2,draws);
        effect.EndEffect(first,AbnormalState.POISON);
        Assert.False(player.GetEffectController().IsAbnormalSet(AbnormalState.POISON));
        Assert.Equal(6050,Damage());Assert.Equal(4,draws);
        effect.EndEffect(second,AbnormalState.POISON);
        Assert.Equal(50,Damage());Assert.Equal(4,draws);Assert.Empty(h.Clock.Faults);
    }

    [Fact]
    public void NativeAerialClearRemovesTheConditionalBonusOnEveryEnd()
    {
        var native=new RetailNpcStatTargetConditions();var p=Program("OpenAerial");
        native.Set(AbnormalState.OPENAERIAL);native.Set(AbnormalState.OPENAERIAL);
        Assert.True(p.AppliesTo(PlayerClass.WARRIOR,Race.ASMODIANS,native.Mask));
        native.Clear(AbnormalState.OPENAERIAL);
        Assert.False(p.AppliesTo(PlayerClass.WARRIOR,Race.ASMODIANS,native.Mask));
    }

    [SkippableFact]
    public void EveryActualKnownStatusSourceKeepsItsIndependentZeroBonusControlAndHostShape()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        Skip.If(string.IsNullOrEmpty(path),"Requires pinned complete native source records");
        using var manifest=JsonDocument.Parse(File.ReadAllBytes(path!));
        string? hostPath=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_HOST_SKILLS");
        Skip.If(string.IsNullOrEmpty(hostPath),"Requires existing4.8 source-bound host templates");
        var hosts=XDocument.Load(hostPath!).Root!.Elements().ToDictionary(x=>(int)x.Attribute("skill_id")!);
        var rows=new List<object>();var expected=new HashSet<int>{20419,20424,21762,21763};
        foreach(var a in manifest.RootElement.GetProperty("skill_sources").EnumerateArray())
        {
            int id=a.GetProperty("id").GetInt32();if(!expected.Contains(id)&&id!=21853)continue;
            byte[] bytes=File.ReadAllBytes(a.GetProperty("path").GetString()!);
            var source=RetailNpcStatSkillCombat.ParseSource(bytes,a.GetProperty("sha256").GetString()!);
            var xml=XElement.Parse(Encoding.Unicode.GetString(bytes));
            Assert.Equal(expected.Contains(id),source.Supported);
            bool hostMatches=false;
            if(source.Supported)
            {
                xml.SetElementValue("effect1_reserved8",0);xml.SetElementValue("effect1_reserved9",0);
                var control=RetailNpcStatSpellBonusTests.Parse(xml);
                Assert.Equal(control,source with {Sha256=control.Sha256,SpellBonus=null});
                using var reader=hosts[id].CreateReader();
                var host=(SkillTemplate)new XmlSerializer(typeof(SkillTemplate),new XmlRootAttribute("skill_template")).Deserialize(reader)!;
                hostMatches=host.GetEffects()?.GetEffects().Count==1; // Verified native programs have no sibling.
                Assert.True(hostMatches);
                Assert.IsType<SpellAttackInstantEffect>(host.GetEffectTemplate(1));
                Assert.Equal(source.Element,host.GetEffectTemplate(1)!.GetElement());
                Assert.Equal(id==20419?65536:2,source.SpellBonus!.StatusMask);
                Assert.False(source.SpellBonus.AppliesTo(PlayerClass.WARRIOR,Race.ASMODIANS));
            }
            else Assert.Equal("Unreviewed spell status emission or sibling context",source.UnsupportedReason);
            rows.Add(new {id,supported=source.Supported,reason=source.UnsupportedReason,hostMatches,program=source.SpellBonus});
        }
        Assert.Equal(5,rows.Count);
        if(Environment.GetEnvironmentVariable("AION_RETAIL_STATS_SPELL_STATUS_OUTPUT") is {Length:>0} output)
        {using var stream=new FileStream(output,FileMode.CreateNew,FileAccess.Write);JsonSerializer.Serialize(stream,new {rows});}
    }
}
