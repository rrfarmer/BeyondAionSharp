using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services.Panesterra.Ahserion;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for Panesterra's base guards, translated from the fifteen <c>Gab1_*</c> guard patterns (see
/// <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
[Collection("GoldenDataManager")]
public sealed class PanesterraGuardAiTests
{
	private const int Panesterra = 400030000;

	// Vritra-side base: calls on 41000, its captain on 41001.
	private const int Cutthroat = 277680;      // calls at 13m
	private const int Lookout = 277660;        // calls at 25m
	private const int Grunt = 277640;          // answers only
	private const int Dreadcaptain = 277580;   // calls on the captain number

	// The other base: 41100 and 41101.
	private const int Patrol = 277656;         // calls at 25m
	private const int Slayer = 277676;         // calls at 13m, and the one with an is_enemy
	private const int Infantry = 277636;       // answers only
	private const int Warcaptain = 277576;
	private const int RivalCutthroat = 277772; // answers the other base's captain

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Panesterra).WithWorldSize(2048)
			.WithAi(typeof(PanesterraCutthroatAI), typeof(PanesterraLookoutAI),
				typeof(PanesterraSoldierAI), typeof(PanesterraDreadcaptainAI),
				typeof(PanesterraPatrolAI), typeof(PanesterraSlayerAI),
				typeof(PanesterraInfantryAI), typeof(PanesterraWarcaptainAI),
				typeof(PanesterraBossKillerAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>A caller, an answerer beside it, and the raider who pulls the caller.</summary>
	private static (BossAiHarness, Npc, Npc, Player) Base(
		int callerId, int answererId, float apart = 10f, Race race = Race.ASMODIANS)
	{
		BossAiHarness harness = NewHarness();
		Npc caller = harness.Spawn(callerId, 300f, 300f, 200f);
		Npc answerer = harness.Spawn(answererId, 300f + apart, 300f, 200f);
		Player raider = harness.SpawnPlayer(303f, 300f, 200f, race: race);
		BossAiHarness.MakeMutuallyKnown(caller, answerer);
		return (harness, caller, answerer, raider);
	}

	/// <summary>
	/// <b>Pull one guard and the base answers with ten.</b> Both bases, both numbers, the same payload.
	/// </summary>
	[Theory]
	[InlineData(Cutthroat, Grunt)]
	[InlineData(Patrol, Infantry)]
	public void PullOneGuardAndTheBaseAnswersWithTen(int callerId, int answererId)
	{
		var (harness, caller, answerer, raider) = Base(callerId, answererId);
		using BossAiHarness _h = harness;

		Assert.Equal(0, answerer.GetAggroList().GetHate(raider));

		harness.Engage(caller, raider);

		Assert.Equal(10, answerer.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>A captain is worth a hundred.</b> That ten-to-one difference is the whole tiering of a base:
	/// the guards are a nuisance and the captain is the pull that brings the room.
	/// </summary>
	[Theory]
	[InlineData(Dreadcaptain, Grunt)]
	[InlineData(Warcaptain, Infantry)]
	public void ACaptainIsWorthAHundred(int captainId, int answererId)
	{
		var (harness, captain, answerer, raider) = Base(captainId, answererId);
		using BossAiHarness _h = harness;

		harness.Engage(captain, raider);

		Assert.Equal(100, answerer.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>A lookout's call carries twice as far as a cutthroat's</b> — thirteen metres against
	/// twenty-five, which is what a lookout is posted for. Pulling the wrong one crosses the base.
	/// </summary>
	[Fact]
	public void ALookoutsCallCarriesTwiceAsFar()
	{
		var (near, cutthroat, farGrunt, raider) = Base(Cutthroat, Grunt, apart: 20f);
		using BossAiHarness _n = near;
		near.Engage(cutthroat, raider);
		Assert.Equal(0, farGrunt.GetAggroList().GetHate(raider));

		var (far, lookout, sameGrunt, other) = Base(Lookout, Grunt, apart: 20f);
		using BossAiHarness _f = far;
		far.Engage(lookout, other);

		Assert.Equal(10, sameGrunt.GetAggroList().GetHate(other));
	}

	/// <summary>
	/// <b>The two bases do not hear each other.</b> Each runs its own pair of numbers, which is what
	/// makes Panesterra four factions in one map rather than one very large fight.
	/// </summary>
	[Fact]
	public void TheTwoBasesDoNotHearEachOther()
	{
		var (harness, cutthroat, infantry, raider) = Base(Cutthroat, Infantry);
		using BossAiHarness _h = harness;

		harness.Engage(cutthroat, raider);

		Assert.Equal(0, infantry.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>A warcaptain is heard by the rival bases' cutthroats</b>, who belong to nobody's base but his
	/// enemies'. Twelve npcs across Aspamon, Atasin and Disilgot listen for a Belani captain and nothing
	/// else.
	/// </summary>
	[Fact]
	public void AWarcaptainIsHeardByTheRivalBases()
	{
		var (harness, warcaptain, rival, raider) = Base(Warcaptain, RivalCutthroat);
		using BossAiHarness _h = harness;

		harness.Engage(warcaptain, raider);

		Assert.Equal(100, rival.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>The slayers are the one pattern in ten that checks whose enemy the named player is.</b> Nine
	/// others answer whoever is named; retail wrote the guard on exactly one, and it is kept.
	/// </summary>
	/// <remarks>
	/// Measured on the turn rather than the hate, for the reason the fortress guards' equivalent pin
	/// gives: <c>AggroList.AddHate</c> already refuses hate aimed at a non-enemy, so the hate is zero
	/// either way and only the facing tells the two apart.
	/// <para>
	/// <b>In Panesterra a player's race does not make them anybody's friend</b> — the guards' tribes are
	/// the four base factions, and both player races are enemies of all of them until a player is
	/// assigned a faction. The first version of this pin used an Elyos raider against Elyos-race guards
	/// and failed, correctly: there is no such thing as a friendly player here without
	/// <c>SetPanesterraFaction</c>. Belani's tribe is <c>GAB1_01_POINT_01</c>, which is
	/// <see cref="PanesterraFaction.BELUS"/>.
	/// </para>
	/// </remarks>
	[Fact]
	public void TheSlayersCheckWhoseEnemyTheNamedPlayerIs()
	{
		var (harness, patrol, slayer, friendly) = Base(Patrol, Slayer);
		using BossAiHarness _h = harness;

		friendly.SetPanesterraFaction(PanesterraFaction.BELUS);
		harness.Engage(patrol, friendly);

		Assert.NotSame(friendly, slayer.GetTarget());
	}

	/// <summary><b>The numbers and the ranges are retail's, not ours.</b></summary>
	[Fact]
	public void TheNumbersAreRetails()
	{
		Assert.Equal(41000, PanesterraCalls.VritraGuard);
		Assert.Equal(41001, PanesterraCalls.VritraCaptain);
		Assert.Equal(41100, PanesterraCalls.LightGuard);
		Assert.Equal(41101, PanesterraCalls.LightCaptain);
		Assert.Equal(13f, PanesterraCalls.Near);
		Assert.Equal(25f, PanesterraCalls.Far);
	}

	// The castle companies, one tier up from the bases.
	private const int SiegemakeMarksman = 880817;   // calls on 40000
	private const int SiegemakeGuard = 880814;      // answers 40000
	private const int SiegemakeRanger = 880808;     // calls on 40100
	private const int SiegemakeDefender = 880804;   // answers 40100

	private static BossAiHarness CastleHarness() =>
		BossAiHarness.For(Panesterra).WithWorldSize(2048)
			.WithAi(typeof(PanesterraCastleWatchAI), typeof(PanesterraCastleGuardAI),
				typeof(PanesterraCastleRangerAI), typeof(PanesterraCastleDefenderAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>
	/// <b>A castle answers harder than a base.</b> Its guards take a hundred where a base's take ten,
	/// which is the whole difference in payload between the two tiers.
	/// </summary>
	[Theory]
	[InlineData(SiegemakeMarksman, SiegemakeGuard)]
	[InlineData(SiegemakeRanger, SiegemakeDefender)]
	public void ACastleAnswersHarderThanABase(int callerId, int answererId)
	{
		using BossAiHarness harness = CastleHarness();
		Npc caller = harness.Spawn(callerId, 300f, 300f, 200f);
		Npc answerer = harness.Spawn(answererId, 310f, 300f, 200f);
		Player raider = harness.SpawnPlayer(303f, 300f, 200f, race: Race.ASMODIANS);
		BossAiHarness.MakeMutuallyKnown(caller, answerer);

		harness.Engage(caller, raider);

		Assert.Equal(100, answerer.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>And it is fussier about who it answers for.</b> Every castle answerer carries
	/// <c>is_enemy</c> on the player named, where among the base guards exactly one did — so a castle
	/// guard hearing its own company name a member of its own faction does nothing.
	/// </summary>
	/// <remarks>
	/// Measured on the turn, as the base slayers' equivalent is: <c>AddHate</c> refuses a non-enemy
	/// either way and only the facing separates the two.
	/// </remarks>
	[Fact]
	public void AndItIsFussierAboutWhoItAnswersFor()
	{
		using BossAiHarness harness = CastleHarness();
		Npc caller = harness.Spawn(SiegemakeRanger, 300f, 300f, 200f);
		Npc answerer = harness.Spawn(SiegemakeDefender, 310f, 300f, 200f);
		Player friendly = harness.SpawnPlayer(303f, 300f, 200f, race: Race.ASMODIANS);
		BossAiHarness.MakeMutuallyKnown(caller, answerer);

		friendly.SetPanesterraFaction(PanesterraFaction.BELUS);
		harness.Engage(caller, friendly);

		Assert.NotSame(friendly, answerer.GetTarget());
	}

	/// <summary>
	/// <b>The two companies do not hear each other</b>, exactly as the two bases do not.
	/// </summary>
	[Fact]
	public void TheTwoCompaniesDoNotHearEachOther()
	{
		using BossAiHarness harness = CastleHarness();
		Npc caller = harness.Spawn(SiegemakeMarksman, 300f, 300f, 200f);
		Npc otherCompany = harness.Spawn(SiegemakeDefender, 310f, 300f, 200f);
		Player raider = harness.SpawnPlayer(303f, 300f, 200f, race: Race.ASMODIANS);
		BossAiHarness.MakeMutuallyKnown(caller, otherCompany);

		harness.Engage(caller, raider);

		Assert.Equal(0, otherCompany.GetAggroList().GetHate(raider));
	}

	/// <summary><b>And the castle numbers are retail's too.</b></summary>
	[Fact]
	public void TheCastleNumbersAreRetails()
	{
		Assert.Equal(40000, PanesterraCastleCalls.Siegemake);
		Assert.Equal(40100, PanesterraCastleCalls.Siegebreak);
		Assert.Equal(25f, PanesterraCastleCalls.CallReach);
		Assert.Equal(100, PanesterraCastleCalls.Claim);
	}
}
