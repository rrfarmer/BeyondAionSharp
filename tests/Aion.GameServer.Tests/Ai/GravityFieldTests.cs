using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Terath's gravity fields, which pulsed on landing and then too slowly.
/// </summary>
/// <remarks>
/// Retail's <c>IDTiamat_Sardha_GravityUp</c> is two rungs: <c>on_wake_up</c> sets an idle timer of
/// 2000, and each firing re-arms at 2000. This port opened at zero and repeated at 3250 — no opening
/// delay, and a beat and a half slower than retail.
/// <para>
/// <b>Behavioural, and only because the clock hook is on.</b> These fields cast through
/// <c>AIActions.UseSkill</c>, which is exactly what every class in this family said could not be
/// observed under the virtual clock. It can now: the harness drives the combat model's clock, so
/// <c>GetLastSkillTime</c> moves with it.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class GravityFieldTests
{
	private const int TiamatStronghold = 300510000;

	/// <summary>The up-field, which has a retail pattern, and the down-field, which has none.</summary>
	private const int GravityUp = 283109;
	private const int GravityDown = 283110;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(TiamatStronghold).WithWorldSize(2048)
			.WithAi(typeof(GravityAI), typeof(GeneralNpcAI)).Build();

	/// <summary>
	/// <b>Neither field pulses the moment it lands.</b>
	/// </summary>
	/// <remarks>
	/// An opening delay is what gives someone caught inside a chance to walk out. Both had none.
	/// </remarks>
	[Theory]
	[InlineData(GravityUp)]
	[InlineData(GravityDown)]
	public void NeitherFieldPulsesOnLanding(int npcId)
	{
		using BossAiHarness harness = NewHarness();
		Npc field = harness.Spawn(npcId, 1030f, 297f, 409f);

		harness.Clock.Advance(TimeSpan.FromMilliseconds(1500));

		Assert.Equal(0L, field.GetGameStats().GetLastSkillTime());
	}

	/// <summary>
	/// <b>And both open at two seconds.</b>
	/// </summary>
	[Theory]
	[InlineData(GravityUp)]
	[InlineData(GravityDown)]
	public void AndBothOpenAtTwoSeconds(int npcId)
	{
		using BossAiHarness harness = NewHarness();
		Npc field = harness.Spawn(npcId, 1030f, 297f, 409f);

		harness.Clock.Advance(TimeSpan.FromMilliseconds(2500));

		Assert.NotEqual(0L, field.GetGameStats().GetLastSkillTime());
	}

	/// <summary>
	/// <b>And pulse every two seconds after that, not every three and a quarter.</b>
	/// </summary>
	/// <remarks>
	/// Measured as the gap between two casts rather than as a count: the last-skill timestamp moves on
	/// every pulse, so two readings a known distance apart say what the period is without needing a
	/// window long enough to count.
	/// </remarks>
	[Fact]
	public void AndPulseEveryTwoSecondsAfterThat()
	{
		using BossAiHarness harness = NewHarness();
		Npc field = harness.Spawn(GravityUp, 1030f, 297f, 409f);

		harness.Clock.Advance(TimeSpan.FromMilliseconds(2500));
		long first = field.GetGameStats().GetLastSkillTime();

		// To 4.5 seconds: at retail's two-second beat the second pulse landed at 4000 and the reading
		// has moved; at 3250 the next is not due until 5250 and it has not. One second on would have
		// shown neither, which is what the first draft of this pin did.
		harness.Clock.Advance(TimeSpan.FromMilliseconds(2000));
		long second = field.GetGameStats().GetLastSkillTime();

		Assert.NotEqual(first, second);
	}

	/// <summary>
	/// <b>And the field stands twenty-four seconds.</b>
	/// </summary>
	/// <remarks>
	/// Retail's <c>live_time</c>, already corrected in an earlier pass from Java's twenty. Pinned here
	/// so the cadence work above cannot quietly change it.
	/// </remarks>
	[Fact]
	public void AndTheFieldStandsTwentyFourSeconds()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(GravityUp, 1030f, 297f, 409f);

		harness.Clock.Advance(TimeSpan.FromSeconds(22));
		Assert.Equal(1, harness.LiveNpcs().Count(n => n.GetNpcId() == GravityUp));

		harness.Clock.Advance(TimeSpan.FromSeconds(4));
		Assert.Equal(0, harness.LiveNpcs().Count(n => n.GetNpcId() == GravityUp));
	}
}
