using Aion.GameServer.Controllers.Attack.RetailNpc;
using Pool = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcMinimumReachPool;

namespace Aion.GameServer.Tests;

/// <summary>
/// Independent hand/instruction-derived branch and binary32 vectors. These are not native
/// oracle captures, RNG tests, final-action selection tests, or proof of live routing.
/// </summary>
public sealed class RetailNpcMinimumReachPoolTests
{
    private static RetailNpcNormalScope Scope() => RetailNpcNormalScope.Create(
        RetailNpcNormalScope.MainBinarySha256, RetailNpcNormalActorKind.OrdinaryOwnerlessNpc,
        RetailNpcNormalActorKind.Player, 212835, 0);

    private static Pool.EligibleAttack Attack(int index, float range, int weight) =>
        new(index, range, weight, $"static-source:combined-entry:{index}");

    private static Pool Prepare(Pool.EffectiveGeometry geometry, params Pool.EligibleAttack[] attacks) =>
        Pool.Prepare(Scope(), Pool.NpcServerBinarySha256, "static-fixture:not-a-native-oracle", geometry, attacks);

    private static Pool.OriginalDrawReceipt Receipt(int total, int result) =>
        new(1, total, result, total > 1, "supplied-original-wrapper:static-example");

    private static float FromBits(uint bits) => BitConverter.Int32BitsToSingle(unchecked((int)bits));
    private static void AssertBits(uint expected, float actual) =>
        Assert.Equal(expected, unchecked((uint)BitConverter.SingleToInt32Bits(actual)));

    [Fact]
    public void ShortestInRangeGroupWinsBeforeWeightsAndKeepsOriginalIndices()
    {
        // Radii 1+1 and ranges 2/32 give reaches 4/34 and squares 16/1156.
        var pool = Prepare(new(1f, 1f, 9f, false), Attack(3, 2f, 1000), Attack(12, 32f, 2000));
        var entry = Assert.Single(pool.Entries);
        Assert.Equal(3, entry.CombinedIndex);
        Assert.Equal("static-source:combined-entry:3", entry.SourceEvidence);
        AssertBits(0x41800000, entry.EffectiveReachSquared);
        Assert.Equal(1000, entry.CumulativeThreshold);
        Assert.Equal(1000, pool.TotalWeight);
        Assert.Equal(16f, pool.MinimumReachSquared);
        Assert.Equal(1156f, pool.MaximumEligibleReachSquared);
        Assert.Equal("static-fixture:not-a-native-oracle", pool.SourceEvidence);
    }

    [Fact]
    public void LongerProfileCanBeTheOnlyInRangeCandidate()
    {
        var pool = Prepare(new(1f, 1f, 100f, false), Attack(0, 2f, 1000), Attack(1, 32f, 1000));
        Assert.Equal(1, Assert.Single(pool.Entries).CombinedIndex);
        Assert.Equal(1156f, pool.MinimumReachSquared);
        Assert.True(pool.RequiresOriginalRangeWrapper);
        Assert.True(pool.RequiresInclusiveAdapter);
    }

    [Fact]
    public void FloorAtTwoMakesDifferentSmallRangesOneOrderedGroup()
    {
        var pool = Prepare(new(0f, 0f, 0f, false), Attack(0, 0f, 1), Attack(7, 1f, 2), Attack(22, 2f, 3));
        Assert.Equal(new[] { 0, 7, 22 }, pool.Entries.Select(e => e.CombinedIndex));
        Assert.Equal(new[] { 1, 3, 6 }, pool.Entries.Select(e => e.CumulativeThreshold));
        Assert.All(pool.Entries, entry => AssertBits(0x40800000, entry.EffectiveReachSquared));
    }

    [Fact]
    public void RangeBoundaryUsesExactlyRoundedPointZeroOneAddition()
    {
        // f32(4 + f32(0.01)) == 0x408051ec. The next representable value is outside.
        var at = Prepare(new(0f, 0f, FromBits(0x408051ec), false), Attack(0, 2f, 1));
        var beyond = Prepare(new(0f, 0f, FromBits(0x408051ed), false), Attack(0, 2f, 1));
        Assert.Single(at.Entries);
        Assert.Empty(beyond.Entries);
        Assert.Null(beyond.MinimumReachSquared);
        Assert.Equal(4f, beyond.MaximumEligibleReachSquared);
    }

    [Fact]
    public void TieBoundaryUsesSquaredReachesAndStrictBinary32Tolerance()
    {
        // (2 + 104*2^-22)^2 rounds to 4 + 208*2^-21, below 0.0001 from 4.
        // (2 + 105*2^-22)^2 rounds to 4 + 210*2^-21, above that tolerance.
        var pool = Prepare(new(0f, 0f, 0f, false), Attack(0, 2f, 10),
            Attack(7, FromBits(0x40000068), 20), Attack(22, FromBits(0x40000069), 30));
        Assert.Equal(new[] { 0, 7 }, pool.Entries.Select(e => e.CombinedIndex));
        Assert.Equal(new[] { 10, 30 }, pool.Entries.Select(e => e.CumulativeThreshold));
        AssertBits(0x408000d0, pool.Entries[1].EffectiveReachSquared);
        AssertBits(0x408000d2, pool.MaximumEligibleReachSquared!.Value);
    }

    [Fact]
    public void GlobalMinimumDoesNotReorderNearTiesWhenShortestEntryComesLater()
    {
        var pool = Prepare(new(0f, 0f, 0f, false), Attack(2, FromBits(0x40000068), 5), Attack(8, 2f, 7));
        Assert.Equal(4f, pool.MinimumReachSquared);
        Assert.Equal(new[] { 2, 8 }, pool.Entries.Select(e => e.CombinedIndex));
        Assert.Equal(new[] { 5, 12 }, pool.Entries.Select(e => e.CumulativeThreshold));
    }

    [Fact]
    public void RadiusAdditionRoundsBeforeEntryRangeRatherThanReassociating()
    {
        // ties-to-even: f32(2^24+1) == 2^24; adding range 1 rounds down again.
        // Combining the two ones first would produce a different squared reach.
        var pool = Prepare(new(16777216f, 1f, 0f, false), Attack(0, 1f, 1));
        AssertBits(0x57800000, Assert.Single(pool.Entries).EffectiveReachSquared); // exactly 2^48
    }

    [Fact]
    public void SquaringTheNextFloatAboveTwoHasIndependentExactBits()
    {
        var pool = Prepare(new(0f, 0f, 0f, false), Attack(0, FromBits(0x40000001), 1));
        AssertBits(0x40800002, Assert.Single(pool.Entries).EffectiveReachSquared);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1000)]
    [InlineData(int.MaxValue)]
    public void SingletonWeightStillRequiresItsOriginalWrapperReceipt(int weight)
    {
        var pool = Prepare(new(0f, 0f, 0f, false), Attack(6, 2f, weight));
        Assert.True(pool.RequiresOriginalRangeWrapper);
        Assert.Equal(weight > 1, pool.RequiresInclusiveAdapter);
        var receipt = Receipt(weight, weight);
        var candidates = pool.EnumerateSatisfiedThresholds(receipt);
        Assert.Equal(receipt, candidates.Receipt);
        Assert.Equal(6, Assert.Single(candidates.Entries).CombinedIndex);
        Assert.Throws<ArgumentException>(() => pool.EnumerateSatisfiedThresholds(
            receipt with { AdapterInvoked = !receipt.AdapterInvoked }));
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(1000, 2)]
    [InlineData(1001, 1)]
    [InlineData(2000, 1)]
    public void OneDrawReturnsOrderedPotentialFallthroughCandidates(int draw, int count)
    {
        var pool = Prepare(new(0f, 0f, 0f, false), Attack(2, 2f, 1000), Attack(9, 2f, 1000));
        var candidates = pool.EnumerateSatisfiedThresholds(Receipt(2000, draw));
        Assert.Equal(count, candidates.Entries.Count);
        Assert.Equal(9, candidates.Entries[^1].CombinedIndex);
        if (count == 2)
            Assert.Equal(2, candidates.Entries[0].CombinedIndex);
        // Both entries remain available for draw <=1000. This method makes no geometry
        // decision: original geometry failure on the first may fall through to the second.
        Assert.Equal(draw, candidates.Receipt.Result);
    }

    [Fact]
    public void ZeroWeightCannotChangeMinimumMaximumOrThresholds()
    {
        var pool = Prepare(new(0f, 0f, 0f, false), Attack(0, 0f, 0), Attack(4, 16f, 1));
        Assert.Equal(4, Assert.Single(pool.Entries).CombinedIndex);
        Assert.Equal(256f, pool.MinimumReachSquared);
        Assert.Equal(256f, pool.MaximumEligibleReachSquared);
        Assert.Equal(1, pool.TotalWeight);
    }

    [Fact]
    public void EmptyAndAllZeroInputsHaveNoRangeRequest()
    {
        var empty = Prepare(new(0f, 0f, 0f, false));
        var zeros = Prepare(new(0f, 0f, 0f, false), Attack(5, 100f, 0));
        foreach (var pool in new[] { empty, zeros })
        {
            Assert.Empty(pool.Entries);
            Assert.Null(pool.MinimumReachSquared);
            Assert.Null(pool.MaximumEligibleReachSquared);
            Assert.Equal(0, pool.TotalWeight);
            Assert.False(pool.RequiresOriginalRangeWrapper);
            Assert.False(pool.RequiresInclusiveAdapter);
            Assert.Throws<InvalidOperationException>(() => pool.EnumerateSatisfiedThresholds(default));
        }
    }

    [Fact]
    public void AllOutOfRangeRetainsMaximumWithoutGuessingNativeFailureCode()
    {
        var pool = Prepare(new(0f, 0f, 10000f, false), Attack(0, 2f, 1000), Attack(1, 3f, 1000));
        Assert.Empty(pool.Entries);
        Assert.Null(pool.MinimumReachSquared);
        Assert.Equal(9f, pool.MaximumEligibleReachSquared);
        Assert.False(pool.RequiresOriginalRangeWrapper);
    }

    [Fact]
    public void SuppliedOriginalBypassChangesRangeInclusionWithoutGeneratingGeometry()
    {
        var geometry = new Pool.EffectiveGeometry(0f, 0f, float.MaxValue, true);
        var pool = Prepare(geometry, Attack(2, 2f, 1), Attack(8, 32f, 1));
        Assert.Equal(geometry, pool.Geometry);
        Assert.Equal(2, Assert.Single(pool.Entries).CombinedIndex);
        Assert.Equal(1024f, pool.MaximumEligibleReachSquared);
    }

    [Fact]
    public void CumulativeInt32OverflowIsRejectedRatherThanReturningGuessedFallback()
    {
        Assert.Throws<ArithmeticException>(() => Prepare(new(0f, 0f, 0f, false),
            Attack(0, 2f, 1500000000), Attack(1, 2f, 1500000000)));
        Assert.Throws<ArithmeticException>(() => Prepare(new(0f, 0f, 0f, false),
            Attack(0, 2f, int.MaxValue), Attack(1, 2f, 1)));
    }

    [Fact]
    public void WeightsOutsideTheMinimumGroupAreNotAddedToItsThresholds()
    {
        var pool = Prepare(new(0f, 0f, 0f, false), Attack(0, 2f, int.MaxValue), Attack(1, 32f, int.MaxValue));
        Assert.Single(pool.Entries);
        Assert.Equal(int.MaxValue, pool.TotalWeight);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void NegativeWeightIsUnsupported(int weight)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Prepare(new(0f, 0f, 0f, false), Attack(0, 2f, weight)));
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 23)]
    [InlineData(4, 4)]
    [InlineData(9, 3)]
    public void InvalidDuplicateAndReorderedIndicesAreRejected(int first, int second)
    {
        Assert.Throws<ArgumentException>(() => Prepare(new(0f, 0f, 0f, false), Attack(first, 2f, 1), Attack(second, 2f, 1)));
    }

    [Fact]
    public void MoreThanFirst23IsRejectedRatherThanSilentlyTruncated()
    {
        var entries = Enumerable.Range(0, 24).Select(i => Attack(i, 2f, 1)).ToArray();
        Assert.Throws<ArgumentOutOfRangeException>(() => Prepare(new(0f, 0f, 0f, false), entries));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(-1f)]
    public void UnsupportedGeometryIsRejectedInEveryInputPosition(float value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Prepare(new(value, 0f, 0f, false), Attack(0, 2f, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Prepare(new(0f, value, 0f, false), Attack(0, 2f, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Prepare(new(0f, 0f, value, false), Attack(0, 2f, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Prepare(new(0f, 0f, 0f, false), Attack(0, value, 1)));
    }

    [Fact]
    public void FiniteInputsWithNonfiniteIntermediateAreRejected()
    {
        Assert.Throws<ArithmeticException>(() => Prepare(new(float.MaxValue, float.MaxValue, 0f, false), Attack(0, 2f, 1)));
        Assert.Throws<ArithmeticException>(() => Prepare(new(0f, 0f, 0f, false), Attack(0, float.MaxValue, 1)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2001)]
    public void ReceiptResultMustBeWithinOriginalInclusiveRequest(int result)
    {
        var pool = Prepare(new(0f, 0f, 0f, false), Attack(0, 2f, 2000));
        Assert.Throws<ArgumentOutOfRangeException>(() => pool.EnumerateSatisfiedThresholds(Receipt(2000, result)));
    }

    [Fact]
    public void ReceiptMustIdentifyExactEndpointsAndExplicitEvidence()
    {
        var pool = Prepare(new(0f, 0f, 0f, false), Attack(0, 2f, 1000));
        Assert.Throws<ArgumentException>(() => pool.EnumerateSatisfiedThresholds(Receipt(1000, 1) with { Minimum = 0 }));
        Assert.Throws<ArgumentException>(() => pool.EnumerateSatisfiedThresholds(Receipt(999, 1)));
        Assert.Throws<ArgumentException>(() => pool.EnumerateSatisfiedThresholds(Receipt(1000, 1) with { SourceEvidence = " " }));
    }

    [Fact]
    public void SourceEvidenceAndPinnedBuildAreMandatory()
    {
        var geometry = new Pool.EffectiveGeometry(0f, 0f, 0f, false);
        var entries = new[] { Attack(0, 2f, 1) };
        Assert.Throws<ArgumentException>(() => Pool.Prepare(Scope(), "other-build", "source", geometry, entries));
        Assert.Throws<ArgumentException>(() => Pool.Prepare(Scope(), null!, "source", geometry, entries));
        Assert.Throws<ArgumentException>(() => Pool.Prepare(Scope(), Pool.NpcServerBinarySha256, "", geometry, entries));
        Assert.Throws<ArgumentException>(() => Prepare(geometry, entries[0] with { SourceEvidence = null! }));
        Assert.Throws<ArgumentNullException>(() => Pool.Prepare(null!, Pool.NpcServerBinarySha256, "source", geometry, entries));
        Assert.Throws<ArgumentNullException>(() => Pool.Prepare(Scope(), Pool.NpcServerBinarySha256, "source", geometry, null!));
    }

    [Fact]
    public void PlayerAttackerCannotEnterNpcSelectionArithmetic()
    {
        var player = RetailNpcNormalScope.Create(RetailNpcNormalScope.MainBinarySha256,
            RetailNpcNormalActorKind.Player, RetailNpcNormalActorKind.OrdinaryOwnerlessNpc, 0, 212835);
        Assert.Throws<ArgumentException>(() => Pool.Prepare(player, Pool.NpcServerBinarySha256, "source",
            new(0f, 0f, 0f, false), new[] { Attack(0, 2f, 1) }));
    }

    [Fact]
    public void InputMutationAndCollectionCastsCannotRewritePoolOrCandidateEvidence()
    {
        var inputs = new[] { Attack(2, 2f, 1000), Attack(9, 2f, 1000) };
        var pool = Prepare(new(0f, 0f, 0f, false), inputs);
        inputs[0] = Attack(0, 32f, 1);
        var candidates = pool.EnumerateSatisfiedThresholds(Receipt(2000, 500));
        Assert.Equal(2, pool.Entries[0].CombinedIndex);
        Assert.Equal("static-source:combined-entry:2", candidates.Entries[0].SourceEvidence);
        Assert.Throws<NotSupportedException>(() => { ((IList<Pool.PoolEntry>)pool.Entries)[0] = default; });
        Assert.Throws<NotSupportedException>(() => { ((IList<Pool.PoolEntry>)candidates.Entries)[0] = default; });
    }
}
