using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Drakenspire Depths' seventeen rank-and-file wave attackers — retail patterns <c>IDSeal_Wave_Fi</c>,
/// <c>_As</c>, <c>_Ra</c>, <c>_Wi</c> and <c>_Pr</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md.
/// <para>
/// <b>The wave is a conversation, and none of it was being had.</b> Fourteen of the seventeen ran plain
/// <c>aggressive_no_loot</c>; the other three ran <see cref="WaveAttackerAI"/>, which is aionemu's
/// approximation and does something retail does not.
/// </para>
/// <para>
/// <b>What the approximation got wrong.</b> That class watches for a creature of tribe
/// <c>IDSEAL_PCGUARD</c> and then adds ten thousand hate to npcs <c>236248</c> and <c>236249</c>,
/// unconditionally, by id. Retail's rung is the same ten thousand hate for the same two npcs — 236248
/// and 236249 <em>are</em> <c>IDSeal_Forward_Guard_Li_Fi</c> and <c>_Da_Fi</c> — but it is reached by
/// <b>hearing message 22760 from one of them, at a one-in-ten chance</b>, and the hate lands on
/// <em>whichever guard spoke</em>. So the port had the right numbers wired to the wrong trigger: a
/// certainty where retail rolls, and both guards where retail picks the one that called.
/// </para>
/// <para>
/// <b>Calling for help.</b> Each attacker shouts once when it drops under seventy and once more under
/// forty — the two bands share retail's <c>BETA_1</c> and <c>BETA_2</c> flags across both the melee and
/// the spell handler, so a fighter that is being hit and cast at still calls twice, not four times.
/// Every band is guarded on <c>is_user</c>: the wave and the raid's forward guards fight each other, and
/// without the guard that brawl would set the whole room shouting.
/// </para>
/// <para>
/// <b>Which shout, and what it names,</b> is the difference between the five classes and is the reason
/// they are five patterns rather than one. The ranged pair name their attacker, the tank names
/// <em>itself</em> — it is asking to be healed, not pointing at anybody — and the priest's melee call
/// carries fifteen metres where every other call carries a hundred.
/// </para>
/// <para>
/// <b>Answering one.</b> The only call answered inside this family is the tank's: on hearing 22755 from
/// the wave <em>healer</em>, a tank already fighting the player the message names peels off to somebody
/// else. Both the healer and the assassin broadcast 22755 and only the healer's counts, which is what
/// <c>tribe_name</c> is for and why <see cref="When.SenderTribe"/> had to exist before this could be
/// written at all.
/// </para>
/// <para>
/// <b>Being dismissed.</b> Eight message numbers, 22764 through 22771, each end the wave. They are sent
/// by the scene messengers of scenes 18 through 21 in both factions' variants, and by three others; a
/// wave attacker answers all eight with <c>despawn_self</c>, which is how a room clears when the fight
/// moves on rather than when the last one dies.
/// </para>
/// <para>
/// <b>The command buff is translated now.</b> Retail's rung answers 22750 — broadcast by nine
/// <c>IDSeal_Wave*_Leader_Lv*</c> patterns, 18 of whose npcs this server carries — with
/// <c>use_skill SKILLI_INDEX_0</c> on itself. The paragraph that stood here said this port could not
/// resolve a skill index to a skill id; <c>npc_skill_lists.tsv</c> resolves them, and all 22 wave
/// attackers agree on index 0: <b>21844</b> <c>IDSeal_Wave_Buff</c>, level 56, a skill this port has
/// a template for. Unanimous across 22 npcs is not an inference.
/// </para>
/// <para>
/// <b>The ranged leaders are two different npcs, and this class used to treat them as one.</b>
/// 236219 runs <c>LeaderGourp_Wi</c> and hears 22750; 236218 runs <c>LeaderGourp_Ra</c> and does
/// <b>not</b> — it hears <c>22753</c> instead, which is a bombardment rather than a buff. Giving both
/// the buff would have handed 236218 a self-heal retail never gives it, so they are split.
/// </para>
/// <para>
/// <b>Still not translated: the bombardment.</b> Retail has 236218 answer 22753 by casting indices 1
/// and 2 — 20402 and 17315, both present here — <i>at the message sender</i>. The sender is
/// <c>IDSeal_Wave_Arrow_Target</c>, npc <b>855923</b>, which this server does spawn: the generated
/// battle table drops it on players' positions. But 855923 carries <c>ai="aggressive_no_loot"</c>, so
/// it runs no pattern and broadcasts nothing, and the rung would answer a call that never comes. The
/// missing piece is the marker's own voice, not the archer's answer.
/// </para>
/// </remarks>
[AIName("seal_wave_attacker")]
public class SealWaveAttackerAI : PatternAi
{
	/// <summary>The tank's call, naming itself: <c>IDSeal_Wave_Fi</c> only.</summary>
	public const int TankCall = 22756;

	/// <summary>The ranged pair's call: <c>IDSeal_Wave_Ra</c> and <c>_Wi</c>.</summary>
	public const int RangedCall = 22754;

	/// <summary>Sent by both <c>IDSeal_Wave_As</c> and <c>IDSeal_Wave_Pr</c>; only the priest's is answered.</summary>
	public const int MeleeCall = 22755;

	/// <summary>Broadcast by the forward guards. A one-in-ten chance of taking it personally.</summary>
	public const int GuardTaunt = 22760;

	/// <summary>Retail's <c>point_to_add=10000</c>, on the guard that spoke.</summary>
	public const int TauntHate = 10000;

	/// <summary>The leaders' command buff. Answered by every attacker except the ranged leader.</summary>
	public const int CommandBuff = 22750;

	/// <summary>
	/// <c>SKILLI_INDEX_0</c> for all 22 wave attackers, resolved from retail's own ordered list.
	/// </summary>
	public const int CommandBuffSkill = 21844;

	/// <summary>Retail's eight wave-end numbers. Every one of them means leave.</summary>
	public static readonly int[] WaveOver = [22764, 22765, 22766, 22767, 22768, 22769, 22770, 22771];

	/// <summary>Retail's <c>FLAGVARI_BETA_1</c> and <c>BETA_2</c>, shared by the melee and spell handlers.</summary>
	private const int MidBand = 1;
	private const int LowBand = 2;

	/// <summary>Retail's <c>FLAGVARI_BETA_3</c>: the leaders only, and it has no health guard.</summary>
	private const int FirstBlood = 3;

	/// <summary>Retail's usual reach. The priest's melee call is the one exception.</summary>
	private const float FarCall = 100f;
	private const float PriestMeleeCall = 15f;

	// The buff priority is retail's own, per pattern: 20 for the rank and file and the first two
	// leaders, 12 for the priest leader, 10 for the ranged one. Nothing else answers 22750, so the
	// number changes no outcome -- it is carried because it is what retail wrote.
	private static readonly AiPattern Tank =
		Build(TankCall, FarCall, FarCall, namesSelf: true, peelsOff: true, commandBuff: 20);
	private static readonly AiPattern Assassin = Build(MeleeCall, FarCall, FarCall, commandBuff: 20);
	private static readonly AiPattern Ranged = Build(RangedCall, FarCall, FarCall, commandBuff: 20);
	private static readonly AiPattern Priest = Build(MeleeCall, PriestMeleeCall, FarCall, commandBuff: 20);

	private static readonly AiPattern TankLeader =
		Build(TankCall, FarCall, FarCall, namesSelf: true, leader: true, commandBuff: 20);
	private static readonly AiPattern AssassinLeader =
		Build(MeleeCall, FarCall, FarCall, leader: true, callsOnFirstBlood: true, commandBuff: 20);
	private static readonly AiPattern PriestLeader =
		Build(MeleeCall, PriestMeleeCall, FarCall, leader: true, callsOnFirstBlood: true, commandBuff: 12);

	/// <summary>236219, <c>LeaderGourp_Wi</c>: hears the buff, at retail's priority 10.</summary>
	private static readonly AiPattern RangedLeaderWi =
		Build(RangedCall, FarCall, FarCall, leader: true, callsOnFirstBlood: true, commandBuff: 10);

	/// <summary>
	/// 236218, <c>LeaderGourp_Ra</c>: the one attacker that does <b>not</b> hear 22750. Its own number
	/// is 22753, the bombardment, which nothing on this server sends yet.
	/// </summary>
	private static readonly AiPattern RangedLeaderRa =
		Build(RangedCall, FarCall, FarCall, leader: true, callsOnFirstBlood: true);

	public SealWaveAttackerAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => GetOwner().GetNpcId() switch
	{
		236204 or 236208 or 236212 or 855844 => Tank,
		236205 or 236209 or 236213 or 855845 => Assassin,
		855847 => Priest,

		236216 => TankLeader,
		236217 => AssassinLeader,
		236220 => PriestLeader,
		236218 => RangedLeaderRa,
		236219 => RangedLeaderWi,

		_ => Ranged,
	};

	/// <summary>
	/// The nine patterns differ only in the number they shout, how far it carries, whether it names the
	/// sender or the attacker, and three flags: the tank's peel-off, the leaders' unrolled taunt, and
	/// the leaders' extra unguarded band.
	/// </summary>
	private static AiPattern Build(int call, float meleeRange, float spellRange,
		bool namesSelf = false, bool peelsOff = false, bool leader = false,
		bool callsOnFirstBlood = false, int? commandBuff = null)
	{
		// is_hp_in_boundary is exclusive at both ends, so larger_than=40 less_than=70 is 41 to 69.
		PatternAction MeleeShout() => namesSelf
			? Do.BroadcastAboutSelf(call, meleeRange)
			: Do.BroadcastAboutAttacker(call, meleeRange);
		PatternAction SpellShout() => namesSelf
			? Do.BroadcastAboutSelf(call, spellRange)
			: Do.BroadcastAboutCaster(call, spellRange);

		List<PatternBranch> onMessage = [];

		// Retail gives each dismissal its own rung, priorities 98 down to 91, highest number first.
		int priority = 98;
		foreach (int over in WaveOver.Reverse())
			onMessage.Add(Branch(priority--, "the wave is over", [When.Message(over)], Do.DespawnSelf()));

		if (peelsOff)
		{
			onMessage.Add(Branch(21, "the healer named who I am on, so get off them",
				[When.Message(MeleeCall), When.SenderTribe(TribeClass.IDSEAL_WAVE_HEALER),
					When.MessageParamIsMyTarget],
				Do.SwitchTarget(AggroTarget.RANDOM_EXCEPT_CURRENT_TARGET)));
		}

		// Retail's rung is unguarded: hear it, buff yourself. On self, and there is no despawn on this
		// branch, so the ordinary queue is the right one.
		if (commandBuff is int buffPriority)
		{
			onMessage.Add(Branch(buffPriority, "a leader called the command buff",
				[When.Message(CommandBuff)], Do.SkillOnSelf(CommandBuffSkill)));
		}

		// The leaders take the guard's shout every time; only the rank and file roll for it.
		onMessage.Add(leader
			? Branch(1, "take the guard's shout personally", [When.Message(GuardTaunt)],
				Do.HateMessageSender(TauntHate))
			: Branch(1, "one time in ten, take the guard's shout personally",
				[When.Chance(10), When.Message(GuardTaunt)],
				Do.HateMessageSender(TauntHate)));

		List<PatternBranch> onAttacked = [];
		List<PatternBranch> onSpelled = [];

		// FLAGVARI_BETA_3, and it carries NO health guard at all. First-match-wins means it takes the
		// very first player blow whatever the leader's health is, spends itself, and leaves the two
		// bands below untouched -- so a leader calls the instant it is engaged and twice more after.
		if (callsOnFirstBlood)
		{
			onAttacked.Add(Branch(4, "somebody has started on me",
				[When.AttackedByPlayer, When.FirstTime(FirstBlood)], MeleeShout()));
			onSpelled.Add(Branch(7, "somebody has started on me",
				[When.SpelledByPlayer, When.FirstTime(FirstBlood)], SpellShout()));
		}

		onAttacked.Add(Branch(3, "under forty",
			[When.HpBelow(40), When.AttackedByPlayer, When.FirstTime(LowBand)], MeleeShout()));
		onAttacked.Add(Branch(2, "under seventy",
			[When.AttackedByPlayer, When.HpBetween(41, 69), When.FirstTime(MidBand)], MeleeShout()));

		onSpelled.Add(Branch(6, "under forty",
			[When.HpBelow(40), When.SpelledByPlayer, When.FirstTime(LowBand)], SpellShout()));
		onSpelled.Add(Branch(5, "under seventy",
			[When.SpelledByPlayer, When.HpBetween(41, 69), When.FirstTime(MidBand)], SpellShout()));

		return new AiPattern
		{
			OnAttacked = [.. onAttacked],
			OnSpelled = [.. onSpelled],
			OnMessage = [.. onMessage],
		};
	}
}
