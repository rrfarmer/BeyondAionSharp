using System.Reflection;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Tests;

public sealed class DrownPeriodPortTests
{
    [Fact]
    public void DrownPeriodIsOneSecond()
    {
        var field = typeof(ZoneLevelService).GetField("DROWN_PERIOD", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(ZoneLevelService).FullName, "DROWN_PERIOD");
        Assert.Equal(1000L, field.GetRawConstantValue());
    }
}
