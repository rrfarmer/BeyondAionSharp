using System;
using System.Threading.Tasks;
using Aion.GameServer.GeoEngine.Collision;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.GeoEngine.Models;
using Aion.GameServer.GeoEngine.Scene;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Aion.GameServer.World.Geo;

namespace Aion.GameServer.Controllers.Observer;

/// <summary>Java parity: controllers/observer/AbstractCollisionObserver (MrPoke, moved Rolandas).</summary>
public abstract class AbstractCollisionObserver : ActionObserver
{
    protected Creature creature;
    protected Vector3f oldPos;
    protected Spatial geometry;
    protected sbyte intentions;
    private readonly CheckType checkType;
    private AtomicBoolean isRunning = new AtomicBoolean();

    public AbstractCollisionObserver(Creature creature, Spatial geometry, sbyte intentions, CheckType checkType)
        : base(ObserverType.MOVE_OR_DIE)
    {
        this.creature = creature;
        this.geometry = geometry;
        WorldPosition lastPos;
        if (creature is Player && (lastPos = ((Player) creature).GetMoveController().GetLastPositionFromClient()) != null)
            this.oldPos = new Vector3f(lastPos.GetX(), lastPos.GetY(), lastPos.GetZ());
        else
            this.oldPos = new Vector3f(creature.GetX(), creature.GetY(), creature.GetZ());
        this.intentions = intentions;
        this.checkType = checkType;
    }

    public override void Moved()
    {
        if (!isRunning.GetAndSet(true))
        {
            ThreadPoolManager.GetInstance().Schedule(ct =>
            {
                try
                {
                    Vector3f pos;
                    Vector3f dir;
                    if (checkType == CheckType.TOUCH) // check if we are standing on the geometry (either top or bottom)
                    {
                        float x = creature.GetX();
                        float y = creature.GetY();
                        float z = creature.GetZ();
                        float zMax = z + 0.05f + creature.GetObjectTemplate().GetBoundRadius().GetUpper();
                        float zMin = z - 0.11f;
                        if (creature is Player)
                        {
                            if (((Player) creature).GetMoveController().IsJumping() || !((Player) creature).IsInGlidingState() && !creature.IsFlying())
                            {
                                float geoZ = GeoService.GetInstance().GetZ(creature.GetWorldId(), x, y, z, creature.GetInstanceId());
                                if (!float.IsNaN(geoZ))
                                {
                                    zMin = geoZ - 0.11f;
                                }
                            }
                        }
                        pos = new Vector3f(x, y, zMax);
                        dir = new Vector3f(pos.GetX(), pos.GetY(), zMin);
                    }
                    else // check if we passed the geometry (either entering or leaving)
                    {
                        pos = new Vector3f(creature.GetX(), creature.GetY(), creature.GetZ() + GeoMap.COLLISION_CHECK_Z_OFFSET);
                        dir = oldPos.Clone();
                        dir.SetZ(dir.GetZ() + GeoMap.COLLISION_CHECK_Z_OFFSET);
                    }
                    float limit = pos.Distance(dir);
                    dir.SubtractLocal(pos).NormalizeLocal();
                    Ray r = new Ray(pos, dir);
                    r.SetLimit(limit);
                    CollisionResults results = new CollisionResults(intentions, creature.GetInstanceId(), true);
                    geometry.CollideWith(r, results);
                    OnMoved(results);
                    oldPos = pos;
                }
                finally
                {
                    isRunning.Set(false);
                }
                return ValueTask.CompletedTask;
            }, TimeSpan.Zero);
        }
    }

    public abstract void OnMoved(CollisionResults result);

    public enum CheckType
    {
        TOUCH,
        PASS
    }
}
