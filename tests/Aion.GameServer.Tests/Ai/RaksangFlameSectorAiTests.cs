using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The Raksang flame quadrants, which were laid out correctly and never lit.
/// </summary>
/// <remarks>
/// Retail walks a fire deliverer to its brazier; at the last waypoint it casts, <b>broadcasts 12501 at
/// eighty metres</b> and despawns, and the quadrant's thirty-two permanent floor markers each answer by
/// putting a torment blaze on themselves for ten seconds.
/// <para>
/// This port places the markers itself, at the same coordinates, because our spawn tables carry no
/// permanent ones — so being placed is this port's version of hearing the broadcast. But all four marker
/// npcs were bound to <c>general</c>: <b>no blaze, no lifetime, nothing</b>. Every delivery left
/// thirty-two invisible npcs standing on the floor for the rest of the instance and lit no fire at all.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class RaksangFlameSectorAiTests
{
	private const int RaksangRuins = 300610000;

	/// <summary>The four quadrants' markers.</summary>
	private const int SectorOne = 282455;
	private const int SectorTwo = 282456;
	private const int SectorThree = 282457;
	private const int SectorFour = 282458;

	/// <summary><c>BIDRaksha_BossFlame</c>.</summary>
	private const int TormentBlaze = 282459;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(RaksangRuins).WithWorldSize(2048)
			.WithAi(typeof(RaksangFlameSectorAI), typeof(GeneralNpcAI), typeof(AggressiveNpcAI))
			.Build();

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>
	/// <b>A marker lights a blaze on itself the moment it is placed.</b>
	/// </summary>
	/// <remarks>
	/// Retail's <c>spawn_range</c> is 1, so the blaze lands on the marker rather than around it.
	/// </remarks>
	[Theory]
	[InlineData(SectorOne)]
	[InlineData(SectorTwo)]
	[InlineData(SectorThree)]
	[InlineData(SectorFour)]
	public void EveryMarkerLightsABlazeWhereItStands(int marker)
	{
		using BossAiHarness harness = NewHarness();
		Npc placed = harness.Spawn(marker, 790f, 970f, 792f);

		Npc blaze = Assert.Single(harness.LiveNpcs(), n => n.GetNpcId() == TormentBlaze);
		Assert.Equal(placed.GetX(), blaze.GetX(), 1);
		Assert.Equal(placed.GetY(), blaze.GetY(), 1);
	}

	/// <summary>
	/// <b>And the blaze burns out after retail's ten seconds.</b>
	/// </summary>
	[Fact]
	public void TheBlazeBurnsForTenSeconds()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(SectorOne, 790f, 970f, 792f);

		harness.Clock.Advance(TimeSpan.FromSeconds(9));
		Assert.Equal(1, Count(harness, TormentBlaze));

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.Equal(0, Count(harness, TormentBlaze));
	}

	/// <summary>
	/// <b>A whole quadrant lights together and goes out together.</b>
	/// </summary>
	/// <remarks>
	/// The count matters as much as the behaviour: a delivery places thirty-two markers, so thirty-two
	/// blazes should appear, and thirty-two markers should leave. Nothing removed them before.
	/// </remarks>
	[Fact]
	public void AQuadrantOfMarkersLightsAndClearsTogether()
	{
		using BossAiHarness harness = NewHarness();

		for (int i = 0; i < 32; i++)
			harness.Spawn(SectorTwo, 800f + i, 980f, 792f);

		Assert.Equal(32, Count(harness, TormentBlaze));

		harness.Clock.Advance(TimeSpan.FromSeconds(11));
		Assert.Equal(0, Count(harness, TormentBlaze));
	}
}
