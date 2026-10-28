using Aion.GameServer.Controllers.Attack.RetailNpc;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcNormalBaseMathTests
{
    [Fact]
    public void EachDualHandReceivesTheCompleteLaterFlatAndItsOwnSelectedSample()
    {
        var ranges = RetailNpcDualNormalPlan.Split(100, 100, 1);
        var current = new RetailNpcStatNormalInputs.Modifiers(20, 20);
        float main = RetailNpcStatNormalInputs.CalculateBase(ranges.Main.Minimum, 150, current);
        float off = RetailNpcStatNormalInputs.CalculateBase(ranges.Offhand.Minimum, 150, current);
        Assert.Equal(105f, main); Assert.Equal(105f, off); Assert.Equal(210f, main + off);
        Assert.Equal(50, ranges.Main.Minimum);
    }

    [Theory]
    [InlineData(0, 100, 0, 0, 1f)]
    [InlineData(100, 0, 0, -20, 1f)]
    [InlineData(100, 150, -200, 0, 1f)]
    [InlineData(100, 100, 0, 20, 120f)]
    public void MinimumFloorOccursAfterBothIndependentChannels(int sample, int profile, int percent, int flat, float expected)
        => Assert.Equal(expected, RetailNpcStatNormalInputs.CalculateBase(sample, profile, new(percent, flat)));
}
