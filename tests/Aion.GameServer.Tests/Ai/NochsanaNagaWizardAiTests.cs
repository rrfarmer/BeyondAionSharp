using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="NochsanaNagaProtectorAI"/> and <see cref="NochsanaNagaTeleporterAI"/>,
/// translated from retail patterns <c>MiNaga_WeA</c> and <c>MiNaga_WeB</c> (see
/// <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Nochsana Training Camp's two naga wizards call each other when pulled, and the Teleporter brings
/// reservists. The pins hold the player forty metres from any listener, so a wizard that found the
/// fight by itself would fail rather than pass.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class NochsanaNagaWizardAiTests
{
	private const int NochsanaTrainingCamp = 300030000;

	private const int Protector = 256690;
	private const int Teleporter = 256691;
	private const int Reservist = 290163;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(NochsanaTrainingCamp).WithWorldSize(2048)
			.WithAi(typeof(NochsanaNagaProtectorAI), typeof(NochsanaNagaTeleporterAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static void Advance(BossAiHarness harness, Npc npc, Player player, int seconds)
	{
		for (int i = 0; i < seconds; i++)
		{
			BossAiHarness.Rehate(npc, player);
			BossAiHarness.KeepAlive(player);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}
	}

	private static int Standing(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary><b>Pulling the Teleporter puts a reservist on the player who pulled.</b></summary>
	[Fact]
	public void PullingTheTeleporterCallsAReservist()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Teleporter, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(340f, 300f, 200f);

		harness.Engage(boss, player);

		Assert.Equal(1, Standing(harness, Reservist));
		Npc reservist = harness.LiveNpcs().Single(n => n.GetNpcId() == Reservist);
		Assert.True(Math.Abs(reservist.GetX() - 340f) < 6f, $"{reservist.GetX()} is not by the player");
	}

	/// <summary><b>And a second one thirty seconds later, once, while he is still above seventy.</b></summary>
	[Fact]
	public void AndASecondThirtySecondsLater()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Teleporter, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(340f, 300f, 200f);
		harness.Engage(boss, player);
		BossAiHarness.SetExactPercent(boss, 90);

		Advance(harness, boss, player, 25);
		Assert.Equal(1, Standing(harness, Reservist));

		Advance(harness, boss, player, 10);
		Assert.Equal(2, Standing(harness, Reservist));

		// And once: the flag var stops the thirty-second relay calling a third.
		Advance(harness, boss, player, 120);
		Assert.Equal(2, Standing(harness, Reservist));
	}

	/// <summary>Below seventy the second one never comes at all.</summary>
	[Fact]
	public void BelowSeventyTheSecondNeverComes()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Teleporter, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(340f, 300f, 200f);
		harness.Engage(boss, player);
		BossAiHarness.SetExactPercent(boss, 60);

		Advance(harness, boss, player, 120);

		Assert.Equal(1, Standing(harness, Reservist));
	}

	/// <summary>
	/// <b>Pulling one wizard brings the other.</b> The Protector stands twenty metres from the
	/// Teleporter and forty from the player, so the call is the only way he reaches it.
	/// </summary>
	[Fact]
	public void PullingOneWizardBringsTheOther()
	{
		using BossAiHarness harness = NewHarness();
		Npc teleporter = harness.Spawn(Teleporter, 300f, 300f, 200f);
		Npc protector = harness.Spawn(Protector, 315f, 300f, 200f);
		Player player = harness.SpawnPlayer(300f, 260f, 200f);
		BossAiHarness.MakeMutuallyKnown(teleporter, protector);
		BossAiHarness.MakeMutuallyKnown(protector, player);
		Assert.Null(protector.GetTarget());

		harness.Engage(teleporter, player);

		Assert.Same(player, protector.GetTarget());
	}

	/// <summary>And the other way round: the Protector's call carries five metres further.</summary>
	[Fact]
	public void AndTheProtectorsCallCarriesFurther()
	{
		using BossAiHarness harness = NewHarness();
		Npc protector = harness.Spawn(Protector, 300f, 300f, 200f);
		Npc teleporter = harness.Spawn(Teleporter, 322f, 300f, 200f);
		Player player = harness.SpawnPlayer(300f, 260f, 200f);
		BossAiHarness.MakeMutuallyKnown(protector, teleporter);
		BossAiHarness.MakeMutuallyKnown(teleporter, player);

		harness.Engage(protector, player);

		// Twenty-two metres: inside the Protector's twenty-five and outside the Teleporter's twenty.
		Assert.Same(player, teleporter.GetTarget());
	}

	/// <summary>
	/// <b>The Protector answers one call a fight and no more.</b> Retail's test-and-set flag is what
	/// stops two wizards bouncing each other between players for the length of the fight.
	/// </summary>
	[Fact]
	public void TheProtectorAnswersOneCallAFight()
	{
		using BossAiHarness harness = NewHarness();
		Npc protector = harness.Spawn(Protector, 300f, 300f, 200f);
		Npc caller = harness.Spawn(Teleporter, 305f, 300f, 200f);
		Player first = harness.SpawnPlayer(300f, 260f, 200f);
		Player second = harness.SpawnPlayer(300f, 261f, 200f);
		BossAiHarness.MakeMutuallyKnown(caller, protector);
		BossAiHarness.MakeMutuallyKnown(protector, first);
		BossAiHarness.MakeMutuallyKnown(protector, second);

		// Called by hand rather than by a pull, so the two calls name two different players.
		NpcMessageBus.Broadcast(caller, NochsanaNagaProtectorAI.Call, first, 30f);
		Assert.Same(first, protector.GetTarget());

		NpcMessageBus.Broadcast(caller, NochsanaNagaProtectorAI.Call, second, 30f);
		Assert.Same(first, protector.GetTarget());
	}

	/// <summary>Both of the Teleporter's exits take the reservists with him.</summary>
	[Fact]
	public void BothExitsClearTheReservists()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Teleporter, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(340f, 300f, 200f);
		harness.Engage(boss, player);
		Assert.Equal(1, Standing(harness, Reservist));

		boss.GetAi().OnGeneralEvent(AiEventType.BackHome);

		Assert.Equal(0, Standing(harness, Reservist));
	}
}
