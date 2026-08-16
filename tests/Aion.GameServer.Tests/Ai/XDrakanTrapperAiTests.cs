using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="XDrakanTrapperAI"/>, translated from retail patterns <c>Dread_XDrakanReA</c>,
/// <c>XDrakan_ReB_50</c> and <c>Dread_SurkanaNm06</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Eight spawned Balaur officers on plain <c>aggressive</c>. The shape worth pinning is that a fight
/// has exactly two peels — one ten seconds in and one on crossing seventy — and that the trap goes
/// down on the player rather than at the officer's feet.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class XDrakanTrapperAiTests
{
	private const int Dredgion = 300110000;

	private const int Triaris = 214820;
	private const int Garkusa = 215087;
	private const int Arcus = 215256;

	private const int DragonsTrap = 281161;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Dredgion).WithWorldSize(2048)
			.WithAi(typeof(XDrakanTrapperAI), typeof(NTrapAI), typeof(AggressiveNpcAI),
				typeof(GeneralNpcAI))
			.Build();

	/// <summary>The raid stands well apart, so which player a trap landed on is unambiguous.</summary>
	private static (BossAiHarness, Npc, List<Player>) Engaged(int npcId)
	{
		BossAiHarness harness = NewHarness();
		Npc officer = harness.Spawn(npcId, 300f, 300f, 200f);
		var raid = new List<Player>
		{
			harness.SpawnPlayer(330f, 300f, 200f),
			harness.SpawnPlayer(330f, 340f, 200f),
			harness.SpawnPlayer(330f, 380f, 200f),
		};

		harness.Engage(officer, raid[0]);
		for (int i = 0; i < raid.Count; i++)
			for (int n = raid.Count - i; n > 0; n--)
				BossAiHarness.Rehate(officer, raid[i]);

		return (harness, officer, raid);
	}

	private static void Advance(BossAiHarness harness, List<Player> raid, Npc officer, int seconds)
	{
		for (int i = 0; i < seconds; i++)
		{
			foreach (Player member in raid)
			{
				BossAiHarness.Rehate(officer, member);
				BossAiHarness.KeepAlive(member);
			}

			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}
	}

	private static int Arrived(BossAiHarness harness, Npc officer, List<Player> raid, int seconds) =>
		harness.WatchNew(seconds, () =>
		{
			foreach (Player member in raid)
			{
				BossAiHarness.Rehate(officer, member);
				BossAiHarness.KeepAlive(member);
			}
		}, DragonsTrap).Total;

	/// <summary>Untouched it lays nothing — the whole pattern hangs off the fight.</summary>
	[Fact]
	public void AnUnpulledOfficerLaysNothing()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(Triaris, 300f, 300f, 200f);

		Assert.Equal(0, harness.Watch(120, null, DragonsTrap).Total);
	}

	/// <summary>
	/// <b>Ten seconds into any fight it turns on the second-most-hated player.</b> Once: the timer that
	/// carries it is armed on entering combat and never re-armed.
	/// </summary>
	[Theory]
	[InlineData(Triaris)]
	[InlineData(Garkusa)]
	[InlineData(Arcus)]
	public void EveryFightOpensWithOnePeel(int officerId)
	{
		var (harness, officer, raid) = Engaged(officerId);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(officer, 90);
		Assert.Same(raid[0], officer.GetTarget());
		Assert.Same(raid[1], officer.GetAggroList().GetTarget(AggroTarget.SECOND_MOST_HATED));

		// Eight seconds is before it, which is the only reading that can see the delay at all.
		Advance(harness, raid, officer, 8);
		Assert.Same(raid[0], officer.GetTarget());

		Advance(harness, raid, officer, 4);
		Assert.Same(raid[1], officer.GetTarget());

		// And that is the only one above seventy: turn the order over and nothing moves again.
		for (int i = 0; i < 5; i++)
			BossAiHarness.Rehate(officer, raid[1]);

		Assert.Same(raid[0], officer.GetAggroList().GetTarget(AggroTarget.SECOND_MOST_HATED));

		Advance(harness, raid, officer, 60);
		Assert.Same(raid[1], officer.GetTarget());
	}

	/// <summary>Above seventy no trap comes, however long the fight runs.</summary>
	[Fact]
	public void AboveSeventyNoTrapComes()
	{
		var (harness, officer, raid) = Engaged(Triaris);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(officer, 90);

		Assert.Equal(0, Arrived(harness, officer, raid, 90));
	}

	/// <summary>
	/// <b>Crossing seventy lays one, and only one.</b> The rung carries a flag var, so a fight spent in
	/// the band pays once however long it lasts.
	/// </summary>
	[Fact]
	public void CrossingSeventyLaysOneTrap()
	{
		var (harness, officer, raid) = Engaged(Triaris);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(officer, 50);
		Assert.Equal(1, Arrived(harness, officer, raid, 10));

		Assert.Equal(0, Arrived(harness, officer, raid, 90));
	}

	/// <summary>
	/// <b>And it goes down on the player, not at the officer's feet.</b> Thirty metres apart, so there
	/// is no reading it either way.
	/// </summary>
	[Fact]
	public void TheTrapLandsOnWhoeverItIsFighting()
	{
		var (harness, officer, raid) = Engaged(Triaris);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(officer, 50);
		Advance(harness, raid, officer, 8);

		Npc trap = Assert.Single(harness.LiveNpcs(), n => n.GetNpcId() == DragonsTrap);
		Assert.True(Math.Abs(trap.GetX() - 330f) <= 6f,
			$"the trap landed at x={trap.GetX()}, which is the officer's ground rather than the raid's");
	}

	/// <summary>
	/// The band rung peels as well as laying, so crossing seventy costs the tank the officer a second
	/// time.
	/// </summary>
	[Fact]
	public void CrossingSeventyAlsoPeels()
	{
		var (harness, officer, raid) = Engaged(Triaris);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(officer, 90);
		Advance(harness, raid, officer, 12);
		Assert.Same(raid[1], officer.GetTarget());

		// The peeled-onto player now holds it, which puts the old tank second.
		for (int i = 0; i < 5; i++)
			BossAiHarness.Rehate(officer, raid[1]);

		Assert.Same(raid[0], officer.GetAggroList().GetTarget(AggroTarget.SECOND_MOST_HATED));

		BossAiHarness.SetExactPercent(officer, 50);
		Advance(harness, raid, officer, 10);

		Assert.Same(raid[0], officer.GetTarget());
	}
}
