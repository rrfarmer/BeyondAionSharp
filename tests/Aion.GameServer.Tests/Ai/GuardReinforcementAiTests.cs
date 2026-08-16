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
}
