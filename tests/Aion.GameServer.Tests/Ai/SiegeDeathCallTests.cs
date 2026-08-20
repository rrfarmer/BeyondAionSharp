using System;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for retail's <c>30003</c>, the protector-down order (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// <b>The defect was a message being sent too often, which is the hardest kind to pin.</b>
/// <see cref="AbstractSiegeProtectorAI"/> broadcast 30003 from every death it handled — 1,219 npcs —
/// where only 342 of them carry it in retail. Nothing failed and nothing looked wrong: a protector that
/// announces its death behaves plausibly, and <c>FortressKillerAI</c> answering by standing down looks
/// like the mechanic working rather than a fortress killer being called off a fight retail leaves
/// running.
/// <para>
/// So the pin that matters here is the <em>negative</em> one, and it is written first.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class SiegeDeathCallTests
{
	private const int Reshanta = 400010000;

	/// <summary><c>Ab1_1401_Boss_Li_3</c>, on <c>AB1_LDGuard_Artifact</c> — its pattern has the rung.</summary>
	private const int Announces = 251469;

	/// <summary>
	/// <c>Ab1_1401_Boss_Dr_1</c>, on <c>AB1_DrGuard_Artifact</c> — the balaur guard, whose pattern is the
	/// same one with the death broadcast taken out.
	/// </summary>
	private const int DiesQuietly = 251450;

	/// <summary><c>LDF5_Village_chief01_L</c> — on <c>simple_abyssguard</c>, and its pattern has the rung.</summary>
	private const int ElyosVillageChief = 277069;

	/// <summary>An abyss guard on the same class whose pattern has no death rung.</summary>
	private const int PlainAbyssGuard = 207556;

	/// <summary><c>LDF5_chief_v01_L</c> — a base protector whose pattern carries the death rung.</summary>
	private const int ChiefWhoAnnounces = 231630;

	/// <summary><c>LF2_1_Lehpar_Chief</c> — a base protector whose pattern does not.</summary>
	private const int ChiefWhoDoesNot = 231549;

	/// <summary>
	/// Kills a protector through its AI and returns every message that left it.
	/// </summary>
	/// <remarks>
	/// <b>The cast is expected to throw and that is the point being relied on.</b>
	/// <see cref="AbstractSiegeProtectorAI.HandleDied"/> broadcasts, then calls <c>StopSiege</c>, which
	/// casts the owner to a <c>SiegeNpc</c> — and the harness spawns a plain <c>Npc</c>, because a
	/// <c>SiegeNpc</c> needs a siege spawn template and a live <c>SiegeService</c> that this suite has no
	/// business standing up.
	/// <para>
	/// Swallowing it here is safe <em>because of the ordering the class documents</em>: retail puts the
	/// broadcast first in the rung and so does this port, so by the time the cast fails the message has
	/// already been sent or already been skipped. A version that moved the broadcast after
	/// <c>StopSiege</c> would make these pins fail rather than silently pass, which is the right way
	/// round.
	/// </para>
	/// </remarks>
	private static List<int> MessagesOnDeathOf(Npc protector)
	{
		var seen = new List<int>();
		using (NpcMessageBusProbe probe = NpcMessageBusProbe.Watch(seen))
		{
			try
			{
				protector.GetAi().OnGeneralEvent(AiEventType.Died);
			}
			catch (InvalidCastException)
			{
				// The siege services are not present; see the remark above.
			}
		}

		return seen;
	}

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Reshanta).WithWorldSize(4096)
			.WithAi(typeof(ArtifactProtectorAI), typeof(FortressProtectorNpcAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>
	/// <b>A protector whose pattern has no death rung dies without a word.</b> Retail's balaur artifact
	/// guard carries the same message answers and the same death spawn as its Elyos twin and simply omits
	/// the broadcast; before this, it sent it anyway.
	/// </summary>
	[Fact]
	public void AProtectorWhosePatternHasNoDeathRungDiesWithoutAWord()
	{
		using BossAiHarness harness = NewHarness();
		Npc protector = harness.Spawn(DiesQuietly, 300f, 300f, 200f);

		List<int> seen = MessagesOnDeathOf(protector);

		Assert.DoesNotContain(AbstractSiegeProtectorAI.ProtectorDown, seen);
	}

	/// <summary>
	/// <b>And one whose pattern has it still announces.</b> Without this the gate could be a deletion
	/// rather than a correction, and the fortress killers would never be told anything.
	/// </summary>
	[Fact]
	public void AndOneWhosePatternHasItStillAnnounces()
	{
		using BossAiHarness harness = NewHarness();
		Npc protector = harness.Spawn(Announces, 300f, 300f, 200f);

		List<int> seen = MessagesOnDeathOf(protector);

		Assert.Contains(AbstractSiegeProtectorAI.ProtectorDown, seen);
	}

	/// <summary>
	/// <b>The table is a minority of what the class is bound to,</b> which is the whole finding stated as
	/// arithmetic. If a future extractor change quietly widened it back to everything, the two pins above
	/// would both still pass.
	/// </summary>
	[Fact]
	public void TheTableIsAMinorityOfWhatTheClassIsBoundTo()
	{
		Assert.InRange(SiegeDeathCalls.ByNpc.Count, 1, 900);
		Assert.Contains(Announces, SiegeDeathCalls.ByNpc.Keys);
		Assert.DoesNotContain(DiesQuietly, SiegeDeathCalls.ByNpc.Keys);
	}

	/// <summary>
	/// <b>The range comes from the pattern, not from a constant.</b> Retail writes fifty everywhere it
	/// appears, and this pin exists so that stays a measured fact rather than an assumption.
	/// </summary>
	[Fact]
	public void TheRangeComesFromThePattern()
	{
		Assert.All(SiegeDeathCalls.ByNpc.Values, reach => Assert.Equal(50f, reach));
	}

	/// <summary>
	/// <b>A village chief announces its death too, and it is not a siege protector.</b> Retail's
	/// <c>LDF5_Village_chiefNN</c> broadcasts 30003 from <c>on_killed_by_user</c> and
	/// <c>on_killed_by_npc</c>, and this port runs those npcs on <c>simple_abyssguard</c> — a class with
	/// no death handler at all until now.
	/// </summary>
	/// <remarks>
	/// The gate and this are opposite corrections and were deliberately committed apart: one removed a
	/// broadcast from 877 npcs, this adds it to 57 and to 69 base protectors. The table is the same in
	/// both directions, which is the argument for it being a table.
	/// </remarks>
	[Fact]
	public void AVillageChiefAnnouncesItsDeathToo()
	{
		using BossAiHarness harness = BossAiHarness.For(600090000).WithWorldSize(4096)
			.WithAi(typeof(AbyssGuardSimpleAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI)).Build();
		Npc chief = harness.Spawn(ElyosVillageChief, 300f, 300f, 200f);
		var seen = new List<int>();

		using (NpcMessageBusProbe probe = NpcMessageBusProbe.Watch(seen))
			chief.GetAi().OnGeneralEvent(AiEventType.Died);

		Assert.Contains(AbstractSiegeProtectorAI.ProtectorDown, seen);
	}

	/// <summary>
	/// <b>And an abyss guard that is not a chief still dies quietly,</b> which is what makes the line in
	/// that class a table lookup rather than a broadcast.
	/// </summary>
	[Fact]
	public void AndAnAbyssGuardThatIsNotAChiefStillDiesQuietly()
	{
		using BossAiHarness harness = BossAiHarness.For(600090000).WithWorldSize(4096)
			.WithAi(typeof(AbyssGuardSimpleAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI)).Build();
		Npc guard = harness.Spawn(PlainAbyssGuard, 300f, 300f, 200f);
		var seen = new List<int>();

		using (NpcMessageBusProbe probe = NpcMessageBusProbe.Watch(seen))
			guard.GetAi().OnGeneralEvent(AiEventType.Died);

		Assert.DoesNotContain(AbstractSiegeProtectorAI.ProtectorDown, seen);
	}

	/// <summary>
	/// <b>A base protector announces too, and the same class holds ones that do not.</b> 69 of the base
	/// protectors carry the rung and the rest do not, which is the reason this is a table and not a line
	/// in a death handler.
	/// </summary>
	/// <remarks>
	/// <b>This pin calls the helper directly rather than killing the npc, and that is not laziness.</b>
	/// The first version raised <c>Died</c> on a <see cref="BaseProtectorAI"/>, which reaches
	/// <c>BaseService</c> — whose static initialiser cannot run without the server's data and throws.
	/// **A failed type initialiser is cached for the life of the process**, so every later test that
	/// touches <c>BaseService</c> inherits the corpse: two `GameServerBootstrapTests` began failing, and
	/// they passed in isolation, which is the signature of exactly this.
	/// <para>
	/// <b>So <see cref="BaseProtectorAI"/>'s wiring is, honestly, not pinned.</b> Deleting the
	/// <c>Announce</c> call from its <c>HandleDied</c> — or moving it below the capture — leaves this
	/// suite green; both were verified caught while the death-path version of this pin existed, and both
	/// survive now. What is pinned here is the decision that wiring depends on: which npcs the table says
	/// should announce, and that a base protector is on both sides of it.
	/// <para>
	/// The two siege-protector pins above <em>do</em> exercise the death path, because that class reaches
	/// its service through a cast that fails cleanly instead of through a static initialiser that
	/// poisons the type. Closing the gap for base protectors needs a <c>BaseService</c> the suite can
	/// stand up, which is a larger job than this change.
	/// </para>
	/// </remarks>
	[Theory]
	[InlineData(ChiefWhoAnnounces, true)]
	[InlineData(ChiefWhoDoesNot, false)]
	public void ABaseProtectorAnnouncesOnlyWhenItsPatternSaysSo(int npcId, bool expected)
	{
		using BossAiHarness harness = BossAiHarness.For(600090000).WithWorldSize(4096)
			.WithAi(typeof(BaseProtectorAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI)).Build();
		Npc chief = harness.Spawn(npcId, 300f, 300f, 200f);
		var seen = new List<int>();

		using (NpcMessageBusProbe probe = NpcMessageBusProbe.Watch(seen))
			SiegeDeathCalls.Announce(chief);

		Assert.Equal(expected, seen.Contains(AbstractSiegeProtectorAI.ProtectorDown));
	}

}
