using System.Xml.Serialization;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcPlayerCurseAbsoluteCaptureTests
{
    private sealed class TimedSkill(SkillTemplate template,Creature caster,Creature target,int level):Skill(template,caster,level,target,null!)
    { internal void SetWord(int word){SetClientHitTime(word);UpdateHitTime(false);} }

    [SkippableFact]
    public void ActiveAbsoluteDebuffWithoutCurseSuppressesBuffOverrideInPreparedVampire()
    {
        string? directory=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_ABSOLUTE_CATALOG");
        string? host=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_HOST_ABSOLUTE");
        string? manifest=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? manifestHash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrWhiteSpace(directory)||string.IsNullOrWhiteSpace(host)
            ||string.IsNullOrWhiteSpace(manifest)||string.IsNullOrWhiteSpace(manifestHash),"Requires pinned absolute and NPC sources");
        byte[] catalogBytes=File.ReadAllBytes(Path.Combine(directory!,"catalog.json"));
        const string hash="a4837b798a026e14b573974d7a644c4ddf719b7a64f69ed52f7cf265ecef3c95";
        var catalog=RetailNpcPlayerCurseAbsoluteCapture.Catalog.LoadForOfflineFixture(catalogBytes,hash,directory!);
        var changed=(byte[])catalogBytes.Clone();changed[0]^=1;
        Assert.Throws<InvalidDataException>(()=>RetailNpcPlayerCurseAbsoluteCapture.Catalog.LoadForOfflineFixture(changed,hash,directory!));
        using var package=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(manifest!),manifestHash!,Path.GetDirectoryName(manifest!)!);
        using var h=BossAiHarness.For(220050000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        using var xml=File.OpenRead(host!);
        var data=(AbsoluteStatsData)new XmlSerializer(typeof(AbsoluteStatsData)).Deserialize(xml)!;data.AfterUnmarshal(null!);
        typeof(StaticData).GetProperty(nameof(StaticData.AbsoluteStatsDataDh))!.SetValue(h.Data.StaticData,data);
        var player=RetailNpcStatPeriodicTests.Player(h,220050000);
        var npc=h.SpawnWithAi(214435,"retail_stat_base_fixture");player.GetCommonData().SetLevel(38);
        npc.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_ACCURACY,10000),
            new StatSetFunction(StatEnum.CURSE_RESISTANCE_PENETRATION,0)]);
        player.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_RESIST,0)]);
        Assert.True(RetailNpcStatCatalog.TryGet(npc,out var profile));
        Assert.True(RetailNpcStatCatalog.TryGetSkill(profile,16596,38,out var source));
        var vampireTemplate=DataManager.SKILL_DATA.GetSkillTemplate(16596);
        var first=Assert.IsType<SpellAtkDrainInstantEffect>(vampireTemplate.GetEffectTemplate(1));
        var curse=Assert.IsType<CurseEffect>(vampireTemplate.GetEffectTemplate(2));
        var skill=new TimedSkill(vampireTemplate,npc,player,38);skill.SetWord(0);
        Effect Prepared()
        {
            var effect=new Effect(skill,player);
            effect.SetReserveds(new EffectReserved(1,20,EffectReserved.ResourceType.HP,true),false);
            RetailNpcStatSpellDrain.Prepare(effect,first,new(100,false,Curse:new(source.Sha256,FixtureResistance:target=>
            {
                if(!RetailNpcPlayerCurseAbsoluteCapture.TryCaptureForOfflineFixture(target,catalog,out var buff,out var debuff))
                    throw new InvalidOperationException("Absolute-status witness retired");
                return new(5,default,new(100,100),buff,debuff);
            })));
            effect.AddSuccessEffect(first);return effect;
        }
        Effect Start(int id)
        {
            var template=DataManager.SKILL_DATA.GetSkillTemplate(id);
            var absolute=Assert.IsAssignableFrom<AbstractAbsoluteStatEffect>(template.GetEffectTemplate(2));
            var effect=new Effect(player,player,template,1);effect.AddSuccessEffect(absolute);effect.ApplyEffect();return effect;
        }
        var buff=Start(21055);
        Assert.True(RetailNpcPlayerCurseAbsoluteCapture.TryCaptureForOfflineFixture(player,catalog,out var buffValue,out var debuffValue));
        Assert.Equal(new RetailNpcStatSpellDrain.CurseAbsolute(30,true,9999),buffValue);Assert.Equal(default,debuffValue);
        var resisted=Prepared();var rolls=new Queue<int>([100,1000,1000]);
        Assert.True(RetailNpcStatSpellDrain.TryCalculateCurse(resisted,curse,(_,_)=>rolls.Dequeue()));
        Assert.False(resisted.IsInSuccessEffects(2));Assert.Empty(rolls);
        var debuff=Start(21218);
        Assert.True(RetailNpcPlayerCurseAbsoluteCapture.TryCaptureForOfflineFixture(player,catalog,out buffValue,out debuffValue));
        Assert.Equal(new RetailNpcStatSpellDrain.CurseAbsolute(30,true,9999),buffValue);
        Assert.Equal(new RetailNpcStatSpellDrain.CurseAbsolute(28,false,0),debuffValue);
        var suppressed=Prepared();rolls=new Queue<int>([100,1000,110]);
        Assert.True(RetailNpcStatSpellDrain.TryCalculateCurse(suppressed,curse,(_,_)=>rolls.Dequeue()));
        Assert.False(suppressed.IsInSuccessEffects(2));Assert.Empty(rolls);
        var admitted=Prepared();rolls=new Queue<int>([100,1000,111]);
        Assert.True(RetailNpcStatSpellDrain.TryCalculateCurse(admitted,curse,(_,_)=>rolls.Dequeue()));
        Assert.True(admitted.IsInSuccessEffects(2));Assert.Empty(rolls);
        debuff.EndEffect();
        Assert.True(RetailNpcPlayerCurseAbsoluteCapture.TryCaptureForOfflineFixture(player,catalog,out buffValue,out debuffValue));
        Assert.Equal(new RetailNpcStatSpellDrain.CurseAbsolute(30,true,9999),buffValue);Assert.Equal(default,debuffValue);
        buff.EndEffect();
        Assert.True(RetailNpcPlayerCurseAbsoluteCapture.TryCaptureForOfflineFixture(player,catalog,out buffValue,out debuffValue));
        Assert.Equal(default,buffValue);Assert.Equal(default,debuffValue);
        player.GetGameStats().AddEffectOnly(null!,[new StatAbsFunction(StatEnum.CURSE_RESISTANCE,77,false)]);
        Assert.False(RetailNpcPlayerCurseAbsoluteCapture.TryCaptureForOfflineFixture(player,catalog,out _,out _));
        Assert.Empty(h.Clock.Faults);
    }
}
