using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Xunit;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// <b>A broadcast names a creature, and the table named none of them.</b>
/// </summary>
/// <remarks>
/// Every one of retail's 6,822 <c>broadcast_message</c> uses carries a <c>param_obj</c> — the creature
/// the message is <em>about</em>. The extractor read the message number and the range and dropped it,
/// so all 12,362 broadcast rows reached their listeners with a null parameter.
/// <para>
/// <b>That is the whole point of the message.</b> On the listening side, 1,008 action rows and 239
/// guard rows read exactly that parameter — <c>HateMessageParam</c>, <c>MessageParamIsEnemy</c>,
/// <c>MessageParamWithin</c>. Every one of them was a no-op, and silently: an unnamed message is still
/// a message, so the shout happened and nothing came of it.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public class BroadcastParameterTests
{
	private const int Eltnen = 210020000;

	/// <summary>
	/// <c>ND2_AnA</c>. Its <c>on_attacked</c> rung fires once its quarry is under half and shouts
	/// twice — 2007 and 3009 — both <c>param_obj=OBJI_CUR_TARGET</c>.
	/// </summary>
	private const int Caller = 211675;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Eltnen).WithWorldSize(4096)
			.WithAi(typeof(BattleCycleAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	[Fact]
	public void TheShoutNamesTheCreatureItIsAbout()
	{
		using BossAiHarness harness = NewHarness();
		Npc caller = harness.Spawn(Caller, 300f, 300f, 200f);
		Player quarry = harness.SpawnPlayer(302f, 300f, 200f, race: Race.ELYOS);
		harness.Engage(caller, quarry);

		// Retail's guard: the creature it is fighting is under half.
		BossAiHarness.SetExactPercent(quarry, 40);

		var named = new List<VisibleObject?>();
		NpcMessageBus.Observer = (_, messageType, param) =>
		{
			if (messageType is 2007 or 3009)
				named.Add(param);
		};
		try
		{
			BossAiHarness.Wound(caller, quarry);
		}
		finally
		{
			NpcMessageBus.Observer = null;
		}

		Assert.NotEmpty(named);
		Assert.All(named, param => Assert.Same(quarry, param));
	}
}
