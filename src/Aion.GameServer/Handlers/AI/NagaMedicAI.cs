using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The twenty-nine naga medics of retail patterns <c>Naga_PeA1</c> through <c>_PeA4</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md.
/// <para>
/// <b>They ran <see cref="DrakanMedicAI"/>, which summons the wrong creature at the wrong time.</b> That
/// class rolls three percent on <em>every swing</em> and, on a hit, calls a <em>drakan</em> servant —
/// 281621 or 281839 depending on rating. Retail's naga medics call a <b>naga</b> servant, level-matched
/// to the medic (280638 at PeA1, 280639 at PeA2, 280640 at PeA3, 281301 at PeA4), and they do it on two specific
/// occasions rather than at random.
/// </para>
/// <para>
/// <b>The two occasions.</b> Once, the first time the medic falls below eighty-five percent — retail
/// guards it with <c>ALPHA_2</c>, so a medic that is pushed down and healed back up does not get a
/// second one. And again on a fifteen-second timer that <b>only exists if somebody asks for it</b>:
/// timer 2 is armed by message <c>3306</c> and by nothing else, so a lone medic never runs it. Both
/// servants live four minutes, carry <c>despawn_at_attack_state</c>, and are filed under retail's
/// <c>SPAWN_ID_1</c> so that leaving combat clears them.
/// </para>
/// <para>
/// <b>It talks the whole time.</b> Four broadcasts at four health bands — 3302 as it opens, 3303 with
/// the servant at eighty-five, 3304 below fifty-five and 3305 below twenty-six — each naming its current
/// target except the first, which names itself. Nothing in this tree answers them yet; they are sent
/// because a medic that does not speak cannot be answered later.
/// </para>
/// <para>
/// <b>Not translated:</b> every <c>use_skill</c>, which is all of the healing this npc is named for —
/// the friend-heal below fifty on <c>on_see_friend_attacked</c>, the dispel pair on
/// <c>on_friend_spelled</c>, and the self-buffs on the timer rungs. Also the two shouts, and
/// <c>use_skill_by_attacker_indicator ATTACKERI_HAS_LOWEST_HP</c> on the mid band — the target switch
/// beside it is translated, so the medic still turns on the weakest player, it just does not hit them
/// with anything special when it arrives.
/// </para>
/// </remarks>
[AIName("naga_medic")]
public class NagaMedicAI : PatternAi
{
	/// <summary>Retail's <c>BD3_Naga_Servant_44/46/48/50_Ae</c> — one per medic tier.</summary>
	/// <remarks>
	/// <b>Seven of these were found only by the reverse audit.</b> The first pass grouped by npcs already
	/// carrying <c>ai="drakanmedic"</c>, so the seven that sat on plain <c>aggressive</c> -- the "New" and
	/// "tune" revisions in Heiron and the sixth Brigade -- were invisible to it. Starting from the retail
	/// pattern rather than from this port's class is the only way round that, and is what
	/// <c>audit_odd_ai.py --reverse</c> does.
	/// <para>
	/// <b>There are four tiers, not three.</b> <c>Naga_PeA4</c> binds a single npc (281300) and was
	/// missed on the first pass because the audit that found this family groups by how many npcs run a
	/// pattern, and a pattern with one npc sorts to the bottom. It summons <c>BD3_Naga_Servant_50_Ae</c>
	/// and is otherwise identical to the other three.
	/// </para>
	/// </remarks>
	public const int ServantLv44 = 280638;
	public const int ServantLv46 = 280639;
	public const int ServantLv48 = 280640;
	public const int ServantLv50 = 281301;

	/// <summary>Retail's <c>SPAWN_ID_1</c>: cleared when the medic leaves combat, and only then.</summary>
	private const int Servants = 1;

	/// <summary>Retail's <c>live_time=240</c>. Four minutes is most of a fight.</summary>
	private const int ServantLife = 240;

	/// <summary>The one-shot below eighty-five uses a tighter ring than the timer does.</summary>
	private const float CloseRing = 4f;
	private const float WideRing = 5f;

	/// <summary>Retail's <c>FLAGVARI_ALPHA_1</c> and <c>ALPHA_2</c>.</summary>
	private const int Opened = 1;
	private const int CalledForHelp = 2;

	/// <summary>
	/// Message <c>3306</c> is the only thing that arms timer 2, and timer 2 is the only repeating
	/// servant. A medic nobody signals summons exactly once all fight.
	/// </summary>
	public const int SendMeHelp = 3306;

	/// <summary>What the medic says, by band. It is the only part of its voice this port can carry.</summary>
	public const int Opening = 3302;
	public const int ServantCalled = 3303;
	public const int BelowFiftyFive = 3304;
	public const int BelowTwentySix = 3305;

	private static AiPattern For(int servant) => new AiPattern
	{
		OnEnterAttack = Of(
			Branch(11, "start the clock", When.Always,
				Do.ArmTimer(0, 7000))),

		OnMessage = Of(
			Branch(9, "somebody asked for a servant", [When.Message(SendMeHelp)],
				Do.ArmTimer(2, 15000))),

		OnLeaveAttack = Of(
			Branch(13, "the servants go when the fight does", When.Always,
				Do.Despawn(Servants))),

		OnBattleTimer = Of(
			Branch(12, "the asked-for servant", [When.Timer(2)],
				Do.SpawnNear(servant, Servants, count: 1, range: WideRing, liveSeconds: ServantLife)),

			Branch(10, "", [When.Timer(4)], Do.ArmTimer(4, 16000)),

			Branch(9, "below twenty-six", [When.Timer(1), When.HpBelow(26)],
				Do.ArmTimer(4, 16000),
				Do.Broadcast(BelowTwentySix, 10f, aboutTarget: true)),

			// is_hp_in_boundary is exclusive at both ends: larger_than=27 less_than=55 is 28 to 54.
			Branch(8, "below fifty-five, and go for the weakest",
				[When.Timer(1), When.HpBetween(28, 54)],
				Do.ArmTimer(1, 10000),
				Do.Broadcast(BelowFiftyFive, 10f, aboutTarget: true),
				Do.SwitchTarget(AggroTarget.LOWEST_HP)),

			// The one-shot. Retail puts the health test FIRST, so the flag is only spent by a medic
			// that is actually below eighty-five -- a full-health medic does not quietly consume it.
			Branch(7, "the first time it is hurt, a servant",
				[When.HpBelow(85), When.Timer(1), When.FirstTime(CalledForHelp)],
				Do.ArmTimer(1, 10000),
				Do.SpawnNear(servant, Servants, count: 1, range: CloseRing, liveSeconds: ServantLife),
				Do.Broadcast(ServantCalled, 5f, aboutTarget: true)),

			Branch(6, "opening", [When.Timer(0), When.HpBetween(87, 99), When.FirstTime(Opened)],
				Do.ArmTimer(1, 9000),
				Do.BroadcastAboutSelf(Opening, 8f)),

			Branch(1, "", [When.Timer(1)], Do.ArmTimer(1, 6000))),
	};

	private static readonly AiPattern Lv44 = For(ServantLv44);
	private static readonly AiPattern Lv46 = For(ServantLv46);
	private static readonly AiPattern Lv48 = For(ServantLv48);
	private static readonly AiPattern Lv50 = For(ServantLv50);

	public NagaMedicAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => GetOwner().GetNpcId() switch
	{
		213676 or 213677 or 214012 or 280635 or 235432 or 235433 => Lv44,
		213433 or 213497 or 214015 or 214123 or 214859 or 280637 or 235489 => Lv48,
		281300 => Lv50,
		_ => Lv46,
	};
}
