using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Archmagus Sayahum (233257) of the Sauro Supply Base. Retail pattern
/// <c>IDVritra_Base_Drakan_Wi_IU_Nmd</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. The third of the base's named drakan, after Chief
/// Gunner Kurmata and Darkblade Ovanuka, and the only one of the three whose whole fight is about
/// <b>who he is looking at</b>. He has no summons, no marks and no messages: three phases of a cast
/// ring, and a turn onto somebody new at set points in each.
/// <list type="table">
/// <item><term>above eighty</term><description>a four-step ring of about thirty-two seconds, and he
/// turns on <b>every other lap</b></description></item>
/// <item><term>crossing eighty</term><description>a turn onto somebody <b>other than</b> his current
/// target, and a new four-step ring</description></item>
/// <item><term>the same again below forty-five</term><description>a five-step ring, and the turn is
/// now on <b>every</b> lap</description></item>
/// </list>
/// <para>
/// <b>The escalation is in how often he turns, not in what he casts.</b> Retail writes the alternation
/// as a flag toggled between two branches on one timer: the lap that finds the flag set turns and
/// clears it, the lap that finds it clear sets it and does not. Below forty-five that pair is gone and
/// a single unconditional branch takes its place, so the turn rate doubles without a word about
/// enraging — the same trick as the Infernomane Vortile's shrinking loop, done to the target instead of
/// to the count.
/// </para>
/// <para>
/// <b>Both crossings turn him off his current target specifically.</b> They use
/// <c>ATTACKERI_RANDOM_ONE_EXCEPT_CURRENT_TARGET</c> where the in-ring turns use plain
/// <c>RANDOM_ONE</c>, so a phase change always moves him and an ordinary lap may not. That distinction
/// is worth keeping: it is the difference between "he might turn" and "he is off you now".
/// </para>
/// <para>
/// <b>The ladder stops below forty-five.</b> The phase-three opener does not re-arm the heartbeat, so
/// nothing checks his health again — which is retail's way of saying the last phase is the last phase.
/// </para>
/// <para>
/// <b>Not translated.</b> Nineteen skill indices and four shouts. Every branch here carries one or two
/// casts that are the visible half of the fight; what is left is the shape of it.
/// </para>
/// </remarks>
[AIName("archmagus_sayahum")]
public class ArchmagusSayahumAI : PatternAi
{
	/// <summary>Retail's <c>FLAGVARI_ALPHA_1</c> and <c>ALPHA_2</c>: the two phase crossings.</summary>
	private const int Below80 = 1;
	private const int Below45 = 2;

	/// <summary>Retail's <c>ALPHA_3</c> and <c>ALPHA_4</c>: the every-other-lap toggles.</summary>
	private const int Lap1 = 3;
	private const int Lap2 = 4;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(
			Branch(2, "", When.Always,
				Do.ArmTimer(0, 5000),
				Do.ArmTimer(1, 8000))),

		OnBattleTimer = Of(
			// Phase three: five steps, and the turn comes round every lap.
			Branch(20, "", [When.Timer(13)], Do.ArmTimer(9, 8000)),
			Branch(19, "", [When.Timer(12)], Do.ArmTimer(13, 8000)),
			Branch(18, "and turns every lap", [When.Timer(11)],
				Do.ArmTimer(12, 12000),
				Do.SwitchTarget(AggroTarget.RANDOM)),
			Branch(17, "", [When.Timer(10)], Do.ArmTimer(11, 8000)),
			Branch(16, "", [When.Timer(9)], Do.ArmTimer(10, 8000)),

			// Below forty-five, and the heartbeat is not re-armed: this is the last phase.
			Branch(15, "below forty-five", [When.Timer(0), When.HpBelow(45), When.FirstTime(Below45)],
				Do.ArmTimer(9, 14000),
				Do.SwitchTarget(AggroTarget.RANDOM_EXCEPT_CURRENT_TARGET)),

			// Phase two: four steps, and the turn on every other lap.
			Branch(14, "", [When.Timer(8), When.HpBetween(46, 80)], Do.ArmTimer(5, 10000)),
			Branch(13, "", [When.Timer(7), When.HpBetween(46, 80)], Do.ArmTimer(8, 8000)),
			Branch(12, "and turns", [When.Timer(6), When.HpBetween(46, 80), When.Consuming(Lap2)],
				Do.ArmTimer(7, 8000),
				Do.SwitchTarget(AggroTarget.RANDOM)),
			Branch(11, "", [When.Timer(6), When.HpBetween(46, 80), When.FirstTime(Lap2)],
				Do.ArmTimer(7, 8000)),
			Branch(10, "", [When.Timer(5), When.HpBetween(46, 80)], Do.ArmTimer(6, 8000)),

			Branch(9, "crossing eighty", [When.Timer(0), When.HpBelow(80), When.FirstTime(Below80)],
				Do.ArmTimer(0, 5000),
				Do.ArmTimer(5, 12000),
				Do.SwitchTarget(AggroTarget.RANDOM_EXCEPT_CURRENT_TARGET)),

			// Phase one: four steps, and the same every-other-lap turn.
			Branch(8, "", [When.Timer(4), When.HpBetween(81, 100)], Do.ArmTimer(1, 8000)),
			Branch(7, "", [When.Timer(3), When.HpBetween(81, 100)], Do.ArmTimer(4, 8000)),
			Branch(6, "", [When.Timer(2), When.HpBetween(81, 100)], Do.ArmTimer(3, 8000)),
			Branch(5, "and turns", [When.Timer(1), When.HpBetween(81, 100), When.Consuming(Lap1)],
				Do.ArmTimer(2, 8000),
				Do.SwitchTarget(AggroTarget.RANDOM)),
			Branch(4, "", [When.Timer(1), When.HpBetween(81, 100), When.FirstTime(Lap1)],
				Do.ArmTimer(2, 8000)),

			Branch(3, "", [When.Timer(0)], Do.ArmTimer(0, 5000))),
	};

	public ArchmagusSayahumAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
