using System;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Sardha's black hole (283096-283098). Retail patterns <c>IDTiamat_Sardha_BlackHoleFX</c>,
/// <c>_BlackHoleDMG</c> and <c>_BlackHoleOnDie</c>.
/// </summary>
/// <remarks>
/// Java parity: ai/instance/tiamatStrongHold/DistortedSpaceAI (@author Cheatkiller). Retail-sourced
/// correction below; see docs/retail-ai-fidelity.md. Found by <c>audit_timer_drift.py</c>.
/// <para>
/// <b>It opened a second early.</b> Retail's FX npc is the controller: <c>on_wake_up</c> sets an idle
/// timer of <b>1500</b>, and each firing lays a three-second damage npc and re-arms at <b>2000</b>.
/// This port opened at <b>500</b> and repeated at 2000 — the repeat was right and the first pulse
/// arrived a full second before it should, which on a hole that pulls players in is the difference
/// between being caught by it and walking clear.
/// </para>
/// <para>
/// <b>The three npcs are collapsed into one here, and the pulsing one is the wrong member of the
/// trio.</b> Retail drives the cadence from the FX npc (283096) and spawns the DMG npc (283097) fresh
/// for each pulse; this port has 283097 do the pulsing itself. That is the usual FX/DMG collapse and it
/// is kept — but it is worth recording which way round it went, because the guard in
/// <see cref="UseSkill"/> reads as though 283097 were the controller.
/// </para>
/// <para>
/// <b>And the ten seconds were a backstop, not the mechanic.</b> Retail's hole is spawned with
/// <c>live_time=10</c>, but the same branch that spawns it arms <c>BTIMERI_INDEX_28</c> at <b>2000</b>,
/// and that timer's rung broadcasts <b>31</b> — which the hole answers by closing. So a black hole
/// stands <b>two seconds</b> in a fight and ten only if the close never arrives. This port had no close
/// at all, so every hole ran the full ten: <b>five times too long</b>, on a hazard that drags players
/// into it.
/// </para>
/// <para>
/// <b>Partly translated.</b> Retail's FX npc answers 31 by spawning
/// <c>IDTiamat_Sardha_BlackHoleOnDie</c> for three seconds and then despawning. This port already
/// spawns all three npcs together at the summoner, so the closing flash is standing before it is
/// needed; the answer here despawns the hole and leaves the flash's own clock to it.
/// </para>
/// </remarks>
[AIName("distortedspace")]
public class DistortedSpaceAI : NpcAI, INpcMessageListener
{
    /// <summary>Retail's "close the hole" message, broadcast by the summoner at fifty metres.</summary>
    public const int CloseHole = 31;

    /// <summary>Retail's <c>set_idle_timer</c> on the controller: 1500 on waking, 2000 thereafter.</summary>
    public const long OpeningMillis = 1500L;
    public const long RepeatMillis = 2000L;

    private ScheduledTask task;

    public DistortedSpaceAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        UseSkill();
    }

    /// <summary>
    /// Retail <c>IDTiamat_Sardha</c> gives this ten seconds; Java closed it at eight.
    /// </summary>
    /// <remarks>
    /// <b>The bound was never missing, only short.</b> An audit pass added a ten-second lifetime to
    /// Terath's spawn call to fix an add that "never expired" — but this add has always killed itself,
    /// two seconds early, and the summoner-side lifetime was dead code because the shorter clock always
    /// won. Corrected here, where the clock actually is.
    /// </remarks>
    private const long BlackHoleLifeMillis = 10000L;

    /// <summary>Retail's <c>on_message 31</c> rung: the hole shuts.</summary>
    public void OnNpcMessage(Npc sender, int messageType, VisibleObject? param)
    {
        if (messageType != CloseHole || IsDead())
            return;

        CancelTask();
        AIActions.DeleteOwner(this);
    }

    private void UseSkill()
    {
        task = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (GetOwner().GetNpcId() == 283097)
                AIActions.UseSkill(this, 20740);
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(OpeningMillis), TimeSpan.FromMilliseconds(RepeatMillis));

        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            CancelTask();
            if (GetOwner().GetNpcId() == 283097)
                AIActions.UseSkill(this, 20742);
            GetOwner().GetController().Die();
            return ValueTask.CompletedTask;
        }, BlackHoleLifeMillis);
    }

    private void CancelTask()
    {
        if (task != null && !task.IsCancelled)
        {
            task.Cancel(true);
        }
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        CancelTask();
        AIActions.DeleteOwner(this);
    }

    protected override void HandleDespawned()
    {
        base.HandleDespawned();
        CancelTask();
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
