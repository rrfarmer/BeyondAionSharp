using Aion.GameServer.Model.Siege;

namespace Aion.GameServer.Model.Templates.Spawns.Siegespawns;

/// <summary>Java parity: model/templates/spawns/siegespawns/SiegeSpawnTemplate.</summary>
public class SiegeSpawnTemplate : SpawnTemplate
{
    private readonly int          _siegeId;
    private readonly SiegeRace    _siegeRace;
    private readonly SiegeModType _siegeModType;

    public SiegeSpawnTemplate(int siegeId, SiegeRace siegeRace, SiegeModType siegeModType,
        SpawnGroup spawnGroup, SpawnSpotTemplate spot)
        : base(spawnGroup, spot)
    {
        _siegeId      = siegeId;
        _siegeRace    = siegeRace;
        _siegeModType = siegeModType;
    }

    public SiegeSpawnTemplate(int siegeId, SiegeRace siegeRace, SiegeModType siegeModType,
        SpawnGroup spawnGroup, float x, float y, float z, byte heading, int randWalk,
        string? walkerId, int staticId)
        : base(spawnGroup, x, y, z, heading, randWalk, walkerId, staticId)
    {
        _siegeId      = siegeId;
        _siegeRace    = siegeRace;
        _siegeModType = siegeModType;
    }

    public int          GetSiegeId()     => _siegeId;
    public SiegeRace    GetSiegeRace()   => _siegeRace;
    public SiegeModType GetSiegeModType() => _siegeModType;
}
