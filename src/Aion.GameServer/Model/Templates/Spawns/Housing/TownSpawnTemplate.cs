using Aion.GameServer.Model.Templates.Spawns;

namespace Aion.GameServer.Model.Templates.Spawns.Housing;

/// <summary>Java parity: model/templates/spawns/housing/TownSpawnTemplate.</summary>
public class TownSpawnTemplate : SpawnTemplate
{
    private readonly int townId;

    public TownSpawnTemplate(SpawnGroup spawnGroup, SpawnSpotTemplate spot, int townId)
        : base(spawnGroup, spot)
    {
        this.townId = townId;
    }

    public int GetTownId()
    {
        return townId;
    }
}
