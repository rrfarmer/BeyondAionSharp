using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Queen Alukina (213747), Beluslan. Retail pattern <c>ND2_FhM</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Her adds were on <c>summoner</c> health bands, which
/// is the wrong mechanism twice over.
/// <para>
/// <b>Not to be confused with <see cref="QueenAlukinaAI"/>.</b> There are two Queen Alukinas: 213747 here
/// in Beluslan on <c>ND2_FhM</c>, and 217590 in the Empyrean Crucible on <c>IDArena_S8_Named_3</c>, which
/// is a separate class under <c>alukina_emp</c>. They share a name, a tribe and a death spawn of seven
/// azure blobbles, and nothing else. This file exists separately for that reason.
/// </para>
/// <para>
/// <b>The faithful servant lands on a player, not on her.</b> Every one of retail's four spawn rungs uses
/// <c>spawn_on_target target_obj=OBJI_CUR_TARGET</c> with <c>spawn_range=2</c> — one servant, at the feet
/// of whoever she is fighting, followed by a <c>switch_target</c> away. Our data placed <b>three at
/// distance 10 from the queen</b>, in three identical bands at 80, 60 and 40. Same npc, entirely
/// different fight: the servant is a thing that appears on you, and it was appearing in a huddle across
/// the room.
/// </para>
/// <para>
/// <b>And seven azure blobbles arrive when she dies.</b> <c>on_killed_by_user</c> puts down seven 280713
/// at her own point for thirty seconds. Nothing in this port did that, and nothing could have: the
/// <c>&lt;summons&gt;</c> schema is keyed on health percentage, so a death spawn cannot be written in it.
/// That is the whole reason this is a class rather than a data edit.
/// </para>
/// <para>
/// <b>The servant cycle is a timer chain, not a ladder, and it does not start until fifty-five.</b>
/// Timer 5 arms timer 4 at nine seconds and timer 4 arms timer 5 at twenty-five, each spawning one
/// servant, so it is a two-beat loop — but nothing arms either slot except the rung that crosses
/// fifty-five. Above that she summons nothing at all, which a first reading of the 27-to-99 guard on the
/// loop rungs gets exactly backwards. Retail gives the two spawns different lifetimes — 1800 seconds
/// and 600 — which is kept even though both outlive any plausible fight, because the difference is
/// retail's and inventing agreement between them would be a change.
/// </para>
/// <para>
/// <b>Below twenty-five she stops summoning.</b> The rung at priority 9 despawns the whole group and
/// arms the closing timer instead, and no spawn rung can fire under 27. Her last quarter is deliberately
/// add-free, which the three-band ladder inverted: it summoned <i>most</i> at 40.
/// </para>
/// <para>
/// <b>Not translated: every <c>use_skill</c> and shout.</b> Seven skill indices are addressed across the
/// pattern (0 through 6) and this port cannot resolve a pattern's skill index to a skill id — the
/// standing blocker recorded in docs/retail-ai-fidelity.md. What is here is index-free: the timers, the
/// bands, the spawns, the despawns and the target switches.
/// </para>
/// </remarks>
[AIName("alukina_beluslan")]
public class QueenAlukinaBeluslanAI : PatternAi
{
	/// <summary>Retail's <c>SPAWN_ID_1</c>: the servants, cleared on reset and at twenty-five.</summary>
	private const int Servants = 1;

	/// <summary>Retail's <c>SPAWN_ID_2</c>: the death blobbles, which nothing ever clears.</summary>
	private const int Blobbles = 2;

	private const int FaithfulServant = 280712;
	private const int AzureBlobble = 280713;

	/// <summary>Retail's <c>ALPHA_1</c>, <c>ALPHA_2</c> and <c>ALPHA_3</c>: the three phase gates.</summary>
	private const int Below80 = 1;
	private const int Below55 = 2;
	private const int Below25 = 3;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(
			Branch(13, "", When.Always,
				Do.ArmTimer(0, 8000),
				Do.ArmTimer(1, 18000))),

		// Retail clears only SPAWN_ID_1 here. The blobbles are not hers to clear -- she is dead when
		// they arrive -- and they carry their own thirty-second lifetime instead.
		OnLeaveAttack = Of(
			Branch(15, "", When.Always,
				Do.Despawn(Servants))),

		OnDie = Of(
			Branch(14, "seven blobbles, for thirty seconds", When.Always,
				Do.Despawn(Servants),
				Do.SpawnNear(AzureBlobble, Blobbles, count: 7, range: 2f, liveSeconds: 30))),

		OnBattleTimer = Of(
			// The closing loop, armed once she crosses twenty-five and never carrying a spawn.
			Branch(12, "", [When.Timer(6)],
				Do.ArmTimer(6, 20000),
				Do.SwitchTarget(AggroTarget.RANDOM)),

			Branch(9, "below twenty-five the servants stop",
				[When.Timer(0), When.HpBelow(25), When.FirstTime(Below25)],
				Do.ArmTimer(6, 20000),
				Do.Despawn(Servants),
				Do.SwitchTarget(AggroTarget.MOST_HATED)),

			// The two-beat servant loop. is_hp_in_boundary is exclusive at both ends, so
			// larger_than=26 less_than=100 is 27 to 99.
			Branch(8, "a servant on the target", [When.Timer(5), When.HpBetween(27, 99)],
				Do.ArmTimer(4, 9000),
				Do.SpawnOnTarget(FaithfulServant, Servants, count: 1, range: 2f, liveSeconds: 1800),
				Do.SwitchTarget(AggroTarget.RANDOM)),

			Branch(7, "and again", [When.Timer(4), When.HpBetween(27, 99)],
				Do.ArmTimer(5, 25000),
				Do.SpawnOnTarget(FaithfulServant, Servants, count: 1, range: 2f, liveSeconds: 600),
				Do.SwitchTarget(AggroTarget.RANDOM)),

			// Crossing fifty-five starts the loop running faster and brings a servant with it.
			Branch(6, "crossing fifty-five",
				[When.Timer(0), When.HpBetween(27, 54), When.FirstTime(Below55)],
				Do.ArmTimer(0, 10000),
				Do.ArmTimer(4, 12000),
				Do.SpawnOnTarget(FaithfulServant, Servants, count: 1, range: 2f, liveSeconds: 1800),
				Do.SwitchTarget(AggroTarget.RANDOM)),

			Branch(4, "", [When.Timer(2), When.HpBetween(57, 99)],
				Do.ArmTimer(2, 25000),
				Do.SwitchTarget(AggroTarget.MOST_HATED)),

			Branch(3, "crossing eighty",
				[When.Timer(0), When.HpBetween(57, 79), When.FirstTime(Below80)],
				Do.ArmTimer(0, 14000),
				Do.ArmTimer(2, 19000),
				Do.SwitchTarget(AggroTarget.RANDOM)),

			Branch(2, "", [When.Timer(1), When.HpBetween(82, 99)],
				Do.ArmTimer(1, 18000)),

			// The heartbeat, last so every band above gets first refusal on timer 0.
			Branch(1, "", [When.Timer(0)],
				Do.ArmTimer(0, 6000))),
	};

	public QueenAlukinaBeluslanAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
