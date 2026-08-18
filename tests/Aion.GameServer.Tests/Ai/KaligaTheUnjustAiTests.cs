using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;

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
}
