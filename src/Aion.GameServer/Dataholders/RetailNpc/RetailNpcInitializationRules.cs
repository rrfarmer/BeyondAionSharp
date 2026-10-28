using System.Collections.Immutable;

namespace Aion.GameServer.Dataholders.RetailNpc;

public enum RetailNpcInitializationStage
{
    SourceProjection,
    NativeDefaultResolution,
    EffectiveStatConstruction
}

public sealed record RetailNpcInitializationObligation(
    string Code, RetailNpcInitializationStage Stage, string RecordPath, string Reason);

public sealed class RetailNpcInitializationAssessment
{
    internal RetailNpcInitializationAssessment(RetailNpcTemplateSourceReceipt receipt,
        ImmutableArray<RetailNpcInitializationObligation> obligations)
    { Receipt = receipt; Obligations = obligations; }
    public RetailNpcTemplateSourceReceipt Receipt { get; }
    public RetailNpcInitializationStage Stage => RetailNpcInitializationStage.SourceProjection;
    public ImmutableArray<RetailNpcInitializationObligation> Obligations { get; }
    public bool CanConstructOrdinarySeed => false;
    public bool CanConstructEffectiveInputs => false;
    public bool ActivationReady => false;
}

/// <summary>
/// Explicit boundary between source parsing and native initialization. No complete constructor/
/// default/effect initialization contract has yet passed U00 instruction/fixture review. The 703
/// byte-verified normal-consumer instructions do not prove those stages. Consequently this unit
/// reports obligations and refuses seed/effective construction; it implements no speculative
/// ratio default, HP/MP rule, effect pass, caps, bounds scaling or generic refill.
///
/// Evidence: phase2/effective-profile-consumers/field-contract.json and fixture-gaps.csv;
/// Npc::CalcFirstLevelAttr 0x140352270, CalcSecondLevelAttr 0x1403580f0, NPCDB parser 0x14027e090.
/// Java NpcData and StatsTemplate remain the integration reference, not a default provider here.
/// </summary>
public static class RetailNpcInitializationRules
{
    public static ImmutableArray<string> InstructionCompleteInitializationContracts => [];

    public static RetailNpcInitializationAssessment Assess(RetailNpcTemplateInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        string ratio = inputs.StatRatio.Presence == RetailNpcSourcePresence.Absent
            ? "Absent stat_ratio needs the pinned loaded level-table/default path; no 1000 fallback is supplied"
            : "Explicit stat_ratio, including zero, needs its own reviewed parser rule before any effect/clamp stage";
        return new(inputs.Receipt, [
            new("U00-G01", RetailNpcInitializationStage.NativeDefaultResolution, "/npc", "Complete constructor/parser width and absent/zero instruction contracts are not reviewed"),
            new("U00-G02", RetailNpcInitializationStage.NativeDefaultResolution, "/npc/stat_ratio", ratio),
            new("U00-G03", RetailNpcInitializationStage.EffectiveStatConstruction, "/npc/level", "Actual kind, spawn level, owner/master, growth and difficulty context must prove the ordinary branch"),
            new("U00-G04", RetailNpcInitializationStage.EffectiveStatConstruction, "/npc", "Current effect/AI override mapping, operation order and snapshot version are unresolved; do not apply generic functions twice"),
            new("U00-G05", RetailNpcInitializationStage.EffectiveStatConstruction, "/npc/max_hp", "Initial/current/recomputed HP and MP lifecycle must be resolved before life-stat construction; raw MP is not effective MP"),
            new("U00-G06", RetailNpcInitializationStage.NativeDefaultResolution, "/npc/abnormal_resists", "Native 25-slot names/indexes, aliases, maximum merges and placeholder behavior need their own proof"),
            new("U00-G07", RetailNpcInitializationStage.EffectiveStatConstruction, "/npc/hp_regen", "Regen scalar units, split-server tick/state and return sequence are not resolved by this source projection"),
            new("U00-G08", RetailNpcInitializationStage.EffectiveStatConstruction, "/npc/bound_radius", "Visual bounds scaling and movement-mode/current-base/effect construction need reviewed inputs"),
            new("U00-G09", RetailNpcInitializationStage.EffectiveStatConstruction, "/npc/attack_delay", "Ordinary NPCServer baseline delay and loaded motion identity are required; raw delay is not a complete clock"),
            new("U00-G10", RetailNpcInitializationStage.EffectiveStatConstruction, "/context/pve", "World, NPC-name, owner and current-effect PvE channels need a coherent loaded context"),
            new("U00-G11", RetailNpcInitializationStage.EffectiveStatConstruction, "/npc/skills", "All required static skill bindings remain effect-semantics unverified and must stay in the coherent package"),
            new("U00-G12", RetailNpcInitializationStage.EffectiveStatConstruction, "/npc", "Runtime narrowing, signed views and clamp boundaries require reviewed initialization contracts; no source-wide fallback applies")
        ]);
    }

    /// <summary>Visible guard for consumers that require more than admitted static source.</summary>
    public static void RequireEffectiveInputs(RetailNpcTemplateInputs inputs)
    {
        var assessment = Assess(inputs);
        throw new InvalidOperationException("Only static NPC source has been projected; ordinary default/effective construction is blocked by "
            + string.Join(", ", assessment.Obligations.Select(x => x.Code)) + ". No runtime binding is available.");
    }
}
