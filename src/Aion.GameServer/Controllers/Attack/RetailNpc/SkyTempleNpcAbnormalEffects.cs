using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using F = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserMagicalInputs;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>
/// Selected Sky Temple Silence/Stun/Hide checks. The existing Effect owns registration,
/// start/end, cancellation and duration. Main AddSkillEffect copies the preceding critical
/// cache before A0 -> CheckResist -> B0 (34543d/34559d/3455d3); these checks never roll it again.
/// </summary>
internal static class SkyTempleNpcAbnormalEffects
{
    internal static bool TryCalculate(Effect effect, EffectTemplate template, SpellStatus? spellStatus, out bool success)
    {
        success = false;
        if (effect.GetEffector() is not Npc npc || !SkyTempleNpcProfileCatalog.TryGet(npc, out var profile))
            return false;
        int id = effect.GetSkillId();
        bool hide = id == 16677 && template is HideEffect;
        bool silence = id is 16590 or 16593 && template is SilenceEffect;
        bool stun = id == 16595 && template is StunEffect;
        if (!hide && !silence && !stun) return false;
        if (!profile.Skills.Any(s => s.Id48 == id && s.Level == effect.GetSkillLevel())
            || !ReferenceEquals(effect.GetSkillTemplate().GetEffectTemplate(template.GetPosition()), template))
            throw new InvalidOperationException("Sky Temple abnormal effect is not its current selected skill/level/slot binding.");

        if (hide)
        {
            if (template.GetPosition() != 1 || !template.IsNoResist())
                throw new InvalidOperationException("Selected Hide must be slot1/noresist.");
            // Kind32 returns before the target-class/immunity and B0 resistance branches.
            // There is no critical cache, resistance draw, or chosen status answer here.
            AddSuccess(effect, template, spellStatus);
            success = true;
            return true;
        }

        SkillElement element = stun ? SkillElement.EARTH : SkillElement.WATER;
        if (template.GetPosition() != 2 || template.IsNoResist() || template.GetElement() != element
            || template.AccMod1 != 0 || template.AccMod2 != 0)
            throw new InvalidOperationException("Selected Silence/Stun slot/element/accuracy source differs.");
        if (effect.GetEffected() is not Player target)
            throw new InvalidOperationException("Selected hostile Sky Temple abnormal requires an actual Player target.");
        // The common EffectTemplate path already checked conditions and preeffect e1/100.
        // Preserve this guard as well for direct callers; failed e2 never removes e1 damage.
        if (!effect.IsInSuccessEffects(1)) return true;
        if (!effect.TryGetCalculatedMagicalCritical(out _))
            throw new InvalidOperationException("Selected secondary abnormal requires the preceding effect's actual critical cache.");
        effect.ReuseMagicalCritical(2);

        // A0 140bbbdd0: managed ordinary gameplay is outside the global shutdown/debug
        // suppression branch. Actual User v+88/v+3d8 are zero; the NPC-template immunity
        // branch is not reached. Do not add Java's separate status-resistance roll here.
        var context = RetailNpcNormalStatusMath.Context.Create(SkyTempleCombatInputs.Scope(profile.NpcId),
            RetailNpcNormalScope.MainBinarySha256, "current4.8 User status/magic resistance ownership", false);
        // Silence b8b640 / Stun b85640 use zero accuracy modifiers and forced flags0.
        // In particular they do not consume a one-time magical-accuracy boost.
        int magicalThreshold = RetailNpcNormalStatusMath.CalculateMagicalResistThreshold(context, new(
            target.GetGameStats().GetMResist().GetCurrent(), npc.GetGameStats().GetMAccuracy().GetCurrent(),
            (uint)profile.StatRatio, profile.Level, target.GetLevel(), SkyTempleCombatInputs.Pose(target)));
        if (target.GetObserveController().CheckAttackStatus(AttackStatus.RESIST)
            || SkyTempleNpcCombat.Draw(1, 1000) <= magicalThreshold)
            return true;
        if (!CheckStatus(effect, stun ? 12 : 8,
                stun ? StatEnum.STUN_RESISTANCE : StatEnum.SILENCE_RESISTANCE,
                stun ? StatEnum.STUN_RESISTANCE_PENETRATION : StatEnum.SILENCE_RESISTANCE_PENETRATION, false))
            return true;
        AddSuccess(effect, template, spellStatus);
        success = true;
        return true;
    }

    /// <summary>
    /// Native B0 140bbc000 for selected Bleed1/Silence8/Stun12. SourceNPC -> User passes
    /// caller threshold0 and selected sources have no exclusive-attribute addition.
    /// Existing managed conditional functions supply current effective values once;
    /// altered-state ALL resistance is folded into that status word, as in the host model.
    /// </summary>
    internal static bool CheckStatus(Effect effect, int type, StatEnum resistance, StatEnum penetration, bool noResist)
    {
        if (effect.GetEffector() is not Npc npc || !SkyTempleNpcProfileCatalog.TryGet(npc, out _))
            throw new InvalidOperationException("Native Sky Temple status check requires an enabled cohort NPC.");
        if (type is not (1 or 8 or 12)) throw new ArgumentOutOfRangeException(nameof(type));
        if ((type == 1 && (resistance != StatEnum.BLEED_RESISTANCE || penetration != StatEnum.BLEED_RESISTANCE_PENETRATION))
            || (type == 8 && (resistance != StatEnum.SILENCE_RESISTANCE || penetration != StatEnum.SILENCE_RESISTANCE_PENETRATION))
            || (type == 12 && (resistance != StatEnum.STUN_RESISTANCE || penetration != StatEnum.STUN_RESISTANCE_PENETRATION)))
            throw new ArgumentException("Selected status kind/stat mapping differs.");
        if (noResist) return true;
        if (effect.GetEffected() is not Player target)
            throw new InvalidOperationException("Selected status resistance requires actual Player target.");
        int currentResistance = target.GetGameStats().GetResistance(resistance).GetCurrent();
        if (type != 1)
            currentResistance = checked(currentResistance + target.GetGameStats().GetAbnormalResistance().GetCurrent());
        int currentPenetration = npc.GetGameStats().GetStat(penetration, 0).GetCurrent();
        int threshold = StatusThreshold(currentResistance, currentPenetration, SkyTempleCombatInputs.Pose(target));
        return SkyTempleNpcCombat.Draw(1, 1000) > threshold;
    }

    internal static int StatusThreshold(int resistance, int penetration, byte pose)
    {
        // These are the native unsigned16 current status channels, not percentages.
        SkyTempleCombatInputs.Word(resistance, nameof(resistance));
        SkyTempleCombatInputs.Word(penetration, nameof(penetration));
        if (pose > 15) throw new ArgumentOutOfRangeException(nameof(pose));
        int difference = resistance - penetration;
        return Math.Clamp(F.Cvtt(F.Mul(difference, pose == 5 ? 0.3f : 1f)), 0, 1000);
    }

    private static void AddSuccess(Effect effect, EffectTemplate template, SpellStatus? spellStatus)
    {
        effect.AddSuccessEffect(template);
        if (spellStatus.HasValue) effect.SetSpellStatus(spellStatus.Value);
        // Preserve the actual host skill-reflection observer. No HP is applied by this helper.
        template.CalculateDamage(effect);
    }
}
