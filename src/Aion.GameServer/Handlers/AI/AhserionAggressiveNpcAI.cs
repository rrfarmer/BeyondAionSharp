using Aion.GameServer.Ai;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Spawns.Panesterra;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The shared aggressive AI for Ahserion's Flight, and the pod assassin's answer to its master's call.
/// </summary>
/// <remarks>
/// Java parity: ai/worlds/panesterra/ahserionsflight/AhserionAggressiveNpcAI (Yeats). Retail-sourced
/// correction below; see docs/retail-ai-fidelity.md.
/// <para>
/// <b>The pod assassins were deaf.</b> 297191 is <c>BGab1_Sub_Pod_Sum_Vri_As_65_Ae</c>, whose retail
/// pattern (<c>Gab1_Sub_Pod_Sum_Vri_As</c>) answers message <b>23000</b> — the one the reserve assault
/// leader broadcasts in the same branch that spawns them. Without it a pair of assassins appeared
/// beside their master and then <b>stood there</b> until somebody happened to walk into them, which is
/// the opposite of what an ambush pod is for.
/// </para>
/// <para>
/// <b>Scoped to that npc on purpose.</b> Several unrelated npcs share this AI name and their retail
/// patterns do not answer 23000; message numbers are per encounter, so a listener that acted on the
/// number alone would pull in bystanders.
/// </para>
/// <para>
/// <b>Simplified:</b> retail has two rungs — a pod already fighting does <c>switch_target</c> with 100
/// points, one that is not does <c>add_hate_point 1</c> and <c>attack_most_hating</c>. Both go through
/// <see cref="SummonOrder"/> here, with the hate value from the matching rung. That is weaker than a
/// forced switch for the fighting case, which is the safer direction: it will not drag a pod off a
/// player it has genuinely built hate on.
/// </para>
/// </remarks>
[AIName("ahserion_aggressive_npc")]
public class AhserionAggressiveNpcAI : AggressiveNoLootNpcAI, INpcMessageListener
{
    /// <summary>The reserve assault leader's call, and the pod that answers it.</summary>
    public const int DestroyerCall = 23000;
    public const int PodAssassin = 297191;

    /// <summary>Retail's <c>points_to_add</c> on the rung for a pod that is already fighting.</summary>
    private const int SwitchPoints = 100;

    /// <summary>
    /// Retail's <c>on_message</c> pair on <c>Gab1_Sub_Pod_Sum_Vri_As</c>, both guarded by
    /// <c>is_enemy(OBJI_MESSAGE_PARAM)</c>, which <see cref="SummonOrder.Take"/> checks for us.
    /// </summary>
    public void OnNpcMessage(Npc sender, int messageType, VisibleObject? param)
    {
        if (messageType != DestroyerCall || GetNpcId() != PodAssassin || IsDead())
            return;

        SummonOrder.Take(GetOwner(), param,
            GetOwner().GetAi().IsInState(AIState.FIGHT) ? SwitchPoints : SummonOrder.OnePoint);
    }

    public AhserionAggressiveNpcAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        if (GetNpcId() == 277242)
        {
            GetOwner().GetController().AddTask(TaskId.DESPAWN,
                ThreadPoolManager.GetInstance().Schedule(_ => { GetOwner().GetController().DeleteIfAliveOrCancelRespawn(); return ValueTask.CompletedTask; }, TimeSpan.FromMinutes(8)));
        }
    }

    protected void AddHateToRndTarget()
    {
        GetAggroList().AddHate(GetAggroList().GetTarget(AggroTarget.RANDOM), 100000);
    }

    protected new AhserionsFlightSpawnTemplate GetSpawnTemplate()
    {
        return (AhserionsFlightSpawnTemplate)base.GetSpawnTemplate();
    }
}
