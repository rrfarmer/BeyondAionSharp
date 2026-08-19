using Aion.GameServer.Handlers.AI;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Sematariux lays one egg, and retail's own data contains a second rung that never fires.
/// </summary>
/// <remarks>
/// <c>LF4_Dramata</c>'s <c>on_arrived_at_waypoint</c> has two rungs, both <c>PLANNED</c>, both guarded
/// by nothing but <c>is_waypoint_index 1</c>:
/// <list type="bullet">
/// <item><description>priority 100 — <c>num_to_spawn 1</c></description></item>
/// <item><description>priority 99 — <c>num_to_spawn 2</c></description></item>
/// </list>
/// Evaluation takes the highest priority whose conditions pass and stops, and the higher rung has no
/// probability on it, so <b>the two-egg rung can never fire</b>. It is dead code in NCSoft's data.
/// <para>
/// This pin exists because that is exactly the kind of thing a future reader corrects in the wrong
/// direction: opening the pattern, seeing a two-egg branch we do not implement, and "fixing" it. The
/// single egg is right, and the reason is a priority ordering three lines above the number.
/// </para>
/// <para>
/// Her sibling <c>DF4_Dramata</c> shows what a live version looks like: its two-egg rung carries
/// <c>test_probability 25</c>, so the one-egg rung beneath it is a real fallback rather than a shadow.
/// </para>
/// </remarks>
// Joined to the collection even though these two pins touch no shared data: outside it the class runs in
// parallel with every test that does, and a single unexplained failure appeared in one whole-solution run
// while it was unattached. That is the same shape as the ordering flake this suite has already paid for
// once, and serialising two assertions costs nothing.
[Collection("GoldenDataManager")]
public sealed class SematariuxEggCountTests
{
	/// <summary>
	/// <b>One egg per laying.</b> Written as a plain assertion on the class's own behaviour rather than
	/// on retail's number, because retail's file contains both numbers and only one of them is reachable.
	/// </summary>
	[Fact]
	public void SheLaysOneEggNotTwo()
	{
		Assert.Equal(1, SematariuxAI.EggsPerLaying);
	}

	/// <summary>
	/// <b>And the egg stands ten minutes.</b> Retail's <c>live_time=600</c>; ours never removed it at all
	/// before that was corrected, and a permanent egg is a permanent extra NPC in an open-world map.
	/// </summary>
	[Fact]
	public void TheEggStandsTenMinutes()
	{
		Assert.Equal(600, SematariuxAI.EggLifeSeconds);
	}
}
