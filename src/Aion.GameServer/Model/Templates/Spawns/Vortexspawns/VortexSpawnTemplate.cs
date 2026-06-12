using Aion.GameServer.Model.Vortex;

namespace Aion.GameServer.Model.Templates.Spawns.Vortexspawns;

/// <summary>Java parity: model/templates/spawns/vortexspawns/VortexSpawnTemplate.</summary>
public class VortexSpawnTemplate : SpawnTemplate
{
    private int            _id;
    private VortexStateType _stateType;

    public VortexSpawnTemplate(SpawnGroup spawnGroup, SpawnSpotTemplate spot)
        : base(spawnGroup, spot) { }

    public int             GetId()        => _id;
    public VortexStateType GetStateType() => _stateType;
    public void            SetId(int id)  => _id = id;
    public void            SetStateType(VortexStateType t) => _stateType = t;

    public bool IsInvasion() => _stateType == VortexStateType.INVASION;
    public bool IsPeace()    => _stateType == VortexStateType.PEACE;
}
