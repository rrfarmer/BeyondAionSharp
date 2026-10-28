using F = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcNormalDamageStages;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Pure source-backed late dual split and shared normal outcome. No actor,
/// stat lookup, RNG, scheduling, protection or HP operation occurs here. Native:
/// Npc::CalcSecondLevelAttr 14035a090..14035a10e; World::AttackCreatureByNpc
/// 14068f150; CalcNormalAttackDamage/CheckNormalAttackActiveDefend/Resist.
/// This domain requires effective main/offhand hit counts1, not a multi-hit shortcut.</summary>
internal static partial class RetailNpcDualNormalPlan
{
    internal readonly record struct NativeStatus(byte Base, byte Critical)
    {
        // Native +30/+31 record bytes are distinct; the 4.8 packet packs them.
        internal byte Packed => unchecked((byte)((Critical << 4) | Base));
    }
    internal sealed class SharedOutcome
    {
        internal AttackStatus MainStatus { get; }
        internal bool MainTerminal { get; }
        internal AttackStatus? OffhandStatus { get; }
        internal NativeStatus MainNative { get; }
        internal NativeStatus? OffhandNative { get; }
        internal bool Critical { get; }
        // 0normal,1dodge,2parry,3block,4resist are the shared native decisions.
        internal int SharedActiveDefense { get; }
        private SharedOutcome(AttackStatus status, byte nativeBase, int decision)
        {
            MainStatus = status; SharedActiveDefense = decision;
            Critical = status.IsCritical(); MainTerminal = decision is 1 or 4;
            MainNative = new(nativeBase, Critical ? (byte)12 : (byte)0);
            if (!MainTerminal)
            {
                OffhandStatus = AttackStatusExtensions.GetOffHandStats(status);
                OffhandNative = new((byte)(nativeBase + 1), Critical ? (byte)13 : (byte)0);
            }
        }
        internal static SharedOutcome FromMain(AttackStatus status) => status switch
        {
            AttackStatus.NORMALHIT or AttackStatus.CRITICAL => new(status, 10, 0),
            AttackStatus.DODGE or AttackStatus.CRITICAL_DODGE => new(status, 0, 1),
            AttackStatus.PARRY or AttackStatus.CRITICAL_PARRY => new(status, 2, 2),
            AttackStatus.BLOCK or AttackStatus.CRITICAL_BLOCK => new(status, 4, 3),
            AttackStatus.RESIST or AttackStatus.CRITICAL_RESIST => new(status, 6, 4),
            _ => throw new ArgumentOutOfRangeException(nameof(status), "An actual main-hand outcome is required")
        };
    }

    internal static SharedOutcome Share(AttackStatus mainStatus) => SharedOutcome.FromMain(mainStatus);

    /// <summary>The NPC offhand draws its own base only after the successful main
    /// record. It consults the cached active-defense and critical decisions and
    /// does not roll those again. A main dodge/resist terminates before an offhand
    /// base request. No additional-hit RNG occurs for current counts1/1.</summary>
    internal static bool ShouldCalculateOffhand(SharedOutcome main)
    {
        ArgumentNullException.ThrowIfNull(main);
        return !main.MainTerminal;
    }

    /// <summary>Pure cached-outcome arithmetic after the offhand's own armor/element
    /// defense. Supply actual current shield/critical-reduction values. PvE, final
    /// flooring/truncation and protections remain with the existing per-hand owner.</summary>
    internal static float ApplySharedOffhand(float afterDefense, SharedOutcome main, bool physical,
        uint attackerStatRatio, int criticalDamageReduction, int shieldRemainingPercent, int shieldMaximumAbsorbed, bool npcDefender = false)
    {
        ArgumentNullException.ThrowIfNull(main);
        if (main.MainTerminal) throw new InvalidOperationException("Terminal main normal has no offhand record");
        if (!float.IsFinite(afterDefense) || afterDefense < 0
            || attackerStatRatio is < 1000 or > 65000 || shieldMaximumAbsorbed < 0)
            throw new ArgumentOutOfRangeException(nameof(afterDefense));
        float value = afterDefense;
        if (npcDefender && main.SharedActiveDefense is 2 or 3) value = F.Multiply(value, .9f);
        else if (main.SharedActiveDefense == 3)
            value = SkyTempleNpcCombat.BlockDamage(value, shieldRemainingPercent, shieldMaximumAbsorbed);
        else if (main.SharedActiveDefense == 2) value = F.Multiply(value, .6f);
        if (main.Critical)
        {
            int normalized = F.TruncateInt32(F.Divide(F.FromInt(criticalDamageReduction), F.Divide(attackerStatRatio, 1000f)));
            float coefficient = Math.Max(1f, F.Subtract(physical ? 1.5f : 1f, F.Divide(F.FromInt(normalized), 1000f)));
            value = F.Multiply(value, coefficient);
        }
        return value;
    }
}
