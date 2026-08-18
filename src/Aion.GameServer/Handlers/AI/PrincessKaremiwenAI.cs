using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Princess Karemiwen (214695), Adma Stronghold. Retail pattern <c>ND2_WhF</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. She was on plain <c>aggressive</c>, and her two
/// maids — banshee maid (281052) and vampire maid (281051) — were spawned by nothing anywhere; their
/// only trace in the server was their own <c>npc_skills</c> entries. <c>AdmaStrongholdInstance</c>
/// does not name her.
/// <para>
/// <b>The maids arrive on a three-minute fuse.</b> Retail writes one timer — sixty seconds — and three
/// branches on it, each guarded by its own one-shot flag. The first two only shout and re-arm; the
/// third shouts, calls both maids, and does <i>not</i> re-arm. So the timer fires at sixty, at a
/// hundred and twenty, and at a hundred and eighty, and only the last does anything, after which the
/// chain is finished for the fight. They arrive at her feet and stay five minutes.
/// </para>
/// <para>
/// It is a countdown written as a flag ladder rather than as a delay, which is why the shouts matter
/// to retail: the first two are the warning that the third is coming.
/// </para>
/// <para>
/// <b>Everything else is omitted, because with the casts gone there is nothing left of it.</b> Six
/// skills, indices 0 through 5, and this pattern has no branch comments at all, so nothing anchors a
/// mapping — the same refusal as Icaronix, Prectaz and RM-56c. Her other five timers exist only to
/// cast: a five-second heartbeat whose health bands each light one of three cast-only timers, a
/// twenty-five second alternation between two skills, and a band timer at full health. None of them
/// spawns, moves or says anything, so arming them would schedule work forever to do nothing. Their
/// shape is recorded in docs/retail-ai-fidelity.md against the day a skill mapping turns up.
/// </para>
/// <para>
/// <b>Also not translated:</b> the shout itself (<c>STR_CHAT_BIDDF2A_NM_Princess_50_Ah</c>), which has
/// no numeric id in our data — so the warning is silent and only the arrival is visible.
/// </para>
/// </remarks>
[AIName("princess_karemiwen")]
public class PrincessKaremiwenAI : PatternAi
{
    private const int VampireMaid = 281051;
    private const int BansheeMaid = 281052;

    /// <summary>Retail's <c>SPAWN_ID_1</c>.</summary>
    private const int Maids = 1;

    /// <summary>Five minutes, and within three metres of her.</summary>
    private const int MaidLife = 300;
    private const float AtHerFeet = 3f;

    private const int Minute = 60000;

    // Retail's ZETA_1..3: one per turn of the sixty-second timer.
    private const int FirstCall = 1;
    private const int SecondCall = 2;
    private const int ThirdCall = 3;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(15, "", [When.Chance(50)],
                Do.ArmTimer(8, Minute))),

        OnBattleTimer = Of(
            // Two turns of warning...
            Branch(16, "first warning", [When.Timer(8), When.FirstTime(FirstCall)],
                Do.ArmTimer(8, Minute)),

            Branch(15, "second warning", [When.Timer(8), When.FirstTime(SecondCall)],
                Do.ArmTimer(8, Minute)),

            // ...and on the third the maids arrive. This branch deliberately does not re-arm: the
            // ladder is spent and nothing calls them again for the rest of the fight.
            Branch(14, "the maids", [When.Timer(8), When.FirstTime(ThirdCall)],
                Do.SpawnNear(BansheeMaid, Maids, count: 1, range: AtHerFeet, liveSeconds: MaidLife),
                Do.SpawnNear(VampireMaid, Maids, count: 1, range: AtHerFeet, liveSeconds: MaidLife))),

        OnLeaveAttack = Of(
            Branch(20, "", When.Always, Do.Despawn(Maids))),

        OnDie = Of(
            Branch(20, "", When.Always, Do.Despawn(Maids))),
    };

    public PrincessKaremiwenAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
