using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Handler;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.GeoEngine.Collision;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Geometry;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Model.Templates.Walker;
using Aion.GameServer.Model.Templates.Zone;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SpawnEngine;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Aion.GameServer.World.Geo;

namespace Aion.GameServer.Controllers.Movement;

/// <summary>Java parity: controllers/movement/NpcMoveController (ATracer) : CreatureMoveController&lt;Npc&gt;. NPC movement/geo-pathing. Matches the movement subsystem's PascalCase convention: inherited Started(AtomicBoolean.CompareAndSet/Get/Set), Owner, Heading, MovementMaskField, TargetDestX/Y/Z, LastMoveUpdate, MOVE_CHECK_OFFSET; SM_MOVE→SmMove; MovementMask.*→PascalCase consts; AILogger.moveinfo→Moveinfo. instanceof Creature→is; LinkedList getLast/removeLast→Last.Value+RemoveLast; synchronized→lock(this); Math.toRadians→*Math.PI/180; currentTimeMillis→UtcNow.ToUnixTimeMilliseconds; final override→sealed override. AI/WalkManager/WalkerGroup(green)/GeoService red-tolerated.</summary>
public class NpcMoveController : CreatureMoveController<Npc>
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(NpcMoveController));
    private const float MOVE_OFFSET = 0.05f;
    private const int MAX_GEO_POINT_DISTANCE = 5;

    private Destination destination = Destination.TARGET_OBJECT;

    private float pointX;
    private float pointY;
    private float pointZ;
    private bool nextPointFromGeo;
    private bool isStop;

    private LinkedList<Point3D> lastSteps;

    private WalkerTemplate walkerTemplate;
    private RouteStep currentStep;

    public NpcMoveController(Npc owner) : base(owner)
    {
    }

    private enum Destination
    {
        TARGET_OBJECT,
        POINT,
        FORCED_POINT
    }

    /// <summary>Move to current target</summary>
    public void MoveToTargetObject()
    {
        if (Started.CompareAndSet(false, true))
        {
            if (Owner.GetAi().IsLogging())
            {
                AILogger.Moveinfo(Owner, "MC: moveToTarget started");
            }
            destination = Destination.TARGET_OBJECT;
            UpdateLastMove();
            Owner.GetController().OnStartMove();
        }
    }

    public void MoveToPoint(float x, float y, float z)
    {
        if (Started.CompareAndSet(false, true))
        {
            if (Owner.GetAi().IsLogging())
            {
                AILogger.Moveinfo(Owner, "MC: moveToPoint started");
            }
            destination = Destination.POINT;
            pointX = x;
            pointY = y;
            pointZ = z;
            UpdateLastMove();
            Owner.GetController().OnStartMove();
        }
    }

    public void ForcedMoveToPoint(float x, float y, float z)
    {
        if (Started.CompareAndSet(false, true))
        {
            if (Owner.GetAi().IsLogging())
            {
                AILogger.Moveinfo(Owner, "MC: forcedMoveToPoint started");
            }
            destination = Destination.FORCED_POINT;
            pointX = x;
            pointY = y;
            pointZ = z;
            UpdateLastMove();
            Owner.GetController().OnStartMove();
        }
    }

    public void MoveToNextPoint()
    {
        if (Started.CompareAndSet(false, true))
        {
            if (Owner.GetAi().IsLogging())
            {
                AILogger.Moveinfo(Owner, "MC: moveToNextPoint started");
            }
            destination = Destination.POINT;
            UpdateLastMove();
            Owner.GetController().OnStartMove();
        }
    }

    public override void MoveToDestination()
    {
        if (Owner.GetAi().IsLogging())
        {
            AILogger.Moveinfo(Owner, "moveToDestination destination: " + destination);
        }
        if (Owner.IsDead())
        {
            AbortMove();
            return;
        }
        if (!Owner.CanPerformMove())
        {
            if (Owner.GetAi().IsLogging())
            {
                AILogger.Moveinfo(Owner, "moveToDestination can't perform move");
            }
            if (Started.CompareAndSet(true, false))
            {
                SetAndSendStopMove(Owner);
                UpdateLastMove();
            }
            return;
        }
        if (Started.CompareAndSet(false, true))
        {
            UpdateLastMove();
            SetAndSendStartMove(Owner);
        }

        switch (destination)
        {
            case Destination.TARGET_OBJECT:
                VisibleObject target = Owner.GetTarget(); // todo no target
                if (target == null)
                    return;
                if (!PositionUtil.IsInRange(target, pointX, pointY, pointZ, MOVE_CHECK_OFFSET))
                {
                    if (GeoDataConfig.GEO_NPC_MOVE && !Owner.IsInFlyingState() && target is Creature creature && (nextPointFromGeo || (nextPointFromGeo = !IsOnGround(creature))))
                    {
                        if (TrySetValidGeoPoint(target.GetX(), target.GetY()) && nextPointFromGeo)
                            nextPointFromGeo = !IsOnGround(creature);
                    }
                    else
                    {
                        pointX = target.GetX();
                        pointY = target.GetY();
                        pointZ = target.GetZ();
                    }
                }
                MoveToLocation(pointX, pointY, pointZ);
                break;
            case Destination.POINT:
            case Destination.FORCED_POINT:
                MoveToLocation(pointX, pointY, pointZ);
                break;
        }
        UpdateLastMove();
    }

    private bool IsOnGround(Creature creature)
    {
        return !creature.IsFlying() && !creature.GetMoveController().IsJumping() && (creature.GetMoveController().GetMovementMask() & MovementMask.Fall) == 0;
    }

    /// <summary>
    /// Sets pointX, pointY and pointZ to valid geo coordinates near or at given position. Tries to detect and stop at cliffs or steep hills.
    /// Returns true if new geo point was set, false if old one was kept (either because it's still valid or needs to be rechecked at the next interval).
    /// </summary>
    private bool TrySetValidGeoPoint(float targetX, float targetY)
    {
        if (pointX == 0 && pointY == 0 && pointZ == 0)
        {
            pointX = Owner.GetX();
            pointY = Owner.GetY();
            pointZ = Owner.GetZ();
            Owner.GetGameStats().SetNextGeoZUpdate(0);
        }
        long nowMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (nowMillis < Owner.GetGameStats().GetNextGeoZUpdate())
            return false;
        float distance2D = (float)PositionUtil.GetDistance(pointX, pointY, targetX, targetY);
        if (distance2D < MOVE_CHECK_OFFSET)
            return false; // no need to recalculate
        if (distance2D > MAX_GEO_POINT_DISTANCE)
        {
            double angleRadians = PositionUtil.CalculateAngleFrom(pointX, pointY, targetX, targetY) * Math.PI / 180;
            targetX = pointX + (float)(Math.Cos(angleRadians) * MAX_GEO_POINT_DISTANCE);
            targetY = pointY + (float)(Math.Sin(angleRadians) * MAX_GEO_POINT_DISTANCE);
            distance2D = MAX_GEO_POINT_DISTANCE;
        }
        float maxZDiff = distance2D + MOVE_CHECK_OFFSET;
        float geoZ = GeoService.GetInstance().GetZ(Owner.GetWorldId(), targetX, targetY, pointZ + maxZDiff, pointZ - maxZDiff, Owner.GetInstanceId());
        if (float.IsNaN(geoZ))
        {
            Owner.GetGameStats().SetNextGeoZUpdate(nowMillis + 1000);
            return false;
        }
        pointX = targetX;
        pointY = targetY;
        pointZ = geoZ;
        Owner.GetGameStats().SetNextGeoZUpdate(nowMillis + 500);
        return true;
    }

    private void MoveToLocation(float targetX, float targetY, float targetZ)
    {
        float ownerX = Owner.GetX();
        float ownerY = Owner.GetY();
        float ownerZ = Owner.GetZ();

        if (Owner.GetAi().IsLogging())
        {
            AILogger.Moveinfo(Owner, "OLD targetDestX: " + TargetDestX + " targetDestY: " + TargetDestY + " targetDestZ " + TargetDestZ);
        }

        // to prevent broken walkers in case of activating/deactivating zones
        if (targetX == 0 && targetY == 0)
        {
            targetX = Owner.GetSpawn().GetX();
            targetY = Owner.GetSpawn().GetY();
            targetZ = Owner.GetSpawn().GetZ();
            ClearBackSteps();
        }
        else if (Owner.GetAi().GetState() == AIState.FIGHT || Owner.GetAi().GetState() == AIState.FOLLOWING)
        {
            TryStoreStep(targetX, targetY, targetZ);
        }

        bool destinationChanged = targetX != TargetDestX || targetY != TargetDestY || targetZ != TargetDestZ;
        if (targetX != TargetDestX || targetY != TargetDestY)
            Heading = PositionUtil.GetHeadingTowards(ownerX, ownerY, targetX, targetY);

        TargetDestX = targetX;
        TargetDestY = targetY;
        TargetDestZ = targetZ;

        if (Owner.GetAi().IsLogging())
        {
            AILogger.Moveinfo(Owner, "ownerX=" + ownerX + " ownerY=" + ownerY + " ownerZ=" + ownerZ);
            AILogger.Moveinfo(Owner, "targetDestX: " + TargetDestX + " targetDestY: " + TargetDestY + " targetDestZ " + TargetDestZ);
        }

        float currentSpeed = Owner.GetGameStats().GetMovementSpeedFloat();
        float futureDistPassed = currentSpeed * (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - LastMoveUpdate) / 1000f;
        float dist = (float)PositionUtil.GetDistance(ownerX, ownerY, ownerZ, targetX, targetY, targetZ);

        if (Owner.GetAi().IsLogging())
        {
            AILogger.Moveinfo(Owner, "futureDist: " + futureDistPassed + " dist: " + dist);
        }

        if (dist == 0)
        {
            if (Owner.GetAi().GetState() == AIState.RETURNING)
            {
                if (Owner.GetAi().IsLogging())
                {
                    AILogger.Moveinfo(Owner, "State RETURNING: abort move");
                }
                TargetEventHandler.OnTargetReached((NpcAI)Owner.GetAi());
            }
            return;
        }

        if (futureDistPassed > dist)
        {
            futureDistPassed = dist;
        }

        float distFraction = futureDistPassed / dist;
        float newX = (TargetDestX - ownerX) * distFraction + ownerX;
        float newY = (TargetDestY - ownerY) * distFraction + ownerY;
        float newZ = (TargetDestZ - ownerZ) * distFraction + ownerZ;
        if (GeoDataConfig.GEO_NPC_MOVE && GeoDataConfig.GEO_ENABLE && Owner.GetAi().GetSubState() != AISubState.WALK_PATH
            && Owner.GetAi().GetState() != AIState.RETURNING && Owner.GetGameStats().GetNextGeoZUpdate() < DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
        {
            // fix Z if npc doesn't move to spawn point
            if (Owner.GetSpawn().GetX() != TargetDestX || Owner.GetSpawn().GetY() != TargetDestY || Owner.GetSpawn().GetZ() != TargetDestZ)
            {
                float geoZ = GeoService.GetInstance().GetZ(Owner.GetWorldId(), newX, newY, newZ + 2, Math.Min(newZ, ownerZ) - 2, Owner.GetInstanceId());
                if (!float.IsNaN(geoZ))
                {
                    if (Math.Abs(newZ - geoZ) > 1)
                        destinationChanged = true;
                    newZ = geoZ;
                    bool isXYDestinationReached = PositionUtil.GetDistance(newX, newY, pointX, pointY) < MOVE_OFFSET;
                    if (isXYDestinationReached && !PositionUtil.IsInRange(newX, newY, newZ, pointX, pointY, pointZ, MOVE_OFFSET))
                        pointZ = newZ; // original pointZ is unreachable, override it so isReachedPoint() can return true
                }
            }
            Owner.GetGameStats().SetNextGeoZUpdate(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 1000);
        }
        if (Owner.GetAi().IsLogging())
        {
            AILogger.Moveinfo(Owner, "newX=" + newX + " newY=" + newY + " newZ=" + newZ + " mask=" + MovementMaskField);
        }

        World.GetInstance().UpdatePosition(Owner, newX, newY, newZ, Heading, false);

        byte newMask = GetMoveMask(destinationChanged);
        if (MovementMaskField != newMask || destinationChanged)
        {
            if (MovementMaskField != newMask)
            {
                if (Owner.GetAi().IsLogging())
                {
                    AILogger.Moveinfo(Owner, "oldMask=" + MovementMaskField + " newMask=" + newMask);
                }
                MovementMaskField = newMask;
            }
            PacketSendUtility.BroadcastPacket(Owner, new SmMove(Owner));
        }
    }

    private byte GetMoveMask(bool directionChanged)
    {
        if (directionChanged)
            return MovementMask.NpcStartMove;
        else if (Owner.GetAi().GetState() == AIState.RETURNING)
            return MovementMask.NpcRunFast;
        else if (Owner.GetAi().GetState() == AIState.FOLLOWING)
            return MovementMask.NpcWalkSlow;

        byte mask = MovementMask.Immediate;
        Stat2 stat = Owner.GetGameStats().GetMovementSpeed();
        if (Owner.IsInState(CreatureState.WEAPON_EQUIPPED))
        {
            mask = stat.GetBonus() < 0 ? MovementMask.NpcRunFast : MovementMask.NpcRunSlow;
        }
        else if (Owner.IsInState(CreatureState.WALK_MODE) || Owner.IsInState(CreatureState.ACTIVE))
        {
            mask = stat.GetBonus() < 0 ? MovementMask.NpcWalkFast : MovementMask.NpcWalkSlow;
        }
        if (Owner.IsFlying())
            mask |= MovementMask.Glide;
        return mask;
    }

    public override void AbortMove()
    {
        if (!Started.Get())
            return;
        ResetMove();
        SetAndSendStopMove(Owner);
    }

    /// <summary>Initialize values to default ones</summary>
    public void ResetMove()
    {
        if (Owner.GetAi().IsLogging())
        {
            AILogger.Moveinfo(Owner, "MC perform stop");
        }
        Owner.GetController().OnStopMove();
        Started.Set(false);
        TargetDestX = 0;
        TargetDestY = 0;
        TargetDestZ = 0;
        pointX = 0;
        pointY = 0;
        pointZ = 0;
        nextPointFromGeo = false;
    }

    public WalkerTemplate GetWalkerTemplate()
    {
        return walkerTemplate;
    }

    public void SetWalkerTemplate(WalkerTemplate walkerTemplate, int stepIndex)
    {
        this.walkerTemplate = walkerTemplate;
        this.currentStep = walkerTemplate.GetRouteStep(stepIndex);
    }

    public void SetRouteStep(RouteStep step)
    {
        Point2D dest = null;
        if (Owner.GetWalkerGroup() != null)
        {
            dest = WalkerGroup.GetLinePoint(new Point2D(currentStep.GetX(), currentStep.GetY()), new Point2D(step.GetX(), step.GetY()),
                Owner.GetWalkerGroup().GetClusterData(Owner));
            this.pointZ = currentStep.GetZ();
            Owner.GetWalkerGroup().SetStep(Owner, step.GetStepIndex());
        }
        else
        {
            this.pointZ = step.GetZ();
            this.isStop = walkerTemplate.GetLoopType() == WalkerTemplate.LoopType.NONE && step.IsLastStep();
        }
        this.currentStep = step;
        this.pointX = dest == null ? step.GetX() : dest.GetX();
        this.pointY = dest == null ? step.GetY() : dest.GetY();
        this.destination = Destination.POINT;
    }

    public RouteStep GetCurrentStep()
    {
        return currentStep;
    }

    public bool IsReachedPoint()
    {
        return PositionUtil.IsInRange(Owner, pointX, pointY, pointZ, MOVE_OFFSET);
    }

    public bool IsNextRouteStepChosen()
    {
        if (isStop)
        {
            WalkManager.StopWalking((NpcAI)Owner.GetAi());
            return false;
        }
        if (walkerTemplate == null)
        {
            WalkManager.StopWalking((NpcAI)Owner.GetAi());
            if (WalkerFormator.ProcessClusteredNpc(Owner, Owner.GetWorldId(), Owner.GetInstanceId()))
                return false;

            SetWalkerTemplate(DataManager.WALKER_DATA.GetWalkerTemplate(Owner.GetSpawn().GetWalkerId()), 0);
            if (walkerTemplate == null)
            {
                log.LogWarning("Bad Walker Id: " + Owner.GetSpawn().GetWalkerId() + " - point: " + currentStep.GetStepIndex());
                return false;
            }
        }
        List<RouteStep> routeSteps = walkerTemplate.GetRouteSteps();
        RouteStep nextStep = currentStep.IsLastStep() ? routeSteps[0] : routeSteps[currentStep.GetStepIndex() + 1];
        SetRouteStep(nextStep);
        return true;
    }

    public bool IsChangingDirection()
    {
        return currentStep.GetStepIndex() == 0;
    }

    public sealed override float GetTargetX2()
    {
        return Started.Get() ? TargetDestX : Owner.GetX();
    }

    public sealed override float GetTargetY2()
    {
        return Started.Get() ? TargetDestY : Owner.GetY();
    }

    public sealed override float GetTargetZ2()
    {
        return Started.Get() ? TargetDestZ : Owner.GetZ();
    }

    public bool IsStop()
    {
        return isStop;
    }

    private void TryStoreStep(float x, float y, float z)
    {
        lock (this)
        {
            if (lastSteps == null)
                lastSteps = new LinkedList<Point3D>();
            Point3D lastStep = lastSteps.Count == 0 ? null : lastSteps.Last.Value;
            if (lastStep == null || !PositionUtil.IsInRange(lastStep.GetX(), lastStep.GetY(), lastStep.GetZ(), x, y, z, 10))
            {
                if (Owner.GetAi().IsLogging())
                {
                    AILogger.Moveinfo(Owner, "store back step: X=" + Owner.GetX() + " Y=" + Owner.GetY() + " Z=" + Owner.GetZ());
                }
                lastSteps.AddLast(new Point3D(x, y, z));
                if (lastSteps.Count > 10)
                    lastSteps.RemoveFirst();
            }
        }
    }

    public void ReturnToLastStepOrSpawn()
    {
        lock (this)
        {
            SpawnTemplate spawn = Owner.GetSpawn();
            Point3D step = null;
            if (lastSteps != null && lastSteps.Count != 0)
            {
                step = lastSteps.Last.Value;
                lastSteps.RemoveLast();
            }
            if (step != null && lastSteps.Count != 0 && PositionUtil.IsInRange(Owner, step.GetX(), step.GetY(), step.GetZ(), 2))
            {
                step = lastSteps.Last.Value;
                lastSteps.RemoveLast();
            }
            if (step == null || GeoService.GetInstance().CanSee(Owner, spawn.GetX(), spawn.GetY(), spawn.GetZ(), IgnoreProperties.ANY_RACE))
            {
                TargetDestX = spawn.GetX();
                TargetDestY = spawn.GetY();
                TargetDestZ = spawn.GetZ();
                if (Owner.GetAi().IsLogging())
                    AILogger.Moveinfo(Owner, "recall back step: spawn point");
            }
            else
            {
                TargetDestX = step.GetX();
                TargetDestY = step.GetY();
                TargetDestZ = step.GetZ();
                if (Owner.GetAi().IsLogging())
                    AILogger.Moveinfo(Owner, "recall back step: X=" + step.GetX() + " Y=" + step.GetY() + " Z=" + step.GetZ());
            }
            MoveToPoint(TargetDestX, TargetDestY, TargetDestZ);
        }
    }

    public void ClearBackSteps()
    {
        lock (this)
        {
            lastSteps = null;
            MovementMaskField = MovementMask.Immediate;
        }
    }
}
