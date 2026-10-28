using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Tests;

public sealed class NpcNormalSampleTests
{
    [Theory]
    [InlineData(101, 80, 80.8f)]
    [InlineData(101, 100, 101f)]
    [InlineData(101, 120, 121.2f)]
    [InlineData(0, 120, 0f)]
    [InlineData(1, 80, 0.8f)]
    [InlineData(int.MaxValue, 120, -1.2f)]
    public void LegacyNpcSampleKeepsFractionalDamageAndIntMultiplyOrder(int attack, int percent, float expected)
        => Assert.Equal(expected, StatFunctions.CalculateNpcNormalSample(attack, percent));
}
