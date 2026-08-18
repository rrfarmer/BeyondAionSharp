using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Terath's gravity distortion, and the two effects it used to leave behind permanently.
/// </summary>
/// <remarks>
/// Retail <c>IDTiamat_Sardha</c> gives the black hole ten seconds and the gravity field twenty-four. This
/// class had neither: the field was cleared only by <c>Despawn()</c>, which runs on death and on going
/// home, and the black hole was not cleared at all — so both accumulated for as long as the fight lasted,
/// once every thirty seconds.
/// <para>
/// Written after the previous entry found that "arena, siege or instance npcs with no harness setup" —
/// the reason given for leaving eight fixes unpinned — was untested. <b>Five of the eight have a spawn
/// entry naming their map</b>; this is the second of them pinned.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class TerathGravityAiTests
{
	private const int TiamatStronghold = 300510000;
	private const int Terath = 219354;

	private const int BlackHole = 283096;
	private const int GravityField = 283109;

	private static (BossAiHarness, Npc) Engaged()
	{
		BossAiHarness harness = BossAiHarness.For(TiamatStronghold).WithWorldSize(2048)
			.WithAi(typeof(BrigadeGeneralTerathAI), typeof(DistortedSpaceAI), typeof(GravityAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();
		Npc boss = harness.Spawn(Terath, 1030f, 301f, 409.08f);
		Player player = harness.SpawnPlayer(1032f, 303f, 409.08f);
		harness.Engage(boss, player);

		// His distortion clock starts on the first blow and first fires five seconds later.
		boss.GetAi().OnCreatureEvent(AiEventType.Attack, player);
		harness.Clock.Advance(TimeSpan.FromSeconds(6));
		return (harness, boss);
	}

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary><b>The distortion places a black hole.</b></summary>
	[Fact]
	public void TheDistortionPlacesABlackHole()
	{
		var (harness, _) = Engaged();
		using BossAiHarness _h = harness;

		Assert.True(Count(harness, BlackHole) > 0);
	}

	/// <summary>
	/// <b>And it closes at ten seconds, not eight.</b> The bound was never missing — this add has always
	/// killed itself — but Java closed it two seconds early, and retail writes ten.
	/// </summary>
	/// <remarks>
	/// <b>This pin was written against the wrong thing first.</b> Its original version asserted the hole
	/// was gone by eleven seconds, which passed whether or not the summoner set a lifetime, because the
	/// add's own clock always won. Nine seconds is the only window that separates retail's ten from
	/// Java's eight.
	/// </remarks>
	[Fact]
	public void TheBlackHoleClosesAtTenSeconds()
	{
		var (harness, _) = Engaged();
		using BossAiHarness _h = harness;

		var first = harness.LiveNpcs().Where(n => n.GetNpcId() == BlackHole).ToHashSet();
		Assert.NotEmpty(first);

		// The hole is placed on the five-second tick and the setup already advanced to six, so it is one
		// second old here: eight more makes it nine, inside retail's ten and past Java's eight.
		harness.Clock.Advance(TimeSpan.FromSeconds(8));
		Assert.All(first, hole => Assert.Contains(hole, harness.LiveNpcs()));

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.DoesNotContain(harness.LiveNpcs(), n => first.Contains(n));
	}

	/// <summary>
	/// <b>And they do not pile up across casts.</b> The distortion repeats every thirty seconds, so three
	/// minutes of fighting used to leave six black holes standing on top of each other.
	/// </summary>
	[Fact]
	public void TheEffectsDoNotAccumulate()
	{
		var (harness, _) = Engaged();
		using BossAiHarness _h = harness;

		harness.Clock.Advance(TimeSpan.FromSeconds(180));

		Assert.True(Count(harness, BlackHole) <= 1,
			$"black holes piled up: {Count(harness, BlackHole)}");
		Assert.True(Count(harness, GravityField) <= 1,
			$"gravity fields piled up: {Count(harness, GravityField)}");
	}
}
