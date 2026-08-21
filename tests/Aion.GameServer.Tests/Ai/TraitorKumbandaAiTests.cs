using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Traitor Kumbanda, whose two mechanics hung off a five per cent roll on every blow he took.
/// </summary>
/// <remarks>
/// Retail runs them from two battle timers — circles at five seconds then every fourteen, the avatar at
/// six then every twenty-five. A roll per hit made the cadence a function of how hard he was being hit:
/// a fast group triggered both constantly and a slow one barely at all.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class TraitorKumbandaAiTests
{
	private const int TiamatStronghold = 300510000;

	private const int Kumbanda = 219355;

	/// <summary>The summoning circle, and the avatar he sends at the raid.</summary>
	private const int Circle = 283086;
	private const int Avatar = 283085;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(TiamatStronghold).WithWorldSize(2048)
			.WithAi(typeof(TraitorKumbandaAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>Engages him near his own room, with a tank and a second player behind.</summary>
	private static (Npc Boss, Player Tank, Player Other) Engaged(BossAiHarness harness)
	{
		Npc boss = harness.Spawn(Kumbanda, 862f, 1319f, 396f);
		Player tank = harness.SpawnPlayer(866f, 1319f, 396f);
		Player other = harness.SpawnPlayer(858f, 1319f, 396f);
		harness.Engage(boss, tank);
		boss.GetAggroList().AddHate(other, 1);
		return (boss, tank, other);
	}

	/// <summary>
	/// <b>The circles stand on retail's four marks.</b>
	/// </summary>
	/// <remarks>
	/// This class put one at his feet and scattered six more at random inside six metres, so the marks a
	/// raid learns to avoid did not exist.
	/// </remarks>
	[Fact]
	public void TheCirclesStandOnTheFourMarks()
	{
		using BossAiHarness harness = NewHarness();
		Engaged(harness);

		harness.Clock.Advance(TimeSpan.FromSeconds(6));

		var marks = harness.LiveNpcs().Where(n => n.GetNpcId() == Circle)
			.Select(n => ((int)n.GetX(), (int)n.GetY())).OrderBy(p => p).ToArray();
		Assert.Equal([(853, 1306), (853, 1332), (871, 1306), (871, 1332)], marks);
	}

	/// <summary>
	/// <b>And they last retail's fifteen seconds, then come again at fourteen-second intervals.</b>
	/// </summary>
	[Fact]
	public void TheCirclesKeepTheirOwnClock()
	{
		using BossAiHarness harness = NewHarness();
		Engaged(harness);

		harness.Clock.Advance(TimeSpan.FromSeconds(6));
		Assert.Equal(4, Count(harness, Circle));

		// The next turn is at nineteen seconds and the first set lives until twenty, so for that one
		// second there are eight -- retail's sets overlap, which is why the "one at a time" guard this
		// class had suppressed every turn after the first.
		harness.Clock.Advance(TimeSpan.FromSeconds(13));
		Assert.Equal(8, Count(harness, Circle));

		// A second later the first set is gone and only the second stands.
		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.Equal(4, Count(harness, Circle));
	}

	/// <summary>
	/// <b>The avatar arrives on somebody other than the tank, already fighting them.</b>
	/// </summary>
	/// <remarks>
	/// Retail's <c>ATTACKERI_RANDOM_ONE_EXCEPT_CURRENT_TARGET</c> with
	/// <c>hatepoints_to_add=2147483647</c>. This class spawned it at Kumbanda's own position with no hate
	/// at all, so it simply walked to whoever he was already fighting — the opposite of the mechanic.
	/// </remarks>
	[Fact]
	public void TheAvatarArrivesOnSomeoneOtherThanTheTank()
	{
		using BossAiHarness harness = NewHarness();
		(Npc boss, Player tank, Player other) = Engaged(harness);

		// Inside retail's fifteen-to-seventy window, so the rung is open.
		BossAiHarness.SetHpPercent(boss, 60);
		// Seven seconds to the avatar, and one more for the tick its hate is applied on.
		harness.Clock.Advance(TimeSpan.FromSeconds(8));

		// Asserted against whoever the boss is actually facing rather than against the player this pin
		// calls the tank: which of the two tops his aggro list is the harness's business, and retail's
		// rule is only "not that one".
		Creature facing = boss.GetAggroList().GetTarget(
			Aion.GameServer.Controllers.Attack.AggroTarget.MOST_HATED)!;

		Npc avatar = Assert.Single(harness.LiveNpcs(), n => n.GetNpcId() == Avatar);
		Assert.NotSame(facing, avatar.GetTarget());
		Assert.Contains(avatar.GetTarget(), new object[] { tank, other });

		Creature landedOn = (Creature)avatar.GetTarget()!;
		Assert.Equal(landedOn.GetX(), avatar.GetX(), 1);
		Assert.True(avatar.GetAggroList().GetHate(landedOn) > 0,
			"the avatar did not arrive locked onto the player it was dropped on");
	}

	/// <summary>
	/// <b>Above seventy per cent there is no avatar at all.</b>
	/// </summary>
	/// <remarks>
	/// Retail's window is fifteen to seventy; this class used "below fifty", so the avatar came late and
	/// then never stopped.
	/// </remarks>
	[Fact]
	public void AboveSeventyPerCentNoAvatarComes()
	{
		using BossAiHarness harness = NewHarness();
		Engaged(harness);

		harness.Clock.Advance(TimeSpan.FromSeconds(30));

		Assert.Equal(0, Count(harness, Avatar));
	}

	/// <summary>
	/// <b>And at sixty per cent one does</b> — which is inside retail's window and outside the old one.
	/// </summary>
	/// <remarks>
	/// This is the case that separates retail's seventy from the fifty this class had: at sixty, retail
	/// sends the avatar and the old code did not.
	/// </remarks>
	[Fact]
	public void AtSixtyPerCentTheAvatarComes()
	{
		using BossAiHarness harness = NewHarness();
		(Npc boss, _, _) = Engaged(harness);

		BossAiHarness.SetHpPercent(boss, 60);
		harness.Clock.Advance(TimeSpan.FromSeconds(7));

		Assert.Equal(1, Count(harness, Avatar));
	}

	/// <summary>
	/// <b>Below fifteen per cent the circles stop.</b>
	/// </summary>
	[Fact]
	public void BelowFifteenPerCentTheCirclesStop()
	{
		using BossAiHarness harness = NewHarness();
		(Npc boss, _, _) = Engaged(harness);

		BossAiHarness.SetHpPercent(boss, 12);
		harness.Clock.Advance(TimeSpan.FromSeconds(30));

		Assert.Equal(0, Count(harness, Circle));
	}

	/// <summary>
	/// <b>He enrages at fifteen per cent, not five.</b>
	/// </summary>
	[Fact]
	public void TheRageWaitsForFifteenPerCent()
	{
		using BossAiHarness harness = NewHarness();
		(Npc boss, Player tank, _) = Engaged(harness);

		BossAiHarness.SetHpPercent(boss, 20);
		boss.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.Attack, tank);
		Assert.False(boss.GetEffectController().HasAbnormalEffect(20942),
			"Kumbanda enraged at twenty per cent, where retail waits for fifteen");

		BossAiHarness.SetHpPercent(boss, 14);
		boss.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.Attack, tank);
		Assert.True(boss.GetEffectController().HasAbnormalEffect(20942),
			"Kumbanda did not enrage at fourteen per cent");
	}
}
