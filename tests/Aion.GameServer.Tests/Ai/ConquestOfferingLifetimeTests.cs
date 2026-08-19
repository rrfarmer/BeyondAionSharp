using Aion.GameServer.Handlers.AI;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The conquest offering portal and buff npc, which both stood for sixty-five seconds.
/// </summary>
/// <remarks>
/// Neither number was retail's. The portal's own pattern sets an idle timer of <b>180000</b> and
/// despawns on it; the buff npc's sets <b>60000</b> on every rung of its wake-up ladder. Sixty-five
/// belongs to neither, and appears in both — one value used for two npcs that share an instance and
/// nothing else.
/// <para>
/// <b>Table pins.</b> Both npcs are placed by instance code and removed by a plain scheduled delete
/// rather than by anything the harness can drive; there is no fight to run. What is fixed here is the
/// two numbers and the fact that they differ.
/// </para>
/// </remarks>
public sealed class ConquestOfferingLifetimeTests
{
	/// <summary>
	/// <b>The portal stands three minutes.</b>
	/// </summary>
	/// <remarks>
	/// Retail's <c>on_wake_up</c> sets 180000 and <c>on_idle_timer</c> is a bare <c>despawn_self</c>;
	/// the rotation monster that drops it spawns it with <c>live_time=0</c>, so the portal's own clock
	/// is the only one on it. At sixty-five seconds it closed before a group could finish the fight it
	/// came from.
	/// </remarks>
	[Fact]
	public void ThePortalStandsThreeMinutes()
	{
		Assert.Equal(180_000L, ConquestOfferingPortalAI.PortalLifeMillis);
	}

	/// <summary>
	/// <b>And the buff npc one minute.</b>
	/// </summary>
	[Fact]
	public void AndTheBuffNpcOneMinute()
	{
		Assert.Equal(60_000L, ConquestOfferingBuffNpcAI.BuffNpcLifeMillis);
	}

	/// <summary>
	/// <b>And the two are not the same number.</b>
	/// </summary>
	/// <remarks>
	/// The defect was one value shared between them. Pinned so that a future tidy-up cannot quietly
	/// reintroduce it by hoisting a constant.
	/// </remarks>
	[Fact]
	public void AndTheTwoAreNotTheSameNumber()
	{
		Assert.NotEqual(ConquestOfferingPortalAI.PortalLifeMillis,
			ConquestOfferingBuffNpcAI.BuffNpcLifeMillis);
	}
}
