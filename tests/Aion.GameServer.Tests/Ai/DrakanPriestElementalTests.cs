using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The drakan guards' fire elemental, which the extractor dropped on the floor.
/// </summary>
/// <remarks>
/// <c>extract_guard_reinforcements.py</c> resolves summon devnames through <c>ai_binding.tsv</c>, and
/// that table only knows npcs which carry an AI pattern. Five fire-elemental devnames are not in it,
/// and because the extractor deliberately discards a whole band when one devname fails — so that a
/// guard missing its healer cannot quietly look like a guard that never heals — <b>19 bands across 15
/// guards were dropped</b>.
/// <para>
/// Adjutant ursanafi (<c>DrGuard_PhB_L48</c>) is the clearest of them. Retail gives him a fire
/// elemental between 36 and 70 and an earth elemental below 35. Only the earth band survived
/// extraction, so in play he had one tier where retail gives two, and the emitted table read as though
/// that were retail's own shape.
/// </para>
/// <para>
/// The fix is the one already adopted everywhere else in this toolchain: consult the client's own npc
/// tables (<c>client_npc_names.py</c>, 87,734 names) after the binding table rather than instead of
/// it, so nothing already resolving can move. The regenerated table added 81 rows and removed none.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class DrakanPriestElementalTests
{
	/// <summary>Indratu Fortress, where the priest stands.</summary>
	private const int IndratuFortress = 310090000;

	/// <summary>Adjutant ursanafi, <c>DrGuard_PhB_L48</c>.</summary>
	private const int Priest = 214163;

	/// <summary>Retail's two tiers, written out rather than read back from the generated table.</summary>
	private const int FireSpirit = 296348;
	private const int EarthSpirit = 296349;

	private static (BossAiHarness, Npc, Player) Engaged(int npcId, int hpPercent)
	{
		BossAiHarness harness = BossAiHarness.For(IndratuFortress).WithWorldSize(2048)
			.WithAi(typeof(GuardReinforcementAI), typeof(ServantNpcAI), typeof(AggressiveNpcAI)).Build();
		Npc guard = harness.Spawn(npcId, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(302f, 302f, 200f);
		BossAiHarness.SetHpPercent(guard, hpPercent);
		harness.Engage(guard, player);
		return (harness, guard, player);
	}

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>
	/// Runs the heartbeat until something is called. This guard's bands are certain rather than a coin
	/// flip, so two heartbeats is a generous window.
	/// </summary>
	private static void RunUntilCalled(BossAiHarness harness, Npc guard, Player player)
	{
		for (int i = 0; i < 2 * 21; i++)
		{
			BossAiHarness.Rehate(guard, player);
			BossAiHarness.KeepAlive(player);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
			if (Count(harness, FireSpirit) + Count(harness, EarthSpirit) > 0)
				return;
		}
	}

	/// <summary>
	/// <b>The upper band calls the fire elemental.</b> This is the band that did not exist: before the
	/// devname fallback, the generated table held one row for this guard and it was the earth band.
	/// </summary>
	[Fact]
	public void TheUpperBandCallsTheFireElemental()
	{
		(BossAiHarness harness, Npc guard, Player player) = Engaged(Priest, 60);
		using BossAiHarness _ = harness;

		RunUntilCalled(harness, guard, player);

		Assert.Equal(1, Count(harness, FireSpirit));
		Assert.Equal(0, Count(harness, EarthSpirit));
	}

	/// <summary>
	/// <b>The lower band still calls the earth elemental.</b> The band that survived extraction — pinned
	/// because the change that added the other one could just as easily have moved this one.
	/// </summary>
	[Fact]
	public void TheLowerBandStillCallsTheEarthElemental()
	{
		(BossAiHarness harness, Npc guard, Player player) = Engaged(Priest, 20);
		using BossAiHarness _ = harness;

		RunUntilCalled(harness, guard, player);

		Assert.Equal(1, Count(harness, EarthSpirit));
		Assert.Equal(0, Count(harness, FireSpirit));
	}

	/// <summary>
	/// <b>Every fire elemental the drakan guards call is somewhere in the table.</b>
	/// </summary>
	/// <remarks>
	/// The first attempt at a generalising pin here asserted that no guard has an upper band without a
	/// lower one, on the theory that a dropped band would leave that gap. <b>It passed against the
	/// broken table.</b> A failed devname does not remove one band — the extractor discards the whole
	/// band it appears in, and these five appear in the low band too, so the guards lost both and left
	/// no gap to find.
	/// <para>
	/// These are the five devnames that resolved to nothing, by id. Naming them is the pin: they are
	/// what "the binding table does not know this npc" looked like in the emitted output, and a
	/// resolver change that reopens the hole drops them out again. Verified by regenerating the table
	/// from the pre-fix extraction, where this fails and the invariant it replaced does not.
	/// </para>
	/// </remarks>
	[Theory]
	[InlineData(294785)]
	[InlineData(295157)]
	[InlineData(296087)]
	[InlineData(296887)]
	[InlineData(296348)]
	public void EveryDrakanFireElementalIsCalledBySomeGuard(int elementalId)
	{
		bool called = GuardReinforcements.ByGuard.Values
			.Any(bands => bands.Any(b => b.Summons.Any(s => s.Item1 == elementalId)));

		Assert.True(called,
			$"elemental {elementalId} is called by no guard in the table, which is what a devname "
				+ "that resolves to nothing looks like once the band around it has been discarded");
	}
}
