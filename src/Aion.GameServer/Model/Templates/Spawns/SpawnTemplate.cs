using Aion.GameServer.Model.Templates.Event;
using Aion.GameServer.SpawnEngine;

namespace Aion.GameServer.Model.Templates.Spawns;

/// <summary>Java parity: model/templates/spawns/SpawnTemplate.</summary>
public class SpawnTemplate
{
    public const string NoAi = "__NO_AI__";

    private float              _x;
    private float              _y;
    private float              _z;
    private byte               _h;
    private int                _staticId;
    private int                _randomWalk;
    private string?            _walkerId;
    private int?               _walkerIdx;
    private string?            _anchor;
    private readonly SpawnGroup _spawnGroup;
    private string?            _aiName;
    private int                _state;
    private int                _creatorId;
    private TemporarySpawn?    _temporarySpawn;

    // ── constructors ─────────────────────────────────────────────────────────

    public SpawnTemplate(SpawnGroup spawnGroup, SpawnSpotTemplate spot)
    {
        _spawnGroup    = spawnGroup;
        _x             = spot.X;
        _y             = spot.Y;
        _z             = spot.Z;
        _h             = spot.H;
        _staticId      = spot.StaticId;
        _randomWalk    = spot.RandomWalk;
        _walkerId      = spot.WalkerId;
        _anchor        = spot.Anchor;
        _walkerIdx     = spot.WalkerIdx;
        _aiName        = spot.Ai;
        _state         = spot.State;
        _temporarySpawn = spot.TemporarySpawn;
    }

    public SpawnTemplate(SpawnGroup spawnGroup, float x, float y, float z, byte heading,
        int randWalk, string? walkerId, int staticId)
        : this(spawnGroup, x, y, z, heading, randWalk, walkerId, staticId, 0, null) { }

    public SpawnTemplate(SpawnGroup spawnGroup, float x, float y, float z, byte heading,
        int randWalk, string? walkerId, int staticId, int creatorId, string? aiName)
    {
        _spawnGroup = spawnGroup;
        _x          = x;
        _y          = y;
        _z          = z;
        _h          = heading;
        _randomWalk = randWalk;
        _walkerId   = walkerId;
        _staticId   = staticId;
        _aiName     = aiName;
        _creatorId  = creatorId;
        AddTemplate();
    }

    private void AddTemplate() => _spawnGroup.AddSpawnTemplate(this);

    // ── accessors ─────────────────────────────────────────────────────────────
    public float   GetX() => _x;
    public void    SetX(float x) => _x = x;
    public float   GetY() => _y;
    public void    SetY(float y) => _y = y;
    public float   GetZ() => _z;
    public void    SetZ(float z) => _z = z;
    public byte    GetHeading() => _h;
    public void    SetHeading(byte h) => _h = h;
    public int     GetStaticId() => _staticId;
    public void    SetStaticId(int staticId) => _staticId = staticId;
    public int     GetRandomWalkRange() => _randomWalk;
    public int     GetNpcId()    => _spawnGroup.GetNpcId();
    public int     GetWorldId()  => _spawnGroup.GetWorldId();
    public string? GetWalkerId() => _walkerId;
    public void    SetWalkerId(string? walkerId) => _walkerId = walkerId;
    public int?    GetWalkerIndex() => _walkerIdx;
    public string? GetAnchor()   => _anchor;
    public string? GetAiName()   => _aiName;
    public int     GetState()    => _state;
    public int     GetCreatorId() => _creatorId;
    public SpawnGroup GetGroup() => _spawnGroup;

    public int GetRespawnTime()    => _spawnGroup.GetRespawnTime();
    public bool IsNoRespawn()      => _spawnGroup.GetRespawnTime() == 0;
    public bool HasPool()          => _spawnGroup.HasPool();
    public bool IsTemporarySpawn() => _spawnGroup.IsTemporarySpawn();
    public SpawnHandlerType? GetHandlerType() => _spawnGroup.GetHandlerType();
    public EventTemplate?  GetEventTemplate() => _spawnGroup.GetEventTemplate();
    public bool IsEventSpawn()     => GetEventTemplate() != null;

    public TemporarySpawn? GetTemporarySpawn() =>
        _temporarySpawn ?? _spawnGroup.GetTemporarySpawn();

    public SpawnTemplate? ChangeTemplate(int instanceId) =>
        _spawnGroup.ReserveRandomFreePoolSpot(instanceId);

    public void ResetPoolSpot(int instanceId) =>
        _spawnGroup.ResetPoolSpot(instanceId, this);
}
