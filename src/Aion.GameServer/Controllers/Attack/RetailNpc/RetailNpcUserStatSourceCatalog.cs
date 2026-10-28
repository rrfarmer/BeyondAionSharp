using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using GI = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserGearStatLifeInputs;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Inactive, caller-pinned selected4.8 source admission. Definitions are not registrations.
/// All input parses/hashes complete before an immutable catalog is published. Raw records are
/// retained only on selection, after rechecking their internally bound source file.</summary>
internal static class RetailNpcUserStatSourceCatalog
{
    internal enum Authority { Java, CSharp }
    internal enum Origin { SelectedSourceDeclaration, SyntheticFixture }
    internal enum Family { Item, Random, Set, Enchant, Tempering, Instance, Pet, Title, Cp, Skill, CraftTree, SkillTree, Absolute }
    internal sealed record Counts(int Files, int Records, int Items, int Skills, int Functions);
    internal sealed record Request(string ManifestPath, string ManifestSha256, string DatasetId,
        string JavaRoot, string CSharpRoot, Origin Origin, Counts JavaExpected, Counts CSharpExpected,
        string SelectionReceipt, long MaxTotalBytes = 2_147_483_648L, int MaxFileBytes = 134_217_728,
        int MaxRecordBytes = 262_144, int MaxRecords = 500_000);
    internal sealed record Key(Authority Authority, Family Family, string Primary, string Secondary = "");
    internal sealed record Metadata(Key Key, int DefinitionOrdinal, int ImportOrdinal, string Relative,
        string FileSha256, long Start, int Length, string RawSha256, int FunctionRows, int Line,
        ImmutableArray<string> OrderedChildTags, ImmutableArray<string> ItemSetMembers);
    internal sealed record Coverage(Authority Authority, int Files, int Records, int Items, int Skills,
        int Functions, int RandomVariants, int EnchantLevels, int TemperingLevels,
        ImmutableDictionary<string, int> FunctionTags, ImmutableArray<Key> DuplicateKeys);
    private sealed record FileInput(Authority Authority, int Import, string DeclaredImport,
        string Relative, string Sha256, long Bytes, string Path, string Root);
    private sealed record Draft(ImmutableArray<FileInput> Files, ImmutableArray<Metadata> Rows, ImmutableArray<Coverage> Coverage);
    private sealed record Shape(Family Family, string Root, string Child, string Primary, string? Secondary = null);
    private static readonly ImmutableDictionary<string, Shape> Shapes = new Dictionary<string, Shape>(StringComparer.Ordinal)
    {
        ["items/item_templates.xml"] = new(Family.Item, "item_templates", "item_template", "id"),
        ["items/item_random_bonuses.xml"] = new(Family.Random, "random_bonuses", "random_bonus", "id", "type"),
        ["item_sets/item_sets.xml"] = new(Family.Set, "item_sets", "itemset", "id"),
        ["enchants/enchant_templates.xml"] = new(Family.Enchant, "enchant_templates", "enchant_list", "item_group"),
        ["enchants/tempering_templates.xml"] = new(Family.Tempering, "tempering_templates", "tempering_list", "item_group"),
        ["instance_bonusattr/instance_bonusattr.xml"] = new(Family.Instance, "instance_bonusattrs", "instance_bonusattr", "buff_id"),
        ["pets/pet_buffs.xml"] = new(Family.Pet, "pet_buffs", "buff", "id"),
        ["player_titles.xml"] = new(Family.Title, "player_titles", "title", "id"),
        ["conqueror_protector_ranks/conqueror_protector_ranks.xml"] = new(Family.Cp, "conqueror_protector_ranks", "rank", "rank_num", "type"),
        ["skills/skill_templates.xml"] = new(Family.Skill, "skill_data", "skill_template", "skill_id"),
        ["skill_tree/craft_skill_tree.xml"] = new(Family.CraftTree, "skill_tree", "skill", "#ordinal"),
        ["skill_tree/skill_tree.xml"] = new(Family.SkillTree, "skill_tree", "skill", "#ordinal"),
        ["stats/absolute_stats.xml"] = new(Family.Absolute, "absolute_stats", "stats_set", "id")
    }.ToImmutableDictionary(StringComparer.Ordinal);
    private static readonly UTF8Encoding Utf8 = new(false, true);
    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static bool Digest(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    internal static bool FunctionNode(XElement node) =>
        node.Parent?.Name.LocalName == "modifiers" && node.Name.LocalName is "add" or "sub" or "rate" or "set" or "abs" or "mean"
        || node.Name.LocalName is "change" or "enchant_stat" or "tempering_stat" or "penalty_attr";

    internal sealed class Catalog
    {
        private readonly ImmutableDictionary<Key, ImmutableArray<Metadata>> definitions;
        private readonly ImmutableDictionary<string, FileInput> sources;
        private readonly int maxFile, maxRecord;
        internal string DatasetId { get; }
        internal string ManifestSha256 { get; }
        internal Origin SourceOrigin { get; }
        internal string SelectionReceipt { get; }
        internal ImmutableArray<Metadata> AllDefinitions { get; }
        internal ImmutableArray<Coverage> Coverage { get; }
        internal bool ActivationReady => false;
        private Catalog(Request request, ImmutableArray<FileInput> files, ImmutableArray<Metadata> rows, ImmutableArray<Coverage> coverage)
        {
            DatasetId = request.DatasetId; ManifestSha256 = request.ManifestSha256; SourceOrigin = request.Origin;
            SelectionReceipt = request.SelectionReceipt; maxFile = request.MaxFileBytes; maxRecord = request.MaxRecordBytes;
            AllDefinitions = rows; Coverage = coverage;
            definitions = rows.GroupBy(r => r.Key).ToImmutableDictionary(g => g.Key, g => g.ToImmutableArray());
            sources = files.ToImmutableDictionary(f => FileKey(f.Authority, f.Relative), StringComparer.Ordinal);
        }
        // Construct inside the nested class; no public record/copy constructor is an admission path.
        internal static Catalog Admit(Request request)
        { Draft d = ReadWhole(request); return new Catalog(request, d.Files, d.Rows, d.Coverage); }
        internal ImmutableArray<Metadata> Definitions(Key key) => definitions.TryGetValue(key, out var rows) ? rows : [];
        internal Metadata Selected(Key key)
        {
            GI.Require(key is not null,"non-null catalog key");
            var rows = Definitions(key); GI.Require(!rows.IsEmpty, "missing catalog identity");
            // Map holders replace later definitions. CP deliberately scans first match.
            return key.Family == Family.Cp ? rows[0] : rows[^1];
        }
        internal Record Retain(Key key) => Record.Admit(this, key);
        internal Metadata? SetForItem(Authority authority, string itemId)
        {
            Metadata? selected=null;
            foreach(Metadata row in AllDefinitions.Where(x=>x.Key.Authority==authority && x.Key.Family==Family.Set)
                .OrderBy(x=>x.ImportOrdinal).ThenBy(x=>x.DefinitionOrdinal))
                if(row.ItemSetMembers.Contains(itemId))selected=row;
            return selected;
        }
        private byte[] ReadOwned(Metadata row)
        {
            GI.Require(sources.TryGetValue(FileKey(row.Key.Authority, row.Relative), out var file), "owned source identity");
            byte[] bytes = ReadFile(SafePath(Path.Combine(file.Root,"data/static_data"),file.Relative), file.Bytes, maxFile);
            GI.Require(Hash(bytes) == row.FileSha256 && row.Start >= 0 && row.Length > 0
                && row.Length <= maxRecord && row.Start + row.Length <= bytes.LongLength, "selected source changed/span bounds");
            byte[] raw = bytes.AsSpan(checked((int)row.Start), row.Length).ToArray();
            GI.Require(Hash(raw) == row.RawSha256, "selected raw record hash");
            return raw;
        }
        internal void ValidateRecord(Record row)
        {
            GI.Require(ReferenceEquals(row.Owner, this) && definitions.TryGetValue(row.Metadata.Key, out var items)
                && items.Contains(row.Metadata) && Hash(row.Raw.AsSpan()) == row.Metadata.RawSha256, "catalog-owned raw record");
        }
        internal sealed class Record
        {
            internal Catalog Owner { get; }
            internal Metadata Metadata { get; }
            internal ImmutableArray<byte> Raw { get; }
            internal string Identity => Owner.DatasetId + ":" + Metadata.Key.Authority + ":" + Metadata.Relative + "#" + Metadata.DefinitionOrdinal.ToString(CultureInfo.InvariantCulture);
            private Record(Catalog owner, Metadata metadata, byte[] raw)
            { Owner = owner; Metadata = metadata; Raw = [..raw]; }
            internal static Record Admit(Catalog owner, Key key)
            {
                Metadata row = owner.Selected(key);
                var record = new Record(owner, row, owner.ReadOwned(row));
                owner.ValidateRecord(record); return record;
            }
            internal XElement Read() { Owner.ValidateRecord(this); return ParseRecord(Raw.AsSpan()); }
        }
    }
    internal static Catalog Read(Request request)
    {
        try { return Catalog.Admit(request); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException or JsonException
            or DecoderFallbackException or OverflowException or ArgumentException or KeyNotFoundException or InvalidOperationException or FormatException)
        { throw new GI.Domain("catalog input: " + ex.GetType().Name); }
    }
    private static Draft ReadWhole(Request request)
    {
        GI.Require(request is not null && request.JavaExpected is not null && request.CSharpExpected is not null, "non-null catalog caller contract");
        GI.Require(Enum.IsDefined(request.Origin) && Digest(request.ManifestSha256)
            && request.DatasetId == "catalog:" + request.ManifestSha256 && !string.IsNullOrWhiteSpace(request.SelectionReceipt), "catalog caller pins/origin");
        GI.Require(request.MaxFileBytes is > 0 and <= 134_217_728 && request.MaxRecordBytes is > 0 and <= 262_144
            && request.MaxRecords is > 0 and <= 500_000 && request.MaxTotalBytes is > 0 and <= 2_147_483_648L, "catalog bounds");
        byte[] manifest = ReadFile(request.ManifestPath, new FileInfo(request.ManifestPath).Length, 2_097_152);
        GI.Require(Hash(manifest) == request.ManifestSha256, "manifest hash");
        using JsonDocument doc = JsonDocument.Parse(manifest, new JsonDocumentOptions { MaxDepth = 32 });
        Unique(doc.RootElement);
        Keys(doc.RootElement, "configs", "entries", "files", "selection");
        var selectionFiles=new List<(string Path,string Hash)> { (request.ManifestPath,request.ManifestSha256) };
        foreach (string section in new[] { "configs", "entries" })
        {
            Keys(doc.RootElement.GetProperty(section), "Java", "CSharp");
            foreach (string authority in new[] { "Java", "CSharp" })
                Keys(doc.RootElement.GetProperty(section).GetProperty(authority), "path", "sha256");
        }
        var inputs = ImmutableArray.CreateBuilder<FileInput>();
        foreach (JsonElement row in doc.RootElement.GetProperty("files").EnumerateArray())
        {
            Keys(row, "import_ordinal", "declared_import", "path", "relative", "country", "country_override", "directory_order",
                "authority", "sha256", "bytes", "full_xml_parse", "root_tag", "top_level_records");
            string a = row.GetProperty("authority").GetString() ?? "";
            GI.Require(a is "Java" or "CSharp", "source authority"); Authority authority = a == "Java" ? Authority.Java : Authority.CSharp;
            string relative = row.GetProperty("relative").GetString() ?? "";
            string root = authority == Authority.Java ? request.JavaRoot : request.CSharpRoot;
            string path = SafePath(Path.Combine(root, "data", "static_data"), relative);
            string digest = row.GetProperty("sha256").GetString() ?? "";
            GI.Require(Digest(digest) && row.GetProperty("country").GetInt32() == 99
                && row.GetProperty("country_override").ValueKind == JsonValueKind.False, "country99/source digest");
            inputs.Add(new(authority, row.GetProperty("import_ordinal").GetInt32(), row.GetProperty("declared_import").GetString() ?? "",
                relative, digest, row.GetProperty("bytes").GetInt64(), path, root));
        }
        GI.Require(inputs.Count > 0 && inputs.Count <= 4096 && inputs.Select(x => FileKey(x.Authority, x.Relative)).Distinct().Count() == inputs.Count, "selected file identities");
        long total = 0; foreach (FileInput f in inputs) { total = checked(total + f.Bytes); GI.Require(f.Bytes > 0 && total <= request.MaxTotalBytes, "whole source byte bound"); }
        foreach (Authority authority in Enum.GetValues<Authority>())
        {
            string name = authority == Authority.Java ? "Java" : "CSharp";
            string root = authority == Authority.Java ? request.JavaRoot : request.CSharpRoot;
            byte[] config = ReadFile(SafePath(root, "config/main/gameserver.properties"), null, 1_048_576);
            GI.Require(Hash(config) == doc.RootElement.GetProperty("configs").GetProperty(name).GetProperty("sha256").GetString(), "config identity");
            selectionFiles.Add((SafePath(root,"config/main/gameserver.properties"),Hash(config)));
            GI.Require(Regex.Matches(Utf8.GetString(config), @"(?m)^\s*gameserver\.country\.code\s*=\s*99\s*$").Count == 1, "selected config country99");
            string entryPath = SafePath(root, "data/static_data/static_data.xml"); byte[] entry = ReadFile(entryPath, null, 1_048_576);
            GI.Require(Hash(entry) == doc.RootElement.GetProperty("entries").GetProperty(name).GetProperty("sha256").GetString(), "import entry identity");
            selectionFiles.Add((entryPath,Hash(entry)));
            CheckImports(root, entry, inputs.Where(x => x.Authority == authority).ToArray());
        }
        var metadata = ImmutableArray.CreateBuilder<Metadata>(); var summaries = ImmutableArray.CreateBuilder<Coverage>();
        foreach (Authority authority in Enum.GetValues<Authority>())
        {
            FileInput[] files = inputs.Where(x => x.Authority == authority).OrderBy(x => x.Import).ThenBy(x => x.Relative, StringComparer.Ordinal).ToArray();
            int records = 0, items = 0, skills = 0, functions = 0, variants = 0, enchants = 0, tempers = 0;
            var tags = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (FileInput file in files)
            {
                byte[] bytes = ReadFile(file.Path, file.Bytes, request.MaxFileBytes);
                GI.Require(Hash(bytes) == file.Sha256, "source file hash"); Utf8.GetString(bytes);
                ValidateWholeXml(bytes, Shapes.ContainsKey(file.Relative));
                if (!Shapes.TryGetValue(file.Relative, out Shape? shape)) continue;
                using (XmlReader rootReader = Reader(bytes))
                { rootReader.MoveToContent(); GI.Require(rootReader.LocalName == shape.Root && rootReader.NamespaceURI.Length == 0, "family root shape"); }
                int ordinal = 0;
                foreach ((int start, int length, int line) in TopRecords(bytes))
                {
                    GI.Require(length <= request.MaxRecordBytes && metadata.Count < request.MaxRecords, "record bounds");
                    ordinal++; XElement element = ParseRecord(bytes.AsSpan(start, length));
                    GI.Require(element.Name.LocalName == shape.Child && element.Name.NamespaceName.Length == 0, "family record shape");
                    string primary = shape.Primary == "#ordinal" ? ordinal.ToString(CultureInfo.InvariantCulture) : element.Attribute(shape.Primary)?.Value ?? "";
                    string secondary = shape.Secondary is null ? "" : element.Attribute(shape.Secondary)?.Value ?? "";
                    GI.Require(primary.Length > 0 && (shape.Secondary is null || secondary.Length > 0), "required catalog key");
                    if (shape.Family is not (Family.Enchant or Family.Tempering))
                        GI.Require(int.TryParse(primary, NumberStyles.None, CultureInfo.InvariantCulture, out int numeric) && numeric > 0, "positive source key");
                    foreach (string singleton in new[] { "modifiers", "improve", "idian", "weapon_stats", "actions", "godstone", "stigma" })
                        GI.Require(shape.Family == Family.Random && singleton == "modifiers" || element.Elements(singleton).Count() <= 1, "source singular child");
                    XElement[] fn = element.DescendantsAndSelf().Where(FunctionNode).ToArray();
                    foreach (XElement f in fn) { string tag = f.Name.LocalName; tags[tag] = tags.GetValueOrDefault(tag) + 1; }
                    metadata.Add(new(new(authority, shape.Family, primary, secondary), ordinal, file.Import, file.Relative, file.Sha256,
                        start, length, Hash(bytes.AsSpan(start, length)), fn.Length, line,
                        element.Elements().Select(x => x.Name.LocalName).ToImmutableArray(),
                        element.Elements("itempart").Select(x=>x.Attribute("itemid")?.Value ?? "").ToImmutableArray()));
                    records++; functions += fn.Length;
                    if (shape.Family == Family.Item) items++;
                    if (shape.Family == Family.Skill) skills++;
                    if (shape.Family == Family.Random) variants += element.Elements("modifiers").Count();
                    if (shape.Family is Family.Enchant or Family.Tempering)
                    {
                        XElement[] levels = element.Elements(shape.Family == Family.Enchant ? "enchant_data" : "tempering_data").ToArray();
                        GI.Require(levels.Select(x => x.Attribute("level")?.Value).All(x => x is not null)
                            && levels.Select(x => x.Attribute("level")!.Value).Distinct().Count() == levels.Length, "nested group/level key");
                        if (shape.Family == Family.Enchant) enchants += levels.Length; else tempers += levels.Length;
                    }
                }
            }
            Counts expected = authority == Authority.Java ? request.JavaExpected : request.CSharpExpected;
            GI.Require(expected == new Counts(files.Length, records, items, skills, functions), "complete catalog coverage/counts");
            var duplicateGroups = metadata.Where(x => x.Key.Authority == authority).GroupBy(x => x.Key).Where(g => g.Count() > 1).ToArray();
            // Directory enumeration order is not a Java observation. It cannot decide a
            // competing definition or set-membership winner. Within-file ordinal and
            // different explicit import ordinals are the only admitted ordering here.
            foreach (var group in duplicateGroups)
                GI.Require(group.GroupBy(x => x.ImportOrdinal).All(g => g.Select(x => x.Relative).Distinct().Count() == 1), "unknown directory duplicate order");
            foreach (var group in metadata.Where(x => x.Key.Authority == authority && x.Key.Family == Family.Set)
                .SelectMany(x => x.ItemSetMembers.Select(id => (Id: id, Row: x))).GroupBy(x => x.Id))
                GI.Require(group.GroupBy(x => x.Row.ImportOrdinal).All(g => g.Select(x => x.Row.Relative).Distinct().Count() == 1), "unknown directory set-membership order");
            var duplicates = duplicateGroups.Select(g => g.Key).ToImmutableArray();
            summaries.Add(new(authority, files.Length, records, items, skills, functions, variants, enchants, tempers, tags.ToImmutableDictionary(), duplicates));
        }
        foreach (Family family in new[] { Family.Item, Family.Skill })
        {
            var java = metadata.Where(x => x.Key.Authority == Authority.Java && x.Key.Family == family).Select(x => (x.Key.Primary, x.Key.Secondary)).ToHashSet();
            GI.Require(java.SetEquals(metadata.Where(x => x.Key.Authority == Authority.CSharp && x.Key.Family == family).Select(x => (x.Key.Primary, x.Key.Secondary))), "paired identity coverage");
        }
        if (request.Origin == Origin.SelectedSourceDeclaration)
        {
            GI.Require(request.JavaExpected == new Counts(664, 121851, 102009, 13570, 348422)
                && request.CSharpExpected == new Counts(670, 121851, 102009, 13570, 348422)
                && summaries.All(x => x.DuplicateKeys.IsEmpty && x.RandomVariants == 4380 && x.EnchantLevels == 836 && x.TemperingLevels == 564), "reviewed selected-source coverage");
        }
        // A late changed file cannot leave a partially published catalog.
        foreach (FileInput file in inputs) GI.Require(HashFile(file.Path) == file.Sha256, "late source identity");
        foreach (var file in selectionFiles) GI.Require(HashFile(file.Path)==file.Hash,"late selection identity");
        return new Draft(inputs.ToImmutable(), metadata.ToImmutable(), summaries.ToImmutable());
    }
    private static string FileKey(Authority authority, string relative) => authority + ":" + relative;
    private static byte[] ReadFile(string path, long? expected, int maximum)
    {
        var info = new FileInfo(path); GI.Require(info.Exists && info.Length > 0 && info.Length <= maximum
            && (!expected.HasValue || info.Length == expected), "file size/presence");
        byte[] bytes = File.ReadAllBytes(path); GI.Require(bytes.LongLength == info.Length, "stable file length"); return bytes;
    }
    private static string HashFile(string path) { using var stream = File.OpenRead(path); return Convert.ToHexStringLower(SHA256.HashData(stream)); }
    private static string SafePath(string root, string relative)
    {
        GI.Require(!string.IsNullOrWhiteSpace(relative) && !Path.IsPathRooted(relative) && !relative.Contains('\\')
            && !relative.Contains(':') && relative.Split('/').All(x => x.Length > 0 && x is not "." and not ".."), "dataset-relative path");
        string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        GI.Require(path.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase), "source root containment");
        string cursor = fullRoot;
        foreach (string segment in relative.Split('/'))
        { cursor = Path.Combine(cursor, segment); if (File.Exists(cursor) || Directory.Exists(cursor)) GI.Require((File.GetAttributes(cursor) & FileAttributes.ReparsePoint) == 0, "source reparse point"); }
        return path;
    }
    private static void CheckImports(string root, byte[] entry, FileInput[] files)
    {
        XElement xml = ParseRecord(entry); GI.Require(xml.Name.LocalName == "static_data", "root import declaration");
        var observed = new List<(int Import, string Declared, string Relative)>(); int ordinal = 0;
        foreach (XElement import in xml.Elements("import"))
        {
            string relative = import.Attribute("file")?.Value ?? ""; string basePath = Path.Combine(root, "data", "static_data");
            string path = SafePath(basePath, relative);
            if (File.Exists(path)) observed.Add((ordinal, relative, relative));
            else
            {
                GI.Require(Directory.Exists(path), "import path");
                var options = new EnumerationOptions { RecurseSubdirectories = import.Attribute("recursiveImport")?.Value != "false", AttributesToSkip = FileAttributes.ReparsePoint };
                foreach (string child in Directory.EnumerateFiles(path, "*.xml", options))
                    observed.Add((ordinal, relative, Path.GetRelativePath(basePath, child).Replace('\\', '/')));
            }
            ordinal++;
        }
        GI.Require(observed.Count == files.Length && observed.ToHashSet().SetEquals(files.Select(f => (f.Import, f.DeclaredImport, f.Relative))), "actual import selection, no missing/extra members");
    }
    private static XmlReader Reader(byte[] bytes) => XmlReader.Create(new MemoryStream(bytes, false), new XmlReaderSettings
    { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 134_217_728, IgnoreWhitespace = false, CloseInput = true });
    internal static XElement ParseRecord(ReadOnlySpan<byte> raw)
    {
        GI.Require(raw.Length is > 0 and <= 134_217_728, "XML bounds");
        Utf8.GetString(raw); using XmlReader reader = Reader(raw.ToArray());
        XElement value = XElement.Load(reader, LoadOptions.PreserveWhitespace);
        while(reader.Read()) { /* Consume/check trailing document syntax before publishing a node. */ }
        return value;
    }
    private static void ValidateWholeXml(byte[] bytes, bool known)
    {
        using XmlReader reader = Reader(bytes); var parents = new Stack<string>();
        while (reader.Read())
        {
            GI.Require(reader.Depth <= 64, "XML depth");
            if (reader.NodeType == XmlNodeType.Element)
            {
                bool candidate = reader.LocalName is "change" or "enchant_stat" or "tempering_stat" or "penalty_attr"
                    || parents.TryPeek(out string? parent) && parent == "modifiers" && reader.LocalName is "add" or "sub" or "rate" or "set" or "abs" or "mean";
                GI.Require(known || !candidate, "unclassified selected source factory");
                if (!reader.IsEmptyElement) parents.Push(reader.LocalName);
            }
            else if (reader.NodeType == XmlNodeType.EndElement) parents.Pop();
        }
    }
    private static IEnumerable<(int Start, int Length, int Line)> TopRecords(byte[] bytes)
    {
        int depth = 0, start = -1, line = 1, recordLine = 0, cursor = 0;
        while (cursor < bytes.Length)
        {
            if (bytes[cursor] != '<') { if (bytes[cursor++] == '\n') line++; continue; }
            int token = cursor;
            if (Starts(bytes, cursor, "<!--")) { cursor = End(bytes, cursor + 4, "-->"); }
            else if (Starts(bytes, cursor, "<![CDATA[")) { cursor = End(bytes, cursor + 9, "]]>"); }
            else if (Starts(bytes, cursor, "<?")) { cursor = End(bytes, cursor + 2, "?>"); }
            else
            {
                GI.Require(!Starts(bytes, cursor, "<!"), "unsupported declaration");
                bool close = bytes[cursor + 1] == '/'; byte quote = 0; cursor++;
                while (cursor < bytes.Length)
                {
                    byte ch = bytes[cursor];
                    if (quote != 0) { if (ch == quote) quote = 0; }
                    else if (ch is 34 or 39) quote = ch;
                    else if (ch == '>') break;
                    cursor++;
                }
                GI.Require(cursor < bytes.Length, "unterminated tag");
                bool self = bytes[cursor - 1] == '/'; cursor++;
                if (!close)
                {
                    depth++; if (depth == 2) { start = token; recordLine = line; }
                    if (self) { if (depth == 2) yield return (start, cursor - start, recordLine); depth--; }
                }
                else { if (depth == 2) yield return (start, cursor - start, recordLine); depth--; }
                GI.Require(depth >= 0, "unbalanced XML");
            }
            for (int i = token; i < cursor; i++) if (bytes[i] == '\n') line++;
        }
        GI.Require(depth == 0, "complete XML root");
    }
    private static bool Starts(byte[] bytes, int at, string token) => at + token.Length <= bytes.Length && bytes.AsSpan(at, token.Length).SequenceEqual(Encoding.ASCII.GetBytes(token));
    private static int End(byte[] bytes, int from, string token)
    { for (int i = from; i + token.Length <= bytes.Length; i++) if (Starts(bytes, i, token)) return i + token.Length; throw new GI.Domain("unterminated XML token"); }
    private static void Unique(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        { var keys = new HashSet<string>(StringComparer.Ordinal); foreach (JsonProperty property in element.EnumerateObject()) { GI.Require(keys.Add(property.Name), "duplicate manifest property"); Unique(property.Value); } }
        else if (element.ValueKind == JsonValueKind.Array) foreach (JsonElement item in element.EnumerateArray()) Unique(item);
    }
    private static void Keys(JsonElement value, params string[] names)
    { GI.Require(value.ValueKind == JsonValueKind.Object && value.EnumerateObject().All(p => names.Contains(p.Name, StringComparer.Ordinal)), "unknown manifest metadata"); }
}
