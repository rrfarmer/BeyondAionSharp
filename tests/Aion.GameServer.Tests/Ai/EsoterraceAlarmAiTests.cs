using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for the Esoterrace surkana feeder and the lab that answers it, translated from retail patterns
/// <c>IDF4Re_FOBJ_1</c> and <c>IDF4Re_Drana_*</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// <b>The feeder has fifty maximum HP</b>, so only even percentages exist on it and
/// <c>SetExactPercent</c> throws its own assertion on anything else. The thresholds below are chosen to
/// land inside each band and to be expressible: 76, 56, 36 and 16.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class EsoterraceAlarmAiTests
{
	private const int Esoterrace = 300250000;

	private const int SurkanaFeeder = 282291;      // IDF4Re_FOBJ_1
	private const int VillageFighter = 217182;     // IDF4Re_Drana_Drakan_Vil_Fi
	private const int LabWizard = 217201;          // IDF4Re_Drana_Drakanlab_Wi

	/// <summary>One point inside each of the four thresholds, in the order a raid meets them.</summary>
	private static readonly int[] InsideEachBand = [76, 56, 36, 16];

	private static BossAiHarness Harness() =>
		BossAiHarness.For(Esoterrace).WithWorldSize(2048)
			.WithAi(typeof(SurkanaFeederAI), typeof(EsoterraceDrakanAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static (BossAiHarness, Npc, Npc, Player) Lab()
	{
		BossAiHarness harness = Harness();
		Npc feeder = harness.SpawnWithAi(SurkanaFeeder, "surkana_feeder", 300f, 300f, 200f);
		Npc drakan = harness.SpawnWithAi(VillageFighter, "esoterrace_drakan", 310f, 300f, 200f);
		Player raider = harness.SpawnPlayer(302f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(feeder, drakan);
		BossAiHarness.MakeMutuallyKnown(drakan, raider);
		// Engage rather than a bare Attack event: a real blow lands damage, which puts the feeder on its
		// own aggro list and into FIGHT. A bare event does neither, and an NPC that never enters combat
		// is sent home after every blow with its flags cleared -- a harness artifact that reads exactly
		// like the ladder misbehaving. See docs/retail-ai-fidelity.md.
		harness.Engage(feeder, raider);
		return (harness, feeder, drakan, raider);
	}

	/// <summary>
	/// <b>The very first blow raises the lab</b>, because the lowest band carries no health guard at
	/// all — the feeder is a machine, and touching it is the alarm.
	/// </summary>
	[Fact]
	public void TheFirstBlowRaisesTheLab()
	{
		var (harness, feeder, drakan, raider) = Lab();
		using BossAiHarness _h = harness;

		// Engage lands a genuine blow, so the bare band has already fired.
		Assert.Equal(EsoterraceAlarm.Notice, drakan.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>And at full health it raises it exactly once.</b> The bare band has a flag like every other,
	/// so a raider beating the feeder without moving its health bar gets one answer, not one per blow.
	/// </summary>
	/// <remarks>
	/// <b>This is the pin the whole encounter turned on, and it turned the wrong way twice.</b> Driven
	/// with bare <c>Attack</c> events the feeder answers every blow, which reads exactly like the flags
	/// failing; driven with <see cref="BossAiHarness.Engage"/>, which lands hate the way a real blow
	/// does, it answers once. The difference is the harness, not the ladder.
	/// </remarks>
	[Fact]
	public void AndAtFullHealthOnlyOnce()
	{
		var (harness, feeder, drakan, raider) = Lab();
		using BossAiHarness _h = harness;

		int afterFirst = drakan.GetAggroList().GetHate(raider);
		Assert.Equal(EsoterraceAlarm.Notice, afterFirst);

		for (int i = 0; i < 5; i++)
			feeder.GetAi().OnCreatureEvent(AiEventType.Attack, raider);

		Assert.Equal(afterFirst, drakan.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>Four thresholds below it, each spent once</b> — eighty, sixty, forty and twenty. A raid that
	/// works the feeder down brings the lab in instalments rather than all at once.
	/// </summary>
	[Fact]
	public void TheFourThresholdsEachAnswerOnce()
	{
		var (harness, feeder, drakan, raider) = Lab();
		using BossAiHarness _h = harness;

		int bands = 1;   // the bare band, spent by Engage

		foreach (int percent in InsideEachBand)
		{
			BossAiHarness.SetExactPercent(feeder, percent);
			int before = drakan.GetAggroList().GetHate(raider);

			feeder.GetAi().OnCreatureEvent(AiEventType.Attack, raider);
			Assert.Equal(before + EsoterraceAlarm.Notice, drakan.GetAggroList().GetHate(raider));
			bands++;

			// ...and the same band does not answer a second time at the same health.
			feeder.GetAi().OnCreatureEvent(AiEventType.Attack, raider);
			Assert.Equal(before + EsoterraceAlarm.Notice, drakan.GetAggroList().GetHate(raider));
		}

		Assert.Equal(5, bands);
		Assert.Equal(5 * EsoterraceAlarm.Notice, drakan.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>And never a sixth.</b> Five bands is the whole ladder: once the feeder is at a fifth there is
	/// nothing left to spend, however long the beating goes on.
	/// </summary>
	[Fact]
	public void AndNeverASixth()
	{
		var (harness, feeder, drakan, raider) = Lab();
		using BossAiHarness _h = harness;

		foreach (int percent in InsideEachBand)
		{
			BossAiHarness.SetExactPercent(feeder, percent);
			feeder.GetAi().OnCreatureEvent(AiEventType.Attack, raider);
		}

		int spent = drakan.GetAggroList().GetHate(raider);
		Assert.Equal(5 * EsoterraceAlarm.Notice, spent);

		BossAiHarness.SetExactPercent(feeder, 4);
		for (int i = 0; i < 5; i++)
			feeder.GetAi().OnCreatureEvent(AiEventType.Attack, raider);

		Assert.Equal(spent, drakan.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>A spell raises it too, and the ladder is shared between the two provocations.</b> Retail
	/// writes the same five flags on <c>on_attacked</c> and <c>on_spelled</c>, so a caster cannot spend
	/// a band the melee has already spent.
	/// </summary>
	[Fact]
	public void ASpellSharesTheSameLadder()
	{
		var (harness, feeder, drakan, raider) = Lab();
		using BossAiHarness _h = harness;

		// Engage spent the bare band on the melee side; the cast must not spend it again.
		int afterEngage = drakan.GetAggroList().GetHate(raider);
		Assert.Equal(EsoterraceAlarm.Notice, afterEngage);

		feeder.GetAi().OnCreatureEvent(AiEventType.Spelled, raider);

		Assert.Equal(afterEngage, drakan.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>Thirty metres, which is what retail writes</b> — wide enough to take the whole room and not
	/// the floor above it.
	/// </summary>
	[Fact]
	public void AndOnlyWithinThirtyMetres()
	{
		var (harness, feeder, near, raider) = Lab();
		using BossAiHarness _h = harness;

		Npc distant = harness.SpawnWithAi(LabWizard, "esoterrace_drakan", 350f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(feeder, distant);
		BossAiHarness.MakeMutuallyKnown(distant, raider);

		BossAiHarness.SetExactPercent(feeder, 76);
		feeder.GetAi().OnCreatureEvent(AiEventType.Attack, raider);

		Assert.Equal(2 * EsoterraceAlarm.Notice, near.GetAggroList().GetHate(raider));
		Assert.Equal(0, distant.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>The lab staff answer the same as the villagers.</b> Sixteen patterns, one answer — ten points
	/// and a turn to fight, whether it is a village fighter or a lab wizard hearing it.
	/// </summary>
	[Fact]
	public void TheLabStaffAnswerTheSame()
	{
		var (harness, feeder, villager, raider) = Lab();
		using BossAiHarness _h = harness;

		Npc wizard = harness.SpawnWithAi(LabWizard, "esoterrace_drakan", 312f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(feeder, wizard);
		BossAiHarness.MakeMutuallyKnown(wizard, raider);

		// The wizard arrived after Engage, so compare on a band it can hear from the start.
		BossAiHarness.SetExactPercent(feeder, 76);
		feeder.GetAi().OnCreatureEvent(AiEventType.Attack, raider);

		Assert.Equal(EsoterraceAlarm.Notice, wizard.GetAggroList().GetHate(raider));
		Assert.Equal(2 * EsoterraceAlarm.Notice, villager.GetAggroList().GetHate(raider));
	}

	/// <summary><b>The number, reach and payload come from the pattern, not from us.</b></summary>
	[Fact]
	public void TheNumbersAreRetails()
	{
		Assert.Equal(10000, EsoterraceAlarm.Alarm);
		Assert.Equal(30f, EsoterraceAlarm.Reach);
		Assert.Equal(10, EsoterraceAlarm.Notice);
	}
}
