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
	/// <summary>
	/// <b>The <c>on_attacked</c> half works, and the two commits that called it broken were measuring
	/// a flag that had already been spent.</b>
	/// </summary>
	/// <remarks>
	/// Bringing the two NPCs into each other's view runs <em>both</em> handlers during setup — the
	/// sighting branch and, through the engine's own attack path, the <c>on_attacked</c> one. Every
	/// earlier reading took its baseline <em>after</em> that, so it measured a once-a-fight branch that
	/// had already fired and reported zero.
	/// <para>
	/// What finally showed it was giving the branch a second, visible action: with
	/// <c>Do.DespawnSelf</c> beside <c>Do.HateAttacker</c> the raider vanished <b>during setup</b>, and
	/// the pin's own direct <c>AddHate</c> afterwards read zero because it was adding hate to a
	/// despawned NPC. That is what "the branch runs but the action does nothing" had really been.
	/// </para>
	/// <para>
	/// <b>Rule: when a once-only branch reads zero, check whether the setup already spent it.</b> Three
	/// experiments in the previous entry ruled out the guard, the evaluation and the attacker
	/// reference, and all three were right — the fault was in where the baseline was taken.
	/// </para>
	/// </remarks>
	[Fact]
	public void TheOnAttackedHalfFiresDuringTheFirstEngagement()
	{
		using BossAiHarness harness = NewHarness();
		Npc party = harness.Spawn(BalaurRaider, 300f, 300f, 200f);
		Npc chief = harness.Spawn(AnyNpcOfRace(Race.GCHIEF_LIGHT), 305f, 300f, 200f);

		BossAiHarness.MakeMutuallyKnown(party, chief);
		int afterSetup = party.GetAggroList().GetHate(chief);

		// Both halves have fired by now, so the total is a multiple of retail's figure rather than one
		// of it -- what is pinned is that it is retail's figure and that nothing keeps adding.
		Assert.True(afterSetup >= 5_000_000, "the garrison was not committed to at all: " + afterSetup);
		Assert.Equal(0, afterSetup % 5_000_000);

		for (int i = 0; i < 4; i++)
			party.GetAi().OnCreatureEvent(AiEventType.Attack, chief);

		Assert.Equal(afterSetup, party.GetAggroList().GetHate(chief));
	}

	/// <summary>
	/// <b>The two halves share one flag, so a raiding party commits once however it was provoked.</b>
	/// Retail puts <c>FLAGVARI_EPSILON_5</c> on both the attacked and the spelled branch, and this pin
	/// is what says the shared flag was translated rather than one flag each.
	/// </summary>
	/// <remarks>
	/// The engagement during setup already spends it — see the sibling pin — so what a later cast adds
	/// is nothing, and that is the claim. A pin asserting a second five million would be asserting two
	/// flags.
	/// </remarks>
	[Fact]
	public void TheTwoHalvesShareOneFlag()
	{
		using BossAiHarness harness = NewHarness();
		Npc party = harness.Spawn(BalaurRaider, 300f, 300f, 200f);
		Npc chief = harness.Spawn(AnyNpcOfRace(Race.GCHIEF_LIGHT), 305f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(party, chief);

		int committed = party.GetAggroList().GetHate(chief);
		Assert.True(committed >= 5_000_000, "the raiding party never committed: " + committed);

		party.GetAi().OnCreatureEvent(AiEventType.Spelled, chief);

		Assert.Equal(committed, party.GetAggroList().GetHate(chief));
	}

	/// <summary>
	/// <b>And a caster of its own faction is ignored</b>, exactly as one hitting it is.
	/// </summary>
	[Fact]
	public void AndACasterOfItsOwnFactionIsIgnored()
	{
		using BossAiHarness harness = NewHarness();
		Npc party = harness.Spawn(BalaurRaider, 300f, 300f, 200f);
		Npc ally = harness.Spawn(AnyNpcOfRace(Race.GCHIEF_DRAGON), 305f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(party, ally);

		int before = party.GetAggroList().GetHate(ally);
		party.GetAi().OnCreatureEvent(AiEventType.Spelled, ally);

		Assert.Equal(before, party.GetAggroList().GetHate(ally));
	}
}
