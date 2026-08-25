using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Xunit;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// <b>On the rescue handlers, retail's "attacker" is the friend's attacker.</b>
/// </summary>
/// <remarks>
/// Retail spells the subjects on <c>on_see_friend_attacked</c> and <c>on_friend_spelled</c> exactly as
/// it spells an NPC's own — <c>OBJI_ATTACKER</c>, <c>OBJI_CASTER</c> — and lets the handler say which
/// creature is meant. This port keeps them in separate fields, so reading one as the other aimed the
/// rescue at whoever last hit the <em>rescuer</em>, which in a rescue is usually nobody.
/// <para>
/// <b>It was wrong in three places and none of them was pinned.</b> 1,032 <c>use_skill</c> rows, 814
/// hate rows and the 130 that name a friend's killer. <c>flee_from</c> was the only element that had
/// ever carried the remapping, and it carried a comment warning about exactly this.
/// </para>
/// <para>
/// The failure mode is why it went unnoticed: an absent creature makes the action a no-op rather than
/// an error, so a healer's rescue simply never happened and nothing said so.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public class RescueTargetingTests
{
	private const int Verteron = 210030000;

	/// <summary>
	/// <c>D2_FnS</c>. Its <c>on_see_friend_attacked</c> rung is deliberately the deterministic one —
	/// guarded on the friend's health and a first-time flag, with no chance roll to make the pin
	/// flake — and it ends by putting a hundred hate on whoever is doing the hitting.
	/// </summary>
	private const int Sentinel = 210126;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Verteron).WithWorldSize(4096)
			.WithAi(typeof(BattleCycleAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	[Fact]
	public void TheRescuerHatesWhoeverIsHittingItsFriendAndNotItsOwnAttacker()
	{
		using BossAiHarness harness = NewHarness();
		Npc watcher = harness.Spawn(Sentinel, 300f, 300f, 200f);
		Npc victim = harness.Spawn(Sentinel, 302f, 300f, 200f);
		Player raider = harness.SpawnPlayer(303f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(watcher, victim);
		BossAiHarness.MakeMutuallyKnown(watcher, raider);

		Assert.Equal(0, watcher.GetAggroList().GetHate(raider));

		// Retail's guard: the friend has to be under half.
		BossAiHarness.SetExactPercent(victim, 40);
		Aion.GameServer.Ai.FriendCombatNotice.Raise(victim, raider, spelled: false);

		Assert.Equal(100, watcher.GetAggroList().GetHate(raider));
		Assert.Same(raider, watcher.GetTarget());
	}

	/// <summary>
	/// <b>And a rescuer nobody has touched still commits.</b> This is the negative that makes the pin
	/// mean something: read as the rescuer's own attacker, <c>LastAttacker</c> is null here — the
	/// watcher has been hit by nobody — so the old reading produced no hate at all while looking
	/// exactly like a rescue that had simply not triggered.
	/// </summary>
	[Fact]
	public void ItCommitsEvenThoughNobodyHasHitTheRescuer()
	{
		using BossAiHarness harness = NewHarness();
		Npc watcher = harness.Spawn(Sentinel, 300f, 300f, 200f);
		Npc victim = harness.Spawn(Sentinel, 302f, 300f, 200f);
		Player raider = harness.SpawnPlayer(303f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(watcher, victim);
		BossAiHarness.MakeMutuallyKnown(watcher, raider);

		var ai = (Aion.GameServer.Ai.Pattern.PatternAi)watcher.GetAi();
		Assert.Null(ai.LastAttacker);

		BossAiHarness.SetExactPercent(victim, 40);
		Aion.GameServer.Ai.FriendCombatNotice.Raise(victim, raider, spelled: false);

		Assert.True(watcher.GetAggroList().GetHate(raider) > 0,
			"the rescuer put no hate on the creature hitting its friend");
	}

	/// <summary>A friend still healthy is not worth answering, which is retail's own guard.</summary>
	[Fact]
	public void AHealthyFriendDrawsNoRescue()
	{
		using BossAiHarness harness = NewHarness();
		Npc watcher = harness.Spawn(Sentinel, 300f, 300f, 200f);
		Npc victim = harness.Spawn(Sentinel, 302f, 300f, 200f);
		Player raider = harness.SpawnPlayer(303f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(watcher, victim);
		BossAiHarness.MakeMutuallyKnown(watcher, raider);

		BossAiHarness.SetExactPercent(victim, 90);
		Aion.GameServer.Ai.FriendCombatNotice.Raise(victim, raider, spelled: false);

		Assert.Equal(0, watcher.GetAggroList().GetHate(raider));
	}
}
