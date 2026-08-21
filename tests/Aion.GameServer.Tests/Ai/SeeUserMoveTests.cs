using Aion.GameServer.Ai;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Xunit;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// <b>Retail's <c>on_see_user_move</c>: a player moving inside an NPC's sight, which is not the same
/// event as first seeing them.</b>
/// </summary>
/// <remarks>
/// 254 patterns carry it and <b>14 have no <c>on_see_user</c> at all</b>, so for those a raid walking
/// up did nothing whatsoever. The extractor had recorded it as unbuildable because "this port raises
/// no 'a player moved nearby' event" -- <c>MovementNotifyTask</c> has raised exactly that the whole
/// time.
/// <para>
/// <c>IDRaksha_Solo_Starter_NPC</c> (206390) is the clearest thing to pin it on: sight of ten metres,
/// and a rung that removes it when a player of either race is seen. Because the same test-and-set flag
/// backs both handlers, whichever fires first spends it -- so the way to prove the <i>move</i> path is
/// to keep the player out of sight until after spawning, which is also the case retail's handler
/// exists for.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public class SeeUserMoveTests
{
	/// <summary>Rakshasa Solo, where the starter npc lives.</summary>
	private const int AnyMap = 300230000;

	/// <summary><c>IDRaksha_Solo_Starter_NPC</c> — <c>srange="10"</c>, and it leaves when it sees you.</summary>
	private const int Starter = 206390;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(AnyMap).WithWorldSize(2048)
			.WithAi(typeof(PassivePatternAI), typeof(BattleCycleAI), typeof(AggressiveNpcAI),
				typeof(GeneralNpcAI))
			.Build();

	/// <summary>
	/// <b>Walking into its sight is enough.</b> The player is placed well outside ten metres, so the
	/// sighting handler has already had its chance and declined; only the movement rung is left.
	/// </summary>
	[Fact]
	public void WalkingIntoSightIsEnoughToSendItAway()
	{
		using BossAiHarness harness = NewHarness();
		Npc starter = harness.Spawn(Starter, 300f, 300f, 200f);
		Player raider = harness.SpawnPlayer(360f, 300f, 200f, race: Race.ELYOS);
		Assert.Contains(starter, harness.LiveNpcs());

		harness.Walk(raider, 303f, 300f, 200f);

		Assert.DoesNotContain(starter, harness.LiveNpcs());
	}

	/// <summary>
	/// <b>Moving about out of range does not.</b> Sight is part of seeing, and the engine's movement
	/// event covers the whole known list — without the range test this rung would fire the moment
	/// anybody stirred anywhere nearby, which is the mistake already recorded on <c>on_see_user</c>.
	/// </summary>
	[Fact]
	public void MovingAboutOutOfRangeDoesNot()
	{
		using BossAiHarness harness = NewHarness();
		Npc starter = harness.Spawn(Starter, 300f, 300f, 200f);
		Player raider = harness.SpawnPlayer(360f, 300f, 200f, race: Race.ELYOS);

		harness.Walk(raider, 355f, 300f, 200f);

		Assert.Contains(starter, harness.LiveNpcs());
	}
}
