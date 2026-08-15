using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for the one mechanic taken from the field generator's retail pattern <c>LGuard_Shield</c>
/// (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Only the ice sheet is translated — the rest of that pattern is an unresolvable rotation on a siege
/// object — so these tests are narrow on purpose. What they are really guarding is that a one-shot
/// mechanic bolted onto a siege class stays one-shot and cleans its timer up.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class ShieldNpcAiTests
{
	private const int Inggison = 210050000;
	private const int FieldGenerator = 260207;
	private const int IceSheet = 295074;

	private static readonly TimeSpan FirstCheck = TimeSpan.FromSeconds(20);
	private static readonly TimeSpan Recheck = TimeSpan.FromSeconds(15);

	private static int Count(BossAiHarness harness) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == IceSheet);

	/// <summary>The generator's ice-sheet timer, which nothing else exposes.</summary>
	private static object? CheckTask(Npc generator) => generator.GetAi().GetType()
		.GetField("checkTask", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
		.GetValue(generator.GetAi());

	private static (BossAiHarness, Npc, Player) Engaged()
	{
		BossAiHarness harness = BossAiHarness.For(Inggison)
			.WithWorldSize(4096)
			.WithAi(typeof(ShieldNpcAI), typeof(AggressiveNpcAI))
			.Build();
		Npc generator = harness.Spawn(FieldGenerator, 1780f, 2260f, 300f);
		Player player = harness.SpawnPlayer(1783f, 2262f, 300f);
		harness.Engage(generator, player);
		return (harness, generator, player);
	}

	[Fact]
	public void DropsAnIceSheetOnTheAttackerOnceItPassesThirtyFive()
	{
		var (harness, generator, player) = Engaged();
		using (harness)
		{
			harness.Clock.Advance(FirstCheck);
			Assert.Equal(0, Count(harness));

			BossAiHarness.SetHpPercent(generator, 40);
			harness.Clock.Advance(Recheck);
			Assert.Equal(0, Count(harness));

			BossAiHarness.SetHpPercent(generator, 30);
			harness.Clock.Advance(Recheck);
			Assert.Equal(1, Count(harness));

			// It lands on whoever it is fighting, within a couple of metres.
			Npc sheet = harness.LiveNpcs().Single(n => n.GetNpcId() == IceSheet);
			float dx = sheet.GetX() - player.GetX();
			float dy = sheet.GetY() - player.GetY();
			Assert.True(MathF.Sqrt((dx * dx) + (dy * dy)) <= 2f);
		}
	}

	[Fact]
	public void DropsOnlyOneHoweverLongTheSiegeRunsOn()
	{
		var (harness, generator, player) = Engaged();
		using (harness)
		{
			BossAiHarness.SetHpPercent(generator, 10);

			// Already well past the threshold, so nothing but the twenty-second delay is holding it.
			harness.Clock.Advance(FirstCheck - TimeSpan.FromSeconds(1));
			Assert.Equal(0, Count(harness));

			harness.Clock.Advance(TimeSpan.FromSeconds(1));
			Assert.Equal(1, Count(harness));

			// The check keeps ticking for the rest of the siege, as retail's timer does, so this is the
			// flag holding it to one rather than the timer having stopped.
			harness.Clock.Advance(TimeSpan.FromMinutes(5));
			Assert.Equal(1, Count(harness));
		}
	}

	[Fact]
	public void StopsCheckingWhenTheGeneratorFalls()
	{
		var (harness, generator, player) = Engaged();
		using (harness)
		{
			Assert.NotNull(CheckTask(generator));
			generator.GetAi().OnGeneralEvent(AiEventType.Died);

			// Asserting on the task itself rather than on the armed count: dying schedules tasks of its
			// own, so the count does not fall even when this one is cancelled. And asserting on the
			// effect would be vacuous, since the task body bails on IsDead() before doing anything --
			// a leaked repeating task is invisible from outside while still running once per siege.
			Assert.Null(CheckTask(generator));

			BossAiHarness.SetHpPercent(generator, 5);
			harness.Clock.Advance(TimeSpan.FromMinutes(5));
			Assert.Equal(0, Count(harness));
		}
	}
}
