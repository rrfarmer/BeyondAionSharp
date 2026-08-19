using Aion.GameServer.Controllers.Attack;

namespace Aion.GameServer.Tests;

/// <summary>
/// Hate arithmetic, and the one value of it that used to invert.
/// </summary>
/// <remarks>
/// <c>AggroInfo.AddHate</c> was <c>_hate += hate</c> on a signed int, followed by a clamp of anything
/// below one back up to one. Those two lines are fine together for every value the server actually
/// produces, and catastrophic for a large one: the addition wraps negative and the clamp then pins the
/// result to <b>1</b>, so the bigger the hate added, the lower the attacker ends up.
/// <para>
/// Retail's <c>switch_target</c> carries <c>points_to_add=2147483647</c> to mean "top of the list", which
/// is how this surfaced. Java has the same arithmetic and never exercises it. Widening to <c>long</c> and
/// saturating leaves every ordinary value untouched and changes only the overflow.
/// </para>
/// </remarks>
public sealed class AggroInfoHateArithmeticTests
{
	/// <summary>
	/// <b>Ordinary additions are unchanged.</b> The clamp still pulls a net-negative total back to one.
	/// </summary>
	[Fact]
	public void OrdinaryHateAddsAndClampsAsBefore()
	{
		AggroInfo info = new AggroInfo(null);

		info.AddHate(500);
		Assert.Equal(500, info.GetHate());

		info.AddHate(250);
		Assert.Equal(750, info.GetHate());

		info.AddHate(-10_000);
		Assert.Equal(1, info.GetHate());
	}

	/// <summary>
	/// <b>A huge addition saturates rather than wrapping to the bottom of the list.</b> This is the whole
	/// defect: with the old arithmetic the assertion below read 1.
	/// </summary>
	[Fact]
	public void AHugeAdditionSaturatesInsteadOfInverting()
	{
		AggroInfo info = new AggroInfo(null);
		info.AddHate(500);

		info.AddHate(int.MaxValue);

		Assert.Equal(int.MaxValue, info.GetHate());
	}

	/// <summary>
	/// <b>And it stays there.</b> Saturation has to be stable, or a second large taunt undoes the first.
	/// </summary>
	[Fact]
	public void SaturatedHateStaysSaturated()
	{
		AggroInfo info = new AggroInfo(null);
		info.AddHate(int.MaxValue);
		info.AddHate(int.MaxValue);

		Assert.Equal(int.MaxValue, info.GetHate());
	}
}
