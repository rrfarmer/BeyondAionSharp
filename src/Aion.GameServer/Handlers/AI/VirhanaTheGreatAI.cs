using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Npcskill;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Virhana the Great, Beshmundir Temple. Retail pattern <c>IDCTH_Boss_StatueDrakan</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. He runs two independent timers: a self-centred
/// Earthly Retribution roughly every fifteen seconds from the twelfth second of the fight, and — once
/// seventy seconds have passed — a Blade of Lunacy every ten seconds that walks him around the raid.
/// <para>
/// Ours had the two halves crossed. It waited the seventy seconds before doing anything but the
/// opening buff, then cast <em>Earthly Retribution</em> on the ten-second chain where retail casts
/// Blade of Lunacy, stopped after twelve casts and started the seventy-second wait over. The
/// fifteen-second chain did not exist, and neither did the target switching.
/// </para>
/// <para>
/// All three indices are corroborated twice over. By role: index 2 is a self-cast on entering combat
/// and 19121 is Seal of Reflection, a buff; index 1 is cast at <c>OBJI_SELF</c> and 18897 is Earthly
/// Retribution, an attack, so it is a sweep centred on him; index 0 is cast at the current target and
/// at a second player, and 18602 is Blade of Lunacy. And by our own previous code, which already used
/// 19121 as the opener and 18897 as the repeated cast.
/// </para>
/// </remarks>
[AIName("virhana")]
public class VirhanaTheGreatAI : PatternAi
{
    /// <summary>Index 0 — Blade of Lunacy, the ten-second chain that starts at seventy seconds.</summary>
    private const int BladeOfLunacy = 18602;

    /// <summary>Index 1 — Earthly Retribution, the sweep centred on him.</summary>
    private const int EarthlyRetribution = 18897;

    /// <summary>Index 2 — Seal of Reflection, the buff he opens with.</summary>
    private const int SealOfReflection = 19121;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(50, "", When.Always,
                Do.ArmTimer(0, 12000),
                Do.ArmTimer(1, 70000),
                Do.SkillOnSelf(SealOfReflection))),

        OnBattleTimer = Of(
            // Seventy seconds in, once: opens the Blade of Lunacy chain by hitting two people at once.
            Branch(40, "", [When.Timer(1)],
                Do.ArmTimer(2, 10000),
                Do.SkillOnTarget(BladeOfLunacy),
                Do.SkillOn(NpcSkillTargetAttribute.RANDOM_EXCEPT_CURRENT_TARGET, BladeOfLunacy)),

            // ...and thereafter every ten seconds, moving to someone else each time. It never stops.
            Branch(35, "", [When.Timer(2)],
                Do.ArmTimer(2, 10000),
                Do.SkillOnTarget(BladeOfLunacy),
                Do.SwitchTarget(AggroTarget.RANDOM_EXCEPT_CURRENT_TARGET)),

            // The three Earthly Retribution branches differ only in how long they wait before the next
            // one. Conditions are in the pattern's own order, probability first, because that is the
            // order it evaluates them in.
            Branch(30, "", [When.Chance(15), When.Timer(0)],
                Do.ArmTimer(0, 8000),
                Do.SkillOnSelf(EarthlyRetribution)),

            Branch(29, "", [When.Chance(10), When.Timer(0)],
                Do.ArmTimer(0, 30000),
                Do.SkillOnSelf(EarthlyRetribution)),

            Branch(28, "", [When.Timer(0)],
                Do.ArmTimer(0, 15000),
                Do.SkillOnSelf(EarthlyRetribution))),
    };

    public VirhanaTheGreatAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
