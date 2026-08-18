using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for Adma Stronghold's zombie traps, translated from retail pattern <c>ND2_Trap_IDDF2A</c>
/// (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
[Collection("GoldenDataManager")]
public sealed class ZombieTrapAiTests
{
	private const int AdmaStronghold = 320130000;
	private const int Trap = 281027;
	private const int Zombie = 281028;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(AdmaStronghold).WithWorldSize(2048)
			.WithAi(typeof(ZombieTrapAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static List<Npc> Live(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Where(n => n.GetNpcId() == npcId).ToList();

	/// <summary>Springs one trap on a passing player and reports how many zombies it left.</summary>
	/// <param name="rolling">
	/// Hands the trap the production dice. The harness forces rolled guards to pass by default, which
	/// makes the two-zombie branch certain -- right for every pin that counts, wrong for the one whose
	/// subject is that both counts occur.
	/// </param>
	private static (int zombies, bool trapGone) Spring(BossAiHarness harness, Race race = Race.ELYOS,
		bool rolling = false)
	{
		Npc trap = harness.Spawn(Trap, 300f, 300f, 200f);
		if (rolling)
			BossAiHarness.RandomRolls(trap);
		Player passer = harness.SpawnPlayer(302f, 300f, 200f, race: race);
		trap.GetAi().OnCreatureEvent(AiEventType.CreatureSee, passer);
		return (Live(harness, Zombie).Count, !trap.IsSpawned());
	}

	/// <summary>
	/// <b>A trap goes off on a passing player and is gone in the same breath.</b> It has been a
	/// harmless prop in the corridor since the instance was ported.
	/// </summary>
	[Fact]
	public void ATrapGoesOffAndIsGone()
	{
		using BossAiHarness harness = NewHarness();

		var (zombies, trapGone) = Spring(harness);

		Assert.InRange(zombies, 2, 3);
		Assert.True(trapGone, "the trap was still standing after it fired");
	}

	/// <summary>
	/// <b>The unlucky roll gives you fewer zombies, not more.</b> Retail puts
	/// <c>test_probability 50</c> on the branch that spawns <b>two</b> and lets the fall-through spawn
	/// <b>three</b> — so the coin flip is a reprieve. Reading the priorities the other way round is the
	/// opposite fight and is invisible unless both counts are pinned.
	/// </summary>
	[Fact]
	public void BothCountsAppearAndTheChanceBranchIsTheSmaller()
	{
		bool sawTwo = false;
		bool sawThree = false;

		for (int i = 0; i < 60 && !(sawTwo && sawThree); i++)
		{
			using BossAiHarness harness = NewHarness();
			var (zombies, _) = Spring(harness, rolling: true);
			sawTwo |= zombies == 2;
			sawThree |= zombies == 3;
			Assert.InRange(zombies, 2, 3);
		}

		Assert.True(sawTwo, "never spawned the two-zombie burst");
		Assert.True(sawThree, "never spawned the three-zombie burst");
	}

	/// <summary>
	/// <b>An NPC walking past does not spring it.</b> Retail's handler is <c>on_see_user</c>; a trap
	/// that went off when the guard beside it wandered by would be spent before anyone arrived.
	/// </summary>
	/// <remarks>
	/// <b>Two guards on this class cannot be pinned with this npc, and both are recorded rather than
	/// covered by a pin that would pass for the wrong reason.</b>
	/// <list type="bullet">
	/// <item><description><c>is_enemy</c>: a monster is hostile to every player, so no player exists
	/// that fails it. A mutation removing the guard survives, and the survivor is honest.</description></item>
	/// <item><description>the <c>on_see_user</c> / <c>on_see_npc</c> split: this pin's seen NPC is
	/// another trap, which <c>is_enemy</c> would reject anyway — so a mutation that routes NPCs through
	/// the user handler also survives. Catching it needs an NPC hostile to the trap, and picking one on
	/// a guess is how a pin ends up measuring the tribe table instead of the split.</description></item>
	/// </list>
	/// </remarks>
	[Fact]
	public void AndOnSeeingAPlayerNotAnNpc()
	{
		using BossAiHarness harness = NewHarness();
		Npc trap = harness.Spawn(Trap, 300f, 300f, 200f);
		Npc other = harness.Spawn(Trap, 302f, 300f, 200f);

		trap.GetAi().OnCreatureEvent(AiEventType.CreatureSee, other);

		Assert.True(trap.IsSpawned(), "an NPC set the trap off");
		Assert.Empty(Live(harness, Zombie));
	}
}
