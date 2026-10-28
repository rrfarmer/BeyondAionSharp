using Aion.GameServer.GeoEngine.RetailPath;
using static Aion.GameServer.GeoEngine.RetailPath.RetailNpcApproachExecution;

namespace Aion.GameServer.Ai.Lifecycle;

/// <summary>Inactive UpdateFollowState0x14011f020 replay. Returns commands; executes no scheduler, callback, movement, removal or HP mutation.</summary>
public static class RetailNpcFollowStatePlan
{
    public static RetailNpcApproachResult Replay(RetailNpcFollowStateObservation observation, RetailNpcApproachLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var run = new RetailNpcApproachExecution(observation.Initial, observation.Sources, observation.RecordedSteps, observation.SyntheticFixture, limits);
        try { run.Validate(); Execute(run, observation); return run.Result(null); }
        catch (RetailNpcApproachStop stop) { return run.Result(null, stop); }
    }

    private static void Execute(RetailNpcApproachExecution e, RetailNpcFollowStateObservation o)
    {
        if (!o.StatePresent || (o.RawEvent != 9 && o.RawEvent > 3)) return;
        bool valid = e.Frame.Actor.FieldB8 < 1 && e.Test(RetailNpcApproachOperation.ActorVirtual22, "FollowActor22") &&
            !e.Test(RetailNpcApproachOperation.ActorVirtual14, "FollowActorGuard") && e.Frame.Target is not null &&
            e.Frame.Target.FieldB8 < 1 && e.Test(RetailNpcApproachOperation.TargetVirtualB0, "FollowTargetValid") &&
            e.Frame.ActorWorld?.Identity == e.Frame.TargetWorld?.Identity;
        if (!valid) { Pop(e); return; }
        uint captured = e.Tick("FollowStart");
        var actor = e.Frame.Actor;
        var target = e.Frame.Target!;
        if ((o.StateEndTick != 0 && captured > o.StateEndTick) ||
            (Squared(target.Position, actor.Position) > 10000f && actor.MasterId == 0 && actor.Field568 == 0))
        { Pop(e); return; }
        float reach = Finite(actor.HalfBound + 2f); reach = Finite(reach * reach);
        bool needsApproach = reach < Squared(target.Position, actor.Position) || !e.Test(RetailNpcApproachOperation.TargetVectorZero, "FollowTargetVectorZero");
        if (needsApproach)
        {
            uint gate = e.Tick("FollowMovementGate");
            if (e.Frame.Actor.MovementEndTick <= gate)
            {
                var request = new RetailNpcApproachRequest(RetailNpcApproachOperation.ChangeState, "FollowMoveState", Flag: 1, Code: 0x19, Reason: 0x1b);
                e.Add(new(RetailNpcApproachCommandKind.ChangeState, "FollowMoveState", Request: request, Code: 0x19));
                e.Read(request, true); // Native return value is ignored, but its post-state must be observed before approach.
                uint result = RetailNpcGroundApproachPlan.Execute(e, reach, 0);
                if ((result & 0xfffffffdu) == 0)
                {
                    e.FailureTick(0, "FollowApproachClearFailure"); Schedule(e, 500); return;
                }
                if (result == 4) { Schedule(e, 500); return; }
                uint previous = e.TryFailureTick(captured, "FollowFailureStart");
                if (previous == 0)
                {
                    e.FailedGoal(e.Frame.Target!.Position, "FollowFailedTarget");
                    e.Add(new(RetailNpcApproachCommandKind.Diagnostic, "FollowFirstFailure"));
                    return; // Native first-entry branch has no new schedule here.
                }
                if (unchecked(previous + 30000u) < captured)
                {
                    var dismiss = new RetailNpcApproachRequest(RetailNpcApproachOperation.Dismiss, "FollowDismiss", Code: 0);
                    e.Add(new(RetailNpcApproachCommandKind.Dismiss, "FollowDismiss", Request: dismiss, Code: 0));
                    if (e.Raw(dismiss) == 0) e.Add(new(RetailNpcApproachCommandKind.Remove, "FollowDismissFailed", Code: 2));
                    return;
                }
                uint retry = unchecked(captured + 3000u);
                if (retry <= e.Frame.Actor.MovementEndTick) retry = e.Frame.Actor.MovementEndTick;
                e.MovementEnd(retry); Schedule(e, 3000); return;
            }
        }
        else
        {
            if (e.Frame.Actor.MasterId != e.Frame.Target!.Id) return;
            uint gate = e.Tick("FollowDirectionGate");
            if (e.Frame.Actor.MovementEndTick <= gate)
            {
                int heading = e.Frame.Target!.Heading;
                e.Add(new(RetailNpcApproachCommandKind.ChangeDirection, "FollowMasterDirection", Code: heading,
                    Request: new(RetailNpcApproachOperation.ChangeDirection, "ChangeDirectionVirtual260", Flag: 1, Code: heading)));
                return;
            }
        }
        Schedule(e, 500);
    }
    private static void Schedule(RetailNpcApproachExecution e, int delay) =>
        e.Add(new(RetailNpcApproachCommandKind.ScheduleEvent, "FollowSchedule", Code: 9, DelayMilliseconds: delay));
    private static void Pop(RetailNpcApproachExecution e)
    {
        e.FailureTick(0, "FollowInvalidClearFailure");
        e.FailedGoal(new(0, 0, 0), "FollowInvalidClearGoal");
        e.Add(new(RetailNpcApproachCommandKind.PopState, "FollowInvalidPopState"));
    }
}
