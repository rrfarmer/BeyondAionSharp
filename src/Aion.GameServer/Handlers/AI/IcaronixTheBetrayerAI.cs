using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Icaronix the Betrayer (214599), the form Icaronix the Deceiver becomes at 75%. Retail pattern
/// <c>NLehpar_BhB</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. He had no AI at all: <c>BetrayerIcaronixAI</c>
/// spawns him and then nothing drives him, so the second half of the fight was a plain aggressive
/// monster. Retail has him call up a different servant as he loses ground — one when the fight starts
/// and another on passing 80, 50 and 30 — each replacing the last, and a strange creature crawling
/// out of him when he dies. **All five were spawned by nothing anywhere in the server.**
/// <list type="table">
/// <item><term>on the pull</term><description>Kuillus, 280937</description></item>
/// <item><term>below 80</term><description>Mudthorn, 280939</description></item>
/// <item><term>below 50</term><description>Pretor, 280938</description></item>
/// <item><term>below 30</term><description>Rottentree, 280940</description></item>
/// <item><term>on death</term><description>a strange creature, 280941, for twelve seconds</description></item>
/// </list>
/// <para>
/// Each servant has its own spawn id and each step clears only its own, which is why they accumulate
/// rather than replace one another — by the end of the fight all four are up. Leaving the fight or
/// dying clears all of them.
/// </para>
/// <para>
/// His rotation is not translated: five indices, five skills, and no branch comments to corroborate
/// any mapping. The timers those branches run on (2 through 6) are not armed here either, since
/// arming a timer whose branches do not exist only starts a chain that dies on its first tick. His
/// npc_skills probabilities are untouched, so he still casts.
/// </para>
/// </remarks>
[AIName("icaronix_the_betrayer")]
public class IcaronixTheBetrayerAI : PatternAi
{
    private const int Kuillus = 280937;
    private const int Mudthorn = 280939;
    private const int Pretor = 280938;
    private const int Rottentree = 280940;
    private const int StrangeCreature = 280941;

    /// <summary>Retail's spawn ids, one per servant, so each step clears only its own.</summary>
    private const int KuillusGroup = 1;
    private const int RottentreeGroup = 2;
    private const int PretorGroup = 3;
    private const int MudthornGroup = 4;
    private const int DeathGroup = 5;

    /// <summary>Twenty minutes — long enough that only the fight ending removes them.</summary>
    private const int ServantLife = 1200;
    private const float ServantRange = 3f;

    /// <summary>Summons one servant, clearing whatever is left of that servant's own group.</summary>
    private static PatternAction[] Summon(int npcId, int group) =>
    [
        Do.ArmTimer(0, 5000),
        Do.Despawn(group),
        Do.SpawnNear(npcId, group, count: 1, range: ServantRange, liveSeconds: ServantLife),
    ];

    private static PatternAction[] ClearEveryServant() =>
    [
        Do.Despawn(KuillusGroup),
        Do.Despawn(RottentreeGroup),
        Do.Despawn(PretorGroup),
        Do.Despawn(MudthornGroup),
    ];

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(11, "", [When.FirstTime(1)],
                Do.ArmTimer(0, 5000),
                Do.Despawn(KuillusGroup),
                Do.SpawnNear(Kuillus, KuillusGroup, count: 1, range: ServantRange, liveSeconds: ServantLife))),

        OnBattleTimer = Of(
            Branch(10, "", [When.Timer(0), When.HpBelow(30), When.FirstTime(2)], Summon(Rottentree, RottentreeGroup)),
            Branch(9, "", [When.Timer(0), When.HpBetween(31, 50), When.FirstTime(3)], Summon(Pretor, PretorGroup)),
            Branch(8, "", [When.Timer(0), When.HpBetween(51, 80), When.FirstTime(4)], Summon(Mudthorn, MudthornGroup)),

            // The heartbeat all three steps wait on. Without it the chain ends on the first tick that
            // crosses nothing, which at full health is the very first one.
            Branch(1, "", [When.Timer(0)],
                Do.ArmTimer(0, 5000))),

        OnEnterIdle = Of(
            Branch(7, "", When.Always, ClearEveryServant())),

        OnDie = Of(
            Branch(7, "", When.Always,
                Do.Despawn(KuillusGroup),
                Do.Despawn(RottentreeGroup),
                Do.Despawn(PretorGroup),
                Do.Despawn(MudthornGroup),
                Do.SpawnNear(StrangeCreature, DeathGroup, count: 1, liveSeconds: 12))),
    };

    public IcaronixTheBetrayerAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
