using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The complete traps RM-56c lays (281281). Retail pattern <c>NLehpar_BhCSumA</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Found by
/// <c>tools/client-extract/audit_retail_messages.py</c> — message <b>6681</b>, which
/// <see cref="Rm56cAI"/> was recorded as not sending because the generic <c>trap</c> class has no
/// listener for it. This is that listener.
/// <para>
/// <b>Every trap-laying branch sends it, immediately after the traps.</b> Ten metres, and the traps
/// answer by leaving — so laying a new arrangement takes the last one away. Without it a boss walked
/// down through two bands stands in two overlapping sets at once, and the re-lay path makes that
/// common rather than rare.
/// </para>
/// <para>
/// <b>The traps he has just laid do not hear it, and that took a change to shared machinery.</b> Our
/// spawn path puts a summon in its spawner's known list before the next action of the same branch
/// runs, so the broadcast reached the new arrangement and deleted it on arrival — measured, and the
/// whole of RM-56c's pin set failed on it. <see cref="Aion.GameServer.Ai.Pattern.PatternAi"/> now
/// remembers what the running branch spawned and excludes it, which is what retail's ordering
/// evidently does for free.
/// </para>
/// <para>
/// It extends <see cref="TrapNpcAI"/> rather than replacing it, so a trap still arms and fires on
/// whoever walks into it; this only adds the branch that dismisses it.
/// </para>
/// <para>
/// <b>Not translated:</b> the cast that accompanies the despawn — one <c>SKILLI_INDEX</c> against a
/// pattern with no branch comments, the same refusal recorded on <see cref="Rm56cAI"/> itself.
/// </para>
/// </remarks>
[AIName("complete_trap")]
public class CompleteTrapAI : TrapNpcAI, INpcMessageListener
{
    /// <summary>Retail's message: the arrangement before this one is finished with.</summary>
    public const int LayAnother = 6681;

    /// <summary>Retail's <c>range_as_meter</c>, on every branch that sends it.</summary>
    public const float Reach = 10f;

    public CompleteTrapAI(Npc owner)
        : base(owner)
    {
    }

    public void OnNpcMessage(Npc sender, int messageType, VisibleObject? param)
    {
        if (messageType != LayAnother || IsDead())
            return;

        AIActions.DeleteOwner(this);
    }
}
