using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The fortress killer and the guards it comes for — retail's npc-versus-npc call family.
/// </summary>
/// <remarks>
/// Separate from <c>AbyssGuardCallAI</c>'s 23000 and the opposite of it in every way that matters: the
/// message names its <b>sender</b> rather than a player, and carries a million hate points rather than
/// one. The killer broadcasts 30001 as it wakes and every protector within fifty metres comes for it; a
/// protector broadcasts 30003 as it dies and the killer stands down.
/// <para>
/// None of it ran before: ten of the killers had no <c>ai</c> attribute at all and the rest were on
/// plain <c>aggressive</c> or on the guard-call class, which only knows 23000.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class FortressKillerCallTests
{
	private const int Reshanta = 400010000;

	/// <summary>An artifact protector, and a killer that hunts protectors.</summary>
	private const int Protector = 251450;
	private const int Killer = 251463;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Reshanta).WithWorldSize(2048)
			.WithAi(typeof(ArtifactProtectorAI), typeof(FortressKillerAI), typeof(AbyssGuardCallAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI)).Build();

	/// <summary>
	/// <b>A killer waking does not currently bring the protectors onto it, and this records why.</b>
	/// </summary>
	/// <remarks>
	/// Retail's rung is <c>add_hate_point target=OBJI_MESSAGE_SENDER points_to_add=1000000</c> guarded by
	/// <c>is_enemy who=OBJI_MESSAGE_SENDER</c>, and the send half is implemented — but
	/// <b>every npc in this family is <c>race="DRAKAN"</c>, <c>tribe="GUARD_DRAGON"</c></b>, protectors
	/// and killers alike, so our aggro list refuses the hate and the protector stays where it is.
	/// <para>
	/// <b>This asserts the wrong behaviour on purpose.</b> The alternative was to delete the pin and
	/// leave the gap invisible. Retail plainly intends these two to fight — the whole family exists for
	/// it — so either the client's tribe relations make <c>GUARD_DRAGON</c> hostile to itself under some
	/// condition our model does not carry, or the killers' real tribe differs from what our
	/// <c>npc_templates</c> says. Until that is settled, the 30001 half is inert and this pin will fail
	/// the day it stops being. That is the intended signal, not a regression.
	/// </para>
	/// </remarks>
	[Fact]
	public void AKillerWakingDoesNotYetBringTheProtectorsOntoIt()
	{
		using BossAiHarness harness = NewHarness();
		Npc protector = harness.Spawn(Protector, 300f, 300f, 200f);
		Npc killer = harness.Spawn(Killer, 320f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(killer, protector);

		killer.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.SPAWNED);

		// Same race and tribe on both sides, so is_enemy is false and the million points never land.
		Assert.Equal(protector.GetObjectTemplate().GetTribe(), killer.GetObjectTemplate().GetTribe());
		Assert.Null(protector.GetTarget());
	}

	/// <summary>
	/// <b>A protector's death sends the killer home.</b>
	/// </summary>
	/// <remarks>
	/// Retail's <c>on_die</c> broadcast, answered by the killer's highest-priority rung — it outranks
	/// the fight, because a killer with nothing left to kill has no reason to stand there.
	/// </remarks>
	[Fact]
	public void AProtectorsDeathSendsTheKillerHome()
	{
		using BossAiHarness harness = NewHarness();
		Npc protector = harness.Spawn(Protector, 300f, 300f, 200f);
		Npc killer = harness.Spawn(Killer, 320f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(protector, killer);
		Player player = harness.SpawnPlayer(302f, 300f, 200f);
		harness.Engage(protector, player);
		Assert.Equal(1, harness.LiveNpcs().Count(n => n.GetNpcId() == Killer));

		try { protector.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.Died); }
		catch (Exception) { /* the siege services below the broadcast are not stood up here */ }

		Assert.Equal(0, harness.LiveNpcs().Count(n => n.GetNpcId() == Killer));
	}

	/// <summary>
	/// <b>And a protector that is only wounded does not.</b>
	/// </summary>
	/// <remarks>
	/// Retail hangs the broadcast on <c>on_die</c> alone. Without this the killer could be sent home by
	/// any event and the pin above would not know.
	/// </remarks>
	[Fact]
	public void AndAProtectorThatIsOnlyWoundedDoesNot()
	{
		using BossAiHarness harness = NewHarness();
		Npc protector = harness.Spawn(Protector, 300f, 300f, 200f);
		Npc killer = harness.Spawn(Killer, 320f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(protector, killer);
		Player player = harness.SpawnPlayer(302f, 300f, 200f);
		harness.Engage(protector, player);

		BossAiHarness.SetHpPercent(protector, 15);
		BossAiHarness.Wound(protector, player, damage: 5000);

		Assert.Equal(1, harness.LiveNpcs().Count(n => n.GetNpcId() == Killer));
	}

	/// <summary>
	/// <b>The protectors ignore the guards' own call for help.</b>
	/// </summary>
	/// <remarks>
	/// 23000 reaches the same npcs and means something else: a player to join on, at one hate point. A
	/// protector that answered it with a million would turn every guard call in a fortress into a
	/// stampede onto whoever pulled first.
	/// </remarks>
	[Fact]
	public void TheProtectorsIgnoreTheGuardsOwnCallForHelp()
	{
		using BossAiHarness harness = NewHarness();
		Npc protector = harness.Spawn(Protector, 300f, 300f, 200f);
		Npc killer = harness.Spawn(Killer, 320f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(killer, protector);

		Aion.GameServer.Ai.NpcMessageBus.Broadcast(killer, AbyssGuardCallAI.CallForHelp, killer, 50f);

		Assert.Null(protector.GetTarget());
	}

	/// <summary>
	/// <b>And another message does not send it home.</b>
	/// </summary>
	/// <remarks>
	/// The stand-down rung sits at priority 100, above the fight, so a killer that answered it on any
	/// message at all would vanish the moment a guard nearby called for help on 23000 — which happens
	/// constantly in a fortress. Deleting the message guard passed every other pin in this file.
	/// </remarks>
	[Fact]
	public void AndAnotherMessageDoesNotSendItHome()
	{
		using BossAiHarness harness = NewHarness();
		Npc killer = harness.Spawn(Killer, 320f, 300f, 200f);
		Npc other = harness.Spawn(Protector, 300f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(other, killer);

		Aion.GameServer.Ai.NpcMessageBus.Broadcast(other, AbyssGuardCallAI.CallForHelp, other, 50f);

		Assert.Equal(1, harness.LiveNpcs().Count(n => n.GetNpcId() == Killer));
	}
}
