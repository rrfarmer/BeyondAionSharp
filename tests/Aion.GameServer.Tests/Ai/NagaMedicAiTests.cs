using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="NagaMedicAI"/> — retail <c>Naga_PeA1</c> through <c>_PeA4</c>
/// (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// <b>The subject is which creature appears, and when.</b> These npcs already summoned something under
/// <see cref="DrakanMedicAI"/> — a drakan servant, on a three percent roll per swing — so a pin that only
/// asked "does a servant appear" would have passed before this class existed and after it. Every pin
/// here names the npc id it expects, and the tier pins exist because the id is the whole defect.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class NagaMedicAiTests
{
	private const int Reshanta = 400010000;

	/// <summary><c>Naga_PeA2</c> — an indratu priest, the largest of the four tiers.</summary>
	private const int MedicLv46 = 214014;

	/// <summary><c>Naga_PeA1</c> — a bakarma fleshmender.</summary>
	private const int MedicLv44 = 213676;

	/// <summary><c>Naga_PeA4</c>, the single-npc tier that was missed on the first pass.</summary>
	private const int MedicLv50 = 281300;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Reshanta).WithWorldSize(4096)
			.WithAi(typeof(NagaMedicAI), typeof(ServantNpcAI), typeof(AggressiveNpcAI),
				typeof(AggressiveNoLootNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static void Hold(Npc medic, Player player)
	{
		BossAiHarness.Rehate(medic, player);
		BossAiHarness.KeepAlive(player);
	}

	/// <summary>
	/// Runs the ten seconds a medic needs to open, at full health, before a pin wounds it.
	/// </summary>
	/// <remarks>
	/// <b>Timer 1 has exactly one origin,</b> and every servant rung hangs off it. Retail's opening rung
	/// takes timer 0 at seven seconds, requires <c>is_hp_in_boundary 86..100</c> — so 87 to 99 — and is
	/// the only thing in the pattern that arms timer 1; after that timer 1 rearms itself. A medic dropped
	/// below eighty-seven <em>before</em> its first tick therefore never starts its chain at all and does
	/// nothing for the rest of the fight.
	/// <para>
	/// The first version of these pins set health to sixty immediately after <c>Engage</c> and every one
	/// of them failed with no servant. That was the measurement, not the mechanic: in a real fight the
	/// medic is untouched when the seven-second tick lands. Same shape as the leader-4 band that excludes
	/// exactly a hundred.
	/// </para>
	/// </remarks>
	private static void Open(BossAiHarness harness, Npc medic, Player player)
	{
		harness.Watch(10, () =>
		{
			Hold(medic, player);
			BossAiHarness.SetHpPercent(medic, 95);
		});
	}

	/// <summary>
	/// <b>Falling below eighty-five brings a naga servant.</b> Retail's one-shot, guarded by
	/// <c>ALPHA_2</c> — and the creature is the point: the class this replaces called a <em>drakan</em>
	/// servant here.
	/// </summary>
	[Fact]
	public void FallingBelowEightyFiveBringsANagaServant()
	{
		using BossAiHarness harness = NewHarness();
		Npc medic = harness.Spawn(MedicLv46, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(300f, 290f, 200f, race: Race.ASMODIANS);
		harness.Engage(medic, player);
		Open(harness, medic, player);
		BossAiHarness.SetHpPercent(medic, 60);

		BossAiHarness.Watched seen = harness.Watch(30, () => Hold(medic, player),
			NagaMedicAI.ServantLv46);

		Assert.True(seen.Total >= 1, "no naga servant appeared below eighty-five");
	}

	/// <summary><b>And never the drakan servant the old class called.</b></summary>
	[Fact]
	public void AndNeverTheDrakanServantTheOldClassCalled()
	{
		using BossAiHarness harness = NewHarness();
		Npc medic = harness.Spawn(MedicLv46, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(300f, 290f, 200f, race: Race.ASMODIANS);
		harness.Engage(medic, player);
		Open(harness, medic, player);
		BossAiHarness.SetHpPercent(medic, 60);

		BossAiHarness.Watched seen = harness.Watch(30, () => Hold(medic, player), 281621, 281839);

		Assert.Equal(0, seen.Total);
	}

	/// <summary>
	/// <b>An untouched medic summons nothing.</b> Retail hangs the servant off a health band, not off
	/// contact — the replaced class rolled on every swing, so a full-health medic could summon on its
	/// first hit.
	/// </summary>
	[Fact]
	public void AnUntouchedMedicSummonsNothing()
	{
		using BossAiHarness harness = NewHarness();
		Npc medic = harness.Spawn(MedicLv46, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(300f, 290f, 200f, race: Race.ASMODIANS);
		harness.Engage(medic, player);
		Open(harness, medic, player);

		BossAiHarness.Watched seen = harness.Watch(30, () =>
		{
			Hold(medic, player);
			BossAiHarness.SetHpPercent(medic, 95);
		}, NagaMedicAI.ServantLv46);

		Assert.Equal(0, seen.Total);
	}

	/// <summary>
	/// <b>It is one servant, not one a minute.</b> The flag is spent by the first crossing, so a medic
	/// held below eighty-five for a whole fight still gets exactly one — unless somebody asks, which is
	/// the pin below.
	/// </summary>
	[Fact]
	public void ItIsOneServantNotOneAMinute()
	{
		using BossAiHarness harness = NewHarness();
		Npc medic = harness.Spawn(MedicLv46, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(300f, 290f, 200f, race: Race.ASMODIANS);
		harness.Engage(medic, player);
		Open(harness, medic, player);
		BossAiHarness.SetHpPercent(medic, 60);

		BossAiHarness.Watched seen = harness.Watch(120, () => Hold(medic, player),
			NagaMedicAI.ServantLv46);

		Assert.Equal(1, seen.Total);
	}

	/// <summary>
	/// <b>Message 3306 is the only thing that brings more.</b> Timer 2 is armed by that message and by
	/// nothing else in the pattern, so a medic nobody signals summons once all fight and a medic that is
	/// signalled keeps going every fifteen seconds.
	/// </summary>
	[Fact]
	public void MessageThreeThreeZeroSixIsTheOnlyThingThatBringsMore()
	{
		using BossAiHarness harness = NewHarness();
		Npc medic = harness.Spawn(MedicLv46, 300f, 300f, 200f);
		Npc caller = harness.Spawn(MedicLv44, 305f, 300f, 200f);
		Player player = harness.SpawnPlayer(300f, 290f, 200f, race: Race.ASMODIANS);
		BossAiHarness.MakeMutuallyKnown(medic, caller);
		harness.Engage(medic, player);
		Open(harness, medic, player);
		BossAiHarness.SetHpPercent(medic, 60);
		NpcMessageBus.Broadcast(caller, NagaMedicAI.SendMeHelp, null, 50f);

		BossAiHarness.Watched seen = harness.Watch(120, () => Hold(medic, player),
			NagaMedicAI.ServantLv46);

		Assert.True(seen.Total >= 2,
			$"a signalled medic produced {seen.Total} servants, so timer 2 is not running");
	}

	/// <summary>
	/// <b>Each tier calls its own servant.</b> Retail level-matches them, and a single class covering
	/// four patterns is exactly where that gets flattened to one id.
	/// </summary>
	[Theory]
	[InlineData(MedicLv44, NagaMedicAI.ServantLv44)]
	[InlineData(MedicLv46, NagaMedicAI.ServantLv46)]
	[InlineData(MedicLv50, NagaMedicAI.ServantLv50)]
	public void EachTierCallsItsOwnServant(int medicId, int servantId)
	{
		using BossAiHarness harness = NewHarness();
		Npc medic = harness.Spawn(medicId, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(300f, 290f, 200f, race: Race.ASMODIANS);
		harness.Engage(medic, player);
		Open(harness, medic, player);
		BossAiHarness.SetHpPercent(medic, 60);

		BossAiHarness.Watched seen = harness.Watch(30, () => Hold(medic, player), servantId);

		Assert.True(seen.Total >= 1, $"medic {medicId} did not call servant {servantId}");
	}

	/// <summary>
	/// <b>Leaving the fight takes the servants with it.</b> Retail's <c>on_leave_attack_state</c>
	/// despawns <c>SPAWN_ID_1</c>, so a medic that resets does not leave its help standing in the field.
	/// </summary>
	[Fact]
	public void LeavingTheFightTakesTheServantsWithIt()
	{
		using BossAiHarness harness = NewHarness();
		Npc medic = harness.Spawn(MedicLv46, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(300f, 290f, 200f, race: Race.ASMODIANS);
		harness.Engage(medic, player);
		Open(harness, medic, player);
		BossAiHarness.SetHpPercent(medic, 60);
		harness.Watch(30, () => Hold(medic, player), NagaMedicAI.ServantLv46);
		Assert.Contains(harness.LiveNpcs(), n => n.GetNpcId() == NagaMedicAI.ServantLv46);

		medic.GetAi().OnGeneralEvent(AiEventType.BackHome);

		Assert.DoesNotContain(harness.LiveNpcs(), n => n.GetNpcId() == NagaMedicAI.ServantLv46);
	}
}
