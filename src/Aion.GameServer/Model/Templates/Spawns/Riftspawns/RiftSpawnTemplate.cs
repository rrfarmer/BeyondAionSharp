namespace Aion.GameServer.Model.Templates.Spawns.Riftspawns;

/// <summary>Java parity: model/templates/spawns/riftspawns/RiftSpawnTemplate.</summary>
public class RiftSpawnTemplate : SpawnTemplate
{
    private int _id;

    public RiftSpawnTemplate(SpawnGroup spawnGroup, SpawnSpotTemplate spot)
        : base(spawnGroup, spot) { }

    public int  GetId()       => _id;
    public void SetId(int id) => _id = id;
}
