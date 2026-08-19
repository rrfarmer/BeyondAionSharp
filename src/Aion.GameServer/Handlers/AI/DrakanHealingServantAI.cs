using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The drakan healing servant (282988). Retail pattern <c>IDYun_Temp_69</c>.
/// </summary>
/// <remarks>
/// Java parity: ai/classNpc/DrakanHealingServantAI (@author Cheatkiller). Retail-sourced correction
/// below; see docs/retail-ai-fidelity.md. Found by <c>audit_timer_drift.py</c>.
/// <para>
/// <b>It healed at half retail's rate.</b> The totem's whole pattern is two rungs: entering attack
/// state arms <c>BTIMERI_INDEX_0</c> at 1000, and the rung it fires re-arms at <b>3000</b> and casts.
/// This port re-armed at <b>6000</b>. A healing servant is an add a group is meant to have to kill
/// quickly; at half the throughput it was never the pressure it should have been.
/// </para>
/// <para>
/// <b>The opening is this port's own and is left alone.</b> Retail measures its 1000 from entering
/// attack state; here the servant waits 2000 after spawning to acquire its creator as a target and
/// then opens at 1000, so the first heal lands about three seconds in rather than one after it is
/// pulled. The acquisition step is plumbing this port needs and retail does not, and shortening it
/// risks a servant that finds no creator and never heals at all — a worse failure than a late first
/// tick. Recorded rather than tuned.
/// </para>
/// </remarks>
[AIName("drakanhealingservant")]
public class DrakanHealingServantAI : NpcAI
{
    /// <summary>Retail's <c>add_battle_timer</c> on the heal rung, which re-arms itself.</summary>
    public const long HealRepeatMillis = 3000L;

    /// <summary>Retail's opening, armed on entering attack state.</summary>
    public const long HealOpeningMillis = 1000L;

    public DrakanHealingServantAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        ThreadPoolManager.GetInstance().Schedule(() =>
        {
            if (GetCreator() == null)
            {
                return;
            }
            AIActions.TargetCreature(this, (Creature)GetCreator());
            Heal();
        }, 2000L);
    }

    private void Heal()
    {
        if (IsDead() || !GetPosition().IsSpawned())
        {
            return;
        }
        ScheduledTask task = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ => { GetOwner().GetController().UseSkill(20520); return System.Threading.Tasks.ValueTask.CompletedTask; }, System.TimeSpan.FromMilliseconds(HealOpeningMillis), System.TimeSpan.FromMilliseconds(HealRepeatMillis));
        GetOwner().GetController().AddTask(TaskId.SKILL_USE, task);
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
