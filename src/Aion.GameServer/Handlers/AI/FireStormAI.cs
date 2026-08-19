using System;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Tahabata's two hazards: the fire storm statue (283045) and the fire tornado (283102).
/// </summary>
/// <remarks>
/// Java parity: ai/instance/tiamatStrongHold/FireStormAI (@author Cheatkiller). Retail-sourced
/// corrections below; see docs/retail-ai-fidelity.md.
/// <para>
/// <b>These are two different retail npcs sharing one class, and neither cadence was right.</b> 283045
/// is <c>IDTiamat_Thor_SumStatue_PhyAtk</c> and 283102 is <c>IDTiamat_Tahabata_Tornado</c> — separate
/// patterns that only look alike because this port collapsed both into "cast on a one-second timer from
/// the moment you spawn".
/// </para>
/// <para>
/// <b>The statue opens after three seconds and then pulses every two.</b> Retail arms
/// <c>BTIMERI_INDEX_0</c> at 3000 and re-arms it at 2000. It was pulsing every second, starting
/// immediately — twice retail's rate, with no opening at all.
/// </para>
/// <para>
/// <b>The tornado opens after two seconds and then pulses every two.</b> Retail's <c>on_wake_up</c> sets
/// an idle timer of 2000 and each firing sets another 2000. Same doubled rate, same missing opening.
/// </para>
/// <para>
/// <b>And the statue stood twenty seconds where retail gives it a hundred and eighty.</b> The boss
/// spawns it in his <c>ConcentratedFire</c> rung with <c>live_time=180</c>; this class deleted it after
/// twenty, so the statue that is meant to follow a player around for three minutes was gone almost at
/// once. Found by <c>audit_lifetime_conflicts.py</c>.
/// </para>
/// <para>
/// <b>What is simplified:</b> retail's tornado does not cast — it spawns
/// <c>IDTiamat_Tahabata_TornadoDMGArea</c> on its own point for three seconds each pulse, the usual
/// FX/DMG pair this port collapses into one npc that casts. The collapse is kept; only the cadence is
/// corrected. Retail also arms the statue's timer on <c>on_enter_attack_state</c> rather than on
/// spawning, which is a distinction without a difference here because neither npc fights.
/// </para>
/// </remarks>
[AIName("tahabatafirestorm")]
public class FireStormAI : NpcAI
{
    /// <summary>The fire tornado, and the fire storm statue that follows a player.</summary>
    public const int Tornado = 283102;
    public const int Statue = 283045;

    /// <summary>
    /// Retail's opening delay on each: the statue's <c>add_battle_timer</c> is 3000, the tornado's
    /// <c>set_idle_timer</c> is 2000. Both then repeat at 2000.
    /// </summary>
    private const long StatueOpeningMillis = 3000L;
    private const long TornadoOpeningMillis = 2000L;
    private const long RepeatMillis = 2000L;

    /// <summary>Retail's opening delay for an npc on this AI. Exposed so the pins can read it.</summary>
    public static long OpeningMillisFor(int npcId) =>
        npcId == Tornado ? TornadoOpeningMillis : StatueOpeningMillis;

    /// <summary>Retail's repeat delay. Both npcs re-arm at two seconds.</summary>
    public static long RepeatMillisFor(int npcId) => RepeatMillis;

    /// <summary>
    /// Retail's <c>live_time</c> on the statue, from the boss's <c>ConcentratedFire</c> rung. The
    /// tornado is spawned with no <c>live_time</c> at all and stands until the fight ends.
    /// </summary>
    private const long StatueLifeMillis = 180_000L;

    private ScheduledTask task;

    public FireStormAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        bool tornado = GetNpcId() == Tornado;
        int skill = tornado ? 20753 : 20759; // 4.0
        task = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(
            _ => { AIActions.UseSkill(this, skill); return ValueTask.CompletedTask; },
            TimeSpan.FromMilliseconds(OpeningMillisFor(GetNpcId())),
            TimeSpan.FromMilliseconds(RepeatMillisFor(GetNpcId())));

        if (!tornado)
            Despawn();
    }

    private void Despawn()
    {
        ThreadPoolManager.GetInstance().Schedule(_ => { GetOwner().GetController().Delete(); return ValueTask.CompletedTask; }, StatueLifeMillis);
    }

    protected override void HandleDespawned()
    {
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
