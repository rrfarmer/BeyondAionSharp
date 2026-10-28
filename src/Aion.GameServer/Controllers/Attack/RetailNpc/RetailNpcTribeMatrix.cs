using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Source-checked reconstruction of the 5.8 startup tribe table.
/// Runtime tribe changes and the other enemy restrictions are not represented.</summary>
internal sealed class RetailNpcTribeMatrix
{
    private const string SourceSha = "e36b331cffe86e3d36951040e071fbaeacb50c82150b30f17cda19a4ca540b6d";
    // The 15 x 15 native base table, in caster-major order, from the pinned
    // MainServer64 image at 0x1410e2030. Values are 0..5.
    private const string NativeDefaults =
        "311332115115315131115332115315113115115332315311332115115315400442005005315255222555555355131115332115315040005442005315525555222555355113115115332315004005005442315552555555222355333333333333335111115115115335555555555555555";
    private static readonly string[] Predefined = ["PC", "PC_Dark", "PC_Dragon", "General", "Guard",
        "Field_Object_Light", "General_Dark", "Guard_Dark", "Field_Object_Dark", "General_Dragon",
        "Guard_Dragon", "Field_Object_Dragon", "UseAll", "Monster", "None"];
    private readonly Dictionary<string, int> ids;
    private readonly byte[] cells;
    private readonly int count;

    private RetailNpcTribeMatrix(Dictionary<string, int> ids, byte[] cells, int count)
        => (this.ids, this.cells, this.count) = (ids, cells, count);

    internal static RetailNpcTribeMatrix ReadReviewedUsaFallback(string mapDirectory, int country)
        => ReadReviewedSource(RetailNpcTribeExplicitRelations.ReadUsaSharedAfterOverrideCheck(mapDirectory, country));

    internal static RetailNpcTribeMatrix ReadReviewedSource(ReadOnlySpan<byte> raw)
    {
        if (raw.Length is <= 0 or > 1_000_000 || Convert.ToHexStringLower(SHA256.HashData(raw)) != SourceSha)
            throw new InvalidDataException("Unreviewed 5.8 tribe relation source");
        using var stream = new MemoryStream(raw.ToArray(), writable: false);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore, XmlResolver = null, MaxCharactersInDocument = 500_000
        });
        var root = XDocument.Load(reader).Root;
        if (root?.Name != "npc_tribe_relations" || NativeDefaults.Length != 225)
            throw new InvalidDataException("Invalid retail tribe source or base table");
        var ids = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (string name in Predefined) ids.Add(name, ids.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in root.Elements())
        {
            string? name = (string?)row.Attribute("Tribe");
            if (row.Name != "tribe" || string.IsNullOrEmpty(name) || !seen.Add(name))
                throw new InvalidDataException("Invalid or duplicate retail tribe");
            if (!ids.ContainsKey(name)) ids.Add(name, ids.Count);
        }
        int n = ids.Count;
        if (n > 10_000) throw new InvalidDataException("Unbounded retail tribe count");
        var cells = new byte[checked(n * n)];
        Array.Fill(cells, byte.MaxValue);
        var bases = new SortedDictionary<int, int>();
        foreach (var row in root.Elements())
        {
            int caster = ids[(string)row.Attribute("Tribe")!];
            foreach (var node in row.Elements())
            {
                if (node.Name == "base_tribe")
                {
                    // Native lookup skips an unknown base rather than creating it.
                    if (ids.TryGetValue(node.Value, out int baseId)) bases.Add(caster, baseId);
                    continue;
                }
                byte relation = node.Name.LocalName switch
                {
                    "aggressive" => 0, "hostile" => 1, "neutral" => 2,
                    "friendly" => 3, "support" => 4, "none" => 5,
                    _ => throw new InvalidDataException("Unknown retail tribe relation")
                };
                foreach (string target in node.Value.Split(','))
                {
                    if (target.Length == 0 || target != target.Trim() || !ids.TryGetValue(target, out int targetId))
                        throw new InvalidDataException("Unknown explicit retail tribe target");
                    int index = caster * n + targetId;
                    if (cells[index] != byte.MaxValue) throw new InvalidDataException("Ambiguous retail relation");
                    cells[index] = relation;
                }
            }
        }
        void SetIfMissing(int caster, int target, byte value)
        {
            int index = caster * n + target;
            if (cells[index] == byte.MaxValue) cells[index] = value;
        }
        byte Default(int caster, int target) => checked((byte)(NativeDefaults[caster * 15 + target] - '0'));
        for (int i = 0; i < 15; i++)
            for (int j = 0; j < 15; j++) SetIfMissing(i, j, Default(i, j));
        for (int i = 0; i < 15; i++)
            for (int j = 15; j < n; j++)
                if (!bases.ContainsKey(j)) SetIfMissing(j, i, Default(13, i));
        for (int i = 0; i < 15; i++)
            for (int j = 15; j < n; j++)
                if (!bases.ContainsKey(j)) SetIfMissing(i, j, Default(i, 13));
        for (int i = 15; i < n; i++)
            if (!bases.ContainsKey(i))
                for (int j = 15; j < n; j++)
                    if (!bases.ContainsKey(j)) SetIfMissing(i, j, 3);
        foreach (var (child, parent) in bases)
            for (int i = 0; i < n; i++)
            {
                SetIfMissing(i, child, cells[i * n + parent]);
                SetIfMissing(child, i, cells[parent * n + i]);
            }
        if (cells.Any(x => x > 5)) throw new InvalidDataException("Incomplete retail tribe matrix");
        return new(ids, cells, n);
    }

    internal int? Find(string caster, string target)
    {
        ArgumentException.ThrowIfNullOrEmpty(caster);
        ArgumentException.ThrowIfNullOrEmpty(target);
        return ids.TryGetValue(caster, out int i) && ids.TryGetValue(target, out int j)
            ? cells[i * count + j] : null;
    }
}
