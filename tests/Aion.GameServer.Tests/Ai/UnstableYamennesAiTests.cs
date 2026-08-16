using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for the portal cadence in <see cref="UnstableYamennesAI"/>, corrected against retail patterns
/// <c>IDAbRe_Core_NamedD_02</c> and <c>IDAbRe_Core_NamedD_Hard_02</c>
/// (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// The alternation itself was already right and is pinned here too, since nothing else covered it: a
/// wave upstairs, then one downstairs, then upstairs again. Only the timing changed — retail opens at
/// 30s and repeats every 65, where this waited a flat 60 both times.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class UnstableYamennesAiTests
{
	private const int UnstableSplinterpath = 300600000;
	private const int DurableYamennes = 219555;
	private const int Painflare = 219563;

	private const int ProtectorsFury = 281819;
	private const int YamennesSliver = 282065;

	/// <summary>The three gates, spawned as a set at whichever floor is due.</summary>
	/// <summary>
	/// Retail's own gate ids, from <c>IDAbRe_Core_NamedD_02</c>. The upstairs three are three
	/// different gates; the downstairs three are the same gate on three marks.
	/// </summary>
	private static readonly int[] Gates = { 283203, 283222, 283223, 283233 };

	/// <summary>Upstairs sits around z 216, downstairs around z 198.</summary>
	private const float UpstairsZ = 210f;

	private static (BossAiHarness, Npc, Player) Engaged() => EngagedSingle(DurableYamennes);

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(UnstableSplinterpath)
			.WithWorldSize(2048)
			.WithAi(typeof(UnstableYamennesAI), typeof(AggressiveNpcAI), typeof(YamennesSpawnGateAI), typeof(GeneralNpcAI))
			.Build();

	private static (BossAiHarness, Npc, Player) EngagedSingle(int npcId)
	{
		BossAiHarness harness = BossAiHarness.For(UnstableSplinterpath)
			.WithWorldSize(2048)
			.WithAi(typeof(UnstableYamennesAI), typeof(AggressiveNpcAI), typeof(YamennesSpawnGateAI), typeof(GeneralNpcAI))
			.Build();
		Npc boss = harness.Spawn(npcId, 330f, 730f, 216f);
		Player player = harness.SpawnPlayer(332f, 732f, 216f);
		harness.Engage(boss, player);
		return (harness, boss, player);
	}

	private static (BossAiHarness, Npc, List<Player>) Engaged(int npcId, int raidSize)
	{
		BossAiHarness harness = BossAiHarness.For(UnstableSplinterpath)
			.WithWorldSize(2048)
			.WithAi(typeof(UnstableYamennesAI), typeof(AggressiveNpcAI), typeof(YamennesSpawnGateAI), typeof(GeneralNpcAI))
			.Build();
		Npc boss = harness.Spawn(npcId, 330f, 730f, 216f);
		var raid = new List<Player>();
		for (int i = 0; i < raidSize; i++)
			raid.Add(harness.SpawnPlayer(332f + i, 732f, 216f));
		harness.Engage(boss, raid[0]);
		foreach (Player member in raid)
			BossAiHarness.Rehate(boss, member);
		return (harness, boss, raid);
	}

	private static List<Npc> LiveGates(BossAiHarness harness) =>
		harness.LiveNpcs().Where(n => Gates.Contains(n.GetNpcId())).ToList();

	[Fact]
	public void OpensItsFirstPortalsAtThirtySecondsNotSixty()
	{
		var (harness, _, _) = Engaged();
		using (harness)
		{
			harness.Clock.Advance(TimeSpan.FromSeconds(29));
			Assert.Empty(LiveGates(harness));

			harness.Clock.Advance(TimeSpan.FromSeconds(1));
			Assert.Equal(3, LiveGates(harness).Count);
		}
	}

	[Fact]
	public void AlternatesFloorsOnASixtyFiveSecondCycle()
	{
		var (harness, _, _) = Engaged();
		using (harness)
		{
			harness.Clock.Advance(TimeSpan.FromSeconds(30));
			bool firstUpstairs = LiveGates(harness).All(g => g.GetZ() > UpstairsZ);

			// Nothing new at the old 60s mark; the first wave is at 30 and the next 65 later, at 95.
			harness.Clock.Advance(TimeSpan.FromSeconds(64));
			Assert.Equal(3, LiveGates(harness).Count);

			harness.Clock.Advance(TimeSpan.FromSeconds(1));
			List<Npc> second = LiveGates(harness);
			Assert.True(second.Count > 3, "a second set of portals should have opened at 95s");

			// The new ones are on the other floor.
			bool secondUpstairs = second.OrderByDescending(g => g.GetObjectId()).First().GetZ() > UpstairsZ;
			Assert.NotEqual(firstUpstairs, secondUpstairs);

			// The first set times out 70s after it opened, at 100s, leaving only the second. Without a
			// lifetime the two sets would simply accumulate, which is what let the old version stall
			// once nobody killed the portals.
			harness.Clock.Advance(TimeSpan.FromSeconds(6));
			List<Npc> remaining = LiveGates(harness);
			Assert.Equal(3, remaining.Count);
			Assert.All(remaining, g => Assert.Equal(secondUpstairs, g.GetZ() > UpstairsZ));
		}
	}

	/// <summary>
	/// Protector's fury, retail's <c>IDCatacombs_Hard_Buff</c>: one on each of the most-hated, a minute
	/// into the fight and every twenty seconds after. Neither this nor the sliver below was spawned by
	/// anything in the server before — the portal cadence was corrected here earlier without noticing
	/// that two of the encounter's adds had no source at all.
	/// </summary>
	[Theory]
	[InlineData(DurableYamennes, 2)]
	[InlineData(Painflare, 3)]
	public void ItDropsProtectorsFuryOnTheMostHated(int npcId, int expected)
	{
		var (harness, boss, raid) = Engaged(npcId, raidSize: 5);
		using BossAiHarness _h = harness;

		// Nothing before the minute is up.
		for (int i = 0; i < 58; i++)
		{
			foreach (Player member in raid)
			{
				BossAiHarness.Rehate(boss, member);
				BossAiHarness.KeepAlive(member);
			}
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}
		Assert.Equal(0, harness.LiveNpcs().Count(n => n.GetNpcId() == ProtectorsFury));

		for (int i = 0; i < 4; i++)
		{
			foreach (Player member in raid)
			{
				BossAiHarness.Rehate(boss, member);
				BossAiHarness.KeepAlive(member);
			}
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}

		Assert.Equal(expected, harness.LiveNpcs().Count(n => n.GetNpcId() == ProtectorsFury));
	}

	/// <summary>Each fury lasts ten seconds, so a wave is gone well before the next is due.</summary>
	[Fact]
	public void EachFuryWaveTimesOutBeforeTheNext()
	{
		var (harness, boss, raid) = Engaged(DurableYamennes, raidSize: 5);
		using BossAiHarness _h = harness;

		void Tick(int seconds)
		{
			for (int i = 0; i < seconds; i++)
			{
				foreach (Player member in raid)
				{
					BossAiHarness.Rehate(boss, member);
					BossAiHarness.KeepAlive(member);
				}
				harness.Clock.Advance(TimeSpan.FromSeconds(1));
			}
		}

		Tick(62);
		Assert.Equal(2, harness.LiveNpcs().Count(n => n.GetNpcId() == ProtectorsFury));

		Tick(12);
		Assert.Equal(0, harness.LiveNpcs().Count(n => n.GetNpcId() == ProtectorsFury));

		// And the next wave arrives on the twenty-second cycle.
		Tick(10);
		Assert.Equal(2, harness.LiveNpcs().Count(n => n.GetNpcId() == ProtectorsFury));
	}

	/// <summary>
	/// Yamennes slivers, retail's <c>IDAbRe_Core_Sum_NamedD_onDie</c>: left where the top of the hate
	/// list stood, one for Durable and two for Painflare, and they have no lifetime.
	/// </summary>
	[Theory]
	[InlineData(DurableYamennes, 1)]
	[InlineData(Painflare, 2)]
	public void ItLeavesSliversBehindWhenItFalls(int npcId, int expected)
	{
		var (harness, boss, player) = EngagedSingle(npcId);
		using BossAiHarness _h = harness;
		Assert.Equal(0, harness.LiveNpcs().Count(n => n.GetNpcId() == YamennesSliver));

		boss.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.Died);

		Assert.Equal(expected, harness.LiveNpcs().Count(n => n.GetNpcId() == YamennesSliver));
	}

	private const int Orkanimum = 283200;
	private const int Lapilima = 283201;

	/// <summary>
	/// An upstairs gate feeds an orkanimum onto its own fixed mark every twelve seconds once attacked.
	/// </summary>
	/// <remarks>
	/// This is the pattern the gates actually run. The class they carried before spawned two other
	/// npcs at ±3 metres from itself, twelve seconds in and once more at seventy-two — a different
	/// mechanic that happened to produce adds near a portal.
	/// </remarks>
	[Fact]
	public void AnUpstairsGateFeedsAnOrkanimumOnItsMark()
	{
		using var harness = NewHarness();
		Npc gate = harness.Spawn(283203, 300f, 740f, 216f);
		Player player = harness.SpawnPlayer(302f, 742f, 216f);
		harness.Engage(gate, player);

		BossAiHarness.Watched fed = harness.Watch(
			5, () => BossAiHarness.Rehate(gate, player), Orkanimum);

		Assert.Equal(1, fed.Total);

		Npc orkanimum = harness.LiveNpcs().First(n => n.GetNpcId() == Orkanimum);
		Assert.Equal(309.95f, orkanimum.GetX(), 1);
		Assert.Equal(738.02f, orkanimum.GetY(), 1);
	}

	/// <summary>And keeps feeding — a second at twelve seconds, not one and done.</summary>
	[Fact]
	public void AnUpstairsGateKeepsFeeding()
	{
		using var harness = NewHarness();
		Npc gate = harness.Spawn(283203, 300f, 740f, 216f);
		Player player = harness.SpawnPlayer(302f, 742f, 216f);
		harness.Engage(gate, player);

		BossAiHarness.Watched fed = harness.Watch(
			20, () => BossAiHarness.Rehate(gate, player), Orkanimum);

		Assert.Equal(2, fed.Total);
	}

	/// <summary>
	/// The lower gate feeds something else, faster, and at its own feet rather than a fixed mark.
	/// </summary>
	[Fact]
	public void TheLowerGateFeedsALapilimaAtItsOwnFeet()
	{
		using var harness = NewHarness();
		Npc gate = harness.Spawn(283233, 305f, 736f, 198f);
		Player player = harness.SpawnPlayer(307f, 738f, 198f);
		harness.Engage(gate, player);

		BossAiHarness.Watched fed = harness.Watch(
			11, () => BossAiHarness.Rehate(gate, player), Lapilima);

		Assert.Equal(1, fed.Total);
		Assert.Equal(0, harness.LiveNpcs().Count(n => n.GetNpcId() == Orkanimum));

		Npc worm = harness.LiveNpcs().First(n => n.GetNpcId() == Lapilima);
		Assert.Equal(gate.GetX(), worm.GetX(), 1);
	}

	/// <summary><c>IDAbRe_Core_Sum_Teleport2_Enemy</c> — the summon that attacks the gate.</summary>
	private const int TeleportEnemy = 282016;

	/// <summary>
	/// A gate nobody touches feeds the room anyway: it summons its own attacker on waking, and that is
	/// what puts it into the attack state its feed timer hangs off.
	/// </summary>
	/// <remarks>
	/// This pin used to assert the opposite — that an unattacked gate feeds nothing — which was true of
	/// the code and false of retail. The on-wake summon had been read as unportable; see
	/// docs/retail-ai-fidelity.md.
	/// </remarks>
	[Fact]
	public void AGateNobodyTouchesStartsItsOwnFight()
	{
		using var harness = NewHarness();
		Npc gate = harness.Spawn(283203, 300f, 740f, 216f);

		Assert.Equal(1, harness.LiveNpcs().Count(n => n.GetNpcId() == TeleportEnemy));

		BossAiHarness.Watched fed = harness.Watch(20, null, Orkanimum);

		Assert.Equal(2, fed.Total);
	}

	/// <summary>The summon it opens with is on a seventy-second clock, like everything else it places.</summary>
	[Fact]
	public void TheOpeningSummonIsNotPermanent()
	{
		using var harness = NewHarness();
		harness.Spawn(283203, 300f, 740f, 216f);

		harness.Clock.Advance(TimeSpan.FromSeconds(71));

		Assert.Equal(0, harness.LiveNpcs().Count(n => n.GetNpcId() == TeleportEnemy));
	}

	/// <summary>
	/// A fury arrives <b>already fighting</b> the player it landed on, with retail's two million hate.
	/// </summary>
	/// <remarks>
	/// The hate is the observable rather than the state: a fury is <c>aggressive</c> with a fifty-metre
	/// search range and lands on top of its target, so it would engage on its own within the same tick
	/// and a pin on state alone would pass whether or not the flag were honoured. Natural aggression is
	/// worth a single point, so anything near two million is retail's number actually arriving — and it
	/// has to arrive, because a fury lives ten seconds and is meant to be dealt with by whoever it
	/// picked rather than peeled onto a tank.
	/// </remarks>
	[Fact]
	public void AProtectorsFuryArrivesAlreadyFighting()
	{
		var (harness, boss, player) = Engaged();
		using (harness)
		{
			for (int i = 0; i < 62; i++)
			{
				BossAiHarness.Rehate(boss, player);
				BossAiHarness.KeepAlive(player);
				harness.Clock.Advance(TimeSpan.FromSeconds(1));
			}

			Npc[] furies = harness.LiveNpcs().Where(n => n.GetNpcId() == ProtectorsFury).ToArray();
			Assert.NotEmpty(furies);
			Assert.All(furies, f => Assert.Equal(AIState.FIGHT, f.GetAi().GetState()));
			Assert.All(furies, f => Assert.True(f.GetAggroList().GetHate(player) > 1000000,
				$"retail's two million should be on the fury: {f.GetAggroList().GetHate(player)}"));
		}
	}

	/// <summary><c>bidabre_core_02_Sum_Golem</c> — hard mode's three ametgolems.</summary>
	private const int SummonedAmetgolem = 283229;

	/// <summary>
	/// Painflare calls three ametgolems onto fixed downstairs marks two minutes in, and again every
	/// three. Only the hard pattern has the branch, so nothing here reached 283229 at all.
	/// </summary>
	/// <remarks>Found by <c>tools/client-extract/audit_shared_ai_names.py</c>.</remarks>
	[Fact]
	public void PainflareCallsThreeAmetgolemsAtTwoMinutes()
	{
		var (harness, boss, player) = EngagedSingle(Painflare);
		using (harness)
		{
			for (int i = 0; i < 119; i++)
			{
				BossAiHarness.Rehate(boss, player);
				BossAiHarness.KeepAlive(player);
				harness.Clock.Advance(TimeSpan.FromSeconds(1));
			}

			Assert.Equal(0, harness.LiveNpcs().Count(n => n.GetNpcId() == SummonedAmetgolem));

			BossAiHarness.Rehate(boss, player);
			harness.Clock.Advance(TimeSpan.FromSeconds(2));

			Npc[] golems = harness.LiveNpcs().Where(n => n.GetNpcId() == SummonedAmetgolem).ToArray();
			Assert.Equal(3, golems.Length);

			// Three separate marks, not three on one.
			Assert.Equal(3, golems.Select(g => (g.GetX(), g.GetY())).Distinct().Count());
		}
	}

	/// <summary>The durable one does not: the branch is hard mode's alone.</summary>
	[Fact]
	public void DurableYamennesCallsNoAmetgolems()
	{
		var (harness, boss, player) = Engaged();
		using (harness)
		{
			for (int i = 0; i < 125; i++)
			{
				BossAiHarness.Rehate(boss, player);
				BossAiHarness.KeepAlive(player);
				harness.Clock.Advance(TimeSpan.FromSeconds(1));
			}

			Assert.Equal(0, harness.LiveNpcs().Count(n => n.GetNpcId() == SummonedAmetgolem));
		}
	}

	/// <summary>
	/// Each golem stays three minutes — which is exactly the interval, so the waves hand over rather
	/// than leaving a gap.
	/// </summary>
	/// <remarks>
	/// So absence cannot show it: the first wave expires at three hundred seconds and the second
	/// arrives at three hundred, and a head-count never reads zero. What is watched is <b>which</b>
	/// three, by object id.
	/// </remarks>
	[Fact]
	public void AWaveOfAmetgolemsHandsOverToTheNext()
	{
		var (harness, boss, player) = EngagedSingle(Painflare);
		using (harness)
		{
			for (int i = 0; i < 121; i++)
			{
				BossAiHarness.Rehate(boss, player);
				BossAiHarness.KeepAlive(player);
				harness.Clock.Advance(TimeSpan.FromSeconds(1));
			}

			int[] first = harness.LiveNpcs().Where(n => n.GetNpcId() == SummonedAmetgolem)
				.Select(n => n.GetObjectId()).ToArray();
			Assert.Equal(3, first.Length);

			for (int i = 0; i < 181; i++)
			{
				BossAiHarness.Rehate(boss, player);
				BossAiHarness.KeepAlive(player);
				harness.Clock.Advance(TimeSpan.FromSeconds(1));
			}

			// Three standing, and not one of them from the first wave.
			Assert.Equal(3, harness.LiveNpcs().Count(n => n.GetNpcId() == SummonedAmetgolem));
			Assert.All(first, id => Assert.DoesNotContain(harness.LiveNpcs(), n => n.GetObjectId() == id));
		}
	}

	/// <summary>
	/// A gate that is <b>removed</b> rather than killed still clears what it has fed out. Retail has
	/// the line on both <c>on_die</c> and <c>on_despawn</c>; only the first was translated, because
	/// until now there was no <c>OnDespawn</c> to put the second in.
	/// </summary>
	/// <remarks>
	/// The pattern reset that used to stand in for it only <em>forgets</em> a spawn group — it does not
	/// clear it — so an orkanimum outlived the gate that called it, for as long as its own seventy
	/// seconds lasted.
	/// </remarks>
	[Fact]
	public void ARemovedGateStillClearsItsFeed()
	{
		using var harness = NewHarness();
		Npc gate = harness.Spawn(283203, 300f, 740f, 216f);

		BossAiHarness.Watched fed = harness.Watch(6, null, Orkanimum);
		Assert.Equal(1, fed.Total);

		gate.GetAi().OnGeneralEvent(AiEventType.Despawned);

		Assert.Equal(0, harness.LiveNpcs().Count(n => n.GetNpcId() == Orkanimum));
	}
}
