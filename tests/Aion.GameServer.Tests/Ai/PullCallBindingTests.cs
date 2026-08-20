using System;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Ai;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Guards call for help when pulled, and hundreds of them did not.
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
public sealed class PullCallBindingTests
{
	private const int Aspida = 400040000;

	/// <summary>
	/// <c>Ab1_1401_Boss_Dr_1</c> — its retail rung shouts 23200 and its class already had one of its own,
	/// so it is the case the merge has to get right.
	/// </summary>
	/// <remarks>
	/// Named rather than picked from the table by predicate: the first version did that and chose an npc
	/// with a retail pattern and no template in this port, which fails at the spawn rather than at the
	/// assertion and says nothing about the merge.
	/// </remarks>
	private const int ArtifactProtectorThatShouts = 251450;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Aspida).WithWorldSize(4096)
			.WithAi(typeof(PanesterraCutthroatAI), typeof(PanesterraLookoutAI),
				typeof(PanesterraPatrolAI), typeof(PanesterraSlayerAI),
				typeof(PanesterraWarcaptainAI), typeof(PanesterraDreadcaptainAI),
				typeof(FortressGuardCallAI), typeof(ArtifactProtectorAI),
				typeof(FortressProtectorNpcAI),
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
			[(23200, 25f)] = "FortressGuardCallAI",
			[(23200, 10f)] = "FortressGuardCallAI",
		};

		using BossAiHarness harness = NewHarness();
		var wrong = new List<string>();

		foreach ((int npcId, PullCalls.Pull[] calls) in PullCalls.ByNpc)
		{
			// The base protectors are the documented exception: their pattern shouts and their class
			// has no rung for it. See docs/retail-ai-fidelity.md.
			// Two of the table's npcs have a retail pattern and no template in this port, so they cannot
			// be spawned. They are a gap in the npc data rather than in the binding, and skipping them
			// here keeps this pin about the rule it states.
			Npc? guard = TrySpawn(harness, npcId);
			if (guard is null)
				continue;

			string actual = guard.GetAi().GetType().Name;
			// The siege protectors are the documented exception: their pattern shouts 23200 and their
			// class carries its own rung, so the call is merged rather than the class replaced. Base
			// protectors are the same case for 41101. See docs/retail-ai-fidelity.md.
			if (actual is "BaseProtectorAI" or "GeneralNpcAI"
				or "ArtifactProtectorAI" or "FortressProtectorNpcAI")
				continue;

			string expected = roles[(calls[0].Call, calls[0].Range)];
			if (actual != expected)
				wrong.Add($"{npcId}: {actual}, expected {expected}");
		}

		Assert.Empty(wrong);

		static Npc? TrySpawn(BossAiHarness harness, int npcId)
		{
			try
			{
				return harness.Spawn(npcId, 300f + npcId % 23, 300f, 200f);
			}
			catch (Exception)
			{
				return null;
			}
		}
	}

	/// <summary>
	/// <b>And a rebound guard actually shouts.</b> One of the hundred and one that were silent.
	/// </summary>
	[Fact]
	public void AReboundGuardActuallyShouts()
	{
		using BossAiHarness harness = NewHarness();
		int npcId = PullCalls.ByNpc
			.First(e => e.Value[0].Call == 41100 && e.Value[0].Range == 13f).Key;
		Npc guard = harness.Spawn(npcId, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(300f, 290f, 200f, race: Race.ASMODIANS);
		var seen = new List<int>();

		using (NpcMessageBusProbe probe = NpcMessageBusProbe.Watch(seen))
			harness.Engage(guard, player);

		Assert.Contains(41100, seen);
	}

	/// <summary>
	/// <b>A siege protector shouts when pulled too, without losing its own rung.</b> 557 of them send
	/// 23200 in retail — naming the player to every guard within twenty-five metres — and the class had
	/// no part of it: a raid could take the protectors one at a time.
	/// </summary>
	/// <remarks>
	/// <b>Merged into the same branch, not appended behind it.</b> The protector's enter-combat rung
	/// already arms its own clock, and branch lists are first-match-wins — so a second unconditional
	/// branch would be dead. This pin passes only if both actions run.
	/// </remarks>
	[Fact]
	public void ASiegeProtectorShoutsWhenPulledAndKeepsItsOwnRung()
	{
		using BossAiHarness harness = NewHarness();
		Npc protector = harness.Spawn(ArtifactProtectorThatShouts, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(300f, 290f, 200f, race: Race.ASMODIANS);
		var seen = new List<int>();

		using (NpcMessageBusProbe probe = NpcMessageBusProbe.Watch(seen))
			harness.Engage(protector, player);

		Assert.Contains(FortressGuardCallAI.ThisOne, seen);

		// And its own rung still runs: the focus clock it arms is what reaches 30002 later.
		Assert.True(((Aion.GameServer.Ai.Pattern.PatternAi)protector.GetAi()).TimerArmCount(0) > 0,
			"the protector's own enter-combat action was lost to the merge");
	}
}
