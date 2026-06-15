using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services;
using Aion.GameServer.Utils.Time;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/events/RvrGuardAI (Bobobear).</summary>
[AIName("rvr_guard")]
public class RvrGuardAI : AggressiveNpcAI
{
    public RvrGuardAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        // add player to event list for additional reward
        if (creature is Player && GetPosition().GetMapId() == 600010000)
        {
            int hour = ServerTime.Now().Hour;
            if (hour >= 19 && hour <= 23)
            {
                Npc bossAsmo = GetPosition().GetWorldMapInstance().GetNpc(220948);
                Npc bossElyos = GetPosition().GetWorldMapInstance().GetNpc(220949);
                if (bossAsmo != null && bossElyos != null)
                {
                    SiegeService.GetInstance().CheckRvrEventPlayer((Player)creature);
                }
            }
        }
    }
}
