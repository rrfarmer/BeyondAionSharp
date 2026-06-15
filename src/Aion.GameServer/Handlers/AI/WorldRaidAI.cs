using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Sykra
/// </summary>
[AIName("world_raid_aggressive")]
public class WorldRaidAI : AggressiveNpcAI
{
    private readonly AtomicBoolean isEffectNpcSpawned = new AtomicBoolean();
    private Npc effectNpc;
    private ScheduledTask effectNpcDespawnTask;

    public WorldRaidAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        if (isEffectNpcSpawned.CompareAndSet(false, true))
        {
            // spawn npc 702549 (WorldRaid_Advent_Effect)
            effectNpc = (Npc)Spawn(702549, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)0);
            effectNpcDespawnTask = ThreadPoolManager.GetInstance().Schedule(ct =>
            {
                if (effectNpc != null)
                {
                    effectNpc.GetController().Delete();
                    effectNpc = null;
                }
                effectNpcDespawnTask = null;
                return ValueTask.CompletedTask;
            }, 10000L);
        }
        base.HandleAttack(creature);
    }

    protected override void HandleBackHome()
    {
        CancelDespawnTaskAndDespawnEffectNpc();
        base.HandleBackHome();
    }

    protected override void HandleDespawned()
    {
        CancelDespawnTaskAndDespawnEffectNpc();
        base.HandleDespawned();
    }

    protected override void HandleDied()
    {
        CancelDespawnTaskAndDespawnEffectNpc();
        base.HandleDied();
    }

    private void CancelDespawnTaskAndDespawnEffectNpc()
    {
        if (effectNpcDespawnTask != null && !effectNpcDespawnTask.IsCancelled)
        {
            effectNpcDespawnTask.Cancel(true);
            effectNpcDespawnTask = null;
        }
        if (effectNpc != null)
        {
            effectNpc.GetController().Delete();
            effectNpc = null;
        }
        isEffectNpcSpawned.Set(false);
    }
}
