using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Behavioural pins for <see cref="VanukaInfernusAI"/>, translated from retail pattern
/// <c>Dragon_G3</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// He had no AI at all, in an instance where half the boss roster is implemented. Both NPCs asserted
/// here were spawned by nothing anywhere in the server.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class VanukaInfernusAiTests
{
	private const int DarkPoeta = 300040000;
	private const int VanukaInfernus = 215282;
	private const int FlameCenter = 281276;
	private const int FaithfulSubordinate = 281275;

	private static (BossAiHarness, Npc, Player) Engaged()
	{
		BossAiHarness harness = BossAiHarness.For(DarkPoeta)
			.WithWorldSize(2048)
			.WithAi(typeof(VanukaInfernusAI), typeof(VanukaLizardAI), typeof(NTrapAI), typeof(AggressiveNpcAI))
			.Build();
		Npc boss = harness.Spawn(VanukaInfernus, 1182f, 1235f, 143f);
		Player player = harness.SpawnPlayer(1184f, 1237f, 143f);
		harness.Engage(boss, player);
		return (harness, boss, player);
	}

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>Runs the clock while keeping him engaged, and reports the most flames seen at once.</summary>
	private static int PeakFlames(BossAiHarness harness, Npc boss, Player player, int seconds) =>
		harness.Watch(seconds, () => BossAiHarness.Rehate(boss, player), FlameCenter).Peak;

	[Fact]
	public void LightsTwoFlamesTheMomentTheFightStarts()
	{
		var (harness, _, _) = Engaged();
		using (harness)
		{
			// Nothing in the server spawned this NPC before; the opener drops a pair.
			Assert.Equal(2, Count(harness, FlameCenter));
		}
	}

	/// <summary>
	/// The opening pair goes off and is gone well inside the ten seconds he gives them.
	/// </summary>
	/// <remarks>
	/// <b>This pin used to say the opposite, and was right at the time.</b> It asserted the pair was
	/// still standing at nine seconds and gone at ten, which is what a flame center did while it was
	/// inert furniture on plain <c>aggressive</c>. It is a <c>NTrap_A</c> trap: it goes off the moment
	/// it appears and leaves when the cast lands. The ten-second <c>live_time</c> he spawns them with
	/// is the backstop for a trap whose cast never happens, not the length of the effect.
	/// <para>
	/// Fifth time a pin has had to change because a later port made its subject more complete.
	/// </para>
	/// </remarks>
	[Fact]
	public void TheOpeningFlamesGoOffWellInsideTheirTenSeconds()
	{
		var (harness, boss, player) = Engaged();
		using (harness)
		{
			Assert.Equal(2, Count(harness, FlameCenter));

			harness.Clock.Advance(TimeSpan.FromSeconds(5));

			Assert.Equal(0, Count(harness, FlameCenter));
		}
	}

	[Fact]
	public void DropsAFullRingOfFourOnceHeIsHurt()
	{
		var (harness, boss, player) = Engaged();
		using (harness)
		{
			BossAiHarness.SetHpPercent(boss, 70);

			// Let the opening pair burn out first. They last ten seconds and the first ring lands at
			// six, so measuring from the start would count 2 + 4 and say nothing about the ring.
			harness.Clock.Advance(TimeSpan.FromSeconds(11));

			// The mid-fight steps drop all four points at once, not one. A table that kept only the
			// last spawn per branch would give one flame here, which is how this was nearly written.
			Assert.Equal(4, PeakFlames(harness, boss, player, 40));
		}
	}

	[Fact]
	public void PutsThemAtFourDistinctPoints()
	{
		var (harness, boss, player) = Engaged();
		using (harness)
		{
			BossAiHarness.SetHpPercent(boss, 70);
			for (int i = 0; i < 40 && Count(harness, FlameCenter) < 4; i++)
			{
				BossAiHarness.Rehate(boss, player);
				harness.Clock.Advance(TimeSpan.FromSeconds(1));
			}

			var points = harness.LiveNpcs().Where(n => n.GetNpcId() == FlameCenter)
				.Select(n => ((int)MathF.Round(n.GetX()), (int)MathF.Round(n.GetY()))).Distinct().ToList();
			Assert.Equal(4, points.Count);
		}
	}

	[Fact]
	public void SwitchesToSummoningBelowThirty()
	{
		var (harness, boss, player) = Engaged();
		using (harness)
		{
			BossAiHarness.SetHpPercent(boss, 25);

			// Below 30 timer 0 hands over to a second chain that summons instead of burning.
			for (int i = 0; i < 60; i++)
			{
				BossAiHarness.Rehate(boss, player);
				harness.Clock.Advance(TimeSpan.FromSeconds(1));
			}
			Assert.True(Count(harness, FaithfulSubordinate) > 0,
				"below 30% he should have summoned a subordinate");
		}
	}

	[Fact]
	public void ClearsEverythingWhenHeDies()
	{
		var (harness, boss, player) = Engaged();
		using (harness)
		{
			Assert.True(Count(harness, FlameCenter) > 0);
			boss.GetAi().OnGeneralEvent(AiEventType.Died);
			Assert.Equal(0, Count(harness, FlameCenter));
			Assert.Equal(0, Count(harness, FaithfulSubordinate));
		}
	}

	/// <summary>
	/// Every subordinate it calls up below 30% comes with a rally carrying its own quarry. An idle
	/// lizard takes that quarry as its own; the pin hands the call straight to one, because the
	/// broadcast reaches whatever is nearby and the point is what the lizard does with it.
	/// </summary>
	[Fact]
	public void AnIdleLizardTakesTheBossesQuarry()
	{
		var (harness, boss, player) = Engaged();
		using BossAiHarness _h = harness;
		Npc lizard = harness.Spawn(FaithfulSubordinate, 1184f, 1239f, 143f);
		BossAiHarness.MakeMutuallyKnown(lizard, player);
		int before = lizard.GetAggroList().GetHate(player);

		var listener = (Aion.GameServer.Ai.INpcMessageListener)lizard.GetAi();
		listener.OnNpcMessage(boss, VanukaInfernusAI.RallyCall, player);

		Assert.True(lizard.GetAggroList().GetHate(player) > before,
			"an idle lizard should take the call's target as its own");
	}

	/// <summary>
	/// A lizard already fighting answers the same call the opposite way — it switches rather than
	/// re-targets, which is why retail splits the branch on its state at all.
	/// </summary>
	[Fact]
	public void AFightingLizardSwitchesInsteadOfRetargeting()
	{
		var (harness, boss, quarry) = Engaged();
		using BossAiHarness _h = harness;
		Npc lizard = harness.Spawn(FaithfulSubordinate, 1184f, 1239f, 143f);
		Player itsOwn = harness.SpawnPlayer(1186f, 1241f, 143f);
		BossAiHarness.MakeMutuallyKnown(lizard, itsOwn);
		BossAiHarness.MakeMutuallyKnown(lizard, quarry);
		harness.Engage(lizard, itsOwn);
		int before = lizard.GetAggroList().GetHate(quarry);

		var listener = (Aion.GameServer.Ai.INpcMessageListener)lizard.GetAi();
		listener.OnNpcMessage(boss, VanukaInfernusAI.RallyCall, quarry);

		// It does not adopt the boss's quarry: the fighting branch switches target and adds no hate.
		Assert.Equal(before, lizard.GetAggroList().GetHate(quarry));
	}

	/// <summary>
	/// The end-to-end shape: the summon branch below 30% does not only call a subordinate up, it rallies
	/// the lizards already standing about onto whoever he is fighting.
	/// </summary>
	/// <remarks>
	/// Three things this pin has to work around. The lizard is placed rather than summoned, because one
	/// summoned mid-fight arrives already fighting and so answers on the other branch — the idle branch
	/// exists for the ones loitering in his room. It is introduced to the boss by hand, because the
	/// harness has no known-list sweep; on the live server <c>World.Spawn</c> files a new NPC into its
	/// neighbours' known lists a moment after the AI's spawned event.
	/// <para>
	/// And what is asserted is the target the call sets, not the hate it adds. The quarry is kept sixty
	/// metres out so that nothing but the rally — which reaches fifty, from a boss the lizard is
	/// standing next to — can point the lizard at it; but at that distance the lizard does not know the
	/// player, and <c>AggroList.AddHate</c> drops a creature the NPC is unaware of, as it does in Java.
	/// Introducing them instead would let the lizard's own aggro explain the result. The target survives
	/// the awareness guard, so it is the observable that isolates the call.
	/// </para>
	/// </remarks>
	[Fact]
	public void TheSummonBranchRalliesTheLoiteringLizards()
	{
		BossAiHarness harness = BossAiHarness.For(DarkPoeta)
			.WithWorldSize(2048)
			.WithAi(typeof(VanukaInfernusAI), typeof(VanukaLizardAI), typeof(NTrapAI), typeof(AggressiveNpcAI))
			.Build();
		using BossAiHarness _h = harness;
		Npc boss = harness.Spawn(VanukaInfernus, 1182f, 1235f, 143f);
		Npc loiterer = harness.Spawn(FaithfulSubordinate, 1185f, 1238f, 143f);
		Player quarry = harness.SpawnPlayer(1242f, 1235f, 143f);
		BossAiHarness.MakeMutuallyKnown(boss, loiterer);
		harness.Engage(boss, quarry);
		BossAiHarness.SetHpPercent(boss, 25);

		void Run(int seconds)
		{
			for (int i = 0; i < seconds; i++)
			{
				BossAiHarness.Rehate(boss, quarry);
				harness.Clock.Advance(TimeSpan.FromSeconds(1));
			}
		}

		// The chain takes forty-five seconds to reach the summon branch: nine to timer 5, then eighteen
		// each to 6 and 7. Nothing rallies before that.
		Run(30);
		Assert.NotSame(quarry, loiterer.GetTarget());

		Run(30);

		Assert.Same(quarry, loiterer.GetTarget());
	}
}
