using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Container;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcIntegerStatTests
{
    [Theory]
    [InlineData("base")]
    [InlineData("base-rate")]
    [InlineData("bonus")]
    [InlineData("bonus-rate")]
    [InlineData("fixed-rate")]
    [InlineData("final-rate")]
    [InlineData("add-base")]
    [InlineData("add-bonus")]
    public void AnySubsequentHookExpiresTheExactInteger(string operation)
    {
        foreach (bool reverse in new[] { false, true })
        {
            Stat2 stat = reverse ? new ReverseStat(StatEnum.MAXHP, 16777216, null!)
                : new AdditionStat(StatEnum.MAXHP, 16777216, null!);
            stat.SetExactIntegerCurrent(16777217);
            Assert.Equal(16777217, stat.GetCurrent());
            switch (operation)
            {
                case "base": stat.SetBase(100); break;
                case "base-rate": stat.SetBaseRate(0.5f); break;
                case "bonus": stat.SetBonus(100); break;
                case "bonus-rate": stat.SetBonusRate(0.5f); break;
                case "fixed-rate": stat.SetFixedBonusRate(0.5f); break;
                case "final-rate": stat.SetFinalRate(0.5f); break;
                case "add-base": stat.AddToBase(1); break;
                case "add-bonus": stat.AddToBonus(1); break;
            }
            Assert.False(stat.HasUnchangedExactIntegerCurrent());
            Assert.Equal((int)stat.GetExactCurrent(), stat.GetCurrent());
        }
    }

    [Fact]
    public void UntaggedStatsRetainLegacyFloatConversion()
    {
        var stat = new AdditionStat(StatEnum.MAXHP, 86954218, null!);
        Assert.False(stat.HasUnchangedExactIntegerCurrent());
        Assert.Equal(86954216, stat.GetCurrent());
        stat.SetExactIntegerCurrent(86954218);
        Assert.Equal(86954218, stat.GetCurrent());
    }

    [Theory]
    [InlineData(100, 50, 20, 170)]
    [InlineData(101, -50, 0, 51)]
    [InlineData(100, -200, 0, 1)]
    [InlineData(100, 0, 2000000000, 2000000000)]
    [InlineData(86954218, 0, 0, 86954218)]
    public void NativeScalarUsesIntegerPercentageThenFlatThenBounds(long seed, int percent, int flat, long expected)
        => Assert.Equal(expected, RetailNpcStatDefenses.Calculate(seed, percent, flat, 1, 2000000000));
}
