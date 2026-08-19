using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The surkana traps, which never appeared.
/// </summary>
/// <remarks>
/// See <see cref="SurkanaAI"/>. Retail drops one <c>NTrap_A</c> beside the surkana at each of six
/// health bands, twice over — once for being struck and once for being cast on — and this port carried
/// only the room aggro that shares those rungs.
/// <para>
/// <b>These count drops, not survivors.</b> <see cref="NTrapAI"/> casts and leaves the instant it
/// appears, so a trap is never on the map for a test to find. A previous attempt at this work was
/// reverted because its pins counted live npcs and read zero — the implementation had been running
/// correctly the whole time.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class SurkanaTrapTests
{
	private const int Dredgion = 300110000;
	private const int Surkana = 700485;

	private static (BossAiHarness, Npc, Player) Engaged()
	{
		BossAiHarness harness = BossAiHarness.For(Dredgion).WithWorldSize(2048)
			.WithAi(typeof(SurkanaAI), typeof(NTrapAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI)).Build();
		Npc surkana = harness.Spawn(Surkana, 500f, 500f, 200f);
		Player raider = harness.SpawnPlayer(503f, 500f, 200f);
		return (harness, surkana, raider);
	}

	private static int Dropped(Npc surkana) => ((SurkanaAI)surkana.GetAi()).TrapsDropped;

	/// <summary>
	/// <b>Being struck below a band lays a trap.</b>
	/// </summary>
	[Fact]
	public void BeingStruckBelowABandLaysATrap()
	{
		(BossAiHarness harness, Npc surkana, Player raider) = Engaged();
		using BossAiHarness _ = harness;

		BossAiHarness.SetExactPercent(surkana, 80);
		surkana.GetAi().OnCreatureEvent(AiEventType.Attack, raider);

		Assert.Equal(1, Dropped(surkana));
	}

	/// <summary>
	/// <b>Each band lays once, however many times it is hit.</b>
	/// </summary>
	[Fact]
	public void EachBandLaysOnce()
	{
		(BossAiHarness harness, Npc surkana, Player raider) = Engaged();
		using BossAiHarness _ = harness;
		BossAiHarness.SetExactPercent(surkana, 80);

		for (int i = 0; i < 5; i++)
			surkana.GetAi().OnCreatureEvent(AiEventType.Attack, raider);

		Assert.Equal(1, Dropped(surkana));
	}

	/// <summary>
	/// <b>One blow across several bands opens all of them.</b>
	/// </summary>
	/// <remarks>
	/// Retail's rungs are independent one-shots rather than a ladder, so a surkana dropped from full
	/// health to a quarter owes every band it passed. A ladder reading would give one.
	/// </remarks>
	[Fact]
	public void OneBlowAcrossSeveralBandsOpensAllOfThem()
	{
		(BossAiHarness harness, Npc surkana, Player raider) = Engaged();
		using BossAiHarness _ = harness;

		BossAiHarness.SetExactPercent(surkana, 24);
		surkana.GetAi().OnCreatureEvent(AiEventType.Attack, raider);

		// 90, 75, 60, 45 and 30 are all above 24.
		Assert.Equal(5, Dropped(surkana));
	}

	/// <summary>
	/// <b>Being cast on lays its own, at the same health.</b>
	/// </summary>
	/// <remarks>
	/// The two handlers carry separate flags in retail, so a surkana struck and then cast on at one
	/// health owes two traps.
	/// </remarks>
	[Fact]
	public void BeingCastOnLaysItsOwn()
	{
		(BossAiHarness harness, Npc surkana, Player raider) = Engaged();
		using BossAiHarness _ = harness;
		BossAiHarness.SetExactPercent(surkana, 80);
		surkana.GetAi().OnCreatureEvent(AiEventType.Attack, raider);
		Assert.Equal(1, Dropped(surkana));

		surkana.GetAi().OnCreatureEvent(AiEventType.Spelled, raider);

		Assert.Equal(2, Dropped(surkana));
	}

	/// <summary>
	/// <b>Nothing is laid above the first band.</b>
	/// </summary>
	[Fact]
	public void NothingIsLaidAboveTheFirstBand()
	{
		(BossAiHarness harness, Npc surkana, Player raider) = Engaged();
		using BossAiHarness _ = harness;

		BossAiHarness.SetExactPercent(surkana, 96);
		surkana.GetAi().OnCreatureEvent(AiEventType.Attack, raider);

		Assert.Equal(0, Dropped(surkana));
	}
}
