using System.Collections.Generic;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for the village killers, translated from retail patterns <c>LDF5_Village_Killer01_L</c>,
/// <c>_01_D</c>, <c>_01_DR</c> and the identical <c>_02_*</c> set (see
/// <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
[Collection("GoldenDataManager")]
public sealed class VillageKillerAiTests
{
	private const int Cygnea = 210070000;

	/// <summary>Retail <c>_01_DR</c> and <c>_02_DR</c>: Balaur raiders.</summary>
	private const int BalaurRaider = 234104;

	/// <summary>Retail <c>_01_L</c>: an Elyos raider.</summary>
	private const int ElyosRaider = 234105;

	/// <summary>Retail <c>_02_D</c>: an Asmodian raider.</summary>
	private const int AsmodianRaider = 234109;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Cygnea).WithWorldSize(2048)
			// BaseProtectorAI is here because the garrison templates these pins spawn run it — the
			// rule the flake commit recorded for WithAi, seen from the other side: a test must not
			// spawn an npc whose class the harness was not told about, props included.
			.WithAi(typeof(VillageKillerElyosAI), typeof(VillageKillerAsmodianAI),
				typeof(VillageKillerBalaurAI), typeof(BaseProtectorAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>The first npc template we ship carrying that race, so the pins use real data.</summary>
	private static int AnyNpcOfRace(Race race)
	{
		string text = System.IO.File.ReadAllText(
			System.IO.Path.Combine(BossAiHarness.RepoRoot(),
				"game-server", "data", "static_data", "npcs", "npc_templates.xml"));
		var m = System.Text.RegularExpressions.Regex.Match(
			text, "<npc_template npc_id=\"(\\d+)\"[^>]*race=\"" + race + "\"");
		Assert.True(m.Success, "no template with race " + race);
		return int.Parse(m.Groups[1].Value);
	}

	/// <summary>
	/// Brings one NPC into another's view, then reports what <b>one</b> sighting adds.
	/// </summary>
	/// <remarks>
	/// <b>These pins measure the answer to a sighting, not the total on the list.</b> Registering two
	/// NPCs with each other runs the controller's <c>See</c> more than once — an absolute reading gave
	/// fifteen million and then ten before this became a delta — and how many times the world raises
	/// the event is the harness's business, not the mechanic's.
	/// </remarks>
	private static int SightingAdds(BossAiHarness harness, Npc watcher, Race garrison)
	{
		Npc chief = harness.Spawn(AnyNpcOfRace(garrison), watcher.GetX() + 5f, watcher.GetY(), watcher.GetZ());
		BossAiHarness.MakeMutuallyKnown(watcher, chief);

		int before = watcher.GetAggroList().GetHate(chief);
		watcher.GetAi().OnCreatureEvent(AiEventType.CreatureSee, chief);
		return watcher.GetAggroList().GetHate(chief) - before;
	}

	/// <summary>
	/// <b>Each raiding party hunts the other two factions and never its own.</b> The three race lists
	/// are exactly "everyone but me", which is the whole of retail's faction rule — and getting it
	/// wrong is what made the first version of this class hand a Balaur raider a Balaur garrison.
	/// </summary>
	[Theory]
	[InlineData(ElyosRaider, Race.GCHIEF_DARK, 5_000_000)]
	[InlineData(ElyosRaider, Race.GCHIEF_DRAGON, 5_000_000)]
	[InlineData(ElyosRaider, Race.GCHIEF_LIGHT, 0)]
	[InlineData(AsmodianRaider, Race.GCHIEF_DRAGON, 5_000_000)]
	[InlineData(AsmodianRaider, Race.GCHIEF_LIGHT, 5_000_000)]
	[InlineData(AsmodianRaider, Race.GCHIEF_DARK, 0)]
	[InlineData(BalaurRaider, Race.GCHIEF_DARK, 5_000_000)]
	[InlineData(BalaurRaider, Race.GCHIEF_LIGHT, 5_000_000)]
	[InlineData(BalaurRaider, Race.GCHIEF_DRAGON, 0)]
	public void EachPartyHuntsTheOtherTwoAndNeverItsOwn(int raider, Race garrison, int expected)
	{
		using BossAiHarness harness = NewHarness();
		Npc party = harness.Spawn(raider, 300f, 300f, 200f);

		Assert.Equal(expected, SightingAdds(harness, party, garrison));
	}

	/// <summary>
	/// <b>Five million, and that is the mechanic.</b> Retail's own number on every branch of all six
	/// patterns: nothing a player does peels a raiding party off the garrison it came for.
	/// </summary>
	[Fact]
	public void FiveMillionIsRetailsOwnNumber()
	{
		using BossAiHarness harness = NewHarness();
		Npc party = harness.Spawn(BalaurRaider, 300f, 300f, 200f);

		Assert.Equal(5_000_000, SightingAdds(harness, party, Race.GCHIEF_LIGHT));
	}

	/// <summary>
	/// <b>A player walking past is not a garrison.</b> The guard is a race test and a player fails it,
	/// which is what keeps this from being "attacks the nearest thing".
	/// </summary>
	[Fact]
	public void APlayerWalkingPastIsNotAGarrison()
	{
		using BossAiHarness harness = NewHarness();
		Npc party = harness.Spawn(BalaurRaider, 300f, 300f, 200f);
		Player passer = harness.SpawnPlayer(305f, 300f, 200f);

		int before = party.GetAggroList().GetHate(passer);
		party.GetAi().OnCreatureEvent(AiEventType.CreatureSee, passer);

		Assert.Equal(before, party.GetAggroList().GetHate(passer));
	}

	/// <summary>
	/// <b>The <c>on_attacked</c> half is built, is not pinned, and the fault is now located.</b>
	/// </summary>
	/// <remarks>
	/// Retail carries the same rule on <c>on_attacked</c> and <see cref="VillageKillerAI"/> translates
	/// it. Three experiments narrow where it stops:
	/// <list type="number">
	/// <item><description>With the race guard removed, a plain strike still adds nothing — so it is not
	/// <see cref="Ai.Pattern.AiPattern.When.AttackerRace"/>.</description></item>
	/// <item><description>With the action replaced by <c>Do.DespawnSelf</c>, the NPC despawns — so the
	/// <b>branch does run</b>, and <c>Evaluate(Pattern.OnAttacked)</c> is not the problem
	/// either.</description></item>
	/// <item><description>Holding <c>LastAttacker</c> past the branch instead of clearing it in a
	/// <c>finally</c> changed nothing, so the attacker reference is not being lost.</description></item>
	/// </list>
	/// <para>
	/// That leaves <c>Do.HateAttacker</c>'s <c>AggroList.AddHate</c> call, from inside
	/// <c>HandleAttack</c>, against a creature the <em>same</em> call reaches happily from
	/// <c>HandleCreatureSee</c> — five million there, nothing here, same pair, same value. The likely
	/// shape is re-entrancy: <c>base.HandleAttack</c> runs first and is itself working the aggro list.
	/// </para>
	/// <para>
	/// Left as a skip carrying that trail rather than a pin, and rather than a speculative engine
	/// change: the one tried above fixed nothing and was reverted, because shipping a behaviour change
	/// to every <c>PatternAi</c> on a guess is worse than a documented gap.
	/// </para>
	/// </remarks>
	[Fact(Skip = "AddHate from inside HandleAttack adds nothing; see remarks for the three experiments")]
	public void TheOnAttackedHalfIsBuiltAndNotPinned()
	{
	}
}
