using Aion.GameServer.GeoEngine.RetailPath;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcPathProgressPlanTests
{
    [Theory]
    [InlineData(0, 7)] [InlineData(1, 7)] [InlineData(2, 8)] [InlineData(-1, 7)] [InlineData(int.MaxValue, 7)]
    public void ConsumeAdvancesOnlyOldTwoAndRetainsDistinctFailedCasExchange(int old, int index)
    {
        var result = RetailNpcPathProgressPlan.Consume(Context(), old, 7);
        Assert.True(result.Success); Assert.Equal(0, result.Progress); Assert.Equal(index, result.PathIndex);
        Assert.Equal(2, result.Operations.Count);
        Assert.Equal("CompareExchangeProgressExpected2Desired0", result.Operations[0].Operation);
        Assert.Equal(old, result.Operations[0].OldValue);
        Assert.Equal(old == 2 ? "IncrementLocalPathIndex" : "ExchangeProgress0", result.Operations[1].Operation);
    }
    [Theory]
    [InlineData(true, 0, 0)] [InlineData(true, 1, 2)] [InlineData(true, 2, 2)] [InlineData(true, -1, -1)]
    [InlineData(false, 0, 0)] [InlineData(false, 1, 1)] [InlineData(false, 2, 2)]
    public void OnlySuccessfulWorldMoveReachesCompareExchangeOneToTwo(bool moveSucceeded, int old, int expected)
    {
        var result = RetailNpcPathProgressPlan.Acknowledge(Context(), old, new(moveSucceeded, new('d', 64)));
        Assert.True(result.Success); Assert.Equal(expected, result.Progress); Assert.Null(result.PathIndex);
        if (moveSucceeded) Assert.Single(result.Operations); else Assert.Empty(result.Operations);
        Assert.False(result.RuntimeVerified); Assert.False(result.AppliesCommands);
    }
    [Fact]
    public void AcknowledgementThenConsumeThenDuplicateCannotAdvanceTwice()
    {
        var ack = RetailNpcPathProgressPlan.Acknowledge(Context(), 1, new(true, new('a', 64)));
        var consumed = RetailNpcPathProgressPlan.Consume(Context(), ack.Progress!.Value, 0);
        var duplicate = RetailNpcPathProgressPlan.Consume(Context(), consumed.Progress!.Value, consumed.PathIndex!.Value);
        Assert.Equal(1, duplicate.PathIndex); Assert.Equal(0, duplicate.Progress);
        Assert.Equal("ExchangeProgress0", duplicate.Operations[1].Operation);
    }
    [Theory]
    [InlineData("owner")] [InlineData("evidence")] [InlineData("binary")] [InlineData("epoch")] [InlineData("identity")]
    public void MissingOwnerOrChangedEpochYieldsNoApplicableAtomicOperations(string fault)
    {
        var context = Context();
        context = fault switch
        {
            "owner" => context with { SerializedOwnership = null }, "evidence" => context with { EvidenceSha256 = "" },
            "binary" => context with { NativeNpcSha256 = new('0', 64) },
            "epoch" => context with { ExpectedEpoch = context.Epoch with { OwnershipEpoch = "new" } },
            _ => context with { Epoch = context.Epoch with { ActorIdentity = "" }, ExpectedEpoch = context.Epoch with { ActorIdentity = "" } }
        };
        var result = RetailNpcPathProgressPlan.Consume(context, 2, 7);
        Assert.False(result.Success); Assert.Null(result.Progress); Assert.Null(result.PathIndex); Assert.Empty(result.Operations);
    }
    [Fact]
    public void UnknownMoveResultIsNotInferredFromAPacketOrTimer()
    {
        var result = RetailNpcPathProgressPlan.Acknowledge(Context(), 1, new(null, new('a', 64)));
        Assert.Equal(RetailNpcPathFollowStatus.UnsupportedProgress, result.Status); Assert.Empty(result.Operations);
    }
    [Fact]
    public void IndexWrapRejectsOnlyWhenTheIncrementIsConsumed()
    {
        Assert.Equal(RetailNpcPathFollowStatus.UnsupportedArithmetic, RetailNpcPathProgressPlan.Consume(Context(), 2, int.MaxValue).Status);
        var untouched = RetailNpcPathProgressPlan.Consume(Context(), 1, int.MaxValue);
        Assert.True(untouched.Success); Assert.Equal(int.MaxValue, untouched.PathIndex);
    }
    private static RetailNpcPathProgressContext Context()
    {
        var epoch = new RetailNpcPathFollowEpoch("actor", "actor1", "owner1", "path1", "world", "instance", "scene", "assets");
        return new(epoch, epoch, true, RetailGroundPathSearch.SupportedNpcBinarySha256, new('b', 64));
    }
}
