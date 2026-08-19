using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Padmarashka's eggs, whose two hatch timers were both guesses.
/// </summary>
/// <remarks>
/// Retail's <c>IDDramata_Egg_01</c> and <c>IDDramata_H_Egg_01</c> both set a sixty-second idle timer on
/// waking and hatch by despawning when it turns. Java carried <c>TODO: Need right value</c> on both and
/// had the huge egg at a hundred and twenty seconds — twice the window a raid gets to kill it.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class PadmarashkaEggAiTests
{
	private const int PadmarashkasCave = 320150000;

	private const int SmallEgg = 282613;
	private const int HugeEgg = 282614;

	/// <summary>What each egg hatches into.</summary>
	private const int NeonateDrakan = 282616;
	private const int VeteranDrakan = 282620;

	/// <summary>The hatchers that answer a dying egg's broadcast.</summary>
	private const int HatcherFire = 282715;
	private const int HatcherWind = 282716;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(PadmarashkasCave).WithWorldSize(2048)
			.WithAi(typeof(PadmarashkaEggAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>
	/// <b>Both eggs hatch on the same sixty-second clock.</b>
	/// </summary>
	/// <remarks>
	/// The huge egg was on a hundred and twenty. Retail gives both <c>set_idle_timer delay=60000</c>.
	/// </remarks>
	[Theory]
	[InlineData(SmallEgg, NeonateDrakan)]
	[InlineData(HugeEgg, VeteranDrakan)]
	public void AnEggHatchesAtSixtySeconds(int egg, int hatchling)
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(egg, 580f, 155f, 66f);

		harness.Clock.Advance(TimeSpan.FromSeconds(59));
		Assert.Equal(0, Count(harness, hatchling));
		Assert.Equal(1, Count(harness, egg));

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.Equal(1, Count(harness, hatchling));

		// And the egg is gone: retail hatches by despawning, so the two never stand together.
		Assert.Equal(0, Count(harness, egg));
	}

	/// <summary>
	/// <b>An egg killed before it turns hatches nothing.</b>
	/// </summary>
	/// <remarks>
	/// Retail says this structurally: the hatch is in <c>on_despawn</c> and shares one test-and-set flag
	/// var with <c>on_die</c>, so whichever fires first locks the other out.
	/// </remarks>
	[Fact]
	public void AnEggKilledFirstHatchesNothing()
	{
		using BossAiHarness harness = NewHarness();
		Npc egg = harness.Spawn(SmallEgg, 580f, 155f, 66f);

		egg.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.Died);
		harness.Clock.Advance(TimeSpan.FromSeconds(90));

		Assert.Equal(0, Count(harness, NeonateDrakan));
	}

	/// <summary>
	/// <b>A dying egg reaches every hatcher within fifty metres</b>, not only the one it spawned.
	/// </summary>
	/// <remarks>
	/// Retail broadcasts message 105 at fifty metres and each hatcher in earshot buffs itself. This class
	/// buffed a single remembered protector, so an egg killed before it was ever attacked — which is the
	/// usual way an egg dies — buffed nothing at all.
	/// <para>
	/// Asserted through the buff's own abnormal effect rather than through the spawn, because the
	/// hatchers are placed by the instance and not by the egg.
	/// </para>
	/// </remarks>
	[Fact]
	public void ADyingEggBuffsEveryHatcherInEarshot()
	{
		using BossAiHarness harness = NewHarness();
		Npc egg = harness.Spawn(SmallEgg, 580f, 155f, 66f);

		// Two hatchers close by and one well outside retail's fifty metres.
		Npc near1 = harness.Spawn(HatcherFire, 585f, 155f, 66f);
		Npc near2 = harness.Spawn(HatcherWind, 590f, 160f, 66f);
		Npc far = harness.Spawn(HatcherFire, 700f, 155f, 66f);

		egg.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.Died);

		Assert.True(near1.GetEffectController().HasAbnormalEffect(20176),
			"the hatcher beside the egg was not buffed");
		Assert.True(near2.GetEffectController().HasAbnormalEffect(20176),
			"the second hatcher in earshot was not buffed");
		Assert.False(far.GetEffectController().HasAbnormalEffect(20176),
			"a hatcher a hundred and twenty metres away answered a fifty-metre broadcast");
	}
}
