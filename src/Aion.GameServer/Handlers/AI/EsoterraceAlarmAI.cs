using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The Esoterrace surkana feeder and the lab that hears it. Retail number <c>10000</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>The feeder is a machine, not a monster</b> — a
/// field object bolted to the drana line — and beating it brings the drakan down in instalments rather
/// than all at once.
/// <para>
/// <b>Five bands, each spent once.</b> The feeder calls on any blow at all, and again at eighty, sixty,
/// forty and twenty percent; every call carries its own flag, so the whole ladder is five calls across
/// the object's life and never a sixth. Twenty drakan hear it inside thirty metres and each adds ten to
/// the attacker before turning to fight, so a raid that works the feeder down brings the lab in waves
/// of its own making.
/// </para>
/// <para>
/// <b>This is the encounter that found the pure-broadcaster bug.</b> Its bands answer a blow with
/// nothing but a broadcast, so the feeder takes no hate, never reaches <c>FIGHT</c>, and was sent home
/// after every blow — clearing the flags the whole ladder rests on. See <c>PatternAi.HandleBackHome</c>.
/// </para>
/// </remarks>
public static class EsoterraceAlarm
{
	/// <summary>Retail's <c>10000</c>: the feeder's alarm.</summary>
	public const int Alarm = 10000;

	/// <summary>Retail's <c>range_as_meter</c> on every band.</summary>
	public const float Reach = 30f;

	/// <summary>Each answerer's <c>point_to_add</c>. Twenty of them answer.</summary>
	public const int Notice = 10;
}

/// <summary>
/// The surkana feeder. Retail pattern <c>IDF4Re_FOBJ_1</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>The lowest band has no health guard.</b> Retail
/// writes four thresholds and then a bare fifth branch, so the very first blow raises the lab and the
/// four bands below it raise it again as the feeder falls. The bare branch does not swallow the
/// thresholds above it only because the bands are priority-ordered and evaluation stops at the first
/// match.
/// <para>
/// <b>Not translated:</b> the <c>on_die</c> pair — <c>set_condition_spawn_variable condition_type=2</c>,
/// which drives the instance's own spawn progression, and the <c>display_system_message</c> that goes
/// with it. Neither has an equivalent here. Also its <c>on_message</c> answer to <c>1001</c>, a
/// <c>despawn_self</c> whose only live callers belong to other instances entirely — one of the thirteen
/// cross-wired numbers the conversation audit now quarantines.
/// </para>
/// </remarks>
[AIName("surkana_feeder")]
public class SurkanaFeederAI : PatternAi
{
	private static PatternBranch Band(int priority, string comment, int percent, int flag, bool spell)
		=> Branch(priority, comment,
			percent > 0 ? [When.HpBelow(percent), When.FirstTime(flag)] : [When.FirstTime(flag)],
			spell
				? Do.BroadcastAboutCaster(EsoterraceAlarm.Alarm, EsoterraceAlarm.Reach)
				: Do.BroadcastAboutAttacker(EsoterraceAlarm.Alarm, EsoterraceAlarm.Reach));

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnAttacked = Of(
			Band(10, "a fifth left", 20, 1, spell: false),
			Band(9, "two fifths", 40, 2, spell: false),
			Band(8, "three fifths", 60, 3, spell: false),
			Band(7, "four fifths", 80, 4, spell: false),
			Band(6, "any blow at all", 0, 5, spell: false)),

		OnSpelled = Of(
			Band(10, "a fifth left", 20, 1, spell: true),
			Band(9, "two fifths", 40, 2, spell: true),
			Band(8, "three fifths", 60, 3, spell: true),
			Band(7, "four fifths", 80, 4, spell: true),
			Band(6, "any cast at all", 0, 5, spell: true)),
	};

	public SurkanaFeederAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The esoterrace drakan who answer it. Retail patterns <c>IDF4Re_Drana_*</c> and
/// <c>IDF4Re_KeyNamed_4</c>, <c>_5</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>Sixteen patterns, one answer</b> — villagers, lab
/// staff, the senior researcher and the supervisor all add ten to whoever the feeder named and then
/// attack their most hated. Ten is small on its own; twenty of them answering at once is the mechanic.
/// </remarks>
[AIName("esoterrace_drakan")]
public class EsoterraceDrakanAI : PatternAi
{
	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnMessage = Of(Branch(1, "the feeder is calling", [When.Message(EsoterraceAlarm.Alarm)],
			Do.HateMessageTarget(EsoterraceAlarm.Notice),
			Do.SwitchTarget(AggroTarget.MOST_HATED))),
	};

	public EsoterraceDrakanAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
