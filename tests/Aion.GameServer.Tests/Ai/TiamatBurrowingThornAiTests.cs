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

	/// <summary>Hard mode's thorn and its own sand.</summary>
	private const int HardThorn = 856040;
	private const int HardUplift = 856041;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(DragonLordsRefuge).WithWorldSize(2048)
			.WithAi(typeof(TiamatBurrowingThornAI), typeof(TiamatSkillHelperAI), typeof(AggressiveNpcAI)).Build();

	private static int Sand(BossAiHarness harness, int uplift = Uplift) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == uplift);

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

	/// <summary>
	/// The hard-mode thorn runs its own sequence and never throws normal mode's sand.
	/// </summary>
	/// <remarks>
	/// Worth its own pin because the devname invites the mistake: 856040 is called
	/// <c>…BurrowingWorm_BurrowFX_Hard</c>, which reads as the same NPC with a suffix, but it binds
	/// <c>IDTiamat_Hard_Earthquake_00</c> and throws <c>…Uplift_Hard</c>. Pointing it at the normal
	/// class — which the name encourages — would have put normal-mode sand in the hard fight and
	/// nothing would have looked wrong.
	/// <para>
	/// <b>The hard sand cannot be counted here, and why is a finding rather than a limitation.</b>
	/// 856041 carries no <c>npc_skills</c> entry and sits on <c>useSkillAndDie</c>, which deletes an
	/// NPC with an empty skill list the instant it spawns — so hard mode's hazard is inert on our
	/// server today. Registering that AI in this harness is also what made two unrelated bootstrap
	/// tests fail, so it is deliberately not registered and the sand simply never materialises. Both
	/// findings are written up in docs/retail-ai-fidelity.md.
	/// </para>
	/// <para>
	/// What is pinned is what this class is responsible for and what a mutation can reach: the hard
	/// thorn runs its own sequence, and no normal-mode sand appears at any point during it.
	/// </para>
	/// </remarks>
	[Fact]
	public void TheHardThornRunsItsOwnSequence()
	{
		BossAiHarness harness = NewHarness();
		using BossAiHarness _h = harness;
		Npc thorn = harness.Spawn(HardThorn, 470f, 514f, 417f);

		// Sampled every second: the sand lives one second, so checking between bursts sees an empty
		// field whatever was thrown — which is how a mutation that made this thorn throw normal-mode
		// sand first slipped through.
		int normalSandSeen = 0;
		for (int i = 0; i < 12; i++)
		{
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
			normalSandSeen += Sand(harness, Uplift);
		}

		Assert.Equal(0, normalSandSeen);
		Assert.True(thorn.IsSpawned(), "it should still be working through its bursts");
	}
}
