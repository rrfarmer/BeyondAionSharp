using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for Chaoslord Kalabar and his stone guard, translated from retail patterns <c>NKrall_WhA</c>
/// and <c>ND2_PnD</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
[Collection("GoldenDataManager")]
public sealed class ChaoslordKalabarAiTests
{
	private const int Eltnen = 210020000;

	private const int Kalabar = 212351;
	private const int Omutata = 212880;
	private const int WheelOfDeath = 280357;
	private const int StoneGuard = 280356;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Eltnen).WithWorldSize(2048)
			.WithAi(typeof(ChaoslordKalabarAI), typeof(StoneGuardAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static (BossAiHarness, Npc, Player) Fight(int bossId = Kalabar, int percent = 100)
	{
		BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(bossId, 300f, 300f, 200f);
		Player raider = harness.SpawnPlayer(303f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.SetExactPercent(boss, percent);
		harness.Engage(boss, raider);
		return (harness, boss, raider);
	}

	/// <summary>
	/// <b>At ninety he makes a wheel of death, and above ninety he makes nothing.</b> Retail's highest
	/// band starts at ninety, so a raid that has barely scratched him is not yet in the fight proper.
	/// </summary>
	[Theory]
	[InlineData(Kalabar)]
	[InlineData(Omutata)]
	public void AtNinetyHeMakesAWheelAndNotBefore(int bossId)
	{
		var (harness, boss, raider) = Fight(bossId);
		using BossAiHarness _h = harness;

		harness.Watch(20, null, WheelOfDeath);
		Assert.Empty(harness.LiveNpcs().Where(n => n.GetNpcId() == WheelOfDeath));

		BossAiHarness.SetExactPercent(boss, 85);
		harness.Watch(20, null, WheelOfDeath);

		Assert.Single(harness.LiveNpcs().Where(n => n.GetNpcId() == WheelOfDeath));
	}

	/// <summary>
	/// <b>At sixty the guard replaces the wheel — in one branch, so the two are never both up.</b> A
	/// raid that left the wheel alone finds it gone; one that killed it early changed nothing.
	/// </summary>
	[Fact]
	public void AtSixtyTheGuardReplacesTheWheel()
	{
		var (harness, boss, raider) = Fight(percent: 85);
		using BossAiHarness _h = harness;

		harness.Watch(20, null, WheelOfDeath);
		Assert.Single(harness.LiveNpcs().Where(n => n.GetNpcId() == WheelOfDeath));

		BossAiHarness.SetExactPercent(boss, 50);
		harness.Watch(20, null, StoneGuard);

		Assert.Empty(harness.LiveNpcs().Where(n => n.GetNpcId() == WheelOfDeath));
		Assert.Single(harness.LiveNpcs().Where(n => n.GetNpcId() == StoneGuard));
	}

	/// <summary>
	/// <b>At thirty-five he calls, and the guard destroys itself.</b> The guard exists to be spent — its
	/// only branch worth anything is the one that ends it.
	/// </summary>
	[Fact]
	public void AtThirtyFiveTheGuardDestroysItself()
	{
		var (harness, boss, raider) = Fight(percent: 50);
		using BossAiHarness _h = harness;

		harness.Watch(20, null, StoneGuard);
		Assert.Single(harness.LiveNpcs().Where(n => n.GetNpcId() == StoneGuard));

		BossAiHarness.SetExactPercent(boss, 20);
		harness.Watch(20, null, StoneGuard);

		Assert.Empty(harness.LiveNpcs().Where(n => n.GetNpcId() == StoneGuard));
	}

	/// <summary>
	/// <b>Each band opens exactly once.</b> Retail gives every band its own flag var, so a raid that
	/// sits in one does not farm its add.
	/// </summary>
	[Fact]
	public void EachBandOpensExactlyOnce()
	{
		var (harness, boss, raider) = Fight(percent: 85);
		using BossAiHarness _h = harness;

		harness.Watch(60, null, WheelOfDeath);

		Assert.Single(harness.LiveNpcs().Where(n => n.GetNpcId() == WheelOfDeath));
	}

	/// <summary>
	/// <b>Dying takes both groups with him.</b> Retail writes that as its own handler rather than
	/// leaning on <c>despawn_at_attack_state</c>, which means the adds cannot be pulled away and kept.
	/// </summary>
	[Fact]
	public void DyingTakesBothGroupsWithHim()
	{
		var (harness, boss, raider) = Fight(percent: 85);
		using BossAiHarness _h = harness;

		harness.Watch(20, null, WheelOfDeath);
		Assert.Single(harness.LiveNpcs().Where(n => n.GetNpcId() == WheelOfDeath));

		boss.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.Died);

		Assert.Empty(harness.LiveNpcs().Where(n => n.GetNpcId() == WheelOfDeath));
	}

	/// <summary>
	/// <b>And exactly thirty-five belongs to no band</b>, the same off-by-one Guardian Vingeveu carries:
	/// retail guards the low band on <c>lower_than 35</c> and the middle on <c>larger_than 36</c>. Kept
	/// rather than closed.
	/// </summary>
	[Fact]
	public void AndExactlyThirtyFiveBelongsToNoBand()
	{
		var (harness, boss, raider) = Fight(percent: 50);
		using BossAiHarness _h = harness;

		harness.Watch(20, null, StoneGuard);
		Assert.Single(harness.LiveNpcs().Where(n => n.GetNpcId() == StoneGuard));

		BossAiHarness.SetExactPercent(boss, 35);
		harness.Watch(30, null, StoneGuard);

		Assert.Single(harness.LiveNpcs().Where(n => n.GetNpcId() == StoneGuard));
	}

	/// <summary><b>The message number and the add ids are retail's, not ours.</b></summary>
	[Fact]
	public void TheNumbersAreRetails()
	{
		Assert.Equal(3008, ChaoslordKalabarAI.GoOff);
		Assert.Equal(50f, ChaoslordKalabarAI.CallReach);
		Assert.Equal(280357, ChaoslordKalabarAI.WheelOfDeath);
		Assert.Equal(280356, ChaoslordKalabarAI.StoneGuard);
	}
}
