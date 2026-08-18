using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for Guardian Vingeveu and his servants, translated from retail patterns <c>ND2_KeB</c> and
/// <c>ND2_Ksum1</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
[Collection("GoldenDataManager")]
public sealed class GuardianVingeveuAiTests
{
	private const int Heiron = 210040000;

	private const int Vingeveu = 212281;
	private const int Servant = 212284;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Heiron).WithWorldSize(2048)
			.WithAi(typeof(GuardianVingeveuAI), typeof(VingeveuServantAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>Vingeveu, one servant in earshot, and the raider he is holding.</summary>
	private static (BossAiHarness, Npc, Npc, Player) Room()
	{
		BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Vingeveu, 300f, 300f, 200f);
		Npc servant = harness.Spawn(Servant, 320f, 300f, 200f);
		Player raider = harness.SpawnPlayer(303f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(boss, servant);
		return (harness, boss, servant, raider);
	}

	/// <summary>
	/// <b>Engaging is itself a band-change call.</b> He opens the fight on <c>6194</c>, so his servants
	/// arrive already knowing who he picked — and already scattering.
	/// </summary>
	[Fact]
	public void EngagingCallsTheServantsWithTheLoudOne()
	{
		var (harness, boss, servant, raider) = Room();
		using BossAiHarness _h = harness;

		Assert.Equal(0, servant.GetAggroList().GetHate(raider));

		harness.Engage(boss, raider);

		Assert.Equal(10, servant.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>The first band's opener is the quiet one.</b> Above seventy health he asks for help — one
	/// point on the servants, not ten — and does not scatter. What separates the bands is which call
	/// they open with.
	/// </summary>
	[Fact]
	public void TheFirstBandOpensQuietly()
	{
		var (harness, boss, servant, raider) = Room();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 90);
		harness.Engage(boss, raider);
		int afterEngage = servant.GetAggroList().GetHate(raider);

		// Timer zero is armed at fifteen seconds by the engage branch; the first band's opener is the
		// branch that claims it.
		harness.Watch(20, null, Servant);

		Assert.Equal(afterEngage + 1, servant.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>The second and third bands open loudly, and each opens exactly once.</b> Retail gives every
	/// band its own flag var, so crossing seventy announces itself and then goes quiet however long the
	/// raid stays there.
	/// </summary>
	[Theory]
	[InlineData(50)]
	[InlineData(20)]
	public void TheLowerBandsOpenLoudlyAndOnlyOnce(int percent)
	{
		var (harness, boss, servant, raider) = Room();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, percent);
		harness.Engage(boss, raider);
		int afterEngage = servant.GetAggroList().GetHate(raider);

		// The opener lands when the engage branch's fifteen-second heartbeat comes round; the timer it
		// arms is fifteen seconds further out and is deliberately outside this window.
		harness.Watch(20, null, Servant);
		int afterOpening = servant.GetAggroList().GetHate(raider);
		Assert.Equal(afterEngage + 10, afterOpening);

		// Long enough for the heartbeat to come round several more times, and for the band timer to
		// land -- so the gain here is the quiet call, once or twice, and never another ten.
		harness.Watch(30, null, Servant);

		Assert.InRange(servant.GetAggroList().GetHate(raider), afterOpening + 1, afterOpening + 9);
	}

	/// <summary>
	/// <b>Crossing a boundary opens the next band, and the previous one stays shut.</b> The flags are
	/// per band rather than one shared "have I announced", which is what lets a single fight change
	/// character twice.
	/// </summary>
	/// <remarks>
	/// <b>Each crossing here is worth eleven and not ten, and the extra point is the mechanic.</b> The
	/// opener arms the band's own timer fifteen seconds out; when that timer comes round it sends the
	/// quiet call. So a band change is a loud announcement followed fifteen seconds later by a request
	/// for help — which the first version of this pin read as an off-by-one and it is not.
	/// <para>
	/// <b>Why the sibling pin sees ten and this one eleven.</b> There, the fight starts inside the band
	/// and the opener lands on the engage branch's fifteen-second heartbeat, so the timer it arms falls
	/// outside the window. Here the heartbeat has already been sped up by the band above, the opener
	/// lands earlier, and its timer lands inside. Both are the same branch; the window moved.
	/// </para>
	/// </remarks>
	[Fact]
	public void CrossingABoundaryOpensTheNextBand()
	{
		var (harness, boss, servant, raider) = Room();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 90);
		harness.Engage(boss, raider);
		harness.Watch(20, null, Servant);
		int afterFirst = servant.GetAggroList().GetHate(raider);

		// Ten for the engage call, one for the first band's quiet opener; its own timer is armed
		// twenty-five seconds out and does not land inside this window.
		Assert.Equal(11, afterFirst);

		BossAiHarness.SetExactPercent(boss, 50);
		harness.Watch(20, null, Servant);
		int afterSecond = servant.GetAggroList().GetHate(raider);
		Assert.Equal(afterFirst + 11, afterSecond);

		BossAiHarness.SetExactPercent(boss, 20);
		harness.Watch(20, null, Servant);

		Assert.Equal(afterSecond + 11, servant.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>And exactly thirty-five belongs to no band.</b> Retail guards the third on
	/// <c>is_hp_lower_than 35</c> and the second on <c>larger_than 36</c>, which leaves one integer
	/// where only the heartbeat runs. Kept rather than closed — inventing a boundary would be inventing
	/// a number.
	/// </summary>
	[Fact]
	public void AndExactlyThirtyFiveBelongsToNoBand()
	{
		var (harness, boss, servant, raider) = Room();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 35);
		harness.Engage(boss, raider);
		int afterEngage = servant.GetAggroList().GetHate(raider);

		harness.Watch(30, null, Servant);

		Assert.Equal(afterEngage, servant.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>A servant out past fifty metres hears nothing.</b> Retail's range on every call he makes is
	/// wide, which is the point — but it is not unlimited.
	/// </summary>
	[Fact]
	public void AndOnlyWithinFiftyMetres()
	{
		var (harness, boss, servant, raider) = Room();
		using BossAiHarness _h = harness;

		Npc distant = harness.Spawn(Servant, 380f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(boss, distant);

		harness.Engage(boss, raider);

		Assert.Equal(10, servant.GetAggroList().GetHate(raider));
		Assert.Equal(0, distant.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>The loud openers scatter him off his tank.</b> Both end in
	/// <c>switch_target_by_attacker_indicator ATTACKERI_RANDOM_ONE</c>, and with three players on his
	/// list that is visible: across twelve fights he does not hold the most-hated every time.
	/// </summary>
	/// <remarks>
	/// <b>This was skipped as "not an observation", and that was true only of the setup it was written
	/// for.</b> With one raider a random pick is that raider and the pin measures nothing. With three,
	/// holding the same player twelve times running is a one-in-half-a-million coincidence — the same
	/// stated-exponent technique Masto's opening scatter uses, which existed before this skip was
	/// written and was not applied to it.
	/// <para>
	/// <b>All three have to have attacked, not merely be hated.</b> The scatter picks from the aggro
	/// list's <em>attackers</em>, so adding hate to a bystander does not put it in the pool — an earlier
	/// version of this pin did exactly that and the boss held its tank every time, looking for all the
	/// world like a scatter that did not work.
	/// </para>
	/// </remarks>
	[Fact]
	public void TheLoudOpenersScatterHimOffHisTank()
	{
		bool alwaysTheTank = true;
		for (int attempt = 0; attempt < 12 && alwaysTheTank; attempt++)
		{
			using BossAiHarness harness = NewHarness();
			Npc boss = harness.Spawn(Vingeveu, 300f, 300f, 200f);
			Player tank = harness.SpawnPlayer(303f, 300f, 200f, race: Race.ELYOS);
			Player second = harness.SpawnPlayer(304f, 300f, 200f, race: Race.ELYOS);
			Player third = harness.SpawnPlayer(305f, 300f, 200f, race: Race.ELYOS);

			// All three have to have attacked, not merely be hated: the scatter picks from the aggro
			// list's attackers. Engaging each in turn is what puts them there.
			harness.Engage(boss, second);
			harness.Engage(boss, third);
			harness.Engage(boss, tank);
			boss.SetTarget(tank);

			// The band openers below seventy carry the same scatter; the clock reaches one of them.
			BossAiHarness.SetExactPercent(boss, 50);
			harness.Watch(30, null);

			alwaysTheTank = ReferenceEquals(tank, boss.GetTarget());
		}

		Assert.False(alwaysTheTank, "he never once let go of his tank");
	}

	/// <summary><b>The message numbers and the range are retail's, not ours.</b></summary>
	[Fact]
	public void TheMessageNumbersAreRetails()
	{
		Assert.Equal(6193, GuardianVingeveuAI.HelpMe);
		Assert.Equal(6194, GuardianVingeveuAI.Again);
		Assert.Equal(50f, GuardianVingeveuAI.CallReach);
	}
}
