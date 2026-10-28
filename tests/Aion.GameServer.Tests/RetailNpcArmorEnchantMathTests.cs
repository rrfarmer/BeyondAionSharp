using Aion.GameServer.Controllers.Attack.RetailNpc;

namespace Aion.GameServer.Tests;

public class RetailNpcArmorEnchantMathTests
{
    [Theory]
    [InlineData(0,0,0)]
    [InlineData(15,45,45)]
    [InlineData(16,51,48)]
    [InlineData(20,75,60)]
    [InlineData(21,81,63)]
    [InlineData(30,135,90)]
    public void ReviewedRobeTorsoUsesDistinctRetailAndHostThresholds(int level,int retail,int host)
    {
        int retailDirect=level*3,hostDirect=level*3;
        Assert.Equal(retail,RetailNpcArmorEnchantMath.Retail(level,16,45,6,retailDirect));
        Assert.Equal(host,RetailNpcArmorEnchantMath.Host(level,21,60,3,hostDirect));
    }

    [Fact]
    public void UnknownOrOverflowingTableContextsStayVisible()
    {
        Assert.Throws<ArgumentOutOfRangeException>(()=>RetailNpcArmorEnchantMath.Retail(1,0,0,1,1));
        Assert.Throws<ArgumentOutOfRangeException>(()=>RetailNpcArmorEnchantMath.Host(11,10,9,1,10));
        Assert.Throws<ArithmeticException>(()=>RetailNpcArmorEnchantMath.Retail(255,1,int.MaxValue,int.MaxValue,0));
    }
}
