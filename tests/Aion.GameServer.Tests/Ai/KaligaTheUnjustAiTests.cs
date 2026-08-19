using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="KaligaTheUnjustAI"/>, <see cref="KromedeDismissalMarkerAI"/> and
/// <see cref="KromedeServantAI"/>, translated from retail patterns <c>Cromede_Named_Angry</c>,
/// <c>Cromede_Kkt_Noshow</c>, <c>Cromede_Torture</c> and <c>Cromede_Assijudge</c> (see
/// <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// The one mechanic of the trial that can be landed without touching
/// <see cref="Aion.GameServer.Handlers.Instance.KromedesTrialInstance"/>: when the Angry Judge falls,
/// three markers go out across the manor and call his servants away. The markers are placed rather
/// than broadcast, which is the part worth pinning — a servant standing anywhere else is untouched.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class KaligaTheUnjustAiTests
{
	private const int KromedesTrial = 300230000;

	private const int Kaliga = 217006;
	private const int Wyr = 217002;
	private const int Hamam = 216982;
	private const int Marker = 282115;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(KromedesTrial).WithWorldSize(2048)
			.WithAi(typeof(KaligaTheUnjustAI), typeof(KromedeDismissalMarkerAI), typeof(KromedeServantAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>The judge on his dais, and two servants on the posts our own spawn data gives them.</summary>
	private static (BossAiHarness, Npc, Npc, Npc) Manor()
	{
		BossAiHarness harness = NewHarness();
		Npc kaliga = harness.Spawn(Kaliga, 669.214f, 774.387f, 216.88f);
		Npc hamam = harness.Spawn(Hamam, 750.314f, 625.116f, 197.545f);
		Npc wyr = harness.Spawn(Wyr, 567.989f, 835.774f, 225.826f);
		return (harness, kaliga, hamam, wyr);
	}

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>While he stands, so do they.</summary>
	[Fact]
	public void WhileHeLivesTheServantsStay()
	{
		var (harness, _, _, _) = Manor();
		using BossAiHarness _h = harness;

		harness.Clock.Advance(TimeSpan.FromSeconds(60));

		Assert.Equal(1, Count(harness, Hamam));
		Assert.Equal(1, Count(harness, Wyr));
	}

	/// <summary><b>When he falls, both servants are called away.</b></summary>
	[Fact]
	public void KillingHimCallsTheServantsAway()
	{
		var (harness, kaliga, _, _) = Manor();
		using BossAiHarness _h = harness;

		kaliga.GetAi().OnGeneralEvent(AiEventType.Died);
		harness.Clock.Advance(TimeSpan.FromSeconds(5));

		Assert.Equal(0, Count(harness, Hamam));
		Assert.Equal(0, Count(harness, Wyr));
	}

	/// <summary>
	/// <b>The markers are placed, not shouted.</b> Retail names three coordinates rather than one
	/// broadcast from the dais, and a servant standing anywhere else keeps its post — which a
	/// fifty-metre call from his own body would not leave alone either way.
	/// </summary>
	[Fact]
	public void AServantOffThoseThreePostsIsUntouched()
	{
		var (harness, kaliga, _, _) = Manor();
		using BossAiHarness _h = harness;

		// The manor's far corner, well over fifty metres from all three placements.
		Npc elsewhere = harness.Spawn(Wyr, 660f, 690f, 200f);

		kaliga.GetAi().OnGeneralEvent(AiEventType.Died);
		harness.Clock.Advance(TimeSpan.FromSeconds(5));

		Assert.Contains(harness.LiveNpcs(), n => ReferenceEquals(n, elsewhere));
	}

	/// <summary>
	/// <b>And the markers do not stay.</b> Retail's <c>despawn_self</c> fires the moment they wake, so
	/// the manor is not left with three invisible NPCs standing about for their minute of life.
	/// </summary>
	[Fact]
	public void TheMarkersRemoveThemselves()
	{
		var (harness, kaliga, _, _) = Manor();
		using BossAiHarness _h = harness;

		kaliga.GetAi().OnGeneralEvent(AiEventType.Died);
		harness.Clock.Advance(TimeSpan.FromSeconds(5));

		Assert.Equal(0, Count(harness, Marker));
	}

	/// <summary>The ladder that was dead: two statues a rung, and a column on his quarry.</summary>
	private const int Nagolem = 282124;
	private const int VotaicColumn = 282120;

	private static (BossAiHarness, Npc, Player) Fighting()
	{
		BossAiHarness harness = NewHarness();
		Npc kaliga = harness.Spawn(Kaliga, 669.214f, 774.387f, 216.88f);
		Player player = harness.SpawnPlayer(671f, 776f, 216.88f);
		BossAiHarness.MakeMutuallyKnown(kaliga, player);
		harness.Engage(kaliga, player);
		return (harness, kaliga, player);
	}

	/// <summary>
	/// <b>Below eighty he sets two statues, and below fifty two more.</b>
	/// </summary>
	/// <remarks>
	/// <b>His entire health ladder used to be dead</b>, and not because it was unported: retail arms its
	/// two clocks only from <c>on_arrived_at_waypoint</c>, at the end of a two-hop walk, and our
	/// instance handler spawns him on one static spot with no route. So the branches existed in retail,
	/// the engine could not reach them, and the fight had no mechanics at all.
	/// <para>
	/// The engine grew waypoint arrival this session, so retail's own branches are written too — but
	/// they still cannot fire without a route, which is why the clocks are also armed on entering
	/// combat. That is the divergence, and it is the difference between a ladder and nothing.
	/// </para>
	/// </remarks>
	[Fact]
	public void TheStatueRungsSetTwoEach()
	{
		var (harness, kaliga, _) = Fighting();
		using BossAiHarness _h = harness;

		Assert.Equal(0, Count(harness, Nagolem));

		BossAiHarness.SetHpPercent(kaliga, 79);
		harness.Clock.Advance(TimeSpan.FromSeconds(6));
		Assert.Equal(2, Count(harness, Nagolem));

		BossAiHarness.SetHpPercent(kaliga, 49);
		harness.Clock.Advance(TimeSpan.FromSeconds(6));
		Assert.Equal(4, Count(harness, Nagolem));
	}

	/// <summary><b>And each rung opens once</b>, however long the fight stays in it.</summary>
	[Fact]
	public void AStatueRungOpensOnlyOnce()
	{
		var (harness, kaliga, _) = Fighting();
		using BossAiHarness _h = harness;

		BossAiHarness.SetHpPercent(kaliga, 79);
		harness.Clock.Advance(TimeSpan.FromSeconds(6));
		Assert.Equal(2, Count(harness, Nagolem));

		// The ladder clock keeps ticking every five seconds; the flag var is what stops the rung.
		harness.Clock.Advance(TimeSpan.FromSeconds(30));
		Assert.Equal(2, Count(harness, Nagolem));
	}

	/// <summary><b>Above eighty he sets none at all.</b></summary>
	[Fact]
	public void AboveEightyThereAreNoStatues()
	{
		var (harness, kaliga, _) = Fighting();
		using BossAiHarness _h = harness;

		BossAiHarness.SetHpPercent(kaliga, 90);
		harness.Clock.Advance(TimeSpan.FromSeconds(40));

		Assert.Equal(0, Count(harness, Nagolem));
		Assert.Equal(0, Count(harness, VotaicColumn));
	}

	/// <summary>
	/// <b>And below fifty the columns start</b>, on a coin flip every twenty seconds.
	/// </summary>
	/// <remarks>
	/// Retail guards the column on <c>test_probability 50</c>, so this asserts that some arrive over
	/// several turns rather than that one arrives on a given turn — the distinction that made five pins
	/// in this suite flaky before it was learned.
	/// </remarks>
	[Fact]
	public void BelowFiftyTheColumnsStart()
	{
		var (harness, kaliga, player) = Fighting();
		using BossAiHarness _h = harness;

		BossAiHarness.SetHpPercent(kaliga, 40);

		BossAiHarness.Watched columns = harness.WatchNew(
			200, () => BossAiHarness.Rehate(kaliga, player), VotaicColumn);

		Assert.True(columns.Total > 0, "no column arrived in ten turns of the twenty-second clock");
	}

	/// <summary>Retail's going-home pair, ten seconds each at his own point.</summary>
	private const int LeavingMarkerA = 282084;
	private const int LeavingMarkerB = 282085;

	/// <summary>
	/// <b>Going home he leaves two markers behind</b>, which nothing asserted until the mutation
	/// harness deleted them and the suite stayed green.
	/// </summary>
	[Fact]
	public void GoingHomeLeavesTwoMarkers()
	{
		var (harness, kaliga, _) = Fighting();
		using BossAiHarness _h = harness;

		Assert.Equal(0, Count(harness, LeavingMarkerA));

		kaliga.GetAi().OnGeneralEvent(AiEventType.BACK_HOME);

		Assert.Equal(1, Count(harness, LeavingMarkerA));
		Assert.Equal(1, Count(harness, LeavingMarkerB));

		// Ten seconds, which is retail's live_time -- they are a going-home effect, not scenery.
		harness.Clock.Advance(TimeSpan.FromSeconds(11));
		Assert.Equal(0, Count(harness, LeavingMarkerA));
		Assert.Equal(0, Count(harness, LeavingMarkerB));
	}
}
