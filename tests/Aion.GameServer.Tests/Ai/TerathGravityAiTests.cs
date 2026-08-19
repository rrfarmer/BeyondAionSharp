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

		// His distortion clock starts on the first blow and first fires at retail's twelve seconds --
		// it was five until the cadence was corrected, and these pins had the old number baked in.
		boss.GetAi().OnCreatureEvent(AiEventType.Attack, player);
		harness.Clock.Advance(TimeSpan.FromSeconds(13));
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
	/// <b>And he shuts it two seconds after opening it.</b>
	/// </summary>
	/// <remarks>
	/// <b>This pin said "ten seconds" until the close was implemented, and it was measuring the
	/// backstop.</b> Retail's branch spawns the hole with <c>live_time=10</c> and, in the same breath,
	/// arms <c>BTIMERI_INDEX_28</c> at 2000; that timer broadcasts 31 and the hole answers by closing.
	/// So ten is what happens when nothing closes it — which, before the message existed here, was every
	/// time. Two is what a fight sees.
	/// <para>
	/// The ten-second backstop still exists and is pinned where it belongs, on a hole standing on its
	/// own: <c>SardhaBlackHoleTests.AndTheHoleClosesAtTenSeconds</c>.
	/// </para>
	/// </remarks>
	[Fact]
	public void HeShutsTheHoleTwoSecondsAfterOpeningIt()
	{
		var (harness, _) = Engaged();
		using BossAiHarness _h = harness;

		var first = harness.LiveNpcs().Where(n => n.GetNpcId() == BlackHole).ToHashSet();
		Assert.NotEmpty(first);

		// The hole is placed on the twelve-second tick and the setup already advanced to thirteen, so
		// it is one second old here: one more second reaches the close, and two more clear it.
		harness.Clock.Advance(TimeSpan.FromSeconds(2));

		Assert.DoesNotContain(harness.LiveNpcs(), n => first.Contains(n));
	}

	/// <summary>
	/// <b>And they do not pile up across casts.</b> The distortion repeats every fifteen seconds, so three
	/// minutes of fighting would otherwise leave a dozen black holes standing on top of each other.
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

	/// <summary>
	/// <b>Below fifteen per cent he stops opening them.</b>
	/// </summary>
	/// <remarks>
	/// Retail guards the distortion branch with <c>is_hp_in_boundary larger_than=15 less_than=100</c> —
	/// the same band as his jump, which has honoured it since an earlier pass. This class ran the
	/// distortion on nothing but "am I alive", so below fifteen he stopped jumping and went on opening
	/// holes: half of a design that is plainly one thing. Retail clears the floor for the end of the
	/// fight.
	/// <para>
	/// Counted as they arrive, and his health held down each second — over a window this long his own
	/// regeneration lifts him back over the line and the holes correctly resume, which reads as the
	/// guard not working.
	/// </para>
	/// </remarks>
	[Fact]
	public void BelowFifteenPerCentHeStopsOpeningThem()
	{
		var (harness, boss) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 12);

		Assert.Equal(0, harness.WatchNew(60, () => BossAiHarness.SetExactPercent(boss, 12), BlackHole).Total);
	}

	/// <summary>
	/// <b>And at twenty he still does.</b> The floor is a number, not "wounded".
	/// </summary>
	[Fact]
	public void AndAtTwentyHeStillDoes()
	{
		var (harness, boss) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 20);

		Assert.True(harness.WatchNew(20, () => BossAiHarness.SetExactPercent(boss, 20), BlackHole).Total > 0,
			"at twenty per cent he should still open holes");
	}
}
