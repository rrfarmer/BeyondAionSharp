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
/// <b>They march in now.</b> Retail hangs a <c>pathname</c> on every one of the eight
/// <c>BIDRuneWP_Main_CallVritra*</c> spawn actions -- the plain callers use
/// <c>NPCPathVriAss_Path01</c> and the B callers <c>NPCPathVriAss_Path02</c> -- so a trooper appears at
/// its caller's feet and then walks a ten-point lane to the objective. Neither lane was in our data; both
/// are now <c>300800000_Infinity_Shard.xml</c>. <b>Each trooper npc id belongs to exactly one lane</b>,
/// which is what lets the trooper find its own route instead of the caller having to hand it one.
/// <para>
/// At the end of the lane retail runs <c>attack_most_hating</c>. The half of that which matters even
/// against an empty hate list is <b>stopping</b>: these routes carry no <c>loop_type</c>, so without it
/// the walker sends the trooper back to the start of the lane and the march never ends.
/// </para>
/// <para>
/// <b>Not translated.</b> Everything else these twelve patterns do, which is a great deal of casting;
/// and the two invisible controllers that also answer <c>21101</c>
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

		// Retail: priority 12 on is_last_waypoint, attack_most_hating; priority 11 walks on. The walker
		// does the walking on, so only the end of the lane needs saying.
		OnArrivedAtWaypoint = Of(
			Branch(12, "", [When.AtLastWaypoint], Do.AttackMostHating())),
	};

	public HyperionDefenceAI(Npc owner)
		: base(owner)
	{
	}

	/// <summary>
	/// Puts the trooper on its lane. The callers spawn these at runtime, so there is no spawn row to carry
	/// a <c>walker_id</c> and the route has to be attached here instead.
	/// </summary>
	/// <remarks>
	/// The lane comes from <see cref="VritraCallers.LaneOf"/>, which is generated from the same spawn
	/// actions as the coordinates beside it. It was a hand-written copy in this class for one commit,
	/// which is one commit of somebody being able to edit one of the two and not the other.
	/// </remarks>
	protected override void HandleSpawned()
	{
		base.HandleSpawned();
		if (VritraCallers.LaneOf.TryGetValue(GetNpcId(), out string? lane))
		{
			GetSpawnTemplate().SetWalkerId(lane);
			Aion.GameServer.Ai.Manager.WalkManager.StartWalking(this);
		}
	}

	protected override AiPattern Pattern => Pattern_;
}
