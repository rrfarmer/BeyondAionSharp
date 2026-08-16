using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="TahabataDrakanAI"/>, <see cref="CalindiDrakanAI"/> and
/// <see cref="ChramatiFiretailAI"/>, translated from retail patterns <c>Dragon_G1SlaveDrakan</c>,
/// <c>Dragon_G2SlaveDrakan</c> and <c>Dragon_G5</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// The last three Dragon Lord's Refuge npcs still on plain <c>aggressive</c> or half-translated: the
/// drakan that leaves an exploder behind whichever way it goes, and the fifth grade, whose whole
/// non-cast pattern is that it hunts the weakest player.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class SlaveDrakanAiTests
{
	private const int DarkPoeta = 300040000;

	private const int TahabataDrakan = 281259;
	private const int CalindiDrakan = 281268;
	private const int Chramati = 215284;

	private const int OwnExploder = 281260;
	private const int OtherExploder = 281269;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(DarkPoeta).WithWorldSize(2048)
			.WithAi(typeof(TahabataDrakanAI), typeof(CalindiDrakanAI), typeof(ChramatiFiretailAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static (BossAiHarness, Npc, List<Player>) Engaged(int npcId, int raidSize = 5)
	{
		BossAiHarness harness = NewHarness();
		Npc npc = harness.Spawn(npcId, 1192f, 1254f, 140f);
		var raid = new List<Player>();
		for (int i = 0; i < raidSize; i++)
			raid.Add(harness.SpawnPlayer(1195f + i, 1254f, 140f));

		harness.Engage(npc, raid[0]);
		for (int i = 0; i < raidSize; i++)
			for (int n = raidSize - i; n > 0; n--)
				BossAiHarness.Rehate(npc, raid[i]);

		return (harness, npc, raid);
	}

	/// <summary>
	/// Advances a second at a time and reports every distinct target seen — the only way to observe a
	/// switch onto a <em>random</em> attacker, which any single reading can miss.
	/// </summary>
	private static HashSet<Creature> TargetsOver(BossAiHarness harness, Npc npc, List<Player> raid,
		int seconds, bool heal = true)
	{
		var seen = new HashSet<Creature>();
		for (int i = 0; i < seconds; i++)
		{
			foreach (Player member in raid)
			{
				BossAiHarness.Rehate(npc, member);
				if (heal)
					BossAiHarness.KeepAlive(member);
			}

			harness.Clock.Advance(TimeSpan.FromSeconds(1));
			if (npc.GetTarget() is Creature target)
				seen.Add(target);
		}

		return seen;
	}

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>
	/// <b>Killed by a player it leaves one exploder; removed any other way it leaves the other.</b>
	/// Retail's two branches name different npcs, and the death one names Calindi's — ported as
	/// written, so this pin is also the record of that oddity.
	/// </summary>
	[Fact]
	public void DyingAndDespawningLeaveDifferentExploders()
	{
		using BossAiHarness harness = NewHarness();
		Npc dying = harness.Spawn(TahabataDrakan, 1192f, 1254f, 140f);
		dying.GetAi().OnGeneralEvent(AiEventType.Died);

		Assert.Equal(1, Count(harness, OtherExploder));
		Assert.Equal(0, Count(harness, OwnExploder));

		Npc leaving = harness.Spawn(TahabataDrakan, 1200f, 1254f, 140f);
		leaving.GetController().DeleteIfAliveOrCancelRespawn();

		Assert.Equal(1, Count(harness, OwnExploder));
	}

	/// <summary>And an exploder stands for ten seconds.</summary>
	[Fact]
	public void AnExploderStandsForTenSeconds()
	{
		using BossAiHarness harness = NewHarness();
		Npc dying = harness.Spawn(TahabataDrakan, 1192f, 1254f, 140f);
		dying.GetAi().OnGeneralEvent(AiEventType.Died);

		harness.Clock.Advance(TimeSpan.FromSeconds(9));
		Assert.Equal(1, Count(harness, OtherExploder));

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.Equal(0, Count(harness, OtherExploder));
	}

	/// <summary>Above half health it holds whoever is holding it, however long the fight runs.</summary>
	[Fact]
	public void AboveHalfTheDrakanNeverPeels()
	{
		var (harness, drakan, raid) = Engaged(TahabataDrakan);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(drakan, 80);
		HashSet<Creature> seen = TargetsOver(harness, drakan, raid, 400);

		Assert.Equal(new HashSet<Creature> { raid[0] }, seen);
	}

	/// <summary>
	/// <b>Below half it starts rounding on people.</b> Five players and four hundred seconds: a peel
	/// onto a random attacker cannot hide in that, and a translation that lost the rung cannot fake it.
	/// </summary>
	[Fact]
	public void BelowHalfTheDrakanRoundsOnSomebody()
	{
		var (harness, drakan, raid) = Engaged(TahabataDrakan);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(drakan, 40);
		HashSet<Creature> seen = TargetsOver(harness, drakan, raid, 400);

		Assert.True(seen.Count > 1, "it never came off the tank in four hundred seconds");
	}

	/// <summary>
	/// <b>The first peel is the rung that opens the relay, and it is its own mechanic.</b> Seven
	/// seconds after the drakan is engaged below half health it turns once, long before the relay's
	/// far end could have come round.
	/// </summary>
	/// <remarks>
	/// Six separate fights, because one is not evidence: the pick is a random attacker, so any single
	/// fifteen-second window has a one-in-five chance of landing back on the tank and reading as
	/// nothing happening. This is what a pin for a random choice costs, and the alternative — a long
	/// window — measures the relay instead, which is a different rung.
	/// </remarks>
	[Fact]
	public void TheOpeningPeelComesLongBeforeTheRelay()
	{
		bool everPeeled = false;

		for (int attempt = 0; attempt < 6 && !everPeeled; attempt++)
		{
			var (harness, drakan, raid) = Engaged(TahabataDrakan);
			using BossAiHarness _h = harness;

			BossAiHarness.SetExactPercent(drakan, 40);
			everPeeled = TargetsOver(harness, drakan, raid, 15).Count > 1;
		}

		Assert.True(everPeeled, "six fights and it never turned inside fifteen seconds");
	}

	/// <summary>
	/// <b>And it keeps doing it.</b> The four-stage relay is what turns one peel into a peel every
	/// fifty-three seconds; its middle rungs are casts, and dropping them would end the mechanic after
	/// the first one — which this pin is placed past deliberately.
	/// </summary>
	[Fact]
	public void TheRelayKeepsThePeelGoing()
	{
		var (harness, drakan, raid) = Engaged(TahabataDrakan);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(drakan, 40);

		// The opening peel is inside this window and is not what is being measured.
		TargetsOver(harness, drakan, raid, 60);

		HashSet<Creature> later = TargetsOver(harness, drakan, raid, 400);
		Assert.True(later.Count > 1,
			"after the opening peel it held one player for four hundred seconds — the relay is dead");
	}

	/// <summary>Calindi's drakan runs the same relay: retail's two timer halves are identical.</summary>
	[Fact]
	public void CalindisDrakanRunsTheSameRelay()
	{
		var (harness, drakan, raid) = Engaged(CalindiDrakan);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(drakan, 40);
		HashSet<Creature> seen = TargetsOver(harness, drakan, raid, 400);

		Assert.True(seen.Count > 1, "it never came off the tank in four hundred seconds");
	}

	/// <summary>
	/// <b>And Calindi clearing the pair detonates them.</b> The call removes the standing drakan, and
	/// removal is what drops the exploder — so a fresh pair is not a quiet swap.
	/// </summary>
	[Fact]
	public void TheClearCallDetonatesCalindisDrakan()
	{
		using BossAiHarness harness = NewHarness();
		Npc drakan = harness.Spawn(CalindiDrakan, 1192f, 1254f, 140f);
		Npc caller = harness.Spawn(CalindiDrakan, 1194f, 1254f, 140f);
		BossAiHarness.MakeMutuallyKnown(caller, drakan);

		NpcMessageBus.Broadcast(caller, DarkPoetaCalindiFlamelordAI.ClearTheDrakan, null, 50f);

		Assert.False(drakan.IsSpawned());
		Assert.True(Count(harness, OtherExploder) >= 1, "leaving left nothing behind");
	}

	/// <summary>
	/// <b>Chramati Firetail hunts the weakest.</b> Ten seconds after something engages it, and then
	/// every thirty-five — retail alternates a fifteen-second slot and a twenty-second one, which is
	/// what makes the gap neither of those numbers.
	/// </summary>
	[Fact]
	public void ChramatiTurnsOnWhoeverIsClosestToDying()
	{
		var (harness, chramati, raid) = Engaged(Chramati, raidSize: 3);
		using BossAiHarness _h = harness;

		raid[2].GetLifeStats().SetCurrentHpPercent(5);
		Assert.Same(raid[0], chramati.GetTarget());

		TargetsOver(harness, chramati, raid, 9, heal: false);
		Assert.Same(raid[0], chramati.GetTarget());

		TargetsOver(harness, chramati, raid, 3, heal: false);
		Assert.Same(raid[2], chramati.GetTarget());

		// The next one is thirty-five seconds later, and by then somebody else is the weakest.
		raid[2].GetLifeStats().SetCurrentHpPercent(100);
		raid[1].GetLifeStats().SetCurrentHpPercent(5);

		TargetsOver(harness, chramati, raid, 36, heal: false);
		Assert.Same(raid[1], chramati.GetTarget());
	}

	/// <summary>Untouched it hunts nobody — the whole thing hangs off the fight.</summary>
	[Fact]
	public void AnUnpulledChramatiHuntsNobody()
	{
		using BossAiHarness harness = NewHarness();
		Npc chramati = harness.Spawn(Chramati, 1192f, 1254f, 140f);

		harness.Clock.Advance(TimeSpan.FromSeconds(120));

		Assert.Null(chramati.GetTarget());
	}
}
