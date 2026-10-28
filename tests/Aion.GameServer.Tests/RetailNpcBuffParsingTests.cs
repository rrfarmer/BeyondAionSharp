using System.Security.Cryptography;
using System.Text;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Retail;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcBuffParsingTests
{
    private static (byte[] Bytes, string Pin) Source(string body)
    {
        byte[] bytes = Encoding.Unicode.GetBytes(body);
        return (bytes, Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }

    [Theory]
    [InlineData(5, "7")]
    [InlineData(8, "8")]
    [InlineData(10, "Fire")]
    [InlineData(11, "100")]
    [InlineData(12, "12")]
    [InlineData(23, "23")]
    [InlineData(24, "24")]
    public void AncillaryFieldsNotReadByNativeStatupApplyDoNotDisableTheNumericProgram(int field, string value)
    {
        var (bytes, pin) = Source($"<skill_base><id>1</id><name>Fixture</name><effect1_type>StatUp</effect1_type>"
            + $"<effect1_reserved{field}>{value}</effect1_reserved{field}><effect1_reserved13>physicalDefend</effect1_reserved13>"
            + "<effect1_reserved2>200</effect1_reserved2></skill_base>");
        var program = Assert.Single(RetailNpcStatBuffs.Parse(bytes, pin).Programs);
        Assert.True(program.Supported);
        Assert.Equal(200, RetailNpcStatBuffs.Value(Assert.Single(program.Parts), 1));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("-1")]
    [InlineData("other")]
    public void TheDistinctReservedNineTargetWriterRemainsAnExplicitBoundary(string value)
    {
        var (bytes, pin) = Source("<skill_base><id>1</id><name>Fixture</name><effect1_type>StatUp</effect1_type>"
            + $"<effect1_reserved9>{value}</effect1_reserved9><effect1_reserved13>physicalDefend</effect1_reserved13>"
            + "<effect1_reserved2>200</effect1_reserved2></skill_base>");
        var program = Assert.Single(RetailNpcStatBuffs.Parse(bytes, pin).Programs);
        Assert.False(program.Supported); Assert.Empty(program.Parts);
        Assert.Contains("reserved9", program.UnsupportedReason);
    }

    [Theory]
    [InlineData("<skill_base><id>1</id><id>2</id><name>Fixture</name></skill_base>")]
    [InlineData("<skill_base><id>-1</id><name>Fixture</name></skill_base>")]
    [InlineData("<skill_base><id>1</id><name> </name></skill_base>")]
    [InlineData("<skill_base region='USA'><id>1</id><name>Fixture</name></skill_base>")]
    public void ASourceDigestDoesNotReplaceIdentityAndXmlShapeValidation(string xml)
    {
        var (bytes, pin) = Source(xml);
        Assert.Throws<InvalidDataException>(() => RetailNpcSkillRecord.Read(bytes, pin));
    }
}
