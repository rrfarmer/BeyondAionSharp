using Aion.GameServer.Handlers.AI;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Panesterra's garrison chiefs: one garrison's captains ran the class and three did not.
/// </summary>
/// <remarks>
/// Retail's <c>Gab1_LGuard_Boss_01</c> gives every npc on it the same <c>on_enter_attack_state</c> rung —
/// broadcast <b>41101</b> at thirteen metres naming the target, which is the captain's order that the
/// guards around it answer. <b>Sixty-five npcs run that pattern and four implemented it</b>: garrison
/// 01's chiefs 01-04. The same four chiefs in garrisons 02, 03 and 04 sat on plain <c>aggressive</c> and
/// gave no order at all.
/// <para>
/// Bound by garrison and chief number rather than by id list, so the shape of the fix is visible: what
/// was wrong is that three of four garrisons were missed, and the pin says exactly that.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class PanesterraChiefBindingTests
{
	private const int Aspida = 400040000;

	/// <summary>Garrison 01's chief 01, which already had the class — the control.</summary>
	private const int AlreadyBound = 277576;

	/// <summary>Chief 01 of garrisons 02, 03 and 04, which did not.</summary>
	private static readonly int[] WereMissing = [277581, 277586, 277591];

	/// <summary><c>GAb1_01_chief05_L</c> — a chief whose class is <c>base_protector</c>.</summary>
	private const int ChiefOnBaseProtector = 880132;

	/// <summary><c>LDF5_chief_v01_L</c> — a Kaldor village chief, same class, no pull call.</summary>
	private const int KaldorVillageChief = 231630;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Aspida).WithWorldSize(4096)
			.WithAi(typeof(PanesterraWarcaptainAI), typeof(BaseProtectorAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary><b>Every garrison's captains give the same order.</b></summary>
	[Fact]
	public void EveryGarrisonsCaptainsGiveTheSameOrder()
	{
		using BossAiHarness harness = NewHarness();

		foreach (int npcId in new[] { AlreadyBound }.Concat(WereMissing))
		{
			Npc chief = harness.Spawn(npcId, 300f + npcId % 19, 300f, 200f);
			Assert.IsType<PanesterraWarcaptainAI>(chief.GetAi());
		}
	}

	/// <summary>
	/// <b>And the order actually leaves them.</b> Retail's rung fires on being pulled, so the binding is
	/// only worth having if the broadcast follows.
	/// </summary>
	[Fact]
	public void AndTheOrderActuallyLeavesThem()
	{
		using BossAiHarness harness = NewHarness();
		Npc chief = harness.Spawn(WereMissing[0], 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(300f, 290f, 200f, race: Race.ASMODIANS);
		var seen = new List<int>();

		using (NpcMessageBusProbe probe = NpcMessageBusProbe.Watch(seen))
			harness.Engage(chief, player);

		Assert.Contains(PanesterraCalls.LightCaptain, seen);
	}

	/// <summary>
	/// <b>A garrison chief on <c>base_protector</c> gives the order too.</b> Forty-eight chiefs shout
	/// 41101 in retail and twelve more shout 41001, and that class had no rung for either.
	/// </summary>
	/// <remarks>
	/// <b>It could not take the generated branch.</b> <c>BaseProtectorAI</c> derives from
	/// <c>AggressiveNpcAI</c>, not <c>PatternAi</c>, so the call is made from its
	/// <c>HandleCreatureAggro</c> — which already latches, because it broadcasts 30002 there once per
	/// fight. Retail's rung is <c>on_enter_attack_state</c> and fires once; an unlatched aggro hook would
	/// shout for the whole fight.
	/// </remarks>
	[Fact]
	public void AGarrisonChiefOnBaseProtectorGivesTheOrderToo()
	{
		using BossAiHarness harness = BossAiHarness.For(Aspida).WithWorldSize(4096)
			.WithAi(typeof(BaseProtectorAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI)).Build();
		Npc chief = harness.Spawn(ChiefOnBaseProtector, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(300f, 290f, 200f, race: Race.ASMODIANS);
		var seen = new List<int>();

		// CreatureAggro, not Engage: BaseProtectorAI answers the aggro event, and Engage raises Attack.
		// The two are different doors into a fight and this class only listens at one of them.
		using (NpcMessageBusProbe probe = NpcMessageBusProbe.Watch(seen))
		{
			harness.Engage(chief, player);
			chief.GetAi().OnCreatureEvent(AiEventType.CreatureAggro, player);
		}

		Assert.Contains(PanesterraCalls.LightCaptain, seen);
	}

	/// <summary>
	/// <b>And a base protector outside Panesterra stays quiet.</b> The class covers village chiefs in
	/// Kaldor and garrison chiefs in Ashunatal; only the second kind has a pull call, which is why this
	/// is a table lookup and not a line in the handler.
	/// </summary>
	[Fact]
	public void AndABaseProtectorOutsidePanesterraStaysQuiet()
	{
		using BossAiHarness harness = BossAiHarness.For(Aspida).WithWorldSize(4096)
			.WithAi(typeof(BaseProtectorAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI)).Build();
		Npc chief = harness.Spawn(KaldorVillageChief, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(300f, 290f, 200f, race: Race.ASMODIANS);
		var seen = new List<int>();

		using (NpcMessageBusProbe probe = NpcMessageBusProbe.Watch(seen))
		{
			harness.Engage(chief, player);
			chief.GetAi().OnCreatureEvent(AiEventType.CreatureAggro, player);
		}

		Assert.DoesNotContain(PanesterraCalls.LightCaptain, seen);
	}
}
