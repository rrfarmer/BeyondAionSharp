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
/// <b>It was scoped to that one npc, and that was wrong.</b> The remark here used to say that other
/// npcs sharing this AI name "do not answer 23000" and that acting on the number alone would pull in
/// bystanders. Measured, <b>sixteen</b> npcs on this name and its sorcerer subclass answer 23000 in
/// retail. The worry was sound and the answer to it is per-npc data, not a hardcoded id:
/// <see cref="GuardAnswers"/> answers only for npcs whose own retail pattern has the rung, so a
/// bystander that genuinely does not answer still does not.
/// </para>
/// <para>
/// The two rungs are no longer simplified either. A pod already fighting does a real
/// <c>switch_target</c> with 100 points; one that is not does <c>add_hate_point 1</c> and
/// <c>attack_most_hating</c>. Both live in <see cref="GuardAnswers"/> now.
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
    public new void OnNpcMessage(Npc sender, int messageType, VisibleObject? param)
    {
        if (IsDead())
            return;

        GuardAnswers.AnswerCall(GetOwner(), sender, messageType, param);
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
