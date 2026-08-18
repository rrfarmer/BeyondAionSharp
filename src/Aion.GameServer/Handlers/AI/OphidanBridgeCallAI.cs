using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Ophidan Bridge's linked pull — the three velkur bosses and the thirteen fugitives that answer each
/// other. Retail patterns <c>BIDF5_U01_Boss_Wi</c>, <c>BIDF5_U01_Monster_01</c> and the twelve
/// <c>BIDF5_U01_Runaway_*</c> patterns, all in <c>NpcAIPatterns_IDLDF5_Under_01_JSM.xml</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Sixteen npcs, every one of them a HERO on plain
/// <c>aggressive</c>, share one branch pair that retail's own comment calls <c>애드 수신</c> — "add
/// receive":
/// <list type="table">
/// <item><term>on engaging</term><description>call everything within <b>thirty metres</b> onto whoever
/// you are fighting</description></item>
/// <item><term>on hearing the call</term><description>put <b>ten thousand</b> hate on the player named
/// and go for them</description></item>
/// </list>
/// <para>
/// <b>It chains, and that is the mechanic rather than a side effect.</b> Everything that answers enters
/// combat, and entering combat is what makes it call in turn, so one careless pull walks across the
/// bridge from group to group. It settles rather than runs away: an NPC already fighting does not
/// re-enter combat, so it does not call again.
/// </para>
/// <para>
/// <b>Ten thousand hate is retail's own number</b> and it is not decoration. It is far above anything a
/// player accumulates, so the called NPC goes to the named target and stays there — this is a hand-off,
/// not a nudge, and a called fugitive will not drift back to whoever it was already looking at.
/// </para>
/// <para>
/// <b>Normal mode does not link.</b> Spirited Velkur (235768, <c>BIDF5_U01_Boss_Wi_Nor</c>) has neither
/// half of the pair — the same fight, one mechanic lighter. Left on <c>aggressive</c> deliberately.
/// </para>
/// <para>
/// <b>Not translated.</b> The six-timer round-robin these bosses run is a cast chain: its only
/// non-cast content is three broadcasts, and <c>10200</c>'s listeners answer with a cast while
/// <c>10600</c> and <c>10700</c>'s only spawning listener is a leader our server never spawns. Their
/// four <c>despawn_by_nameid</c> triggers, <c>set_condition_spawn_variable under_01_out</c>, fifteen
/// skill indices and a shout. Each is recorded in the log with the reason.
/// </para>
/// </remarks>
[AIName("ophidan_bridge_call")]
public class OphidanBridgeCallAI : PatternAi
{
	/// <summary>Retail's <c>10500</c>: "this one is mine, help".</summary>
	public const int Call = 10500;

	/// <summary>Retail's <c>range_as_meter</c> on the call.</summary>
	private const float Reach = 30f;

	/// <summary>Retail's <c>point_to_add</c>, which is meant to end the argument about who to hit.</summary>
	private const int Decisive = 10000;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(
			Branch(1000, "", When.Always,
				Do.Broadcast(Call, Reach, aboutTarget: true))),

		OnMessage = Of(
			Branch(1300, "", [When.Message(Call)],
				Do.HateMessageTarget(Decisive))),
	};

	public OphidanBridgeCallAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
