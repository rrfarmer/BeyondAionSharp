using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The imperial obelisk, whose second buff came five per cent of health too early.
/// </summary>
/// <remarks>
/// Retail hangs three rungs on this tower: a buff on waking, then
/// <c>is_hp_in_boundary(larger_than=30, less_than=69)</c>, then
/// <c>is_hp_in_boundary(larger_than=0, less_than=29)</c>. aionemu wrote them as <c>HpPhases(70, 35)</c>,
/// and this port copied that faithfully. Three things follow from reading the pattern instead:
/// the second rung is thirty, not thirty-five; the boundaries are exclusive at the <i>bottom</i> as well,
/// so a rung the tower outran in one hit is never played; and both rungs hang on <c>on_spelled</c> as
/// well as <c>on_attacked</c>.
/// <para>
/// Found by <c>audit_hp_phases.py</c>. Both patterns bound to this AI -- <c>IDDF2Flying_event01_D_Tower02</c>
/// and <c>IDDF2Flying_event01_B_WavePortal1_55_Ae</c> -- carry identical rungs, so one correction serves
/// all three npcs.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class ShugoTombImperialObeliskAiTests
{
	private const int ShugoImperialTomb = 300400000;

	private const int Obelisk = 831250;

	private const int WakingBuff = 21097;
	private const int FirstRungBuff = 21098;
	private const int SecondRungBuff = 21099;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(ShugoImperialTomb).WithWorldSize(2048)
			.WithAi(typeof(ShugoTombImperialObeliskAI), typeof(GeneralNpcAI))
			.Build();

	private static Npc Engaged(BossAiHarness harness)
	{
		Npc obelisk = harness.Spawn(Obelisk, 500f, 500f, 100f);
		Player player = harness.SpawnPlayer(504f, 500f, 100f);
		harness.Engage(obelisk, player);
		return obelisk;
	}

	private static void Struck(Npc obelisk) =>
		obelisk.GetAi().OnCreatureEvent(AiEventType.Attack, obelisk);

	/// <summary>
	/// <b>It buffs itself the moment it wakes.</b> The one rung aionemu already had right.
	/// </summary>
	[Fact]
	public void ItBuffsItselfOnWaking()
	{
		using BossAiHarness harness = NewHarness();
		Npc obelisk = Engaged(harness);

		Assert.True(obelisk.GetEffectController().HasAbnormalEffect(WakingBuff),
			"the obelisk did not take its waking buff");
	}

	/// <summary>
	/// <b>The first rung waits for sixty-eight per cent.</b> Retail's <c>less_than=69</c> is exclusive, so
	/// sixty-nine is still above it.
	/// </summary>
	[Fact]
	public void TheFirstRungWaitsForSixtyEightPerCent()
	{
		using BossAiHarness harness = NewHarness();
		Npc obelisk = Engaged(harness);

		BossAiHarness.SetExactPercent(obelisk, 69);
		Struck(obelisk);
		Assert.False(obelisk.GetEffectController().HasAbnormalEffect(FirstRungBuff),
			"the obelisk took its first buff at sixty-nine per cent, above retail's boundary");

		BossAiHarness.SetExactPercent(obelisk, 68);
		Struck(obelisk);
		Assert.True(obelisk.GetEffectController().HasAbnormalEffect(FirstRungBuff),
			"the obelisk did not take its first buff at sixty-eight per cent");
	}

	/// <summary>
	/// <b>The second rung is thirty per cent, not thirty-five.</b> This is the defect: aionemu's
	/// <c>HpPhases(70, 35)</c> hands the tower its last buff five per cent of its health early.
	/// </summary>
	[Fact]
	public void TheSecondRungIsThirtyPerCentNotThirtyFive()
	{
		using BossAiHarness harness = NewHarness();
		Npc obelisk = Engaged(harness);

		BossAiHarness.SetExactPercent(obelisk, 68);
		Struck(obelisk);

		BossAiHarness.SetExactPercent(obelisk, 35);
		Struck(obelisk);
		Assert.False(obelisk.GetEffectController().HasAbnormalEffect(SecondRungBuff),
			"the obelisk took its last buff at thirty-five per cent, which is aionemu's number, not retail's");

		BossAiHarness.SetExactPercent(obelisk, 28);
		Struck(obelisk);
		Assert.True(obelisk.GetEffectController().HasAbnormalEffect(SecondRungBuff),
			"the obelisk did not take its last buff at twenty-eight per cent");
	}

	/// <summary>
	/// <b>A fall through both bands leaves only the lower buff.</b>
	/// </summary>
	/// <remarks>
	/// Retail's boundaries are exclusive at the bottom as well, so a hit carrying the tower from full health
	/// past both thresholds satisfies only the lower rung and the upper buff is never played. This class
	/// implements that, but <b>the test cannot see it</b>: 21098 and 21099 share <c>tslot="BUFF"</c>, so the
	/// lower buff evicts the upper one and the end state is identical either way. What is pinned here is that
	/// end state -- which is worth pinning, because reaching it needs both rungs evaluated on a single blow.
	/// The exclusive lower bound itself has no pin, and deliberately: it is unobservable, not untested.
	/// </remarks>
	[Fact]
	public void AFallThroughBothBandsLeavesOnlyTheLowerBuff()
	{
		using BossAiHarness harness = NewHarness();
		Npc obelisk = Engaged(harness);

		BossAiHarness.SetExactPercent(obelisk, 25);
		Struck(obelisk);

		Assert.True(obelisk.GetEffectController().HasAbnormalEffect(SecondRungBuff),
			"one blow through both bands left the obelisk without the buff for the band it landed in");
		Assert.False(obelisk.GetEffectController().HasAbnormalEffect(FirstRungBuff),
			"the obelisk was left holding the buff for a band it had already fallen through");
	}

	/// <summary>
	/// <b>Both rungs answer being spelled, not only being struck.</b> Damage carrying an effect reaches the
	/// AI through hate anyway; a spell that deals none raises this event and nothing else, and retail hangs
	/// the same two patterns on <c>on_spelled</c>.
	/// </summary>
	[Fact]
	public void ASpellWithNoDamageStillAdvancesTheRungs()
	{
		using BossAiHarness harness = NewHarness();
		Npc obelisk = Engaged(harness);

		BossAiHarness.SetExactPercent(obelisk, 68);
		obelisk.GetAi().OnCreatureEvent(AiEventType.Spelled, obelisk);

		Assert.True(obelisk.GetEffectController().HasAbnormalEffect(FirstRungBuff),
			"being spelled did not advance the obelisk's rungs, so a caster-only fight never buffs it");
	}
}
