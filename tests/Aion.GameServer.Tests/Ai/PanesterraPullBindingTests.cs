using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Ai;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Ashunatal's guards call for help when pulled, and 255 of them did not.
/// </summary>
/// <remarks>
/// The answering half of this mechanic was ported and the sending half was not: <b>552 npcs listen for
/// 41101 alone and sixteen sent it.</b> Retail's enter-combat rung broadcasts one of four calls at one
/// of two ranges, and <b>the pair identifies the role exactly</b> — every npc already on
/// <c>panesterra_cutthroat</c> sends 41000 at thirteen metres, every <c>panesterra_lookout</c> sends
/// 41000 at twenty-five, and so on through all six, with no exceptions in 474 npcs.
/// <para>
/// So the generic ones were bound by what their own pattern shouts. That is a stronger rule than a
/// family majority: it does not ask what the siblings do, it asks what this npc's retail rung says.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class PanesterraPullBindingTests
{
	private const int Aspida = 400040000;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Aspida).WithWorldSize(4096)
			.WithAi(typeof(PanesterraCutthroatAI), typeof(PanesterraLookoutAI),
				typeof(PanesterraPatrolAI), typeof(PanesterraSlayerAI),
				typeof(PanesterraWarcaptainAI), typeof(PanesterraDreadcaptainAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>
	/// <b>The table and the classes agree, all 474 of them.</b> Every npc whose retail rung shouts a
	/// given call at a given range is on the class that sends exactly that — which is the rule the
	/// binding used, so this is the check that the rule held.
	/// </summary>
	[Fact]
	public void EveryGuardIsOnTheClassItsOwnCallImplies()
	{
		var roles = new Dictionary<(int, float), string>
		{
			[(41000, 13f)] = "PanesterraCutthroatAI",
			[(41000, 25f)] = "PanesterraLookoutAI",
			[(41100, 13f)] = "PanesterraSlayerAI",
			[(41100, 25f)] = "PanesterraPatrolAI",
			[(41101, 13f)] = "PanesterraWarcaptainAI",
			[(41001, 13f)] = "PanesterraDreadcaptainAI",
		};

		using BossAiHarness harness = NewHarness();
		var wrong = new List<string>();

		foreach ((int npcId, PanesterraPulls.Pull[] calls) in PanesterraPulls.ByNpc)
		{
			// The base protectors are the documented exception: their pattern shouts and their class
			// has no rung for it. See docs/retail-ai-fidelity.md.
			Npc guard = harness.Spawn(npcId, 300f + npcId % 23, 300f, 200f);
			string actual = guard.GetAi().GetType().Name;
			if (actual is "BaseProtectorAI" or "GeneralNpcAI")
				continue;

			string expected = roles[(calls[0].Call, calls[0].Range)];
			if (actual != expected)
				wrong.Add($"{npcId}: {actual}, expected {expected}");
		}

		Assert.Empty(wrong);
	}

	/// <summary>
	/// <b>And a rebound guard actually shouts.</b> One of the hundred and one that were silent.
	/// </summary>
	[Fact]
	public void AReboundGuardActuallyShouts()
	{
		using BossAiHarness harness = NewHarness();
		int npcId = PanesterraPulls.ByNpc
			.First(e => e.Value[0].Call == 41100 && e.Value[0].Range == 13f).Key;
		Npc guard = harness.Spawn(npcId, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(300f, 290f, 200f, race: Race.ASMODIANS);
		var seen = new List<int>();

		using (NpcMessageBusProbe probe = NpcMessageBusProbe.Watch(seen))
			harness.Engage(guard, player);

		Assert.Contains(41100, seen);
	}
}
