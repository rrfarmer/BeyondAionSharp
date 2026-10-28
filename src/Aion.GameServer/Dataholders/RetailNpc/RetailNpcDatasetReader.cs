using System.Buffers;
using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace Aion.GameServer.Dataholders.RetailNpc;

/// <summary>
/// Reads the frozen source-extracted schema1. Every input is caller-pinned and fully
/// validated before an immutable cohort is returned. Provenance paths are never opened.
/// </summary>
public static class RetailNpcDatasetReader
{
    private static readonly string[] ArtifactNames =
    [
        "coverage.csv", "identity-blockers.csv", "pilot-profiles.jsonl", "profiles.jsonl",
        "provenance.json", "semantic-diff.json", "source-manifest.json", "summary.json",
        "unsupported-skill-bindings.csv", "validation.json"
    ];

    public static async Task<RetailNpcStaticDataset> LoadAsync(RetailNpcDatasetRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var limits = request.Limits ?? throw new ArgumentException("Resource limits are required");
        RetailNpcDatasetValidation.ValidateLimits(limits);
        RetailNpcDatasetValidation.Hash(request.ExpectedDatasetId);
        RetailNpcDatasetValidation.Hash(request.ExpectedOutputManifestSha256);
        ArgumentNullException.ThrowIfNull(request.RequiredNpcIdDevnames);
        var wanted = request.RequiredNpcIdDevnames.ToFrozenDictionary();
        RetailNpcDatasetValidation.Require(wanted.Count > 0 && wanted.Count <= limits.MaxRetainedProfiles, "Requested cohort exceeds profile bounds or is empty");
        foreach (var (id, name) in wanted)
            RetailNpcDatasetValidation.Require(id > 0 && !string.IsNullOrEmpty(name), "Required NPC identity is invalid");
        RetailNpcDatasetValidation.Require(wanted.Values.Distinct(StringComparer.Ordinal).Count() == wanted.Count, "Required NPC devnames must be unique");
        string directory = Path.GetFullPath(request.Directory);
        RetailNpcDatasetValidation.Require((File.GetAttributes(directory) & FileAttributes.ReparsePoint) == 0, "Dataset directory cannot be a reparse point");
        var streams = new Dictionary<string, FileStream>(StringComparer.Ordinal);
        var documents = new List<JsonDocument>();
        try
        {
            FileStream Open(string name)
            {
                RetailNpcDatasetValidation.Require(name == Path.GetFileName(name) && !name.Contains('/') && !name.Contains('\\'), "Artifact must be a dataset-relative leaf filename");
                string path = Path.Combine(directory, name);
                RetailNpcDatasetValidation.Require((File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0, "Artifact cannot be a reparse point");
                var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
                streams.Add(name, stream);
                return stream;
            }
            var manifestStream = Open("output-manifest.json");
            RetailNpcDatasetValidation.Require(manifestStream.Length <= limits.MaxMetadataFileBytes, "Output manifest exceeds metadata bounds");
            await CheckHash(manifestStream, request.ExpectedOutputManifestSha256, cancellationToken).ConfigureAwait(false);
            async Task<JsonElement> Metadata(string name)
            {
                var stream = streams[name];
                RetailNpcDatasetValidation.Require(stream.Length <= limits.MaxMetadataFileBytes, name + " exceeds metadata bounds");
                stream.Position = 0;
                var bytes = new byte[checked((int)stream.Length)];
                await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
                var doc = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = limits.MaxJsonDepth });
                documents.Add(doc);
                RetailNpcDatasetValidation.UniqueJson(doc.RootElement);
                return doc.RootElement;
            }
            var manifest = await Metadata("output-manifest.json").ConfigureAwait(false);
            RetailNpcDatasetValidation.Require(manifest.ValueKind == JsonValueKind.Array, "Output manifest must be an array");
            var receipts = new Dictionary<string, RetailNpcArtifactReceipt>(StringComparer.Ordinal);
            long wholeBytes = manifestStream.Length;
            foreach (var row in manifest.EnumerateArray())
            {
                string name = RetailNpcDatasetValidation.String(row, "file");
                RetailNpcDatasetValidation.Require(ArtifactNames.Contains(name, StringComparer.Ordinal), "Unexpected artifact path: " + name);
                var receipt = new RetailNpcArtifactReceipt(name, row.GetProperty("bytes").GetInt64(), RetailNpcDatasetValidation.String(row, "sha256"));
                RetailNpcDatasetValidation.Hash(receipt.Sha256);
                RetailNpcDatasetValidation.Require(receipt.Bytes >= 0 && receipts.TryAdd(name, receipt), "Negative length or duplicate artifact: " + name);
                wholeBytes = checked(wholeBytes + receipt.Bytes);
                RetailNpcDatasetValidation.Require(wholeBytes <= limits.MaxWholeInputBytes, "Dataset exceeds whole-input byte limit");
            }
            RetailNpcDatasetValidation.Require(receipts.Count == ArtifactNames.Length, "Output manifest is missing a required artifact");
            foreach (string name in ArtifactNames)
            {
                var stream = Open(name);
                RetailNpcDatasetValidation.Require(stream.Length == receipts[name].Bytes, "Artifact size mismatch: " + name);
                await CheckHash(stream, receipts[name].Sha256, cancellationToken).ConfigureAwait(false);
            }

            var summary = await Metadata("summary.json").ConfigureAwait(false);
            var provenance = await Metadata("provenance.json").ConfigureAwait(false);
            var sources = await Metadata("source-manifest.json").ConfigureAwait(false);
            var validation = await Metadata("validation.json").ConfigureAwait(false);
            var semantic = await Metadata("semantic-diff.json").ConfigureAwait(false);
            foreach (var element in new[] { summary, provenance })
            {
                RetailNpcDatasetValidation.Require(element.GetProperty("schema_version").GetInt32() == 1, "Incompatible dataset schema");
                RetailNpcDatasetValidation.Require(RetailNpcDatasetValidation.String(element, "dataset_id") == request.ExpectedDatasetId, "Dataset identity mismatch");
                RetailNpcDatasetValidation.False(element, "activation_ready");
            }
            RetailNpcDatasetValidation.False(summary, "fixture_verified");
            RetailNpcDatasetValidation.ValidateExtractionFlags(validation);
            var sourceReceipts = RetailNpcDatasetValidation.SourceReceipts(sources);
            var selectedSources = provenance.GetProperty("selection").EnumerateArray()
                .Select(row => RetailNpcDatasetValidation.String(row, "selected")).ToHashSet(StringComparer.Ordinal);
            RetailNpcDatasetValidation.Require(selectedSources.Count > 0 && selectedSources.All(sourceReceipts.ContainsKey), "Selected source provenance is incomplete");
            int declaredReviewed = summary.TryGetProperty("reviewed_same_id_reference_drift_bindings", out var reviewedCount) ? reviewedCount.GetInt32() : 0;
            RetailNpcDatasetValidation.Require(declaredReviewed is 0 or 90, "Only the complete reviewed 90-ID extension is supported");
            if (declaredReviewed != 0)
            {
                RetailNpcDatasetValidation.Require(request.ReviewedIdentityBindings is not null, "Changed-name dataset requires an explicit admitted identity review");
                request.ReviewedIdentityBindings.ValidateSourceContext(sources, provenance);
            }
            var differences = new Dictionary<int, string>();
            foreach (var difference in semantic.GetProperty("differences").EnumerateArray())
                RetailNpcDatasetValidation.Require(differences.TryAdd(difference.GetProperty("npc_id").GetInt32(), RetailNpcDatasetValidation.String(difference, "status")), "Duplicate semantic difference ID");
            RetailNpcDatasetValidation.Count(summary, "semantic_differences", differences.Count);
            RetailNpcDatasetValidation.Require(semantic.GetProperty("compared_selected_records").GetInt32() == summary.GetProperty("selected_retail_records").GetInt32()
                && semantic.GetProperty("aggregate_records").GetInt32() == summary.GetProperty("aggregate_records").GetInt32(), "Semantic crosscheck counts disagree");

            var coverage = RetailNpcDatasetValidation.Coverage(streams["coverage.csv"], limits, cancellationToken);
            RetailNpcDatasetValidation.Count(summary, "target_count", coverage.Count);
            foreach (var (id, name) in wanted)
                RetailNpcDatasetValidation.Require(coverage.TryGetValue(id, out var row) && row.Exported && row.Devnames48 == name
                    && (row.RetailDevname == name || request.ReviewedIdentityBindings?.Covers(id, name, row.RetailDevname) == true),
                    "Required NPC is missing, identity-blocked or incompatible: " + id);
            RetailNpcDatasetValidation.IdentityBlockers(streams["identity-blockers.csv"], coverage, summary, limits, cancellationToken);

            var pilotHashes = new Dictionary<int, string>();
            await foreach (var line in Lines(streams["pilot-profiles.jsonl"], limits.MaxProfileLineBytes, cancellationToken))
            {
                using var doc = JsonDocument.Parse(line, new JsonDocumentOptions { MaxDepth = limits.MaxJsonDepth });
                RetailNpcDatasetValidation.UniqueJson(doc.RootElement);
                int id = doc.RootElement.GetProperty("npc_id").GetInt32();
                RetailNpcDatasetValidation.Require(pilotHashes.Count < limits.MaxCoverageRows && pilotHashes.TryAdd(id, Convert.ToHexStringLower(SHA256.HashData(line))), "Duplicate pilot ID or pilot bound exceeded");
            }
            RetailNpcDatasetValidation.Count(summary, "pilot_profiles", pilotHashes.Count);
            var seenPilots = new HashSet<int>();
            var profileIds = new HashSet<int>();
            var skillIssues = new Dictionary<RetailNpcDatasetValidation.SkillIssue, int>();
            var retained = new Dictionary<int, RetailNpcSourceProfile>();
            var reviewedIds = new HashSet<int>();
            long retainedBytes = 0;
            int previousId = 0;
            await foreach (var line in Lines(streams["profiles.jsonl"], limits.MaxProfileLineBytes, cancellationToken))
            {
                using var doc = JsonDocument.Parse(line, new JsonDocumentOptions { MaxDepth = limits.MaxJsonDepth });
                var profile = doc.RootElement;
                int id = RetailNpcDatasetValidation.Profile(profile, request.ExpectedDatasetId, coverage, selectedSources, sourceReceipts, differences, skillIssues,
                    request.ReviewedIdentityBindings, reviewedIds, out var identityReceipt);
                RetailNpcDatasetValidation.Require(id > previousId && profileIds.Add(id), "Profile IDs must be unique and increasing");
                previousId = id;
                if (pilotHashes.TryGetValue(id, out var pilotHash))
                {
                    RetailNpcDatasetValidation.Require(Convert.ToHexStringLower(SHA256.HashData(line)) == pilotHash, "Pilot row differs from complete profile: " + id);
                    seenPilots.Add(id);
                }
                if (!wanted.ContainsKey(id)) continue;
                var incompatibleSkills = profile.GetProperty("skill_bindings").EnumerateArray()
                    .Where(x => RetailNpcDatasetValidation.String(x, "status") != "dual_join_agrees_static_only")
                    .Select(x => RetailNpcDatasetValidation.String(x, "status")).Distinct(StringComparer.Ordinal).ToArray();
                RetailNpcDatasetValidation.Require(incompatibleSkills.Length == 0,
                    "Required NPC has unresolved skill identity blockers: " + id + " [" + string.Join(",", incompatibleSkills) + "]");
                retainedBytes = checked(retainedBytes + line.Length);
                RetailNpcDatasetValidation.Require(retainedBytes <= limits.MaxRetainedProfileBytes, "Retained cohort exceeds byte limit");
                retained.Add(id, new RetailNpcSourceProfile(profile, identityReceipt));
            }
            RetailNpcDatasetValidation.Count(summary, "exported_profiles", profileIds.Count);
            RetailNpcDatasetValidation.Require(reviewedIds.Count == declaredReviewed, "Reviewed identity summary differs from the complete profile stream");
            if (declaredReviewed != 0) request.ReviewedIdentityBindings!.RequireComplete(reviewedIds);
            RetailNpcDatasetValidation.Require(profileIds.SetEquals(coverage.Values.Where(x => x.Exported).Select(x => x.Id)), "Profile IDs do not exactly cover exported identities");
            RetailNpcDatasetValidation.Require(seenPilots.SetEquals(pilotHashes.Keys) && retained.Count == wanted.Count, "Pilot or required cohort is incomplete");
            RetailNpcDatasetValidation.SkillIssues(streams["unsupported-skill-bindings.csv"], skillIssues, summary, limits, cancellationToken);

            // Hold all handles through validation; final hashes also detect mutation on
            // platforms where FileShare.Read does not provide Windows sharing semantics.
            await CheckHash(manifestStream, request.ExpectedOutputManifestSha256, cancellationToken).ConfigureAwait(false);
            foreach (var (name, receipt) in receipts)
            {
                RetailNpcDatasetValidation.Require(streams[name].Length == receipt.Bytes, "Artifact changed during admission: " + name);
                await CheckHash(streams[name], receipt.Sha256, cancellationToken).ConfigureAwait(false);
            }
            return new(request.ExpectedDatasetId, request.ExpectedOutputManifestSha256, retained, coverage.Count,
                coverage.Values.Count(x => !x.Exported), retainedBytes, receipts.Values.OrderBy(x => x.File, StringComparer.Ordinal), provenance, sources, reviewedIds.Count);
        }
        catch (Exception error) when (error is JsonException or FormatException or OverflowException or KeyNotFoundException or InvalidOperationException)
        {
            throw new InvalidDataException("Retail NPC static dataset admission failed: " + error.Message, error);
        }
        finally
        {
            foreach (var document in documents) document.Dispose();
            foreach (var stream in streams.Values) await stream.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async Task CheckHash(FileStream stream, string expected, CancellationToken cancellationToken)
    {
        stream.Position = 0;
        string actual = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
        RetailNpcDatasetValidation.Require(actual == expected, "Artifact SHA256 mismatch: " + Path.GetFileName(stream.Name));
        stream.Position = 0;
    }

    private static async IAsyncEnumerable<byte[]> Lines(FileStream stream, int limit, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        stream.Position = 0;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        using var row = new MemoryStream();
        try
        {
            int read;
            while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) != 0)
            {
                int start = 0;
                for (int index = 0; index < read; index++)
                {
                    if (buffer[index] != (byte)'\n') continue;
                    RetailNpcDatasetValidation.Require(row.Length + index - start <= limit, "Profile JSONL line exceeds byte limit");
                    row.Write(buffer, start, index - start);
                    RetailNpcDatasetValidation.Require(row.Length > 0, "Empty profile JSONL row");
                    yield return row.ToArray();
                    row.SetLength(0);
                    row.Position = 0;
                    start = index + 1;
                }
                RetailNpcDatasetValidation.Require(row.Length + read - start <= limit, "Profile JSONL line exceeds byte limit");
                row.Write(buffer, start, read - start);
            }
            if (row.Length > 0) yield return row.ToArray();
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }
}
