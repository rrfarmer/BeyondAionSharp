using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Explicit entries from the pinned 5.8 tribe source, before defaults
/// and inherited entries. This is not a complete runtime relation matrix.</summary>
internal sealed class RetailNpcTribeExplicitRelations
{
    private const string SourceSha = "e36b331cffe86e3d36951040e071fbaeacb50c82150b30f17cda19a4ca540b6d";
    private readonly Dictionary<(string Caster, string Target), int> entries;

    private RetailNpcTribeExplicitRelations(Dictionary<(string Caster, string Target), int> entries)
        => this.entries = entries;

    // The supplied 5.8 configuration selects country 1 (USA). With the
    // standard country prefix and no regional file, its loader falls back to
    // the shared XML. A present regional file needs separate review even if it
    // might fail to parse; do not silently substitute the shared file.
    internal static RetailNpcTribeExplicitRelations ReadReviewedUsaFallback(string mapDirectory, int country)
        => ReadReviewedSource(ReadUsaSharedAfterOverrideCheck(mapDirectory, country));

    // Callers must still validate the returned content against their reviewed
    // source; this method only establishes the conservative file selection.
    internal static byte[] ReadUsaSharedAfterOverrideCheck(string mapDirectory, int country)
    {
        ArgumentException.ThrowIfNullOrEmpty(mapDirectory);
        if (country != 1) throw new InvalidDataException("Unreviewed retail tribe country");
        string xml = Path.Combine(mapDirectory, "XML");
        bool regionalFileExists;
        try
        {
            using var regional = File.Open(Path.Combine(xml, "USA", "npc_tribe_relation.xml"),
                FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            regionalFileExists = true;
        }
        catch (FileNotFoundException) { regionalFileExists = false; }
        catch (DirectoryNotFoundException) { regionalFileExists = false; }
        if (regionalFileExists)
            throw new InvalidDataException("Unreviewed USA tribe relation override");
        return File.ReadAllBytes(Path.Combine(xml, "npc_tribe_relation.xml"));
    }

    internal static RetailNpcTribeExplicitRelations ReadReviewedSource(ReadOnlySpan<byte> raw)
    {
        if (raw.Length is <= 0 or > 1_000_000 || Convert.ToHexStringLower(SHA256.HashData(raw)) != SourceSha)
            throw new InvalidDataException("Unreviewed 5.8 tribe relation source");

        using var stream = new MemoryStream(raw.ToArray(), writable: false);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore, XmlResolver = null, MaxCharactersInDocument = 500_000
        });
        var root = XDocument.Load(reader).Root;
        if (root?.Name != "npc_tribe_relations") throw new InvalidDataException("Invalid tribe relation root");
        var rows = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<(string, string), int>();
        foreach (var tribe in root.Elements())
        {
            var caster = (string?)tribe.Attribute("Tribe");
            if (tribe.Name != "tribe" || string.IsNullOrEmpty(caster) || !rows.Add(caster))
                throw new InvalidDataException("Invalid or duplicate tribe relation row");
            foreach (var relation in tribe.Elements())
            {
                int? value = relation.Name.LocalName switch
                {
                    "aggressive" => 0, "hostile" => 1, "neutral" => 2,
                    "friendly" => 3, "support" => 4, "none" => 5, "base_tribe" => null,
                    _ => throw new InvalidDataException("Unknown tribe relation kind")
                };
                if (value is null) continue;
                foreach (var target in relation.Value.Split(','))
                {
                    if (string.IsNullOrEmpty(target) || target != target.Trim()
                        || !result.TryAdd((caster.ToUpperInvariant(), target.ToUpperInvariant()), value.Value))
                        throw new InvalidDataException("Invalid or ambiguous explicit tribe relation");
                }
            }
        }
        return new(result);
    }

    // A miss is unresolved: defaults, base tribes and dynamic actor state are
    // not represented by explicit XML entries. No host relation is substituted.
    internal int? FindExplicit(string casterTribe, string targetTribe)
    {
        ArgumentException.ThrowIfNullOrEmpty(casterTribe);
        ArgumentException.ThrowIfNullOrEmpty(targetTribe);
        return entries.TryGetValue((casterTribe.ToUpperInvariant(), targetTribe.ToUpperInvariant()), out int value)
            ? value : null;
    }
}
