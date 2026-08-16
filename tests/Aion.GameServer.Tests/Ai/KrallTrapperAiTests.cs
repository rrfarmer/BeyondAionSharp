using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="KrallTrapperAI"/>, <see cref="KrallScoutTrapperAI"/> and
/// <see cref="KrallHunterTrapperAI"/>, translated from retail patterns <c>NKrall_ReA</c>,
/// <c>NKrall_ReB</c>, <c>NKrall_ReC</c> and <c>Nkrall_RhA</c> (see
/// <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Twenty-five spawned world npcs that were all on plain <c>aggressive</c>. The shape worth pinning is
/// that the escape rung is melee-only — a group killing them at range never sees the powerful trap —
/// and that firing it ends the clock that watches for it.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class KrallTrapperAiTests
{
	/// <summary>Beluslan, where most of them stand.</summary>
	private const int Beluslan = 220030000;

	private const int Loudmouth = 211018;
	private const int Scout = 212010;
	private const int Kurka = 211039;

	private const int Trap29 = 280449;
	private const int PowerfulTrap29 = 280450;
	private const int Trap38 = 280451;
	private const int PowerfulTrap38 = 280452;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Beluslan).WithWorldSize(2048)
			.WithAi(typeof(KrallTrapperAI), typeof(KrallScoutTrapperAI), typeof(KrallHunterTrapperAI),
				typeof(NTrapAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>
	/// Engages the krall with a player standing on top of it, or well out of melee — which is the
	/// difference the escape rung is guarded on.
	/// </summary>
	private static (BossAiHarness, Npc, Player) Engaged(int npcId, float quarryOffset = 2f)
	{
		BossAiHarness harness = NewHarness();
		Npc krall = harness.Spawn(npcId, 300f, 300f, 200f);
		Player quarry = harness.SpawnPlayer(300f + quarryOffset, 300f, 200f);
		harness.Engage(krall, quarry);
		return (harness, krall, quarry);
	}

	private static void Advance(BossAiHarness harness, Npc krall, Player quarry, int seconds)
	{
		for (int i = 0; i < seconds; i++)
		{
			BossAiHarness.Rehate(krall, quarry);
			BossAiHarness.KeepAlive(quarry);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}
	}

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>Untouched it lays nothing: the first trap goes down with the fight.</summary>
	/// <remarks>
	/// Watched rather than counted at the end, for the seventh time in this suite: a trap fires and is
	/// gone in about five seconds, so an empty field at two minutes says nothing at all.
	/// </remarks>
	[Fact]
	public void AnUnpulledKrallLaysNothing()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(Loudmouth, 300f, 300f, 200f);

		BossAiHarness.Watched seen = harness.Watch(120, null, Trap38, PowerfulTrap38);

		Assert.Equal(0, seen.Total);
	}

	/// <summary>
	/// And it is guarded on health as well as on distance: something standing on it at full health, or
	/// at sixty percent, still does not get the powerful trap.
	/// </summary>
	/// <remarks>
	/// Sixty rather than only a hundred, because a threshold set too high is the likelier slip and a
	/// full-health reading cannot see it — the guard is <c>below</c>, so any wrong number under a
	/// hundred still reads false there.
	/// </remarks>
	[Theory]
	[InlineData(100)]
	[InlineData(60)]
	public void AboveThirtyFiveThePowerfulTrapDoesNotCome(int percent)
	{
		var (harness, krall, quarry) = Engaged(Loudmouth);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(krall, percent);
		BossAiHarness.Watched seen = harness.Watch(60, () =>
		{
			BossAiHarness.Rehate(krall, quarry);
			BossAiHarness.KeepAlive(quarry);
		}, PowerfulTrap38);

		Assert.Equal(0, seen.Total);
	}

	/// <summary>
	/// One goes down when the fight starts and another every twenty seconds — counted by watching
	/// rather than by looking at the ground, because a trap does not stay on it.
	/// </summary>
	[Fact]
	public void ATrapWithThePullAndOneEveryTwentySeconds()
	{
		var (harness, krall, quarry) = Engaged(Loudmouth, quarryOffset: 20f);
		using BossAiHarness _h = harness;

		BossAiHarness.Watched seen = harness.Watch(45, () =>
		{
			BossAiHarness.Rehate(krall, quarry);
			BossAiHarness.KeepAlive(quarry);
		}, Trap38);

		Assert.Equal(3, seen.Total);
		Assert.Equal(1, seen.Peak);
	}

	/// <summary>
	/// <b>A laid trap fires and goes; it does not lie in wait.</b> Retail's <c>live_time</c> on these —
	/// sixty seconds, fifty minutes, or none at all — is a ceiling for a trap nobody trips, and
	/// <see cref="NTrapAI"/> removes it the moment its skill lands. Measured at about five seconds here,
	/// pinned as "not twenty", because the exact figure is the cast's and not the pattern's.
	/// </summary>
	[Fact]
	public void ALaidTrapFiresAndGoesRatherThanStanding()
	{
		var (harness, krall, quarry) = Engaged(Loudmouth, quarryOffset: 20f);
		using BossAiHarness _h = harness;

		Advance(harness, krall, quarry, 1);
		Npc laid = Assert.Single(harness.LiveNpcs(), n => n.GetNpcId() == Trap38);

		Advance(harness, krall, quarry, 19);
		Assert.False(laid.IsSpawned(), "it was still standing twenty seconds later");
	}

	/// <summary>
	/// <b>The powerful trap is melee-only.</b> Retail guards the rung on the target being inside six
	/// metres, so a group killing the krall at range never sees it.
	/// </summary>
	[Fact]
	public void ThePowerfulTrapNeedsSomethingInMeleeRange()
	{
		var (harness, krall, quarry) = Engaged(Loudmouth, quarryOffset: 20f);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(krall, 20);
		BossAiHarness.Watched seen = harness.Watch(40, () =>
		{
			BossAiHarness.Rehate(krall, quarry);
			BossAiHarness.KeepAlive(quarry);
		}, PowerfulTrap38);

		Assert.Equal(0, seen.Total);
	}

	/// <summary>And with something standing on it, below thirty-five, it lays one — once.</summary>
	[Fact]
	public void InMeleeAndLowItLaysOnePowerfulTrap()
	{
		var (harness, krall, quarry) = Engaged(Loudmouth);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(krall, 20);
		BossAiHarness.Watched seen = harness.Watch(90, () =>
		{
			BossAiHarness.Rehate(krall, quarry);
			BossAiHarness.KeepAlive(quarry);
		}, PowerfulTrap38);

		Assert.Equal(1, seen.Total);
	}

	/// <summary>
	/// The scouts open with the ordinary trap and then lay <b>powerful</b> ones on the loop — the
	/// reverse of the heavy trappers, whose loop lays the ordinary kind.
	/// </summary>
	[Fact]
	public void AScoutOpensOrdinaryAndLoopsPowerful()
	{
		var (harness, krall, quarry) = Engaged(Scout, quarryOffset: 20f);
		using BossAiHarness _h = harness;

		void Tick()
		{
			BossAiHarness.Rehate(krall, quarry);
			BossAiHarness.KeepAlive(quarry);
		}

		BossAiHarness.Watched ordinary = harness.Watch(45, Tick, Trap29);
		Assert.Equal(1, ordinary.Total);

		var (h2, scout, q2) = Engaged(Scout, quarryOffset: 20f);
		using BossAiHarness _h2 = h2;
		BossAiHarness.Watched powerful = h2.Watch(45, () =>
		{
			BossAiHarness.Rehate(scout, q2);
			BossAiHarness.KeepAlive(q2);
		}, PowerfulTrap29);
		Assert.Equal(2, powerful.Total);
	}

	/// <summary>
	/// Chieftain Kurka lays the level-38 pair rather than the scouts' level-29 one — the trap tier is
	/// what separates his pattern from theirs.
	/// </summary>
	[Fact]
	public void KurkaLaysTheHeavyPair()
	{
		var (harness, krall, quarry) = Engaged(Kurka, quarryOffset: 20f);
		using BossAiHarness _h = harness;

		void Tick()
		{
			BossAiHarness.Rehate(krall, quarry);
			BossAiHarness.KeepAlive(quarry);
		}

		BossAiHarness.Watched heavy = harness.Watch(25, Tick, Trap38, PowerfulTrap38);
		Assert.Equal(2, heavy.Total);

		var (h2, chief, q2) = Engaged(Kurka, quarryOffset: 20f);
		using BossAiHarness _h2 = h2;
		BossAiHarness.Watched light = h2.Watch(25, () =>
		{
			BossAiHarness.Rehate(chief, q2);
			BossAiHarness.KeepAlive(q2);
		}, Trap29, PowerfulTrap29);
		Assert.Equal(0, light.Total);
	}
}
