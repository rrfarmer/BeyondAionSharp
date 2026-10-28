using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;
using Family=Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcPeriodicSpellParameters.Family;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcStatPeriodicSpellTests
{
    private sealed class Owner:IStatOwner { }
    [Fact]
    public void MissingBoostHeaderUsesNativeConstructorZeroWhileMdefGateIsIndependent()
    {
        byte[] bytes=Encoding.Unicode.GetBytes("<skill_base><id>910008</id><name>PeriodicSpellWitness</name>"
            +"<effect1_type>SpellATK</effect1_type><effect1_reserved8>1</effect1_reserved8><effect1_reserved9>100</effect1_reserved9>"
            +"<effect1_reserved10>Fire</effect1_reserved10><effect1_checktime>3000</effect1_checktime><effect1_reserved12>100</effect1_reserved12></skill_base>");
        var part=Assert.Single(RetailNpcStatPeriodicCombat.Parse(bytes,Convert.ToHexStringLower(SHA256.HashData(bytes))).Parts);
        Assert.True(part.Supported);Assert.False(part.ApplyBoost);Assert.True(part.ApplyMagicalDefense);
        Assert.Equal(Family.SpellAttack,part.Family);Assert.Equal(100,part.CriticalPercent);
    }

    [SkippableFact]
    public void CompleteSpellSourceCensusIncludesMdefAndMatchingJitterButKeepsExclusiveVisible()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        Skip.If(string.IsNullOrEmpty(path),"Requires selected full09 native sources");
        using var doc=JsonDocument.Parse(File.ReadAllBytes(path!));int accepted=0,rejected=0,mdef=0,jitter=0;
        foreach(var asset in doc.RootElement.GetProperty("skill_sources").EnumerateArray())
        {
            var source=RetailNpcStatPeriodicCombat.Parse(File.ReadAllBytes(asset.GetProperty("path").GetString()!),asset.GetProperty("sha256").GetString()!);
            foreach(var part in source.Parts.Where(p=>p.Family==Family.SpellAttack))
            {
                if(!part.Supported){rejected++;Assert.Equal(21605,source.Id);Assert.Equal("unreviewed-periodic-source-exclusive",part.UnsupportedReason);continue;}
                accepted++;if(part.ApplyMagicalDefense)mdef++;if(part.DurationJitter>0)jitter++;
            }
        }
        Assert.Equal((237,1,65,13),(accepted,rejected,mdef,jitter));
    }

    [SkippableFact]
    public void RealSpellsCacheBoostButReadDefenseElementAndPveAtEachExistingTick()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_PERIODIC_SPELL_UNIT_CASES");
        Skip.If(string.IsNullOrEmpty(path),"Requires external three-source SpellATK fixture");
        using var doc=JsonDocument.Parse(File.ReadAllBytes(path!));var root=doc.RootElement;
        string manifest=root.GetProperty("manifest").GetString()!,hash=root.GetProperty("sha256").GetString()!;
        using var installed=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(manifest),hash,Path.GetDirectoryName(manifest)!);
        foreach(var c in root.GetProperty("cases").EnumerateArray())
        {
            int map=c.GetProperty("map").GetInt32();using var h=BossAiHarness.For(map).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
            var n=h.SpawnWithAi(c.GetProperty("npc").GetInt32(),"retail_stat_base_fixture");var p=RetailNpcStatPeriodicTests.Player(h,map);
            var actor=new Owner();var target=new Owner();
            n.GetGameStats().AddEffectOnly(actor,[new StatSetFunction(StatEnum.BOOST_MAGICAL_SKILL,1500)]);
            p.GetGameStats().AddEffectOnly(target,[new StatSetFunction(StatEnum.MAXHP,50000),new StatSetFunction(StatEnum.REGEN_HP,0),
                new StatSetFunction(StatEnum.MAGIC_SKILL_BOOST_RESIST,500),new StatSetFunction(StatEnum.MAGICAL_DEFEND,0),
                new StatSetFunction(StatEnum.FIRE_RESISTANCE,0),new StatSetFunction(StatEnum.WATER_RESISTANCE,0),
                new StatSetFunction(StatEnum.WIND_RESISTANCE,0),new StatSetFunction(StatEnum.EARTH_RESISTANCE,0)]);
            p.GetLifeStats().SetCurrentHp(50000);
            Assert.Equal(1500,n.GetGameStats().GetMBoost().GetCurrent());Assert.Equal(500,p.GetGameStats().GetMBResist().GetCurrent());
            var skill=DataManager.SKILL_DATA.GetSkillTemplate(c.GetProperty("skill").GetInt32());
            var part=Assert.IsType<SpellAttackEffect>(skill.GetEffectTemplate(c.GetProperty("position").GetInt32()));
            int level=c.GetProperty("level").GetInt32(),basis=c.GetProperty("base").GetInt32();
            int pve=c.GetProperty("expectedPveDefense").GetInt32();
            bool boosted=c.GetProperty("applyBoost").GetBoolean(),defended=c.GetProperty("applyMdef").GetBoolean();
            int cached=boosted?2*basis:basis;
            var effect=new Effect(n,p,skill,level);effect.SetCalculatedMagicalCritical(part.Position,true);
            Assert.True(RetailNpcStatPeriodicCombat.TryResolveCritical(effect,part));effect.AddSuccessEffect(part);part.StartEffect(effect);
            Assert.Equal(cached,effect.GetReserveds(part.Position).GetValue());Assert.Equal((0,pve),SkyTempleCombatInputs.Pve(n,p,false));
            int hp=p.GetLifeStats().GetCurrentHp();h.Clock.Advance(TimeSpan.FromMilliseconds(part.checktime));
            Assert.Empty(h.Clock.Faults);Assert.Equal(hp-(int)(cached*(1f-pve/1000f)),p.GetLifeStats().GetCurrentHp());
            // These later boost operands must not rebuild the cached Check value.
            n.GetGameStats().AddEffectOnly(new Owner(),[new StatSetFunction(StatEnum.BOOST_MAGICAL_SKILL,9999)]);
            p.GetGameStats().AddEffectOnly(new Owner(),[new StatSetFunction(StatEnum.MAGIC_SKILL_BOOST_RESIST,9999),
                new StatSetFunction(StatEnum.MAGICAL_DEFEND,1500),new StatSetFunction(StatEnum.FIRE_RESISTANCE,650),
                new StatSetFunction(StatEnum.WATER_RESISTANCE,650),new StatSetFunction(StatEnum.WIND_RESISTANCE,650),
                new StatSetFunction(StatEnum.EARTH_RESISTANCE,650),new StatSetFunction(StatEnum.MAGICAL_CRITICAL_DAMAGE_REDUCE,1000),
                new StatSetFunction(StatEnum.PVE_DEFEND_RATIO_MAGICAL,450)]);
            Assert.Equal(1500,p.GetGameStats().GetMDef().GetCurrent());Assert.Equal((0,pve+450),SkyTempleCombatInputs.Pve(n,p,false));
            int expected=(int)((int)((cached-(defended?150:0))*.5f)*(1f-(pve+450)/1000f));
            hp=p.GetLifeStats().GetCurrentHp();h.Clock.Advance(TimeSpan.FromMilliseconds(part.checktime));
            Assert.Empty(h.Clock.Faults);Assert.Equal(hp-expected,p.GetLifeStats().GetCurrentHp());
            effect.EndEffect();hp=p.GetLifeStats().GetCurrentHp();h.Clock.Advance(TimeSpan.FromMilliseconds(2*part.checktime));
            Assert.Equal(hp,p.GetLifeStats().GetCurrentHp());Assert.Empty(h.Clock.Faults);
        }
    }
}
