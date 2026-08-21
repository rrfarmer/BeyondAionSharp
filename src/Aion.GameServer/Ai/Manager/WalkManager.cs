using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Ai;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.GeoEngine.Collision;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Walker;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Geo;

namespace Aion.GameServer.Ai.Manager;

/// <summary>
/// Java parity: ai/manager/WalkManager (ATracer). Random/route/forced NPC walking. Anonymous Runnables -> inline async ThreadPool
/// lambdas (Schedule(ct => {...; return ValueTask.CompletedTask;}, TimeSpan)); AISubState.WALK_PATH/WALK_RANDOM/WALK_WAIT_GROUP/TALK ->
/// PascalCase; AIState.WALKING/FORCED_WALKING/IDLE -> Walking/ForcedWalking/Idle; Vector3f.x -> .X. Geo/Config/RouteStep red-tolerated.
/// </summary>
public class WalkManager
{
    public static byte RANDOM_WALK_GEO_FLAGS = (byte)(CollisionIntention.CANT_SEE_COLLISIONS.GetId() | CollisionIntention.WALK.GetId() | CollisionIntention.PHYSICAL_SEE_THROUGH.GetId());

    /// <summary>True if the npc started walking; false if disabled, unsupported, or already walking.</summary>
    public static bool StartWalking(NpcAI npcAI)
    {
        if (!AIConfig.ACTIVE_NPC_MOVEMENT || !npcAI.GetOwner().IsSpawned())
            return false;
        return StartRandomWalking(npcAI) || StartRouteWalking(npcAI);
    }

    private static bool StartRandomWalking(NpcAI npcAI)
    {
        if (!npcAI.GetOwner().IsRandomWalker())
            return false;
        if (!npcAI.SetStateIfNot(AIState.WALKING) || !npcAI.SetSubStateIfNot(AISubState.WALK_RANDOM))
            return false;
        EmoteManager.EmoteStartWalking(npcAI.GetOwner());
        ChooseNextRandomPoint(npcAI);
        return true;
    }

    /// <summary>
    /// Retail's <c>goto_waypoint</c>: start this npc's route at a given step rather than where it left
    /// off.
    /// </summary>
    /// <remarks>
    /// The step index is retail's own -- its <c>&lt;waypoint&gt;</c> is a position in the npc's route,
    /// not a route name, and 929 of its 1,112 uses ask for step 0. `SetWalkerTemplate` already takes a
    /// starting step, so this is the ordinary route walk with that argument filled in rather than a
    /// second way of moving.
    /// </remarks>
    public static bool StartRouteWalkingAt(NpcAI npcAI, int step)
    {
        if (!AIConfig.ACTIVE_NPC_MOVEMENT || !npcAI.GetOwner().IsSpawned())
            return false;

        Npc owner = npcAI.GetOwner();
        if (!owner.IsPathWalker())
            return false;

        string? walkerId = owner.GetSpawn()?.GetWalkerId();
        WalkerTemplate? template = owner.GetMoveController().GetWalkerTemplate()
            ?? (walkerId == null ? null : DataManager.WALKER_DATA.GetWalkerTemplate(walkerId));
        if (template == null || step < 0 || step >= template.GetRouteSteps().Count)
            return false;

        owner.GetMoveController().SetWalkerTemplate(template, step);
        if (!npcAI.SetStateIfNot(AIState.WALKING) || !npcAI.SetSubStateIfNot(AISubState.WALK_PATH))
            return false;

        owner.GetMoveController().SetRouteStep(FindNextRoutStep(owner));
        EmoteManager.EmoteStartWalking(owner);
        owner.GetMoveController().MoveToNextPoint();
        return true;
    }

    /// <summary>Starts the npc down its own route from wherever the walker template says it is.</summary>
    /// <remarks>
    /// <c>internal</c> rather than <c>private</c> so <c>PatternAi.ContinueRoute</c> can ask for the
    /// route specifically. <see cref="StartWalking"/> tries random walking first, and an npc may carry
    /// both a walker id and a random-walk range, so going through it could set a route npc wandering.
    /// </remarks>
    internal static bool StartRouteWalking(NpcAI npcAI)
    {
        Npc owner = npcAI.GetOwner();
        if (!owner.IsPathWalker())
            return false;
        if (owner.GetMoveController().GetWalkerTemplate() == null)
        {
            WalkerTemplate template = DataManager.WALKER_DATA.GetWalkerTemplate(owner.GetSpawn().GetWalkerId());
            if (template == null)
                return false;
            owner.GetMoveController().SetWalkerTemplate(template, 0);
        }
        if (!npcAI.SetStateIfNot(AIState.WALKING) || !npcAI.SetSubStateIfNot(AISubState.WALK_PATH))
            return false;
        RouteStep nextStep = FindNextRoutStep(owner);
        owner.GetMoveController().SetRouteStep(nextStep);
        EmoteManager.EmoteStartWalking(npcAI.GetOwner());
        npcAI.GetOwner().GetMoveController().MoveToNextPoint();
        return true;
    }

    public static void StartForcedWalking(NpcAI npcAI, float x, float y, float z)
    {
        npcAI.SetStateIfNot(AIState.FORCED_WALKING);
        npcAI.SetSubStateIfNot(AISubState.NONE);
        EmoteManager.EmoteStartWalking(npcAI.GetOwner());
        npcAI.GetOwner().GetMoveController().ForcedMoveToPoint(x, y, z);
    }

    protected static RouteStep FindNextRoutStep(Npc owner)
    {
        RouteStep currentStep = owner.GetMoveController().GetCurrentStep();
        RouteStep nextStep = null;
        if (currentStep.GetStepIndex() != 0)
        {
            nextStep = FindNextRouteStepAfterPause(owner, currentStep);
        }
        else
        {
            nextStep = FindClosestRouteStep(owner);
        }
        return nextStep;
    }

    protected static RouteStep FindClosestRouteStep(Npc owner)
    {
        List<RouteStep> route = owner.GetMoveController().GetWalkerTemplate().GetRouteSteps();
        RouteStep nextStep = null;
        if (owner.GetWalkerGroup() != null)
        {
            nextStep = route[owner.GetWalkerGroup().GetGroupStep()];
        }
        else
        {
            double closestDist = 0;
            float x = owner.GetX();
            float y = owner.GetY();
            float z = owner.GetZ();
            foreach (RouteStep step in route)
            {
                double stepDist = PositionUtil.GetDistance(x, y, z, step.GetX(), step.GetY(), step.GetZ());
                if (closestDist == 0 || stepDist < closestDist)
                {
                    closestDist = stepDist;
                    nextStep = step;
                }
            }
        }
        return nextStep;
    }

    protected static RouteStep FindNextRouteStepAfterPause(Npc owner, RouteStep currentStep)
    {
        if (PositionUtil.IsInRange(owner, currentStep.GetX(), currentStep.GetY(), currentStep.GetZ(), 1))
        {
            List<RouteStep> route = owner.GetMoveController().GetWalkerTemplate().GetRouteSteps();
            if (currentStep.IsLastStep())
                return route[0];
            else
                return route[currentStep.GetStepIndex() + 1];
        }
        return currentStep;
    }

    public static void TargetReached(NpcAI npcAI)
    {
        if (npcAI.IsInState(AIState.WALKING))
        {
            switch (npcAI.GetSubState())
            {
                case AISubState.WALK_PATH:
                    npcAI.GetOwner().UpdateKnownlist();
                    if (npcAI.GetOwner().GetWalkerGroup() != null)
                    {
                        npcAI.GetOwner().GetWalkerGroup().TargetReached(npcAI);
                    }
                    else
                    {
                        ChooseNextRouteStep(npcAI);
                    }
                    break;
                case AISubState.WALK_WAIT_GROUP:
                    npcAI.SetSubStateIfNot(AISubState.WALK_PATH);
                    ChooseNextRouteStep(npcAI);
                    break;
                case AISubState.WALK_RANDOM:
                    ChooseNextRandomPoint(npcAI);
                    break;
                case AISubState.TALK:
                    npcAI.SetStateIfNot(AIState.IDLE);
                    npcAI.GetOwner().GetMoveController().AbortMove();
                    break;
            }
        }
        else if (npcAI.IsInState(AIState.FORCED_WALKING))
        {
            npcAI.GetOwner().GetMoveController().AbortMove();
            npcAI.SetStateIfNot(AIState.IDLE);
            npcAI.Think();
        }
    }

    protected static void ChooseNextRouteStep(NpcAI npcAI)
    {
        int walkPause = npcAI.GetOwner().GetMoveController().GetCurrentStep().GetRestTime();
        if (walkPause == 0)
        {
            npcAI.GetOwner().GetMoveController().ResetMove();
            if (npcAI.GetOwner().GetMoveController().IsNextRouteStepChosen())
                npcAI.GetOwner().GetMoveController().MoveToNextPoint();
        }
        else
        {
            npcAI.GetOwner().GetMoveController().AbortMove();
            if (npcAI.GetOwner().GetMoveController().IsNextRouteStepChosen())
            {
                ThreadPoolManager.GetInstance().Schedule(ct =>
                {
                    if (npcAI.IsInState(AIState.WALKING))
                    {
                        npcAI.GetOwner().GetMoveController().MoveToNextPoint();
                    }
                    return ValueTask.CompletedTask;
                }, TimeSpan.FromMilliseconds(walkPause));
            }
        }
    }

    private static void ChooseNextRandomPoint(NpcAI npcAI)
    {
        Npc owner = npcAI.GetOwner();
        owner.GetMoveController().AbortMove();

        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            if (!npcAI.IsInState(AIState.WALKING))
                return ValueTask.CompletedTask;
            int randomWalkRange = owner.GetSpawn().GetRandomWalkRange();
            int diameter = randomWalkRange * 2;
            float nextX = Rnd.NextFloat(diameter) - randomWalkRange + owner.GetSpawn().GetX();
            float nextY = Rnd.NextFloat(diameter) - randomWalkRange + owner.GetSpawn().GetY();
            if (GeoDataConfig.GEO_ENABLE && GeoDataConfig.GEO_NPC_MOVE)
            {
                Vector3f loc = GeoService.GetInstance().GetClosestCollision(owner, nextX, nextY, owner.GetZ(), true, (sbyte)RANDOM_WALK_GEO_FLAGS, IgnoreProperties.Of(owner.GetRace()));
                owner.GetMoveController().MoveToPoint(loc.X, loc.Y, loc.Z);
            }
            else
            {
                owner.GetMoveController().MoveToPoint(nextX, nextY, owner.GetZ());
            }
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(Rnd.Get(AIConfig.MINIMIMUM_DELAY, AIConfig.MAXIMUM_DELAY) * 1000));
    }

    public static void StopWalking(NpcAI npcAI)
    {
        npcAI.GetOwner().GetMoveController().AbortMove();
        npcAI.SetStateIfNot(AIState.IDLE);
        if (npcAI.GetSubState() != AISubState.FREEZE)
            npcAI.SetSubStateIfNot(AISubState.NONE);
        EmoteManager.EmoteStopWalking(npcAI.GetOwner());
    }

    public static bool IsArrivedAtPoint(NpcAI npcAI)
    {
        return npcAI.GetOwner().GetMoveController().IsReachedPoint();
    }
}
