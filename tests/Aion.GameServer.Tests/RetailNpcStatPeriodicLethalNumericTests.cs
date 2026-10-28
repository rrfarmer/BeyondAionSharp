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

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcStatPeriodicLethalNumericTests
{
    private sealed class Owner:IStatOwner { }

    [SkippableFact]
    public void LethalSimCasesMatchIndependentPeriodicNumbersInIsolatedTimerHpFixtures()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_PERIODIC_LETHAL_UNIT_CASES");
        Skip.If(string.IsNullOrEmpty(path),"Requires independent source-bound lethal-case numbers");
        using var doc=JsonDocument.Parse(File.ReadAllBytes(path!));var root=doc.RootElement;
        string manifest=root.GetProperty("manifest").GetString()!,hash=root.GetProperty("sha256").GetString()!;
        using var installed=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(manifest),hash,Path.GetDirectoryName(manifest)!);
        Assert.NotEmpty(root.GetProperty("cases").EnumerateArray());
        foreach(var c in root.GetProperty("cases").EnumerateArray())
        {
            int map=c.GetProperty("map").GetInt32();
            using var h=BossAiHarness.For(map).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
            var n=h.SpawnWithAi(c.GetProperty("npc").GetInt32(),"retail_stat_base_fixture");
            var p=RetailNpcStatPeriodicTests.Player(h,map);
            Assert.True(RetailNpcStatCatalog.TryGet(n,out var profile));
            Assert.Equal(c.GetProperty("sourceProfileHash").GetString(),profile.SourceRecordSha256);
            int skillId=c.GetProperty("skill").GetInt32(),level=c.GetProperty("level").GetInt32();
            Assert.True(RetailNpcStatCatalog.TryGetPeriodic(profile,skillId,level,out var source));
            Assert.Equal(c.GetProperty("sourceSkillHash").GetString(),source.Sha256);
            var skill=DataManager.SKILL_DATA.GetSkillTemplate(skillId);
            var part=Assert.IsAssignableFrom<AbstractOverTimeEffect>(skill.GetEffectTemplate(c.GetProperty("position").GetInt32()));
            n.GetGameStats().AddEffectOnly(new Owner(),[new StatSetFunction(StatEnum.BOOST_MAGICAL_SKILL,1500)]);
            p.GetGameStats().AddEffectOnly(new Owner(),[new StatSetFunction(StatEnum.MAXHP,50000),
                new StatSetFunction(StatEnum.REGEN_HP,0),new StatSetFunction(StatEnum.MAGIC_SKILL_BOOST_RESIST,500),
                new StatSetFunction(StatEnum.MAGICAL_DEFEND,1500),new StatSetFunction(StatEnum.FIRE_RESISTANCE,650),
                new StatSetFunction(StatEnum.WATER_RESISTANCE,650),new StatSetFunction(StatEnum.WIND_RESISTANCE,650),
                new StatSetFunction(StatEnum.EARTH_RESISTANCE,650),new StatSetFunction(StatEnum.MAGICAL_CRITICAL_DAMAGE_REDUCE,0)]);
            p.GetLifeStats().SetCurrentHp(50000);
            Assert.Equal(1500,n.GetGameStats().GetMBoost().GetCurrent());
            Assert.Equal(500,p.GetGameStats().GetMBResist().GetCurrent());
            int pve=c.GetProperty("expectedPveDefense").GetInt32();
            Assert.Equal((0,pve),SkyTempleCombatInputs.Pve(n,p,false));
            var effect=new Effect(n,p,skill,level);effect.SetCalculatedMagicalCritical(part.Position,true);
            Assert.True(RetailNpcStatPeriodicCombat.TryResolveCritical(effect,part));
            effect.AddSuccessEffect(part);part.StartEffect(effect);
            Assert.Equal(c.GetProperty("cachedBase").GetInt32(),effect.GetReserveds(part.Position).GetValue());
            // These are isolated numeric fixtures, not ordinary-player SIMs.
            // The primary is deliberately absent and unit-only HP permits a tick.
            // Expected numbers come from raw sources and an external f32 replay.
            for(int tick=0;tick<2;tick++)
            {
                if(tick==1)
                {
                    n.GetGameStats().AddEffectOnly(new Owner(),[new StatSetFunction(StatEnum.BOOST_MAGICAL_SKILL,9999)]);
                    p.GetGameStats().AddEffectOnly(new Owner(),[new StatSetFunction(StatEnum.MAGIC_SKILL_BOOST_RESIST,9999),
                        new StatSetFunction(StatEnum.MAGICAL_DEFEND,2500),new StatSetFunction(StatEnum.FIRE_RESISTANCE,850),
                        new StatSetFunction(StatEnum.WATER_RESISTANCE,850),new StatSetFunction(StatEnum.WIND_RESISTANCE,850),
                        new StatSetFunction(StatEnum.EARTH_RESISTANCE,850),new StatSetFunction(StatEnum.PVE_DEFEND_RATIO_MAGICAL,450),
                        new StatSetFunction(StatEnum.MAGICAL_CRITICAL_DAMAGE_REDUCE,1000)]);
                    Assert.Equal((0,pve+450),SkyTempleCombatInputs.Pve(n,p,false));
                }
                int before=p.GetLifeStats().GetCurrentHp();
                h.Clock.Advance(TimeSpan.FromMilliseconds(part.checktime));
                Assert.Empty(h.Clock.Faults);
                Assert.Equal(c.GetProperty("expectedDamage")[tick].GetInt32(),before-p.GetLifeStats().GetCurrentHp());
            }
            effect.EndEffect();int ended=p.GetLifeStats().GetCurrentHp();
            h.Clock.Advance(TimeSpan.FromMilliseconds(2*part.checktime));
            Assert.Equal(ended,p.GetLifeStats().GetCurrentHp());Assert.Empty(h.Clock.Faults);
        }
    }
}
