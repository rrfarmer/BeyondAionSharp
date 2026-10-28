using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Materializes the independent percentage/flat NPC normal-attack
/// channels. Native GetBonusAttrTo adds/subtracts signed dword halves separately;
/// ChangeAttackParamater 14035a280 converts the percentage once, then
/// CalcNormalAttackDamage 140369e10 adds it to the selected profile rate.
/// This deliberately does not evaluate a Java/player stat program on a sample.
/// Proof: phase2/ordinary-effective-stats/modifier-origins.json and
/// phase3/npc-to-user-normal-closure/NORMAL-USER-CLOSURE.md.</summary>
internal static partial class RetailNpcStatNormalInputs
{
    /// <summary>Read before drawing attack RNG. Only stat operators with a proved
    /// NPC channel translation are admitted. Keep the actual proxy for condition
    /// validation so owner-sensitive effects are not replaced by a detached leaf.</summary>
    internal static Modifiers CurrentModifiers(Npc owner, bool physical) =>
        CurrentModifiers(owner, physical ? StatEnum.PHYSICAL_ATTACK : StatEnum.MAGICAL_ATTACK);

    /// <summary>Primary spells consume the independent BoostSpellAttackEffect
    /// pair, not the ordinary MAGICAL_ATTACK channel. Share only extraction and
    /// validation; each native consumer retains its own arithmetic order.</summary>
    internal static Modifiers CurrentModifiers(Npc owner, StatEnum name)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (name is not (StatEnum.PHYSICAL_ATTACK or StatEnum.MAGICAL_ATTACK or StatEnum.BOOST_SPELL_ATTACK))
            throw new ArgumentOutOfRangeException(nameof(name), "Unproved NPC attack modifier channel");
        var probe = new AdditionStat(name, 0, owner);
        int percent = 0, flat = 0;
        foreach (var original in owner.GetGameStats().GetStatsSorted(name))
        {
            IStatFunction inner = original;
            if (inner is StatFunctionProxy proxy)
            {
                inner = proxy.GetProxiedFunction();
                // The existing proxy validator accepts a StatFunction leaf.
                // Do not invent semantics for a nested proxy that it cannot call.
                if (inner is StatFunctionProxy)
                    throw new InvalidOperationException("Unsupported nested NPC normal stat proxy");
            }
            Type type = inner.GetType();
            if (type != typeof(StatRateFunction) && type != typeof(StatAddFunction)
                && type != typeof(StatSubFunction) && type != typeof(StatFunction))
                throw new InvalidOperationException("Unsupported NPC normal stat operator: " + type.Name);
            if (original.GetName() != name)
                throw new InvalidOperationException("NPC normal stat channel differs from its registered key");
            if (!original.Validate(probe)) continue;
            if (type == typeof(StatRateFunction)) percent = unchecked(percent + inner.GetValue());
            else if (type == typeof(StatAddFunction)) flat = unchecked(flat + inner.GetValue());
            else if (type == typeof(StatSubFunction)) flat = unchecked(flat - inner.GetValue());
            // Exact StatFunction.Apply is a no-op. In particular, a nonzero
            // Value on that base class is not an implicit additive modifier.
        }
        return new(percent, flat);
    }

}
