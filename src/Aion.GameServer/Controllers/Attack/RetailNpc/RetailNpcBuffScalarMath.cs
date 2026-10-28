using System.Runtime.CompilerServices;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

// Native NPC getters only. No movement-base selection, path or task owner.
internal static class RetailNpcBuffScalarMath
{
    private static readonly ILogger Log = AionLog.For(nameof(RetailNpcBuffScalarMath));
    private static readonly ConditionalWeakTable<Npc, HashSet<string>> Warnings = new();

    // Npc::CalcSecondLevelAttr 1403580f0: signed64 division, global caps,
    // then bounds derived from the original delay. Modifier29 subtracts.
    internal static int AttackDelay(int basis, int percent, int flat)
    {
        if (basis is < 500 or > 10000) throw new ArgumentOutOfRangeException(nameof(basis));
        long value = Math.Clamp(basis + (long)basis * percent / 100 + flat, 500, 10000);
        return (int)Math.Clamp(value, basis / 2, basis * 2);
    }

    // Npc::CalcCurSpeed 1400bc8e0: SSE binary32 order; flat input has already
    // been translated from native tenths to managed thousandths of m/s.
    internal static float Speed(float basisScaled, int percent, int flatScaled)
    {
        if (!float.IsFinite(basisScaled) || basisScaled < 0) throw new ArgumentOutOfRangeException(nameof(basisScaled));
        float basis = RetailNpcNormalDamageStages.Divide(basisScaled, 1000f);
        float delta = RetailNpcNormalDamageStages.Add(RetailNpcNormalDamageStages.Multiply(
            RetailNpcNormalDamageStages.Multiply(percent, basis), 0.01f),
            RetailNpcNormalDamageStages.Divide(flatScaled, 1000f));
        float value = Math.Clamp(RetailNpcNormalDamageStages.Add(delta, basis), 0.1f, 16f);
        value = Math.Max(value, RetailNpcNormalDamageStages.Multiply(basis, 0.1f));
        value = Math.Min(value, RetailNpcNormalDamageStages.Multiply(basis, 2f));
        return RetailNpcNormalDamageStages.Multiply(Math.Max(0, value), 1000f);
    }

    internal static bool TryApply(Npc npc, StatEnum name, Stat2 stat)
    {
        if (name is not (StatEnum.SPEED or StatEnum.ATTACK_SPEED) || !RetailNpcStatCatalog.TryGet(npc, out var profile)) return false;
        var functions = npc.GetGameStats().GetStatsSorted(name).ToArray();
        if (!functions.Any(RetailNpcStatBuffs.OwnsSelectedFunction)) return false;
        bool Compatibility(string reason)
        {
            var warnings = Warnings.GetValue(npc, _ => []);
            lock (warnings) if (warnings.Add(name + ":" + reason))
                Log.LogWarning("RETAIL_STAT_BUFF_SCALAR_COMPAT npc={Npc} stat={Stat} reason={Reason}; retained host getter", npc.GetNpcId(), name, reason);
            return false;
        }
        if (stat.GetType() != typeof(AdditionStat) || stat.GetBaseRate() != 1 || stat.GetBonusRate() != 1
            || stat.GetFixedBonusRate() != 0 || stat.GetFinalRate() != 1 || stat.GetExactBonus() != 0)
            return Compatibility("nonstandard-getter");
        if (name == StatEnum.ATTACK_SPEED && stat.GetExactBaseWithoutBaseRate() != profile.AttackDelay)
            return Compatibility("nonstandard-delay-base");
        int percent = 0, flat = 0;
        foreach (var function in functions)
        {
            var leaf = function is StatFunctionProxy proxy ? proxy.GetProxiedFunction() : function;
            var type = leaf.GetType();
            if (function.GetName() != name || type != typeof(StatFunction) && type != typeof(StatRateFunction)
                && type != typeof(StatAddFunction) && type != typeof(StatSubFunction)) return Compatibility("operator-" + type.Name);
            if (!function.Validate(stat)) continue;
            if (type == typeof(StatRateFunction)) percent = unchecked(percent + leaf.GetValue());
            else if (type == typeof(StatAddFunction)) flat = unchecked(flat + leaf.GetValue());
            else if (type == typeof(StatSubFunction)) flat = unchecked(flat - leaf.GetValue());
        }
        float basis = stat.GetExactBaseWithoutBaseRate();
        float value = name == StatEnum.ATTACK_SPEED ? AttackDelay(profile.AttackDelay, percent, flat) : Speed(basis, percent, flat);
        // Keep the getter's base available to its existing timing consumers.
        stat.SetBonus(value - basis);
        // Preserve the protocol-unit integer before base/bonus reconstruction
        // can move a boundary by one binary32 ULP.
        stat.SetExactIntegerCurrent((int)value);
        return true;
    }
}
