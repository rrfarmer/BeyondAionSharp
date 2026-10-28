using Aion.GameServer.Ai.Lifecycle;
using Plan = Aion.GameServer.Ai.Lifecycle.RetailNpcReturnStatePlan;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcReturnStatePlanTests
{
    private static Plan.Observation Input() => new(
        RetailNpcRegenerationPlan.NpcBinarySha256, "synthetic branch fixture, not native capture",
        0, true, false, new(0, 0, 0), new(10, 0, 0), false, 0, 1, 100, 0, 37,
        null, new(0, "original movement observation fixture"), true, 320050000);

    private static Plan.ActionKind[] Kinds(Plan.Result result) => result.Actions.Select(x => x.Kind).ToArray();

    [Theory]
    [InlineData(4)]
    [InlineData(12)]
    [InlineData(14)]
    [InlineData(int.MaxValue)]
    public void OtherEventsAreIgnoredBeforeReadingLateInputs(int code)
    {
        var result = Plan.Replay(Input() with { RawEventCode = code, NativeHomePredicate = null, Move = null });
        Assert.Equal(Plan.Outcome.IgnoredEventOrMissingState, result.Disposition);
        Assert.Empty(result.Actions);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(13)]
    public void SignedEventAdmissionPreservesTheOriginalComparison(int code) =>
        Assert.Equal(Plan.Outcome.MovementContinues, Plan.Replay(Input() with { RawEventCode = code }).Disposition);

    [Fact]
    public void MissingStateAndVirtual14HaveDifferentOutcomes()
    {
        Assert.Empty(Plan.Replay(Input() with { HasReturnState = false, Move = null }).Actions);
        var result = Plan.Replay(Input() with { NativeVirtual14 = true, NativeHomePredicate = null, Move = null });
        Assert.Equal(Plan.Outcome.PopOnly, result.Disposition);
        Assert.Equal([Plan.ActionKind.PopState], Kinds(result));
    }

    [Fact]
    public void ExactGoalCompletesBeforeHomePredicateAndClock()
    {
        var input = Input();
        var result = Plan.Replay(input with
        {
            Position = input.ReturnGoal, NativeHomePredicate = null, CapturedTick = uint.MaxValue,
            StartTick = 0, Move = null
        });
        Assert.Equal(Plan.Outcome.Arrived, result.Disposition);
        Assert.Equal([Plan.ActionKind.ChangeDirection, Plan.ActionKind.PopState,
            Plan.ActionKind.ReevaluateAfterReturn], Kinds(result));
        Assert.Equal(37u, result.Actions[0].Value);
        Assert.Empty(result.ConsumedCallEvidence);
    }

    [Fact]
    public void NativeHomePredicateMayCompleteAwayFromTheReturnCoordinate() =>
        Assert.Equal(Plan.Outcome.Arrived, Plan.Replay(Input() with { NativeHomePredicate = true, Move = null }).Disposition);

    [Fact]
    public void HomeResultCannotBeInferredFromDistanceOrVisibility() =>
        Assert.Throws<ArgumentException>(() => Plan.Replay(Input() with { NativeHomePredicate = null }));

    [Theory]
    [InlineData(0, 60001u, false)]
    [InlineData(0, 60002u, true)]
    [InlineData(1, 30001u, false)]
    [InlineData(1, 30002u, true)]
    [InlineData(-1, 30002u, true)]
    public void DistinctTemplateBranchesHaveStrictElapsedBoundaries(int policy, uint now, bool timedOut)
    {
        var result = Plan.Replay(Input() with { Template16dc = policy, CapturedTick = now });
        Assert.Equal(timedOut ? policy == 0 ? Plan.Outcome.RemovalRequested : Plan.Outcome.TeleportRequested
            : Plan.Outcome.MovementContinues, result.Disposition);
        if (timedOut)
            Assert.DoesNotContain(Plan.ActionKind.MoveToGoal, Kinds(result));
    }

    [Theory]
    [InlineData(0, 59999u, false)]
    [InlineData(0, 60000u, true)]
    [InlineData(1, 29999u, false)]
    [InlineData(1, 30000u, true)]
    public void TimeoutSubtractionWrapsAsAnUnsignedTick(int policy, uint now, bool timeout)
    {
        var result = Plan.Replay(Input() with { Template16dc = policy, CapturedTick = now, StartTick = uint.MaxValue });
        Assert.Equal(timeout ? policy == 0 ? Plan.Outcome.RemovalRequested : Plan.Outcome.TeleportRequested
            : Plan.Outcome.MovementContinues, result.Disposition);
    }

    [Fact]
    public void FirstEntryRequiresTheOriginalMessageAndUsesItsLateClockObservation()
    {
        var result = Plan.Replay(Input() with
        {
            StartTick = 0, CapturedTick = 10, MovementEndTick = 0,
            AfterMessage = new(0, 11, "after original OnMessage25"), Move = null
        });
        Assert.Equal(Plan.Outcome.WaitingForMovementClock, result.Disposition);
        Assert.Equal([Plan.ActionKind.GotoSpawnMessage25, Plan.ActionKind.StoreStartTick], Kinds(result));
        Assert.Equal(10u, result.Actions[1].Value);
        Assert.Equal(new[] { "after original OnMessage25" }, result.ConsumedCallEvidence.ToArray());
    }

    [Fact]
    public void MessagePolicyMinusOneTeleportsWithoutStoringStartOrMoving()
    {
        var input = Input() with { StartTick = 0, AfterMessage = new(-1, uint.MaxValue, "after message"), Move = null };
        var result = Plan.Replay(input);
        Assert.Equal([Plan.ActionKind.GotoSpawnMessage25, Plan.ActionKind.Teleport], Kinds(result));
        Assert.Equal(input.ReturnGoal, result.Actions[1].Destination);
        Assert.Equal(320050000u, result.Actions[1].Value);
        Assert.Equal(input.SpawnHeading, result.Actions[1].Heading);
    }

    [Fact]
    public void ExistingStartDoesNotReplayTheMessageOrReadItsPolicy()
    {
        var result = Plan.Replay(Input() with { AfterMessage = new(-1, uint.MaxValue, "unused response") });
        Assert.Equal([Plan.ActionKind.MoveToGoal], Kinds(result));
        Assert.DoesNotContain("unused response", result.ConsumedCallEvidence);
    }

    [Fact]
    public void TickZeroIsStoredAsZeroAndWillStillBeAnUnstartedTimestampNextTime()
    {
        var result = Plan.Replay(Input() with { StartTick = 0, CapturedTick = 0, AfterMessage = new(0, 0, "after message") });
        Assert.Equal(0u, result.Actions.Single(x => x.Kind == Plan.ActionKind.StoreStartTick).Value);
        Assert.Equal(Plan.Outcome.MovementContinues, result.Disposition);
    }

    [Fact]
    public void MovementClockUsesUnsignedAbsoluteComparisonRatherThanElapsedDeadline()
    {
        Assert.Equal(Plan.Outcome.WaitingForMovementClock,
            Plan.Replay(Input() with { CapturedTick = 0, StartTick = uint.MaxValue, MovementEndTick = uint.MaxValue }).Disposition);
        Assert.Equal(Plan.Outcome.MovementContinues,
            Plan.Replay(Input() with { CapturedTick = uint.MaxValue, StartTick = uint.MaxValue, MovementEndTick = 0 }).Disposition);
    }

    [Fact]
    public void NativeMovementResultTwoCompletesInOrderAfterTheMove()
    {
        var result = Plan.Replay(Input() with { Move = new(2, "original MoveToGoal2") });
        Assert.Equal(Plan.Outcome.Arrived, result.Disposition);
        Assert.Equal([Plan.ActionKind.MoveToGoal, Plan.ActionKind.ChangeDirection,
            Plan.ActionKind.PopState, Plan.ActionKind.ReevaluateAfterReturn], Kinds(result));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(-1)]
    public void OtherMovementResultsTakeTheOriginalTeleportBranch(int resultCode)
    {
        var result = Plan.Replay(Input() with { Move = new(resultCode, "original nonzero result") });
        Assert.Equal(Plan.Outcome.TeleportRequested, result.Disposition);
        Assert.Equal([Plan.ActionKind.MoveToGoal, Plan.ActionKind.Teleport], Kinds(result));
    }

    [Fact]
    public void TeleportWithoutWorldHasNoTeleportOrPopCommand()
    {
        var result = Plan.Replay(Input() with { Move = new(1, "original failure"), WorldPresentAtTeleport = false, WorldIdAtTeleport = null });
        Assert.Equal(Plan.Outcome.TeleportSkippedMissingWorld, result.Disposition);
        Assert.Equal([Plan.ActionKind.MoveToGoal], Kinds(result));
    }

    [Fact]
    public void MissingConsumedResponsesAndProvenanceRemainVisible()
    {
        Assert.Throws<ArgumentException>(() => Plan.Replay(Input() with { StartTick = 0 }));
        Assert.Throws<ArgumentException>(() => Plan.Replay(Input() with { Move = null }));
        Assert.Throws<ArgumentException>(() => Plan.Replay(Input() with { Move = new(0, " ") }));
        Assert.Throws<ArgumentException>(() => Plan.Replay(Input() with { StartTick = 0, AfterMessage = new(0, 0, "") }));
        Assert.Throws<ArgumentException>(() => Plan.Replay(Input() with { Evidence = "" }));
        Assert.Throws<ArgumentException>(() => Plan.Replay(Input() with { SourceNpcSha256 = "unknown" }));
        Assert.Throws<ArgumentException>(() => Plan.Replay(Input() with { Move = new(1, "failure"), WorldPresentAtTeleport = null }));
        Assert.Throws<ArgumentException>(() => Plan.Replay(Input() with { Move = new(1, "failure"), WorldIdAtTeleport = null }));
    }

    [Fact]
    public void PositionComparisonHasStrictComponentBoundariesAndNoRadiusTest()
    {
        var zero = new Plan.Point(0, 0, 0);
        float epsilon = 0.0001f;
        Assert.False(Plan.SamePoint(new(epsilon, 0, 0), zero));
        Assert.False(Plan.SamePoint(new(-epsilon, 0, 0), zero));
        Assert.True(Plan.SamePoint(new(float.BitDecrement(epsilon), float.BitDecrement(epsilon), float.BitDecrement(epsilon)), zero));
        Assert.False(Plan.SamePoint(new(0, epsilon, 0), zero));
        Assert.False(Plan.SamePoint(new(0, 0, epsilon), zero));
        Assert.Throws<ArgumentException>(() => Plan.SamePoint(new(float.NaN, 0, 0), zero));
        Assert.Throws<ArgumentException>(() => Plan.SamePoint(new(0, float.PositiveInfinity, 0), zero));
    }
}
