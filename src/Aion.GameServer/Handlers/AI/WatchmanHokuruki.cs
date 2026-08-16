using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Watchman Hokuruki (235634), the Shugo Emperor's Vault stage-one boss. Retail pattern
/// <c>IDSweep_Monster_Nmd03</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Java parity was
/// <c>ai/instance/theShugoEmperorsVault/WatchmanHokuruki</c> (Yeats), and the port of it was faithful —
/// what follows is a divergence from aionemu.
/// <para>
/// <b>He summons one thing, and it is not the gunners.</b> aionemu calls an intruder marksman (236083)
/// and two intruder snipers (235649) at each of three hand-placed positions, shuffled per fight. No
/// retail pattern spawns either template — they are stage one's own room population — and Hokuruki's
/// pattern names exactly one add: the <b>tamed mosbear</b> (235632), which aionemu already had, and used
/// for two of its five phases.
/// </para>
/// <para>
/// <b>Three waves of bears, scattered around him.</b> Entering the fight brings <b>four</b> within five
/// metres; below fifty percent <b>two</b> more within eight; below twenty-five, <b>three</b>. Retail's
/// ladder has no seventy-five or fifteen rung at all, and no fixed coordinates anywhere — the placement
/// is a scatter from his own position, so the three position sets and their shuffle were an
/// approximation of a random spawn.
/// </para>
/// <para>
/// <b>The ladder is longer than the part that summons, and the extra rungs cost a hit.</b> Retail puts
/// three higher-priority rungs above the two that call bears — at eighty, sixty and thirty percent —
/// whose only action is <c>set_condition_spawn_variable 2STAGE_ING</c>, the instance's stage counter.
/// We cannot express that action, but the rungs are kept: these are first-match-wins chains, so a rung
/// that matches <em>consumes the hit</em> whether or not we can perform what it does. Dropping them
/// would bring the bears several hits early. Below fifty with everything fresh, retail spends one hit
/// on the sixty rung, one on the eighty rung, and calls the bears on the <b>third</b>.
/// </para>
/// <para>
/// <b>Death clears stage one.</b> He broadcasts <c>140505</c> to a hundred metres, and eleven templates
/// answer it with <c>despawn_self</c> — see <see cref="IDSweepStageAddAI"/>. That is where the gunners
/// really belong in his fight: not as things he calls, but as things that leave when he falls.
/// </para>
/// <para>
/// <b>Not translated.</b> The ten-second cast loop on battle timer 0 (two <c>SKILLI_INDEX</c> casts);
/// the three <c>say_to_all</c> lines, which have no rows in our <c>npc_shouts.xml</c>; the
/// <c>set_condition_spawn_variable</c> actions on the ladder, on entering combat and on death; and
/// <c>despawn_at_attack_state</c> on the bear spawns. Retail also gives no <c>hatepoints_to_add</c> to
/// any of the three waves, so the single hate point aionemu put on the most-hated is gone with them —
/// the bears are aggressive and find their own way in.
/// </para>
/// <para>
/// <b>One retail branch is unreachable here.</b> A second <c>on_enter_attack_state</c> rung, below the
/// opening wave, sets the same flag var the fifty-percent bears are gated on — so a retail Hokuruki that
/// resets and is re-engaged loses that wave for good. Our convention is that a boss which resets replays
/// its steps (see <see cref="Aion.GameServer.Ai.Pattern.PatternAi"/>), which clears the flag the rung
/// would have consumed, so the rung can never fire. Recorded rather than modelled.
/// </para>
/// </remarks>
[AIName("watchman_hokuruki")]
public class WatchmanHokuruki : IDSweep_Bosses
{
	/// <summary><c>IDSweep_S1_Mosbear_65_An</c> — the only add his pattern names.</summary>
	private const int TamedMosbear = 235632;

	/// <summary>Stage one is over: every add in the room removes itself.</summary>
	public const int StageIsOver = 140505;

	private const float ClearRange = 100f;

	private const int OpeningBears = 4;
	private const float OpeningScatter = 5f;
	private const float LaterScatter = 8f;

	/// <summary>
	/// One rung of the <c>on_attacked</c> chain, in retail's priority order.
	/// </summary>
	/// <param name="Below">Its <c>is_hp_lower_than</c> guard.</param>
	/// <param name="Bears">
	/// How many mosbears it calls, or zero for a rung whose only action is the stage counter we cannot
	/// express. A zero rung still consumes the hit, which is the whole reason it is here.
	/// </param>
	private readonly record struct Rung(int Below, int Bears);

	// Retail's ALPHA_5, ALPHA_3, ALPHA_2, BETA_1, BETA_2 -- one flag each, so one firing each.
	private static readonly Rung[] Ladder =
	[
		new Rung(Below: 30, Bears: 0),
		new Rung(Below: 60, Bears: 0),
		new Rung(Below: 80, Bears: 0),
		new Rung(Below: 50, Bears: 2),
		new Rung(Below: 25, Bears: 3),
	];

	private readonly object gate = new object();
	private readonly bool[] spent = new bool[Ladder.Length];
	private bool inCombat;

	public WatchmanHokuruki(Npc owner)
		: base(owner)
	{
	}

	/// <summary>The ladder as retail orders it, for tests.</summary>
	internal static (int Below, int Bears)[] Rungs()
	{
		var rungs = new (int, int)[Ladder.Length];
		for (int i = 0; i < Ladder.Length; i++)
			rungs[i] = (Ladder[i].Below, Ladder[i].Bears);
		return rungs;
	}

	protected override void HandleAttack(Creature creature)
	{
		base.HandleAttack(creature);

		bool opening;
		lock (gate)
		{
			opening = !inCombat;
			inCombat = true;
		}

		if (opening)
			CallBears(OpeningBears, OpeningScatter);

		Climb();
	}

	/// <summary>
	/// Runs the first rung that matches and has not fired, and stops there — whether or not that rung
	/// had anything we can perform.
	/// </summary>
	private void Climb()
	{
		int hp = GetLifeStats().GetHpPercentage();
		int bears = 0;

		lock (gate)
		{
			for (int i = 0; i < Ladder.Length; i++)
			{
				if (spent[i] || hp >= Ladder[i].Below)
					continue;

				spent[i] = true;
				bears = Ladder[i].Bears;
				break;
			}
		}

		if (bears > 0)
			CallBears(bears, LaterScatter);
	}

	/// <summary>Scatters mosbears around him, uniformly within <paramref name="scatter"/> metres.</summary>
	private void CallBears(int count, float scatter)
	{
		for (int i = 0; i < count; i++)
			RndSpawnInRange(TamedMosbear, 0f, scatter);
	}

	protected override void HandleBackHome()
	{
		base.HandleBackHome();
		Reset();
	}

	protected override void HandleDespawned()
	{
		Reset();
		base.HandleDespawned();
	}

	private void Reset()
	{
		lock (gate)
		{
			inCombat = false;
			System.Array.Clear(spent);
		}
	}

	protected override void HandleDied()
	{
		// Before the delete: the broadcast reads the sender's known list, and a deleted NPC has none.
		NpcMessageBus.Broadcast(GetOwner(), StageIsOver, null, ClearRange);

		base.HandleDied();
		GetOwner().GetController().Delete();
	}
}

/// <summary>
/// Everything in stage one of the Shugo Emperor's Vault. Retail patterns <c>IDSweep_Monster_02</c>,
/// <c>IDSweep_S1_Monster</c> and <c>IDSweep_S1_Shulack_Gu_01</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Eleven templates across three patterns — the
/// mosbears, the shulack intruders, the brownie peon, the turncoat and both marksmen — and all three
/// patterns answer <b>140505</b> the same way: <c>despawn_self</c>. That is the other half of
/// <see cref="WatchmanHokuruki"/>'s death, and until both halves existed neither was worth shipping.
/// <para>
/// It extends <see cref="IDSweep_Shugos"/> rather than replacing it, so the instance-progression check
/// on spawn and the damage variance every Vault NPC shares are untouched; this only adds the listener.
/// </para>
/// <para>
/// <b>What each pattern does beyond the despawn is not translated</b>, and differs: the mosbears and
/// the marksmen run seven-second <c>SKILLI_INDEX</c> cast loops, and the shulack intruders add to the
/// instance's <c>1STAGE_START</c> counter when they die. Sharing one class for the despawn is not a
/// claim that the three patterns are the same — only that this branch of them is.
/// </para>
/// </remarks>
[AIName("idsweep_stage_add")]
public class IDSweepStageAddAI : IDSweep_Shugos, INpcMessageListener
{
	public IDSweepStageAddAI(Npc owner)
		: base(owner)
	{
	}

	public void OnNpcMessage(Npc sender, int messageType, Aion.GameServer.Model.GameObjects.VisibleObject? param)
	{
		if (messageType == WatchmanHokuruki.StageIsOver && !IsDead())
			AIActions.DeleteOwner(this);
	}
}
