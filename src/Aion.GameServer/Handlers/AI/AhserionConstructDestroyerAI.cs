using System.Threading.Tasks;
using Aion.GameServer.Model;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.SpawnEngine;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Should also be able to request support once by dropping below 35% HP.
/// Java parity: ai/worlds/panesterra/ahserionsflight/AhserionConstructDestroyerAI (@author Estrayl).
/// </summary>
[AIName("ahserion_construct_destroyer")]
public class AhserionConstructDestroyerAI : AhserionAggressiveNpcAI
{
    private readonly AtomicBoolean isActivated = new AtomicBoolean();

    public AhserionConstructDestroyerAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        if (GetSpawnTemplate().GetHandlerType() == SpawnHandlerType.ATTACKER)
        {
            GetOwner().GetController().AddTask(TaskId.DESPAWN,
                ThreadPoolManager.GetInstance().Schedule(_ => { GetOwner().GetController().DeleteIfAliveOrCancelRespawn(); return ValueTask.CompletedTask; }, (long)System.TimeSpan.FromMinutes(9).TotalMilliseconds));
        }
    }

    /// <summary>Retail's <c>range_as_meter</c> on the call.</summary>
    private const float CallRange = 20f;

    /// <summary>
    /// Retail's <c>SPAWN_LOCATION_RELATIVE x=5 y=-5 z=5</c>, mirrored for the second pod.
    /// </summary>
    /// <remarks>
    /// The z was <b>0.5</b> here. Five is what the command says, and on an air fortress the difference
    /// is a pod that arrives beside its master rather than under his feet.
    /// </remarks>
    private const float PodUp = 5f;

    protected override void HandleCreatureAggro(Creature creature)
    {
        base.HandleCreatureAggro(creature);
        if (!isActivated.CompareAndSet(false, true))
            return;

        WorldPosition p = GetPosition();
        SpawnFor(PodAssassin, p.GetX() + 5, p.GetY() - 5, p.GetZ() + PodUp, (sbyte)0, TrooperLife);
        SpawnFor(PodAssassin, p.GetX() - 5, p.GetY() + 5, p.GetZ() + PodUp, (sbyte)0, TrooperLife);

        // Retail's last action in the same branch: broadcast_message 23000 to twenty metres, naming
        // its current target. The pods are meant to hear it -- that is what puts them on a player.
        NpcMessageBus.Broadcast(GetOwner(), DestroyerCall, creature, CallRange);
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        if (skillTemplate.GetSkillId() == 17446 && skillLevel == 57)
            AddHateToRndTarget();
    }

    /// <summary>
    /// Retail gives these troopers three minutes. <b>The nine-minute schedule in this class deletes the
    /// destroyer itself, not them</b> — a bound on the summoner is not a bound on the summoned, which is
    /// the same distinction that hid four other rows behind a self-timed verdict.
    /// </summary>
    private const int TrooperLife = 180;

    private void DespawnAssassins()
    {
        GetKnownList().ForEachNpc(npc =>
        {
            if (npc.GetNpcId() == PodAssassin)
                npc.GetController().DeleteIfAliveOrCancelRespawn();
        });
    }

    protected override void HandleBackHome()
    {
        DespawnAssassins();
        isActivated.Set(false);
        base.HandleBackHome();
    }

    protected override void HandleDied()
    {
        DespawnAssassins();
        base.HandleDied();
    }

    protected override void HandleDespawned()
    {
        DespawnAssassins();
        base.HandleDespawned();
    }
}
