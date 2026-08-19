using Aion.GameServer.Handlers.AI;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The twin door demolishers, which destroyed the door and then stood there for ever.
/// </summary>
/// <remarks>
/// See <see cref="TwinDoorDestroyerAI"/>. Retail ends the run with <c>is_last_waypoint</c>, a
/// <c>Scene_08</c> bomber on the demolisher's own mark, and <c>despawn_self</c>. The successor is the
/// npc that stays by the ruined door and greets the raid; nothing in this port ever placed it.
/// <para>
/// <b>The handoff itself is not pinned, and that is stated rather than faked.</b> It runs from
/// <c>HandleMoveArrived</c> behind <c>IsStop()</c>, which the move controller sets <i>only</i> when it
/// advances onto the last step of a route whose <c>loop_type</c> is <c>NONE</c>. Four attempts to
/// reproduce that in the harness failed: <c>SetRouteStep</c> does not set the flag, raising
/// <c>MoveArrived</c> does not advance the step, and a route with no <c>loop_type</c> defaults to
/// <c>NORMAL</c> and never stops at all. What is missing is a way to make an NPC genuinely finish a
/// walk — see docs/retail-ai-fidelity.md.
/// </para>
/// <para>
/// What is pinned is the table the handoff reads, which is where a paste error would land.
/// </para>
/// </remarks>
public sealed class TwinDoorDestroyerAiTests
{
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
