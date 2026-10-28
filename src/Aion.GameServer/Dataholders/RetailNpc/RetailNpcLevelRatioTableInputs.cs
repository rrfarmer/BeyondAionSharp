using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Aion.GameServer.Dataholders.RetailNpc;

/// <summary>Caller-pinned static context. Evidence paths are never opened by this unit.</summary>
public sealed record RetailNpcLevelRatioTablePolicy(string ExpectedDatasetId,
    string ExpectedOutputManifestSha256, string ExpectedRegion, string ExpectedSourceSha256,
    string SourceEvidencePath, string SelectedRelativePath, string SelectionReason);

public sealed record RetailNpcLevelRatioTableReceipt(string DatasetId, string OutputManifestSha256,
    string Region, string SourceSha256, int SourceBytes, string SourceEvidencePath,
    string SelectedRelativePath, string SelectionReason, string MainBinarySha256);

public sealed record RetailNpcLevelRatioRow(int Level, ushort Ratio, string LevelLexeme,
    string RatioLexeme, int SourceLine);

public sealed class RetailNpcLevelRatioTableAdmissionResult
{
    internal RetailNpcLevelRatioTableAdmissionResult(RetailNpcLevelRatioTableInputs? table,
        ImmutableArray<RetailNpcProjectionIssue> issues)
    { Table = table; Issues = issues; }
    public RetailNpcLevelRatioTableInputs? Table { get; }
    public ImmutableArray<RetailNpcProjectionIssue> Issues { get; }
    public bool Success => Table is not null && Issues.IsEmpty;
}

/// <summary>
/// Complete immutable static table, not a receipt of a loaded server table. Main 0x140dd8fe0
/// initializes 101 u16 entries to 1000 then writes ordered levels 1..100. Its partial failure state
/// is deliberately inadmissible. Country/selection is explicit caller evidence, never inferred
/// from files or the currently running server. No file/actor access.
/// </summary>
public sealed class RetailNpcLevelRatioTableInputs
{
    public const int MaximumSourceBytes = 1_048_576;
    private static readonly Regex Integer = new(@"^[+-]?[0-9]+$", RegexOptions.CultureInvariant);
    private static readonly ImmutableArray<string> Regions = ["USA", "Europe", "Asian", "Japan", "China", "Taiwan", "Russia", "L10N", "base"];

    private RetailNpcLevelRatioTableInputs(RetailNpcLevelRatioTableReceipt receipt,
        ImmutableArray<RetailNpcLevelRatioRow> rows, ImmutableArray<byte> bytes)
    { Receipt = receipt; Rows = rows; SourceBytes = bytes; }

    public RetailNpcLevelRatioTableReceipt Receipt { get; }
    public ImmutableArray<RetailNpcLevelRatioRow> Rows { get; }
    public ImmutableArray<byte> SourceBytes { get; }
    public ushort EntryZero => 1000;
    public bool StaticSourceAdmitted => true;
    public bool LoadedRuntimeTableVerified => false;
    public bool ActivationReady => false;

    public ushort ForTemplateLevel(int level)
    {
        if (level is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(level), "A template default needs level 0..100");
        return level == 0 ? EntryZero : Rows[level - 1].Ratio;
    }

    public static RetailNpcLevelRatioTableAdmissionResult Admit(ReadOnlySpan<byte> source,
        RetailNpcLevelRatioTablePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        try
        {
            RetailNpcDatasetValidation.Hash(policy.ExpectedDatasetId);
            RetailNpcDatasetValidation.Hash(policy.ExpectedOutputManifestSha256);
            RetailNpcDatasetValidation.Hash(policy.ExpectedSourceSha256);
            Need(Regions.Contains(policy.ExpectedRegion, StringComparer.Ordinal), "table_region", "Explicit supported region is required");
            Need(!string.IsNullOrWhiteSpace(policy.SourceEvidencePath) && !string.IsNullOrWhiteSpace(policy.SelectionReason),
                "table_receipt", "Source evidence path and selection reason are required");
            Need(!string.IsNullOrWhiteSpace(policy.SelectedRelativePath), "table_selection", "Explicit dataset-relative table selection is required");
            string selected = policy.SelectedRelativePath.Replace('\\', '/');
            Need(selected == "pc_stat_ratio.xml" || (policy.ExpectedRegion != "base"
                && selected == policy.ExpectedRegion + "/pc_stat_ratio.xml"), "table_selection", "Unexpected dataset-relative table selection");
            Need(source.Length is > 0 and <= MaximumSourceBytes, "table_size", "Table bytes exceed the admitted bound");
            byte[] bytes = source.ToArray();
            string hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
            Need(hash == policy.ExpectedSourceSha256, "table_hash", "Complete source hash differs from the caller pin");

            // The supplied XML has unused legacy entity declarations (including redefinitions
            // of predefined entities). Skip the DTD, retain its exact source bytes, and do not
            // resolve custom/external entity references. Such references in values are outside
            // this strict domain. Check depth before materializing the bounded document.
            using (var reader = Reader(bytes))
                while (reader.Read()) Need(reader.Depth <= 8, "table_depth", "Table XML depth exceeds 8");
            XDocument document;
            using (var reader = Reader(bytes)) document = XDocument.Load(reader, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
            var root = document.Root;
            Need(root is not null && root.Name == "pc_stat_ratios" && !root.HasAttributes,
                "table_root", "Expected unnamespaced pc_stat_ratios root without attributes");
            // The explicit guard makes the flow and atomic publication boundary visible.
            if (root is null) throw new InvalidDataException("Missing table root");
            Need(OnlyWhitespaceOutsideElements(root), "table_shape", "Unexpected root text");
            var levels = root.Elements().ToArray();
            Need(levels.Length == 100, "table_count", "All 100 ordered levels are required");
            var rows = ImmutableArray.CreateBuilder<RetailNpcLevelRatioRow>(100);
            for (int i = 0; i < levels.Length; i++)
            {
                var level = levels[i];
                var attributes = level.Attributes().ToArray();
                Need(level.Name == "level" && attributes.Length == 1 && attributes[0].Name == "level",
                    "table_level_shape", "Each level needs exactly one level attribute");
                string levelLexeme = attributes[0].Value;
                int number = Decimal(levelLexeme);
                Need(number == i + 1, "table_level_order", "Levels must appear exactly once in order 1..100");
                Need(OnlyWhitespaceOutsideElements(level), "table_shape", "Unexpected level text");
                var children = level.Elements().ToArray();
                Need(children.Length == 1 && children[0].Name == "stat_ratio" && !children[0].HasAttributes
                    && !children[0].HasElements && children[0].Nodes().All(x => x is XText),
                    "table_ratio_shape", "One scalar stat_ratio child is required");
                string ratioLexeme = children[0].Value;
                int ratio = Decimal(ratioLexeme);
                Need(ratio is >= 1000 and <= 65000, "table_ratio_range", "Table ratio must be 1000..65000");
                var line = (IXmlLineInfo)children[0];
                rows.Add(new(number, (ushort)ratio, levelLexeme, ratioLexeme, line.HasLineInfo() ? line.LineNumber : 0));
            }
            var receipt = new RetailNpcLevelRatioTableReceipt(policy.ExpectedDatasetId, policy.ExpectedOutputManifestSha256,
                policy.ExpectedRegion, hash, bytes.Length, policy.SourceEvidencePath, selected, policy.SelectionReason,
                RetailNpcTemplateProjection.MainBinarySha256);
            return new(new(receipt, rows.MoveToImmutable(), bytes.ToImmutableArray()), []);
        }
        catch (TableError error) { return new(null, [new(error.Code, "/pc_stat_ratios", error.Message)]); }
        catch (Exception error) when (error is XmlException or InvalidDataException or FormatException
            or OverflowException or ArgumentException or InvalidOperationException)
        { return new(null, [new("table_contract", "/pc_stat_ratios", error.Message)]); }
    }

    private static XmlReader Reader(byte[] bytes) => XmlReader.Create(new MemoryStream(bytes, writable: false), new XmlReaderSettings
    {
        DtdProcessing = DtdProcessing.Ignore, XmlResolver = null, CheckCharacters = true,
        MaxCharactersInDocument = MaximumSourceBytes, MaxCharactersFromEntities = 1024,
        CloseInput = true, ConformanceLevel = ConformanceLevel.Document
    });

    private static bool OnlyWhitespaceOutsideElements(XElement element) => element.Nodes().All(x => x is XElement
        or XComment || x is XText text && string.IsNullOrWhiteSpace(text.Value));

    private static int Decimal(string value)
    {
        string trimmed = value.Trim();
        Need(Integer.IsMatch(trimmed) && int.TryParse(trimmed, NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out _), "table_integer", "Strict signed32 decimal lexeme required");
        return int.Parse(trimmed, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
    }

    private static void Need(bool value, string code, string message)
    { if (!value) throw new TableError(code, message); }
    private sealed class TableError(string code, string message) : Exception(message)
    { internal string Code { get; } = code; }
}
