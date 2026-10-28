using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
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
using Aion.GameServer.GeoEngine.RetailPath;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Controllers.Attack.RetailNpc;

namespace Aion.GameServer.Controllers.Movement;

/// <summary>Java parity: controllers/movement/NpcMoveController (ATracer) : CreatureMoveController&lt;Npc&gt;. NPC movement/geo-pathing. Matches the movement subsystem's PascalCase convention: inherited Started(AtomicBoolean.CompareAndSet/Get/Set), Owner, Heading, MovementMaskField, TargetDestX/Y/Z, LastMoveUpdate, MOVE_CHECK_OFFSET; SM_MOVE→SmMove; MovementMask.*→PascalCase consts; AILogger.moveinfo→Moveinfo. instanceof Creature→is; LinkedList getLast/removeLast→Last.Value+RemoveLast; synchronized→lock(this); Math.toRadians→*Math.PI/180; currentTimeMillis→SystemClock.CurrentMillis; final override→sealed override. AI/WalkManager/WalkerGroup(green)/GeoService red-tolerated.</summary>
public class NpcMoveController : CreatureMoveController<Npc>
{
    private static readonly ILogger log = AionLog.For(nameof(NpcMoveController));
    private const float MOVE_OFFSET = 0.05f;
    private const int MAX_GEO_POINT_DISTANCE = 5;

    private Destination destination = Destination.TARGET_OBJECT;

    private float pointX;
    private float pointY;
    private float pointZ;
    private bool nextPointFromGeo;
    private bool isStop;

    private List<Point3D> lastSteps;

    private WalkerTemplate walkerTemplate;
    private RouteStep currentStep;
    private SkyTempleNpcNavigation? retailNavigation;
    private SkyTempleMoveKind? retailLastMove;
    private string? retailLastDiagnostic;

    internal SkyTempleNpcNavigation RetailNavigation => retailNavigation ??= new(Owner);
    internal bool UsesRetailNavigation => SkyTempleNpcNavigation.Enabled(Owner);
    // Snapshot before acquiring the skill-state gate; navigation itself queries
    // selected skills before its own lock, so reversing that order is unsafe.
    internal bool RetailHasRouteFailure
    { get { var nav=RetailNavigation;lock(nav)return nav.HasRouteFailure; } }
    internal SkyTempleArrival RetailArrival(Creature target,float? range=null)
    {lock(RetailNavigation)return RetailNavigation.Arrival(target,range);}
    internal bool RetailDestinationReached()
    {
        // Return completion is owned by the bound ReturnState's actual-position
        // or computed native2 command, never Java MoveArrived/birth heuristics.
        if(Owner.GetAi().GetState()==AIState.RETURNING)return false;
        if(Owner.GetAi().GetState()!=AIState.FIGHT)return retailLastMove==SkyTempleMoveKind.Arrived;
        // Skill-state owns source/count/reuse/queue selection. Query before taking the
        // navigation lock: the selector itself may call RetailArrival under its state lock.
        var target=Owner.GetTarget();
        if(!SkyTempleNpcSkillState.TryGetApproachGoal(Owner,out var goal)||!ReferenceEquals(target,Owner.GetTarget()))return false;
        lock(RetailNavigation)return RetailNavigation.CurrentGoal(goal)&&
            RetailNavigation.Arrival(goal.Target,goal.Range)==SkyTempleArrival.Arrived&&ReferenceEquals(target,Owner.GetTarget());
    }

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

    public bool MoveToPoint(float x, float y, float z)
    {
        bool startedMoving = Started.CompareAndSet(false, true);
        if (!startedMoving && destination != Destination.POINT)
            return false;
        if (Owner.GetAi().IsLogging())
        {
            AILogger.Moveinfo(Owner, "MC: moveToPoint (startedMoving=" + startedMoving + ")");
        }
        destination = Destination.POINT;
        pointX = x;
        pointY = y;
        pointZ = z;
        UpdateLastMove();
        if (startedMoving)
            Owner.GetController().OnStartMove();
        return true;
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
        if(UsesRetailNavigation&&AttackManager.UpdateRetailChase((NpcAI)Owner.GetAi()))return;
        SkyTempleNpcReturnState.Frame? returnFrame=null;
        if(UsesRetailNavigation&&Owner.GetAi().GetState()==AIState.RETURNING)
        {
            if(!SkyTempleNpcReturnState.TryRead(Owner,out var currentReturn))
            {
                var nav=RetailNavigation;lock(nav)LogRetailTransition(nav,"return-unsupported",Owner.GetTarget(),
                    SkyTempleNpcNavigation.Position(Owner),"missing-or-stale-return-goal-owner");return;
            }
            returnFrame=currentReturn;
            switch(currentReturn.Tick(Owner,SystemClock.RetailTick32()))
            {
                case SkyTempleNpcReturnState.Control.Complete:
                    ReturningEventHandler.CompleteRetailReturn((NpcAI)Owner.GetAi(),currentReturn);return;
                case SkyTempleNpcReturnState.Control.RemoveReason16:
                    currentReturn.Remove(Owner);return;
                case SkyTempleNpcReturnState.Control.Unsupported:return;
            }
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
            // The retail adapter emits only a computed, revalidated route step. A resumed
            // movement task must not first broadcast the previous unchecked destination.
            if(!UsesRetailNavigation)SetAndSendStartMove(Owner);
        }

        if(UsesRetailNavigation)
        {
            MoveRetail(returnFrame);
            UpdateLastMove();
            return;
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
        return !creature.IsFlying() && !creature.GetMoveController().IsJumping() && (creature.GetMoveController().GetMovementMask() & MovementMask.FALL) == 0;
    }

    private void MoveRetail(SkyTempleNpcReturnState.Frame? returnFrame)
    {
        var nav=RetailNavigation;
        var ownerTarget=Owner.GetTarget();
        // Skill-state may acquire navigation during action selection. Its recovery
        // read must precede nav, just like the selected approach read below.
        int recovery=returnFrame is null?SkyTempleNpcSkillState.GetNextMovementInterval(Owner):
            returnFrame.CapturedTick<SkyTempleNpcSkillState.GetReturnMovementEndTick(Owner)?1:0;
        SkyTempleNpcApproachGoal? goal=null;
        bool requiresApproach=destination==Destination.TARGET_OBJECT&&Owner.GetAi().GetState()==AIState.FIGHT;
        if(requiresApproach)
        {
            if(SkyTempleNpcSkillState.TryGetApproachGoal(Owner,out var selected))goal=selected;
        }
        SkyTempleGiveUpLease? giveUp=null;
        SkyTempleMoveStep? returnAction=null;
        lock(nav)
        {
            if(recovery>0)
            {
                nav.RecoveryWait(SystemClock.RetailTick32());retailLastMove=SkyTempleMoveKind.Wait;
                LogRetailTransition(nav,"skill-recovery",ownerTarget,goal?.Target is { } recoveringTarget?
                    SkyTempleNpcNavigation.Position(recoveringTarget):new(pointX,pointY,pointZ),"movement72c");
                if(IsInMove())SetAndSendStopMove(Owner);
                return; // Keep the single existing task/route; no recovery timer or failed chase.
            }
            var step=nav.Tick(destination==Destination.TARGET_OBJECT,returnFrame?.Goal??new(pointX,pointY,pointZ),SystemClock.RetailTick32(),goal,ownerTarget,requiresApproach);
            retailLastMove=step.Kind;
            LogRetailTransition(nav,step.Kind==SkyTempleMoveKind.Move?"route-acquired":
                step.Kind==SkyTempleMoveKind.GiveUp?"give-up-pending":step.Kind.ToString(),
                ownerTarget,nav.RequestedGoal??step.Destination,step.Detail);
            if(returnFrame is not null&&step.Kind is SkyTempleMoveKind.ReturnTeleport or SkyTempleMoveKind.ReturnCompleted)
            {
                returnAction=step;
            }
            else if(step.Kind==SkyTempleMoveKind.GiveUp)
            {
                giveUp=nav.ClaimGiveUp(step);
            }
            else if(step.Kind!=SkyTempleMoveKind.Move)
            {
                if(IsInMove())SetAndSendStopMove(Owner);
                return;
            }
            else
            {
                if(!nav.CanApply(step))
                {
                    nav.Confirm(step,false);retailLastMove=SkyTempleMoveKind.Blocked;
                    LogRetailTransition(nav,"blocked-recovery",ownerTarget,step.Destination,"stale-step-or-scene");
                    if(IsInMove())SetAndSendStopMove(Owner);return;
                }
                int instance=Owner.GetInstanceId();
                TargetDestX=step.Destination.X;TargetDestY=step.Destination.Y;TargetDestZ=step.Destination.Z;
                Heading=step.Heading;
                Aion.GameServer.World.World.GetInstance().UpdatePosition(Owner,step.Next.X,step.Next.Y,step.Next.Z,Heading,false);
                // World.UpdatePosition is void and can decline invalid membership. Only its actual
                // same-lifetime position write acknowledges the follower's progress exchange.
                bool applied=Owner.IsSpawned()&&!Owner.IsDead()&&Owner.GetInstanceId()==instance&&
                    SkyTempleNpcNavigation.Position(Owner)==step.Next;
                nav.Confirm(step,applied);
                if(!applied)
                {
                    retailLastMove=SkyTempleMoveKind.Blocked;
                    LogRetailTransition(nav,"blocked-recovery",ownerTarget,step.Destination,"world-position-declined");return;
                }
                SetInMove(true);MovementMaskField=MovementMask.NPC_STARTMOVE;
                PacketSendUtility.BroadcastToSightedPlayers(Owner,new SM_MOVE(Owner.GetObjectId(),Owner.GetX(),Owner.GetY(),Owner.GetZ(),
                    Heading,MovementMaskField,TargetDestX,TargetDestY,TargetDestZ));
            }
        }
        if(returnAction is not null&&returnFrame is not null)ApplyRetailReturn(returnFrame,returnAction);
        if(giveUp is not null)DispatchRetailGiveUp(nav,giveUp);
    }

    private void ApplyRetailReturn(SkyTempleNpcReturnState.Frame frame,SkyTempleMoveStep step)
    {
        bool completed=false;
        lock(Owner)lock(this)
        {
            if(Owner.GetAi().GetState()!=AIState.RETURNING||!frame.IsCurrent(Owner))return;
            var nav=RetailNavigation;
            lock(nav)
            {
                if(!nav.CanApply(step))return;
                nav.Cancel();
            }
            if(step.Kind==SkyTempleMoveKind.ReturnCompleted)
            {
                // Native raw2 pops/reevaluates at the ACTUAL position. A clipped
                // path ending does not establish position==goal or grant HP.
                completed=true;
            }
            else if(step.Kind==SkyTempleMoveKind.ReturnTeleport)
            {
            // Host4.8 transport/membership adapter, not a native packet claim.
            // This request is reached only from a computed native follow1/4;
            // search/unsupported/defensive failures never manufacture raw1.
            var goal=frame.Goal;
            Aion.GameServer.World.World.GetInstance().UpdatePosition(Owner,goal.X,goal.Y,goal.Z,frame.Heading,false);
            if(!frame.IsCurrent(Owner)||!SkyTempleNpcReturnState.SamePoint(SkyTempleNpcNavigation.Position(Owner),goal))
            {retailLastMove=SkyTempleMoveKind.Blocked;return;}
            TargetDestX=goal.X;TargetDestY=goal.Y;TargetDestZ=goal.Z;Heading=frame.Heading;
            SetAndSendStopMove(Owner);retailLastMove=SkyTempleMoveKind.Wait;
            LogRetailTransition(nav,"return-teleport",Owner.GetTarget(),goal,step.Detail);
            // No pop here: next update tests actual position before expiry.
            }
        }
        if(completed)ReturningEventHandler.CompleteRetailReturn((NpcAI)Owner.GetAi(),frame);
    }

    private void DispatchRetailGiveUp(SkyTempleNpcNavigation nav,SkyTempleGiveUpLease lease)
    {
        // Match ReturnToLastStepOrSpawn's controller->nav order. In particular,
        // OnStopMove observers and TargetGiveup/Think run with NO nav lock held.
        lock(Owner)lock(this)
        {
            lock(nav)if(!nav.IsCurrentGiveUp(lease))return;
            if(Started.Get())
            {
                ResetMove(false); // ClaimGiveUp already cancelled precisely that route.
                SetAndSendStopMove(Owner);
            }
            lock(nav)
            {
                if(!nav.FinishGiveUp(lease))return;
                LogRetailTransition(nav,"give-up",lease.Target,nav.RequestedGoal??SkyTempleNpcNavigation.Position(Owner),"bounded-chase-ended");
            }
            if(lease.PointMovement)
            {
                // The bounded managed point-recovery owner must not fabricate
                // arrival or run combat hate/return events on an idle patrol.
                WalkManager.StopWalking((NpcAI)Owner.GetAi());
                return;
            }
            SkyTempleNpcReturnState.PreparePathFailure(Owner);
            Owner.GetAi().OnGeneralEvent(AiEventType.TargetGiveup);
        }
    }

    private void LogRetailTransition(SkyTempleNpcNavigation nav,string transition,VisibleObject? target,
        RetailSearchPoint goal,string? detail)
    {
        // Route identity/positions deliberately do not participate in this key:
        // moving-target replans and ordinary200ms progress must not log each tick.
        string phase=Owner.GetAi().GetState()==AIState.RETURNING?"return":"chase";
        string key=$"{phase}/{transition}/{target?.GetObjectId()}/{nav.SceneRevision}/{detail}";
        if(key==retailLastDiagnostic)return;
        retailLastDiagnostic=key;
        log.LogInformation("SkyTemple navigation {Phase}/{Transition}: npc={Npc} template={Template} target={Target} map={Map} instance={Instance} from={From} goal={Goal} waypoints={Waypoints} scene={Scene} reason={Reason}",
            phase,transition,Owner.GetObjectId(),Owner.GetNpcId(),target?.GetObjectId(),Owner.GetWorldId(),Owner.GetInstanceId(),
            SkyTempleNpcNavigation.Position(Owner),goal,nav.RemainingWaypoints,nav.SceneRevision,detail);
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
        long nowMillis = SystemClock.CurrentMillis();
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
        float futureDistPassed = currentSpeed * (SystemClock.CurrentMillis() - LastMoveUpdate) / 1000f;
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
        if (GeoDataConfig.GEO_NPC_MOVE && GeoDataConfig.MODE == GeoDataConfig.Mode.ON && Owner.GetAi().GetSubState() != AISubState.WALK_PATH
            && Owner.GetAi().GetState() != AIState.RETURNING && Owner.GetGameStats().GetNextGeoZUpdate() < SystemClock.CurrentMillis())
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
            Owner.GetGameStats().SetNextGeoZUpdate(SystemClock.CurrentMillis() + 1000);
        }
        if (Owner.GetAi().IsLogging())
        {
            AILogger.Moveinfo(Owner, "newX=" + newX + " newY=" + newY + " newZ=" + newZ + " mask=" + MovementMaskField);
        }

        Aion.GameServer.World.World.GetInstance().UpdatePosition(Owner, newX, newY, newZ, Heading, false);

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
            PacketSendUtility.BroadcastPacket(Owner, new SM_MOVE(Owner));
        }
    }

    private byte GetMoveMask(bool directionChanged)
    {
        if (directionChanged)
            return MovementMask.NPC_STARTMOVE;
        else if (Owner.GetAi().GetState() == AIState.RETURNING)
            return MovementMask.NPC_RUN_FAST;
        else if (Owner.GetAi().GetState() == AIState.FOLLOWING)
            return MovementMask.NPC_WALK_SLOW;

        byte mask = MovementMask.IMMEDIATE;
        Stat2 stat = Owner.GetGameStats().GetMovementSpeed();
        if (Owner.IsInState(CreatureState.WEAPON_EQUIPPED))
        {
            mask = stat.GetBonus() < 0 ? MovementMask.NPC_RUN_FAST : MovementMask.NPC_RUN_SLOW;
        }
        else if (Owner.IsInState(CreatureState.WALK_MODE) || Owner.IsInState(CreatureState.ACTIVE))
        {
            mask = stat.GetBonus() < 0 ? MovementMask.NPC_WALK_FAST : MovementMask.NPC_WALK_SLOW;
        }
        if (Owner.IsFlying())
            mask |= MovementMask.GLIDE;
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
    public void ResetMove()=>ResetMove(true);
    private void ResetMove(bool cancelRetail,bool notify=true)
    {
        if(cancelRetail&&retailNavigation is not null)lock(retailNavigation)retailNavigation.Cancel();
        retailLastMove=null;
        if (Owner.GetAi().IsLogging())
        {
            AILogger.Moveinfo(Owner, "MC perform stop");
        }
        if(notify)Owner.GetController().OnStopMove();
        Started.Set(false);
        TargetDestX = 0;
        TargetDestY = 0;
        TargetDestZ = 0;
        pointX = 0;
        pointY = 0;
        pointZ = 0;
        nextPointFromGeo = false;
    }

    internal void StopRetailReturnMotion(SkyTempleNpcReturnState.Frame frame)
    {
        lock(this)
        {
            if(!frame.LifeIsCurrent(Owner))return;
            ResetMove(true,false);SetInMove(false);
        }
        if(!frame.LifeIsCurrent(Owner))return;
        Owner.GetController().OnStopMove(); // No movement/nav monitor across observers.
        if(frame.LifeIsCurrent(Owner)&&!Started.Get())SetAndSendStopMove(Owner);
    }

    public WalkerTemplate GetWalkerTemplate()
    {
        return walkerTemplate;
    }

    public bool SetWalkerTemplate(WalkerTemplate walkerTemplate, int stepIndex)
    {
        if(UsesRetailNavigation)
        {
            if(SkyTempleNpcWaypointProjection.TryPrepare(Owner,walkerTemplate,out var prepared,out var failure))
                walkerTemplate=prepared!;
            else if(failure is not null)
            {
                log.LogError("RETAIL_SKYTEMPLE_WAYPOINT_REJECT npc={Npc} object={Object} route={Route} instance={Instance} reason={Reason}",
                    Owner.GetNpcId(),Owner.GetObjectId(),walkerTemplate?.GetRouteId(),Owner.GetInstanceId(),failure);
                RejectRetailWalker();
                return false;
            }
            if(stepIndex<0||stepIndex>=walkerTemplate.GetRouteSteps().Count)
            {
                log.LogError("RETAIL_SKYTEMPLE_WAYPOINT_REJECT npc={Npc} route={Route} reason=prepared-step-index-outside-route step={Step}",
                    Owner.GetNpcId(),walkerTemplate.GetRouteId(),stepIndex);
                RejectRetailWalker();
                return false;
            }
        }
        this.walkerTemplate = walkerTemplate;
        this.currentStep = walkerTemplate.GetRouteStep(stepIndex);
        return true;
    }

    private void RejectRetailWalker()
    {
        // Native waypoint preparation clears the complete invalid route. An
        // unsuccessful reassignment must not leave a previously admitted clone
        // available for subsequent patrol/return selection.
        lock(this)
        {
            walkerTemplate=null!;currentStep=null!;isStop=false;
            if(Owner.GetAi().GetState() is AIState.WALKING or AIState.FORCED_WALKING)
                WalkManager.StopWalking((NpcAI)Owner.GetAi());
        }
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

            if(!SetWalkerTemplate(DataManager.WALKER_DATA.GetWalkerTemplate(Owner.GetSpawn().GetWalkerId()), 0))return false;
            if (walkerTemplate == null)
            {
                log.LogWarning("Bad Walker Id: {OwnerGetSpawnGetWalkerId} - point: {CurrentStepGetStepIndex}", Owner.GetSpawn().GetWalkerId(), currentStep.GetStepIndex());
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
                lastSteps = new List<Point3D>(3);
            Point3D lastStep = lastSteps.Count == 0 ? null : lastSteps[^1];
            if (lastStep == null || !PositionUtil.IsInRange(lastStep.GetX(), lastStep.GetY(), lastStep.GetZ(), x, y, z, 10))
            {
                if (Owner.GetAi().IsLogging())
                {
                    AILogger.Moveinfo(Owner, "store back step: X=" + Owner.GetX() + " Y=" + Owner.GetY() + " Z=" + Owner.GetZ());
                }
                lastSteps.Add(new Point3D(x, y, z));
                if (lastSteps.Count > 10)
                    lastSteps.RemoveAt(0);
            }
        }
    }

    public void ReturnToLastStepOrSpawn()
    {
        if(UsesRetailNavigation)
        {
            lock(Owner)lock(this)
            {
                if(destination!=Destination.POINT)AbortMove();
                if(SkyTempleNpcReturnState.TryRead(Owner,out var frame))
                    MoveToPoint(frame.Goal.X,frame.Goal.Y,frame.Goal.Z);
                else log.LogError("SkyTemple return owner unavailable npc={Npc} template={Template} life={Life}",
                    Owner.GetObjectId(),Owner.GetNpcId(),Owner.GetController().RetailLifeEpoch);
            }
            return;
        }
        lock (this)
        {
            // E2E-authorized correction to Java ce54b7931 (plan §7 #61): LoseAggro clears
            // the pursuit target, but MoveToPoint refuses an active non-point movement.
            // Stop that movement before selecting the return destination, retaining back steps.
            if (destination != Destination.POINT)
                AbortMove();
            SpawnTemplate spawn = Owner.GetSpawn();
            Point3D step = null;
            if (lastSteps != null && lastSteps.Count != 0)
            {
                step = lastSteps[^1];
                lastSteps.RemoveAt(lastSteps.Count - 1);
            }
            if (step != null && lastSteps.Count != 0 && PositionUtil.IsInRange(Owner, step.GetX(), step.GetY(), step.GetZ(), 2))
            {
                step = lastSteps[^1];
                lastSteps.RemoveAt(lastSteps.Count - 1);
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
            MovementMaskField = MovementMask.IMMEDIATE;
        }
    }
}
