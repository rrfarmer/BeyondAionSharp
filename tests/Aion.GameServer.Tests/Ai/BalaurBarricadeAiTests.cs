using Aion.GameServer.Ai.Event;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="BalaurBarricadeAI"/>, translated from retail patterns <c>ND2_H50_3</c>,
/// <c>ND2_H50_4</c> and <c>ND2_KnQ</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Dark Poeta's three barricades. aionemu had two of them holding each other's reinforcement
/// positions, all four guards drawn from the wrong templates, and a health ladder where retail runs a
/// six-second poll — so the pins here are mostly about <em>where</em> and <em>which</em>, which is
/// unusual for an encounter and is the whole point of this one.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class BalaurBarricadeAiTests
{
	private const int DarkPoeta = 300040000;

	private const int BarricadeA = 700517;
	private const int BarricadeB = 700556;
	private const int BarricadeC = 700558;

	private const int Fighter = 215452;
	private const int Knight = 215453;
	private const int Wizard = 215451;

	// The three aionemu reached for instead. Same names on screen, different templates.
	private const int WorldProconsul = 215262;
	private const int WorldPraefectus = 215263;
	private const int WorldMagist = 214883;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(DarkPoeta).WithWorldSize(2048)
			.WithAi(typeof(BalaurBarricadeAI), typeof(AggressiveNpcAI))
			.Build();

	/// <summary>
	/// A barricade is <c>onedmg_passive</c>: it never moves and never fights back, so the player only
	/// has to be known to it and adjacent enough to hold the fight open.
	/// </summary>
	private static (BossAiHarness, Npc, Player) Engaged(int npcId, float x, float y, float z)
	{
		BossAiHarness harness = NewHarness();
		Npc barricade = harness.Spawn(npcId, x, y, z);
		Player player = harness.SpawnPlayer(x + 3f, y, z);
		harness.Engage(barricade, player);
		return (harness, barricade, player);
	}

	private static void Advance(BossAiHarness harness, Npc barricade, Player player, int seconds)
	{
		for (int i = 0; i < seconds; i++)
		{
			BossAiHarness.Rehate(barricade, player);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}
	}

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	private static List<Npc> Of(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Where(n => n.GetNpcId() == npcId).ToList();

	private static void AssertAt(SpawnSpot expected, Npc actual)
	{
		Assert.Equal(expected.X, actual.GetX(), 2);
		Assert.Equal(expected.Y, actual.GetY(), 2);
		Assert.Equal(expected.Z, actual.GetZ(), 2);
		Assert.Equal(expected.Heading, (sbyte)actual.GetHeading());
	}

	/// <summary>
	/// The postings, read straight off the three retail patterns. This is the pin that catches the
	/// transposition: 700517's fighters belong at (315, 982) and (308, 990) — where aionemu put
	/// <b>700556</b>'s — and 700556's at (290.71, 1002.67) and (284.28, 1004.98).
	/// </summary>
	[Theory]
	[InlineData(BarricadeA, 315f, 982f, 111f, 308f, 990f, 113f)]
	[InlineData(BarricadeB, 290.71f, 1002.67f, 113.36f, 284.28f, 1004.98f, 113.3f)]
	[InlineData(BarricadeC, 202f, 856f, 102f, 201f, 843f, 100f)]
	public void EachBarricadePostsItsFightersWhereRetailDoes(
		int barricade, float x1, float y1, float z1, float x2, float y2, float z2)
	{
		Assert.True(BalaurBarricadeAI.TryGetPosting(barricade, out SpawnSpot[] fighters, out _, out _));

		Assert.Equal(2, fighters.Length);
		Assert.Equal(x1, fighters[0].X, 2);
		Assert.Equal(y1, fighters[0].Y, 2);
		Assert.Equal(z1, fighters[0].Z, 2);
		Assert.Equal(x2, fighters[1].X, 2);
		Assert.Equal(y2, fighters[1].Y, 2);
		Assert.Equal(z2, fighters[1].Z, 2);
	}

	/// <summary>
	/// Stated as its own pin because it is the bug rather than a consequence of it: the two
	/// transposed barricades stand roughly thirty metres apart, so neither set of coordinates is
	/// anywhere near the other barricade.
	/// </summary>
	[Fact]
	public void TheTwoTransposedBarricadesDoNotShareAPosting()
	{
		Assert.True(BalaurBarricadeAI.TryGetPosting(BarricadeA, out SpawnSpot[] a, out _, out _));
		Assert.True(BalaurBarricadeAI.TryGetPosting(BarricadeB, out SpawnSpot[] b, out _, out _));

		foreach (SpawnSpot one in a)
			foreach (SpawnSpot other in b)
				Assert.True(Math.Abs(one.X - other.X) + Math.Abs(one.Y - other.Y) > 20f,
					$"({one.X}, {one.Y}) and ({other.X}, {other.Y}) are the same posting");
	}

	/// <summary>
	/// Retail writes headings in degrees and the client wants 0..120. 141° is 47, not 141 — a raw copy
	/// would overflow past a full turn and land two of the six guards facing backwards.
	/// </summary>
	[Fact]
	public void HeadingsAreRetailsDegreesInClientUnits()
	{
		Assert.True(BalaurBarricadeAI.TryGetPosting(BarricadeA, out SpawnSpot[] fighters,
			out SpawnSpot knight, out SpawnSpot wizard));

		Assert.Equal((sbyte)47, fighters[0].Heading);   // 141
		Assert.Equal((sbyte)108, fighters[1].Heading);  // 324
		Assert.Equal((sbyte)22, knight.Heading);        // 66
		Assert.Equal((sbyte)75, wizard.Heading);        // 225
	}

	/// <summary>Nothing at all above seventy percent, however long the fight runs.</summary>
	[Fact]
	public void ABarricadeAboveSeventyCallsNobody()
	{
		var (harness, barricade, player) = Engaged(BarricadeA, 300f, 300f, 200f);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(barricade, 71);
		Advance(harness, barricade, player, 30);

		Assert.Equal(0, Count(harness, Fighter));
	}

	/// <summary>
	/// Below seventy it calls two fighters, and they stand exactly where the pattern says.
	/// </summary>
	[Fact]
	public void BelowSeventyItCallsTwoFightersToTheirPosts()
	{
		var (harness, barricade, player) = Engaged(BarricadeA, 300f, 300f, 200f);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(barricade, 69);
		Advance(harness, barricade, player, 7);

		List<Npc> called = Of(harness, Fighter);
		Assert.Equal(2, called.Count);

		Assert.True(BalaurBarricadeAI.TryGetPosting(BarricadeA, out SpawnSpot[] posts, out _, out _));
		AssertAt(posts[0], called.Single(n => n.GetX() > 311f));
		AssertAt(posts[1], called.Single(n => n.GetX() < 311f));
	}

	/// <summary>
	/// <b>The call is polled, not instant.</b> Retail arms a six-second timer, so crossing seventy does
	/// not summon on the crossing hit — this is what distinguishes the pattern from the health ladder
	/// it replaced, which fired the moment the threshold was passed.
	/// </summary>
	[Fact]
	public void CrossingSeventyDoesNotSummonUntilTheNextPoll()
	{
		var (harness, barricade, player) = Engaged(BarricadeA, 300f, 300f, 200f);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(barricade, 69);

		Advance(harness, barricade, player, 5);
		Assert.Equal(0, Count(harness, Fighter));

		Advance(harness, barricade, player, 1);
		Assert.Equal(2, Count(harness, Fighter));
	}

	/// <summary>
	/// <b>A barricade that dies inside six seconds never calls its fighters at all.</b> The poll has
	/// not come round once, so only the death pair appears — four guards in retail, two here. No
	/// threshold port can produce this, which is why it is worth a pin of its own.
	/// </summary>
	[Fact]
	public void ABarricadeKilledInsideOnePollNeverCallsItsFighters()
	{
		var (harness, barricade, player) = Engaged(BarricadeA, 300f, 300f, 200f);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(barricade, 5);
		Advance(harness, barricade, player, 3);
		barricade.GetAi().OnGeneralEvent(AiEventType.Died);

		Assert.Equal(0, Count(harness, Fighter));
		Assert.Equal(1, Count(harness, Knight));
		Assert.Equal(1, Count(harness, Wizard));
	}

	/// <summary>
	/// The branch that summons is the one branch that does not re-arm the timer, so the fighters come
	/// once and the clock stops with them. Driven well past the point where a surviving poll would
	/// have fired again.
	/// </summary>
	[Fact]
	public void TheFightersComeOnceAndTheClockStopsWithThem()
	{
		var (harness, barricade, player) = Engaged(BarricadeA, 300f, 300f, 200f);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(barricade, 69);
		Advance(harness, barricade, player, 7);
		Assert.Equal(2, Count(harness, Fighter));

		// Drop it further: a live poll would find a fresh reason to fire on every one of these ticks.
		BossAiHarness.SetExactPercent(barricade, 20);
		Advance(harness, barricade, player, 120);
		Assert.Equal(2, Count(harness, Fighter));
	}

	/// <summary>
	/// Death leaves a knight and a wizard — one each, not a second pair of fighters — at their own
	/// coordinates, which are not the fighters'.
	/// </summary>
	[Theory]
	[InlineData(BarricadeA, 300f, 300f, 200f)]
	[InlineData(BarricadeB, 400f, 400f, 200f)]
	[InlineData(BarricadeC, 500f, 500f, 200f)]
	public void DeathLeavesOneKnightAndOneWizard(int npcId, float x, float y, float z)
	{
		var (harness, barricade, _) = Engaged(npcId, x, y, z);
		using BossAiHarness _h = harness;

		barricade.GetAi().OnGeneralEvent(AiEventType.Died);

		Assert.True(BalaurBarricadeAI.TryGetPosting(npcId, out _, out SpawnSpot knight, out SpawnSpot wizard));
		AssertAt(knight, Assert.Single(Of(harness, Knight)));
		AssertAt(wizard, Assert.Single(Of(harness, Wizard)));
	}

	/// <summary>
	/// <b>The summoned templates, not the ones already standing in Dark Poeta.</b> Retail has a
	/// dedicated trio whose names on screen match the world NPCs pair for pair — which is exactly how
	/// an observed port picks the wrong one — so this asserts the absence as well as the presence.
	/// </summary>
	[Fact]
	public void TheGuardsAreTheSummonedTemplates()
	{
		var (harness, barricade, player) = Engaged(BarricadeA, 300f, 300f, 200f);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(barricade, 69);
		Advance(harness, barricade, player, 7);
		barricade.GetAi().OnGeneralEvent(AiEventType.Died);

		Assert.Equal(2, Count(harness, Fighter));
		Assert.Equal(1, Count(harness, Knight));
		Assert.Equal(1, Count(harness, Wizard));

		Assert.Equal(0, Count(harness, WorldProconsul));
		Assert.Equal(0, Count(harness, WorldPraefectus));
		Assert.Equal(0, Count(harness, WorldMagist));
	}

	/// <summary>
	/// Retail's five minutes, and it is observable: all three guards are plain <c>aggressive</c> NPCs
	/// with no pattern of their own, so nothing removes them earlier than the barricade's
	/// <c>live_time</c> does.
	/// </summary>
	[Fact]
	public void AGuardStandsForFiveMinutes()
	{
		var (harness, barricade, _) = Engaged(BarricadeA, 300f, 300f, 200f);
		using BossAiHarness _h = harness;

		barricade.GetAi().OnGeneralEvent(AiEventType.Died);
		Assert.Equal(1, Count(harness, Knight));

		harness.Clock.Advance(TimeSpan.FromSeconds(299));
		Assert.Equal(1, Count(harness, Knight));

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.Equal(0, Count(harness, Knight));
	}

	/// <summary>A barricade that is not one of the three calls nobody rather than somebody else's guard.</summary>
	[Fact]
	public void AnUnlistedBarricadePostsNobody()
	{
		Assert.False(BalaurBarricadeAI.TryGetPosting(700000, out SpawnSpot[] fighters, out _, out _));
		Assert.Empty(fighters);
	}
}
