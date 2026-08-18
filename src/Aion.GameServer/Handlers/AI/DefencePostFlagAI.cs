using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Ophidan Bridge's four defence post generators (230413–230416). Retail patterns
/// <c>IDF5_Under_01_VriFlag_01</c> through <c>_04</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. A flag that takes one point of damage a hit and
/// never fights back, and which shouts twice while it is being taken:
/// <list type="table">
/// <item><term>as the fight starts</term><description><c>21212</c> at <b>thirty-five</b> metres —
/// its guards take a hundred hate on the player and go</description></item>
/// <item><term>on every blow after that</term><description><c>21215</c> at <b>fifty</b> metres — its
/// guards <b>turn</b> towards whoever landed it, and nothing more</description></item>
/// </list>
/// <para>
/// <b>Two calls with two weights, and the difference is the mechanic.</b> The first commits the post
/// to whoever pulled the flag; the second only points. A raid splitting damage across the flag and its
/// guards is being redirected by the second call and held by the first, and a class that sent one
/// number for both would lose that entirely.
/// </para>
/// <para>
/// <b>It keeps the Java class it had.</b> <c>onedmg_passive</c> is shared by a hundred and twelve npcs,
/// so the calls could not go there; this class extends <see cref="OneDmgNoActionAI"/> instead and adds
/// nothing but the two broadcasts, so the one-damage rule and the stat suppression beside it are
/// untouched.
/// </para>
/// <para>
/// <b>Not translated.</b> The four <c>set_condition_spawn_variable</c> on its death and one more on a
/// sensory area — the bridge's own progression, which belongs to an instance handler — and the system
/// message that goes with them.
/// </para>
/// </remarks>
[AIName("defence_post_flag")]
public class DefencePostFlagAI : OneDmgNoActionAI
{
	/// <summary>Retail's <c>21212</c>: the post is under attack, commit.</summary>
	public const int PostUnderAttack = 21212;

	/// <summary>Retail's <c>21215</c>: that one, there.</summary>
	public const int ThatOneThere = 21215;

	private const float CommitReach = 35f;
	private const float PointReach = 50f;

	/// <summary>
	/// Retail's <c>on_enter_attack_state</c>, which for a flag means the first blow.
	/// </summary>
	/// <remarks>
	/// <see cref="CombatAlarm"/> would have been the obvious home for this and is the wrong shape: it
	/// names the owner's <em>target</em> as the message parameter, and a flag that never fights has
	/// none. Retail names the attacker on both calls, so the latch is a bool here and the parameter is
	/// the creature that landed the blow.
	/// </remarks>
	private bool opened;

	public DefencePostFlagAI(Npc owner)
		: base(owner)
	{
	}

	protected override void HandleAttack(Creature creature)
	{
		base.HandleAttack(creature);

		// The opening call once, and the pointing call on every blow after -- so a raid that switches
		// who is hitting the flag moves its guards, while the commitment stays with whoever pulled it.
		if (!opened)
		{
			opened = true;
			NpcMessageBus.Broadcast(GetOwner(), PostUnderAttack, creature, CommitReach);
		}

		NpcMessageBus.Broadcast(GetOwner(), ThatOneThere, creature, PointReach);
	}

	protected override void HandleBackHome()
	{
		opened = false;
		base.HandleBackHome();
	}

	protected override void HandleDied()
	{
		opened = false;
		base.HandleDied();
	}
}

/// <summary>
/// The guards of Ophidan Bridge's defence posts — eight npcs across five retail patterns, the
/// <c>IDF5_U1_War_Vri_Def*</c> family.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Two branches, and they answer the flag's two calls
/// with two different weights: <b>a hundred hate points</b> when the post is first attacked, and a
/// bare <b>turn</b> on every blow after.
/// <para>
/// <b>Retail splits the first answer on npc state and we do not need to.</b> One branch adds the hate
/// alone and the other adds it and attacks, depending on whether the guard is already fighting;
/// <see cref="Do.HateMessageTarget"/> does both at once, and for a guard already in a fight the attack
/// half is what it was doing anyway.
/// </para>
/// <para>
/// <b>Two of the ten listeners keep their own class.</b> The defence post and guard post rearguards
/// (233477, 233487) run <c>vritra_rearguard</c>, so they answer neither call — recorded rather than
/// overwritten, the same call made for the twenty-two abyss guards that already had classes.
/// </para>
/// <para>
/// <b>Not translated:</b> everything else these five patterns do, and message <c>21214</c> — a bridge
/// watcher that sees a player and points the posts at them, whose npc our data never spawns.
/// </para>
/// </remarks>
[AIName("defence_post_guard")]
public class DefencePostGuardAI : PatternAi
{
	/// <summary>Retail's <c>point_to_add</c> on the opening call.</summary>
	private const int Commit = 100;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnMessage = Of(
			// Retail patterns <c>IDF5_U1_War_Vri_Def01_Re_Fi_65_Ae</c> and its six siblings. All seven
			// answer 21212 with <c>add_hate_point</c> and none of them switches, so a guard already
			// engaged notes the post and finishes what it is doing.
			Branch(2, "the post is under attack", [When.Message(DefencePostFlagAI.PostUnderAttack)],
				Do.HateMessageParam(Commit)),

			Branch(1, "that one, there", [When.Message(DefencePostFlagAI.ThatOneThere)],
				Do.TargetMessageParam())),
	};

	public DefencePostGuardAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
