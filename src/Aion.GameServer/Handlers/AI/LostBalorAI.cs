using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Lost Balor (214567), the Theobomos world boss. Retail pattern <c>ND2_FhV</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. He had no AI at all — a plain aggressive monster
/// on a four-hour respawn — and retail has him call up statues as he loses ground:
/// <list type="table">
/// <item><term>below 80</term><description>a Kuillus statue, 280956</description></item>
/// <item><term>below 50</term><description>a test statue, 280957</description></item>
/// <item><term>below 30</term><description>two at once, 280954 and 280955</description></item>
/// </list>
/// All four were spawned by nothing anywhere in the server. Each step files its statues under its own
/// spawn id and clears nothing, so they accumulate; leaving the fight clears all of them.
/// <para>
/// His rotation is not translated. Six skills, five indices addressed, and no branch comments to
/// corroborate any mapping — the same position this port took on Icaronix the Betrayer, whose summon
/// ladder is the same shape. The timers those branches run on (1 through 6) are not armed here, since
/// arming a timer whose branches do not exist starts a chain that dies on its first tick.
/// </para>
/// </remarks>
[AIName("lost_balor")]
public class LostBalorAI : PatternAi
{
    private const int KuillusStatue = 280956;
    private const int TestStatue = 280957;
    private const int StatueF = 280954;
    private const int StatueM = 280955;

    /// <summary>Retail's spawn ids: the last step shares one, the two above have their own.</summary>
    private const int LastPair = 1;
    private const int TestGroup = 2;
    private const int KuillusGroup = 3;

    /// <summary>Ten minutes, and three metres from him.</summary>
    private const int StatueLife = 600;
    private const float StatueRange = 3f;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(11, "", When.Always,
                Do.ArmTimer(0, 5000))),

        OnBattleTimer = Of(
            Branch(10, "", [When.Timer(0), When.HpBelow(30), When.FirstTime(2)],
                Do.ArmTimer(0, 5000),
                Do.SpawnNear(StatueF, LastPair, count: 1, range: StatueRange, liveSeconds: StatueLife),
                Do.SpawnNear(StatueM, LastPair, count: 1, range: StatueRange, liveSeconds: StatueLife)),

            Branch(9, "", [When.Timer(0), When.HpBetween(31, 50), When.FirstTime(3)],
                Do.ArmTimer(0, 5000),
                Do.SpawnNear(TestStatue, TestGroup, count: 1, range: StatueRange, liveSeconds: StatueLife)),

            Branch(8, "", [When.Timer(0), When.HpBetween(51, 80), When.FirstTime(4)],
                Do.ArmTimer(0, 5000),
                Do.SpawnNear(KuillusStatue, KuillusGroup, count: 1, range: StatueRange, liveSeconds: StatueLife)),

            // The heartbeat every step waits on. At full health none of them match, so without this
            // the chain would end on its first tick and no statue would ever appear.
            Branch(1, "", [When.Timer(0)],
                Do.ArmTimer(0, 5000))),

        OnEnterIdle = Of(
            Branch(7, "", When.Always,
                Do.Despawn(LastPair),
                Do.Despawn(TestGroup),
                Do.Despawn(KuillusGroup))),
    };

    public LostBalorAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
