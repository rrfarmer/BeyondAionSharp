using System.Linq;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for the village killers, translated from retail patterns <c>LDF5_Village_Killer01_DR</c>,
/// <c>_01_L</c>, <c>_02_D</c> and <c>_02_DR</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
[Collection("GoldenDataManager")]
public sealed class VillageKillerAiTests
{
	private const int Cygnea = 210070000;

	private const int StonereachForce = 234104;
	private const int FlamecrestForce = 234107;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Cygnea).WithWorldSize(2048)
			// BaseProtectorAI is here because the garrison npcs these pins spawn run it -- the same
			// rule the flake commit recorded for WithAi, seen from the other side: a test must not
			// spawn an npc whose class the harness was not told about.
			.WithAi(typeof(VillageKiller01AI), typeof(VillageKiller02AI), typeof(BaseProtectorAI),
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
	/// NPCs with each other runs the controller's <c>See</c> more than once — the pin read fifteen
	/// million and then ten before this became a delta — and how many times the world raises the event
	/// is the harness's business, not the mechanic's. Retail's claim is what <em>one</em> sighting is
	/// worth.
	/// </remarks>
	private static int SightingAdds(Npc watcher, Creature seen)
	{
		if (seen is Npc other)
			BossAiHarness.MakeMutuallyKnown(watcher, other);

		int before = watcher.GetAggroList().GetHate(seen);
		watcher.GetAi().OnCreatureEvent(AiEventType.CreatureSee, seen);
		return watcher.GetAggroList().GetHate(seen) - before;
	}

	private static void See(Npc watcher, Creature seen) => SightingAdds(watcher, seen);

	/// <summary>
	/// <b>A thrasher that sees a garrison chief goes for it, and nothing peels it off.</b> Five million
	/// hate points is retail's own number on every branch of all four patterns.
	/// </summary>
	[Fact]
	public void SeeingAGarrisonChiefCommitsItCompletely()
	{
		using BossAiHarness harness = NewHarness();
		Npc thrasher = harness.Spawn(StonereachForce, 300f, 300f, 200f);
		Npc chief = harness.Spawn(AnyNpcOfRace(Race.GCHIEF_LIGHT), 305f, 300f, 200f);

		int added = SightingAdds(thrasher, chief);

		Assert.Same(chief, thrasher.GetTarget());
		Assert.Equal(5_000_000, added);
	}

	/// <summary>
	/// <b>The squads hunt different factions.</b> A stonereach thrasher goes for an Asmodian garrison
	/// and a flamecrest one ignores it — retail's <c>01</c> patterns watch <c>gchief_dark</c> and its
	/// <c>02</c> patterns watch <c>gchief_dragon</c> instead.
	/// </summary>
	[Fact]
	public void TheSquadsHuntDifferentFactions()
	{
		using BossAiHarness harness = NewHarness();
		Npc stonereach = harness.Spawn(StonereachForce, 300f, 300f, 200f);
		Npc flamecrest = harness.Spawn(FlamecrestForce, 320f, 300f, 200f);
		Npc asmodian = harness.Spawn(AnyNpcOfRace(Race.GCHIEF_DARK), 305f, 300f, 200f);

		Assert.Equal(5_000_000, SightingAdds(stonereach, asmodian));
		Assert.Equal(0, SightingAdds(flamecrest, asmodian));
	}

	/// <summary>
	/// <b>The dragon garrison is refused by the aggro list, and that is recorded rather than forced.</b>
	/// </summary>
	/// <remarks>
	/// Retail's <c>02</c> patterns hunt <c>gchief_dragon</c>, and the race guard matches — but
	/// <c>AggroList.AddHate</c> will not put hate on a creature the owner is not an enemy of, and our
	/// tribe relations make a flamecrest thrasher and a Balaur garrison friends. So the call lands and
	/// the hate does not.
	/// <para>
	/// Pinned as zero, not as five million, because the alternative is to bypass the aggro list to make
	/// a test pass. Whoever resolves it is choosing between retail's pattern and our tribe table, and
	/// should say which one is wrong rather than route around either. The same gate is why this class
	/// ships without its <c>on_attacked</c> half — see <see cref="VillageKillerAI"/>.
	/// </para>
	/// </remarks>
	[Fact]
	public void TheDragonGarrisonIsRefusedByTheAggroList()
	{
		using BossAiHarness harness = NewHarness();
		Npc stonereach = harness.Spawn(StonereachForce, 300f, 300f, 200f);
		Npc flamecrest = harness.Spawn(FlamecrestForce, 320f, 300f, 200f);
		Npc dragon = harness.Spawn(AnyNpcOfRace(Race.GCHIEF_DRAGON), 322f, 300f, 200f);

		Assert.Equal(0, SightingAdds(flamecrest, dragon));
		Assert.Equal(0, SightingAdds(stonereach, dragon));
	}

	/// <summary>
	/// <b>A player walking past is not a garrison.</b> The guard is a race test and a player fails it,
	/// which is what keeps this from being "attacks the nearest thing".
	/// </summary>
	[Fact]
	public void APlayerWalkingPastIsNotAGarrison()
	{
		using BossAiHarness harness = NewHarness();
		Npc thrasher = harness.Spawn(StonereachForce, 300f, 300f, 200f);
		Player passer = harness.SpawnPlayer(305f, 300f, 200f);

		Assert.Equal(0, SightingAdds(thrasher, passer));
	}
}
