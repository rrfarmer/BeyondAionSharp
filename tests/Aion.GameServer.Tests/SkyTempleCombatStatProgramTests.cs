using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.SkillEngine.Condition;
using Aion.GameServer.Tests.Ai;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class SkyTempleCombatStatProgramTests
{
    private sealed class Owner : IStatOwner { }
    private sealed class OwnerCondition(IStatOwner expected) : Condition
    {
        public override bool Validate(Aion.GameServer.SkillEngine.Model.Skill skill)=>true;
        public override bool Validate(Stat2 stat,IStatFunction function)=>ReferenceEquals(expected,function.GetOwner());
    }
    // A real registered host stat program exercises filtering before a final rate and
    // truncation. Subtracting the map value from the computed integer cannot pass.
    private sealed class FinalRate(StatEnum name) : StatFunction(name,25,false)
    {
        public override int GetPriority()=>120;
        public override void Apply(Stat2 stat,ISet<CalculationType> types)=>stat.SetFinalRate(1.25f);
    }
    [Fact]
    public void SelectedPveOmitsOnlyPredefinedMapProgramsBeforeOwnedFunctionsAndRounding()
    {
        using var h=BossAiHarness.For(320050000).Build();
        var player=h.SpawnPlayer();var stats=player.GetGameStats();
        // Production entry-world registers these; the minimal boss harness does not.
        PlayerStatFunctions.AddPredefinedStatFunctions(player);
        var map=DataManager.WORLD_MAPS_DATA.GetTemplate(320050000);
        // Harness maps are minimal; bind the actual4.8 SkyTemple source metadata.
        map.PveAttackRatio=150;map.PveDefendRatio=50;
        Assert.Equal(150,map.GetPvEAttackRatio());Assert.Equal(50,map.GetPvEDefendRatio());
        Assert.Equal(150,stats.GetStat(StatEnum.PVE_ATTACK_RATIO,0).GetCurrent());
        Assert.Equal(50,stats.GetStat(StatEnum.PVE_DEFEND_RATIO,0).GetCurrent());
        Assert.Equal(0,SkyTempleCombatInputs.PveStat(player,StatEnum.PVE_ATTACK_RATIO));
        Assert.Equal(0,SkyTempleCombatInputs.PveStat(player,StatEnum.PVE_DEFEND_RATIO));
        var equipment=new Owner();var effect=new Owner();
        foreach(var name in new[]{StatEnum.PVE_ATTACK_RATIO,StatEnum.PVE_DEFEND_RATIO})
        {
            var add=new StatAddFunction(name,7,true).WithConditions(new Conditions{conditions=[new OwnerCondition(equipment)]});
            stats.AddEffectOnly(equipment,[add]);
            stats.AddEffectOnly(effect,[new StatAddFunction(name,3,true),new FinalRate(name)]);
            Assert.Contains(stats.GetStatsSorted(name),f=>f is StatFunctionProxy proxy&&
                ReferenceEquals(proxy.GetOwner(),equipment)&&ReferenceEquals(proxy.GetProxiedFunction(),add));
        }
        // Attack: (150+7+3)*1.25=200. Defense: (50+7+3)*1.25=75.
        // Selected: (7+3)*1.25 truncates12 in both, preserving actual proxy conditions.
        Assert.Equal(200,stats.GetStat(StatEnum.PVE_ATTACK_RATIO,0).GetCurrent());
        Assert.Equal(75,stats.GetStat(StatEnum.PVE_DEFEND_RATIO,0).GetCurrent());
        Assert.Equal(12,SkyTempleCombatInputs.PveStat(player,StatEnum.PVE_ATTACK_RATIO));
        Assert.Equal(12,SkyTempleCombatInputs.PveStat(player,StatEnum.PVE_DEFEND_RATIO));
        // General host/player path is unchanged; selected filtering is local.
        Assert.Equal(200,stats.GetStat(StatEnum.PVE_ATTACK_RATIO,0).GetCurrent());
    }
}
