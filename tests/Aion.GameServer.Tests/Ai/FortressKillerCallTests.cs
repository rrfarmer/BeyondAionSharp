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
	private const int Protector = 251467;
	private const int Killer = 251463;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Reshanta).WithWorldSize(2048)
			.WithAi(typeof(ArtifactProtectorAI), typeof(FortressKillerAI), typeof(AbyssGuardCallAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI)).Build();

	/// <summary>
	/// <b>A killer waking brings the protectors onto it.</b>
	/// </summary>
	/// <remarks>
	/// The protector is never touched and no player is involved: this is the mechanic that takes a
	/// fortress's guards down without either side being played.
	/// <para>
	/// <b>The protector here is 251467, <c>PROTECTGUARD_LIGHT</c>, and the choice is the whole pin.</b>
	/// Retail guards the rung with <c>is_enemy who=OBJI_MESSAGE_SENDER</c>, and a hundred and fifty-five
	/// of the artifact protectors share the killers' own <c>GUARD_DRAGON</c> tribe — see
	/// <see cref="ASameTribeProtectorCorrectlyIgnoresIt"/>. Picking one of those first made this look
	/// like a faction-data gap when it is retail behaving exactly as its data says.
	/// </para>
	/// </remarks>
	[Fact]
	public void AKillerWakingBringsTheProtectorsOntoIt()
	{
		using BossAiHarness harness = NewHarness();
		Npc protector = harness.Spawn(Protector, 300f, 300f, 200f);
		Npc killer = harness.Spawn(Killer, 320f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(killer, protector);

		killer.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.SPAWNED);

		Assert.Equal(killer, protector.GetTarget());
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

	/// <summary>
	/// <b>A protector of the killer's own tribe correctly ignores it.</b>
	/// </summary>
	/// <remarks>
	/// Not a limitation — retail's own data. 251450 is <c>GUARD_DRAGON</c>, the same tribe the killers
	/// carry, and our <c>tribe_relations.xml</c> matches the client's <c>npc_tribe_relation.xml</c>
	/// entry for it exactly: one <c>friendly</c> line naming two teleporters, and no hostility to
	/// anything. So <c>is_enemy</c> is false and the million points never land.
	/// <para>
	/// The mechanic works between the killers and the three hundred and thirty-two protectors on
	/// <c>PROTECTGUARD_LIGHT</c> and <c>PROTECTGUARD_DARK</c>, and between the Advance killers and the
	/// village guards their tribe lists as <c>aggro</c>. Pinned so that the exception is recorded as
	/// intended rather than rediscovered as a bug.
	/// </para>
	/// </remarks>
	[Fact]
	public void ASameTribeProtectorCorrectlyIgnoresIt()
	{
		using BossAiHarness harness = NewHarness();
		Npc sameTribe = harness.Spawn(251450, 300f, 300f, 200f);
		Npc killer = harness.Spawn(Killer, 320f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(killer, sameTribe);

		killer.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.SPAWNED);

		Assert.Equal(sameTribe.GetObjectTemplate().GetTribe(), killer.GetObjectTemplate().GetTribe());
		Assert.Null(sameTribe.GetTarget());
	}

	/// <summary>
	/// <b>And a protector already fighting a player drops it for the killer.</b>
	/// </summary>
	/// <remarks>
	/// This is what retail's million points are for. A protector that kept tanking its attacker would
	/// leave the killer unopposed, which is the mechanic failing quietly rather than loudly.
	/// <para>
	/// <b>The magnitude itself is not pinned, and cannot be from here.</b> Dropping
	/// <c>DropEverything</c> from a million to <b>1</b> passes this pin at any damage the player deals,
	/// because <see cref="SummonOrder"/> ends by targeting whoever is <em>then</em> most-hated and a
	/// fresh hate entry takes that place regardless of size. That is <see cref="SummonOrder"/>'s own
	/// documented behaviour rather than a fault here, but it means the number is held by review.
	/// </para>
	/// </remarks>
	[Fact]
	public void AndAProtectorAlreadyOnAPlayerDropsIt()
	{
		using BossAiHarness harness = NewHarness();
		Npc protector = harness.Spawn(Protector, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(302f, 300f, 200f);
		harness.Engage(protector, player);
		// Enough damage that a single hate point cannot outrank it. At 5000 it could, so the size of
		// retail's points_to_add was unpinned and dropping it to 1 passed.
		BossAiHarness.Wound(protector, player, damage: 500_000);
		Assert.Equal(player, protector.GetTarget());

		Npc killer = harness.Spawn(Killer, 320f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(killer, protector);
		killer.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.SPAWNED);

		Assert.Equal(killer, protector.GetTarget());
	}
}
