using Aion.GameServer.Controllers.Attack.RetailNpc;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcDualNormalRangeTests
{
    [Fact]
    public void OddSourceWordsLoseTheirFractionsBeforeEitherHandGetsItsModifiers()
    {
        var ranges = RetailNpcDualNormalPlan.Split(335, 503, 1);
        Assert.Equal(new(167, 251), ranges.Main);
        Assert.Equal(ranges.Main, ranges.Offhand);
        Assert.Equal(334, ranges.Main.Minimum + ranges.Offhand.Minimum);
        Assert.Equal(502, ranges.Main.Maximum + ranges.Offhand.Maximum);
        Assert.Equal(1, ranges.MainHitCount);
        Assert.Equal(1, ranges.OffhandHitCount);
    }

    [Theory]
    [InlineData(-1, 10, 1)]
    [InlineData(10, 65536, 1)]
    [InlineData(10, 20, 2)]
    public void SourceWidthAndExtraHitBoundariesStayExplicit(int minimum, int maximum, int hits)
        => Assert.Throws<ArgumentOutOfRangeException>(() => RetailNpcDualNormalPlan.Split(minimum, maximum, hits));
}
