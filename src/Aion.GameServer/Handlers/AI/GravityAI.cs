using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Terath's gravity fields (283109, 283110). Retail pattern <c>IDTiamat_Sardha_GravityUp</c>.
/// </summary>
/// <remarks>
/// Java parity: ai/instance/tiamatStrongHold/GravityAI (@author Cheatkiller, Luzien). Retail-sourced
/// correction below; see docs/retail-ai-fidelity.md. Found by <c>audit_timer_drift.py</c>.
/// <para>
/// <b>It pulsed on landing and then every three and a quarter seconds.</b> Retail's whole pattern is
/// two rungs: <c>on_wake_up</c> sets an idle timer of <b>2000</b>, and each firing re-arms at
/// <b>2000</b>. So the field opens after two seconds and pulses every two — <b>a beat and a half faster
/// than this port ran it, and with an opening delay it did not have.</b> Standing in a gravity field is
/// meant to cost more than it did.
/// </para>
/// <para>
/// <b>The FX/DMG collapse is kept.</b> Retail's rung does not cast: it spawns
/// <c>IDTiamat_Thor_SumStatue_MgcAtk_NoShowNpc</c> on its own point for three seconds, the usual pair
/// this port folds into one npc that casts. Only the cadence is corrected.
/// </para>
/// <para>
/// <b>283110 has no pattern in the 5.8 dump at all</b> — only the up-field does. Both are given
/// retail's cadence here, because nothing contradicts it and the pair is placed together by
/// <c>BrigadeGeneralTerathAI</c> as one mechanic.
/// </para>
/// </remarks>
[AIName("gravity")]
public class GravityAI : NpcAI
{
    /// <summary>Retail <c>IDTiamat_Sardha</c> gives the field twenty-four seconds; Java used twenty.</summary>
    private const long FieldLifeMillis = 24000L;

    /// <summary>Retail's <c>set_idle_timer</c>, on waking and on every firing alike.</summary>
    public const long OpeningMillis = 2000L;
    public const long RepeatMillis = 2000L;

    private ScheduledTask? task;

    public GravityAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        task = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            AIActions.UseSkill(this, 20738);
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(OpeningMillis), System.TimeSpan.FromMilliseconds(RepeatMillis));
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            AIActions.DeleteOwner(this);
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, FieldLifeMillis);
    }

    protected override void HandleDespawned()
    {
        if (task != null)
            task.Cancel(true);
        base.HandleDespawned();
    }

    public override bool Ask(AIQuestion question)
    {
        switch (question)
        {
            case AIQuestion.ALLOW_DECAY:
            case AIQuestion.ALLOW_RESPAWN:
            case AIQuestion.REWARD_AP_XP_DP_LOOT:
                return false;
            default:
                return base.Ask(question);
        }
    }
}
