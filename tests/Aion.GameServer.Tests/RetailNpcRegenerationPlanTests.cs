using Aion.GameServer.Ai.Lifecycle;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcRegenerationPlanTests
{
    [Theory]
    [InlineData(5999u, 0u, false)]
    [InlineData(6000u, 0u, true)]
    [InlineData(0u, 0u, false)]
    [InlineData(5998u, uint.MaxValue, false)]
    [InlineData(5999u, uint.MaxValue, true)]
    public void HeartbeatUsesUnsignedElapsedStrictBoundary(uint now, uint last, bool expected) =>
        Assert.Equal(expected, RetailNpcRegenerationPlan.HasHeartbeatOpportunity(now, last));

    [Theory]
    [InlineData(false, 0, 2000)]
    [InlineData(false, 200, 2200)]
    [InlineData(true, 0, 6000)]
    [InlineData(true, 200, 6200)]
    public void RearmConsumesSuppliedDraw(bool asleep, int draw, int expected) =>
        Assert.Equal(expected, RetailNpcRegenerationPlan.TimerZeroRearmDelay(asleep, draw));

    [Theory]
    [InlineData(-1)]
    [InlineData(201)]
    public void InvalidDrawIsVisible(int draw) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => RetailNpcRegenerationPlan.TimerZeroRearmDelay(false, draw));

    [Theory]
    [InlineData(false, false, 0u, 50, 100, 10)]
    [InlineData(true, true, 0u, 50, 100, 10)]
    [InlineData(true, false, 0x80u, 50, 100, 10)]
    [InlineData(true, false, 0u, 100, 100, 10)]
    [InlineData(true, false, 0u, 0, 100, 10)]
    [InlineData(true, false, 0u, 50, 100, 0)]
    [InlineData(true, false, 0u, 50, 100, -1)]
    public void IneligibleOrNonpositiveCasesDoNotRequest(
        bool eligible, bool attackState, uint flags, int current, int maximum, int regen) =>
        Assert.Null(RetailNpcRegenerationPlan.PlanNormalHpRequest(eligible, attackState, flags, 0, current, maximum, regen));

    [Theory]
    [InlineData(0, 40, 100, 30, 30, false)]
    [InlineData(2, 40, 100, 30, 60, false)]
    [InlineData(2, 40, 100, 31, 62, true)]
    [InlineData(3, 40, 100, 31, 31, false)]
    public void RequestPreservesOriginalDeltaAndStrictOvercapRelease(
        int order, int current, int maximum, int regen, int expectedDelta, bool release)
    {
        var request = RetailNpcRegenerationPlan.PlanNormalHpRequest(true, false, 0, order, current, maximum, regen);
        Assert.NotNull(request);
        Assert.Equal(new RetailNpcHpRequest(expectedDelta, current, 3, release), request.Value);
    }

    [Fact]
    public void NativeDoubledSignedOverflowCanSuppressRequest() =>
        Assert.Null(RetailNpcRegenerationPlan.PlanNormalHpRequest(true, false, 0, 2, 1, 100, int.MaxValue));

    [Fact]
    public void NegativeRegenCanWrapPositiveOnlyAtTheNativeDoublingStage()
    {
        var wrapped = RetailNpcRegenerationPlan.PlanNormalHpRequest(true, false, 0, 2, 1, 100, -2_147_483_647);
        Assert.Equal(new RetailNpcHpRequest(2, 1, 3, false), wrapped!.Value);
        Assert.Null(RetailNpcRegenerationPlan.PlanNormalHpRequest(true, false, 0, 2, 1, 100, int.MinValue));
        Assert.Null(RetailNpcRegenerationPlan.PlanNormalHpRequest(true, false, 0, 0, 1, 100, -2_147_483_647));
    }

    [Fact]
    public void MpSuppressionFlagDoesNotSuppressHp()
    {
        Assert.NotNull(RetailNpcRegenerationPlan.PlanNormalHpRequest(true, false, 0x400, 0, 1, 100, 10));
        Assert.Null(RetailNpcRegenerationPlan.PlanNormalHpRequest(true, false, 0x480, 0, 1, 100, 10));
    }

    [Fact]
    public void RequestGuardWrapDoesNotReplaceMainWideArithmetic()
    {
        var request = RetailNpcRegenerationPlan.PlanNormalHpRequest(true, false, 0, 0,
            1_900_000_000, 2_000_000_000, 500_000_000)!.Value;
        Assert.False(request.ReleaseAttackersBeforeSend);
        Assert.Equal(500_000_000, request.OriginalDelta);
        var main = RetailNpcRegenerationPlan.PredictMainHp(0, 1_900_000_000, 2_000_000_000, request.OriginalDelta);
        Assert.Equal(2_000_000_000, main.PredictedHp);
    }

    [Theory]
    [InlineData(0, 50, 100, 20, true, 70)]
    [InlineData(0, 90, 100, 20, true, 100)]
    [InlineData(0, 10, 100, -30, true, 0)]
    [InlineData(0, 50, 100, 0, true, 50)]
    [InlineData(1, 50, 100, 20, false, 50)]
    [InlineData(0, 0, 100, 20, true, 20)]
    [InlineData(0, 110, 100, 0, true, 100)]
    [InlineData(0, 110, 100, -5, true, 100)]
    [InlineData(0, 100, 100, -5, true, 95)]
    public void MainPredictionUsesCurrentMainStateAndWideClamp(
        int state, int current, int maximum, int delta, bool sends, int expected)
    {
        var result = RetailNpcRegenerationPlan.PredictMainHp(state, current, maximum, delta);
        Assert.Equal(sends, result.SendsHpNotifications);
        Assert.Equal(expected, result.PredictedHp);
        Assert.Equal(sends ? delta : 0, result.OriginalNotificationDelta);
    }

    [Fact]
    public void MainHpMayDifferFromNpcObservationBeforeRequestArrives()
    {
        var request = RetailNpcRegenerationPlan.PlanNormalHpRequest(true, false, 0, 0, 50, 100, 20)!.Value;
        var main = RetailNpcRegenerationPlan.PredictMainHp(0, 25, 100, request.OriginalDelta);
        Assert.Equal(45, main.PredictedHp);
        Assert.Equal(45, RetailNpcRegenerationPlan.PredictNpcHpEcho(50, main.PredictedHp));
        Assert.Equal(0, RetailNpcRegenerationPlan.PredictNpcHpEcho(0, main.PredictedHp));
    }

    [Theory]
    [InlineData(1, 100, 100)]
    [InlineData(0, 100, 0)]
    [InlineData(-1, 100, 0)]
    public void EchoDoesNotResurrectPreviouslyDeadNpc(int oldHp, int payload, int expected) =>
        Assert.Equal(expected, RetailNpcRegenerationPlan.PredictNpcHpEcho(oldHp, payload));

    [Fact]
    public void UnsupportedNegativeHpInputIsVisible()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RetailNpcRegenerationPlan.PredictMainHp(0, -1, 100, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => RetailNpcRegenerationPlan.PredictMainHp(0, 1, -100, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => RetailNpcRegenerationPlan.PlanNormalHpRequest(true, false, 0, 0, -1, 100, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => RetailNpcRegenerationPlan.PlanNormalHpRequest(true, false, 0, 0, 1, -100, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => RetailNpcRegenerationPlan.PredictNpcHpEcho(1, -1));
    }
}
