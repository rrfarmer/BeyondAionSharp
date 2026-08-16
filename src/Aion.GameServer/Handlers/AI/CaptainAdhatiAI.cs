using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Captain Adhati of the Dreadgion (214823). Retail pattern <c>Dread_DrakanBoss</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. He was on <c>xdrakanpriest</c>, a generic
/// Java-parity behaviour shared with <b>ninety-four other NPCs</b>: a three-percent chance per hit of
/// calling up one to three of npc 282988. That is not a weaker version of his fight, it is somebody
/// else's fight — none of his own three servants was spawned by anything anywhere.
/// <para>
/// <b>What he actually does is a five-rung escalation.</b> Two servants come out the moment he is
/// engaged, onto fixed marks on the deck; after that a ten-second heartbeat carries one-shot steps at
/// 80, 65, 45, 35 and 20 percent, each calling a differently-composed wave to offsets around him and
/// then <b>rounding on somebody else</b>:
/// </para>
/// <list type="table">
/// <item><term>on engaging</term><description>two attackers on two fixed marks, 25s</description></item>
/// <item><term>below 80</term><description>four attackers, 30s — then the second-most-hated</description></item>
/// <item><term>below 65</term><description>an attacker and a <b>buffer</b>, 22s — then the third</description></item>
/// <item><term>below 45</term><description>three attackers and a <b>healer</b>, 30s — then the second</description></item>
/// <item><term>below 35</term><description>nothing but a cast, and it still spends the tick</description></item>
/// <item><term>below 20</term><description>four attackers, a healer <em>and</em> a buffer, 30s — then
/// a random attacker</description></item>
/// </list>
/// <para>
/// The rungs are one-shots and the deepest outranks the rest, so a boss burned down quickly skips
/// straight to the wave it deserves. Everything he calls goes away when he dies or resets.
/// </para>
/// <para>
/// <b>The empty rung at 35 is kept and is not decoration.</b> It re-arms the heartbeat at <b>ten</b>
/// seconds where the fallback branch re-arms it at <b>seven</b>, so running it changes when the next
/// rung can fire. That is observable even though its own action — a single cast — is not translated.
/// </para>
/// <para>
/// <b>Not translated.</b> Four skill indices, and with them timer 1, which is a cast on a twenty-second
/// cycle carrying nothing else. Timers 2 and 3 go too: they are a chain of <c>broadcast_message</c> at
/// 6835 and 6837, and nothing in our tree listens for either — his servants run the plain
/// <c>servant</c> AI, which is not a message listener. Also out: the <c>goto_waypoint</c> he opens with,
/// since we have no route for him, and the shout at the twenty-percent rung.
/// </para>
/// </remarks>
[AIName("captain_adhati")]
public class CaptainAdhatiAI : PatternAi
{
    /// <summary><c>BDread_SerMATK_50_An</c> — holy servant, the attacker of the three.</summary>
    private const int Attacker = 281344;

    /// <summary><c>BDread_SerHeal_50_An</c> — healing energy.</summary>
    private const int Healer = 281345;

    /// <summary><c>BDread_SerMDBuff_50_An</c> — chaotic energy, the buffer.</summary>
    private const int Buffer = 281346;

    /// <summary>Retail's <c>SPAWN_ID_1</c>: dying or resetting clears every wave at once.</summary>
    private const int Called = 1;

    /// <summary>The two marks on the deck his opening pair takes, and the heading retail gives them.</summary>
    private static readonly SpawnSpot[] OpeningMarks =
    [
        new SpawnSpot(488.21f, 805.47f, 421f, Facing(90)),
        new SpawnSpot(482.21f, 805.47f, 421f, Facing(90)),
    ];

    private static sbyte Facing(int degrees) =>
        (sbyte)PositionUtil.ConvertAngleToHeading((degrees + 360) % 360);

    private const int OpeningLife = 25;
    private const int WaveLife = 30;
    private const int ShortWaveLife = 22;

    private const int HeartbeatMillis = 10000;

    /// <summary>The fallback re-arm, which is three seconds shorter than a rung's. See the remarks.</summary>
    private const int IdleHeartbeatMillis = 7000;

    // Retail's BETA_1..5, one per rung, deepest last as it names them.
    private const int Below80 = 1;
    private const int Below65 = 2;
    private const int Below45 = 3;
    private const int Below35 = 4;
    private const int Below20 = 5;

    /// <summary>One servant of a wave, at the offset retail gives it.</summary>
    private static PatternAction At(int npcId, float dx, float dy, float dz, int liveSeconds)
        => Do.SpawnOffset(npcId, Called, dx, dy, liveSeconds, dz);

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        // Retail also arms timer 1 at seventeen seconds and broadcasts to the room; both are casts and
        // messages, and neither is translated.
        OnEnterAttack = Of(
            Branch(15, "", When.Always,
                Do.ArmTimer(0, HeartbeatMillis),
                Do.SpawnAt(Attacker, Called, OpeningLife, OpeningMarks))),

        OnBattleTimer = Of(
            // Deepest rung first: a boss that drops fast reaches the wave it deserves rather than
            // walking every one on the way down.
            Branch(12, "below 20", [When.Timer(0), When.HpBelow(20), When.FirstTime(Below20)],
                Do.ArmTimer(0, HeartbeatMillis),
                At(Attacker, 8f, 8f, 3f, WaveLife),
                At(Healer, -5f, 0f, 3f, WaveLife),
                At(Attacker, -8f, 8f, 3f, WaveLife),
                At(Attacker, 3f, 5f, 3f, WaveLife),
                At(Buffer, 5f, 0f, 3f, WaveLife),
                At(Attacker, -3f, 5f, 3f, WaveLife),
                Do.SwitchTarget(AggroTarget.RANDOM)),

            Branch(11, "below 35", [When.Timer(0), When.HpBelow(35), When.FirstTime(Below35)],
                Do.ArmTimer(0, HeartbeatMillis)),

            Branch(8, "below 45", [When.Timer(0), When.HpBelow(45), When.FirstTime(Below45)],
                Do.ArmTimer(0, HeartbeatMillis),
                At(Attacker, -5f, 0f, 3f, WaveLife),
                At(Healer, 5f, 0f, 3f, WaveLife),
                At(Attacker, 8f, 8f, 4f, WaveLife),
                At(Attacker, -8f, 8f, 4f, WaveLife),
                Do.SwitchTarget(AggroTarget.SECOND_MOST_HATED)),

            Branch(7, "below 65", [When.Timer(0), When.HpBelow(65), When.FirstTime(Below65)],
                Do.ArmTimer(0, HeartbeatMillis),
                At(Attacker, 5f, 0f, 3f, ShortWaveLife),
                At(Buffer, -5f, 0f, 3f, ShortWaveLife),
                Do.SwitchTarget(AggroTarget.THIRD_MOST_HATED)),

            Branch(6, "below 80", [When.Timer(0), When.HpBelow(80), When.FirstTime(Below80)],
                Do.ArmTimer(0, HeartbeatMillis),
                At(Attacker, 3f, -2f, 3f, WaveLife),
                At(Attacker, -3f, -2f, 3f, WaveLife),
                At(Attacker, 3f, 13f, 3f, WaveLife),
                At(Attacker, -3f, 13f, 3f, WaveLife),
                Do.SwitchTarget(AggroTarget.SECOND_MOST_HATED)),

            Branch(2, "", [When.Timer(0)],
                Do.ArmTimer(0, IdleHeartbeatMillis))),

        // Retail's on_leave_attack_state and on_killed_by_user both clear SPAWN_ID_1.
        OnLeaveAttack = Of(
            Branch(21, "", When.Always, Do.Despawn(Called))),

        OnDie = Of(
            Branch(20, "", When.Always, Do.Despawn(Called))),
    };

    public CaptainAdhatiAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
