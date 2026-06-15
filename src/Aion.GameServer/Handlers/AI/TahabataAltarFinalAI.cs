using System;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/tiamatStrongHold/TahabataAltarFinalAI (Luzien).</summary>
[AIName("tahabataaltar2")]
public class TahabataAltarFinalAI : NpcAI
{
    private ScheduledTask task;

    public TahabataAltarFinalAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        task = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ => { AIActions.UseSkill(this, 20972); return ValueTask.CompletedTask; }, TimeSpan.Zero, TimeSpan.FromMilliseconds(2000));
    }

    protected override void HandleDespawned()
    {
        task.Cancel(true);
        base.HandleDespawned();
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.ALLOW_DECAY or AIQuestion.ALLOW_RESPAWN or AIQuestion.REWARD_AP_XP_DP_LOOT => false,
            _ => base.Ask(question),
        };
    }
}
