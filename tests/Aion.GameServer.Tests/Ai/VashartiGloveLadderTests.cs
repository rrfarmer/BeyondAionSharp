using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Vasharti's glove drop — retail's sixteen-rung ladder, which this port ran as a fixed-rate loop.
/// </summary>
/// <remarks>
/// Retail hands the whole phase to a controller npc (<c>IDYun_Vasharti_Glove_ControllerA</c>, <c>C</c>
/// and <c>E</c>) whose battle timer walks a scripted ladder: five triples of "put a smash under two or
/// three named players, then rain three red at the glove point, then three blue", each rung armed by the
/// one before and each carrying its own test-and-set flag var so the sequence runs once and in order.
/// <para>
/// What stood here dropped fourteen, nineteen or twenty-four smashes around the boss every 7.1 seconds,
/// all of them area drops — so the half of the mechanic that puts a smash under a named player did not
/// exist at all.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class VashartiGloveLadderTests
{
	private const int RentusBase = 300230000;

	private const int Vasharti = 217313;

	private const int SmashRed = 283008;
	private const int SmashBlue = 283009;

	private const int RedWall = 283010;
	private const int GloveBuffer = 283007;

	/// <summary>The skill whose start opens the glove phase.</summary>
	private const int SeaOfFire = 20534;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(RentusBase).WithWorldSize(2048)
			.WithAi(typeof(BrigadeGeneralVashartiAI), typeof(DancingFlameAI), typeof(UseSkillAndDieAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>Puts Vasharti into the glove phase without going through the skill chain.</summary>
	private static (BossAiHarness Harness, Npc Boss, Player Player) InTheGlovePhase()
	{
		BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Vasharti, 188f, 414f, 260.6f);
		Player player = harness.SpawnPlayer(193f, 414f, 260.6f);
		harness.Engage(boss, player);

		// The real entry point: retail reaches the glove phase through Sea of Fire, and the class hangs
		// the whole ladder off that skill starting.
		((BrigadeGeneralVashartiAI)boss.GetAi()).OnStartUseSkill(
			Aion.GameServer.Dataholders.DataManager.SKILL_DATA.GetSkillTemplate(SeaOfFire), 1);
		return (harness, boss, player);
	}

	/// <summary>
	/// <b>The wall and the buffer arrive at retail's glove point, and both carry a lifetime.</b>
	/// </summary>
	/// <remarks>
	/// Neither had one. Retail gives the wall forty seconds (forty-five for the burning ground) and the
	/// controller despawns it besides — and Java's cleanup could not, because it switched on the boss's
	/// own npc id rather than the npc it was looking at, so <b>every wall stood for the rest of the
	/// instance</b>.
	/// </remarks>
	[Fact]
	public void TheWallStandsAtTheGlovePointAndLeavesOnRetailsClock()
	{
		(BossAiHarness harness, _, _) = InTheGlovePhase();
		using BossAiHarness _h = harness;

		Npc wall = Assert.Single(harness.LiveNpcs(), n => n.GetNpcId() == RedWall);
		Assert.Equal(188.33f, wall.GetX(), 2);
		Assert.Equal(414.61f, wall.GetY(), 2);
		Assert.Equal(1, Count(harness, GloveBuffer));

		harness.Clock.Advance(TimeSpan.FromSeconds(39));
		Assert.Equal(1, Count(harness, RedWall));

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.Equal(0, Count(harness, RedWall));
	}

	/// <summary>
	/// <b>Nothing falls for four seconds</b>, which is retail's opening timer.
	/// </summary>
	[Fact]
	public void TheLadderWaitsFourSecondsBeforeItsFirstRung()
	{
		(BossAiHarness harness, _, _) = InTheGlovePhase();
		using BossAiHarness _h = harness;

		harness.Clock.Advance(TimeSpan.FromMilliseconds(3500));

		Assert.Equal(0, Count(harness, SmashRed));
		Assert.Equal(0, Count(harness, SmashBlue));
	}

	/// <summary>
	/// <b>The first rung puts a smash of each colour under a player</b>, and none at the glove point.
	/// </summary>
	/// <remarks>
	/// This is the half of the mechanic that was missing. With one player in the room retail's
	/// <c>total_set_to_spawn=2</c> can only reach that one player, so exactly one red and one blue land.
	/// </remarks>
	[Fact]
	public void TheFirstRungDropsOnTheNamedPlayers()
	{
		(BossAiHarness harness, _, Player player) = InTheGlovePhase();
		using BossAiHarness _h = harness;

		harness.Clock.Advance(TimeSpan.FromMilliseconds(4500));

		Assert.Equal(1, Count(harness, SmashRed));
		Assert.Equal(1, Count(harness, SmashBlue));

		foreach (Npc smash in harness.LiveNpcs().Where(
			n => n.GetNpcId() == SmashRed || n.GetNpcId() == SmashBlue))
		{
			Assert.Equal(player.GetX(), smash.GetX(), 0);
			Assert.Equal(player.GetY(), smash.GetY(), 0);
		}
	}

	/// <summary>
	/// <b>And the two rungs after it rain three of one colour each, a second and then two apart.</b>
	/// </summary>
	/// <remarks>
	/// Retail's triple is 3000ms to the red rain, 1000ms to the blue, 2000ms back to the next pick.
	/// <para>
	/// Both counts are <b>four, not three</b>: each smash lives six seconds, so the one dropped under the
	/// player on the first rung is still standing when the rain of three arrives three seconds later.
	/// Reading three here was my own arithmetic slip and the pin caught it.
	/// </para>
	/// </remarks>
	[Fact]
	public void TheTripleRainsRedThenBlue()
	{
		(BossAiHarness harness, _, _) = InTheGlovePhase();
		using BossAiHarness _h = harness;

		// 4s opening + 3s to the red rain.
		harness.Clock.Advance(TimeSpan.FromMilliseconds(7500));
		Assert.Equal(4, Count(harness, SmashRed));

		// A second later the blue rain follows.
		harness.Clock.Advance(TimeSpan.FromMilliseconds(1000));
		Assert.Equal(4, Count(harness, SmashBlue));
	}

	/// <summary>
	/// <b>The ladder ends.</b> It is sixteen rungs and thirty-eight seconds, not a loop.
	/// </summary>
	/// <remarks>
	/// The fixed-rate task it replaced never stopped on its own — it ran until the sea-of-fire effect
	/// ended and cancelled it.
	/// </remarks>
	[Fact]
	public void TheLadderStopsAfterItsLastRung()
	{
		(BossAiHarness harness, _, _) = InTheGlovePhase();
		using BossAiHarness _h = harness;

		// Well past the thirty-eight seconds the rungs sum to, and past the six-second smash life.
		harness.Clock.Advance(TimeSpan.FromSeconds(50));

		Assert.Equal(0, Count(harness, SmashRed));
		Assert.Equal(0, Count(harness, SmashBlue));
	}
}
