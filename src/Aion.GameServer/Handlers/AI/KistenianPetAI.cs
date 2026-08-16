using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Fire spirit (295180), Kistenian's pet. Retail pattern <c>DGuard_KistenianPet</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Half of the loop that keeps Kistenian
/// reinforced — see <see cref="KistenianAI"/> for the other half and the messages between them.
/// <list type="bullet">
/// <item>every twenty to forty seconds it calls <b>10016</b>, which is what makes Kistenian put more
/// spirits out, and switches to a random attacker as it does</item>
/// <item>on first crossing 75, 50 and 25 percent it calls <b>10015</b>, once each</item>
/// <item>dying leaves the despawn effect where it stood, and that effect's cry takes every other
/// spirit with it</item>
/// <item>hearing <b>10017</b> — that cry — it removes itself</item>
/// </list>
/// <para>
/// <b>Its casts are not translated:</b> five indices are addressed and it has no <c>npc_skills</c>
/// entry at all, so there is nothing to map them onto. Everything above is index-free.
/// </para>
/// </remarks>
[AIName("kistenian_pet")]
public class KistenianPetAI : PatternAi
{
    /// <summary>Kistenian's own three-second call; the reply is a cast we cannot resolve.</summary>
    public const int LordCalls = 10014;

    /// <summary>"I am hurt" — three one-shots as it weakens.</summary>
    public const int Hurt = 10015;

    /// <summary>"Send more" — what brings the next pair of spirits out.</summary>
    public const int CallForMore = 10016;

    /// <summary>The despawn effect's cry: every spirit that hears it goes.</summary>
    public const int Disperse = 10017;

    private const int DespawnEffect = 295181;

    private const int Below25 = 1;
    private const int Band26To50 = 2;
    private const int Band51To75 = 3;

    private const float CryRange = 50f;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(8, "", When.Always,
                Do.ArmTimer(0, 30000),
                Do.ArmTimer(9, 5000))),

        OnBattleTimer = Of(
            // The hurt calls, one per band, deepest first so a spirit dropped fast still reports.
            Branch(7, "hurt", [When.Timer(9), When.HpBelow(25), When.FirstTime(Below25)],
                Do.ArmTimer(9, 5000), Do.Broadcast(Hurt, CryRange)),
            Branch(6, "hurt", [When.Timer(9), When.HpBetween(26, 50), When.FirstTime(Band26To50)],
                Do.ArmTimer(9, 5000), Do.Broadcast(Hurt, CryRange)),
            Branch(5, "hurt", [When.Timer(9), When.HpBetween(51, 75), When.FirstTime(Band51To75)],
                Do.ArmTimer(9, 5000), Do.Broadcast(Hurt, CryRange)),
            Branch(4, "", [When.Timer(9)], Do.ArmTimer(9, 5000)),

            // Call for more. The interval is the coin flip: a quarter of the time twenty seconds,
            // half of the rest thirty, otherwise forty.
            Branch(3, "call", [When.Chance(25), When.Timer(0)],
                Do.ArmTimer(0, 20000), Do.Broadcast(CallForMore, CryRange),
                Do.SwitchTarget(AggroTarget.RANDOM)),
            Branch(2, "call", [When.Chance(50), When.Timer(0)],
                Do.ArmTimer(0, 30000), Do.Broadcast(CallForMore, CryRange),
                Do.SwitchTarget(AggroTarget.RANDOM)),
            Branch(1, "call", [When.Timer(0)],
                Do.ArmTimer(0, 40000), Do.Broadcast(CallForMore, CryRange),
                Do.SwitchTarget(AggroTarget.RANDOM))),

        OnMessage = Of(
            Branch(13, "disperse", [When.Message(Disperse)],
                Do.DespawnSelf())),

        OnDie = Of(
            Branch(15, "leave the effect", When.Always,
                Do.SpawnNear(DespawnEffect, 0, count: 1, range: 1f, liveSeconds: 6))),
    };

    public KistenianPetAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
