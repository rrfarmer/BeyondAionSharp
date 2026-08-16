using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The two illusion gates: the one the awakened chamber lords open below 25% (281226,
/// <c>BGuard_DrGateChiefD</c>) and the one the fortress duke and the abyss chief open
/// (284978, <c>IDAB_Reward_Item_NoShowNPC_09</c>).
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
/// <b>The second gate was pouring out the wrong guards.</b> 284978 already carried this AI name, and
/// the class had one hardcoded guard set — so the duke's gate opened and the <em>chamber lord's</em>
/// warguard, bowguard and aetherguard came through it. Its own three (284979, 284980, 284981) were in
/// nobody's reach, which is how the audit found it. The two patterns are the same mechanic with the
/// same timings and a different set of ids, so the class is now a two-row table.
/// <para>
/// Worth naming the trap: a shared <c>ai_name</c> is not a shared guard list. Only reading the second
/// pattern showed that the ids differ, and the class looked correct from the inside either way.
/// </para>
/// </para>
/// <para>
/// <b>Not translated:</b> nothing. Neither pattern casts a skill and every branch is here.
/// </para>
/// </remarks>
[AIName("illusion_gate")]
public class IllusionGateAI : PatternAi
{
    /// <summary>What one gate pours out. Every gate's set is a warguard, a bowguard and an aetherguard.</summary>
    private readonly record struct Guardians(int Warguard, int Bowguard, int Aetherguard);

    private static readonly Dictionary<int, Guardians> ByGate = new Dictionary<int, Guardians>
    {
        // BGuard_DrGateChiefD -- the awakened chamber lords' gate.
        [281226] = new Guardians(281227, 281228, 281229),

        // IDAB_Reward_Item_NoShowNPC_09 -- the fortress duke's, and one abyss chief's last call.
        [284978] = new Guardians(284979, 284980, 284981),
    };

    /// <summary>A gate whose id is not in the table pours out nothing rather than somebody else's set.</summary>
    private static PatternAction Pour(System.Func<Guardians, int> which, int count) => ai =>
    {
        if (ByGate.TryGetValue(ai.GetOwner().GetNpcId(), out Guardians guards))
            ai.SpawnNear(which(guards), Guards, count: count, range: AtTheGate, liveSeconds: GuardLife);
    };

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
                Pour(g => g.Warguard, 1),
                Pour(g => g.Aetherguard, 1)),

            Branch(2, "second wave", [When.Timer(1)],
                Do.ArmTimer(2, 5000),
                Pour(g => g.Bowguard, 1),
                Pour(g => g.Aetherguard, 2)),

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
