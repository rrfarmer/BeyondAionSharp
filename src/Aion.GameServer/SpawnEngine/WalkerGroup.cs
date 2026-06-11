using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Model.Templates.Walker;
using Aion.GameServer.Model.Templates.Zone;

namespace Aion.GameServer.SpawnEngine;

/// <summary>Java parity: spawnengine/WalkerGroup (vlog, Rolandas). Comparator.comparing(walkerIndex, nullsLast(natural)).reversed()→OrderBy with custom Comparer (negated nulls-last); IntStream.of(rows).sum()→rows.Sum(); mapToDouble.sum→Sum; Math.signum→Math.Sign; synchronized(members)→lock(members); Integer== (small cached indices)→int? value-equality (faithful runtime behavior); slf4j→ILogger. AI/Npc/Point2D/WalkManager/RouteStep red-tolerated.</summary>
public class WalkerGroup
{
    private static readonly ILogger Log = NullLoggerFactory.Instance.CreateLogger(nameof(WalkerGroup));

    private List<ClusteredNpc> members;
    private WalkerGroupType type;
    private readonly float walkerXpos;
    private readonly float walkerYpos;
    private int[] memberSteps;
    private volatile int groupStep;
    private readonly string versionId;
    private bool isSpawned;

    public WalkerGroup(List<ClusteredNpc> members)
    {
        // Comparator.comparing(getWalkerIndex, nullsLast(naturalOrder())).reversed()
        this.members = members.OrderBy(x => x, Comparer<ClusteredNpc>.Create((a, b) =>
        {
            int? ai = a.GetWalkerIndex();
            int? bi = b.GetWalkerIndex();
            int cmp;
            if (ai == null && bi == null)
                cmp = 0;
            else if (ai == null) // nulls last
                cmp = 1;
            else if (bi == null)
                cmp = -1;
            else
                cmp = ai.Value.CompareTo(bi.Value);
            return -cmp; // reversed
        })).ToList();
        memberSteps = new int[members.Count];
        walkerXpos = members[0].GetX();
        walkerYpos = members[0].GetY();
        type = members[0].GetWalkTemplate().GetType_();
        versionId = members[0].GetWalkTemplate().GetVersionId();
    }

    public void Form()
    {
        if (GetWalkType() == WalkerGroupType.SQUARE)
        {
            int[] rows = members[0].GetWalkTemplate().GetRows();
            if (rows.Sum() != members.Count)
            {
                Log.LogWarning("Invalid row sizes for walk cluster " + members[0].GetWalkTemplate().GetRouteId());
            }
            if (rows.Length == 1)
            {
                // Line formation: distance 2 meters from each other (divide by 2 and multiple by 2)
                // negative at left hand and positive at the right hand
                float bounds = (float)members.Sum(cNpc => (double)cNpc.GetNpc().GetObjectTemplate().GetBoundRadius().GetSide());
                float distance = (1 - members.Count) / 2f * (WalkerGroupShift.DISTANCE + bounds);
                Point2D origin = new Point2D(walkerXpos, walkerYpos);
                Point2D destination = new Point2D(members[0].GetWalkTemplate().GetRouteStep(1).GetX(),
                    members[0].GetWalkTemplate().GetRouteStep(1).GetY());
                for (int i = 0; i < members.Count; i++, distance += WalkerGroupShift.DISTANCE)
                {
                    WalkerGroupShift shift = new WalkerGroupShift(distance, 0);
                    Point2D loc = GetLinePoint(origin, destination, shift);
                    ClusteredNpc clusteredNpc = members[i];
                    clusteredNpc.Set(shift);
                    clusteredNpc.SetX(loc.GetX());
                    clusteredNpc.SetY(loc.GetY());
                    Npc member = clusteredNpc.GetNpc();
                    member.SetWalkerGroup(this);
                    // distance += npc.getObjectTemplate().getBoundRadius().getSide();
                }
            }
            else if (rows.Length != 0)
            {
                float[] rowDistances = new float[rows.Length - 1];
                float coronalDist = 0;
                for (int i = 0; i < rows.Length - 1; i++)
                {
                    if (rows[i] % 2 != rows[i + 1] % 2)
                        rowDistances[i] = 0.86602540378443864676372317075294f * WalkerGroupShift.DISTANCE;
                    else
                        rowDistances[i] = WalkerGroupShift.DISTANCE;
                    coronalDist -= rowDistances[i];
                }
                Point2D origin = new Point2D(walkerXpos, walkerYpos);
                Point2D destination = new Point2D(members[0].GetWalkTemplate().GetRouteStep(1).GetX(),
                    members[0].GetWalkTemplate().GetRouteStep(1).GetY());
                int index = 0;
                for (int i = 0; i < rows.Length; i++)
                {
                    float sagittalDist = (1 - rows[i]) / 2f * WalkerGroupShift.DISTANCE;
                    for (int j = 0; j < rows[i]; j++, sagittalDist += WalkerGroupShift.DISTANCE)
                    {
                        if (index > members.Count - 1)
                            break;
                        WalkerGroupShift shift = new WalkerGroupShift(sagittalDist, coronalDist);
                        Point2D loc = GetLinePoint(origin, destination, shift);
                        ClusteredNpc cnpc = members[index++];
                        cnpc.Set(shift);
                        cnpc.SetX(loc.GetX());
                        cnpc.SetY(loc.GetY());
                        cnpc.GetNpc().SetWalkerGroup(this);
                    }
                    if (i < rows.Length - 1)
                        coronalDist += rowDistances[i];
                }
            }
        }
        else if (GetWalkType() == WalkerGroupType.POINT)
        {
            Log.LogWarning("No formation specified for walk cluster " + members[0].GetWalkTemplate().GetRouteId());
        }
    }

    private float GetSidesExtra(int[] rows, int startIndex, int endIndex)
    {
        return 0;
    }

    /// <summary>
    /// Returns coordinates of NPC in 2D from the initial spawn location.
    /// origin - initial spawn location; destination - point of next move; shift - distance from origin located in lines perpendicular to destination;
    /// for SagittalShift if negative then located to the left from origin, otherwise to the right; for CoronalShift if negative then to back, otherwise to front.
    /// </summary>
    public static Point2D GetLinePoint(Point2D origin, Point2D destination, WalkerGroupShift shift)
    {
        // TODO: implement angle shift
        WalkerGroupShift dir = GetShiftSigns(origin, destination);
        Point2D result = null;
        if (origin.GetY() - destination.GetY() == 0)
        {
            return new Point2D(origin.GetX() + dir.GetCoronalShift() * shift.GetCoronalShift(),
                origin.GetY() - dir.GetSagittalShift() * shift.GetSagittalShift());
        }
        else if (origin.GetX() - destination.GetX() == 0)
        {
            return new Point2D(origin.GetX() + dir.GetCoronalShift() * shift.GetSagittalShift(),
                origin.GetY() + dir.GetCoronalShift() * shift.GetCoronalShift());
        }
        else
        {
            double slope = (origin.GetX() - destination.GetX()) / (origin.GetY() - destination.GetY());
            double dx = Math.Abs(shift.GetSagittalShift()) / Math.Sqrt(1 + slope * slope);
            if (shift.GetSagittalShift() * dir.GetCoronalShift() < 0)
                result = new Point2D((float)(origin.GetX() - dx), (float)(origin.GetY() + dx * slope));
            else
                result = new Point2D((float)(origin.GetX() + dx), (float)(origin.GetY() - dx * slope));
        }
        if (shift.GetCoronalShift() != 0)
        {
            Point2D rotatedShift = null;
            if (shift.GetSagittalShift() != 0)
            {
                rotatedShift = GetLinePoint(origin, destination,
                    new WalkerGroupShift(Math.Sign(shift.GetSagittalShift()) * Math.Abs(shift.GetCoronalShift()), 0));
            }
            else
            {
                rotatedShift = GetLinePoint(origin, destination, new WalkerGroupShift(Math.Abs(shift.GetCoronalShift()), 0));
            }

            // since it's rotated, and perpendicular, dx and dy are reciprocal when not rotated
            float dx = Math.Abs(origin.GetX() - rotatedShift.GetX());
            float dy = Math.Abs(origin.GetY() - rotatedShift.GetY());
            if (shift.GetCoronalShift() < 0)
            {
                if (dir.GetSagittalShift() < 0 && dir.GetCoronalShift() < 0)
                {
                    result = new Point2D(result.GetX() + dy, result.GetY() + dx);
                }
                else if (dir.GetSagittalShift() > 0 && dir.GetCoronalShift() > 0)
                {
                    result = new Point2D(result.GetX() - dy, result.GetY() - dx);
                }
                else if (dir.GetSagittalShift() < 0 && dir.GetCoronalShift() > 0)
                {
                    result = new Point2D(result.GetX() + dy, result.GetY() - dx);
                }
                else if (dir.GetSagittalShift() > 0 && dir.GetCoronalShift() < 0)
                {
                    result = new Point2D(result.GetX() - dy, result.GetY() + dx);
                }
            }
            else
            {
                if (dir.GetSagittalShift() < 0 && dir.GetCoronalShift() < 0)
                {
                    result = new Point2D(result.GetX() - dy, result.GetY() - dx);
                }
                else if (dir.GetSagittalShift() > 0 && dir.GetCoronalShift() > 0)
                {
                    result = new Point2D(result.GetX() + dy, result.GetY() + dx);
                }
                else if (dir.GetSagittalShift() < 0 && dir.GetCoronalShift() > 0)
                {
                    result = new Point2D(result.GetX() - dy, result.GetY() + dx);
                }
                else if (dir.GetSagittalShift() > 0 && dir.GetCoronalShift() < 0)
                {
                    result = new Point2D(result.GetX() + dy, result.GetY() - dx);
                }
            }
        }
        return result;
    }

    /*
     * Return a normalized direction vector
     */
    private static WalkerGroupShift GetShiftSigns(Point2D origin, Point2D destination)
    {
        float dx = Math.Sign(destination.GetX() - origin.GetX());
        float dy = Math.Sign(destination.GetY() - origin.GetY());
        return new WalkerGroupShift(dx, dy);
    }

    public void SetStep(Npc member, int step)
    {
        int currentStep = 0;
        for (int i = 0; i < members.Count; i++)
        {
            if (memberSteps[i] > currentStep)
                currentStep = memberSteps[i];
            if (members[i].GetNpc().Equals(member))
            {
                AILogger.Info(members[i].GetNpc().GetAi(), "Setting step to " + step);
                memberSteps[i] = step;
            }
        }
        if (step > currentStep || step == 0)
            groupStep = step;
    }

    public void TargetReached(NpcAI npcAI)
    {
        lock (members)
        {
            npcAI.SetSubStateIfNot(AISubState.WALK_WAIT_GROUP);
            bool allArrived = true;
            foreach (ClusteredNpc snpc in members)
            {
                Npc npc = snpc.GetNpc();
                allArrived &= npc.IsDead() || npc.GetAi().GetSubState() == AISubState.WALK_WAIT_GROUP;
                if (!allArrived)
                    break;
            }

            for (int i = 0; i < members.Count; i++)
            {
                ClusteredNpc snpc = members[i];
                if (!snpc.GetNpc().IsDead() && snpc.GetNpc().GetAi().GetSubState() == AISubState.WALK_WAIT_GROUP)
                {
                    if (memberSteps[i] == groupStep && !allArrived)
                        snpc.GetNpc().GetMoveController().AbortMove();
                    else
                        WalkManager.TargetReached((NpcAI)(snpc.GetNpc().GetAi()));
                }
            }
        }
    }

    public bool IsSpawned()
    {
        return isSpawned;
    }

    public void Spawn()
    {
        foreach (ClusteredNpc snpc in members)
        {
            float height = GetHeight(snpc.GetX(), snpc.GetY(), snpc.GetNpc().GetSpawn());
            snpc.Spawn(height);
        }
        isSpawned = true;
    }

    public void Respawn(Npc npc)
    {
        for (int index = 0; index < members.Count; index++)
        {
            ClusteredNpc snpc = members[index];
            if (snpc.GetNpc().GetNpcId() == npc.GetNpcId() && (npc.GetSpawn().GetWalkerIndex() == null && snpc.GetNpc().IsDead()
                || npc.GetSpawn().GetWalkerIndex() != null && npc.GetSpawn().GetWalkerIndex() == snpc.GetWalkerIndex()))
            {
                lock (members)
                {
                    memberSteps[index] = Math.Max(0, groupStep - 1);
                    RouteStep step = snpc.GetWalkTemplate().GetRouteStep(memberSteps[index]);
                    npc.GetMoveController().SetWalkerTemplate(snpc.GetWalkTemplate(), memberSteps[index]);
                    snpc.SetNpc(npc, step);
                    snpc.Spawn(step.GetZ());
                }
                break;
            }
        }
    }

    public void Despawn()
    {
        foreach (ClusteredNpc snpc in members)
        {
            snpc.Despawn();
            // reset positions
            Form();
            for (int index = 0; index < memberSteps.Length; index++)
                memberSteps[index] = 0;
            groupStep = 0;
        }
        isSpawned = false;
    }

    public ClusteredNpc GetClusterData(Npc npc)
    {
        foreach (ClusteredNpc snpc in members)
        {
            if (snpc.GetNpc().Equals(npc))
                return snpc;
        }
        return null;
    }

    private float GetHeight(float x, float y, SpawnTemplate template)
    {
        /*
         * if (GeoService.getInstance().isGeoOn()) { return GeoService.getInstance().getZ(template.getWorldId(), x, y, z, ); }
         */
        return template.GetZ();
    }

    public int GetPool()
    {
        return members.Count;
    }

    public WalkerGroupType GetWalkType()
    {
        return type;
    }

    public bool IsLinearlyPositioned(Npc npc)
    {
        if (type != WalkerGroupType.SQUARE)
            return false;
        foreach (ClusteredNpc snpc in members)
        {
            if (snpc.GetNpc().Equals(npc))
                return snpc.GetWalkTemplate().GetRows().Length == 1;
        }
        return false;
    }

    public int GetGroupStep()
    {
        return groupStep;
    }

    public string GetVersionId()
    {
        return versionId;
    }
}
