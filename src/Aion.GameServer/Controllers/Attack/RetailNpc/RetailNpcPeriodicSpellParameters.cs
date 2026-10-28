using System.Collections.Immutable;
using System.Globalization;
using P = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcSpellInstantParameters;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Selected complete pilot source, not loaded actors or effect admission.
/// Reprojects the original spell capability rather than trusting its exposed fields.
/// Native consumers: bbe370/bbed40 and the Bleed vtable overrides; external complete
/// PE proof a12032f354bd4c9f82d88a43b4846ec47d366de1687212471f58d7f4bb4f8484.
/// Existing instant selection proves the whole record and ordered NPC binding.
/// This child selects effect2 independently; it does not dispatch either effect.</summary>
internal static class RetailNpcPeriodicSpellParameters
{
    internal enum Family { SpellAttack, Bleed, Poison, SpellDrain }
    internal sealed record Scalar(int Value, bool SourcePresent, string? RawText, string DefaultRule);
    internal sealed class SelectedPeriodic
    {
        internal P.SelectedSpell CompleteSource { get; }
        internal Family Kind { get; }
        internal int EffectIndex => 1;
        internal int EffectId { get; }
        internal int Remain1 { get; }
        internal int Remain2 { get; }
        internal int CheckTime { get; }
        internal int R8 { get; }
        internal int R9 { get; }
        internal int Reserved11 { get; }
        internal int CriticalProbabilityMod2 { get; }
        internal Scalar NoResist { get; }
        internal Scalar DurationJitter { get; }
        internal Scalar MagicalDefenseGateR12 { get; }
        internal Scalar SharingR19 { get; }
        internal Scalar CriticalDamageMod1 { get; }
        internal Scalar CriticalDamageMod2 { get; }
        internal string Element { get; }
        internal string? HopType { get; }
        internal ImmutableArray<P.SourceField> OrderedEffectFields { get; }
        internal bool IsLoadedRegionObservation => false;
        private SelectedPeriodic(P.SelectedSpell source, Spec spec)
        {
            CompleteSource = source; Kind = spec.Kind;
            EffectId = Integer(source, "effect2_effectid");
            Remain1 = Integer(source, "effect2_remain1"); Remain2 = Integer(source, "effect2_remain2");
            CheckTime = Integer(source, "effect2_checktime");
            R8 = Integer(source, "effect2_reserved8"); R9 = Integer(source, "effect2_reserved9");
            Reserved11 = Integer(source, "effect2_reserved11");
            CriticalProbabilityMod2 = Integer(source, "effect2_critical_prob_mod2");
            NoResist = OptionalZero(source, "effect2_noresist");
            DurationJitter = OptionalZero(source, "effect2_randomtime");
            MagicalDefenseGateR12 = OptionalZero(source, "effect2_reserved12");
            SharingR19 = OptionalZero(source, "effect2_reserved19");
            CriticalDamageMod1 = OptionalZero(source, "effect2_critical_add_dmg_mod1");
            CriticalDamageMod2 = OptionalZero(source, "effect2_critical_add_dmg_mod2");
            Element = One(source, "effect2_reserved10");
            var hop = source.Occurrences("effect2_hop_type");
            Require(hop.Length <= 1, "Repeated periodic hop field");
            HopType = hop.Length == 0 ? null : hop[0].Text;
            OrderedEffectFields = source.OrderedFields.Where(x => x.Name.StartsWith("effect2_", StringComparison.Ordinal)).ToImmutableArray();
            Require(One(source, "effect2_type") == (Kind == Family.Bleed ? "Bleed" : "SpellATK")
                && EffectId == spec.EffectId && Remain1 == spec.Remain1 && Remain2 == spec.Remain2
                && CheckTime == spec.Interval && R8 == 1 && R9 == spec.R9 && Reserved11 == 100
                && CriticalProbabilityMod2 == 100 && NoResist.Value == spec.NoResist
                && Element == spec.Element && DurationJitter.Value == 0 && MagicalDefenseGateR12.Value == 0
                && SharingR19.Value == 0 && CriticalDamageMod1.Value == 0 && CriticalDamageMod2.Value == 0,
                "Selected periodic source contract differs");
        }
        internal static SelectedPeriodic Reproject(P.SelectedSpell source)
        {
            var original = P.ProjectSelectedSpell(source.RawBytes.AsSpan(), source.Binding, P.RequiredPins);
            Require(Specs.TryGetValue(original.Id, out var spec), "Selected spell has no supported pilot periodic effect");
            return new(original, spec!);
        }
    }
    private sealed record Spec(Family Kind, int EffectId, int Remain1, int Remain2,
        int Interval, int R9, int NoResist, string Element);
    private static readonly ImmutableDictionary<int, Spec> Specs = new Dictionary<int, Spec>
    {
        [16576] = new(Family.SpellAttack, 10165762, 80, 4100, 2000, 15, 0, "Fire"),
        [16589] = new(Family.SpellAttack, 10165892, 200, 8100, 2000, 15, 0, "Fire"),
        [16592] = new(Family.SpellAttack, 10165922, 200, 8100, 2000, 10, 0, "Fire"),
        [16594] = new(Family.Bleed, 20006, 300, 6100, 3000, 49, 0, "Air"),
        [16710] = new(Family.SpellAttack, 80001, 200, 8100, 2000, 10, 1, "Fire")
    }.ToImmutableDictionary();

    internal static SelectedPeriodic Project(P.SelectedSpell source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return SelectedPeriodic.Reproject(source);
    }
    private static string One(P.SelectedSpell source, string name)
    {
        var fields = source.Occurrences(name);
        Require(fields.Length == 1, "Expected exactly one source field: " + name);
        return fields[0].Text;
    }
    private static int Integer(P.SelectedSpell source, string name)
    {
        Require(int.TryParse(One(source, name), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value),
            "Expected source int32: " + name);
        return value;
    }
    private static Scalar OptionalZero(P.SelectedSpell source, string name)
    {
        var fields = source.Occurrences(name);
        Require(fields.Length <= 1, "Repeated source field: " + name);
        return fields.Length == 0
            ? new(0, false, null, "Skill constructor clears each complete 0x210 effect; selected parser leaves absent field unchanged")
            : new(Integer(source, name), true, fields[0].Text, "explicit source value");
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new ArgumentException(message);
    }
}
