using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Behavioural pins for the Adma Stronghold coffin chain — <see cref="SuspiciousCoffinAI"/> and
/// <see cref="LordLannokAI"/>, from retail patterns <c>NoAction_CoffinA</c>..<c>F</c> and
/// <c>Adma_DeathknightNamed</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// This is a two-NPC mechanic, so it is tested as one: the coffin shouting and Lannok answering are
/// each useless alone, and neither would fail visibly on its own. None of it happened before — the
/// coffins were plain aggressive NPCs and nothing listened to them.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class AdmaCoffinAiTests
{
	private const int AdmaStronghold = 320130000;
	private const int LordLannok = 214696;
	private const int SuspiciousCoffin = 280942;
	private const int AnotherCoffin = 281055;

	private static BossAiHarness NewHarness() => BossAiHarness.For(AdmaStronghold)
		.WithWorldSize(2048)
		.WithAi(typeof(LordLannokAI), typeof(SuspiciousCoffinAI), typeof(AggressiveNpcAI))
		.Build();

	/// <summary>Lannok and a coffin within his earshot, plus the player who will disturb it.</summary>
	private static (BossAiHarness, Npc Lannok, Npc Coffin, Player) Room()
	{
		BossAiHarness harness = NewHarness();
		Npc lannok = harness.Spawn(LordLannok, 600f, 745f, 200f);
		Npc coffin = harness.Spawn(SuspiciousCoffin, 605f, 750f, 200f);
		Player player = harness.SpawnPlayer(607f, 752f, 200f);
		BossAiHarness.MakeMutuallyKnown(lannok, coffin);
		BossAiHarness.MakeMutuallyKnown(coffin, player);
		BossAiHarness.MakeMutuallyKnown(lannok, player);
		return (harness, lannok, coffin, player);
	}

	[Fact]
	public void BringsLannokDownOnWhoeverDisturbsACoffin()
	{
		var (harness, lannok, coffin, player) = Room();
		using (harness)
		{
			Assert.Equal(0, lannok.GetAggroList().GetHate(player));

			harness.Engage(coffin, player);

			// The coffin shouts, Lannok hears it, and the player who hit the coffin is now his problem.
			Assert.True(lannok.GetAggroList().GetHate(player) > 0,
				"disturbing a coffin should have pulled Lord Lannok onto the player");
			Assert.Equal(player, lannok.GetTarget());
		}
	}

	[Fact]
	public void LeavesLannokAloneWhenNothingHasTouchedACoffin()
	{
		var (harness, lannok, coffin, player) = Room();
		using (harness)
		{
			// Nothing disturbed, nothing shouted. Guards against the alarm being wired to something
			// that happens anyway, like the coffin merely existing near him.
			harness.Clock.Advance(TimeSpan.FromMinutes(1));
			Assert.Equal(0, lannok.GetAggroList().GetHate(player));
		}
	}

	[Fact]
	public void ShoutsOnceUntilLannokSoundsTheAllClear()
	{
		var (harness, lannok, coffin, player) = Room();
		using (harness)
		{
			harness.Engage(coffin, player);
			int first = lannok.GetAggroList().GetHate(player);

			// Still being hit, but the alarm is a one-shot: hitting a coffin repeatedly must not stack
			// hate on Lannok forever.
			for (int i = 0; i < 5; i++)
				coffin.GetAi().OnCreatureEvent(AiEventType.Attack, player);
			Assert.Equal(first, lannok.GetAggroList().GetHate(player));
		}
	}

	[Fact]
	public void ReArmsEveryCoffinWhenLannokFalls()
	{
		var (harness, lannok, coffin, player) = Room();
		using (harness)
		{
			Npc second = harness.Spawn(AnotherCoffin, 596f, 741f, 200f);
			BossAiHarness.MakeMutuallyKnown(lannok, second);
			BossAiHarness.MakeMutuallyKnown(second, player);

			harness.Engage(coffin, player);
			harness.Engage(second, player);
			int afterAlarms = lannok.GetAggroList().GetHate(player);
			Assert.True(afterAlarms > 0);

			// His death is the all-clear: it reaches the coffins and resets their alarms. Asserting via
			// the flag rather than by re-shouting, since he is dead and no longer listening.
			lannok.GetAi().OnGeneralEvent(AiEventType.Died);

			foreach (Npc c in new[] { coffin, second })
			{
				Assert.False(Alerted(c), "the all-clear should have re-armed the coffin's alarm");
			}
		}
	}

	/// <summary>Reads the coffin's retail <c>BETA_1</c> flag, which nothing else exposes.</summary>
	private static bool Alerted(Npc coffin)
	{
		var flags = (bool[])coffin.GetAi().GetType().BaseType!
			.GetField("flags", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
			.GetValue(coffin.GetAi())!;
		return flags[1];
	}
}
