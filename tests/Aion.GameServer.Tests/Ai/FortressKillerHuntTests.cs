using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for what a fortress killer does besides answering messages (see docs/retail-ai-fidelity.md).
/// </summary>
/// <remarks>
/// <b>The killers are not uniform, and the pins are mostly about that.</b> A class with three constants
/// in it would pass a test written against the artifact killers and be wrong for the nineteen Advance
/// killers and wrong again for the village one — so every pin here names the npc and the number, and the
/// last one asserts the table holds more than one shape.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class FortressKillerHuntTests
{
	private const int Reshanta = 400010000;

	/// <summary><c>LDF4_Advance_SS_A_Killer_Dr_01</c> — hunts a garrison chief on sight, and does not walk.</summary>
	/// <remarks>
	/// The first draft of this pin used 234164, which is <em>a garrison chief</em> on
	/// <c>ai="aggressive"</c> — the quarry, not the hunter. It failed for that reason and not for the
	/// mechanic, which is why the ids here are taken from the extractor's own output rather than guessed
	/// from a pattern name.
	/// </remarks>
	private const int AdvanceKiller = 235543;

	/// <summary><c>AB1_DrGuard_Artifact_Killer</c> — walks its route, and waits to be called.</summary>
	private const int ArtifactKiller = 251160;

	/// <summary>
	/// <c>race="GCHIEF_LIGHT"</c>, <c>tribe="LDF4_ADVANCE_LGUARD"</c> — what an Advance killer came for.
	/// </summary>
	/// <remarks>
	/// <b>The race is retail's condition and the tribe is ours.</b> Retail's rung tests only
	/// <c>is_race gchief_light</c>, but <c>AddHate</c> here applies the aggro list's own tribe check —
	/// which <see cref="FortressKillerAI"/>'s remark says is deliberate, the condition being carried
	/// rather than re-implemented. So a Kaldor chief (<c>LDF5_V_CHIEF_L</c>) takes no hate from an
	/// <em>Advance</em> killer however right its race is, and the first draft of this pin used one and
	/// read zero. The quarry has to be a chief this killer is actually at war with.
	/// </remarks>
	private const int ElyosGarrisonChief = 234197;

	/// <summary>
	/// <c>LDF4_Advance_PvP_Guard_Li_Kn</c> — <c>race="ELYOS"</c>, same war, <b>not</b> a chief. An enemy
	/// the killer will fight and must not focus.
	/// </summary>
	private const int OrdinaryAdvanceGuard = 233957;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Reshanta).WithWorldSize(4096)
			.WithAi(typeof(FortressKillerAI), typeof(AbyssGuardSimpleAI), typeof(BaseProtectorAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>
	/// <b>An Advance killer goes for a garrison chief the moment it sees one.</b> A million hate, which
	/// is retail's way of saying "this is the only thing here that matters" — and it is the largest
	/// killer family in the game, nineteen npcs, none of which did anything but stand still.
	/// </summary>
	[Fact]
	public void AnAdvanceKillerGoesForAGarrisonChiefTheMomentItSeesOne()
	{
		using BossAiHarness harness = NewHarness();
		Npc killer = harness.Spawn(AdvanceKiller, 300f, 300f, 200f);
		Npc chief = harness.Spawn(ElyosGarrisonChief, 305f, 300f, 200f);
		// Introducing them IS the sight event: MakeMutuallyKnown fills the known lists and the engine
		// raises CreatureSee off that. An earlier draft also raised the event by hand and read exactly
		// twice the hate, which is the tell for a doubled trigger rather than a wrong number.
		BossAiHarness.MakeMutuallyKnown(killer, chief);

		// Retail's own literal, NOT FortressKillers.ByNpc[...].SightHate. Comparing against the table
		// under test makes the assertion self-referential: zeroing the table turns this into
		// Equal(0, 0) and the pin passes on a killer that no longer hunts. That mutation survived once
		// here, and the same shape was caught earlier in this work on the gravity bomb's reach.
		Assert.Equal(1_000_000, killer.GetAggroList().GetHate(chief));
	}

	/// <summary>
	/// <b>And an artifact killer does not.</b> It waits to be called by a protector, which is the whole
	/// difference between the two families and would be flattened by a single class-wide rung.
	/// </summary>
	[Fact]
	public void AndAnArtifactKillerDoesNot()
	{
		using BossAiHarness harness = NewHarness();
		Npc killer = harness.Spawn(ArtifactKiller, 300f, 300f, 200f);
		Npc chief = harness.Spawn(ElyosGarrisonChief, 305f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(killer, chief);

		Assert.Equal(0, killer.GetAggroList().GetHate(chief));
	}

	/// <summary>
	/// <b>The wake call still goes out, and it names the killer.</b> Retail's rung carries
	/// <c>param_obj=OBJI_SELF</c>; this port used to broadcast it with no parameter at all, which the
	/// protectors did not notice because they read the sender — but a message that names nobody cannot be
	/// answered by anything that reads the parameter, and several things in this dump do.
	/// </summary>
	[Fact]
	public void TheWakeCallStillGoesOutAndNamesTheKiller()
	{
		using BossAiHarness harness = NewHarness();
		Npc killer = harness.Spawn(ArtifactKiller, 300f, 300f, 200f);
		var seen = new List<int>();

		using (NpcMessageBusProbe probe = NpcMessageBusProbe.Watch(seen))
			killer.GetAi().OnGeneralEvent(AiEventType.Spawned);

		Assert.Contains(FortressKillerAI.KillerAwake, seen);
	}

	/// <summary>
	/// <b>The table holds more than one shape,</b> which is the reason it is a table. Nineteen killers
	/// hunt on sight and the rest do not; seventeen walk and the rest stand where they spawned.
	/// </summary>
	[Fact]
	public void TheTableHoldsMoreThanOneShape()
	{
		var hunters = FortressKillers.ByNpc.Values.Count(k => k.SightHate > 0);
		var walkers = FortressKillers.ByNpc.Values.Count(k => k.Walks);

		Assert.InRange(hunters, 1, FortressKillers.ByNpc.Count - 1);
		Assert.InRange(walkers, 1, FortressKillers.ByNpc.Count - 1);
		Assert.True(FortressKillers.ByNpc.Values.Select(k => k.WakeRange).Distinct().Count() > 1,
			"every killer shouts at the same range, so the range did not need reading");
	}

	/// <summary>
	/// <b>A killer already fighting keeps choosing the chief.</b> Retail's one translatable battle-timer
	/// rung: hate on a current target that <em>is</em> a garrison chief — 900,000 every five seconds for
	/// the Advance killers — so whoever else joins the fight does not pull them off it.
	/// </summary>
	/// <remarks>
	/// <b>The previous entry said this could not be pinned, and that was wrong.</b> The claim was that
	/// the harness cannot hold two NPCs in a fight; what actually happens is that the killer dies inside
	/// the first tick, because it loads with 140 max HP against the chief's 32,215.
	/// <see cref="BossAiHarness.HoldFight"/> is the fix and carries the detail.
	/// </remarks>
	[Fact]
	public void AKillerAlreadyFightingKeepsChoosingTheChief()
	{
		using BossAiHarness harness = NewHarness();
		Npc killer = harness.Spawn(AdvanceKiller, 300f, 300f, 200f);
		Npc chief = harness.Spawn(ElyosGarrisonChief, 303f, 300f, 200f);
		harness.Engage(killer, chief);
		int before = killer.GetAggroList().GetHate(chief);

		for (int second = 0; second < 12; second++)
		{
			BossAiHarness.HoldFight(killer, chief);
			harness.Clock.Advance(System.TimeSpan.FromSeconds(1));
		}

		Assert.True(killer.GetAggroList().GetHate(chief) >= before + 900_000,
			"the focus rung never landed, so the killer can be pulled off the chief");
	}

	/// <summary>
	/// <b>An artifact killer focuses too, and it hunts nobody on sight.</b> That pair is why the focus
	/// race list is a separate field: <c>Hunted</c> is empty for these and <c>Focused</c> is not, so a
	/// rung reading the sight list would test an empty array and never fire.
	/// </summary>
	/// <remarks>
	/// <b>This pin could not be written until the health data was repaired.</b> The artifact killer
	/// carried 140-odd HP against its quarry's thirty thousand and died inside the first tick whatever
	/// the harness did; it has retail's 3,377,604 now. The earlier entry blamed the harness, then blamed
	/// the tribe relation, and both were explanations fitted to a symptom.
	/// </remarks>
	[Fact]
	public void AnArtifactKillerFocusesTooAndHuntsNobodyOnSight()
	{
		using BossAiHarness harness = NewHarness();
		Npc killer = harness.Spawn(ArtifactKiller, 300f, 300f, 200f);
		Npc chief = harness.Spawn(ElyosGarrisonChief, 303f, 300f, 200f);
		harness.Engage(killer, chief);
		int before = killer.GetAggroList().GetHate(chief);

		// Retail puts its first focus rung at eight seconds; fifteen holds it comfortably.
		for (int second = 0; second < 15; second++)
		{
			BossAiHarness.HoldFight(killer, chief);
			harness.Clock.Advance(System.TimeSpan.FromSeconds(1));
		}

		Assert.True(killer.GetAggroList().GetHate(chief) >= before + 200_000,
			"the artifact killer's focus rung never landed");
	}

	/// <summary>
	/// <b>And an ordinary enemy does not get it.</b> Retail guards the rung on the target's race; without
	/// that the killer would pile nine hundred thousand onto whatever it happened to be hitting.
	/// </summary>
	[Fact]
	public void AndAnOrdinaryEnemyDoesNotGetIt()
	{
		using BossAiHarness harness = NewHarness();
		Npc killer = harness.Spawn(AdvanceKiller, 300f, 300f, 200f);
		Npc bystander = harness.Spawn(OrdinaryAdvanceGuard, 303f, 300f, 200f);
		harness.Engage(killer, bystander);
		int before = killer.GetAggroList().GetHate(bystander);

		for (int second = 0; second < 12; second++)
		{
			BossAiHarness.HoldFight(killer, bystander);
			harness.Clock.Advance(System.TimeSpan.FromSeconds(1));
		}

		Assert.True(killer.GetAggroList().GetHate(bystander) < before + 900_000,
			"a guard that is not a chief took the chief's focus hate");
	}

	/// <summary>
	/// <b>The focus rung is read out of the ladder, and it is not the sight rung.</b> Retail's one
	/// translatable battle-timer branch adds hate to a <em>current target</em> that is a garrison chief —
	/// 200,000 every 28 seconds for the artifact killers, 900,000 every 5 for the Advance ones — so
	/// whoever else joins the fight does not pull the killer off.
	/// </summary>
	/// <remarks>
	/// This pins the <em>numbers</em>; the two pins above exercise the rung itself.
	/// <para>
	/// It is still worth having beside them, because it is the part that was wrong twice: the cadences,
	/// and the focus race list being separate from the sight one.
	/// <para>
	/// <b>One mutation still survives: swapping the rung's race list back to <c>Hunted</c>.</b> It is a
	/// no-op for the Advance killers the behaviour pins drive, whose two lists are the same three races,
	/// and only the artifact killers tell them apart — they focus and hunt nobody, so <c>Hunted</c> is
	/// empty for them. A pin driving an artifact killer through its eight-second focus rung was written
	/// and did not fire for a reason not yet isolated, so it was removed rather than left red. That is
	/// the gap, stated so it is not mistaken for coverage.
	/// </para> Sharing a field made the rung test an
	/// empty array and never fire — a mechanic wired and silently inert, which a behaviour pin catches
	/// only once the behaviour can be driven at all.
	/// </para>
	/// </remarks>
	[Fact]
	public void TheFocusRungIsReadOutOfTheLadderAndIsNotTheSightRung()
	{
		FortressKillers.Killer artifact = FortressKillers.ByNpc[ArtifactKiller];
		FortressKillers.Killer advance = FortressKillers.ByNpc[AdvanceKiller];

		// The artifact killer hunts nobody on sight and still focuses: two lists, not one.
		Assert.Empty(artifact.Hunted);
		Assert.NotEmpty(artifact.Focused);
		Assert.Equal(200_000, artifact.FocusHate);
		Assert.Equal(28_000, artifact.FocusPeriod);

		// The Advance killer's ladder has no unguarded rung, so its period comes off the guarded one.
		Assert.Equal(900_000, advance.FocusHate);
		Assert.Equal(5_000, advance.FocusPeriod);

		Assert.NotEqual(artifact.FocusPeriod, advance.FocusPeriod);
	}
}
