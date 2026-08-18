using Aion.GameServer.Ai.Event;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="DefencePostFlagAI"/> and <see cref="DefencePostGuardAI"/>, translated from
/// retail patterns <c>IDF5_Under_01_VriFlag_0*</c> and the <c>IDF5_U1_War_Vri_Def*</c> family (see
/// <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// The flag shouts twice while it is being taken, with two different weights. Guards are kept out of
/// the players' known lists so a call is the only way either player can reach them, and the players
/// are Asmodian because the aggro list refuses hate between friends.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class DefencePostAiTests
{
	private const int OphidanBridge = 300590000;

	private const int Flag = 230416;
	private const int Combatant = 233475;
	private const int Scout = 233476;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(OphidanBridge).WithWorldSize(2048)
			.WithAi(typeof(DefencePostFlagAI), typeof(DefencePostGuardAI), typeof(AggressiveNpcAI),
				typeof(GeneralNpcAI))
			.Build();

	private static (BossAiHarness, Npc, Npc, Player) Post()
	{
		BossAiHarness harness = NewHarness();
		Npc flag = harness.Spawn(Flag, 300f, 300f, 200f);
		Npc guard = harness.Spawn(Combatant, 320f, 300f, 200f);
		Player raider = harness.SpawnPlayer(300f, 260f, 200f, race: Race.ASMODIANS);
		BossAiHarness.MakeMutuallyKnown(flag, guard);
		return (harness, flag, guard, raider);
	}

	private static void Strike(Npc flag, Creature attacker) =>
		flag.GetAi().OnCreatureEvent(AiEventType.Attack, attacker);

	/// <summary><b>The first blow commits the post's guards to whoever landed it.</b></summary>
	[Fact]
	public void TheFirstBlowCommitsTheGuards()
	{
		var (harness, flag, guard, raider) = Post();
		using BossAiHarness _h = harness;
		Assert.Null(guard.GetTarget());

		Strike(flag, raider);

		Assert.Same(raider, guard.GetTarget());
		Assert.Equal(100, guard.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>Every blow after that only points.</b> Retail's second call carries no hate at all, so a
	/// raider who takes over hitting the flag turns the guards without taking them off the first.
	/// </summary>
	[Fact]
	public void EveryBlowAfterThatOnlyPoints()
	{
		var (harness, flag, guard, raider) = Post();
		using BossAiHarness _h = harness;
		Strike(flag, raider);

		Player second = harness.SpawnPlayer(301f, 260f, 200f, race: Race.ASMODIANS);
		Strike(flag, second);

		Assert.Same(second, guard.GetTarget());
		Assert.Equal(0, guard.GetAggroList().GetHate(second));
		Assert.Equal(100, guard.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>The commitment is paid once.</b> Retail hangs it on <c>on_enter_attack_state</c>, which for
	/// a flag that never fights is the first blow and nothing else.
	/// </summary>
	[Fact]
	public void TheCommitmentIsPaidOnce()
	{
		var (harness, flag, guard, raider) = Post();
		using BossAiHarness _h = harness;

		for (int i = 0; i < 10; i++)
			Strike(flag, raider);

		Assert.Equal(100, guard.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>The two calls carry different distances.</b> A guard thirty-five to fifty metres out is
	/// pointed and never committed, which is retail's own thirty-five against fifty.
	/// </summary>
	[Fact]
	public void TheTwoCallsCarryDifferentDistances()
	{
		var (harness, flag, near, raider) = Post();
		using BossAiHarness _h = harness;

		// Forty-two metres from the flag -- outside the commitment's thirty-five and inside the
		// pointing call's fifty -- and two metres from the raider, which matters: a guard fifty metres
		// from the player cannot take hate on them at all, so a pin placed on the far side of the flag
		// measures the aggro list's own reach instead of retail's range.
		Npc far = harness.Spawn(Scout, 300f, 258f, 200f);
		BossAiHarness.MakeMutuallyKnown(flag, far);

		Strike(flag, raider);

		Assert.Equal(100, near.GetAggroList().GetHate(raider));
		Assert.Equal(0, far.GetAggroList().GetHate(raider));
		Assert.Same(raider, far.GetTarget());
	}

	/// <summary>And a guard beyond fifty metres hears neither.</summary>
	[Fact]
	public void AndBeyondFiftyItHearsNeither()
	{
		var (harness, flag, near, raider) = Post();
		using BossAiHarness _h = harness;

		Npc distant = harness.Spawn(Scout, 360f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(flag, distant);

		Strike(flag, raider);

		Assert.Null(distant.GetTarget());
	}

	/// <summary>The flag going home re-arms the commitment, so a second attempt pays it again.</summary>
	[Fact]
	public void GoingHomeReArmsTheCommitment()
	{
		var (harness, flag, guard, raider) = Post();
		using BossAiHarness _h = harness;
		Strike(flag, raider);

		flag.GetAi().OnGeneralEvent(AiEventType.BackHome);

		Npc latecomer = harness.Spawn(Combatant, 321f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(flag, latecomer);
		Strike(flag, raider);

		Assert.Equal(100, latecomer.GetAggroList().GetHate(raider));
	}
}
