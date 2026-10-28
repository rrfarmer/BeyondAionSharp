using System.Security.Cryptography;
using System.Text;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcBuffScalarMathTests
{
    [Theory]
    [InlineData(2310, -80, 0, 1155)]
    [InlineData(2311, -80, 0, 1155)]
    [InlineData(600, -80, 0, 500)]
    [InlineData(10000, -100, 0, 5000)]
    [InlineData(500, 20000, 10000, 1000)]
    [InlineData(2000, 0, -10, 1990)]
    [InlineData(2000, -3, 0, 1940)]
    public void NativeDelayAppliesGlobalThenOriginalRelativeBounds(int basis, int percent, int flat, int expected) =>
        Assert.Equal(expected, RetailNpcBuffScalarMath.AttackDelay(basis, percent, flat));

    [Theory]
    [InlineData(6000, 99, 0, 11940)]
    [InlineData(6000, -200, 0, 600)]
    [InlineData(3000, -100, 0, 300)]
    [InlineData(3000, 0, 10000, 6000)]
    [InlineData(12000, 0, 10000, 16000)]
    [InlineData(1000, 1000, 0, 2000)]
    [InlineData(4500, -50, 0, 2250)]
    [InlineData(0, 0, 10000, 0)]
    public void NativeSpeedPreservesSseOrderAndAbsoluteThenRelativeBounds(float basis, int percent, int flat, int expected) =>
        Assert.Equal(expected, (int)RetailNpcBuffScalarMath.Speed(basis, percent, flat));

    [Fact]
    public void ArAllWritesExactlyTheNativeTwentyFiveSlots()
    {
        byte[] raw = Encoding.Unicode.GetBytes("<skill_base><id>1</id><name>Fixture</name><effect1_type>StatUp</effect1_type>"
            + "<effect1_reserved13>ArAll</effect1_reserved13><effect1_reserved2>10000</effect1_reserved2></skill_base>");
        var source = RetailNpcStatBuffs.Parse(raw, Convert.ToHexStringLower(SHA256.HashData(raw)));
        var program = Assert.Single(source.Programs);
        Assert.True(program.Supported); Assert.Equal(25, program.Parts.Length);
        for (int i = 0; i < 25; i++)
        {
            Assert.Equal($"abnormal_resist[{i}]", SkyTempleNpcProfile.AbnormalField(program.Parts[i].Stat));
            Assert.Equal(10000, program.Parts[i].Value);
            Assert.False(program.Parts[i].Percent);
        }
        Assert.Equal(StatEnum.SIMPLEROOT_RESISTANCE, program.Parts[^1].Stat);
    }

    [Fact]
    public void NativeDelaySubtractionIsTranslatedOnceWhileCastingRetainsReverseOwner()
    {
        byte[] raw = Encoding.Unicode.GetBytes("<skill_base><id>1</id><name>Fixture</name><effect1_type>StatUp</effect1_type>"
            + "<effect1_reserved13>AttackDelay</effect1_reserved13><effect1_reserved2>80</effect1_reserved2><effect1_reserved6>1</effect1_reserved6>"
            + "<effect1_reserved14>BoostCastingTime</effect1_reserved14><effect1_reserved4>3</effect1_reserved4><effect1_reserved7>1</effect1_reserved7></skill_base>");
        var program = Assert.Single(RetailNpcStatBuffs.Parse(raw, Convert.ToHexStringLower(SHA256.HashData(raw))).Programs);
        Assert.True(program.Supported);
        Assert.Equal(-80, Assert.Single(program.Parts, p => p.Stat == StatEnum.ATTACK_SPEED).Value);
        Assert.Equal(3, Assert.Single(program.Parts, p => p.Stat == StatEnum.BOOST_CASTING_TIME).Value);
    }
}
