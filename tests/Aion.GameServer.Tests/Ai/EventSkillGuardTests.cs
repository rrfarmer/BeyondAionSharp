using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Xunit;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// <b>Retail's <c>is_event_skill_id</c>: the branch fires for one skill and not for another.</b>
/// </summary>
/// <remarks>
/// 259 uses across 185 patterns, and every one of them was refused before this: the extractor had no
/// way to say "the skill that just hit me". <c>IDRose_Mandurit_S_An</c> (230627) is the clearest of
/// them -- a top-priority rung, one guard, one cast -- so it is the one pinned.
/// <para>
/// <b>The negative pin is the whole point.</b> A guard that reads a skill id nobody set is false for
/// every skill, and a guard that ignores the id is true for every skill; both leave the positive pin
/// passing. Only the pair distinguishes them.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public class EventSkillGuardTests
{
	/// <summary>Reshanta, the same world the other pattern pins use.</summary>
	private const int Reshanta = 400010000;

	/// <summary><c>IDRose_Mandurit_S_An</c> — answers 20385 with 20549 on itself, at priority 100.</summary>
	private const int Mandurit = 230627;

	private const int TheSkillItAnswers = 20385;
	private const int TheAnswer = 20549;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Reshanta).WithWorldSize(4096)
			.WithAi(typeof(BattleCycleAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI)).Build();

	[Fact]
	public void TheSkillItNamesSetsItCasting()
	{
		using BossAiHarness harness = NewHarness();
		Npc mandurit = harness.Spawn(Mandurit, 300f, 300f, 200f);
		Player caster = harness.SpawnPlayer(305f, 300f, 200f, race: Race.ELYOS);
		harness.Engage(mandurit, caster);

		BossAiHarness.SpellHit(mandurit, caster, TheSkillItAnswers);

		Assert.Contains(BossAiHarness.DrainQueuedSkills(mandurit), cast => cast.SkillId == TheAnswer);
	}

	/// <summary>
	/// <b>Another skill, landed the same way, leaves it alone.</b> Without this the guard could be
	/// ignoring the id entirely and the pin above would still pass.
	/// </summary>
	[Fact]
	public void AnotherSkillDoesNot()
	{
		using BossAiHarness harness = NewHarness();
		Npc mandurit = harness.Spawn(Mandurit, 300f, 300f, 200f);
		Player caster = harness.SpawnPlayer(305f, 300f, 200f, race: Race.ELYOS);
		harness.Engage(mandurit, caster);

		BossAiHarness.SpellHit(mandurit, caster, TheAnswer);

		Assert.DoesNotContain(BossAiHarness.DrainQueuedSkills(mandurit), cast => cast.SkillId == TheAnswer);
	}

	/// <summary>
	/// <b>The id does not outlive the handler.</b> A plain blow after the skill must not re-fire the
	/// branch: retail's guard asks what hit it <i>this time</i>, and a stale id would have the npc
	/// answering a skill nobody cast.
	/// </summary>
	[Fact]
	public void ThePreviousSkillDoesNotAnswerTheNextBlow()
	{
		using BossAiHarness harness = NewHarness();
		Npc mandurit = harness.Spawn(Mandurit, 300f, 300f, 200f);
		Player caster = harness.SpawnPlayer(305f, 300f, 200f, race: Race.ELYOS);
		harness.Engage(mandurit, caster);

		BossAiHarness.SpellHit(mandurit, caster, TheSkillItAnswers);
		BossAiHarness.DrainQueuedSkills(mandurit);

		BossAiHarness.SpellHit(mandurit, caster, TheAnswer);

		Assert.DoesNotContain(BossAiHarness.DrainQueuedSkills(mandurit), cast => cast.SkillId == TheAnswer);
	}
}
