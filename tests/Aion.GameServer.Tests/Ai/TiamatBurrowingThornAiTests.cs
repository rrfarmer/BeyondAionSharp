using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="TiamatBurrowingThornAI"/>, translated from retail pattern
/// <c>IDTiamat_BurrowingWorm_BurrowFX</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
[Collection("GoldenDataManager")]
public sealed class TiamatBurrowingThornAiTests
{
	private const int DragonLordsRefuge = 300520000;
	private const int Thorn = 283057;
	private const int Uplift = 283135;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(DragonLordsRefuge).WithWorldSize(2048)
			.WithAi(typeof(TiamatBurrowingThornAI), typeof(TiamatSkillHelperAI), typeof(AggressiveNpcAI)).Build();

	private static int Sand(BossAiHarness harness) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == Uplift);

	/// <summary>It throws nothing the instant it appears — the first burst is two seconds out.</summary>
	/// <remarks>
	/// Measured at exactly two seconds, not after. Each burst of sand lives a single second, so a
	/// check at three seconds finds an empty field and reads as "it never threw anything" — which is
	/// what the first version of this pin claimed.
	/// </remarks>
	[Fact]
	public void ItWaitsTwoSecondsBeforeTheFirstBurst()
	{
		BossAiHarness harness = NewHarness();
		using BossAiHarness _h = harness;
		harness.Spawn(Thorn, 470f, 514f, 417f);
		Assert.Equal(0, Sand(harness));

		harness.Clock.Advance(TimeSpan.FromSeconds(1));
		Assert.Equal(0, Sand(harness));

		harness.Clock.Advance(TimeSpan.FromSeconds(1));

		Assert.Equal(3, Sand(harness));
	}

	/// <summary>
	/// Five bursts of three, four, three, four and four, at widening intervals, and then it is gone.
	/// </summary>
	/// <remarks>
	/// Counted cumulatively because each burst of sand lives a single second: watching the field at any
	/// one moment sees one burst, never the total. The intervals widen — two, two, two and a half,
	/// three, three and a half — so the whole sequence runs about thirteen seconds.
	/// </remarks>
	[Fact]
	public void ItThrowsFiveBurstsAndThenLeaves()
	{
		BossAiHarness harness = NewHarness();
		using BossAiHarness _h = harness;
		Npc thorn = harness.Spawn(Thorn, 470f, 514f, 417f);

		var bursts = new List<int>();
		int standing = 0;
		for (int i = 0; i < 20; i++)
		{
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
			int now = Sand(harness);
			if (now > standing)
				bursts.Add(now);
			standing = now;
		}

		Assert.Equal([3, 4, 3, 4, 4], bursts);
		Assert.False(thorn.IsSpawned(), "the fifth burst removes it");
	}

	/// <summary>The sand lands on the thorn, which is why the boss's thorn marks are the mechanic.</summary>
	/// <remarks>
	/// The non-empty assertion is doing real work. The first version advanced three seconds — past the
	/// one-second life of the burst — so the loop ran over an empty collection and passed whatever the
	/// scatter was. A mutation widening it from three metres to forty survived until this was fixed:
	/// <b>a foreach over nothing asserts nothing.</b>
	/// </remarks>
	[Fact]
	public void TheSandLandsOnTheThorn()
	{
		BossAiHarness harness = NewHarness();
		using BossAiHarness _h = harness;
		Npc thorn = harness.Spawn(Thorn, 470f, 514f, 417f);

		harness.Clock.Advance(TimeSpan.FromSeconds(2));

		Npc[] sand = harness.LiveNpcs().Where(n => n.GetNpcId() == Uplift).ToArray();
		Assert.Equal(3, sand.Length);
		foreach (Npc grain in sand)
			Assert.True(Math.Abs(grain.GetX() - thorn.GetX()) <= 4f,
				$"sand at {grain.GetX():F1} should be within the thorn's three-metre scatter of {thorn.GetX():F1}");
	}
}
