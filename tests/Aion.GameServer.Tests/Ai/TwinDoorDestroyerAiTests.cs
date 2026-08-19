using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The twin door demolishers, which destroyed the door and then stood there for ever.
/// </summary>
/// <remarks>
/// See <see cref="TwinDoorDestroyerAI"/>. Retail ends the run with <c>is_last_waypoint</c>, a
/// <c>Scene_08</c> bomber on the demolisher's own mark, and <c>despawn_self</c>. The successor is the
/// npc that stays by the ruined door and greets the raid; nothing in this port ever placed it.
/// <para>
/// <b>An earlier version of this file said the handoff could not be pinned.</b> It can:
/// <c>NpcMoveController</c> sets its stop flag in <c>SetRouteStep</c> when the route's
/// <c>loop_type</c> is <c>NONE</c> <i>and</i> the step is the last one. Those two had been varied one
/// at a time and never combined. <see cref="BossAiHarness.FinishWalk"/> states the combination once.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class TwinDoorDestroyerAiTests
{
	private const int DrakenspireDepths = 301390000;

	/// <summary>A non-looping route in the demolishers' own instance; see <see cref="BossAiHarness.FinishWalk"/>.</summary>
	private const string EndingRoute = "301390000_NPCPathFunction_Npc_Path01";

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(DrakenspireDepths).WithWorldSize(2048).WithWalkerRoutes()
			.WithAi(typeof(TwinDoorDestroyerAI), typeof(GeneralNpcAI), typeof(AggressiveNpcAI))
			.Build();

	private static Npc Arrived(BossAiHarness harness, int npcId)
	{
		Npc bomber = harness.Spawn(npcId, 500f, 500f, 200f);
		BossAiHarness.FinishWalk(bomber, EndingRoute);
		harness.Clock.Advance(TimeSpan.FromSeconds(5));
		return bomber;
	}

	/// <summary>
	/// <b>Each demolisher leaves its own side's successor at the door.</b>
	/// </summary>
	[Theory]
	[InlineData(TwinDoorDestroyerAI.ElyosDemolisher, TwinDoorDestroyerAI.ElyosSuccessor)]
	[InlineData(TwinDoorDestroyerAI.AsmodianDemolisher, TwinDoorDestroyerAI.AsmodianSuccessor)]
	public void EachDemolisherLeavesItsSuccessor(int demolisher, int successor)
	{
		using BossAiHarness harness = NewHarness();
		Arrived(harness, demolisher);

		Assert.Equal(1, harness.LiveNpcs().Count(n => n.GetNpcId() == successor));
	}

	/// <summary>
	/// <b>And goes itself.</b> Retail's <c>despawn_self</c> — without it the raid is left with a
	/// demolisher standing at a door it has already destroyed.
	/// </summary>
	[Theory]
	[InlineData(TwinDoorDestroyerAI.ElyosDemolisher)]
	[InlineData(TwinDoorDestroyerAI.AsmodianDemolisher)]
	public void AndTheDemolisherGoes(int demolisher)
	{
		using BossAiHarness harness = NewHarness();
		Arrived(harness, demolisher);

		Assert.Equal(0, harness.LiveNpcs().Count(n => n.GetNpcId() == demolisher));
	}

	/// <summary>
	/// <b>Each demolisher hands off to its own side's successor.</b>
	/// </summary>
	[Fact]
	public void EachDemolisherHandsOffToItsOwnSide()
	{
		Assert.Equal(TwinDoorDestroyerAI.ElyosSuccessor,
			TwinDoorDestroyerAI.SuccessorFor(TwinDoorDestroyerAI.ElyosDemolisher));
		Assert.Equal(TwinDoorDestroyerAI.AsmodianSuccessor,
			TwinDoorDestroyerAI.SuccessorFor(TwinDoorDestroyerAI.AsmodianDemolisher));
	}

	/// <summary>
	/// <b>And the two sides do not share one.</b> The ids differ by a single digit in places, which is
	/// exactly where a transcription slip lands.
	/// </summary>
	[Fact]
	public void TheTwoSidesDoNotShareASuccessor()
	{
		Assert.NotEqual(TwinDoorDestroyerAI.ElyosSuccessor, TwinDoorDestroyerAI.AsmodianSuccessor);
	}

	/// <summary>
	/// <b>An npc that is not one of the two hands off to nothing.</b> The lookup returns 0 rather than
	/// guessing, so a third demolisher added later cannot silently inherit the Elyos successor.
	/// </summary>
	[Fact]
	public void AnUnknownDemolisherHandsOffToNothing()
	{
		Assert.Equal(0, TwinDoorDestroyerAI.SuccessorFor(209999));
	}
}
