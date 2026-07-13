using Aion.GameServer.Handlers.AdminCommands;

namespace Aion.GameServer.Tests;

public sealed class MoveToPortTests
{
    [Theory]
    [InlineData(0, 110f, 200f)]
    [InlineData(30, 100f, 210f)]
    [InlineData(60, 90f, 200f)]
    [InlineData(90, 100f, 190f)]
    public void ForwardMovementUsesPlayerHeading(byte heading, float expectedX, float expectedY)
    {
        (float x, float y) = MoveTo.CalculateForwardPosition(100, 200, heading, 10);

        Assert.Equal(expectedX, x, precision: 4);
        Assert.Equal(expectedY, y, precision: 4);
    }

    [Theory]
    [InlineData(300040000, 1, 300040000)]
    [InlineData(300040000, 2, 300040001)]
    [InlineData(300040000, 42, 300040041)]
    public void CoordinateParsingEncodesCurrentInstance(int worldId, int instanceId, int expected)
    {
        Assert.Equal(expected, MoveTo.EncodeMapAndInstanceId(worldId, instanceId));
    }
}
