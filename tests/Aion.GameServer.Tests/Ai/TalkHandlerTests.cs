using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// <c>on_talked_by_user</c>, the last engine gap this log had verified as real and unblocked.
/// </summary>
/// <remarks>
/// Retail uses talk branches as <b>gates</b>, not conversation. The Raksha shortcut is the case that
/// named this gap: talking to the trigger teleports you only when a world flag is set, and three other
/// npcs set that flag by being cleared. <b>The flag half has been expressible since world flags were
/// built four passes ago; the talk half did not exist.</b>
/// <para>
/// Pinned on a probe rather than through an encounter, because the encounter still needs its destination
/// alias — client data this port has not extracted. <b>The capability is separable from the data</b>, and
/// this pins the capability.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class TalkHandlerTests
{
	private const int Map = 300520000;
	private const int SomeNpc = 219361;

	private static int talks;

	[AIName("talk_probe")]
	private sealed class TalkProbeAI : PatternAi
	{
		private static readonly AiPattern Pattern_ = new AiPattern
		{
			OnTalk = Of(
				Branch(10, "counts the talker", When.Always,
					Do.Custom(_ => talks++))),
		};

		public TalkProbeAI(Npc owner)
			: base(owner)
		{
		}

		protected override AiPattern Pattern => Pattern_;
	}

	/// <summary><b>A talk event reaches the pattern.</b> Before this, <c>OnTalk</c> did not exist.</summary>
	[Fact]
	public void TalkingRunsTheTalkBranches()
	{
		talks = 0;
		using BossAiHarness harness = BossAiHarness.For(Map).WithWorldSize(2048)
			.WithAi(typeof(TalkProbeAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI)).Build();
		Npc npc = harness.SpawnWithAi(SomeNpc, "talk_probe", 504f, 514f, 417.5f);
		Player player = harness.SpawnPlayer(506f, 516f, 417.5f);

		npc.GetAi().OnCreatureEvent(AiEventType.DialogStart, player);

		Assert.Equal(1, talks);
	}

	/// <summary>
	/// <b>And the talker is readable while the branches run, and not afterwards.</b> A stale talker is
	/// how a later branch would teleport the wrong player.
	/// </summary>
	[Fact]
	public void TheTalkerIsClearedAfterwards()
	{
		talks = 0;
		using BossAiHarness harness = BossAiHarness.For(Map).WithWorldSize(2048)
			.WithAi(typeof(TalkProbeAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI)).Build();
		Npc npc = harness.SpawnWithAi(SomeNpc, "talk_probe", 504f, 514f, 417.5f);
		Player player = harness.SpawnPlayer(506f, 516f, 417.5f);

		var ai = (PatternAi)npc.GetAi();
		Assert.Null(ai.Talker);

		npc.GetAi().OnCreatureEvent(AiEventType.DialogStart, player);

		Assert.Equal(1, talks);
		Assert.Null(ai.Talker);
	}
}
