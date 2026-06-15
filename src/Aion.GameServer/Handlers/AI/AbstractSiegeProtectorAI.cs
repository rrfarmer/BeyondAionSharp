using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Siege;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Siege;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/siege/AbstractSiegeProtectorAI.</summary>
public abstract class AbstractSiegeProtectorAI : SiegeNpcAI
{
    public AbstractSiegeProtectorAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        GetAggroList().Clear(); // make sure old damages aren't counted in stopSiege
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        StopSiege((SiegeNpc)GetOwner());
    }

    internal static void StopSiege(SiegeNpc siegeProtector)
    {
        Siege siege = SiegeService.GetInstance().GetSiege(siegeProtector.GetSiegeId());
        foreach (AggroInfo aggroInfo in siegeProtector.GetAggroList().Stream())
            siege.GetSiegeCounter().AddDamage(aggroInfo.GetAttacker().GetMaster(), aggroInfo.GetDamage());
        siege.SetBossKilled(true);
        SiegeService.GetInstance().StopSiege(siege.GetSiegeLocationId());
    }
}
