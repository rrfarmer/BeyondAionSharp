using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The mosqua egg, which hatched on a clock instead of on being seen.
/// </summary>
/// <remarks>
/// Retail's only rung is <c>on_see_user</c>, flag-guarded so it fires once: cast, put a hatchling on
/// itself for eighteen seconds, and despawn. This class hatched seventeen seconds after spawning
/// whatever anyone did — and hatched the wrong npc, the queen's summon rather than the egg's own.
/// <para>
/// Found by <c>audit_lifetime_conflicts.py</c>, which flagged a seventeen-second self-delete against a
/// retail lifetime of three hundred.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class MosquaEggAiTests
{
	private const int TalocsHollow = 300190000;

	private const int Egg = 282006;

	/// <summary>What retail hatches, and the queen's summon this class used instead.</summary>
	private const int Hatchling = 282082;
	private const int QueensSummon = 217132;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(TalocsHollow).WithWorldSize(2048)
			.WithAi(typeof(MosquaEggAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>
	/// <b>An egg nobody goes near never opens.</b>
	/// </summary>
	/// <remarks>
	/// This is the mechanic: a room of eggs is something a raid can walk around. On a seventeen-second
	/// clock it was not.
	/// </remarks>
	[Fact]
	public void AnEggNobodyApproachesStaysShut()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(Egg, 300f, 300f, 200f);

		harness.Clock.Advance(TimeSpan.FromMinutes(2));

		Assert.Equal(1, Count(harness, Egg));
		Assert.Equal(0, Count(harness, Hatchling));
	}

	/// <summary>
	/// <b>And one a player walks up to opens at once, into retail's hatchling.</b>
	/// </summary>
	/// <remarks>
	/// 282082 is <c>BIDElim_NeutWorkmanflySummon_51_n</c>; 217132 is the queen's summon, which the
	/// instance places from its own spawn table. Both are called "spawned supraklaw", which is what let
	/// the wrong one pass unnoticed.
	/// </remarks>
	[Fact]
	public void AnEggAPlayerApproachesHatchesRetailsOwn()
	{
		using BossAiHarness harness = NewHarness();
		Npc egg = harness.Spawn(Egg, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(302f, 300f, 200f);

		egg.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.CREATURE_SEE, player);

		Assert.Equal(1, Count(harness, Hatchling));
		Assert.Equal(0, Count(harness, QueensSummon));
		Assert.Equal(0, Count(harness, Egg));
	}

	/// <summary>
	/// <b>The hatchling stands eighteen seconds.</b> It had no lifetime at all.
	/// </summary>
	[Fact]
	public void TheHatchlingStandsEighteenSeconds()
	{
		using BossAiHarness harness = NewHarness();
		Npc egg = harness.Spawn(Egg, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(302f, 300f, 200f);
		egg.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.CREATURE_SEE, player);

		harness.Clock.Advance(TimeSpan.FromSeconds(17));
		Assert.Equal(1, Count(harness, Hatchling));

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.Equal(0, Count(harness, Hatchling));
	}

	/// <summary>
	/// <b>An egg opens once, however many players walk past.</b>
	/// </summary>
	/// <remarks>
	/// Retail's rung carries a test-and-set flag var.
	/// <para>
	/// <b>This passes for a reason other than the flag, and the mutation sweep showed it:</b> deleting
	/// the once-guard survives, because the egg removes itself as it hatches and the second event never
	/// reaches a live npc. The guard is belt-and-braces behind that delete rather than the thing being
	/// asserted here. It is kept because it is retail's, and because an egg that ever stops deleting
	/// itself would need it.
	/// </para>
	/// </remarks>
	[Fact]
	public void AnEggOpensOnlyOnce()
	{
		using BossAiHarness harness = NewHarness();
		Npc egg = harness.Spawn(Egg, 300f, 300f, 200f);
		Player first = harness.SpawnPlayer(302f, 300f, 200f);
		Player second = harness.SpawnPlayer(303f, 300f, 200f);

		egg.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.CREATURE_SEE, first);
		egg.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.CREATURE_SEE, second);

		Assert.Equal(1, Count(harness, Hatchling));
	}

	/// <summary>
	/// <b>An npc walking past leaves it shut.</b> Retail's rung is <c>on_see_user</c>.
	/// </summary>
	/// <remarks>
	/// Taloc's Hollow is full of wandering supraklaw, so an egg that opened for anything that saw it
	/// would empty the room on its own before a raid arrived.
	/// </remarks>
	[Fact]
	public void AnNpcWalkingPastLeavesItShut()
	{
		using BossAiHarness harness = NewHarness();
		Npc egg = harness.Spawn(Egg, 300f, 300f, 200f);
		Npc passerby = harness.Spawn(QueensSummon, 302f, 300f, 200f);

		egg.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.CREATURE_SEE, passerby);

		Assert.Equal(1, Count(harness, Egg));
		Assert.Equal(0, Count(harness, Hatchling));
	}
}
