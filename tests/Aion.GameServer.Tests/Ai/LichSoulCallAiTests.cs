using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for the lich soul call, translated from retail patterns <c>ND2_Callsoulst</c> and
/// <c>ND2_PnC</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
[Collection("GoldenDataManager")]
public sealed class LichSoulCallAiTests
{
	private const int Brusthonin = 220050000;

	private const int LichHighPriest = 212319;
	private const int EbonSorcererLich = 212589;
	private const int FaithfulServant = 286080;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Brusthonin).WithWorldSize(2048)
			.WithAi(typeof(LichSoulCallAI), typeof(FaithfulServantAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static List<Npc> Live(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Where(n => n.GetNpcId() == npcId).ToList();

	private static (BossAiHarness, Npc, Player) Fight(int lichId = LichHighPriest)
	{
		BossAiHarness harness = NewHarness();
		Npc lich = harness.Spawn(lichId, 300f, 300f, 200f);
		Player raider = harness.SpawnPlayer(302f, 300f, 200f, race: Race.ASMODIANS);
		harness.Engage(lich, raider);
		return (harness, lich, raider);
	}

	/// <summary>
	/// <b>Below half health it calls a servant and sets it on whoever it is holding, in one breath.</b>
	/// The stoneskin stoffu arms a three-second timer before its call; the lich has no such window.
	/// </summary>
	[Fact]
	public void BelowHalfItCallsAServantAndSetsItOn()
	{
		var (harness, lich, raider) = Fight();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(lich, 60);
		lich.GetAi().OnCreatureEvent(AiEventType.Attack, raider);
		Assert.Empty(Live(harness, FaithfulServant));

		BossAiHarness.SetExactPercent(lich, 40);
		lich.GetAi().OnCreatureEvent(AiEventType.Attack, raider);

		Npc servant = Assert.Single(Live(harness, FaithfulServant));
		Assert.Same(raider, servant.GetTarget());
		// A hundred exactly, unlike the corask clodworms' hundred and one: those arrive through
		// AttackAfterSpawn and gain one more when they start swinging, while a servant is pointed by a
		// message and simply takes retail's points_to_add.
		Assert.Equal(100, servant.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>A spell calls it too, and the same flag stops it happening twice.</b> Retail writes the branch
	/// on both handlers with one <c>FLAGVARI_ALPHA_1</c> across them.
	/// </summary>
	[Fact]
	public void ASpellCallsItTooAndTheFlagIsShared()
	{
		var (harness, lich, raider) = Fight();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(lich, 40);
		lich.GetAi().OnCreatureEvent(AiEventType.Spelled, raider);
		Assert.Single(Live(harness, FaithfulServant));

		lich.GetAi().OnCreatureEvent(AiEventType.Attack, raider);
		lich.GetAi().OnCreatureEvent(AiEventType.Spelled, raider);

		Assert.Single(Live(harness, FaithfulServant));
	}

	/// <summary>
	/// <b>Every lich in the family calls.</b> Retail binds fourteen npcs to this one pattern; four of
	/// them are live on our server and two are pinned here, because a class that only worked for the
	/// npc it was written against would pass a single-boss pin.
	/// </summary>
	[Theory]
	[InlineData(LichHighPriest)]
	[InlineData(EbonSorcererLich)]
	public void EveryLichInTheFamilyCalls(int lichId)
	{
		var (harness, lich, raider) = Fight(lichId);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(lich, 40);
		lich.GetAi().OnCreatureEvent(AiEventType.Attack, raider);

		Assert.Single(Live(harness, FaithfulServant));
	}

	/// <summary>
	/// <b>And it takes the servant with it.</b> Retail clears the group on dying, so a servant never
	/// outlives the lich that called it.
	/// </summary>
	[Fact]
	public void AndItTakesTheServantWithIt()
	{
		var (harness, lich, raider) = Fight();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(lich, 40);
		lich.GetAi().OnCreatureEvent(AiEventType.Attack, raider);
		Assert.Single(Live(harness, FaithfulServant));

		lich.GetAi().OnGeneralEvent(AiEventType.Died);

		Assert.Empty(Live(harness, FaithfulServant));
	}

	/// <summary>
	/// <b>And only within ten metres</b> — a tenth of the stoffu's forty, so a lich sets its own servant
	/// on somebody and nobody else's.
	/// </summary>
	[Fact]
	public void AndOnlyWithinTenMetres()
	{
		var (harness, lich, raider) = Fight();
		using BossAiHarness _h = harness;

		Npc distant = harness.Spawn(FaithfulServant, 340f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(lich, distant);

		BossAiHarness.SetExactPercent(lich, 40);
		lich.GetAi().OnCreatureEvent(AiEventType.Attack, raider);

		Assert.Null(distant.GetTarget());
	}

	/// <summary>
	/// <b>The message number is retail's, not ours</b> — and it is <c>2006</c>, the same number the
	/// stoneskin stoffu uses for its fragments. Two encounters share the call and each has its own
	/// listener; a number changed here would move both and no other pin would notice.
	/// </summary>
	[Fact]
	public void TheMessageNumberIsRetails()
	{
		Assert.Equal(2006, LichSoulCallAI.PointIt);
		Assert.Equal(LichSoulCallAI.PointIt, StoneskinStoffuAI.PointIt);
	}
}
