using Aion.GameServer.Controllers.Attack.RetailNpc;
using MathStages = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcNormalStatusMath;
using Stage = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcNormalStatusMath.ChanceStage;

namespace Aion.GameServer.Tests;

/// <summary>Hand/instruction-derived scalar cases, not executed native or live status oracles.</summary>
public sealed class RetailNpcNormalStatusMathTests
{
    private static RetailNpcNormalScope NpcToPlayer() => RetailNpcNormalScope.Create(
        RetailNpcNormalScope.MainBinarySha256, RetailNpcNormalActorKind.OrdinaryOwnerlessNpc,
        RetailNpcNormalActorKind.Player, 212835, 0);

    private static RetailNpcNormalScope PlayerToNpc() => RetailNpcNormalScope.Create(
        RetailNpcNormalScope.MainBinarySha256, RetailNpcNormalActorKind.Player,
        RetailNpcNormalActorKind.OrdinaryOwnerlessNpc, 0, 212776);

    private static MathStages.Context Context(RetailNpcNormalScope? scope = null) => MathStages.Context.Create(
        scope ?? NpcToPlayer(), RetailNpcNormalScope.MainBinarySha256, "static-effective-inputs:not-a-native-oracle", false);

    private static MathStages.OriginalMainDrawReceipt Receipt(Stage stage, int result) => new(
        RetailNpcNormalScope.MainBinarySha256, stage, 1, 1000, result, true,
        "supplied-original-Main-call:static-example");

    [Theory]
    [InlineData((int)Stage.Dodge, 660)]
    [InlineData((int)Stage.Block, 720)]
    [InlineData((int)Stage.Parry, 720)]
    public void NpcLevelAdjustmentActsOnDefenseBeforeAccuracy(int rawStage, int adjusted)
    {
        var stage = (Stage)rawStage;
        // Levels 30/33: evasion 600*1.1=660, block/parry600*1.2=720.
        // Subtracting accuracy 500 afterwards gives 160/220, not 110/120.
        var context = Context(PlayerToNpc());
        int defense = MathStages.AdjustOwnerlessNpcDefense(context, stage, 600, 30, 33);
        Assert.Equal(adjusted, defense);
        Assert.Equal(adjusted - 500, MathStages.CalculatePhysicalThreshold(context, stage,
            new(defense, 500, 0, 1000)));
    }

    [Theory]
    [InlineData((int)Stage.Dodge)]
    [InlineData((int)Stage.Block)]
    [InlineData((int)Stage.Parry)]
    public void LevelDifferenceThroughTwoReturnsTheOriginalWord(int rawStage)
    {
        var stage = (Stage)rawStage;
        var context = Context(PlayerToNpc());
        Assert.Equal(65535, MathStages.AdjustOwnerlessNpcDefense(context, stage, 65535, 30, 32));
        Assert.Equal(600, MathStages.AdjustOwnerlessNpcDefense(context, stage, 600, 33, 30));
    }

    [Fact]
    public void EvasionNarrowsAfterConversionWhileBlockAndParryRetainInt32()
    {
        var context = Context(PlayerToNpc());
        // f32(65535 * f32(1+0.1)) ==72088.5; trunc 72088; low16=6552.
        Assert.Equal(6552, MathStages.AdjustOwnerlessNpcDefense(context, Stage.Dodge, 65535, 30, 33));
        Assert.Equal(78642, MathStages.AdjustOwnerlessNpcDefense(context, Stage.Block, 65535, 30, 33));
        Assert.Equal(78642, MathStages.AdjustOwnerlessNpcDefense(context, Stage.Parry, 65535, 30, 33));
    }

    [Fact]
    public void NativeLevelAdjustmentContinuesPastTwelveLevelDifference()
    {
        var context = Context(PlayerToNpc());
        Assert.Equal(1200, MathStages.AdjustOwnerlessNpcDefense(context, Stage.Dodge, 600, 30, 42));
        Assert.Equal(1260, MathStages.AdjustOwnerlessNpcDefense(context, Stage.Dodge, 600, 30, 43));
    }

    [Theory]
    [InlineData((int)Stage.Dodge, 300)]
    [InlineData((int)Stage.Block, 500)]
    [InlineData((int)Stage.Parry, 400)]
    public void PhysicalThresholdsHaveDistinctCaps(int rawStage, int expected)
    {
        var stage = (Stage)rawStage;
        Assert.Equal(expected, MathStages.CalculatePhysicalThreshold(Context(), stage, new(1000, 0, 0, 1000)));
    }

    [Theory]
    [InlineData((int)Stage.Dodge)]
    [InlineData((int)Stage.Block)]
    [InlineData((int)Stage.Parry)]
    public void PhysicalMovementAndAttackerRatioAreAppliedBeforeCap(int rawStage)
    {
        var stage = (Stage)rawStage;
        Assert.Equal(175, MathStages.CalculatePhysicalThreshold(Context(), stage, new(700, 400, 50, 2000)));
        Assert.Equal(0, MathStages.CalculatePhysicalThreshold(Context(), stage, new(100, 400, -50, 1000)));
    }

    [Fact]
    public void EvasionInputMustAlreadyBeNarrowedButNpcBlockCanRetainLargeHelperResult()
    {
        var context = Context(PlayerToNpc());
        Assert.Throws<ArgumentOutOfRangeException>(() => MathStages.CalculatePhysicalThreshold(context, Stage.Dodge, new(72088, 0, 0, 1000)));
        Assert.Equal(500, MathStages.CalculatePhysicalThreshold(context, Stage.Block, new(78642, 0, 0, 1000)));
        Assert.Throws<ArgumentOutOfRangeException>(() => MathStages.CalculatePhysicalThreshold(Context(), Stage.Block, new(78642, 0, 0, 1000)));
    }

    [Fact]
    public void MagicalLevelOffsetAppliesEvenWithZeroResistance()
    {
        Assert.Equal(100, MathStages.CalculateMagicalResistThreshold(Context(), new(0, 0, 1000, 30, 35, 0)));
        Assert.Equal(0, MathStages.CalculateMagicalResistThreshold(Context(), new(0, 0, 1000, 30, 34, 0)));
    }

    [Fact]
    public void MagicalBaseClampPrecedesNegativeLevelOffsetAndFinalClamp()
    {
        Assert.Equal(400, MathStages.CalculateMagicalResistThreshold(Context(), new(600, 0, 1000, 35, 30, 0)));
        Assert.Equal(900, MathStages.CalculateMagicalResistThreshold(Context(), new(600, 0, 1000, 30, 40, 0)));
        Assert.Equal(0, MathStages.CalculateMagicalResistThreshold(Context(), new(0, 0, 1000, 40, 30, 0)));
    }

    [Theory]
    [InlineData(4, 200)]
    [InlineData(-4, 200)]
    [InlineData(5, 300)]
    [InlineData(-5, 100)]
    public void MagicalLevelOffsetHasExactPlusMinusFourBoundary(int difference, int expected)
    {
        Assert.Equal(expected, MathStages.CalculateMagicalResistThreshold(Context(), new(200, 0, 1000, 30, 30 + difference, 0)));
    }

    [Fact]
    public void MagicalRawCodeFiveTruncatesBeforeRatioNormalization()
    {
        // trunc(f32(5*0.5))=2; trunc(f32(2/f32(1200/1000)))=1.
        // A single final truncation would yield 2 instead.
        Assert.Equal(1, MathStages.CalculateMagicalResistThreshold(Context(), new(5, 0, 1200, 30, 30, 5)));
        Assert.Equal(4, MathStages.CalculateMagicalResistThreshold(Context(), new(5, 0, 1200, 30, 30, 0)));
        Assert.Equal(4, MathStages.CalculateMagicalResistThreshold(Context(), new(5, 0, 1200, 30, 30, 15)));
    }

    [Fact]
    public void NegativeMagicDifferenceIsClampedBeforeTheIndependentLevelOffset()
    {
        Assert.Equal(100, MathStages.CalculateMagicalResistThreshold(Context(), new(0, 1000, 1000, 30, 35, 0)));
    }

    [Fact]
    public void CriticalUsesDefenderRatioAndTruncatesSignedValueTowardZero()
    {
        Assert.Equal(150, MathStages.CalculateMainCriticalThreshold(Context(), new(350, 50, 1, 2000, 0)));
        Assert.Equal(-1, MathStages.CalculateMainCriticalThreshold(Context(), new(47, 50, 0, 2000, 0)));
        Assert.Equal(-1, MathStages.CalculateMainCriticalThreshold(Context(), new(49, 50, 0, 1000, 0)));
    }

    [Fact]
    public void CriticalMovementAndRawCodeBonusPrecedeItsFiveHundredUpperCap()
    {
        Assert.Equal(500, MathStages.CalculateMainCriticalThreshold(Context(), new(600, 50, 0, 1000, 0)));
        Assert.Equal(475, MathStages.CalculateMainCriticalThreshold(Context(), new(100, 150, 25, 1000, 5)));
        Assert.Equal(-25, MathStages.CalculateMainCriticalThreshold(Context(), new(100, 150, 25, 1000, 0)));
    }

    [Theory]
    [InlineData((int)Stage.Dodge)]
    [InlineData((int)Stage.Block)]
    [InlineData((int)Stage.Parry)]
    [InlineData((int)Stage.MagicalResist)]
    [InlineData((int)Stage.MainCritical)]
    public void ZeroThresholdStillRequiresAnOriginalRangeReceipt(int rawStage)
    {
        var stage = (Stage)rawStage;
        var context = Context();
        Assert.False(MathStages.CompareOriginalMainRoll(context, stage, 0, Receipt(stage, 1)));
        Assert.Throws<ArgumentException>(() => MathStages.CompareOriginalMainRoll(context, stage, 0, default));
        Assert.Throws<ArgumentException>(() => MathStages.CompareOriginalMainRoll(context, stage, 0,
            Receipt(stage, 1) with { InclusiveAdapterInvoked = false }));
    }

    [Theory]
    [InlineData((int)Stage.Dodge, 300)]
    [InlineData((int)Stage.Block, 500)]
    [InlineData((int)Stage.Parry, 400)]
    [InlineData((int)Stage.MagicalResist, 900)]
    [InlineData((int)Stage.MainCritical, 500)]
    public void OriginalComparisonIsInclusiveAndKeepsStageBoundaries(int rawStage, int maximum)
    {
        var stage = (Stage)rawStage;
        var context = Context();
        Assert.True(MathStages.CompareOriginalMainRoll(context, stage, maximum, Receipt(stage, maximum)));
        Assert.False(MathStages.CompareOriginalMainRoll(context, stage, maximum, Receipt(stage, maximum + 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => MathStages.CompareOriginalMainRoll(context, stage, maximum + 1, Receipt(stage, 1)));
    }

    [Fact]
    public void NegativeCriticalThresholdIsPreservedAndStillComparesAnOriginalRoll()
    {
        var context = Context();
        Assert.False(MathStages.CompareOriginalMainRoll(context, Stage.MainCritical, -1, Receipt(Stage.MainCritical, 1)));
        Assert.False(MathStages.CompareOriginalMainRoll(context, Stage.MainCritical, int.MinValue, Receipt(Stage.MainCritical, 1000)));
        Assert.Throws<ArgumentOutOfRangeException>(() => MathStages.CompareOriginalMainRoll(context, Stage.Dodge, -1, Receipt(Stage.Dodge, 1)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public void OriginalResultsOutsideOneThroughThousandAreRejected(int result)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MathStages.CompareOriginalMainRoll(Context(), Stage.Block, 500, Receipt(Stage.Block, result)));
    }

    [Fact]
    public void OriginalReceiptRequiresMatchingStageEndpointsMainBuildAndEvidence()
    {
        var context = Context();
        var receipt = Receipt(Stage.Block, 10);
        Assert.Throws<ArgumentException>(() => MathStages.CompareOriginalMainRoll(context, Stage.Block, 500, receipt with { Stage = Stage.Parry }));
        Assert.Throws<ArgumentException>(() => MathStages.CompareOriginalMainRoll(context, Stage.Block, 500, receipt with { Minimum = 0 }));
        Assert.Throws<ArgumentException>(() => MathStages.CompareOriginalMainRoll(context, Stage.Block, 500, receipt with { Maximum = 999 }));
        Assert.Throws<ArgumentException>(() => MathStages.CompareOriginalMainRoll(context, Stage.Block, 500, receipt with { SourceMainSha256 = RetailNpcMinimumReachPool.NpcServerBinarySha256 }));
        Assert.Throws<ArgumentException>(() => MathStages.CompareOriginalMainRoll(context, Stage.Block, 500, receipt with { SourceEvidence = " " }));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(999u)]
    [InlineData(65001u)]
    [InlineData(uint.MaxValue)]
    public void EffectiveRatiosOutsideVerifiedConstructionDomainAreRejected(uint ratio)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MathStages.CalculatePhysicalThreshold(Context(), Stage.Dodge, new(0, 0, 0, ratio)));
        Assert.Throws<ArgumentOutOfRangeException>(() => MathStages.CalculateMagicalResistThreshold(Context(), new(0, 0, ratio, 30, 30, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => MathStages.CalculateMainCriticalThreshold(Context(), new(0, 0, 0, ratio, 0)));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void UnsupportedLevelsAreNotReplacedWithDefaults(int level)
    {
        var context = Context(PlayerToNpc());
        Assert.Throws<ArgumentOutOfRangeException>(() => MathStages.AdjustOwnerlessNpcDefense(context, Stage.Dodge, 1, level, 30));
        Assert.Throws<ArgumentOutOfRangeException>(() => MathStages.AdjustOwnerlessNpcDefense(context, Stage.Dodge, 1, 30, level));
        Assert.Throws<ArgumentOutOfRangeException>(() => MathStages.CalculateMagicalResistThreshold(context, new(0, 0, 1000, level, 30, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => MathStages.CalculateMagicalResistThreshold(context, new(0, 0, 1000, 30, level, 0)));
    }

    [Theory]
    [InlineData((byte)16)]
    [InlineData(byte.MaxValue)]
    public void RawStateMustBeTheExplicitLowNibble(byte value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MathStages.CalculateMagicalResistThreshold(Context(), new(0, 0, 1000, 30, 30, value)));
        Assert.Throws<ArgumentOutOfRangeException>(() => MathStages.CalculateMainCriticalThreshold(Context(), new(0, 0, 0, 1000, value)));
    }

    [Fact]
    public void NpcWordSourcesCannotSilentlyAcceptWiderStats()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MathStages.CalculatePhysicalThreshold(Context(), Stage.Block, new(0, 65536, 0, 1000)));
        Assert.Throws<ArgumentOutOfRangeException>(() => MathStages.CalculateMagicalResistThreshold(Context(), new(0, 65536, 1000, 30, 30, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => MathStages.CalculateMagicalResistThreshold(Context(PlayerToNpc()), new(65536, 0, 1000, 30, 30, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => MathStages.CalculateMainCriticalThreshold(Context(), new(65536, 0, 0, 1000, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => MathStages.CalculateMainCriticalThreshold(Context(PlayerToNpc()), new(0, 65536, 0, 1000, 0)));
    }

    [Fact]
    public void NegativeEffectiveStatsAreRejectedInsteadOfBeingTreatedAsBonuses()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MathStages.CalculatePhysicalThreshold(Context(), Stage.Dodge, new(-1, 0, 0, 1000)));
        Assert.Throws<ArgumentOutOfRangeException>(() => MathStages.CalculatePhysicalThreshold(Context(), Stage.Dodge, new(0, -1, 0, 1000)));
        Assert.Throws<ArgumentOutOfRangeException>(() => MathStages.CalculateMagicalResistThreshold(Context(), new(-1, 0, 1000, 30, 30, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => MathStages.CalculateMagicalResistThreshold(Context(), new(0, -1, 1000, 30, 30, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => MathStages.CalculateMainCriticalThreshold(Context(), new(-1, 0, 0, 1000, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => MathStages.CalculateMainCriticalThreshold(Context(), new(0, -1, 0, 1000, 0)));
    }

    [Fact]
    public void IntegerWrapIsRejectedBeforeAnyChanceClamp()
    {
        Assert.Throws<ArithmeticException>(() => MathStages.CalculatePhysicalThreshold(Context(), Stage.Dodge, new(1, 0, int.MaxValue, 1000)));
        Assert.Throws<ArithmeticException>(() => MathStages.CalculatePhysicalThreshold(Context(), Stage.Block, new(0, 1, int.MinValue, 1000)));
        Assert.Throws<ArithmeticException>(() => MathStages.CalculateMainCriticalThreshold(Context(), new(1, 0, int.MaxValue, 1000, 0)));
        Assert.Throws<ArithmeticException>(() => MathStages.CalculateMainCriticalThreshold(Context(), new(0, 0, int.MaxValue, 1000, 5)));
    }

    [Fact]
    public void FloatRoundingToUnrepresentablePositiveInt32IsRejectedBeforeClamping()
    {
        // int.MaxValue converts to float 2^31. Native indefinite CVTT behavior is unsupported.
        Assert.Throws<ArithmeticException>(() => MathStages.CalculatePhysicalThreshold(Context(), Stage.Dodge, new(0, 0, int.MaxValue, 1000)));
        Assert.Throws<ArithmeticException>(() => MathStages.CalculateMainCriticalThreshold(Context(), new(0, 0, int.MaxValue, 1000, 0)));
        Assert.Throws<ArithmeticException>(() => MathStages.CalculateMagicalResistThreshold(Context(), new(int.MaxValue, 0, 1000, 30, 30, 0)));
    }

    [Fact]
    public void ExactlyRepresentableInt32MinimumRemainsInsideConversionDomain()
    {
        Assert.Equal(0, MathStages.CalculatePhysicalThreshold(Context(), Stage.Dodge, new(0, 0, int.MinValue, 1000)));
        Assert.Equal(int.MinValue, MathStages.CalculateMainCriticalThreshold(Context(), new(0, 0, int.MinValue, 1000, 0)));
    }

    [Theory]
    [InlineData((int)Stage.MagicalResist)]
    [InlineData((int)Stage.MainCritical)]
    [InlineData(999)]
    public void PhysicalOnlyOperationsRejectOtherStages(int rawStage)
    {
        var stage = (Stage)rawStage;
        Assert.Throws<ArgumentOutOfRangeException>(() => MathStages.AdjustOwnerlessNpcDefense(Context(PlayerToNpc()), stage, 1, 30, 30));
        Assert.Throws<ArgumentOutOfRangeException>(() => MathStages.CalculatePhysicalThreshold(Context(), stage, new(1, 0, 0, 1000)));
    }

    [Fact]
    public void ContextAndLevelHelperRejectUnsupportedDomainsAndMissingEvidence()
    {
        Assert.Throws<ArgumentNullException>(() => MathStages.Context.Create(null!, RetailNpcNormalScope.MainBinarySha256, "source", false));
        Assert.Throws<ArgumentException>(() => MathStages.Context.Create(NpcToPlayer(), "other-build", "source", false));
        Assert.Throws<ArgumentException>(() => MathStages.Context.Create(NpcToPlayer(), null!, "source", false));
        Assert.Throws<ArgumentException>(() => MathStages.Context.Create(NpcToPlayer(), RetailNpcNormalScope.MainBinarySha256, " ", false));
        Assert.Throws<ArgumentException>(() => MathStages.Context.Create(NpcToPlayer(), RetailNpcNormalScope.MainBinarySha256, "source", true));
        Assert.Throws<ArgumentException>(() => MathStages.AdjustOwnerlessNpcDefense(Context(), Stage.Dodge, 1, 30, 30));
        Assert.Equal("static-effective-inputs:not-a-native-oracle", Context().SourceEvidence);
    }

    [Fact]
    public void NullContextDoesNotBecomeDefaultEffectiveInputs()
    {
        Assert.Throws<ArgumentNullException>(() => MathStages.AdjustOwnerlessNpcDefense(null!, Stage.Dodge, 0, 30, 30));
        Assert.Throws<ArgumentNullException>(() => MathStages.CalculatePhysicalThreshold(null!, Stage.Dodge, new(0, 0, 0, 1000)));
        Assert.Throws<ArgumentNullException>(() => MathStages.CalculateMagicalResistThreshold(null!, new(0, 0, 1000, 30, 30, 0)));
        Assert.Throws<ArgumentNullException>(() => MathStages.CalculateMainCriticalThreshold(null!, new(0, 0, 0, 1000, 0)));
        Assert.Throws<ArgumentNullException>(() => MathStages.CompareOriginalMainRoll(null!, Stage.Dodge, 0, Receipt(Stage.Dodge, 1)));
    }

    [Fact]
    public void UnknownComparisonStageIsNotAnEnumOrdinalFallback()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MathStages.CompareOriginalMainRoll(Context(), (Stage)999, 0, Receipt((Stage)999, 1)));
    }

    [Theory]
    [InlineData((int)Stage.Dodge)]
    [InlineData((int)Stage.Block)]
    [InlineData((int)Stage.Parry)]
    public void ConstructorZeroNpcLevelKeepsItsDefenseWithoutInventingALevel(int stage)
    {
        Assert.Equal(600,MathStages.AdjustOwnerlessNpcDefense(Context(PlayerToNpc()),(Stage)stage,600,38,0));
    }

    [Fact]
    public void ZeroNpcLevelParticipatesInTheOriginalSignedMagicLevelDifference()
    {
        Assert.Equal(0,MathStages.CalculateMagicalResistThreshold(Context(PlayerToNpc()),new(500,0,1000,38,0,0)));
        Assert.Equal(900,MathStages.CalculateMagicalResistThreshold(Context(),new(0,0,1000,0,38,0)));
        Assert.Throws<ArgumentOutOfRangeException>(()=>MathStages.CalculateMagicalResistThreshold(Context(),new(0,0,1000,38,0,0)));
        Assert.Throws<ArgumentOutOfRangeException>(()=>MathStages.AdjustOwnerlessNpcDefense(Context(PlayerToNpc()),Stage.Dodge,600,0,0));
    }
}
