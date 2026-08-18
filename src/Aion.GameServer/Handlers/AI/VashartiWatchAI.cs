using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The vasharti elite watchers and watch officers, which call their neighbours onto whoever they are
/// fighting — and keep calling. Retail pattern <c>IDYun_Temp_62</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>A watch post that engages shouts every three
/// seconds for as long as the fight lasts</b>, naming its current target at twenty-five metres, and
/// every watcher that hears it puts <b>one</b> hate point on that player and goes.
/// <para>
/// <b>One point is the whole design.</b> It is not enough to take a player off whoever they are
/// already fighting — the klaw nest's "a hundred is a claim and one is a glance" — so what this builds
/// is not a snap-aggro but a <em>drift</em>: every three seconds the whole post edges further onto one
/// target, and a group that stays too long ends up fighting all of it. A larger number would make the
/// post collapse onto the first player instantly, which is a different and much cruder fight.
/// </para>
/// <para>
/// <b>The opening shout names itself and the repeats name the target.</b> Retail's
/// <c>on_enter_attack_state</c> broadcasts <c>450</c> with <c>param_obj=OBJI_SELF</c>, so a neighbour
/// answering it tries to put hate on a friend and the aggro list refuses — the opening call is
/// effectively an "I am fighting" with no payload, and the timer that follows is what does the work.
/// Translated exactly as written rather than tidied into a second target call, because the difference
/// is one wasted broadcast in retail and would be one extra player pulled here.
/// </para>
/// <para>
/// <b>Not translated:</b> message <c>900</c>, which a dying watcher broadcasts at twenty-five metres.
/// Nothing in the entire 5.8 dump listens for it — the pattern is its own only sender and its own only
/// listener, and <c>900</c> appears on neither side of anything else. Left out rather than given an
/// invented meaning.
/// </para>
/// </remarks>
[AIName("vasharti_watch")]
public class VashartiWatchAI : PatternAi
{
	/// <summary>Retail's <c>450</c>: onto this one.</summary>
	public const int OntoThisOne = 450;

	/// <summary>Retail's <c>range_as_meter</c> on every broadcast in the pattern.</summary>
	private const float Reach = 25f;

	/// <summary>Retail's <c>BTIMERI_INDEX_0</c> and its <c>delay</c>.</summary>
	private const int Beat = 0;
	private const int BeatMillis = 3000;

	/// <summary>Retail's <c>point_to_add</c>. A glance, not a claim.</summary>
	private const int Glance = 1;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(Branch(2, "raise the post",
			[],
			Do.Broadcast(OntoThisOne, Reach),
			Do.ArmTimer(Beat, BeatMillis))),

		OnBattleTimer = Of(Branch(1, "and again, every three seconds",
			[When.Timer(Beat)],
			Do.ArmTimer(Beat, BeatMillis),
			Do.Broadcast(OntoThisOne, Reach, aboutTarget: true))),

		OnMessage = Of(Branch(3, "onto the one they named",
			[When.Message(OntoThisOne)],
			Do.HateMessageTarget(Glance))),
	};

	public VashartiWatchAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
