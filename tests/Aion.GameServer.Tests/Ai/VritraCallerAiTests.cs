using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="VritraCallerAI"/>, translated from the retail
/// <c>BIDRuneWP_Main_CallVritra*</c> patterns (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Eight invisible controllers standing in Infinity Shard, in our spawn data and doing nothing.
/// Two shapes: a weighted cascade that picks one trooper, and a squad that puts three out at once.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class VritraCallerAiTests
{
	private const int InfinityShard = 300800000;

	/// <summary>A cascade controller — ten weighted branches and a guaranteed fallback.</summary>
	private const int CascadeCaller = 284675;

	/// <summary>A squad controller — one unguarded branch, three troopers.</summary>
	private const int SquadCaller = 284677;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(InfinityShard).WithWorldSize(2048)
			.WithAi(typeof(VritraCallerAI), typeof(HyperionDefenceAI), typeof(AggressiveNpcAI)).Build();

	/// <remarks>
	/// <see cref="HyperionDefenceAI"/> is registered because the troopers these callers place are
	/// Hyperion's defence force, and they carry that class since retail's <c>21101</c> dismissal was
	/// built. A harness only knows the AI classes it is handed, so a template repointed anywhere else
	/// in the project silently stops spawning here — which is exactly what happened, and cost five
	/// pins until the reason was found.
	/// </remarks>
	private static int Troopers(BossAiHarness harness, int caller) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() != caller);

	/// <summary>
	/// The cascade puts <b>exactly one</b> trooper out, every time. Ten branches roll in order and
	/// evaluation stops at the first that passes; the unguarded branch beneath them is what makes it
	/// exactly one rather than nought to ten.
	/// </summary>
	/// <remarks>
	/// Run twenty times because it is the guarantee being pinned, not a single draw — a reading that
	/// rolled all ten branches independently would average about two and would show three or more
	/// often enough to fail this.
	/// </remarks>
	[Fact]
	public void TheCascadeCallsExactlyOneTrooperEveryTime()
	{
		for (int run = 0; run < 20; run++)
		{
			BossAiHarness harness = NewHarness();
			using (harness)
			{
				harness.Spawn(CascadeCaller, 150f, 145f, 125f);

				Assert.Equal(1, Troopers(harness, CascadeCaller));
			}
		}
	}

	/// <summary>
	/// And it picks among its options rather than always taking the fallback.
	/// </summary>
	/// <remarks>
	/// The pin that matters alongside "exactly one": a table read in the wrong order — fallback first,
	/// where retail puts it last — still produces exactly one trooper every time, and produces the
	/// same one every time. Only counting distinct troopers over many runs tells the two apart. The
	/// cascade's first branch is a one-in-five, so twenty runs seeing a single id would be about a
	/// one-in-eighty-thousand accident.
	/// </remarks>
	[Fact]
	public void TheCascadePicksAmongItsOptions()
	{
		var seen = new HashSet<int>();
		for (int run = 0; run < 20; run++)
		{
			BossAiHarness harness = NewHarness();
			using (harness)
			{
				harness.Spawn(CascadeCaller, 150f, 145f, 125f);
				seen.Add(harness.LiveNpcs().First(n => n.GetNpcId() != CascadeCaller).GetNpcId());
			}
		}

		Assert.True(seen.Count > 1, $"the cascade should pick among its options, saw only {seen.Count}");
	}

	/// <summary>The squad shape puts three out at once, and one of them stands apart.</summary>
	[Fact]
	public void TheSquadCallerPutsThreeOutAtOnce()
	{
		BossAiHarness harness = NewHarness();
		using BossAiHarness _h = harness;

		harness.Spawn(SquadCaller, 150f, 145f, 125f);

		Npc[] troopers = harness.LiveNpcs().Where(n => n.GetNpcId() != SquadCaller).ToArray();
		Assert.Equal(3, troopers.Length);
		Assert.Equal(2, troopers.Select(t => (t.GetX(), t.GetY())).Distinct().Count());
	}

	/// <summary>
	/// The controller retires two seconds after its call. It is invisible furniture whose only job is
	/// the call, and leaving it standing would leave eight of them in the room.
	/// </summary>
	[Fact]
	public void TheControllerRemovesItselfTwoSecondsLater()
	{
		BossAiHarness harness = NewHarness();
		using BossAiHarness _h = harness;
		Npc caller = harness.Spawn(CascadeCaller, 150f, 145f, 125f);
		Assert.True(caller.IsSpawned());

		harness.Clock.Advance(TimeSpan.FromSeconds(1));
		Assert.True(caller.IsSpawned(), "it should still be there a second in");

		harness.Clock.Advance(TimeSpan.FromSeconds(2));

		Assert.False(caller.IsSpawned());
	}

	/// <summary>
	/// The trooper lands where retail drops it, not where the controller happens to stand. Retail
	/// places absolutely, and the controller's own position is incidental.
	/// </summary>
	[Fact]
	public void TheTrooperLandsOnRetailsMarkNotOnTheController()
	{
		BossAiHarness harness = NewHarness();
		using BossAiHarness _h = harness;

		harness.Spawn(CascadeCaller, 400f, 400f, 125f);

		Npc trooper = harness.LiveNpcs().First(n => n.GetNpcId() != CascadeCaller);
		Assert.Equal(150.03f, trooper.GetX(), 2);
		Assert.Equal(145.5f, trooper.GetY(), 2);
	}
}
