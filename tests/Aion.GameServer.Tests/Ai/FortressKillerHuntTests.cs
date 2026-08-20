using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;

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
}
