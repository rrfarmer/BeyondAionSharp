using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Popuchin's shulack guided bomb, which a player could outrun forever.
/// </summary>
/// <remarks>
/// Retail's <c>Station_Flight_GuiBomb</c> is one <c>on_enter_attack_state</c> that arms a 3000 fuse and
/// a 13000 backstop. This class polled every second and only detonated within four units, so a player
/// who kept moving was never hit; and its clock ran from spawning, not from aggro.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class ShulackGuidedBombAiTests
{
	private const int AturamSkyFortress = 300350000;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(AturamSkyFortress).WithWorldSize(2048)
			.WithAi(typeof(ShulackGuidedBombAI), typeof(PopuchinAI), typeof(AggressiveNpcAI),
				typeof(GeneralNpcAI)).Build();

	private static int Bombs(BossAiHarness harness) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == PopuchinAI.GuidedBomb);

	/// <summary>
	/// <b>A bomb nobody aggros has no clock of its own.</b>
	/// </summary>
	/// <remarks>
	/// It used to delete itself ten seconds after spawning. Retail arms nothing until the bomb enters
	/// attack state — an untouched bomb is Popuchin's to clear when he resets, which is the rung below.
	/// </remarks>
	[Fact]
	public void ABombNobodyAggrosKeepsWaiting()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(PopuchinAI.GuidedBomb, 300f, 300f, 200f);

		harness.Clock.Advance(TimeSpan.FromMinutes(1));

		Assert.Equal(1, Bombs(harness));
	}

	/// <summary>
	/// <b>One that does aggro goes off three seconds later, wherever its target is standing.</b>
	/// </summary>
	/// <remarks>
	/// This is the whole defect: the old class detonated only within four units, so <b>a player who kept
	/// walking was never hit at all</b> — the bomb trailed him until its own clock expired. The player
	/// here is put sixty units away and left there.
	/// </remarks>
	[Fact]
	public void OneThatAggrosGoesOffThreeSecondsLaterAtAnyRange()
	{
		using BossAiHarness harness = NewHarness();
		Npc bomb = harness.Spawn(PopuchinAI.GuidedBomb, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(360f, 300f, 200f);
		bomb.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.CREATURE_AGGRO, player);

		harness.Clock.Advance(TimeSpan.FromMilliseconds(2500));
		Assert.Equal(1, Bombs(harness));

		// Fuse at 3000, then the 3.2s grace the npc needs to outlive its own cast.
		harness.Clock.Advance(TimeSpan.FromSeconds(4));
		Assert.Equal(0, Bombs(harness));
	}

	/// <summary>
	/// <b>And it is gone by thirteen seconds whatever happens.</b> Retail's second battle timer.
	/// </summary>
	[Fact]
	public void AndItIsGoneByThirteenSeconds()
	{
		Assert.Equal(13_000L, ShulackGuidedBombAI.BackstopMillis);
		Assert.Equal(3000L, ShulackGuidedBombAI.FuseMillis);
	}

	/// <summary>
	/// <b>Popuchin takes his bombs with him when he resets.</b>
	/// </summary>
	/// <remarks>
	/// Retail's <c>on_leave_attack_state</c> is <c>control_door</c> and <c>despawn SPAWN_ID_1</c>. That
	/// despawn was missing, and it did not show while the bomb carried a ten-second self-delete it should
	/// never have had. Moving the bomb's clock onto retail's aggro timer is what makes this the only
	/// thing that clears an untouched bomb.
	/// </remarks>
	[Fact]
	public void PopuchinTakesHisBombsWithHimWhenHeResets()
	{
		using BossAiHarness harness = NewHarness();
		Npc popuchin = harness.Spawn(217373, 300f, 300f, 200f);
		harness.Spawn(PopuchinAI.GuidedBomb, 302f, 300f, 200f);
		harness.Spawn(PopuchinAI.ScatteredBomb, 304f, 300f, 200f);

		popuchin.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.BACK_HOME);

		Assert.Equal(0, Bombs(harness));
		Assert.Equal(0, harness.LiveNpcs().Count(n => n.GetNpcId() == PopuchinAI.ScatteredBomb));
	}
}
