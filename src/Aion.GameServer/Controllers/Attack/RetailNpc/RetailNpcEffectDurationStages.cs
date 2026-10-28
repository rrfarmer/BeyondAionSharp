using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using S = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcPeriodicSpellParameters;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Pure finite duration arithmetic, not current actor admission, a Check result or
/// an effect/timer owner. Main bbba10 and 33e050 are complete PE-checked bodies.
/// Source-projected parameters do not certify installed fields or caller getters.
/// Java/current Effect and AbstractOverTimeEffect were read first; the approved current
/// interval tick-count/cleanup fixes are not changed by these inactive helpers.</summary>
internal static class RetailNpcEffectDurationStages
{
    internal sealed record Formula(int DurationType180, short SkillLevel, int Remain1,
        int Remain2, int CalculatedJitter, uint CheckTime);
    /// <summary>Mathematical operands only. ApplicablePvpPercent has already been selected
    /// by the owning caller's actual ordered actor/classifier reads; it is not that proof.</summary>
    internal sealed record Modifiers(float? CallerSubtypeMultiplier, uint? ApplicablePvpPercent);
    internal sealed record Calculation(bool Infinite, int? WrappedBase,
        float? CallerProduct, int? AfterCaller, float? PvpRatio, float? PvpProduct,
        int? AfterPvp, uint? IntervalRemainder, uint Value)
    {
        internal bool AppliesHpOrSchedules => false;
        internal bool CurrentOperandsProved => false;
    }
    internal sealed record Unified(ImmutableArray<uint> Durations, uint FirstNonzero)
    {
        internal bool HasDeathPenaltyAdjustment => false;
        internal bool ActivationReady => false;
    }

    internal static Calculation CalculateBase(Formula formula, Modifiers modifiers)
    {
        ArgumentNullException.ThrowIfNull(formula);
        // bbba10 returns the sentinel before jitter, actor and interval operands.
        if (formula.DurationType180 is 1 or 8 || formula.Remain2 == 86_400_000)
            return new(true, null, null, null, null, null, null, null, uint.MaxValue);
        ArgumentNullException.ThrowIfNull(modifiers);
        if (modifiers.ApplicablePvpPercent.HasValue && !modifiers.CallerSubtypeMultiplier.HasValue)
            throw new ArgumentException("The native PvP duration child is nested inside the non-null caller branch.");
        int basis = unchecked(formula.Remain1 * formula.SkillLevel - formula.CalculatedJitter + formula.Remain2);
        int duration = basis;
        float? callerProduct = null, pvpRatio = null, pvpProduct = null;
        int? afterCaller = null, afterPvp = null;
        if (modifiers.CallerSubtypeMultiplier is float multiplier)
        {
            Finite(multiplier);
            // MOVD/CVTDQ2PS is signed int32; CVTTSS2SI at bbbbce writes EBX.
            callerProduct = Mul(duration, multiplier);
            duration = Cvtt(callerProduct.Value); afterCaller = duration;
            if (modifiers.ApplicablePvpPercent is uint percent)
            {
                // MOV EAX zero-extends before CVTSI2SS XMM1,RAX: percent is uint32.
                pvpRatio = Div(percent, 100f);
                pvpProduct = Mul(pvpRatio.Value, duration);
                duration = Cvtt(pvpProduct.Value); afterPvp = duration;
            }
        }
        uint result = unchecked((uint)duration);
        uint? remainder = null;
        if (formula.CheckTime != 0)
        {
            // bbbc34 is unsigned DIV, even when the preceding signed duration is negative.
            remainder = result % formula.CheckTime;
            result = unchecked(result + 1000u - remainder.Value);
        }
        // This sign clamp does not apply to the early infinite sentinel.
        if (unchecked((int)result) < 0) result = 1000;
        return new(false, basis, callerProduct, afterCaller, pvpRatio, pvpProduct,
            afterPvp, remainder, result);
    }

    /// <summary>Reproject the complete source and original NPC binding. The five selected
    /// records omit jitter, whose constructed zero makes the native equal-bound RNG
    /// wrapper state-free. This is a source/math child, not loaded-field validation.</summary>
    internal static Calculation CalculateSelectedPeriodic(S.SelectedPeriodic selected,
        int currentDurationType180, Modifiers modifiers)
    {
        ArgumentNullException.ThrowIfNull(selected);
        var source = S.Project(selected.CompleteSource);
        if (source.DurationJitter.Value != 0)
            throw new ArgumentException("The selected source domain has no nonzero jitter producer.");
        return CalculateBase(new(currentDurationType180,
            checked((short)source.CompleteSource.Binding.SkillLevel), source.Remain1,
            source.Remain2, 0, checked((uint)source.CheckTime)), modifiers);
    }

    /// <summary>33e050 after per-slot virtual duration calls, restricted to skills outside
    /// 2058..206b death-penalty handling. Returned uint32 values convert exactly through
    /// positive binary64 and CVTTSD2SI64 before their low32 store. First NONZERO, including
    /// ffffffff, governs later nonzero slots; zero slots stay zero. No getter is executed.</summary>
    internal static Unified UnifyWithoutDeathPenalty(int skillId, ReadOnlySpan<uint> completeSlotResults)
    {
        if (unchecked((uint)(skillId - 0x2058)) < 0x14)
            throw new ArgumentException("Death-penalty duration adjustment requires its separate owner.");
        if (completeSlotResults.Length > 4)
            throw new ArgumentException("The finite output contract contains at most four duration words.");
        var result = ImmutableArray.CreateBuilder<uint>(completeSlotResults.Length);
        uint first = 0;
        foreach (uint value in completeSlotResults)
        {
            uint stored = value;
            if (stored != 0)
            {
                if (first == 0) first = stored;
                else stored = first;
            }
            result.Add(stored);
        }
        return new(result.MoveToImmutable(), first);
    }

    private static void Finite(float value)
    {
        if (!float.IsFinite(value))
            throw new ArithmeticException("Exceptional duration operands are outside this finite child.");
    }
    private static int Cvtt(float value)
    {
        if (!float.IsFinite(value) || value < -2147483648f || value >= 2147483648f)
            throw new ArithmeticException("Unrepresentable CVTTSS2SI is outside this finite duration child.");
        return (int)value;
    }
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Mul(float a, float b) => a * b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Div(float a, float b) => a / b;
}
