using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Brigade General Terath, whose jump event was placing two hostile drakan.
/// </summary>
/// <remarks>
/// Retail's <c>IDTiamat_Sardha</c> names every npc this fight places. Java named one of them with a
/// <c>TODO find Right ID</c> and guessed 283558 — <c>3rd vituperators assassin</c>, a real aggressive
/// monster one digit away from the effect npc retail actually uses. These pins assert the ids, the posts
/// and the lifetimes, which are the parts the pattern states outright.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class BrigadeGeneralTerathAiTests
{
	private const int TiamatStronghold = 300510000;

	private const int Terath = 219354;

	/// <summary>Retail's <c>IDTiamat_Sadha_JumpBoxFX</c>, and the drakan Java put in its place.</summary>
	private const int JumpBoxFx = 283158;
	private const int VituperatorsAssassin = 283558;

	/// <summary>The aetheric field, and the two gravity npcs the jump drops between the posts.</summary>
	private const int AethericField = 730692;
	private const int GravityUp = 283109;
	private const int GravityDown = 283110;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(TiamatStronghold).WithWorldSize(2048)
			// The gravity pair carries an AI of its own and the harness validates every name it is asked
			// to place, so omitting it makes the spawn throw. Fifth pin this session to fail that way.
			.WithAi(typeof(BrigadeGeneralTerathAI), typeof(DistortedSpaceAI), typeof(GravityAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	private static Npc Engaged(BossAiHarness harness)
	{
		Npc terath = harness.Spawn(Terath, 1030f, 300f, 409f);
		Player player = harness.SpawnPlayer(1035f, 300f, 409f);
		harness.Engage(terath, player);
		return terath;
	}

	/// <summary>
	/// <b>Engaging Terath raises his aetheric field inside the room.</b>
	/// </summary>
	/// <remarks>
	/// Java spawned it at <c>(1030.08, 1030.08, 1030.08)</c> — the x repeated into y and z — which puts it
	/// seven hundred units up the map from the fight. Retail's post is <c>(1030.08, 297.31, 407.04)</c>.
	/// </remarks>
	[Fact]
	public void HisFieldStandsWhereRetailPutsIt()
	{
		using BossAiHarness harness = NewHarness();
		Engaged(harness);

		Npc field = Assert.Single(harness.LiveNpcs(), n => n.GetNpcId() == AethericField);
		Assert.Equal(1030.08f, field.GetX(), 2);
		Assert.Equal(297.31f, field.GetY(), 2);
		Assert.Equal(407.04f, field.GetZ(), 2);
	}

	/// <summary>
	/// <b>The jump event places effect npcs and no monsters.</b>
	/// </summary>
	/// <remarks>
	/// This is the whole point of the correction: 283558 is a monster and would fight the party, 283158
	/// is an effect npc on <c>general</c> and does not.
	/// </remarks>
	[Fact]
	public void TheJumpPlacesEffectNpcsAndNoDrakan()
	{
		using BossAiHarness harness = NewHarness();
		Npc terath = Engaged(harness);

		// Retail's jump timer: thirty-five seconds after he enters combat.
		harness.Clock.Advance(TimeSpan.FromSeconds(36));

		Assert.Equal(2, Count(harness, JumpBoxFx));
		Assert.Equal(0, Count(harness, VituperatorsAssassin));
	}

	/// <summary>
	/// <b>And they stand on retail's two posts</b>, and are cleared when the event ends.
	/// </summary>
	/// <remarks>
	/// Retail's live time is twenty-nine seconds and this class ends its whole gravity event at thirty,
	/// deleting whatever is left by hand — so the two are <b>one second apart</b>, and only an assertion
	/// inside that second tells them apart. It is made below: present at twenty-eight, gone at
	/// twenty-nine and a half. Without the lifetime the boxes survive to the thirty-second sweep.
	/// <para>
	/// The gravity pair has no such window: retail gives it twenty-four seconds from a spawn ten seconds
	/// in, which lands at thirty-four, and the sweep at thirty always beats it. That lifetime is set
	/// because it matters the moment the cadence is corrected, but nothing here can observe it.
	/// </para>
	/// </remarks>
	[Fact]
	public void TheJumpBoxesStandOnBothPostsAndAreClearedWithTheEvent()
	{
		using BossAiHarness harness = NewHarness();
		Npc terath = Engaged(harness);

		// Retail's jump timer: thirty-five seconds after he enters combat.
		harness.Clock.Advance(TimeSpan.FromSeconds(36));

		float[] posts = harness.LiveNpcs().Where(n => n.GetNpcId() == JumpBoxFx)
			.Select(n => n.GetX()).OrderBy(x => x).ToArray();
		Assert.Equal(1002.07f, posts[0], 2);
		Assert.Equal(1056.8f, posts[1], 2);

		harness.Clock.Advance(TimeSpan.FromSeconds(27));
		Assert.Equal(2, Count(harness, JumpBoxFx));

		// Inside the one second between retail's live time and this class's own sweep.
		harness.Clock.Advance(TimeSpan.FromMilliseconds(1500));
		Assert.Equal(0, Count(harness, JumpBoxFx));
	}

	/// <summary>
	/// <b>The two gravity npcs arrive ten seconds in, on the same point.</b>
	/// </summary>
	/// <remarks>
	/// Retail places both at <c>(1029.93, 297.31, 409)</c>; this port had them a few centimetres apart on
	/// two hand-typed rows. They are cleared with the event, as above.
	/// </remarks>
	[Fact]
	public void TheGravityPairArrivesTogetherAndIsClearedWithTheEvent()
	{
		using BossAiHarness harness = NewHarness();
		Npc terath = Engaged(harness);

		// Retail's jump timer: thirty-five seconds after he enters combat.
		harness.Clock.Advance(TimeSpan.FromSeconds(36));

		harness.Clock.Advance(TimeSpan.FromSeconds(8));
		Assert.Equal(0, Count(harness, GravityUp));

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Npc up = Assert.Single(harness.LiveNpcs(), n => n.GetNpcId() == GravityUp);
		Npc down = Assert.Single(harness.LiveNpcs(), n => n.GetNpcId() == GravityDown);
		Assert.Equal(up.GetX(), down.GetX(), 2);
		Assert.Equal(up.GetY(), down.GetY(), 2);
		Assert.Equal(1029.93f, up.GetX(), 2);

		harness.Clock.Advance(TimeSpan.FromSeconds(20));
		Assert.Equal(0, Count(harness, GravityUp));
		Assert.Equal(0, Count(harness, GravityDown));
	}

	/// <summary>The black hole's own npc — the one that carries its five damage ticks.</summary>
	private const int BlackHoleTicker = 283097;

	/// <summary>
	/// <b>The black hole opens twelve seconds into the fight, not five.</b>
	/// </summary>
	/// <remarks>
	/// Retail arms <c>BTIMERI_INDEX_2</c> at twelve seconds when Terath enters combat. This class opened
	/// at five.
	/// </remarks>
	[Fact]
	public void TheBlackHoleOpensAtTwelveSeconds()
	{
		using BossAiHarness harness = NewHarness();
		Engaged(harness);

		harness.Clock.Advance(TimeSpan.FromSeconds(11));
		Assert.Equal(0, Count(harness, BlackHoleTicker));

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.Equal(1, Count(harness, BlackHoleTicker));
	}

	/// <summary>
	/// <b>And again every fifteen seconds, not every thirty.</b>
	/// </summary>
	/// <remarks>
	/// This is the half of the correction that changes how the fight feels: at thirty seconds a raid saw
	/// the hazard half as often as retail's. Counted by arrivals rather than by what is standing, because
	/// each black hole closes itself after ten seconds.
	/// <para>
	/// The window stops short of thirty-five seconds on purpose. <b>This class cancels the black hole
	/// while the jump event runs</b> and restarts it thirty seconds later, which retail does not do —
	/// retail's timer 2 is never touched by the jump rung. That divergence is recorded in
	/// docs/retail-ai-fidelity.md rather than fixed here, and this pin measures the stretch before the
	/// first jump so it asserts the cadence and not the interruption.
	/// </para>
	/// </remarks>
	[Fact]
	public void TheBlackHoleReturnsEveryFifteenSeconds()
	{
		using BossAiHarness harness = NewHarness();
		Engaged(harness);

		// Twelve seconds to the first, then one every fifteen: two inside the first thirty-three.
		BossAiHarness.Watched seen = harness.WatchNew(33, null, BlackHoleTicker);

		Assert.Equal(2, seen.Total);
	}

	/// <summary>
	/// <b>Terath enrages at fourteen per cent, not twenty-five.</b>
	/// </summary>
	/// <remarks>
	/// Retail's rung reads <c>is_hp_lower_than percent=14</c>. Eleven points of health is a long stretch
	/// of this fight to spend enraged.
	/// <para>
	/// Asserted through the buff's own abnormal effect: the enrage is a self-cast, so there is nothing
	/// spawned to count.
	/// </para>
	/// </remarks>
	[Fact]
	public void TheRageWaitsForFourteenPerCent()
	{
		using BossAiHarness harness = NewHarness();
		Npc terath = Engaged(harness);

		BossAiHarness.SetHpPercent(terath, 20);
		terath.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.Attack, terath);
		Assert.False(terath.GetEffectController().HasAbnormalEffect(20942),
			"Terath enraged at twenty per cent, where retail waits for fourteen");

		BossAiHarness.SetHpPercent(terath, 13);
		terath.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.Attack, terath);
		Assert.True(terath.GetEffectController().HasAbnormalEffect(20942),
			"Terath did not enrage at thirteen per cent");
	}

	/// <summary>
	/// <b>He jumps again fifty-five seconds later, and not on any health threshold.</b>
	/// </summary>
	/// <remarks>
	/// Retail arms <c>BTIMERI_INDEX_1</c> at 35 seconds and re-arms it at 55 after every jump. This class
	/// fired the jump off HP phases, so a raid that burned him quickly saw four jumps in a row and one
	/// that ground him down slowly saw none between phases.
	/// </remarks>
	[Fact]
	public void HeJumpsAgainFiftyFiveSecondsLater()
	{
		using BossAiHarness harness = NewHarness();
		Engaged(harness);

		// First jump at 35; the boxes it drops live 29 seconds, so by 70 the room is clear again.
		harness.Clock.Advance(TimeSpan.FromSeconds(70));
		Assert.Equal(0, Count(harness, JumpBoxFx));

		// Second jump at 90, with no change in health at all.
		harness.Clock.Advance(TimeSpan.FromSeconds(21));
		Assert.Equal(2, Count(harness, JumpBoxFx));
	}

	/// <summary>
	/// <b>And below fifteen per cent he stops jumping.</b>
	/// </summary>
	/// <remarks>
	/// Retail's rung is guarded by <c>is_hp_in_boundary larger_than=15 less_than=100</c>, and the bare
	/// rung underneath only re-arms at three seconds — so once he is under the floor the jump never comes
	/// again. That is the opposite of the HP ladder this class had, which fired a jump <i>because</i> he
	/// had dropped past a threshold.
	/// </remarks>
	[Fact]
	public void BelowFifteenPerCentHeStopsJumping()
	{
		using BossAiHarness harness = NewHarness();
		Npc terath = Engaged(harness);

		BossAiHarness.SetHpPercent(terath, 12);

		// Past the first jump, and past two more turns of the fifty-five second clock.
		harness.Clock.Advance(TimeSpan.FromSeconds(150));

		Assert.Equal(0, Count(harness, JumpBoxFx));
	}

	/// <summary>
	/// <b>But at thirty per cent he still jumps.</b> The floor is fifteen, not "wounded".
	/// </summary>
	/// <remarks>
	/// Without this the floor's <i>value</i> is unpinned: a guard set anywhere between sixteen and
	/// ninety-nine passes the pin above, because that one only ever asks about twelve per cent. Raising
	/// the floor to half health survived the mutation sweep until this case was added.
	/// </remarks>
	[Fact]
	public void AtThirtyPerCentHeStillJumps()
	{
		using BossAiHarness harness = NewHarness();
		Npc terath = Engaged(harness);

		BossAiHarness.SetHpPercent(terath, 30);
		harness.Clock.Advance(TimeSpan.FromSeconds(36));

		Assert.Equal(2, Count(harness, JumpBoxFx));
	}
}
