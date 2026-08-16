using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="ShadowshiftAI"/>, translated from retail pattern <c>IDCT_Boss_Shadow</c>
/// (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// The mechanic is per-target rather than per-boss, so the pins are about who the spectres land on
/// as much as how many there are.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class ShadowshiftAiTests
{
	private const int Catacombs = 300150000;
	private const int Shadowshift = 216247;
	private const int SpectreNear = 281657;
	private const int SpectreFar = 281658;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Catacombs).WithWorldSize(2048)
			.WithAi(typeof(ShadowshiftAI), typeof(ServantNpcAI), typeof(AggressiveNpcAI)).Build();

	private static void Advance(BossAiHarness harness, Npc boss, Player[] players, int seconds)
	{
		for (int i = 0; i < seconds; i++)
		{
			foreach (Player p in players)
			{
				BossAiHarness.Rehate(boss, p);
				BossAiHarness.KeepAlive(p);
			}

			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}
	}

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	private static (BossAiHarness, Npc, Player[]) Engaged(int howManyPlayers)
	{
		BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Shadowshift, 300f, 300f, 200f);
		var players = new Player[howManyPlayers];
		for (int i = 0; i < howManyPlayers; i++)
		{
			players[i] = harness.SpawnPlayer(310f + (i * 25f), 300f, 200f);
			BossAiHarness.MakeMutuallyKnown(boss, players[i]);
			BossAiHarness.Rehate(boss, players[i]);
		}

		harness.Engage(boss, players[0]);
		return (harness, boss, players);
	}

	/// <summary>Nothing happens until it is pulled — the whole chain hangs off entering combat.</summary>
	[Fact]
	public void AnUnpulledShadowshiftCallsNothing()
	{
		BossAiHarness harness = NewHarness();
		using BossAiHarness _h = harness;
		harness.Spawn(Shadowshift, 300f, 300f, 200f);

		harness.Clock.Advance(TimeSpan.FromSeconds(60));

		Assert.Equal(0, Count(harness, SpectreNear));
		Assert.Equal(0, Count(harness, SpectreFar));
	}

	/// <summary>
	/// The far spectre comes at seven seconds and the near one at ten, which is the order the two
	/// timers are armed in rather than the order they are numbered.
	/// </summary>
	[Fact]
	public void TheFarSpectreArrivesBeforeTheNearOne()
	{
		var (harness, boss, players) = Engaged(1);
		using BossAiHarness _h = harness;

		Advance(harness, boss, players, 8);
		Assert.Equal(1, Count(harness, SpectreFar));
		Assert.Equal(0, Count(harness, SpectreNear));

		Advance(harness, boss, players, 3);

		Assert.Equal(1, Count(harness, SpectreNear));
	}

	/// <summary>
	/// <b>On the players, not on the boss — but capped.</b> That is what
	/// <c>spawn_on_multi_target</c> is: the hazard goes to people rather than to a mark. Retail caps
	/// the far spectre at <b>one</b> per cycle and gives it to the most-hated, so a group of three
	/// gets one spectre, on the tank.
	/// </summary>
	/// <remarks>
	/// This pin asserted three — one per player — and passed, because the class had read the pattern
	/// as uncapped. Retail's <c>total_set_to_spawn</c> is 1 here and 2 for the near spectre. See
	/// docs/retail-ai-fidelity.md.
	/// </remarks>
	[Fact]
	public void TheFarSpectreGoesToTheMostHatedAlone()
	{
		var (harness, boss, players) = Engaged(3);
		using BossAiHarness _h = harness;

		Advance(harness, boss, players, 8);

		Assert.Equal(1, Count(harness, SpectreFar));

		// ORDERI_DESCENDING: it lands on whoever holds the most hate, which the harness makes players[0]
		// by engaging with them first and topping everyone up equally afterwards.
		Npc spectre = Assert.Single(harness.LiveNpcs().Where(n => n.GetNpcId() == SpectreFar));
		Player nearest = players.OrderBy(p => Math.Abs(p.GetX() - spectre.GetX())).First();
		Assert.Equal(players[0].GetObjectId(), nearest.GetObjectId());
	}

	/// <summary>And the near pair goes to two different players, not twice to one.</summary>
	/// <remarks>
	/// The players stand twenty-five metres apart on purpose: retail scatters the near spectre three
	/// metres around its target, so anything closer would let one land nearer somebody else and make
	/// the nearest-player assignment meaningless.
	/// </remarks>
	[Fact]
	public void TheNearPairLandsOnTwoDifferentPlayers()
	{
		var (harness, boss, players) = Engaged(3);
		using BossAiHarness _h = harness;

		Advance(harness, boss, players, 11);

		Npc[] spectres = harness.LiveNpcs().Where(n => n.GetNpcId() == SpectreNear).ToArray();
		Assert.Equal(2, spectres.Length);

		int claimed = spectres
			.Select(s => players.OrderBy(p => Math.Abs(p.GetX() - s.GetX())).First().GetObjectId())
			.Distinct()
			.Count();
		Assert.Equal(2, claimed);
	}

	/// <summary>
	/// The far timer re-arms every four seconds, so they keep coming. Retail really does say four —
	/// it is the fastest re-arm in the fight and the reason the room fills up.
	/// </summary>
	/// <remarks>
	/// Measured at seven seconds and eleven, stopping short of the near spectre&apos;s own casting. Run
	/// past about thirteen seconds and the black essence starts casting into the harness&apos;s stand-in
	/// player, and the effect engine throws — a harness limitation rather than anything about this
	/// boss, but it bounds what can be watched here. See the write-up in docs/retail-ai-fidelity.md.
	/// </remarks>
	[Fact]
	public void TheFarSpectresKeepComingEveryFourSeconds()
	{
		var (harness, boss, players) = Engaged(1);
		using BossAiHarness _h = harness;
		Advance(harness, boss, players, 7);
		Assert.Equal(1, Count(harness, SpectreFar));

		Advance(harness, boss, players, 4);

		Assert.Equal(2, Count(harness, SpectreFar));
	}

	/// <summary>Dying clears them, or a cleared room would still be full of spectres.</summary>
	[Fact]
	public void DyingClearsTheSpectres()
	{
		var (harness, boss, players) = Engaged(2);
		using BossAiHarness _h = harness;
		Advance(harness, boss, players, 11);
		Assert.True(Count(harness, SpectreFar) > 0);

		boss.GetAi().OnGeneralEvent(AiEventType.Died);

		Assert.Equal(0, Count(harness, SpectreFar));
		Assert.Equal(0, Count(harness, SpectreNear));
	}

	/// <summary>So does resetting.</summary>
	[Fact]
	public void SoDoesResetting()
	{
		var (harness, boss, players) = Engaged(2);
		using BossAiHarness _h = harness;
		Advance(harness, boss, players, 11);
		Assert.True(Count(harness, SpectreFar) > 0);

		boss.GetAi().OnGeneralEvent(AiEventType.BackHome);

		Assert.Equal(0, Count(harness, SpectreFar));
	}

	/// <summary>
	/// A spectre arrives <b>already fighting</b> the player it materialised on — retail's
	/// <c>attack_target_after_spawn</c> with a single hate point.
	/// </summary>
	/// <remarks>
	/// <b>The hate is what has to be asserted here, not the state.</b> A spectre is <c>aggressive</c>
	/// and lands within its own ten-metre search range, so it engages the player on its own in the same
	/// tick — state and target are identical whether or not the flag is honoured, and a pin on them
	/// passes for the wrong reason. Natural aggro contributes one point; the flag adds retail's
	/// <c>hatepoints_to_add</c> on top, so two is the fingerprint and one means the flag was dropped.
	/// </remarks>
	[Fact]
	public void ASpectreArrivesAlreadyFighting()
	{
		var (harness, boss, players) = Engaged(1);
		using BossAiHarness _h = harness;

		Advance(harness, boss, players, 8);
		Npc spectre = Assert.Single(harness.LiveNpcs().Where(n => n.GetNpcId() == SpectreFar));

		// One more tick: the provoke is deferred, for the reasons PatternAi.ProvokeNextTick gives.
		Advance(harness, boss, players, 1);

		Assert.Equal(AIState.FIGHT, spectre.GetAi().GetState());
		Assert.Same(players[0], spectre.GetTarget());
		Assert.True(spectre.GetAggroList().GetHate(players[0]) >= 2,
			$"one point is what it would aggro on its own; the flag adds retail's own on top: "
			+ $"{spectre.GetAggroList().GetHate(players[0])}");
	}
}
