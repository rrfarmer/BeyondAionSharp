using Aion.GameServer.GeoEngine.RetailPath;

namespace Aion.GameServer.Tests;

public class RetailNpcSegmentReachFactorTests
{
    // Independent binary32 examples generated before this implementation from the checked
    // instruction recipe; no C# output or live native execution supplied the expected bits.
    [Theory]
    [InlineData(10, 0, 0, 10, 0, 0, 4, 0x3f4ccccd, 1)]
    [InlineData(-10, 0, 0, -10, 0, 0, 4, 0x3f4ccccd, 1)]
    [InlineData(10, 0, 0, 5, 0, 0, 1, 0x3ecccccd, 1)]
    [InlineData(10, 0, 0, 5, 1, 0, 1, 0x3f000000, 1)]
    [InlineData(10, 0, 0, 5, 3, 0, 4, 0x3f800000, 0)]
    [InlineData(10, 0, 0, -5, 1, 0, 1, 0x3f80a3d1, 201)]
    [InlineData(10, 0, 0, 20, 0, 0, 4, 0x3f80a3d1, 201)]
    [InlineData(10, 0, 0, 0, 0, 0, 4, 0, 1)]
    [InlineData(0, 0, 0, 10, 0, 0, 1, 0x3f80a3d1, 201)]
    [InlineData(0, 0, 0, 0, 0, 0, 0, 0, 1)]
    [InlineData(.009, 0, 0, .009, 0, 0, 1e-8, 0x3f7d7097, 199)]
    [InlineData(0, 0, 10, 0, 0, 10, 4, 0x3f4ccccd, 1)]
    [InlineData(3, 4, 12, 3, 4, 12, 1, 0x3f6c4ec5, 1)]
    [InlineData(10, 0, 0, 5, 0, 0, 0, 0x3f000000, 1)]
    [InlineData(10, 0, 0, 10, 1, 0, 1, 0x3f800000, 1)]
    [InlineData(10, 0, 0, 0, 1, 0, 1, 0, 1)]
    [InlineData(.01, 0, 0, .01, 0, 0, 1e-8, 0x3f7d70e7, 1)]
    [InlineData(10, 0, 0, 7.77, 0, 0, .0001, 0x3f46a8b4, 1)]
    public void EntireHelperMatchesFixedInstructionOrderExamples(float x, float y, float z,
        float tx, float ty, float tz, float squaredRadius, int bits, int samples)
    {
        var result = Calculate(new(0, 0, 0), new(x, y, z), new(tx, ty, tz), squaredRadius);
        Assert.Equal(RetailNpcSegmentReachStatus.Calculated, result.Status);
        Assert.Equal(bits, BitConverter.SingleToInt32Bits(result.Factor!.Value));
        Assert.Equal(samples, result.FineSamples);
        Assert.False(result.RuntimeVerified); Assert.False(result.ExecutesMovement);
    }

    [Fact]
    public void OriginalWorldCoordinatesAndFineStepRoundingArePreserved()
    {
        var translated = Calculate(new(100, -100, 575), new(10, 0, 0), new(110, -100, 575), 4);
        Assert.Equal(0x3f4ccccd, BitConverter.SingleToInt32Bits(translated.Factor!.Value));
        var fine = Calculate(new(47.75f, 133.75f, 575.26f), new(.123f, .456f, .789f), new(48, 134, 576), .1f);
        Assert.Equal(RetailNpcSegmentReachStatus.Calculated, fine.Status);
        Assert.Equal(0x3f1c71f3, BitConverter.SingleToInt32Bits(fine.Factor!.Value));
        Assert.Equal(2, fine.FineSamples);
        Assert.True(fine.Factor > fine.AnalyticCandidate);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(float.Epsilon)]
    [InlineData(float.MaxValue)]
    public void UnsupportedArithmeticPublishesNoFactor(float value)
    {
        var result = Calculate(new(0, 0, 0), new(value, 0, 0), new(1, 0, 0), 1);
        Assert.Equal(RetailNpcSegmentReachStatus.UnsupportedArithmetic, result.Status);
        Assert.Null(result.Factor); Assert.NotEmpty(result.Detail!);
    }

    [Fact]
    public void RadiusAndBuildCannotSilentlyFallback()
    {
        Assert.Equal(RetailNpcSegmentReachStatus.UnsupportedArithmetic,
            Calculate(new(0, 0, 0), new(1, 0, 0), new(1, 0, 0), -1).Status);
        var foreign = RetailNpcSegmentReachFactor.Calculate(new(0, 0, 0), new(1, 0, 0), new(1, 0, 0), 1, new('f', 64));
        Assert.Equal(RetailNpcSegmentReachStatus.UnsupportedSource, foreign.Status); Assert.Null(foreign.Factor);
        var zero = Calculate(new(0, 0, 0), new(-0f, 0, 0), new(0, 0, 0), -0f);
        Assert.Equal(RetailNpcSegmentReachStatus.Calculated, zero.Status); Assert.Equal(0f, zero.Factor);
    }

    private static RetailNpcSegmentReachResult Calculate(RetailSearchPoint origin, RetailSearchPoint delta, RetailSearchPoint target, float squaredRadius) =>
        RetailNpcSegmentReachFactor.Calculate(origin, delta, target, squaredRadius, RetailGroundPathSearch.SupportedNpcBinarySha256);
}
