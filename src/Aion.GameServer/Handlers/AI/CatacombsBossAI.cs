using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The Catacombs bosses' threat rule: a templar hitting one of them generates thousands of extra hate
/// points. Retail patterns <c>IDCT_Boss_TombsDrakan</c>, <c>_Hard</c>,
/// <c>IDCT_Boss_ElementalFire_Hard</c>, <c>IDCT_Boss_DeathKnight</c> and <c>_Hard</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>This is retail's threat assistance for tanks, and
/// it is invisible in the client.</b> Nothing is cast, nothing is said; a
/// <c>CLASSI_KNIGHT</c> attacker simply counts for far more on the aggro list than anyone else, which
/// is how a templar holds one of these bosses off the group. Without it a Catacombs boss is held by
/// whoever does the most damage — a materially different fight, and one nobody would think to file as
/// a bug.
/// <para>
/// <b>Five bosses, one rule, four weights</b>, and the weights are not ordered the way the difficulty
/// is:
/// </para>
/// <list type="table">
/// <item><term>Taros Lifebane, normal</term><description><b>35,000</b></description></item>
/// <item><term>Captain Lakhara, both modes</term><description><b>22,000</b></description></item>
/// <item><term>Flarestorm, hard</term><description><b>5,000</b></description></item>
/// <item><term>Taros Lifebane, <em>hard</em></term><description><b>5,000</b></description></item>
/// </list>
/// <para>
/// <b>Taros Lifebane's hard mode gives a templar seven times less help than his normal one</b>, and
/// Captain Lakhara's two modes give the same. Both are retail's numbers, read from the dump, and the
/// first is the kind of asymmetry a shared constant would quietly erase — which is why this is a
/// weight per class rather than one figure for the instance.
/// </para>
/// <para>
/// <c>CLASSI_KNIGHT</c> is <see cref="PlayerClass.TEMPLAR"/>: the enum carries the client's own naming
/// in its comments, beside <c>GLADIATOR, // fighter</c> and <c>SORCERER, // wizard</c>.
/// </para>
/// <para>
/// <b>Not translated:</b> everything else these five patterns do — their timer chains are skill
/// indices, their shouts are shouts, and <c>control_door</c> and the condition spawn variables on death
/// belong to an instance handler.
/// </para>
/// </remarks>
public abstract class CatacombsBossAI : PatternAi
{
	/// <summary>Retail's <c>is_user_class</c> / <c>add_hate_point target=OBJI_ATTACKER</c>.</summary>
	protected static AiPattern Build(int extraHateForTemplars) => new AiPattern
	{
		OnAttacked = Of(Branch(1, "a knight is holding it",
			[When.AttackerClass(PlayerClass.TEMPLAR)],
			Do.HateAttacker(extraHateForTemplars))),
	};

	protected CatacombsBossAI(Npc owner)
		: base(owner)
	{
	}
}

/// <summary>Taros Lifebane (216248). Retail <c>IDCT_Boss_DeathKnight</c>.</summary>
[AIName("catacombs_boss_35k")]
public class CatacombsBoss35kAI : CatacombsBossAI
{
	private static readonly AiPattern Pattern_ = Build(35_000);

	public CatacombsBoss35kAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>Captain Lakhara, both modes (216238, 216157). Retail <c>IDCT_Boss_TombsDrakan</c>.</summary>
[AIName("catacombs_boss_22k")]
public class CatacombsBoss22kAI : CatacombsBossAI
{
	private static readonly AiPattern Pattern_ = Build(22_000);

	public CatacombsBoss22kAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// Flarestorm (216168) and Taros Lifebane's hard mode (216167). Retail
/// <c>IDCT_Boss_ElementalFire_Hard</c> and <c>IDCT_Boss_DeathKnight_Hard</c>.
/// </summary>
[AIName("catacombs_boss_5k")]
public class CatacombsBoss5kAI : CatacombsBossAI
{
	private static readonly AiPattern Pattern_ = Build(5_000);

	public CatacombsBoss5kAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
