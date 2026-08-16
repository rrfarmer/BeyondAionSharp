using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="TiamatDyingRotationAI"/>, translated from retail pattern
/// <c>IDTiamat_Tiamat_Dragon_Dying_Named_60_Al</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// The whole point of this port is that the sequence is <b>fixed</b> where the class it replaces
/// rolled a die, so the pins are about order as much as about what appears.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class TiamatDyingRotationAiTests
{
	private const int DragonLordsRefuge = 300520000;
	private const int Tiamat = 219362;

	private const int BeaconLeft = 283155;
	private const int BeaconMiddle = 283156;
	private const int BeaconRight = 283157;
	private const int Thorn = 283057;
	private const int CyclopsCrack = 283139;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(DragonLordsRefuge).WithWorldSize(2048)
			.WithAi(typeof(TiamatDyingRotationAI), typeof(TiamatBurrowingThornAI),
				typeof(TiamatSkillHelperAI), typeof(DivisiveCreationAI), typeof(AggressiveNpcAI)).Build();

	private static (BossAiHarness, Npc, Player) Engaged(int hpPercent)
	{
		BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Tiamat, 470f, 514f, 417f);
		Player player = harness.SpawnPlayer(474f, 514f, 417f);
		BossAiHarness.MakeMutuallyKnown(boss, player);
		BossAiHarness.SetHpPercent(boss, hpPercent);
		harness.Engage(boss, player);
		return (harness, boss, player);
	}

	/// <summary>Runs the clock and records the order beacons appear in, one entry per appearance.</summary>
	private static List<int> BeaconOrder(BossAiHarness harness, Npc boss, Player player, int seconds)
	{
		var seen = new List<int>();
		var standing = new HashSet<int>();
		for (int i = 0; i < seconds; i++)
		{
			BossAiHarness.Rehate(boss, player);
			BossAiHarness.KeepAlive(player);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));

			var now = harness.LiveNpcs()
				.Where(n => n.GetNpcId() is BeaconLeft or BeaconMiddle or BeaconRight)
				.Select(n => n.GetObjectId())
				.ToHashSet();
			foreach (Npc beacon in harness.LiveNpcs()
						.Where(n => n.GetNpcId() is BeaconLeft or BeaconMiddle or BeaconRight))
			{
				if (standing.Add(beacon.GetObjectId()))
					seen.Add(beacon.GetNpcId());
			}

			standing.IntersectWith(now);
		}

		return seen;
	}

	/// <summary>Nothing until she is engaged: the chain is armed by entering combat, seven seconds out.</summary>
	[Fact]
	public void SheTelegraphsNothingBeforeSheIsPulled()
	{
		BossAiHarness harness = NewHarness();
		using BossAiHarness _h = harness;
		harness.Spawn(Tiamat, 470f, 514f, 417f);

		harness.Clock.Advance(TimeSpan.FromSeconds(60));

		Assert.Empty(harness.LiveNpcs().Where(n => n.GetNpcId() == BeaconMiddle));
	}

	/// <summary>
	/// Above 76% the order is middle, middle, left, right — retail's, and eighteen seconds apart.
	/// </summary>
	/// <remarks>
	/// This is the pin the whole port exists for. The class this replaces picked with
	/// <c>Rnd.NextInt(3)</c>, which would produce this exact sequence about one run in eighty.
	/// </remarks>
	[Fact]
	public void TheHealthiestBandBreathesMiddleMiddleLeftRight()
	{
		var (harness, boss, player) = Engaged(90);
		using BossAiHarness _h = harness;

		List<int> order = BeaconOrder(harness, boss, player, 70);

		Assert.Equal([BeaconMiddle, BeaconMiddle, BeaconLeft, BeaconRight], order.Take(4));
	}

	/// <summary>Each beacon stands seven seconds — the warning the raid reads before the breath.</summary>
	[Fact]
	public void EachBeaconStandsForSevenSeconds()
	{
		var (harness, boss, player) = Engaged(90);
		using BossAiHarness _h = harness;

		int alive = 0;
		for (int i = 0; i < 30; i++)
		{
			BossAiHarness.Rehate(boss, player);
			BossAiHarness.KeepAlive(player);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
			if (harness.LiveNpcs().Any(n => n.GetNpcId() == BeaconMiddle))
				alive++;
		}

		// Two beacons inside thirty seconds, seven seconds each, give or take the sampling tick.
		Assert.InRange(alive, 12, 16);
	}

	/// <summary>
	/// Each beacon is placed on the heading retail gives it — dir 17 for left against none for middle.
	/// The heading is what picks the breath's cone, so a beacon facing the wrong way is a lie to the
	/// raid about where to stand.
	/// </summary>
	/// <remarks>
	/// The heading is read <b>the tick the beacon is found</b>, not at the end. A beacon is an
	/// aggressive NPC and turns toward whoever is nearby, so holding the reference and reading later
	/// measures where it has swung to rather than where it was placed: the first version of this pin
	/// read 119 for a beacon placed on 0, and a mutation that flattened every heading to zero passed
	/// because the rotation hid it.
	/// </remarks>
	[Fact]
	public void TheBeaconsCarryTheirRetailHeadings()
	{
		var (harness, boss, player) = Engaged(90);
		using BossAiHarness _h = harness;

		int? middle = null;
		int? left = null;
		for (int i = 0; i < 70 && (middle is null || left is null); i++)
		{
			BossAiHarness.Rehate(boss, player);
			BossAiHarness.KeepAlive(player);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
			middle ??= harness.LiveNpcs()
				.FirstOrDefault(n => n.GetNpcId() == BeaconMiddle)?.GetHeading();
			left ??= harness.LiveNpcs()
				.FirstOrDefault(n => n.GetNpcId() == BeaconLeft)?.GetHeading();
		}

		Assert.NotNull(middle);
		Assert.NotNull(left);
		Assert.Equal(0, middle);
		Assert.NotEqual(0, left);
	}

	/// <summary>
	/// The 51-75 band brings thorn rows between the breaths, which the healthiest band never does.
	/// </summary>
	[Fact]
	public void TheSecondBandAddsThornRows()
	{
		var (harness, boss, player) = Engaged(60);
		using BossAiHarness _h = harness;

		int thorns = 0;
		for (int i = 0; i < 80; i++)
		{
			BossAiHarness.Rehate(boss, player);
			BossAiHarness.KeepAlive(player);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
			thorns = Math.Max(thorns, harness.LiveNpcs().Count(n => n.GetNpcId() == Thorn));
		}

		Assert.True(thorns >= 13, $"a thorn row is thirteen at once, saw {thorns}");
	}

	/// <summary>And the healthiest band brings none of them.</summary>
	[Fact]
	public void TheHealthiestBandBringsNoThorns()
	{
		var (harness, boss, player) = Engaged(90);
		using BossAiHarness _h = harness;

		int thorns = 0;
		for (int i = 0; i < 80; i++)
		{
			BossAiHarness.Rehate(boss, player);
			BossAiHarness.KeepAlive(player);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
			thorns = Math.Max(thorns, harness.LiveNpcs().Count(n => n.GetNpcId() == Thorn));
		}

		Assert.Equal(0, thorns);
	}

	/// <summary>Dying clears everything she placed — retail's <c>Despawn_All</c>.</summary>
	/// <remarks>
	/// She is killed <b>while a beacon is still standing</b>. Running the clock first and then killing
	/// her proves nothing: beacons live seven seconds and thorns remove themselves after five bursts,
	/// so the field empties on its own and the pin passes with the despawn deleted — which is exactly
	/// what a mutation showed.
	/// </remarks>
	[Fact]
	public void DyingClearsWhatSheHasPlaced()
	{
		var (harness, boss, player) = Engaged(60);
		using BossAiHarness _h = harness;

		for (int i = 0; i < 40; i++)
		{
			BossAiHarness.Rehate(boss, player);
			BossAiHarness.KeepAlive(player);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
			if (harness.LiveNpcs().Any(n => n.GetNpcId() is BeaconLeft or BeaconMiddle or BeaconRight))
				break;
		}

		Assert.NotEmpty(harness.LiveNpcs().Where(
			n => n.GetNpcId() is BeaconLeft or BeaconMiddle or BeaconRight));

		boss.GetAi().OnGeneralEvent(AiEventType.Died);

		Assert.Empty(harness.LiveNpcs().Where(
			n => n.GetNpcId() is BeaconLeft or BeaconMiddle or BeaconRight or Thorn or CyclopsCrack));
	}
}
