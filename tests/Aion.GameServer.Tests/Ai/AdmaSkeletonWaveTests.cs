using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for Adma's skeleton waves — Lord Lannok calling and the coffins answering, translated from
/// <c>Adma_DeathknightNamed</c> and <c>NoAction_CoffinA</c> through <c>F</c>
/// (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Recorded as a gap early in this work — "the skeleton adds need something to spawn the three
/// invisible controllers" — and blocked for a long time on the wrong half of the mechanic. The
/// controllers are still unreachable; Lord Lannok's own calls are not, and they drive coffins D, E
/// and F.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class AdmaSkeletonWaveTests
{
	private const int AdmaStronghold = 320130000;
	private const int LordLannok = 214696;

	private const int CoffinA = 280942;
	private const int CoffinD = 281056;

	private const int FaithfulPage = 280933;
	private const int DiligentPage = 280949;

	private static BossAiHarness NewHarness() => BossAiHarness.For(AdmaStronghold)
		.WithWorldSize(2048)
		.WithAi(typeof(LordLannokAI), typeof(SuspiciousCoffinAI), typeof(AggressiveNpcAI))
		.Build();

	private static int Pages(BossAiHarness harness) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() is FaithfulPage or DiligentPage);

	private static void Call(Npc coffin, int message)
	{
		var listener = (Aion.GameServer.Ai.INpcMessageListener)coffin.GetAi();
		listener.OnNpcMessage(coffin, message, null);
	}

	/// <summary>The first call is always a page, never a mage.</summary>
	[Fact]
	public void TheFirstCallPutsOutAFaithfulPage()
	{
		using BossAiHarness harness = NewHarness();
		Npc coffin = harness.Spawn(CoffinD, 596f, 723f, 198.6f);

		Call(coffin, LordLannokAI.CallForPages);

		Assert.Equal(1, harness.LiveNpcs().Count(n => n.GetNpcId() == FaithfulPage));
		Assert.Equal(0, harness.LiveNpcs().Count(n => n.GetNpcId() == DiligentPage));
	}

	/// <summary>
	/// The two triplets are what keep the halves apart: coffins A, B and C answer the unreachable
	/// controllers, D, E and F answer Lord Lannok. An A coffin must ignore his calls.
	/// </summary>
	[Fact]
	public void ACoffinIgnoresACallMeantForTheOtherThree()
	{
		using BossAiHarness harness = NewHarness();
		Npc coffin = harness.Spawn(CoffinA, 601f, 765f, 198.6f);

		Call(coffin, LordLannokAI.CallForPages);
		Call(coffin, LordLannokAI.CallForMore);

		Assert.Equal(0, Pages(harness));
	}

	/// <summary>Each coffin puts its pages at its own point, which is why the six patterns exist.</summary>
	[Fact]
	public void EachCoffinPlacesItsPagesAtItsOwnPoint()
	{
		using BossAiHarness harness = NewHarness();
		Npc coffin = harness.Spawn(CoffinD, 900f, 900f, 198.6f);

		Call(coffin, LordLannokAI.CallForPages);

		Npc page = Assert.Single(harness.LiveNpcs().Where(n => n.GetNpcId() == FaithfulPage));
		Assert.Equal(596f, page.GetX());
		Assert.Equal(723f, page.GetY());
	}

	/// <summary>The third call is a mage on a small roll and a page otherwise — never both.</summary>
	[Fact]
	public void TheThirdCallPutsOutExactlyOneEitherWay()
	{
		using BossAiHarness harness = NewHarness();
		Npc coffin = harness.Spawn(CoffinD, 596f, 723f, 198.6f);

		for (int i = 0; i < 20; i++)
			Call(coffin, LordLannokAI.CallForMore);

		Assert.Equal(20, Pages(harness));
	}

	/// <summary>His all-clear on death sends the whole wave away.</summary>
	[Fact]
	public void TheAllClearSendsThemAway()
	{
		using BossAiHarness harness = NewHarness();
		Npc coffin = harness.Spawn(CoffinD, 596f, 723f, 198.6f);
		Call(coffin, LordLannokAI.CallForPages);
		Assert.Equal(1, Pages(harness));

		Call(coffin, 6601);

		Assert.Equal(0, Pages(harness));
	}

	/// <summary>
	/// He lights the fuse only once he is between 26 and 50, and only then starts calling. Retail hangs
	/// that on a battle timer whose rotation is not translated, so it hangs on being hit instead.
	/// </summary>
	[Fact]
	public void HeCallsOnlyOnceHeIsWounded()
	{
		using BossAiHarness harness = NewHarness();
		Npc lannok = harness.Spawn(LordLannok, 600f, 745f, 198.6f);
		Npc coffin = harness.Spawn(CoffinD, 596f, 723f, 198.6f);
		Player player = harness.SpawnPlayer(602f, 747f, 198.6f);
		BossAiHarness.MakeMutuallyKnown(lannok, coffin);
		harness.Engage(lannok, player);

		// Healthy: the fuse never lights, so nothing arrives however long he is hit.
		for (int i = 0; i < 50; i++)
		{
			lannok.GetAi().OnCreatureEvent(AiEventType.Attack, player);
			BossAiHarness.KeepAlive(player);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}
		Assert.Equal(0, Pages(harness));

		// Wounded, the fuse lights at thirty-five seconds and he calls every forty-five after. Counted
		// by identity over ten minutes, because the pages expire at three and because both branches of
		// the coin flip have to re-arm — dropping the re-arm from one of them still leaves the chain
		// running until that side comes up, which a single "did anything arrive" check cannot see.
		BossAiHarness.SetHpPercent(lannok, 40);
		var seen = new HashSet<Npc>();
		for (int i = 0; i < 600; i++)
		{
			lannok.GetAi().OnCreatureEvent(AiEventType.Attack, player);
			BossAiHarness.KeepAlive(player);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
			foreach (Npc page in harness.LiveNpcs()
				.Where(n => n.GetNpcId() is FaithfulPage or DiligentPage))
				seen.Add(page);
		}

		Assert.True(seen.Count >= 8,
			$"ten minutes of a wounded boss should be a dozen calls, saw {seen.Count}");
	}
}
