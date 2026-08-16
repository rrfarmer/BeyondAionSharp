using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="NTrapAI"/>, translated from retail pattern <c>NTrap_A</c>
/// (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Fifty-three NPCs bind this pattern. What is pinned here is the shape they all share: appear, cast
/// the one skill, leave — and, just as importantly, leave nothing queued behind, which is what the
/// whole class did before the runtime learned to cast outside combat.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class NTrapAiTests
{
	private const int DarkPoeta = 300040000;

	/// <summary>Tahabata's flame center. 18221 "Flame Shower".</summary>
	private const int FlameCenter = 281261;

	/// <summary>What Tahabata leaves where he falls. 18224 "Final Blow".</summary>
	private const int PrimalDragon = 281265;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(DarkPoeta).WithWorldSize(2048)
			.WithAi(typeof(NTrapAI), typeof(AggressiveNpcAI)).Build();

	/// <summary>
	/// It goes off on appearing and leaves when the cast lands — not before. Retail's <c>use_skill</c>
	/// and <c>despawn_self</c> are both PLANNED, so the despawn is queued behind the cast; removing the
	/// NPC in the same breath would take it out of the world while its own skill was still in flight.
	/// </summary>
	[Theory]
	[InlineData(FlameCenter)]
	[InlineData(PrimalDragon)]
	public void ItGoesOffOnAppearingAndLeavesWhenTheCastLands(int npcId)
	{
		BossAiHarness harness = NewHarness();
		using BossAiHarness _h = harness;

		Npc trap = harness.Spawn(npcId, 1177f, 1241f, 143f);
		Assert.True(trap.IsSpawned(), "it should stand for as long as its skill takes");

		harness.Clock.Advance(TimeSpan.FromSeconds(5));

		Assert.False(trap.IsSpawned());
		Assert.Empty(harness.LiveNpcs().Where(n => n.GetNpcId() == npcId));
	}

	/// <summary>
	/// And it does not leave its cast sitting in the queue. This is the whole reason the class exists:
	/// the queue is drained by the attack loop and only while the NPC has a target it hates, so a
	/// marker that never fights would queue its one skill and never fire it. Every one of these NPCs
	/// was doing exactly that — or rather, was on plain <c>aggressive</c> and not casting at all.
	/// </summary>
	[Fact]
	public void ItDoesNotLeaveTheCastSittingInTheQueue()
	{
		BossAiHarness harness = NewHarness();
		using BossAiHarness _h = harness;

		Npc trap = harness.Spawn(FlameCenter, 1177f, 1241f, 143f);

		Assert.Empty(BossAiHarness.DrainQueuedSkills(trap));
	}

	/// <summary>
	/// It stands where it was put until it goes off, rather than walking anywhere. Retail places these
	/// on fixed marks and the mark is the mechanic — a flame patch that wanders is not a flame patch.
	/// </summary>
	[Fact]
	public void ItGoesOffWhereItWasPut()
	{
		BossAiHarness harness = NewHarness();
		using BossAiHarness _h = harness;

		Npc trap = harness.Spawn(FlameCenter, 1177f, 1241f, 143.322f);

		Assert.Equal(1177f, trap.GetX());
		Assert.Equal(1241f, trap.GetY());
	}
}
