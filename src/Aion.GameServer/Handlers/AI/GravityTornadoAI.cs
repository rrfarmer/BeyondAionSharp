using System;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Tiamat's gravity tornado, normal (283140) and hard (856046). Java parity:
/// ai/instance/dragonLordsRefuge/GravityTornadoAI (@author Luzien, Estrayl), with the crusher and the
/// mode split taken from retail patterns <c>IDTiamat_Tiamat_Gravity</c> and <c>IDTiamat_Hard_Gravity</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Found by
/// <c>tools/client-extract/audit_shared_ai_names.py</c>, and it was wrong in two ways at once.
/// <list type="bullet">
/// <item><b>It never spawned its crusher.</b> Both patterns put a gravity crusher on the tornado's own
/// mark as it appears — 283142 in normal, <b>856047</b> in hard — and clear it when the tornado goes.
/// The hard one was reachable by nobody.</item>
/// <item><b>Both modes cast the hard-mode skill.</b> The class chose with
/// <c>GetNpcId() == 283142 ? 20966 : 21901</c> — and 283142 is the <em>crusher</em>, which never
/// carries this AI, so the test could not be true and every tornado took the else branch.</item>
/// </list>
/// <para>
/// <b>Which skill is which is corroborated, not guessed.</b> Both are named "Gravitational Confusion"
/// and are told apart by their stack name: 20966 is <c>IDTIAMAT_TIAMAT_GRAVITY_SKILL</c> and 21901 is
/// <c>IDTIAMAT_HARD_TIAMAT_GRAVITY_SKILL</c>. That matches the two patterns exactly, so the ternary's
/// intent is certain even though what it tested could not work. Keyed by tornado now.
/// </para>
/// <para>
/// <b>The cast cadence is ours and stays.</b> Retail casts once on waking and then only on
/// <c>on_message</c> 204 — and nothing in our tree sends 204, so translating that half literally would
/// leave the tornado casting once and falling silent. The Java timer keeps it doing its job; the
/// divergence is recorded rather than repaired, because repairing it means an instance script we do
/// not have.
/// </para>
/// </remarks>
[AIName("gravity_tornado")]
public class GravityTornadoAI : NpcAI
{
    private const int NormalTornado = 283140;
    private const int HardTornado = 856046;

    private const int NormalCrusher = 283142;
    private const int HardCrusher = 856047;

    /// <summary>Told apart by stack name, not by guesswork — see the remarks.</summary>
    private const int NormalGravity = 20966;
    private const int HardGravity = 21901;

    private static readonly TimeSpan FirstCast = TimeSpan.FromMilliseconds(2500);
    private static readonly TimeSpan CastInterval = TimeSpan.FromMilliseconds(6000);

    private ScheduledTask? task;
    private Npc? crusher;

    public GravityTornadoAI(Npc owner)
        : base(owner)
    {
    }

    /// <summary>Which gravity skill a tornado casts. The bug this class was carrying, in one line.</summary>
    /// <remarks>
    /// Internal so it can be pinned directly: the cast itself goes through
    /// <c>NpcController.UseSkill</c>, which fires immediately rather than through the skill queue the
    /// harness can read, so the choice is observable where the cast is not.
    /// </remarks>
    internal static int GravitySkillFor(int tornadoId) =>
        tornadoId == HardTornado ? HardGravity : NormalGravity;

    /// <summary>Which crusher a tornado brings, for the same reason.</summary>
    internal static int CrusherFor(int tornadoId) =>
        tornadoId == HardTornado ? HardCrusher : NormalCrusher;

    protected override void HandleSpawned()
    {
        base.HandleSpawned();

        // Retail's on_wake_up: the crusher lands on the tornado's own mark.
        if (Spawn(CrusherFor(GetNpcId()), GetOwner().GetX(), GetOwner().GetY(),
                GetOwner().GetZ(), (sbyte)GetOwner().GetHeading()) is Npc spawned)
            crusher = spawned;

        int skillId = GravitySkillFor(GetNpcId());
        task = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            AIActions.UseSkill(this, skillId);
            return ValueTask.CompletedTask;
        }, FirstCast, CastInterval);
    }

    protected override void HandleDespawned()
    {
        if (task != null && !task.IsDone())
            task.Cancel(true);
        task = null;

        // Retail's on_despawn clears SPAWN_ID_1, which is the crusher.
        crusher?.GetController().DeleteIfAliveOrCancelRespawn();
        crusher = null;

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
