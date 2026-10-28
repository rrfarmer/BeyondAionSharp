using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using System.Runtime.CompilerServices;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils.Stats;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>The weapon contribution to a retained 4.8 physical skill recipe.
/// One source right-hand sample supplies both its raw percentage basis and its
/// current-stat-adjusted weapon contribution. This does not claim that the
/// retained skill scalar, statuses or secondary effects are native equivalents.
/// Source proof: stats-only-20261025-01/combat-worker/legacy-skill-review and
/// dual-normal. Source-native primary calculators bypass AttackUtil's legacy path.</summary>
internal static class RetailNpcLegacySkillWeapon
{
    private static readonly ILogger Log = AionLog.For(nameof(RetailNpcLegacySkillWeapon));
    private static readonly ConditionalWeakTable<Npc,HashSet<string>> CompatibilityWarnings = new();
    internal readonly record struct Sample(int RawBase, float WeaponDamage);

    internal static bool TryPhysical(Effect effect, SkillElement element, bool templateDamage,
        AttackStatus status, ISet<CalculationType> calculationTypes, out Sample result)
    {
        result = default;
        if (templateDamage || element != SkillElement.NONE || effect.GetEffector() is not Npc npc
            || effect.GetEffected() is not Player || !calculationTypes.Contains(CalculationType.SKILL)
            || !RetailNpcStatCatalog.TryGet(npc, out var profile)) return false;
        bool terminal = AttackStatusExtensions.GetBaseStatus(status) is AttackStatus.DODGE or AttackStatus.RESIST;
        if (terminal) return true;
        RetailNpcStatNormalInputs.Modifiers modifiers;
        try { modifiers = RetailNpcStatNormalInputs.CurrentModifiers(npc,true); }
        catch (InvalidOperationException e)
        {
            var warnings = CompatibilityWarnings.GetValue(npc,_=>[]);
            lock(warnings)
                if(warnings.Add(e.Message))
                    Log.LogWarning("RETAIL_STAT_LEGACY_WEAPON_COMPAT npc={Npc} reason={Reason}; existing4.8 weapon basis retained",
                        profile.NpcId,e.Message);
            return false;
        }
        bool dual = profile.Source.Integers["dual"].ParsedValue is not (null or 0);
        result = Create(profile.MinDamage, profile.MaxDamage, dual, terminal, SkyTempleNpcCombat.Draw,
            sample => WeaponContribution(sample,modifiers));
        return true;
    }

    internal static float WeaponContribution(int sample, RetailNpcStatNormalInputs.Modifiers modifiers) =>
        RetailNpcStatSkillCombat.StatisticsPhysicalBase(sample,0,modifiers,0,0);

    /// <summary>Pure sampling boundary for deterministic witnesses. Native dual
    /// endpoints are u16 words, halved at consumption, not constructor seeds.
    /// Validated percentage and flat effect channels are independent; no Java
    /// flat/rate cross-term and no legacy80–120% reroll.</summary>
    internal static Sample Create(int minimum, int maximum, bool dual, bool terminal,
        System.Func<int, int, int> draw, System.Func<int, float> applySkillStats)
    {
        if (minimum is < 0 or > 65535 || maximum < minimum || maximum > 65535)
            throw new ArgumentOutOfRangeException(nameof(minimum), "Source weapon range must be ordered u16 endpoints");
        if (terminal) return default;
        int low = dual ? minimum / 2 : minimum;
        int high = dual ? maximum / 2 : maximum;
        int raw = draw(low, high);
        if (raw < low || raw > high) throw new InvalidOperationException("Source weapon draw outside selected right-hand range");
        return new(raw, applySkillStats(raw));
    }
}
