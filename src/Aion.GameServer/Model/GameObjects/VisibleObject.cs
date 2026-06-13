using Aion.GameServer.Controllers;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.World;
using Aion.GameServer.World.Knownlist;

namespace Aion.GameServer.Model.GameObjects;

/// <summary>
/// Base class for all in-game objects that can be spawned in the world at a position (players, npcs, …).
/// Tracks which objects are "known" via its <see cref="KnownList"/>.
/// Java parity: model/gameobjects/VisibleObject.
/// </summary>
public abstract class VisibleObject : AionObject
{
    private readonly VisibleObjectTemplate _objectTemplate;

    /// <summary>Position of object in the world. Java parity: protected WorldPosition position.</summary>
    protected WorldPosition Position;

    private KnownList _knownlist = null!;
    private readonly VisibleObjectController _controller;
    private VisibleObject? _target;
    private readonly SpawnTemplate? _spawnTemplate;

    // Java parity: VisibleObject(int, VisibleObjectController, SpawnTemplate, VisibleObjectTemplate, WorldPosition, boolean autoReleaseObjectId)
    // Note: the autoReleaseObjectId GC path is FR-1 (deferred); C# base ctor is AionObject(int).
    protected VisibleObject(int objId, VisibleObjectController controller, SpawnTemplate? spawnTemplate,
        VisibleObjectTemplate objectTemplate, WorldPosition position, bool autoReleaseObjectId)
        : base(objId)
    {
        _controller = controller;
        Position = position;
        _spawnTemplate = spawnTemplate;
        _objectTemplate = objectTemplate;
    }

    // Java parity: getName()
    public override string Name => _objectTemplate.GetName();

    // Java parity: getInstanceId()
    public int GetInstanceId() => GetPosition().GetInstanceId();

    // Java parity: getWorldId()
    public int GetWorldId() => GetPosition().GetMapId();

    // Java parity: getWorldType()
    public virtual WorldType GetWorldType() => World.World.GetInstance().GetWorldMap(GetWorldId()).GetWorldType();

    // Java parity: getWorldDropType()
    public WorldDropType GetWorldDropType() => World.World.GetInstance().GetWorldMap(GetWorldId()).GetWorldDropType();

    // Java parity: getX()/getY()/getZ()/getHeading()
    public float GetX() => GetPosition().GetX();
    public float GetY() => GetPosition().GetY();
    public float GetZ() => GetPosition().GetZ();
    public byte GetHeading() => GetPosition().GetHeading();

    // Java parity: getPosition()
    public WorldPosition GetPosition() => Position;

    // Java parity: getWorldMapInstance()
    public WorldMapInstance GetWorldMapInstance() => GetPosition().GetWorldMapInstance();

    // Java parity: isSpawned()
    public bool IsSpawned() => GetPosition().IsSpawned();

    // Java parity: isInWorld()
    public bool IsInWorld() => World.World.GetInstance().IsInWorld(ObjectId);

    // Java parity: isInInstance()
    public bool IsInInstance() => GetPosition().IsInstanceMap();

    // Java parity: clearKnownlist()
    public void ClearKnownlist() => ClearKnownlist(Aion.GameServer.Model.Animations.ObjectDeleteAnimation.FadeOut);

    // Java parity: clearKnownlist(ObjectDeleteAnimation)
    public void ClearKnownlist(Aion.GameServer.Model.Animations.ObjectDeleteAnimation animation) => GetKnownList().Clear(animation);

    // Java parity: updateKnownlist()
    public void UpdateKnownlist() => GetKnownList().Update();

    // Java parity: canSee(VisibleObject)
    public virtual bool CanSee(VisibleObject obj) => obj != null;

    // Java parity: setKnownlist(KnownList)
    public void SetKnownlist(KnownList knownlist) => _knownlist = knownlist;

    // Java parity: getKnownList()
    public KnownList GetKnownList() => _knownlist;

    // Java parity: getController()
    public virtual VisibleObjectController GetController() => _controller;

    // Java parity: final getTarget()
    public VisibleObject? GetTarget() => _target;

    // Java parity: final setTarget(VisibleObject)
    public void SetTarget(VisibleObject? creature)
    {
        if (_target != creature)
        {
            // Java assigns target BEFORE the callback, so onTargetChanged gets (newTarget, newTarget). Verbatim.
            _target = creature;
            GetController().OnTargetChanged(_target!, creature!);
        }
    }

    // Java parity: isTargeting(int)
    public bool IsTargeting(int objectId) => _target != null && _target.ObjectId == objectId;

    // Java parity: getSpawn()
    public virtual SpawnTemplate? GetSpawn() => _spawnTemplate;

    // Java parity: getObjectTemplate()
    public virtual VisibleObjectTemplate GetObjectTemplate() => _objectTemplate;

    // Java parity: setPosition(WorldPosition)
    public virtual void SetPosition(WorldPosition position) => Position = position;

    // Java parity: getVisibleDistance()
    public virtual float GetVisibleDistance() => 95;

    // Java parity: toString()
    public override string ToString() =>
        GetType().Name + " [" + (_objectTemplate != null ? "templateId=" + _objectTemplate.GetTemplateId() + ", " : "")
        + "objectId=" + ObjectId + ", name=" + Name + ", position=" + Position + "]";
}
