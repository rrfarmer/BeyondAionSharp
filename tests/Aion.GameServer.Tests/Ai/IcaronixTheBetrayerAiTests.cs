using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Behavioural pins for <see cref="IcaronixTheBetrayerAI"/>, translated from retail pattern
/// <c>NLehpar_BhB</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// He had no AI at all — <c>BetrayerIcaronixAI</c> spawned him and nothing drove him — so the second
/// half of this fight was a plain aggressive monster. All five NPCs asserted here were spawned by
/// nothing anywhere in the server.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class IcaronixTheBetrayerAiTests
{
	private const int AzoturanFortress = 310100000;
	private const int IcaronixTheBetrayer = 214599;

	private const int Kuillus = 280937;
	private const int Mudthorn = 280939;
	private const int Pretor = 280938;
	private const int Rottentree = 280940;
	private const int StrangeCreature = 280941;

	private static readonly TimeSpan Tick = TimeSpan.FromSeconds(5);

	private static (BossAiHarness, Npc, Player) Engaged()
	{
		BossAiHarness harness = BossAiHarness.For(AzoturanFortress)
			.WithAi(typeof(IcaronixTheBetrayerAI), typeof(AggressiveNpcAI),
				// One of his servants is declared ai="ntrap" in npc_templates. Without the class
				// registered, AIEngine throws inside the spawn, the exception is swallowed in the AI
				// path, and the add is simply absent -- no error, no clue. Same shape as Padmarashka's
				// acid bomb; found by sweeping every handler's spawned ids against its test's WithAi.
				typeof(NTrapAI), typeof(GeneralNpcAI))
			.Build();
		Npc boss = harness.Spawn(IcaronixTheBetrayer, 461.07f, 439.876f, 993.046f);
		Player player = harness.SpawnPlayer(461.07f, 439.876f, 993.046f);
		harness.Engage(boss, player);
		return (harness, boss, player);
	}

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	private static void DropTo(BossAiHarness harness, Npc boss, Player player, int percent)
	{
		BossAiHarness.SetHpPercent(boss, percent);
		BossAiHarness.Rehate(boss, player);
		harness.Clock.Advance(Tick);
	}

	[Fact]
	public void CallsUpHisFirstServantAsSoonAsTheFightStarts()
	{
		var (harness, boss, _) = Engaged();
		using (harness)
		{
			Assert.Equal(1, Count(harness, Kuillus));

			// Only the first. The other three wait for their thresholds.
			Assert.Equal(0, Count(harness, Mudthorn));
			Assert.Equal(0, Count(harness, Pretor));
			Assert.Equal(0, Count(harness, Rottentree));
		}
	}

	[Fact]
	public void CallsADifferentServantAtEachThresholdAndKeepsTheEarlierOnes()
	{
		var (harness, boss, player) = Engaged();
		using (harness)
		{
			DropTo(harness, boss, player, 90);
			Assert.Equal(0, Count(harness, Mudthorn));

			DropTo(harness, boss, player, 75);
			Assert.Equal(1, Count(harness, Mudthorn));

			DropTo(harness, boss, player, 45);
			Assert.Equal(1, Count(harness, Pretor));

			DropTo(harness, boss, player, 25);
			Assert.Equal(1, Count(harness, Rottentree));

			// Each step clears only its own spawn id, so by the end all four are up at once.
			Assert.Equal(1, Count(harness, Kuillus));
			Assert.Equal(1, Count(harness, Mudthorn));
			Assert.Equal(1, Count(harness, Pretor));
		}
	}

	[Fact]
	public void CallsEachServantOnlyOnce()
	{
		var (harness, boss, player) = Engaged();
		using (harness)
		{
			DropTo(harness, boss, player, 75);
			int summoned = harness.LiveNpcs().Single(n => n.GetNpcId() == Mudthorn).GetObjectId();

			// Sitting in the same band must not keep summoning. Counting is not enough to see that:
			// each step despawns its own group before spawning, so a repeating step would delete and
			// replace the servant and still leave exactly one. It is the same servant that matters.
			for (int i = 0; i < 10; i++)
				DropTo(harness, boss, player, 75);

			Assert.Equal(summoned, harness.LiveNpcs().Single(n => n.GetNpcId() == Mudthorn).GetObjectId());
		}
	}

	[Fact]
	public void LeavesAStrangeCreatureBehindAndSendsHisServantsAwayWhenHeDies()
	{
		var (harness, boss, player) = Engaged();
		using (harness)
		{
			// Walk the whole ladder so all four servants are out; killing him with only one up would
			// not notice a step whose despawn is missing.
			foreach (int hp in new[] { 75, 45, 25 })
				DropTo(harness, boss, player, hp);
			foreach (int servant in new[] { Kuillus, Mudthorn, Pretor, Rottentree })
				Assert.Equal(1, Count(harness, servant));

			boss.GetAi().OnGeneralEvent(AiEventType.Died);

			foreach (int servant in new[] { Kuillus, Mudthorn, Pretor, Rottentree })
				Assert.Equal(0, Count(harness, servant));
			Assert.Equal(1, Count(harness, StrangeCreature));

			// It crawls out for twelve seconds and then goes.
			harness.Clock.Advance(TimeSpan.FromSeconds(12));
			Assert.Equal(0, Count(harness, StrangeCreature));
		}
	}

	[Fact]
	public void SendsEveryServantAwayIfHeResets()
	{
		var (harness, boss, player) = Engaged();
		using (harness)
		{
			DropTo(harness, boss, player, 45);
			Assert.True(Count(harness, Pretor) > 0);

			boss.GetAi().OnGeneralEvent(AiEventType.BACK_HOME);

			foreach (int servant in new[] { Kuillus, Mudthorn, Pretor, Rottentree })
				Assert.Equal(0, Count(harness, servant));
		}
	}
}
