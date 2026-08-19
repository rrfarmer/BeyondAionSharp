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
/// <b>The cast cadence is retail's now.</b> This used to read "the cast cadence is ours and stays …
/// nothing in our tree sends 204 … repairing it means an instance script we do not have". <b>No
/// instance script was needed.</b> The sender is the damage twin this class already spawns on its own
/// mark, which ran plain <c>aggressive</c> and so beat no time; it has a class now
/// (<see cref="GravityBombDamageAI"/>) and pulses 204 at one metre, one second in and every three
/// after.
/// <para>
/// The Java timer it replaces fired first at 2.5 seconds and then every <b>six</b> — half retail's rate.
/// The tornado still casts once as it appears, which retail does too on <c>on_wake_up</c>.
/// </para>
/// </para>
/// </remarks>
[AIName("gravity_tornado")]
public class GravityTornadoAI : NpcAI, INpcMessageListener
{
	/// <summary>Retail's <c>on_message</c> 204, sent by the damage twin standing on this tornado.</summary>
	public void OnNpcMessage(Npc sender, int messageType, VisibleObject? param)
	{
		if (messageType == GravityBombDamageAI.CastNow && !IsDead())
			AIActions.UseSkill(this, GravitySkillFor(GetNpcId()));
	}

    private const int NormalTornado = 283140;
    private const int HardTornado = 856046;

    private const int NormalCrusher = 283142;
    private const int HardCrusher = 856047;

    /// <summary>Told apart by stack name, not by guesswork — see the remarks.</summary>
    private const int NormalGravity = 20966;
    private const int HardGravity = 21901;

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

        // Retail's own on_wake_up cast. Everything after it is driven by the twin's 204, so there is
        // no repeating timer here any more -- see the remarks.
        AIActions.UseSkill(this, GravitySkillFor(GetNpcId()));
    }

    protected override void HandleDespawned()
    {
        // No timer to cancel any more: the beat comes from the twin, and clearing the twin below stops
        // it. That is retail's own arrangement -- the tornado never kept time for itself.

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
