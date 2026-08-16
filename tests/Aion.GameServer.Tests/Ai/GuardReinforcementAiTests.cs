using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="GuardReinforcementAI"/>, translated from the retail <c>[DL]Guard_*</c> family
/// (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// One mechanic across 460 guards, so what is pinned is the mechanic and one guard of each shape:
/// the three-band escalation (Nina, <c>DGuard_PhA</c>) and the single-band call that most of the
/// family uses. The per-guard facts are generated rather than written, so a pin per guard would only
/// be testing the generator.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class GuardReinforcementAiTests
{
	/// <summary>Reshanta, where the abyss guards stand.</summary>
	private const int Reshanta = 400010000;

	/// <summary>Nina, <c>DGuard_PhA</c> — the full three-band escalation.</summary>
	private const int Nina = 204303;
	private const int HolyServantAttacker = 294767;
	private const int HolyServantHealer = 294770;

	private static (BossAiHarness, Npc, Player) Engaged(int npcId, int hpPercent)
	{
		BossAiHarness harness = BossAiHarness.For(Reshanta).WithWorldSize(4096)
			.WithAi(typeof(GuardReinforcementAI), typeof(ServantNpcAI), typeof(AggressiveNpcAI)).Build();
		Npc guard = harness.Spawn(npcId, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(302f, 302f, 200f);
		BossAiHarness.SetHpPercent(guard, hpPercent);
		harness.Engage(guard, player);
		return (harness, guard, player);
	}

	private static void Advance(BossAiHarness harness, Npc guard, Player player, int seconds)
	{
		for (int i = 0; i < seconds; i++)
		{
			BossAiHarness.Rehate(guard, player);
			BossAiHarness.KeepAlive(player);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}
	}

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>
	/// Runs until the first call lands and reports what it brought.
	/// </summary>
	/// <remarks>
	/// Deliberately the <i>first</i> call rather than the most seen. The reinforcements live ten
	/// minutes and the guard keeps calling on every twenty-second heartbeat the coin allows, so a
	/// sustained fight piles them up — the first version of these pins measured the peak over ten
	/// heartbeats and read fifteen where the band says three. Stacking is retail's own behaviour and
	/// the fight is what ends it; what the band decides is the size of one call.
	/// <para>
	/// The call is a coin flip, so the window has to cover several heartbeats: ten of them puts a
	/// run of misses past one in a thousand.
	/// </para>
	/// </remarks>
	private static (int Attackers, int Healers) FirstCall(BossAiHarness harness, Npc guard, Player player)
	{
		for (int i = 0; i < 10 * 21; i++)
		{
			Advance(harness, guard, player, 1);
			int attackers = Count(harness, HolyServantAttacker);
			if (attackers > 0)
				return (attackers, Count(harness, HolyServantHealer));
		}

		return (0, 0);
	}

	/// <summary>Nothing is called before the fight starts — the whole chain hangs off entering combat.</summary>
	[Fact]
	public void AGuardNobodyHasTouchedCallsNobody()
	{
		BossAiHarness harness = BossAiHarness.For(Reshanta).WithWorldSize(4096)
			.WithAi(typeof(GuardReinforcementAI), typeof(ServantNpcAI), typeof(AggressiveNpcAI)).Build();
		using BossAiHarness _h = harness;
		Npc guard = harness.Spawn(Nina, 300f, 300f, 200f);
		BossAiHarness.SetHpPercent(guard, 20);

		harness.Clock.Advance(TimeSpan.FromSeconds(300));

		Assert.Equal(0, Count(harness, HolyServantAttacker));
	}

	/// <summary>At full health she calls two attackers and no healer.</summary>
	[Fact]
	public void HealthySheCallsTwoAttackersAndNoHealer()
	{
		var (harness, guard, player) = Engaged(Nina, 90);
		using BossAiHarness _h = harness;

		Assert.Equal((2, 0), FirstCall(harness, guard, player));
	}

	/// <summary>Worn to the middle band the healer joins them.</summary>
	[Fact]
	public void InTheMiddleBandAHealerComesToo()
	{
		var (harness, guard, player) = Engaged(Nina, 50);
		using BossAiHarness _h = harness;

		Assert.Equal((2, 1), FirstCall(harness, guard, player));
	}

	/// <summary>And in trouble it is three and two.</summary>
	[Fact]
	public void CorneredSheCallsThreeAndTwo()
	{
		var (harness, guard, player) = Engaged(Nina, 20);
		using BossAiHarness _h = harness;

		Assert.Equal((3, 2), FirstCall(harness, guard, player));
	}

	/// <summary>
	/// At exactly 35% she calls nobody. Retail writes the bands as below-35 and 36-70, so the value
	/// between them matches nothing — a dead spot this port keeps rather than tidies.
	/// </summary>
	[Fact]
	public void AtThirtyFiveExactlyTheBandsLeaveAGap()
	{
		var (harness, guard, player) = Engaged(Nina, 35);
		using BossAiHarness _h = harness;
		BossAiHarness.SetExactPercent(guard, 35);

		Advance(harness, guard, player, 210);

		Assert.Equal(0, Count(harness, HolyServantAttacker));
		Assert.Equal(0, Count(harness, HolyServantHealer));
	}

	/// <summary>Leaving the fight sends the wave away, or a reset would strand it on the field.</summary>
	[Fact]
	public void LeavingTheFightSendsThemAway()
	{
		var (harness, guard, player) = Engaged(Nina, 20);
		using BossAiHarness _h = harness;
		Assert.Equal((3, 2), FirstCall(harness, guard, player));

		guard.GetAi().OnGeneralEvent(AiEventType.BackHome);

		Assert.Equal(0, Count(harness, HolyServantAttacker));
		Assert.Equal(0, Count(harness, HolyServantHealer));
	}

	/// <summary>A medic leader, <c>DGuard_PhA_L50</c> — an abyss guard on its own aggro class.</summary>
	private const int MedicLeader = 207596;
	private const int MedicAttacker = 295151;
	private const int MedicHealer = 295152;

	private static BossAiHarness AbyssHarness() =>
		BossAiHarness.For(Reshanta).WithWorldSize(4096)
			.WithAi(typeof(AbyssGuardReinforcementAI), typeof(AbyssGuardSimpleAI), typeof(ServantNpcAI),
				typeof(AggressiveNpcAI)).Build();

	private static (BossAiHarness, Npc, Player) AbyssEngaged(int npcId, int hpPercent)
	{
		BossAiHarness harness = AbyssHarness();
		Npc guard = harness.Spawn(npcId, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(302f, 302f, 200f);
		BossAiHarness.SetHpPercent(guard, hpPercent);
		harness.Engage(guard, player);
		return (harness, guard, player);
	}

	private static (int Attackers, int Healers) FirstAbyssCall(BossAiHarness harness, Npc guard, Player player)
	{
		for (int i = 0; i < 10 * 21; i++)
		{
			Advance(harness, guard, player, 1);
			int attackers = Count(harness, MedicAttacker);
			if (attackers > 0)
				return (attackers, Count(harness, MedicHealer));
		}

		return (0, 0);
	}

	/// <summary>
	/// The forty-nine guards that already carried <c>simple_abyssguard</c> get the reinforcements too,
	/// without losing the aggro rules that class exists for.
	/// </summary>
	[Fact]
	public void AnAbyssGuardCallsReinforcementsToo()
	{
		var (harness, guard, player) = AbyssEngaged(MedicLeader, 20);
		using BossAiHarness _h = harness;

		Assert.Equal((3, 2), FirstAbyssCall(harness, guard, player));
	}

	/// <summary>And it reads its own band, not Nina's.</summary>
	[Fact]
	public void AnAbyssGuardReadsItsOwnBand()
	{
		var (harness, guard, player) = AbyssEngaged(MedicLeader, 90);
		using BossAiHarness _h = harness;

		Assert.Equal((2, 0), FirstAbyssCall(harness, guard, player));
	}

	/// <summary>
	/// It still refuses to answer another guard's call for help, which is the aggro rule its own class
	/// carries and the thing that would have been lost by overwriting the AI name.
	/// </summary>
	[Fact]
	public void AnAbyssGuardKeepsItsOwnAggroRules()
	{
		var (harness, guard, _) = AbyssEngaged(MedicLeader, 90);
		using BossAiHarness _h = harness;

		Assert.IsAssignableFrom<AbyssGuardSimpleAI>(guard.GetAi());
	}

	/// <summary>
	/// Most guards keep their wave at their own feet — the <c>spawn</c> op rather than
	/// <c>spawn_on_target</c>.
	/// </summary>
	/// <remarks>
	/// The mirror of <see cref="SomeGuardsDropTheirWaveOnTheirQuarry"/>, and it exists because without
	/// it a mutation that sent <i>every</i> wave onto the target passed: the other self-placement pins
	/// stand the guard two metres from its quarry, where the two placements are indistinguishable.
	/// </remarks>
	[Fact]
	public void MostGuardsKeepTheirWaveAtTheirOwnFeet()
	{
		BossAiHarness harness = BossAiHarness.For(Reshanta).WithWorldSize(4096)
			.WithAi(typeof(GuardReinforcementAI), typeof(ServantNpcAI), typeof(AggressiveNpcAI)).Build();
		using BossAiHarness _h = harness;
		Npc guard = harness.Spawn(Nina, 300f, 300f, 200f);
		Player quarry = harness.SpawnPlayer(360f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(guard, quarry);
		BossAiHarness.SetHpPercent(guard, 20);
		harness.Engage(guard, quarry);

		Npc? arrived = null;
		for (int i = 0; i < 10 * 21 && arrived is null; i++)
		{
			Advance(harness, guard, quarry, 1);
			arrived = harness.LiveNpcs().FirstOrDefault(n => n.GetNpcId() == HolyServantAttacker);
		}

		Assert.NotNull(arrived);
		Assert.True(Math.Abs(arrived!.GetX() - guard.GetX()) < Math.Abs(arrived.GetX() - quarry.GetX()),
			"the wave should land at the guard's own feet");
	}

	/// <summary>A garrison senior patrol, <c>LGuard_PhB</c> — the <c>spawn_on_target</c> shape.</summary>
	private const int GarrisonPatrol = 207773;
	private const int PatrolAttacker = 294734;
	private const int PatrolHealer = 294737;

	/// <summary>
	/// Some guards drop their wave <b>on whoever they are fighting</b> rather than at their own feet —
	/// retail's <c>spawn_on_target</c>. That is a materially different fight for a raid, and it was
	/// missed entirely on the first pass: the extractor only looked for the <c>spawn</c> op, so four
	/// pattern variants read as guards that call nobody.
	/// </summary>
	/// <remarks>
	/// The guard and its quarry are kept well apart so the landing spot is unambiguous. Retail scatters
	/// the wave three metres around the target, so what is asserted is which of the two they arrived
	/// next to, not an exact coordinate.
	/// </remarks>
	[Fact]
	public void SomeGuardsDropTheirWaveOnTheirQuarry()
	{
		BossAiHarness harness = BossAiHarness.For(Reshanta).WithWorldSize(4096)
			.WithAi(typeof(GuardReinforcementAI), typeof(ServantNpcAI), typeof(AggressiveNpcAI)).Build();
		using BossAiHarness _h = harness;
		Npc guard = harness.Spawn(GarrisonPatrol, 300f, 300f, 200f);
		Player quarry = harness.SpawnPlayer(360f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(guard, quarry);
		BossAiHarness.SetHpPercent(guard, 20);
		harness.Engage(guard, quarry);

		Npc? arrived = null;
		for (int i = 0; i < 10 * 21 && arrived is null; i++)
		{
			Advance(harness, guard, quarry, 1);
			arrived = harness.LiveNpcs().FirstOrDefault(
				n => n.GetNpcId() == PatrolAttacker || n.GetNpcId() == PatrolHealer);
		}

		Assert.NotNull(arrived);
		float toQuarry = Math.Abs(arrived!.GetX() - quarry.GetX());
		float toGuard = Math.Abs(arrived.GetX() - guard.GetX());
		Assert.True(toQuarry < toGuard,
			$"the wave should land on the quarry: {toQuarry:F1}m from it, {toGuard:F1}m from the guard");
	}

	/// <summary>
	/// A wave lives as long as its own pattern says, not as long as the first guard read did.
	/// </summary>
	/// <remarks>
	/// The garrison patrol's reinforcements last a hundred seconds where Nina's last ten minutes, and
	/// the class hardcoded ten minutes for everyone. That was right for the guards it was written
	/// against — every branch the extractor could then see carried <c>live_time=600</c> — and wrong for
	/// the family: the ops it could not see, and the drakan guards it did not match at all, carry a
	/// hundred. A constant taken from a uniform subset is a constant that will be wrong as soon as the
	/// subset grows.
	/// <para>
	/// What separates the two is where the population <b>plateaus</b>, not any single wave. The patrol
	/// calls every twenty seconds and never stops, so waves accumulate until the oldest start
	/// expiring: at a hundred seconds that settles around five calls' worth, at ten minutes it keeps
	/// climbing. Ninety seconds tells them apart not at all — nothing has expired yet under either —
	/// which is what the first version of this pin asserted.
	/// </para>
	/// </remarks>
	[Fact]
	public void AWaveLivesForItsOwnPatternsLifetime()
	{
		BossAiHarness harness = BossAiHarness.For(Reshanta).WithWorldSize(4096)
			.WithAi(typeof(GuardReinforcementAI), typeof(ServantNpcAI), typeof(AggressiveNpcAI)).Build();
		using BossAiHarness _h = harness;
		Npc guard = harness.Spawn(GarrisonPatrol, 300f, 300f, 200f);
		Player quarry = harness.SpawnPlayer(360f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(guard, quarry);
		BossAiHarness.SetHpPercent(guard, 20);
		harness.Engage(guard, quarry);

		BossAiHarness.Watched run = harness.Watch(
			300, () => BossAiHarness.Rehate(guard, quarry), PatrolAttacker, PatrolHealer);

		Assert.True(run.Total > 10, $"five minutes of calls should be more than ten arrivals: {run.Total}");

		// Five minutes of twenty-second calls is around fifteen waves. With a hundred-second lifetime
		// only the last five or so are still standing; with ten minutes, all of them would be.
		int standing = Count(harness, PatrolAttacker) + Count(harness, PatrolHealer);
		Assert.True(standing < run.Total / 2,
			$"a hundred-second wave should have retired most of {run.Total} arrivals, {standing} standing");
	}
}
