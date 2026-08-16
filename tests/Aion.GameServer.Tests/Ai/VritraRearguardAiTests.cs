using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Behavioural pins for <see cref="VritraRearguardAI"/>, translated from retail pattern
/// <c>IDF5_U1_War_Vri_Def01_Ra_SN_65_Ae</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Two NPCs share the class, so the shared assertions run against both. The substance is the traps —
/// three mines per chain cycle and two nets on first crossing 50 — none of which existed before.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class VritraRearguardAiTests
{
	private const int EngulfedOphidanBridge = 301250000;
	private const int GuardPost = 233487;
	private const int DefensePost = 233477;

	private const int NetTrap = 284692;
	private const int MineTrap = 284693;

	public static TheoryData<int> Rearguards => new() { GuardPost, DefensePost };

	private static (BossAiHarness, Npc, Player) Engaged(int npcId, int hpPercent)
	{
		BossAiHarness harness = BossAiHarness.For(EngulfedOphidanBridge).WithWorldSize(2048)
			.WithAi(typeof(VritraRearguardAI), typeof(TrapNpcAI), typeof(AggressiveNpcAI)).Build();
		Npc npc = harness.Spawn(npcId, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(302f, 302f, 200f);
		BossAiHarness.SetHpPercent(npc, hpPercent);
		harness.Engage(npc, player);
		return (harness, npc, player);
	}

	private static void Advance(BossAiHarness harness, Npc npc, Player player, int seconds)
	{
		for (int i = 0; i < seconds; i++)
		{
			BossAiHarness.Rehate(npc, player);
			BossAiHarness.KeepAlive(player);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}
	}

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	[Theory]
	[MemberData(nameof(Rearguards))]
	public void TheHealthyChainLaysThreeMinesOnItsFirstTick(int npcId)
	{
		var (harness, npc, player) = Engaged(npcId, 90);
		using BossAiHarness _h = harness;

		Advance(harness, npc, player, 11);

		Assert.Equal(3, Count(harness, MineTrap));
		Assert.Equal(0, Count(harness, NetTrap));
	}

	[Theory]
	[MemberData(nameof(Rearguards))]
	public void CrossingFiftyLaysTwoNetsAsWell(int npcId)
	{
		var (harness, npc, player) = Engaged(npcId, 40);
		using BossAiHarness _h = harness;

		Advance(harness, npc, player, 6);

		Assert.Equal(2, Count(harness, NetTrap));
	}

	/// <summary>
	/// The nets are a never-again flag, not a per-tick one. Timer 0 keeps ticking every five seconds
	/// below fifty, and without the flag each tick would lay another pair.
	/// </summary>
	[Fact]
	public void TheNetsAreLaidOnceForTheWholeFight()
	{
		var (harness, npc, player) = Engaged(GuardPost, 40);
		using BossAiHarness _h = harness;
		var seen = new HashSet<Npc>();

		for (int i = 0; i < 30; i++)
		{
			Advance(harness, npc, player, 1);
			foreach (Npc net in harness.LiveNpcs().Where(n => n.GetNpcId() == NetTrap))
				seen.Add(net);
		}

		Assert.Equal(2, seen.Count);
	}

	/// <summary>
	/// The never-again flag only earns its keep on a second descent: while health stays below fifty the
	/// latch alone already blocks the branch, so the two flags look redundant until something heals the
	/// rearguard back up and pushes it down again.
	/// </summary>
	/// <remarks>
	/// This is also the documented stranding path — see the class remarks. The latch is spent by the
	/// branch that then fails on the never-again flag, so the branch beneath, which exists to re-arm the
	/// low chain without laying traps, finds the latch already gone.
	/// </remarks>
	[Fact]
	public void HealedAboveFiftyAndPushedBackDownItLaysNoMoreNets()
	{
		var (harness, npc, player) = Engaged(GuardPost, 40);
		using BossAiHarness _h = harness;
		var seen = new HashSet<Npc>();

		void Watch(int seconds)
		{
			for (int i = 0; i < seconds; i++)
			{
				Advance(harness, npc, player, 1);
				foreach (Npc net in harness.LiveNpcs().Where(n => n.GetNpcId() == NetTrap))
					seen.Add(net);
			}
		}

		Watch(6);
		Assert.Equal(2, seen.Count);

		// Back above fifty long enough for timer 0 to release the latch, then down again.
		BossAiHarness.SetHpPercent(npc, 90);
		Watch(12);
		BossAiHarness.SetHpPercent(npc, 40);
		Watch(20);

		Assert.Equal(2, seen.Count);
	}

	/// <summary>
	/// Below fifty the low chain takes over and lays its own mines; the healthy chain's branches stop
	/// matching, so a rearguard that drops does not run both.
	/// </summary>
	[Fact]
	public void TheLowChainLaysMinesToo()
	{
		var (harness, npc, player) = Engaged(GuardPost, 40);
		using BossAiHarness _h = harness;

		Advance(harness, npc, player, 11);

		Assert.Equal(3, Count(harness, MineTrap));
	}

	[Fact]
	public void TheTrapsLiveFifteenSecondsAndGo()
	{
		var (harness, npc, player) = Engaged(GuardPost, 90);
		using BossAiHarness _h = harness;
		Advance(harness, npc, player, 11);
		List<Npc> first = harness.LiveNpcs().Where(n => n.GetNpcId() == MineTrap).ToList();
		Assert.Equal(3, first.Count);

		Advance(harness, npc, player, 13);
		Assert.All(first, t => Assert.True(t.IsSpawned(), "should still stand short of fifteen seconds"));

		Advance(harness, npc, player, 3);
		Assert.All(first, t => Assert.False(t.IsSpawned(), "should have gone once fifteen seconds passed"));
	}

	/// <summary>Being told to stand down removes it outright.</summary>
	[Fact]
	public void TheDismissMessageTakesItOffTheField()
	{
		var (harness, npc, player) = Engaged(GuardPost, 90);
		using BossAiHarness _h = harness;
		Assert.True(npc.IsSpawned());

		var listener = (Aion.GameServer.Ai.INpcMessageListener)npc.GetAi();
		listener.OnNpcMessage(npc, VritraRearguardAI.Dismiss, null);

		Assert.False(npc.IsSpawned());
	}

	/// <summary>Being told who to fight puts a hundred hate on whoever the message carried.</summary>
	[Fact]
	public void TheTargetMessageAddsHateToWhoeverItNames()
	{
		var (harness, npc, player) = Engaged(GuardPost, 90);
		using BossAiHarness _h = harness;
		Player other = harness.SpawnPlayer(305f, 305f, 200f);
		BossAiHarness.MakeMutuallyKnown(npc, other);
		int before = npc.GetAggroList().GetHate(other);

		var listener = (Aion.GameServer.Ai.INpcMessageListener)npc.GetAi();
		listener.OnNpcMessage(npc, VritraRearguardAI.Target, other);

		Assert.True(npc.GetAggroList().GetHate(other) > before,
			"the named player should have gained hate");
	}

	[Theory]
	[MemberData(nameof(Rearguards))]
	public void DyingClearsTheNets(int npcId)
	{
		var (harness, npc, player) = Engaged(npcId, 40);
		using BossAiHarness _h = harness;
		Advance(harness, npc, player, 6);
		Assert.Equal(2, Count(harness, NetTrap));

		npc.GetAi().OnGeneralEvent(AiEventType.Died);

		Assert.Equal(0, Count(harness, NetTrap));
	}
}
