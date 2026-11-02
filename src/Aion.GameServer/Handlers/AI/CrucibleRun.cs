using System;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Geo;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Java parity: ai/instance/empyreanCrucible/CrucibleRun.
/// Lets a Crucible NPC run for a while without fighting: away from a creature, or in random directions when there is none. Afterwards the NPC
/// fights on and the given action runs.
/// </summary>
public static class CrucibleRun
{
    public static void Start(NpcAI ai, Creature? from, int millis, Action? after)
    {
        Npc npc = ai.GetOwner();
        npc.GetController().CancelCurrentSkill(null!);
        npc.GetMoveController().AbortMove();
        ai.SetStateIfNot(AIState.FEAR);
        ScheduledTask moveTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (npc.IsDead())
                return System.Threading.Tasks.ValueTask.CompletedTask;
            float angle = from != null ? PositionUtil.CalculateAngleFrom(from, npc) : Rnd.Get(0, 359);
            float distance = npc.GetGameStats().GetMovementSpeedFloat() * 2.5f;
            Vector3f point = GeoService.GetInstance().FindMovementCollision(npc, angle, distance);
            ((Aion.GameServer.Controllers.Movement.NpcMoveController)npc.GetMoveController()).MoveToPoint(point.GetX(), point.GetY(), point.GetZ());
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, TimeSpan.Zero, TimeSpan.FromMilliseconds(from != null ? 1000 : 2000));
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            moveTask.Cancel();
            if (npc.IsDead())
                return System.Threading.Tasks.ValueTask.CompletedTask;
            npc.GetMoveController().AbortMove();
            ai.SetStateIfNot(AIState.IDLE);
            ai.OnCreatureEvent(AiEventType.Attack, npc);
            after?.Invoke();
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(millis));
    }
}
