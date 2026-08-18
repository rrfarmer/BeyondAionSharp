using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Hyperion's defence force — twenty-two npcs across twelve retail patterns, the
/// <c>IDRuneWP_Main_*</c> family. Retail message <c>21101</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. One branch, and it is the same branch in all twelve
/// patterns: <b>when Hyperion goes, they go.</b> He broadcasts at fifty metres as he dies and as he
/// leaves the fight, and combatants, assaulters, medics, healers, snipers, marksmen, scouts,
/// assassins, sorcerers, mages, a turret and a summoned tyrhund all answer with
/// <c>despawn_self</c>.
/// <para>
/// <b>Found by audit rather than by reading.</b> <c>audit_message_senders.py</c> reported twelve
/// listener patterns waiting on a number whose only sender runs a bespoke class of ours that never
/// mentioned it — the third gap of that exact shape, after Modor's obscura and the Sauro guards, and
/// the first one the audit caught before a human did.
/// </para>
/// <para>
/// <b>Not translated.</b> Everything else these twelve patterns do, which is a great deal of casting
/// and a good deal of walking; and the two invisible controllers that also answer <c>21101</c>
/// (<c>BIDRuneWP_CtrlCharger_NoShowNPC</c> and <c>BIDRuneWP_CtrlLimitTime_NoShowNPC</c>), neither of
/// which our data spawns.
/// </para>
/// </remarks>
[AIName("hyperion_defence")]
public class HyperionDefenceAI : PatternAi
{
	/// <summary>Retail's <c>21101</c>: Hyperion is finished, one way or the other.</summary>
	public const int StandDown = 21101;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnMessage = Of(
			Branch(1, "", [When.Message(StandDown)],
				Do.DespawnSelf())),
	};

	public HyperionDefenceAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
