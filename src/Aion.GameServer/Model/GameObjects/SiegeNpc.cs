namespace Aion.GameServer.Model.GameObjects.Siege;

/// <summary>Java parity: model/gameobjects/siege/SiegeNpc extends Npc.</summary>
public class SiegeNpc : Npc
{
    public SiegeNpc(Aion.GameServer.Controllers.NpcController controller, Aion.GameServer.Model.Templates.Spawns.Siege.SiegeSpawnTemplate spawnTemplate, Aion.GameServer.Model.Templates.Npc.NpcTemplate objectTemplate)
        : base(controller, spawnTemplate, objectTemplate)
    {
    }

    public Aion.GameServer.Model.Siege.SiegeRace GetSiegeRace()
    {
        return GetSpawn().GetSiegeRace();
    }

    public int GetSiegeId()
    {
        return GetSpawn().GetSiegeId();
    }

    public override Aion.GameServer.Model.Templates.Spawns.Siege.SiegeSpawnTemplate GetSpawn()
    {
        return (Aion.GameServer.Model.Templates.Spawns.Siege.SiegeSpawnTemplate)base.GetSpawn();
    }

    public override bool IsEnemyFrom(Creature creature)
    {
        if (creature is SiegeNpc siegeNpc && siegeNpc.GetSiegeRace() != GetSiegeRace())
            return true;
        return base.IsEnemyFrom(creature);
    }
}
