using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Popuchin's shulack guided bomb (217374). Retail pattern <c>Station_Flight_GuiBomb</c>.
/// </summary>
/// <remarks>
/// Java parity: ai/instance/aturamSkyFortress/ShulackGuidedBombAI (xTz). Retail-sourced corrections
/// below; see docs/retail-ai-fidelity.md.
/// <para>
/// <b>It is a three-second fuse, not a proximity mine.</b> Retail's whole pattern is one
/// <c>on_enter_attack_state</c> that shouts and arms two battle timers — 3000 to cast on its current
/// target and despawn, 13000 to despawn regardless. There is no distance condition anywhere in it.
/// This class instead polled every second and only detonated once its target was <b>within four
/// units</b>, so <b>a player who simply kept walking was never hit at all</b>: the bomb trailed after
/// him until its ten-second clock ran out. That turns the boss's signature mechanic into scenery.
/// </para>
/// <para>
/// <b>And its clock started at the wrong moment.</b> The ten seconds ran from spawning; retail's
/// thirteen run from entering attack state. A bomb that nobody aggros has no timer of its own at all —
/// Popuchin despawns his own bombs when he leaves attack state, which is where that now happens.
/// </para>
/// <para>
/// <b>Deliberately kept:</b> the 3.2-second gap between the detonation and the npc leaving. Retail's
/// <c>despawn_self</c> is in the same action list as the <c>use_skill</c>, but the npc has to outlive
/// its own cast here, so the grace stays.
/// </para>
/// <para>
/// <b>Not translated:</b> the shout, <c>STR_CHAT_ShulackNM_06</c>, which has no id we can resolve.
/// </para>
/// </remarks>
[AIName("shulack_guided_bomb")]
public class ShulackGuidedBombAI : AggressiveNpcAI
{
    /// <summary>
    /// Retail's two battle timers, both armed on entering attack state: <c>BTIMERI_INDEX_1</c> at 3000
    /// detonates, <c>BTIMERI_INDEX_0</c> at 13000 removes a bomb that somehow never did.
    /// </summary>
    public const long FuseMillis = 3000L;
    public const long BackstopMillis = 13_000L;

    /// <summary>The grace the npc needs to outlive its own cast. Retail despawns in the same list.</summary>
    private const long AfterBlastMillis = 3200L;

    private bool isDestroyed;
    private bool isHome = true;
    private ScheduledTask fuseTask;
    private ScheduledTask backstopTask;

    public ShulackGuidedBombAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDespawned()
    {
        base.HandleDespawned();
        fuseTask?.Cancel(true);
        backstopTask?.Cancel(true);
    }

    /// <summary>
    /// Retail's <c>on_enter_attack_state</c>. Nothing is armed before this — a bomb nobody aggros waits
    /// for Popuchin to leave attack state and take it with him.
    /// </summary>
    protected override void HandleCreatureAggro(Creature creature)
    {
        base.HandleCreatureAggro(creature);
        if (!isHome)
            return;

        isHome = false;
        fuseTask = ThreadPoolManager.GetInstance().Schedule(_ => { Destroy(); return ValueTask.CompletedTask; }, FuseMillis);
        backstopTask = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!isDestroyed)
                Despawn();
            return ValueTask.CompletedTask;
        }, BackstopMillis);
    }

    private void Despawn()
    {
        if (!IsDead())
        {
            AIActions.DeleteOwner(this);
        }
    }

    /// <summary>
    /// Retail's detonation rung: cast on <c>OBJI_CUR_TARGET</c> and go. No distance is tested — whether
    /// the blast reaches is the skill's business, not the bomb's.
    /// </summary>
    private void Destroy()
    {
        if (isDestroyed || IsDead())
            return;

        isDestroyed = true;
        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19415, 49, GetOwner()).UseNoAnimationSkill();
        ThreadPoolManager.GetInstance().Schedule(_ => { Despawn(); return ValueTask.CompletedTask; }, AfterBlastMillis);
    }

    public override bool Ask(AIQuestion question)
    {
        switch (question)
        {
            case AIQuestion.ALLOW_DECAY:
            case AIQuestion.ALLOW_RESPAWN:
            case AIQuestion.REWARD_AP_XP_DP_LOOT:
                return false;
            case AIQuestion.IS_IMMUNE_TO_ABNORMAL_STATES:
                return true;
            default:
                return base.Ask(question);
        }
    }
}
