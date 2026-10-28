using System.Text.Json;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcPeriodicHpReceiptTests
{
    private sealed class Owner : IStatOwner;
    // Death rewards require a database-backed PvP singleton, outside this
    // offline HP fixture. Keep the real HP commit while isolating that callback.
    private sealed class FixtureLifeStats(Player player) : Aion.GameServer.Model.Stats.Container.PlayerLifeStats(player)
    {
        internal bool HealAfterNextHit;
        protected override void OnHpChanged(Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus.TYPE type,
            int previousHp,int newHp,Creature effector)
        {
            if(newHp!=0)
            {
                base.OnHpChanged(type,previousHp,newHp,effector);
                if(HealAfterNextHit)
                {
                    HealAfterNextHit=false;
                    SetCurrentHp(newHp+7,effector);
                }
            }
        }
    }

    [SkippableFact]
    public void RealPeriodicHpOwnerReportsCommittedLossForOrdinaryAndLethalHits()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_PERIODIC_UNIT_CASES");
        Skip.If(string.IsNullOrWhiteSpace(path),"Requires source-bound periodic fixture");
        using var doc=JsonDocument.Parse(File.ReadAllBytes(path!));
        var root=doc.RootElement;
        string manifest=root.GetProperty("manifest").GetString()!,hash=root.GetProperty("sha256").GetString()!;
        using var installed=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(manifest),hash,Path.GetDirectoryName(manifest)!);
        var c=root.GetProperty("cases")[0];
        int map=c.GetProperty("map").GetInt32(),id=c.GetProperty("skill").GetInt32();
        using var h=BossAiHarness.For(map).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(c.GetProperty("npc").GetInt32(),"retail_stat_base_fixture");
        var target=RetailNpcStatPeriodicTests.Player(h,map);
        var life=new FixtureLifeStats(target);
        target.SetLifeStats(life);
        target.GetGameStats().AddEffectOnly(new Owner(),[new StatSetFunction(StatEnum.MAXHP,50000),new StatSetFunction(StatEnum.REGEN_HP,0)]);
        target.GetLifeStats().SetCurrentHp(1000);
        var template=DataManager.SKILL_DATA.GetSkillTemplate(id);
        var part=Assert.IsAssignableFrom<AbstractOverTimeEffect>(template.GetEffectTemplate(c.GetProperty("position").GetInt32()));
        var effect=new Effect(npc,target,template,c.GetProperty("level").GetInt32());
        effect.SetCalculatedMagicalCritical(part.Position,true);
        Assert.True(RetailNpcStatPeriodicCombat.TryResolveCritical(effect,part));
        effect.AddSuccessEffect(part);part.StartEffect(effect);

        int before=target.GetLifeStats().GetCurrentHp();
        life.HealAfterNextHit=true;
        Assert.True(RetailNpcStatPeriodicCombat.TryTick(effect,part,out int? ordinaryLoss));
        Assert.True(ordinaryLoss>7);
        Assert.Equal(before-target.GetLifeStats().GetCurrentHp()+7,ordinaryLoss);
        target.GetLifeStats().SetCurrentHp(5);
        Assert.True(RetailNpcStatPeriodicCombat.TryTick(effect,part,out int? lethalLoss));
        Assert.Equal(5,lethalLoss);
        Assert.Equal(0,target.GetLifeStats().GetCurrentHp());
        Assert.True(RetailNpcStatPeriodicCombat.TryTick(effect,part,out int? retiredLoss));
        Assert.Null(retiredLoss);
        effect.EndEffect();
        Assert.Empty(h.Clock.Faults);
    }
}
