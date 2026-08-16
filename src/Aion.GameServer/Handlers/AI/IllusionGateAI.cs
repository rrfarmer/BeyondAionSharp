using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The illusion gate (281226) the awakened chamber lords open below 25%. Retail pattern
/// <c>BGuard_DrGateChiefD</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Found only because porting
/// <see cref="AwakenedChamberLordAI"/> made the gate spawnable — it then surfaced in the audit as an
/// encounter of its own with three adds nobody could reach: warguard (281227), bowguard (281228) and
/// aetherguard (281229).
/// <para>
/// <b>The gate is a spawner, not scenery.</b> Once it is engaged it pours out five guards and then
/// removes itself:
/// </para>
/// <list type="bullet">
/// <item>five seconds in — a warguard and an aetherguard</item>
/// <item>thirty seconds later — a bowguard and two more aetherguards</item>
/// <item>five seconds after that — the gate closes behind them</item>
/// </list>
/// <para>
/// It also listens for message <b>10009</b>, which is exactly what the chamber lord broadcasts when it
/// leaves the fight: reset the lord and its gate shuts on its own. That pairing only became visible
/// once both halves were read together, and it is the reason this class carries an <c>OnMessage</c>
/// handler for a number nothing else sends.
/// </para>
/// <para>
/// <b>One consequence worth stating.</b> Our data had this on <c>groupgate</c> — the dialog-driven
/// portal AI that 207539 and its neighbours use to teleport a group. That is a devname match, not a
/// behaviour match: retail gives this npc attack-state handlers and an ELITE rating, so it is a combat
/// NPC. Moving it onto the pattern runtime therefore also makes it aggressive, where before it stood
/// inert and offered a dialog. That is the retail behaviour, but it is a change in kind and not only
/// in detail.
/// </para>
/// <para>
/// <b>Not translated:</b> nothing. This pattern casts no skills and every branch is here.
/// </para>
/// </remarks>
[AIName("illusion_gate")]
public class IllusionGateAI : PatternAi
{
    private const int Warguard = 281227;
    private const int Bowguard = 281228;
    private const int Aetherguard = 281229;

    /// <summary>Everything it pours out, cleared together if the fight ends.</summary>
    private const int Guards = 1;

    /// <summary>Ten minutes each, two metres out.</summary>
    private const int GuardLife = 600;
    private const float AtTheGate = 2f;

    /// <summary>Broadcast by the chamber lord when it leaves the fight.</summary>
    public const int LordDisengaged = 10009;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(5, "", When.Always, Do.ArmTimer(0, 5000))),

        OnBattleTimer = Of(
            Branch(3, "first pair", [When.Timer(0)],
                Do.ArmTimer(1, 30000),
                Do.SpawnNear(Warguard, Guards, count: 1, range: AtTheGate, liveSeconds: GuardLife),
                Do.SpawnNear(Aetherguard, Guards, count: 1, range: AtTheGate, liveSeconds: GuardLife)),

            Branch(2, "second wave", [When.Timer(1)],
                Do.ArmTimer(2, 5000),
                Do.SpawnNear(Bowguard, Guards, count: 1, range: AtTheGate, liveSeconds: GuardLife),
                Do.SpawnNear(Aetherguard, Guards, count: 2, range: AtTheGate, liveSeconds: GuardLife)),

            // The gate closes behind them; the guards it left stay.
            Branch(1, "close", [When.Timer(2)],
                Do.DespawnSelf())),

        OnMessage = Of(
            Branch(7, "the lord disengaged", [When.Message(LordDisengaged)],
                Do.DespawnSelf())),

        OnLeaveAttack = Of(
            Branch(4, "", When.Always,
                Do.Despawn(Guards),
                Do.DespawnSelf())),
    };

    public IllusionGateAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
