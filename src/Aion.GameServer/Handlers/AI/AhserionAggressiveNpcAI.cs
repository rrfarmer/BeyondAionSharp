using Aion.GameServer.Ai;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Spawns.Panesterra;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/worlds/panesterra/ahserionsflight/AhserionAggressiveNpcAI (Yeats).</summary>
[AIName("ahserion_aggressive_npc")]
public class AhserionAggressiveNpcAI : AggressiveNoLootNpcAI
{
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
