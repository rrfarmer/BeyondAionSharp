using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Aion.GameServer.Dataholders.RetailNpc;

public enum RetailNpcDifficultyInputOrigin { Unspecified, FixedMathematicalFixture, ExternalSourceAssertion }
public enum RetailNpcDifficultySelection { Unspecified, Base, RegionalOverride, RegionalMissingBaseFallback }
public enum RetailNpcDifficultyFloatContract { Unspecified, Binary32NearestEvenMaskedInvalid }

/// <summary>All paths are bounded evidence strings; this unit never opens a path.</summary>
public sealed record RetailNpcDifficultyTablePolicy(string ExpectedDatasetId, string ExpectedOutputManifestSha256,
    string ExpectedRegion, string ExpectedPackageContextSha256, string ExpectedScalingSha256, string ExpectedWorldIdsSha256,
    string ScalingEvidencePath, string WorldIdsEvidencePath, string SelectedScalingRelativePath,
    string SelectedWorldIdsRelativePath, RetailNpcDifficultySelection ScalingSelection,
    RetailNpcDifficultySelection WorldIdsSelection, string? Flavor, RetailNpcDifficultyInputOrigin Origin);

public sealed record RetailNpcDifficultyTableReceipt(string DatasetId, string OutputManifestSha256, string Region,
    string PackageContextSha256, string ScalingSha256, int ScalingBytes, string WorldIdsSha256, int WorldIdsBytes,
    string ScalingEvidencePath, string WorldIdsEvidencePath, string SelectedScalingRelativePath,
    string SelectedWorldIdsRelativePath, RetailNpcDifficultySelection ScalingSelection,
    RetailNpcDifficultySelection WorldIdsSelection, string? Flavor, RetailNpcDifficultyInputOrigin Origin,
    string MainSha256, string NpcSha256);

public sealed record RetailNpcDifficultySourceField(string Name, RetailNpcSourcePresence Presence,
    string? Lexeme, int Value, int SourceLine)
{
    public bool NativeRecordZeroDefaultApplied => Presence == RetailNpcSourcePresence.Absent;
    public bool LoadedRuntimeValueVerified => false;
}

/// <summary>Normalized XML is named explicitly; immutable complete file bytes retain original spelling/encoding.</summary>
public sealed class RetailNpcDifficultyRecordInputs
{
    internal RetailNpcDifficultyRecordInputs(int id, int worldId, string worldName, int ordinal, int line,
        string xml, string worldXml, IDictionary<string, RetailNpcDifficultySourceField> fields)
    {
        Id = id; WorldId = worldId; WorldName = worldName; Ordinal = ordinal; SourceLine = line;
        NormalizedXml = xml; NormalizedXmlSha256 = RetailNpcDifficultyTableInputs.Digest(Encoding.UTF8.GetBytes(xml));
        WorldBindingNormalizedXml = worldXml;
        Fields = fields.ToFrozenDictionary(StringComparer.Ordinal);
    }
    public int Id { get; }
    public int WorldId { get; }
    public string WorldName { get; }
    public int Ordinal { get; }
    public int SourceLine { get; }
    public string NormalizedXml { get; }
    public string NormalizedXmlSha256 { get; }
    public string WorldBindingNormalizedXml { get; }
    public FrozenDictionary<string, RetailNpcDifficultySourceField> Fields { get; }
    public RetailNpcDifficultySourceField StatScalingRatio => Fields["stat_scaling_ratio"];
    public bool LoadedRuntimeRecordVerified => false;
}

public sealed class RetailNpcDifficultyTableAdmissionResult
{
    internal RetailNpcDifficultyTableAdmissionResult(RetailNpcDifficultyTableInputs? table, ImmutableArray<RetailNpcProjectionIssue> issues)
    { Table = table; Issues = issues; }
    public RetailNpcDifficultyTableInputs? Table { get; }
    public ImmutableArray<RetailNpcProjectionIssue> Issues { get; }
    public bool Success => Table is not null && Issues.IsEmpty;
}

/// <summary>
/// Strict whole-source admission for the pinned WorldDb table consumer, not a loaded table.
/// Earlier native inserts may survive native failure; this pure source unit publishes nothing on any failure.
/// Java NpcData/SummonGameStats were read first. Approved evidence: owned-scaled-construction.
/// </summary>
public sealed class RetailNpcDifficultyTableInputs
{
    public const int MaximumScalingBytes = 1_048_576;
    public const int MaximumWorldIdsBytes = 2_097_152;
    public const int MaximumRows = 4096;
    internal static ImmutableArray<string> NumericFields { get; } = ["id", "up_limit_level", "down_limit_level",
        "exp_scaling_ratio", "ap_scaling_ratio", "gp_scaling_ratio", "stat_scaling_ratio", "skill_scaling"];
    private static readonly ImmutableArray<string> Regions = ["base", "USA", "Europe", "Asian", "Japan", "China", "Taiwan", "Russia", "L10N"];
    private static readonly Regex Integer = new(@"^[+-]?[0-9]+$", RegexOptions.CultureInvariant);
    private RetailNpcDifficultyTableInputs(RetailNpcDifficultyTableReceipt receipt, IEnumerable<RetailNpcDifficultyRecordInputs> rows,
        byte[] scaling, byte[] world)
    { Receipt = receipt; Records = rows.ToImmutableArray(); ScalingSourceBytes = scaling.ToImmutableArray(); WorldIdsSourceBytes = world.ToImmutableArray(); }
    public RetailNpcDifficultyTableReceipt Receipt { get; }
    public ImmutableArray<RetailNpcDifficultyRecordInputs> Records { get; }
    public ImmutableArray<byte> ScalingSourceBytes { get; }
    public ImmutableArray<byte> WorldIdsSourceBytes { get; }
    public bool StaticSourceAdmitted => true;
    public bool LoadedRuntimeTableVerified => false;
    public bool ActivationReady => false;

    public static RetailNpcDifficultyTableAdmissionResult Admit(ReadOnlySpan<byte> scalingSource,
        ReadOnlySpan<byte> worldIdsSource, RetailNpcDifficultyTablePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        try
        {
            foreach (string hash in new[] { policy.ExpectedDatasetId, policy.ExpectedOutputManifestSha256,
                policy.ExpectedPackageContextSha256, policy.ExpectedScalingSha256, policy.ExpectedWorldIdsSha256 }) RetailNpcDatasetValidation.Hash(hash);
            Need(Regions.Contains(policy.ExpectedRegion, StringComparer.Ordinal), "difficulty_region", "/policy", "Explicit supported region required");
            Need(policy.Origin is RetailNpcDifficultyInputOrigin.FixedMathematicalFixture or RetailNpcDifficultyInputOrigin.ExternalSourceAssertion,
                "difficulty_origin", "/policy", "Explicit source origin required");
            Need(Text(policy.ScalingEvidencePath, 4096) && Text(policy.WorldIdsEvidencePath, 4096), "difficulty_evidence", "/policy", "Bounded evidence paths required");
            Need(policy.Flavor is null || Regex.IsMatch(policy.Flavor, @"^[A-Za-z0-9_-]{1,64}$", RegexOptions.CultureInvariant),
                "difficulty_flavor", "/policy", "Flavor is null or a bounded native path component");
            string scalingName = policy.Flavor is null ? "instance_scaling.xml" : policy.Flavor + "/instance_scaling_" + policy.Flavor + ".xml";
            string selectedScaling = Selected(policy.SelectedScalingRelativePath, scalingName, policy.ExpectedRegion, policy.ScalingSelection);
            string selectedWorld = Selected(policy.SelectedWorldIdsRelativePath, "ID/WorldId.xml", policy.ExpectedRegion, policy.WorldIdsSelection);
            Need(scalingSource.Length is > 0 and <= MaximumScalingBytes && worldIdsSource.Length is > 0 and <= MaximumWorldIdsBytes,
                "difficulty_size", "/source", "Whole input bytes exceed admitted bounds");
            byte[] scaling = scalingSource.ToArray(), world = worldIdsSource.ToArray();
            Need(Digest(scaling) == policy.ExpectedScalingSha256 && Digest(world) == policy.ExpectedWorldIdsSha256,
                "difficulty_hash", "/source", "Complete scaling/world source differs from caller pins");
            var worldRoot = Document(world, MaximumWorldIdsBytes).Root;
            Need(worldRoot is not null && worldRoot.Name == "world_id" && !worldRoot.HasAttributes,
                "difficulty_world_root", "/world_id", "Canonical world_id root required");
            Shape(worldRoot, "/world_id");
            var worldRows = worldRoot.Elements().ToArray();
            Need(worldRows.Length is > 0 and <= MaximumRows, "difficulty_world_count", "/world_id", "Bounded complete world table required");
            var names = new Dictionary<string, (int Id, string Xml)>(StringComparer.Ordinal);
            var insensitive = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var ids = new HashSet<int>();
            foreach (var row in worldRows)
            {
                Need(row.Name == "data" && !row.HasElements && row.Nodes().All(x => x is XText),
                    "difficulty_world_shape", "/world_id/data", "Canonical scalar world binding required");
                var attribute = row.Attribute("id");
                Need(attribute is not null, "difficulty_world_id", "/world_id/data", "Exactly one ID attribute required");
                int id = Decimal(attribute.Value); string name = row.Value.Trim();
                Need(id > 0 && Text(name, 1024) && ids.Add(id) && insensitive.Add(name),
                    "difficulty_world_duplicate", "/world_id/data", "Unique positive world IDs and names required");
                names.Add(name, (id, row.ToString(SaveOptions.DisableFormatting)));
            }
            var root = Document(scaling, MaximumScalingBytes).Root;
            Need(root is not null && root.Name == "instance_scalings" && !root.HasAttributes,
                "difficulty_root", "/instance_scalings", "Canonical instance_scalings root required");
            Shape(root, "/instance_scalings");
            var entries = root.Elements().ToArray();
            Need(entries.Length is > 0 and <= MaximumRows, "difficulty_count", "/instance_scalings", "Bounded complete table required");
            var records = new List<RetailNpcDifficultyRecordInputs>(); var recordIds = new HashSet<int>(); var worldKeys = new HashSet<int>();
            for (int i = 0; i < entries.Length; i++)
            {
                var row = entries[i]; string path = "/instance_scalings/instance_scaling[" + i + "]";
                Need(row.Name == "instance_scaling" && !row.HasAttributes, "difficulty_record_shape", path, "Canonical scaling record required");
                Shape(row, path);
                var children = row.Elements().ToArray();
                foreach (string name in NumericFields.Concat(["name", "instant_dungeon_name"]))
                {
                    var same = children.Where(x => string.Equals(x.Name.LocalName, name, StringComparison.OrdinalIgnoreCase)).ToArray();
                    Need(same.Length <= 1 && (same.Length == 0 || same[0].Name == name), "difficulty_alias_or_duplicate", path + "/" + name,
                        "Repeated/case/namespace semantic aliases cannot be first-selected");
                }
                string worldName = RequiredLeaf(children, "instant_dungeon_name", path).Trim();
                Need(names.TryGetValue(worldName, out var binding), "difficulty_world_binding", path, "Every world name must resolve in the complete pinned world table");
                Need(binding.Id is >= 300_000_000 and < 400_000_000, "difficulty_dungeon", path, "Native table supports dungeon-range world IDs only");
                var fields = new Dictionary<string, RetailNpcDifficultySourceField>(StringComparer.Ordinal);
                foreach (string name in NumericFields)
                {
                    var element = children.SingleOrDefault(x => x.Name == name);
                    string? lexeme = element is null ? null : Leaf(element, path + "/" + name);
                    int value = lexeme is null ? 0 : Decimal(lexeme); // Byte-proved native zeroed record, then ordered parser writes.
                    fields.Add(name, new(name, lexeme is null ? RetailNpcSourcePresence.Absent : value == 0 ? RetailNpcSourcePresence.ExplicitZero
                        : RetailNpcSourcePresence.ExplicitValue, lexeme, value, element is IXmlLineInfo line && line.HasLineInfo() ? line.LineNumber : 0));
                }
                Need(fields["id"].Presence != RetailNpcSourcePresence.Absent && fields["id"].Value > 0
                    && recordIds.Add(fields["id"].Value) && worldKeys.Add(binding.Id),
                    "difficulty_duplicate", path, "Unique positive record IDs and world keys required; no partial table publication");
                var sourceLine = (IXmlLineInfo)row;
                records.Add(new(fields["id"].Value, binding.Id, worldName, i, sourceLine.HasLineInfo() ? sourceLine.LineNumber : 0,
                    row.ToString(SaveOptions.DisableFormatting), binding.Xml, fields));
            }
            var receipt = new RetailNpcDifficultyTableReceipt(policy.ExpectedDatasetId, policy.ExpectedOutputManifestSha256, policy.ExpectedRegion,
                policy.ExpectedPackageContextSha256, policy.ExpectedScalingSha256, scaling.Length, policy.ExpectedWorldIdsSha256, world.Length,
                policy.ScalingEvidencePath, policy.WorldIdsEvidencePath, selectedScaling, selectedWorld, policy.ScalingSelection, policy.WorldIdsSelection,
                policy.Flavor, policy.Origin, RetailNpcTemplateProjection.MainBinarySha256, RetailNpcTemplateProjection.NpcBinarySha256);
            return new(new(receipt, records, scaling, world), []);
        }
        catch (DifficultyInputError e) { return new(null, [new(e.Code, e.Path, e.Message)]); }
        catch (Exception e) when (e is XmlException or InvalidDataException or ArgumentException or InvalidOperationException or OverflowException)
        { return new(null, [new("difficulty_table_contract", "/source", e.Message)]); }
    }

    internal static string Digest(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    internal static bool Text(string? value, int maximum) => !string.IsNullOrWhiteSpace(value) && value.Length <= maximum;
    private static string Selected(string relative, string file, string region, RetailNpcDifficultySelection selection)
    {
        Need(Text(relative, 256), "difficulty_selection", "/policy", "Explicit relative selection required");
        string canonical = relative.Replace('\\', '/');
        string expected = selection switch
        {
            RetailNpcDifficultySelection.Base when region == "base" => file,
            RetailNpcDifficultySelection.RegionalOverride when region != "base" => region + "/" + file,
            RetailNpcDifficultySelection.RegionalMissingBaseFallback when region != "base" => file,
            _ => throw new DifficultyInputError("difficulty_selection", "/policy", "Region/selection combination is unresolved")
        };
        Need(canonical == expected, "difficulty_selection", "/policy", "Selected relative source differs from explicit region/flavor/fallback");
        return canonical;
    }
    private static XDocument Document(byte[] bytes, int maximum)
    {
        XmlReader Reader() => XmlReader.Create(new MemoryStream(bytes, false), new XmlReaderSettings
        { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null, CheckCharacters = true,
            MaxCharactersInDocument = maximum, MaxCharactersFromEntities = 1024, CloseInput = true });
        using (var reader = Reader()) while (reader.Read()) Need(reader.Depth <= 8, "difficulty_depth", "/source", "XML depth exceeds8");
        using var parse = Reader();
        return XDocument.Load(parse, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
    }
    private static string RequiredLeaf(XElement[] children, string name, string path)
    {
        var nodes = children.Where(x => x.Name == name).ToArray();
        Need(nodes.Length == 1, "difficulty_required", path + "/" + name, "One required scalar field expected");
        return Leaf(nodes[0], path + "/" + name);
    }
    private static string Leaf(XElement element, string path)
    {
        Need(!element.HasAttributes && !element.HasElements && element.Nodes().All(x => x is XText),
            "difficulty_leaf", path, "Canonical scalar text expected");
        return element.Value;
    }
    private static void Shape(XElement element, string path) => Need(element.Nodes().All(x => x is XElement or XComment
        || x is XText t && string.IsNullOrWhiteSpace(t.Value)), "difficulty_shape", path, "Unexpected text in record container");
    private static int Decimal(string lexeme)
    {
        string value = lexeme.Trim();
        Need(Integer.IsMatch(value) && int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _),
            "difficulty_integer", "/source", "Strict signed32 decimal required");
        return int.Parse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
    }
    private static void Need([DoesNotReturnIf(false)] bool value, string code, string path, string message)
    { if (!value) throw new DifficultyInputError(code, path, message); }
    private sealed class DifficultyInputError(string code, string path, string message) : Exception(message)
    { internal string Code { get; } = code; internal string Path { get; } = path; }
}

/// <summary>Explicit caller assertions, not verified observations. No null/default owner or floating-point inference.</summary>
public sealed record RetailNpcDifficultyContext(RetailNpcTemplateSourceReceipt ExpectedSource,
    RetailNpcLevelRatioTableReceipt ExpectedLevelRatioTable, RetailNpcDifficultyTableReceipt ExpectedDifficultyTable,
    string ExpectedPackageContextSha256, int ExpectedRecordId, string ExpectedRecordNormalizedXmlSha256,
    string Epoch, string? ActorObjectIdentity, string EvidenceReference, RetailNpcDifficultyInputOrigin Origin,
    bool? FreshUniqueTemplate, bool? FreshActor, int? NativeKind, uint? MasterObjectId, uint? SourceRelatedObjectId,
    bool? VirtualProfileRecordPresent, bool? DualWeaponFlagPresent, bool? DifficultyRecordPresent,
    bool? DynamicWorldResolved, uint? NativeDynamicWorldObjectId, int? NativeBaseWorldId,
    int? NativeCounter, int? NativeStatScalingRatio, RetailNpcDifficultyFloatContract? FloatContract);
