using System.Collections.Immutable;

namespace Aion.GameServer.Ai.Lifecycle;

/// <summary>
/// Inactive replay of NPC::UpdateReturnSpawnPointState (0x14011e9d0). This records
/// ordered requests; it does not execute AI messages, movement, teleport, removal,
/// a state pop, a callback or healing. Late observations must describe the actual
/// original call, rather than a guessed C# AI state or a generic failure policy.
/// </summary>
internal static class RetailNpcReturnStatePlan
{
    internal readonly record struct Point(float X, float Y, float Z);

    internal enum ActionKind
    {
        GotoSpawnMessage25,
        StoreStartTick,
        MoveToGoal,
        ChangeDirection,
        PopState,
        ReevaluateAfterReturn,
        Teleport,
        RemoveReason16
    }

    internal enum Outcome
    {
        IgnoredEventOrMissingState,
        PopOnly,
        Arrived,
        WaitingForMovementClock,
        MovementContinues,
        TeleportRequested,
        TeleportSkippedMissingWorld,
        RemovalRequested
    }

    // For MoveToGoal: param3=0, squared tolerance=0, supplied path=null,
    // param6=2, param7=0. The raw result, not an inferred LOS/range result, is used.
    internal sealed record MoveResponse(int RawResult, string Evidence);

    // Native reloads the template policy and movement end tick AFTER OnMessage.
    // The tick stored on this first attempt is still the earlier captured tick.
    internal sealed record AfterGotoSpawn(int Template16d4, uint MovementEndTick, string Evidence);

    internal sealed record Observation(
        string SourceNpcSha256,
        string Evidence,
        int RawEventCode,
        bool HasReturnState,
        bool NativeVirtual14,
        Point Position,
        Point ReturnGoal,
        bool? NativeHomePredicate,
        int Template16dc,
        uint StartTick,
        uint CapturedTick,
        uint MovementEndTick,
        uint SpawnHeading,
        AfterGotoSpawn? AfterMessage,
        MoveResponse? Move,
        bool? WorldPresentAtTeleport,
        uint? WorldIdAtTeleport);

    internal sealed record Action(ActionKind Kind, uint? Value = null, Point? Destination = null,
        uint? Heading = null);
    internal sealed record Result(Outcome Disposition, ImmutableArray<Action> Actions,
        string Evidence, ImmutableArray<string> ConsumedCallEvidence);

    /// <summary>
    /// Complete branch replay for one coherent sequence of explicitly supplied
    /// original-call observations. This is not an atomic actor snapshot: message
    /// and movement results are separate observations, required only if consumed.
    /// Neither an Arrived result nor a PopState authorizes HP restoration.
    /// </summary>
    internal static Result Replay(Observation input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!string.Equals(input.SourceNpcSha256, RetailNpcRegenerationPlan.NpcBinarySha256,
                StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Unverified NPC binary identity.", nameof(input));
        RequireEvidence(input.Evidence);
        var actions = ImmutableArray.CreateBuilder<Action>();
        var consumed = ImmutableArray.CreateBuilder<string>();

        Result Done(Outcome outcome) => new(outcome, actions.ToImmutable(), input.Evidence, consumed.ToImmutable());

        Result Arrive()
        {
            actions.Add(new(ActionKind.ChangeDirection, input.SpawnHeading));
            actions.Add(new(ActionKind.PopState));
            // 0x140128620 is a separate post-pop motion/SeeAroundAndAttack callback.
            // Do not replace it with reset/heal/re-aggro assumptions.
            actions.Add(new(ActionKind.ReevaluateAfterReturn));
            return Done(Outcome.Arrived);
        }

        Result Teleport()
        {
            bool worldPresent = input.WorldPresentAtTeleport ??
                throw new ArgumentException("Missing late world-presence observation at teleport.", nameof(input));
            if (!worldPresent)
                return Done(Outcome.TeleportSkippedMissingWorld);
            uint worldId = input.WorldIdAtTeleport ??
                throw new ArgumentException("Missing late world identity at teleport.", nameof(input));
            // Request carries the return goal, current world ID and spawn heading;
            // this does not model Teleport's state/packet/collision side effects.
            actions.Add(new(ActionKind.Teleport, worldId, input.ReturnGoal, input.SpawnHeading));
            return Done(Outcome.TeleportRequested);
        }

        // JG, not an unsigned comparison: negative raw codes reach this body too.
        if ((input.RawEventCode != 13 && input.RawEventCode > 3) || !input.HasReturnState)
            return Done(Outcome.IgnoredEventOrMissingState);
        if (input.NativeVirtual14)
        {
            actions.Add(new(ActionKind.PopState));
            return Done(Outcome.PopOnly);
        }
        if (SamePoint(input.ReturnGoal, input.Position))
            return Arrive();
        bool home = input.NativeHomePredicate ??
            throw new ArgumentException("Missing original home-predicate result.", nameof(input));
        if (home)
            return Arrive();

        uint elapsed = unchecked(input.CapturedTick - input.StartTick);
        if (input.StartTick != 0)
        {
            if (input.Template16dc == 0 && elapsed > 60_000u)
            {
                actions.Add(new(ActionKind.RemoveReason16, 0x10));
                return Done(Outcome.RemovalRequested);
            }
            if (input.Template16dc != 0 && elapsed > 30_000u)
                return Teleport();
        }

        uint movementEndTick = input.MovementEndTick;
        if (input.StartTick == 0)
        {
            actions.Add(new(ActionKind.GotoSpawnMessage25, 0x25));
            var after = input.AfterMessage ??
                throw new ArgumentException("Missing observation after original goto-spawn message.", nameof(input));
            RequireEvidence(after.Evidence);
            consumed.Add(after.Evidence);
            if (after.Template16d4 == -1)
                return Teleport();
            actions.Add(new(ActionKind.StoreStartTick, input.CapturedTick));
            movementEndTick = after.MovementEndTick;
        }

        // Original unsigned absolute comparison, deliberately not elapsed math.
        if (input.CapturedTick < movementEndTick)
            return Done(Outcome.WaitingForMovementClock);
        actions.Add(new(ActionKind.MoveToGoal, 2, input.ReturnGoal));
        var move = input.Move ??
            throw new ArgumentException("Missing original MoveToGoal result.", nameof(input));
        RequireEvidence(move.Evidence);
        consumed.Add(move.Evidence);
        if (move.RawResult == 0)
            return Done(Outcome.MovementContinues);
        return move.RawResult == 2 ? Arrive() : Teleport();
    }

    // FUN_140144620: strict component epsilon, not squared range or visibility.
    internal static bool SamePoint(Point left, Point right)
    {
        RequireFinite(left);
        RequireFinite(right);
        float x = left.X - right.X;
        float y = left.Y - right.Y;
        float z = left.Z - right.Z;
        return x < 0.0001f && x > -0.0001f && y < 0.0001f && y > -0.0001f &&
            z < 0.0001f && z > -0.0001f;
    }

    private static void RequireFinite(Point value)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z))
            throw new ArgumentException("Nonfinite native coordinates are outside the replay domain.");
    }

    private static void RequireEvidence(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Explicit source/call evidence is required.");
    }
}
