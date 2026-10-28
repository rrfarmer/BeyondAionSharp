using System.Text.Json;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpc17860PrimaryProjectionTests
{
    private sealed class TimedSkill(SkillTemplate template,Creature caster,Creature target):Skill(template,caster,1,target,null!)
    { internal void SetWord(int word){SetClientHitTime(word);UpdateHitTime(false);} }

    [SkippableFact]
    public void PreparedKasikaInitializesAndAppliesDamageDefenseAndHealAsOneEffect()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrWhiteSpace(path)||string.IsNullOrWhiteSpace(hash),"Requires pinned retail package");
        byte[] manifestBytes=File.ReadAllBytes(path!);using var manifest=JsonDocument.Parse(manifestBytes);
        var asset=manifest.RootElement.GetProperty("skill_sources").EnumerateArray().Single(x=>x.GetProperty("id").GetInt32()==17860);
        byte[] source=File.ReadAllBytes(asset.GetProperty("path").GetString()!);
        var candidate=RetailNpc17860PrimaryProjection.ReadForOfflineFixture(source);
        using var active=RetailNpcStatCatalog.InstallForTests(manifestBytes,hash!,Path.GetDirectoryName(path!)!);
        using var h=BossAiHarness.For(220020000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(212874,"retail_stat_base_fixture");
        var player=RetailNpcStatPeriodicTests.Player(h,220020000);player.GetCommonData().SetLevel(40);
        player.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAXHP,100000),
            new StatSetFunction(StatEnum.MAGICAL_RESIST,0),new StatSetFunction(StatEnum.MAGICAL_DEFEND,0),
            new StatSetFunction(StatEnum.EARTH_RESISTANCE,0)]);
        player.GetLifeStats().OnStatsChange(null!);player.GetLifeStats().SetCurrentHp(80000);npc.GetLifeStats().SetCurrentHp(100);
        var template=DataManager.SKILL_DATA.GetSkillTemplate(17860);var skill=new TimedSkill(template,npc,player);skill.SetWord(0);
        var effect=new Effect(skill,player);
        RetailNpc17860PrimaryProjection.AttachInitializationForOfflineFixture(effect,candidate,(_,high)=>high);
        effect.Initialize();
        Assert.True(RetailNpc17860PrimaryProjection.TryGetInitializationForOfflineFixture(effect,out bool handled,out bool success));
        Assert.True(handled);Assert.True(success);Assert.True(effect.IsInSuccessEffects(1));Assert.True(effect.IsInSuccessEffects(2));
        int damage=effect.GetReserveds(1).GetValue();Assert.True(damage>0);
        var element=new RetailNpc17860DefenseEffect.ElementInput(500,0,0,-10000,10000);
        var inputs=new RetailNpc17860DefenseEffect.Inputs(2000,1000,0,0,element,element,element,element,element,element);
        RetailNpc17860DefenseEffect.AttachForOfflineFixture(effect,source,inputs,p=>ReferenceEquals(p,player));
        int hpBefore=player.GetLifeStats().GetCurrentHp();int defenseBefore=player.GetGameStats().GetStat(StatEnum.PHYSICAL_DEFENSE,1000).GetCurrent();
        effect.ApplyEffect();
        Assert.Equal(hpBefore-damage,player.GetLifeStats().GetCurrentHp());
        Assert.Equal(defenseBefore-100,player.GetGameStats().GetStat(StatEnum.PHYSICAL_DEFENSE,1000).GetCurrent());
        Assert.Same(effect,player.GetEffectController().GetAbnormalEffect(effect.GetStack()));
        effect.ApplyEffect();Assert.Equal(hpBefore-damage,player.GetLifeStats().GetCurrentHp());
        h.Clock.Advance(TimeSpan.FromMilliseconds(999));Assert.Equal(100,npc.GetLifeStats().GetCurrentHp());
        h.Clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(Math.Min(npc.GetLifeStats().GetMaxHp(),100+RetailNpcStatSpellDrain.HealAmount(damage,200)),npc.GetLifeStats().GetCurrentHp());
        effect.EndEffect(false);Assert.Equal(defenseBefore,player.GetGameStats().GetStat(StatEnum.PHYSICAL_DEFENSE,1000).GetCurrent());
        var wrongLevel=new Effect(new Skill(template,npc,2,player,null!),player);
        RetailNpc17860PrimaryProjection.AttachInitializationForOfflineFixture(wrongLevel,candidate,(_,high)=>high);
        wrongLevel.Initialize();
        Assert.True(RetailNpc17860PrimaryProjection.TryGetInitializationForOfflineFixture(wrongLevel,out handled,out success));
        Assert.False(handled);Assert.False(success);Assert.Empty(wrongLevel.GetSuccessEffects());
        int casterHp=npc.GetLifeStats().GetCurrentHp(),targetHp=player.GetLifeStats().GetCurrentHp();
        wrongLevel.ApplyEffect();
        Assert.Equal(casterHp,npc.GetLifeStats().GetCurrentHp());Assert.Equal(targetHp,player.GetLifeStats().GetCurrentHp());
        var primary=Assert.IsType<SpellAtkDrainInstantEffect>(template.GetEffectTemplate(1));
        Assert.False(RetailNpcStatSpellDrain.TryApply(wrongLevel,primary,()=>throw new InvalidOperationException()));
        var resistant=RetailNpcStatPeriodicTests.Player(h,220020000);resistant.GetCommonData().SetLevel(40);
        resistant.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_RESIST,9999)]);
        npc.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_ACCURACY,0)]);
        var resisted=new Effect(new Skill(template,npc,1,resistant,null!),resistant);
        RetailNpc17860PrimaryProjection.AttachInitializationForOfflineFixture(resisted,candidate,(low,_)=>low);
        resisted.Initialize();
        Assert.True(RetailNpc17860PrimaryProjection.TryGetInitializationForOfflineFixture(resisted,out handled,out success));
        Assert.True(handled);Assert.False(success);Assert.False(resisted.IsInSuccessEffects(1));Assert.False(resisted.IsInSuccessEffects(2));
        int resistantHp=resistant.GetLifeStats().GetCurrentHp();
        resisted.ApplyEffect();
        Assert.Equal(casterHp,npc.GetLifeStats().GetCurrentHp());Assert.Equal(resistantHp,resistant.GetLifeStats().GetCurrentHp());
        Assert.False(RetailNpcStatSpellDrain.TryApply(resisted,primary,()=>throw new InvalidOperationException()));
        Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void KasikaPrimaryDealsDamageAndHealsInOfflineActorFixture()
    {
        string? manifestPath=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(manifestPath)||string.IsNullOrEmpty(hash),"Requires pinned retail source package");
        byte[] bytes=File.ReadAllBytes(manifestPath!);
        using var manifest=JsonDocument.Parse(bytes);
        var asset=manifest.RootElement.GetProperty("skill_sources").EnumerateArray().Single(x=>x.GetProperty("id").GetInt32()==17860);
        byte[] retailSource=File.ReadAllBytes(asset.GetProperty("path").GetString()!);
        var candidate=RetailNpc17860PrimaryProjection.ReadForOfflineFixture(retailSource);
        using var installed=RetailNpcStatCatalog.InstallForTests(bytes,hash!,Path.GetDirectoryName(manifestPath!)!);
        using var h=BossAiHarness.For(220020000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(212874,"retail_stat_base_fixture");
        var player=RetailNpcStatPeriodicTests.Player(h,220020000);player.GetCommonData().SetLevel(40);
        player.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAXHP,100000),
            new StatSetFunction(StatEnum.MAGICAL_RESIST,0),new StatSetFunction(StatEnum.MAGICAL_DEFEND,0),
            new StatSetFunction(StatEnum.EARTH_RESISTANCE,0)]);
        player.GetLifeStats().OnStatsChange(null!);player.GetLifeStats().SetCurrentHp(80000);
        npc.GetLifeStats().SetCurrentHp(100);
        var template=DataManager.SKILL_DATA.GetSkillTemplate(17860);
        var first=Assert.IsType<SpellAtkDrainInstantEffect>(template.GetEffectTemplate(1));
        var second=Assert.IsType<StatdownEffect>(template.GetEffectTemplate(2));
        Assert.True(second.IsNoResist());
        var skill=new TimedSkill(template,npc,player);skill.SetWord(0);
        int Draw(int lo,int hi)=>Math.Max(lo,hi);
        var missingPrimary=new Effect(skill,player);
        Assert.False(second.Calculate(missingPrimary,null,null));
        Assert.False(missingPrimary.IsInSuccessEffects(2));
        var plain=new Effect(skill,player);
        Assert.True(RetailNpcStatSkillCombat.TryPrepare17860PrimaryForOfflineFixture(plain,candidate,Draw,out bool hit));
        Assert.True(hit);int plainDamage=plain.GetReserveds(1).GetValue();Assert.True(plainDamage>0);
        Assert.True(second.Calculate(plain,null,null));
        Assert.True(plain.IsInSuccessEffects(2));
        var paralyze=new ParalyzeEffect();var status=new Effect(player,player,new SkillTemplate {skillId=910018},1);
        paralyze.StartEffect(status);
        var paralyzed=new Effect(skill,player);
        Assert.True(RetailNpcStatSkillCombat.TryPrepare17860PrimaryForOfflineFixture(paralyzed,candidate,Draw,out hit));
        Assert.True(hit);int damage=paralyzed.GetReserveds(1).GetValue();Assert.True(damage>plainDamage);
        Assert.True(second.Calculate(paralyzed,null,null));
        // Damage is rounded at multiple stages; scaling an already rounded
        // ordinary hit can differ by a point from the real pipeline.
        int scaled=RetailNpcStatSpellBonus.Apply(plainDamage,250,true,true,0,2,Draw).Damage;
        Assert.InRange(damage,scaled-1,scaled+1);
        int before=player.GetLifeStats().GetCurrentHp();
        first.ApplyEffect(paralyzed);
        Assert.Equal(before-damage,player.GetLifeStats().GetCurrentHp());
        first.ApplyEffect(paralyzed);Assert.Equal(before-damage,player.GetLifeStats().GetCurrentHp());
        StatEnum[] channels=[StatEnum.PHYSICAL_DEFENSE,StatEnum.WATER_RESISTANCE,StatEnum.WIND_RESISTANCE,
            StatEnum.EARTH_RESISTANCE,StatEnum.FIRE_RESISTANCE,StatEnum.LIGHT_RESISTANCE,StatEnum.DARK_RESISTANCE];
        var stats=player.GetGameStats();
        int[] beforeDefense=channels.Select(s=>stats.GetStat(s,1000).GetCurrent()).ToArray();
        StatEnum[] hostAliases=[StatEnum.ELEMENTAL_RESISTANCE_LIGHT,StatEnum.ELEMENTAL_RESISTANCE_DARK];
        int[] beforeAliases=hostAliases.Select(s=>stats.GetStat(s,1000).GetCurrent()).ToArray();
        int beforeLight=stats.GetElementalDefenseFor(SkillElement.LIGHT),beforeDark=stats.GetElementalDefenseFor(SkillElement.DARK);
        int beforeMagic=stats.GetStat(StatEnum.MAGICAL_RESIST,1000).GetCurrent();
        var element=new RetailNpc17860DefenseEffect.ElementInput(500,0,0,-10000,10000);
        var inputs=new RetailNpc17860DefenseEffect.Inputs(2000,1000,0,0,element,element,element,element,element,element);
        var alteredSource=(byte[])retailSource.Clone();alteredSource[^1]^=1;
        Assert.Throws<InvalidDataException>(()=>RetailNpc17860DefenseEffect.AttachForOfflineFixture(paralyzed,alteredSource,inputs,_=>true));
        bool current=true;
        RetailNpc17860DefenseEffect.AttachDynamicForOfflineFixture(paralyzed,retailSource,p=>ReferenceEquals(p,player)?inputs:null,
            p=>current&&ReferenceEquals(p,player));
        current=false;
        Assert.Throws<InvalidOperationException>(()=>second.StartEffect(paralyzed));
        for(int i=0;i<channels.Length;i++)Assert.Equal(beforeDefense[i],stats.GetStat(channels[i],1000).GetCurrent());
        current=true;
        second.ApplyEffect(paralyzed);
        Assert.Same(paralyzed,player.GetEffectController().GetAbnormalEffect(paralyzed.GetStack()));
        for(int i=0;i<channels.Length;i++)
        {
            Assert.Equal(beforeDefense[i]-(i==0?100:50),stats.GetStat(channels[i],1000).GetCurrent());
            Assert.Single(stats.GetStatsSorted(channels[i]),f=>ReferenceEquals(f.GetOwner(),paralyzed));
        }
        Assert.Equal(beforeMagic,stats.GetStat(StatEnum.MAGICAL_RESIST,1000).GetCurrent());
        for(int i=0;i<hostAliases.Length;i++)
        {
            Assert.Equal(beforeAliases[i]-50,stats.GetStat(hostAliases[i],1000).GetCurrent());
            Assert.Single(stats.GetStatsSorted(hostAliases[i]),f=>ReferenceEquals(f.GetOwner(),paralyzed));
        }
        Assert.Equal(beforeLight-50,stats.GetElementalDefenseFor(SkillElement.LIGHT));
        Assert.Equal(beforeDark-50,stats.GetElementalDefenseFor(SkillElement.DARK));
        Assert.DoesNotContain(stats.GetStatsSorted(StatEnum.MAGICAL_RESIST),f=>ReferenceEquals(f.GetOwner(),paralyzed));
        Assert.Throws<InvalidOperationException>(()=>second.StartEffect(paralyzed));
        var changedElement=element with {Initial=700};
        inputs=inputs with {PhysicalCurrent=2500,ArmorBasis=1500,Water=changedElement,Wind=changedElement,
            Earth=changedElement,Fire=changedElement,Light=changedElement,Dark=changedElement};
        for(int i=0;i<channels.Length;i++)
            Assert.Equal(beforeDefense[i]-(i==0?150:70),stats.GetStat(channels[i],1000).GetCurrent());
        for(int i=0;i<hostAliases.Length;i++)Assert.Equal(beforeAliases[i]-70,stats.GetStat(hostAliases[i],1000).GetCurrent());
        current=false;
        Assert.Throws<InvalidOperationException>(()=>stats.GetStat(StatEnum.PHYSICAL_DEFENSE,1000));
        current=true;
        h.Clock.Advance(TimeSpan.FromMilliseconds(999));Assert.Equal(100,npc.GetLifeStats().GetCurrentHp());
        h.Clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(Math.Min(npc.GetLifeStats().GetMaxHp(),100+RetailNpcStatSpellDrain.HealAmount(damage,200)),npc.GetLifeStats().GetCurrentHp());
        paralyzed.EndEffect(false);
        Assert.Null(player.GetEffectController().GetAbnormalEffect(paralyzed.GetStack()));
        current=false; // Removed functions must no longer consult the old input provider.
        for(int i=0;i<channels.Length;i++)
        {
            Assert.Equal(beforeDefense[i],stats.GetStat(channels[i],1000).GetCurrent());
            Assert.DoesNotContain(stats.GetStatsSorted(channels[i]),f=>ReferenceEquals(f.GetOwner(),paralyzed));
        }
        for(int i=0;i<hostAliases.Length;i++)
        {
            Assert.Equal(beforeAliases[i],stats.GetStat(hostAliases[i],1000).GetCurrent());
            Assert.DoesNotContain(stats.GetStatsSorted(hostAliases[i]),f=>ReferenceEquals(f.GetOwner(),paralyzed));
        }
        Assert.Equal(beforeMagic,stats.GetStat(StatEnum.MAGICAL_RESIST,1000).GetCurrent());
        paralyze.EndEffect(status);Assert.Empty(h.Clock.Faults);
        // Inputs and registration are explicitly supplied; this does not enable the live skill.
    }

    [SkippableFact]
    public void RealRetailSourceBindsConditionalDamageAndProtectedHitHealing()
    {
        string? manifest=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        Skip.If(string.IsNullOrWhiteSpace(manifest),"Requires selected 5.8 17860 source");
        using var doc=JsonDocument.Parse(File.ReadAllBytes(manifest!));
        var asset=doc.RootElement.GetProperty("skill_sources").EnumerateArray().Single(x=>x.GetProperty("id").GetInt32()==17860);
        byte[] raw=File.ReadAllBytes(asset.GetProperty("path").GetString()!);
        var complete=RetailNpcStatSkillCombat.ParseSource(raw,"52953fccf5f044eb35e7ab1fdebb06af5ff5948ccd67dacecf6aacb855367ee2");
        Assert.False(complete.Supported); // The defense sibling remains a gameplay gate.
        var selected=RetailNpc17860PrimaryProjection.ReadForOfflineFixture(raw);
        Assert.False(selected.GameplayReady);Assert.Equal(50,selected.Primary.Percent);
        Assert.Equal(200,selected.Primary.Drain!.HpPercent);
        int rolls=0;int Draw(int low,int high){rolls++;Assert.Equal((1,100),(low,high));return 100;}
        var ordinary=selected.ApplyConditionalBonus(1000,65,PlayerClass.WARRIOR,Race.ASMODIANS,0,0,2,Draw);
        Assert.Equal(1000,ordinary.Damage);Assert.Null(ordinary.ChanceDraw);Assert.Equal(0,rolls);
        var paralyzed=selected.ApplyConditionalBonus(1000,65,PlayerClass.WARRIOR,Race.ASMODIANS,
            RetailNpcStatTargetConditions.ParalyzeMask,0,2,Draw);
        Assert.Equal(3500,paralyzed.Damage);Assert.Equal(2500,paralyzed.AddedDamage);Assert.Equal(1,rolls);
        Assert.Equal(2400,selected.HealFromProtectedHit(1200)); // Supplied protected hit, not observed HP loss.
        Assert.Equal(0,selected.HealFromProtectedHit(0));
        var altered=(byte[])raw.Clone();altered[^1]^=1;
        Assert.Throws<InvalidDataException>(()=>RetailNpc17860PrimaryProjection.ReadForOfflineFixture(altered));
    }
}
