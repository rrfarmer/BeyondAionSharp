using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace Aion.GameServer.Dataholders.RetailNpc;

/// <summary>
/// Complete85-output native scalar evaluation with explicitly supplied161-pair aggregate.
/// Evidence: phase2/ordinary-effective-stats. No process/file access, implicit empty effects,
/// descriptor activation/expiry, current HP rescaling, PvE folding or runtime routing.
/// </summary>
public static class RetailNpcOrdinaryScalarEvaluation
{
    public const int ModifierPairCount = 161;
    public const int ModifierContentBytes = ModifierPairCount * 8;

    public static RetailNpcModifierAggregateAdmissionResult AdmitModifierAggregate(
        IReadOnlyList<RetailNpcModifierPairInput> all161Pairs, RetailNpcModifierAggregateReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(all161Pairs); ArgumentNullException.ThrowIfNull(receipt);
        try
        {
            Need(all161Pairs.Count == ModifierPairCount, "modifier_coverage", "/modifiers", "Exactly161 explicit pairs required");
            Need(receipt.Source is not null && receipt.Source.MainSha256 == RetailNpcTemplateProjection.MainBinarySha256
                && receipt.Source.NpcSha256 == RetailNpcTemplateProjection.NpcBinarySha256,
                "modifier_binary", "/receipt/source", "Pinned native source/build required");
            foreach (string hash in new[] { receipt.Source.DatasetId, receipt.Source.OutputManifestSha256,
                receipt.Source.RawRecordSha256, receipt.Source.SemanticRecordSha256, receipt.Source.SourceFileSha256,
                receipt.RatioTableSha256, receipt.PackageContextSha256, receipt.ContentSha256 }) Hash(hash);
            Need(receipt.Source.NpcId > 0 && !string.IsNullOrWhiteSpace(receipt.Source.Devname)
                && !string.IsNullOrWhiteSpace(receipt.Source.Region), "modifier_source", "/receipt/source", "Complete source identity required");
            Need(receipt.ContentBytes == ModifierContentBytes, "modifier_bytes", "/receipt", "Native array is exactly0x508 bytes");
            Need(receipt.Origin is RetailNpcModifierInputOrigin.FixedMathematicalFixture or RetailNpcModifierInputOrigin.ExternalActorSnapshot,
                "modifier_origin", "/receipt", "Explicit supported input origin required");
            Need(Text(receipt.Epoch, 256) && Text(receipt.EvidenceReference, 4096), "modifier_epoch", "/receipt", "Epoch and evidence reference required");
            Need(receipt.Origin == RetailNpcModifierInputOrigin.ExternalActorSnapshot ? Text(receipt.ActorObjectIdentity, 256)
                : receipt.ActorObjectIdentity is null, "modifier_actor", "/receipt", "External snapshot requires actor identity; fixed fixture has no observed actor");
            var seen = new bool[ModifierPairCount];
            var pairs = new RetailNpcModifierPairInput[ModifierPairCount];
            byte[] bytes = new byte[ModifierContentBytes];
            for (int i = 0; i < ModifierPairCount; i++)
            {
                var pair = all161Pairs[i];
                Need(pair is not null && pair.Index is >= 0 and < ModifierPairCount && !seen[pair.Index],
                    "modifier_indices", "/modifiers", "Unique native indices0..160 required");
                seen[pair.Index] = true; pairs[pair.Index] = new(pair.Index, pair.Percent, pair.Flat);
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(pair.Index * 8, 4), pair.Percent);
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(pair.Index * 8 + 4, 4), pair.Flat);
            }
            Need(seen.All(x => x) && Convert.ToHexStringLower(SHA256.HashData(bytes)) == receipt.ContentSha256,
                "modifier_hash", "/receipt", "Complete canonical array hash differs from caller pin");
            return new(new(pairs.ToImmutableArray(), bytes.ToImmutableArray(), receipt), []);
        }
        catch (ScalarError e) { return new(null, [new(e.Code, e.Path, e.Message)]); }
        catch (Exception e) when (e is InvalidDataException or ArgumentException or InvalidOperationException or OverflowException)
        { return new(null, [new("modifier_contract", "/modifiers", e.Message)]); }
    }

    public static RetailNpcOrdinaryScalarEvaluationResult Evaluate(RetailNpcOrdinaryScalarInputs bases,
        RetailNpcModifierAggregate aggregate, RetailNpcOrdinaryScalarEvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(bases); ArgumentNullException.ThrowIfNull(aggregate); ArgumentNullException.ThrowIfNull(context);
        try
        {
            foreach (string hash in new[] { context.ExpectedRatioTableSha256, context.ExpectedPackageContextSha256,
                context.ExpectedModifierContentSha256 }) Hash(hash);
            var receipt = aggregate.Receipt;
            Need(context.ExpectedSource == bases.SourceReceipt && receipt.Source == bases.SourceReceipt,
                "scalar_effect_source", "/context/source", "Full source/region/build identities must match");
            Need(context.ExpectedRatioTableSha256 == bases.RatioTableReceipt.SourceSha256
                && receipt.RatioTableSha256 == context.ExpectedRatioTableSha256
                && receipt.PackageContextSha256 == context.ExpectedPackageContextSha256,
                "scalar_effect_context", "/context", "Package and level-table context must match");
            Need(context.ExpectedModifierContentSha256 == receipt.ContentSha256 && context.ExpectedEpoch == receipt.Epoch
                && Text(context.ExpectedEpoch, 256) && context.ExpectedOrigin == receipt.Origin
                && context.ExpectedActorObjectIdentity == receipt.ActorObjectIdentity,
                "scalar_effect_epoch", "/context", "Caller-pinned aggregate, origin, actor and epoch must match");
            Need(context.MasterObjectId == 0u, "scalar_effect_master", "/context/master_object_id",
                "Current native master ID +188c must be explicitly zero; a seed assertion or null profile cannot prove it");
            Need(context.SourceRelatedObjectId == 0u, "scalar_effect_related", "/context/source_related_object_id",
                "Current native source-related ID +1894 must be explicitly zero; null/nonzero is outside this domain");
            Need(context.NativeKind == 1 && context.VirtualProfileRecordPresent == false
                && context.DifficultyRecordPresent == false && context.DualWeaponFlagPresent == false,
                "scalar_effect_domain", "/context", "Current ordinary kind1, absent virtual profile/difficulty and nondual eligibility required");
            Need(bases.Fields.Count == RetailNpcOrdinaryScalarProjection.OutputCount && aggregate.Pairs.Length == ModifierPairCount,
                "scalar_coverage", "/fields", "Complete scalar and modifier inputs required");
            var values = new Dictionary<string, RetailNpcOrdinaryScalarValue>(StringComparer.Ordinal);
            var consumed = new Dictionary<int, RetailNpcConsumedModifierComponent>();
            foreach (var specification in RetailNpcOrdinaryScalarProjection.Specifications)
            {
                Need(bases.Fields.TryGetValue(specification.Field, out var source) && source.Specification == specification,
                    "scalar_coverage", "/fields/" + specification.Field, "Field contract differs from the pinned85-field registry");
                int? percent = null, flat = null; long? product = null, contribution = null;
                long calculated = source.Value;
                if (specification.Operation != RetailNpcScalarOperation.Copy)
                {
                    Need(specification.ModifierIndex is >= 0 and < ModifierPairCount, "scalar_channel", "/fields", "Missing native modifier index");
                    int index = specification.ModifierIndex.GetValueOrDefault();
                    var pair = aggregate.Pairs[index]; flat = pair.Flat;
                    if (specification.Operation == RetailNpcScalarOperation.PercentAndFlat)
                    {
                        percent = pair.Percent; product = checked(source.Value * (long)pair.Percent);
                        contribution = product.Value / 100; // Signed integer division truncates toward zero, like the verified native sequence.
                        calculated = checked(source.Value + contribution.Value + pair.Flat);
                        consumed[index] = new(index, true, true);
                    }
                    else
                    {
                        Need(source.Value == 0, "scalar_generated_seed", "/fields/" + specification.Field, "Generated heal-boost seed must be zero");
                        calculated = checked(source.Value + pair.Flat); consumed[index] = new(index, false, true);
                    }
                }
                Need(specification.Minimum.HasValue == specification.Maximum.HasValue, "scalar_clamp", "/fields", "Both clamp bounds or neither required");
                long result = specification.Minimum.HasValue
                    ? Math.Clamp(calculated, specification.Minimum.Value, specification.Maximum.GetValueOrDefault()) : calculated;
                values.Add(specification.Field, new(source, new(source.Value, percent, flat, product, contribution,
                    calculated, specification.Minimum, specification.Maximum, result)));
            }
            Need(values.Count == RetailNpcOrdinaryScalarProjection.OutputCount, "scalar_coverage", "/fields", "Atomic complete result required");
            return new(new(bases, aggregate, context, values, consumed.Values.OrderBy(x => x.Index)), []);
        }
        catch (ScalarError e) { return new(null, [new(e.Code, e.Path, e.Message)]); }
        catch (Exception e) when (e is InvalidDataException or ArgumentException or InvalidOperationException or KeyNotFoundException or OverflowException)
        { return new(null, [new("scalar_evaluation_contract", "/fields", e.Message)]); }
    }

    private static bool Text(string? value, int limit) => !string.IsNullOrWhiteSpace(value) && value.Length <= limit;
    private static void Hash(string value) => Need(value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'),
        "scalar_hash", "/receipt", "Expected lowercase SHA256");
    private static void Need([DoesNotReturnIf(false)] bool condition, string code, string path, string message)
    { if (!condition) throw new ScalarError(code, path, message); }
    private sealed class ScalarError(string code, string path, string message) : Exception(message)
    { internal string Code => code; internal string Path => path; }
}
