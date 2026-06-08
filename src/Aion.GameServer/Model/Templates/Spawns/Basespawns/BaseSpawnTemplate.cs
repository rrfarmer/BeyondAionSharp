using Aion.GameServer.Model.Base;

namespace Aion.GameServer.Model.Templates.Spawns.Basespawns;

/// <summary>Java parity: model/templates/spawns/basespawns/BaseSpawnTemplate.</summary>
public class BaseSpawnTemplate : SpawnTemplate
{
    private int           _id;
    private BaseOccupier  _occupier;

    public BaseSpawnTemplate(SpawnGroup spawnGroup, SpawnSpotTemplate spot)
        : base(spawnGroup, spot) { }

    public int          GetId()       => _id;
    public void         SetId(int id) => _id = id;
    public BaseOccupier GetOccupier() => _occupier;
    public void         SetOccupier(BaseOccupier occupier) => _occupier = occupier;
}
