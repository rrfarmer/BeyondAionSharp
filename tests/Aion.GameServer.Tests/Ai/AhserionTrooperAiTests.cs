using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The construct destroyer's troopers, and the one case where a summoner-side lifetime is the <b>only</b>
/// place the number can go.
/// </summary>
/// <remarks>
/// This log arrived at "one add, one clock" after four summoner-side lifetimes turned out to be dead code
/// duplicating the add's own. <b>Ahserion's troopers are the exception that shows the rule is not
/// general.</b> Retail gives the same npc — <c>BGab1_Sub_Pod_Sum_Vri_As</c> — two different lifetimes
/// depending on who summoned it:
/// <list type="bullet">
/// <item><c>Gab1_Sub_AssultPod_Strike</c> and <c>Gab1_Sub_AssultTBM_Strike</c>: <b>7,200 seconds</b></item>
/// <item><c>Gab1_Sub_Tank_Destroyer</c>: <b>180 seconds</b></item>
/// </list>
/// <b>No clock in the add can express that</b>, because the add does not know who called it. The
/// destroyer's three minutes has to live in the destroyer, and it does.
/// <para>
/// It survives the add's own eight-minute backstop only by being shorter. That is the same "smaller wins"
/// arrangement that made four other fixes inert — here it is correct, and it is correct for a reason worth
/// stating rather than by luck.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class AhserionTrooperAiTests
{
	private const int AhserionsFlight = 400030000;
	private const int ConstructDestroyer = 297185;
	private const int TrooperAssassin = 297191;

	private static (BossAiHarness, Npc) Aggroed()
	{
		BossAiHarness harness = BossAiHarness.For(AhserionsFlight).WithWorldSize(2048)
			.WithAi(typeof(AhserionConstructDestroyerAI), typeof(AhserionAggressiveNpcAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();
		Npc destroyer = harness.Spawn(ConstructDestroyer, 396f, 405f, 689.154f);
		Player player = harness.SpawnPlayer(398f, 407f, 689.154f);
		BossAiHarness.MakeMutuallyKnown(destroyer, player);
		harness.Engage(destroyer, player);

		// Its troopers land on the aggro event, which Engage does not raise on its own.
		destroyer.GetAi().OnCreatureEvent(AiEventType.CreatureAggro, player);
		return (harness, destroyer);
	}

	private static int Troopers(BossAiHarness harness) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == TrooperAssassin);

	/// <summary><b>Aggroing it lands a pair of troopers.</b></summary>
	[Fact]
	public void AggroLandsTwoTroopers()
	{
		var (harness, _) = Aggroed();
		using BossAiHarness _h = harness;

		Assert.Equal(2, Troopers(harness));
	}

	/// <summary>
	/// <b>And they leave at three minutes, not eight.</b> The add's own class removes it after eight
	/// minutes; retail's destroyer keeps it for three, and three is what a raid actually sees.
	/// </summary>
	[Fact]
	public void TheTroopersLeaveAtThreeMinutes()
	{
		var (harness, _) = Aggroed();
		using BossAiHarness _h = harness;

		harness.Clock.Advance(TimeSpan.FromSeconds(179));
		Assert.Equal(2, Troopers(harness));

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.Equal(0, Troopers(harness));
	}
}
