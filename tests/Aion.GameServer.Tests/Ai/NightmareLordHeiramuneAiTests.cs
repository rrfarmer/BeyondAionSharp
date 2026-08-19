using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Nightmare Lord Heiramune, who was putting two adds on the floor every twenty seconds forever.
/// </summary>
/// <remarks>
/// Retail's <c>IDAsteria_IU_world_3Stage_Boss</c> has three <c>on_attacked</c> thresholds — 80, 55 and
/// 40 — of which only 55 spawns anything, and no repeating spawn timer anywhere. This class started a
/// fixed-rate task at 80% that never stopped, using <b>233457</b>, a second-wave event npc the
/// third-stage boss does not own.
/// <para>
/// The add it does own, <b>233162</b> (<c>IDAsteria_IU_3w_Shu_Fi_65_An</c>), was already right; the
/// pin for it lives in <c>RetailHpThresholdTests</c> and stays there.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class NightmareLordHeiramuneAiTests
{
	private const int NightmareCircus = 301200000;
	private const int Heiramune = 233467;
	private const int Add = 233162;

	/// <summary>The second-wave npc the invented train used.</summary>
	private const int SecondWaveMammoth = 233457;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(NightmareCircus).WithWorldSize(2048)
			.WithAi(typeof(NightmareLordHeiramuneAI), typeof(NightmareLordHeiramuneCloneAI),
				typeof(EnragedNightmareAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI)).Build();

	private static Npc Wounded(BossAiHarness harness, int toPercent)
	{
		Npc boss = harness.Spawn(Heiramune, 520f, 560f, 200f);
		Player player = harness.SpawnPlayer(522f, 560f, 200f);
		harness.Engage(boss, player);
		// The thresholds are driven from HandleAttack, so each step needs an attack event —
		// dropping HP alone never advances the phase.
		for (int hp = 99; hp >= toPercent; hp--)
		{
			BossAiHarness.SetHpPercent(boss, hp);
			boss.SetTarget(player);
			boss.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.Attack, player);
		}

		return boss;
	}

	/// <summary>
	/// <b>Crossing eighty per cent brings nothing onto the floor.</b>
	/// </summary>
	/// <remarks>
	/// It used to start a twenty-second train of two enraged nightmares that ran for the rest of the
	/// fight. Retail's eighty-per-cent rung is a shout and a conditional-spawn variable; the mammoth it
	/// spawned belongs to the second stage of the event, not to this boss.
	/// <para>
	/// Counted as they stand rather than through a watch window on purpose: an enraged nightmare has no
	/// lifetime, so anything the train produced would still be here — and if the train ever comes back,
	/// four minutes of it is twenty-four npcs, not zero.
	/// </para>
	/// </remarks>
	[Fact]
	public void CrossingEightyBringsNothingOntoTheFloor()
	{
		using BossAiHarness harness = NewHarness();
		Wounded(harness, 75);

		harness.Clock.Advance(TimeSpan.FromMinutes(4));

		Assert.Equal(0, harness.LiveNpcs().Count(n => n.GetNpcId() == SecondWaveMammoth));
	}

	/// <summary>
	/// <b>And neither does the whole fight down to the floor.</b>
	/// </summary>
	/// <remarks>
	/// The 55% add is the only thing he spawns, so walking him past every threshold should leave exactly
	/// one npc behind. This is what separates "the train is gone" from "the train moved to another
	/// threshold".
	/// </remarks>
	[Fact]
	public void AndTheWholeFightLeavesExactlyOneAdd()
	{
		using BossAiHarness harness = NewHarness();
		Wounded(harness, 10);

		harness.Clock.Advance(TimeSpan.FromMinutes(4));

		Assert.Equal(0, harness.LiveNpcs().Count(n => n.GetNpcId() == SecondWaveMammoth));
		Assert.Equal(1, harness.LiveNpcs().Count(n => n.GetNpcId() == Add));
	}

	/// <summary>
	/// <b>He takes his add with him when he resets.</b>
	/// </summary>
	/// <remarks>
	/// Retail's spawn command carries <c>despawn_at_attack_state=TRUE</c>. This class cleared the floor
	/// when he died but not when he went home, so an add outlived a wipe.
	/// </remarks>
	[Fact]
	public void HeTakesHisAddWithHimWhenHeResets()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = Wounded(harness, 50);
		Assert.Equal(1, harness.LiveNpcs().Count(n => n.GetNpcId() == Add));

		boss.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.BACK_HOME);

		Assert.Equal(0, harness.LiveNpcs().Count(n => n.GetNpcId() == Add));
	}
}
