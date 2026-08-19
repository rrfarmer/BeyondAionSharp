using Aion.GameServer.Dataholders;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The forty drakan guards whose reinforcement rows were generated and then never run.
/// </summary>
/// <remarks>
/// <c>GuardReinforcements.cs</c> is keyed by npc id, but it is only ever consulted by
/// <see cref="GuardReinforcementAI"/> and <see cref="AbyssGuardReinforcementAI"/>. A guard with rows in
/// the table and some other <c>ai</c> on its template carries data nothing reads.
/// <para>
/// 99 of the 1,265 guards in the table were in that state. Forty of them were <c>DrGuard_*</c> on
/// <c>ai="aggressive"</c> — <b>a partial gap, not a missing family</b>: 305 drakan rows were already on
/// <c>guard_reinforcement</c> and these forty were left behind. Among them is brigadier indratu, whose
/// own fire elemental had been eaten by the resolver hole as well, so he was doubly silent.
/// </para>
/// <para>
/// Switching them is a translation rather than a judgement because <c>PatternAi</c> derives from
/// <c>AggressiveNpcAI</c> and every one of its twelve overrides delegates to base after evaluating the
/// pattern — checked, not assumed. A guard whose id is absent from the table gets an empty pattern and
/// behaves exactly as <c>aggressive</c> did.
/// </para>
/// <para>
/// <b>The other 59 are deliberately left alone</b> and listed in <c>docs/retail-ai-fidelity.md</c>:
/// <c>fortress_protector</c> (38), <c>general</c>, <c>siege_shieldnpc</c>, <c>gate_squad</c> and kin are
/// not <c>PatternAi</c> subclasses, so re-pointing them would trade one mechanic for another.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class DrakanGuardWiringTests
{
	private const int IndratuFortress = 310090000;

	/// <summary>Brigadier indratu, <c>DrGuard_PhA_L48</c> — one of the forty.</summary>
	private const int Brigadier = 214159;
	private const int FireSpirit = 296348;
	private const int EarthSpirit = 296349;

	/// <summary>
	/// <b>No guard in the table sits on plain <c>aggressive</c>.</b>
	/// </summary>
	/// <remarks>
	/// This is the pin that generalises, and it is deliberately narrower than "every guard runs a reader
	/// AI" — that would be false, and falsely reassuring, because 59 guards legitimately run siege and
	/// fortress AIs that this table cannot drive. <c>aggressive</c> is the one value that means nobody
	/// made a decision: it is the template default, so a guard left on it has table rows by accident of
	/// generation rather than by choice.
	/// </remarks>
	[Fact]
	public void NoGuardWithReinforcementRowsIsLeftOnPlainAggressive()
	{
		using BossAiHarness harness = BossAiHarness.For(IndratuFortress).WithWorldSize(256)
			.WithAi(typeof(GuardReinforcementAI)).Build();

		List<int> stranded = GuardReinforcements.ByGuard.Keys
			.Where(id => DataManager.NPC_DATA.GetNpcTemplate(id)?.ai == "aggressive")
			.ToList();

		Assert.True(stranded.Count == 0,
			"guards with reinforcement rows still on ai=\"aggressive\", so the rows never run: "
				+ string.Join(", ", stranded));
	}

	/// <summary>
	/// <b>And the brigadier actually calls somebody.</b> The invariant above is satisfiable by deleting
	/// rows; this is the half that says the wiring produces a reinforcement in play.
	/// </summary>
	[Fact]
	public void TheBrigadierCallsHisReinforcements()
	{
		using BossAiHarness harness = BossAiHarness.For(IndratuFortress).WithWorldSize(2048)
			.WithAi(typeof(GuardReinforcementAI), typeof(ServantNpcAI), typeof(AggressiveNpcAI)).Build();
		Npc guard = harness.Spawn(Brigadier, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(302f, 302f, 200f);
		BossAiHarness.SetHpPercent(guard, 20);
		harness.Engage(guard, player);

		for (int i = 0; i < 2 * 21; i++)
		{
			BossAiHarness.Rehate(guard, player);
			BossAiHarness.KeepAlive(player);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
			if (harness.LiveNpcs().Any(n => n.GetNpcId() == EarthSpirit))
				break;
		}

		Assert.Equal(1, harness.LiveNpcs().Count(n => n.GetNpcId() == FireSpirit));
		Assert.Equal(1, harness.LiveNpcs().Count(n => n.GetNpcId() == EarthSpirit));
	}
}
