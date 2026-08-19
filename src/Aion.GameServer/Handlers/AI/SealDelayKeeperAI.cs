using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The seal guardian chiefs' delay keeper (855540). Retail pattern <c>IDSeal_Guardian_Seal_Keep</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md.
/// <para>
/// A chief drops one of these at its feet on waking and it stands eighty seconds. <b>This class is the
/// listener for <c>22610</c></b> — the broadcast a chief makes at fifty metres when it is killed,
/// carrying its killer as the message parameter. On hearing it the keeper casts on that killer, shows a
/// system message, and <b>leaves twenty seconds later</b> rather than standing out the rest of its
/// eighty.
/// </para>
/// <para>
/// An earlier pass added the keeper and recorded 22610 as <i>"names the killer at fifty metres and has
/// no listener in this port"</i>. It has one; it is this npc, and nothing was binding it. So a chief's
/// death left its keeper standing for however much of the eighty seconds remained.
/// </para>
/// <para>
/// <b>Not translated: the cast.</b> Retail's <c>use_skill</c> names <c>SKILLI_INDEX_0</c> against
/// <c>OBJI_MESSAGE_PARAM</c> — the killer — and 855540 has no row in our npc skill data. The timing is
/// ported and the effect on the killer is not.
/// </para>
/// </remarks>
[AIName("seal_delay_keeper")]
public class SealDelayKeeperAI : NpcAI, INpcMessageListener
{
    /// <summary>The chief's dying broadcast, and how long the keeper lingers after it.</summary>
    public const int ChiefKilled = 22610;
    private const long LingerMillis = 20_000L;

    private ScheduledTask lingerTask;

    public SealDelayKeeperAI(Npc owner)
        : base(owner)
    {
    }

    /// <summary>
    /// <c>22610</c> — the chief that dropped this keeper has been killed.
    /// </summary>
    public void OnNpcMessage(Npc sender, int messageType, VisibleObject param)
    {
        if (messageType != ChiefKilled || lingerTask != null)
            return;

        lingerTask = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (GetOwner().IsSpawned())
                AIActions.DeleteOwner(this);
            return ValueTask.CompletedTask;
        }, LingerMillis);
    }

    private void Stop()
    {
        if (lingerTask != null && !lingerTask.IsDone())
            lingerTask.Cancel(true);
        lingerTask = null;
    }

    protected override void HandleDespawned()
    {
        Stop();
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
