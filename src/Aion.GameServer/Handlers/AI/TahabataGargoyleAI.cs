using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Tahabata's faithful subordinate (281258). Retail pattern <c>Dragon_G1Slave</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. It steps off a summon spot
/// (<see cref="TahabataSummonSpotAI"/>) and is a fuse rather than a fighter: ten seconds after
/// something engages it, it blows itself up, and four seconds after that it is gone.
/// <para>
/// <b>It also answers Tahabata's ring call.</b> Every time he puts a fresh ring of spots out he first
/// broadcasts 3415, and every subordinate still standing removes itself on hearing it — no explosion,
/// no fight, it simply leaves. That is what keeps the wave at four however long the band lasts, and it
/// was the last unimplemented message on this pattern.
/// </para>
/// <para>
/// <b>What changed from the aionemu class.</b> It hung the removal off the end of the cast, so the
/// four-second gap between blowing up and disappearing did not exist, and the ring call was not heard
/// at all. Its reading of the skill was right: retail casts index 0 on itself here, the npc has one
/// skill, and that skill is <b>18219 "Mana Regression"</b> with the stack name
/// <c>BNWI_SPELLATKTA5_SELFBLOW_NR</c> — a self blow. Three things agreeing is a resolution.
/// </para>
/// </remarks>
[AIName("tahabata_gargoyle")]
public class TahabataGargoyleAI : PatternAi
{
    /// <summary>"Mana Regression" — the self-detonation.</summary>
    private const int SelfBlow = 18219;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnMessage = Of(
            Branch(3, "the next ring is coming", [When.Message(TahabataPyrelordAI.ClearTheOldWave)],
                Do.DespawnSelf())),

        OnEnterAttack = Of(
            Branch(3, "", When.Always,
                Do.ArmTimer(0, 10000))),

        OnBattleTimer = Of(
            Branch(2, "and gone", [When.Timer(2)],
                Do.DespawnSelf()),

            Branch(1, "the fuse", [When.Timer(0)],
                Do.ArmTimer(2, 4000),
                Do.SkillOnSelf(SelfBlow))),
    };

    public TahabataGargoyleAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
