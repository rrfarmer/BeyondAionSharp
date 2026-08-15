using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Behavioural pins for <see cref="LostBalorAI"/>, translated from retail pattern <c>ND2_FhV</c>
/// (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// He had no AI at all — a plain aggressive world boss — and all four statues asserted here were
/// spawned by nothing anywhere in the server.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class LostBalorAiTests
{
	private const int Theobomos = 210060000;
	private const int LostBalor = 214567;

	private const int KuillusStatue = 280956;
	private const int TestStatue = 280957;
	private const int StatueF = 280954;
	private const int StatueM = 280955;

	private static readonly TimeSpan Tick = TimeSpan.FromSeconds(5);

	private static (BossAiHarness, Npc, Player) Engaged()
	{
		BossAiHarness harness = BossAiHarness.For(Theobomos)
			.WithWorldSize(4096)
			.WithAi(typeof(LostBalorAI), typeof(AggressiveNpcAI))
			.Build();
		Npc boss = harness.Spawn(LostBalor, 1900f, 1500f, 300f);
		Player player = harness.SpawnPlayer(1903f, 1502f, 300f);
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
	public void CallsNoStatuesWhileHealthyAndStillCallsThemAfterwards()
	{
		var (harness, boss, player) = Engaged();
		using (harness)
		{
			// Several ticks above every threshold, matching nothing.
			for (int i = 0; i < 4; i++)
				DropTo(harness, boss, player, 90);
			foreach (int statue in new[] { KuillusStatue, TestStatue, StatueF, StatueM })
				Assert.Equal(0, Count(harness, statue));

			// And the ladder still works afterwards. This is what the catch-all heartbeat is for: only
			// a branch can re-arm the timer, so without one those healthy ticks would have ended the
			// chain and no statue would ever appear for the rest of the fight.
			DropTo(harness, boss, player, 75);
			Assert.Equal(1, Count(harness, KuillusStatue));
		}
	}

	[Fact]
	public void CallsOneStatuePerStepAndTwoAtTheLast()
	{
		var (harness, boss, player) = Engaged();
		using (harness)
		{
			DropTo(harness, boss, player, 75);
			Assert.Equal(1, Count(harness, KuillusStatue));
			Assert.Equal(0, Count(harness, TestStatue));

			DropTo(harness, boss, player, 45);
			Assert.Equal(1, Count(harness, TestStatue));

			// The last step brings two at once, which is what makes it the last step.
			DropTo(harness, boss, player, 25);
			Assert.Equal(1, Count(harness, StatueF));
			Assert.Equal(1, Count(harness, StatueM));

			// Nothing clears anything, so all four stand together at the end.
			Assert.Equal(1, Count(harness, KuillusStatue));
			Assert.Equal(1, Count(harness, TestStatue));
		}
	}

	[Fact]
	public void CallsEachStepOnlyOnce()
	{
		var (harness, boss, player) = Engaged();
		using (harness)
		{
			DropTo(harness, boss, player, 75);
			int summoned = harness.LiveNpcs().Single(n => n.GetNpcId() == KuillusStatue).GetObjectId();

			// Nothing here despawns before it spawns, so a repeating step would pile statues up rather
			// than replace one -- but assert identity anyway, since that is the property that matters.
			for (int i = 0; i < 8; i++)
				DropTo(harness, boss, player, 75);

			Assert.Equal(1, Count(harness, KuillusStatue));
			Assert.Equal(summoned, harness.LiveNpcs().Single(n => n.GetNpcId() == KuillusStatue).GetObjectId());
		}
	}

	[Fact]
	public void SendsEveryStatueAwayIfHeResets()
	{
		var (harness, boss, player) = Engaged();
		using (harness)
		{
			foreach (int hp in new[] { 75, 45, 25 })
				DropTo(harness, boss, player, hp);
			foreach (int statue in new[] { KuillusStatue, TestStatue, StatueF, StatueM })
				Assert.Equal(1, Count(harness, statue));

			boss.GetAi().OnGeneralEvent(AiEventType.BACK_HOME);

			foreach (int statue in new[] { KuillusStatue, TestStatue, StatueF, StatueM })
				Assert.Equal(0, Count(harness, statue));
		}
	}
}
