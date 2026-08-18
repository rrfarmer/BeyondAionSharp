using System;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for Vallakhan's illusions, translated from retail patterns <c>IDTP_Fanatic_Boss_EL</c> and
/// <c>IDTP_Fanatic_Elementalearth2</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
[Collection("GoldenDataManager")]
public sealed class IllusionOfMelancholyAiTests
{
	private const int UdasTemple = 300110000;

	private const int Vallakhan = 215782;
	private const int Illusion = 281524;
	private const int Spirit = 281384;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(UdasTemple).WithWorldSize(2048)
			.WithAi(typeof(VallakhanAI), typeof(IllusionOfMelancholyAI), typeof(SummonerAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static List<Npc> Live(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Where(n => n.GetNpcId() == npcId).ToList();

	/// <summary>
	/// <b>One blow and the illusion is gone.</b> Its whole pattern is three ways of leaving and one way
	/// of engaging; it is a distraction with a cost, not an add.
	/// </summary>
	[Fact]
	public void OneBlowAndTheIllusionIsGone()
	{
		using BossAiHarness harness = NewHarness();
		Npc illusion = harness.Spawn(Illusion, 300f, 300f, 200f);

		// The raider stands well outside the illusion's sight. Two metres away it aggroes on its own,
		// fights, leaves combat and despawns through the other branch -- which is correct behaviour and
		// makes the pin unable to tell "pops on a blow" from "pops on its own". The blow is delivered as
		// an event, which needs no proximity.
		Player raider = harness.SpawnPlayer(360f, 300f, 200f);

		harness.Clock.Advance(TimeSpan.FromSeconds(10));
		Assert.True(illusion.IsSpawned(), "the illusion left before anyone touched it");

		illusion.GetAi().OnCreatureEvent(AiEventType.Attack, raider);

		Assert.False(illusion.IsSpawned(), "the illusion survived being touched");
	}

	/// <summary>
	/// <b>A spell pops it too.</b> Retail carries the same body on <c>on_spelled</c>, so a caster who
	/// never lands a melee blow removes an illusion exactly as a melee player does — the half this
	/// class shipped without, for want of an engine event.
	/// </summary>
	[Fact]
	public void ASpellPopsItToo()
	{
		using BossAiHarness harness = NewHarness();
		Npc illusion = harness.Spawn(Illusion, 300f, 300f, 200f);
		Player caster = harness.SpawnPlayer(360f, 300f, 200f, race: Race.ASMODIANS);

		harness.Clock.Advance(TimeSpan.FromSeconds(10));
		Assert.True(illusion.IsSpawned(), "the illusion left before anyone cast at it");

		BossAiHarness.SetExactPercent(illusion, 98);
		illusion.GetAi().OnCreatureEvent(AiEventType.Spelled, caster);

		Assert.False(illusion.IsSpawned(), "the illusion survived a spell");
	}

	/// <summary>
	/// <b>And a spell that does no damage leaves it standing.</b> Retail guards the branch on
	/// <c>is_hp_lower_than 99</c>, so a buff or a miss is not a way to clear the room.
	/// </summary>
	[Fact]
	public void AndASpellThatDoesNoDamageLeavesItStanding()
	{
		using BossAiHarness harness = NewHarness();
		Npc illusion = harness.Spawn(Illusion, 300f, 300f, 200f);
		Player caster = harness.SpawnPlayer(360f, 300f, 200f, race: Race.ASMODIANS);

		BossAiHarness.SetExactPercent(illusion, 100);
		illusion.GetAi().OnCreatureEvent(AiEventType.Spelled, caster);

		Assert.True(illusion.IsSpawned(), "an undamaging spell popped the illusion");
	}

	/// <summary>
	/// <b>And it leaves when the fight ends</b>, so a group that walks away is not followed by two of
	/// them.
	/// </summary>
	[Fact]
	public void AndItLeavesWhenTheFightEnds()
	{
		using BossAiHarness harness = NewHarness();
		Npc illusion = harness.Spawn(Illusion, 300f, 300f, 200f);

		illusion.GetAi().OnGeneralEvent(AiEventType.BackHome);

		Assert.False(illusion.IsSpawned(), "the illusion stayed after the fight");
	}

	/// <summary>
	/// <b>The call names nobody.</b> Retail answers it with a bare <c>attack_most_hating</c>, so a
	/// freshly-placed illusion with an empty aggro list is told to go and has nowhere to go.
	/// </summary>
	/// <remarks>
	/// <b>This pin used to claim the opposite</b>, and reasoned its way there: "attack_most_hating on a
	/// freshly-placed illusion with an empty aggro list means the one it was just named". It does not.
	/// An empty aggro list has no most-hated, and the answer was written as a zero-point hate on the
	/// message parameter, which manufactured a target retail never gives it.
	/// <para>
	/// Told directly rather than driven through Vallakhan's health — his summon table fires the spirit
	/// at 99% and the illusions at 75%, and in this harness only the spirit arrives however the descent
	/// is staged.
	/// </para>
	/// </remarks>
	[Fact]
	public void AnIllusionToldToGoWithNothingToHateStaysPut()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Vallakhan, 300f, 300f, 200f);
		Npc illusion = harness.Spawn(Illusion, 302f, 300f, 200f);
		Player raider = harness.SpawnPlayer(303f, 300f, 200f, race: Race.ASMODIANS);
		BossAiHarness.MakeMutuallyKnown(boss, illusion);

		((Aion.GameServer.Ai.INpcMessageListener)illusion.GetAi())
			.OnNpcMessage(boss, VallakhanAI.SetThemOn, raider);

		Assert.Null(illusion.GetTarget());
	}

	// NOT PINNED: that it goes for its own most hated rather than the one named. The illusion despawns
	// itself on any blow or damaging spell -- that is its whole mechanic -- so it cannot be given a
	// fight to be in, and a bare AddHate on an idle illusion is refused. The claim is real and the
	// action is retail's; there is no arrangement in this harness that shows it. Recorded in
	// docs/retail-ai-fidelity.md.

	/// <summary>
	/// <b>And it ignores anything that is not its own call.</b>
	/// </summary>
	[Fact]
	public void AndItIgnoresAnythingThatIsNotItsOwnCall()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Vallakhan, 300f, 300f, 200f);
		Npc illusion = harness.Spawn(Illusion, 302f, 300f, 200f);
		Player raider = harness.SpawnPlayer(303f, 300f, 200f, race: Race.ASMODIANS);
		BossAiHarness.MakeMutuallyKnown(boss, illusion);

		((Aion.GameServer.Ai.INpcMessageListener)illusion.GetAi())
			.OnNpcMessage(boss, VallakhanAI.SetThemOn + 1, raider);

		Assert.Null(illusion.GetTarget());
	}

	/// <summary>
	/// <b>The message number is retail's, not ours.</b> Boss and illusion share one constant.
	/// </summary>
	[Fact]
	public void TheMessageNumberIsRetails()
	{
		Assert.Equal(6915, VallakhanAI.SetThemOn);
	}
}
