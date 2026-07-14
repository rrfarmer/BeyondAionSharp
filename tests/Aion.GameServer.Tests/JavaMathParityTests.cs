using Aion.GameServer.Utils;

namespace Aion.GameServer.Tests;

public sealed class JavaMathParityTests
{
	[Theory]
	[InlineData(0.5f, 1)]
	[InlineData(1.5f, 2)]
	[InlineData(2.5f, 3)]
	[InlineData(-0.5f, 0)]
	[InlineData(-1.5f, -1)]
	[InlineData(-2.5f, -2)]
	public void RoundFloat_MatchesJavaMidpoints(float value, int expected)
	{
		Assert.Equal(expected, JavaMath.Round(value));
	}

	[Theory]
	[InlineData(0.5d, 1L)]
	[InlineData(2.5d, 3L)]
	[InlineData(-0.5d, 0L)]
	[InlineData(-2.5d, -2L)]
	public void RoundDouble_MatchesJavaMidpoints(double value, long expected)
	{
		Assert.Equal(expected, JavaMath.Round(value));
	}

	[Fact]
	public void Round_MatchesJavaSpecialValuesAndSaturation()
	{
		Assert.Equal(0, JavaMath.Round(float.NaN));
		Assert.Equal(int.MaxValue, JavaMath.Round(float.PositiveInfinity));
		Assert.Equal(int.MinValue, JavaMath.Round(float.NegativeInfinity));
		Assert.Equal(0L, JavaMath.Round(double.NaN));
		Assert.Equal(long.MaxValue, JavaMath.Round(double.PositiveInfinity));
		Assert.Equal(long.MinValue, JavaMath.Round(double.NegativeInfinity));
	}

	[Fact]
	public void Round_UsesJavaResultsForGateAndArenaHalfValues()
	{
		Assert.Equal(3, (int)JavaMath.Round(250 * 0.01d));
		Assert.Equal(3, JavaMath.Round(5f / 2));
	}
}
