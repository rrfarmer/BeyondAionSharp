using System.Collections.Frozen;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Aion.GameServer.Dataholders.RetailNpc;
using Aion.GameServer.Model;

namespace Aion.GameServer.Retail;

/// <summary>Combat-only selected USA sources. Native proof: external
/// stats-only-20261025-01/coverage-worker/combat-map-stats. No geometry or AI admission.
/// World bases are added after the precomputed player bonus getter, then the NPC
/// counterparty delta. Neither source is a replacement for that bonus getter.</summary>
internal sealed class RetailNpcStatPveInputs
{
    internal const string WorldSourceSha256 = "92eeb260572fc3e2aa57ec814fa65c2164ded14eee85eb49e31f73e56742fa2a";
    internal const string NpcSourceSha256 = "8e43dcb1fbf1531ebf7a2090727a30860acba05841f8658056420e1ac3f380c4";
    private const int MaximumBytes = 2 * 1024 * 1024;
    private sealed record WorldRow(bool Apply, FrozenDictionary<string, int> Values);
    private readonly FrozenDictionary<int, WorldRow> worlds;
    private readonly FrozenDictionary<string, FrozenDictionary<string, int>> npcs;
    private readonly FrozenSet<int> supportedMaps;
    private static readonly string[] Fields =
    [
        "pve_attack_ratio_physical_light", "pve_attack_ratio_physical_dark",
        "pve_defend_ratio_physical_light", "pve_defend_ratio_physical_dark",
        "pve_attack_ratio_magical_light", "pve_attack_ratio_magical_dark",
        "pve_defend_ratio_magical_light", "pve_defend_ratio_magical_dark"
    ];

    private RetailNpcStatPveInputs(byte[] worldXml, byte[] npcXml, IEnumerable<int> mapIds)
    {
        int[] maps = mapIds.ToArray();
        Require(maps.Length is > 0 and <= 2048 && maps.All(x => x > 0)
            && maps.Distinct().Count() == maps.Length, "Missing/duplicate combat map membership");
        supportedMaps = maps.ToFrozenSet();
        worlds = ReadWorlds(worldXml);
        npcs = ReadNpcs(npcXml);
    }

    internal static RetailNpcStatPveInputs Parse(ReadOnlySpan<byte> bytes, string expectedHash)
    {
        Require(bytes.Length is > 0 and <= MaximumBytes, "Oversized/missing combat PvE source artifact");
        Hash(bytes, expectedHash, "combat PvE source artifact");
        using var document = JsonDocument.Parse(bytes.ToArray(), new() { MaxDepth = 64 });
        JsonElement root = document.RootElement;
        RetailNpcDatasetValidation.UniqueJson(root);
        Require(root.GetProperty("version").GetInt32() == 1
            && root.GetProperty("dataset_id").GetString() == RetailNpcStatCatalog.SourceDatasetId
            && root.GetProperty("region").GetString() == "USA"
            && root.GetProperty("country_code").GetInt32() == 1
            && root.GetProperty("special_server_type").GetInt32() == 0,
            "Unreviewed combat PvE source region/special-server context");
        Require(root.GetProperty("global_pve_defaults").EnumerateArray().Select(x => x.GetInt32())
            .SequenceEqual(new[] { 0, 0, 0, 0 })
            && root.GetProperty("world_apply_flag_default").ValueKind == JsonValueKind.True,
            "Combat PvE constructor defaults differ from the native proof");
        byte[] world = Source(root.GetProperty("world_source"), WorldSourceSha256,
            "USA/ID/WorldId.xml", "ID/WorldId.xml");
        byte[] npc = Source(root.GetProperty("npc_adjust_source"), NpcSourceSha256,
            "USA/pve_adjust.xml", "pve_adjust.xml");
        return new(world, npc, root.GetProperty("supported_map_ids").EnumerateArray().Select(x => x.GetInt32()));
    }

    private static byte[] Source(JsonElement source, string expectedHash, string relative, string requested)
    {
        Require(source.GetProperty("sha256").GetString() == expectedHash
            && source.GetProperty("selected_relative").GetString() == relative
            && source.GetProperty("native_relative_request").GetString() == requested,
            "Combat source selection differs from the complete USA override");
        byte[] raw = Convert.FromBase64String(source.GetProperty("base64").GetString()!);
        Require(raw.Length is > 0 and <= MaximumBytes && raw.Length == source.GetProperty("bytes").GetInt64(),
            "Combat source size differs");
        Hash(raw, expectedHash, relative);
        return raw;
    }

    internal bool HasMapSource(int mapId) => worlds.ContainsKey(mapId);
    internal bool HasNpcOverride(string devname) => npcs.ContainsKey(devname);
    internal int SupportedMapCount => supportedMaps.Count;

    /// <summary>User::UpdatePVEAdjust, 14035ae00. A missing selected WorldID row
    /// or disabled apply flag takes the independently initialized four zero globals.
    /// This does not overlay the base WorldId file onto a complete USA selection.</summary>
    internal int PlayerBase(int mapId, Race playerRace, bool physical, bool attack)
    {
        string field = Field(playerRace, physical, attack);
        Require(supportedMaps.Contains(mapId), "Combat map is outside the selected stat package: " + mapId);
        return worlds.TryGetValue(mapId, out var row) && row.Apply ? row.Values[field] : 0;
    }

    /// <summary>Npc slot190, 14036c1a0. For NPC attacks this is added to PLAYER
    /// DEFENSE after its getter. For player attacks it is added to PLAYER ATTACK.
    /// Do not move this term across the ratio arguments or ahead of player effects.</summary>
    internal int NpcCounterpartyDelta(SkyTempleNpcProfile profile, Race playerRace,
        bool physical, bool npcAttack) => NpcCounterpartyDelta(profile.Devname, profile.Source.RawRecord,
            playerRace, physical, npcAttack);

    internal int NpcCounterpartyDelta(string devname, JsonElement rawNpc, Race playerRace,
        bool physical, bool npcAttack)
    {
        string field = Field(playerRace, physical, attack: !npcAttack);
        int table = npcs.TryGetValue(devname, out var row) ? row[field] : 0;
        string template = "npc_pve_" + (npcAttack ? "attack" : "defend")
            + "_ratio_" + (physical ? "physical" : "magical");
        return unchecked(table - TemplateRatio(rawNpc, template));
    }

    internal static int TemplateRatio(JsonElement rawNpc, string name)
    {
        // Native +23c/+240/+244/+248 are signed32 constructor zeros. Repeated or
        // aliased raw roots require an ordered source review; do not first-select.
        var rows = RetailNpcSourceProfile.RawChildren(rawNpc)
            .Where(x => string.Equals(x[0].GetString(), name, StringComparison.OrdinalIgnoreCase)).ToArray();
        Require(rows.Length <= 1, "Repeated NPC PvE template field: " + name);
        if (rows.Length == 0) return 0;
        Require(rows[0][0].GetString() == name && !RetailNpcSourceProfile.RawChildren(rows[0]).Any(),
            "Unreviewed NPC PvE template field alias/layout: " + name);
        return Integer(RetailNpcSourceProfile.RawText(rows[0]), name);
    }

    private static string Field(Race race, bool physical, bool attack) => "pve_"
        + (attack ? "attack" : "defend") + "_ratio_" + (physical ? "physical" : "magical")
        + (race switch { Race.ELYOS => "_light", Race.ASMODIANS => "_dark",
            _ => throw new InvalidDataException("Combat counterparty requires an actual player faction") });

    private static FrozenDictionary<int, WorldRow> ReadWorlds(byte[] raw)
    {
        XElement root = Xml(raw, "world_id");
        var result = new Dictionary<int, WorldRow>();
        foreach (var row in root.Elements())
        {
            Require(row.Name == "data", "Unexpected combat WorldID record");
            var attrs = Attributes(row);
            Require(attrs.TryGetValue("id", out var id), "Missing combat WorldID");
            int map = Integer(id!, "WorldID");
            bool apply = !attrs.TryGetValue("apply_pve_adjust", out var flag) || Boolean(flag);
            // 14007f570 clamps every world-table channel before publication.
            var values = Fields.ToFrozenDictionary(x => x,
                x => attrs.TryGetValue(x, out var text) ? Math.Clamp(Integer(text, x), -10000, 10000) : 0,
                StringComparer.Ordinal);
            Require(map > 0 && result.TryAdd(map, new(apply, values)), "Duplicate/invalid combat WorldID");
        }
        Require(result.Count > 0, "Empty combat WorldID source");
        return result.ToFrozenDictionary();
    }

    private static FrozenDictionary<string, FrozenDictionary<string, int>> ReadNpcs(byte[] raw)
    {
        XElement root = Xml(raw, "pve_adjusts");
        var result = new Dictionary<string, FrozenDictionary<string, int>>(StringComparer.Ordinal);
        foreach (var row in root.Elements())
        {
            Require(row.Name == "name", "Unexpected NPC PvE adjustment record");
            var attrs = Attributes(row);
            Require(attrs.TryGetValue("name", out var name) && !string.IsNullOrWhiteSpace(name),
                "Missing NPC PvE devname");
            var values = Fields.ToDictionary(x => x, _ => 0, StringComparer.Ordinal);
            foreach (var child in row.Elements())
            {
                Require(child.Name.Namespace == XNamespace.None, "Namespaced NPC PvE field");
                string field = child.Name.LocalName.ToLowerInvariant();
                if (!values.ContainsKey(field)) continue; // Native ignores unrelated children/comments.
                Require(!child.HasElements, "Nested NPC PvE scalar");
                values[field] = Integer(child.Value, field); // Native ordered writes; no world-row clamp here.
            }
            Require(result.TryAdd(name!, values.ToFrozenDictionary(StringComparer.Ordinal)),
                "Duplicate NPC PvE devname");
        }
        return result.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private static Dictionary<string, string> Attributes(XElement row)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in row.Attributes())
            Require(a.Name.Namespace == XNamespace.None && result.TryAdd(a.Name.LocalName, a.Value),
                "Repeated/aliased combat XML attribute");
        return result;
    }

    private static XElement Xml(byte[] raw, string expected)
    {
        try
        {
            using var stream = new MemoryStream(raw, writable: false);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            { DtdProcessing = DtdProcessing.Parse, XmlResolver = null, MaxCharactersFromEntities = 4096,
                MaxCharactersInDocument = MaximumBytes });
            var root = XDocument.Load(reader, LoadOptions.PreserveWhitespace).Root;
            Require(root is not null && root.Name == expected, "Unexpected combat XML root");
            return root!;
        }
        catch (XmlException error) { throw new InvalidDataException("Invalid combat XML", error); }
    }

    private static bool Boolean(string text) => text.ToLowerInvariant() switch
    { "true" => true, "false" => false, _ => throw new InvalidDataException("Invalid native combat boolean") };
    private static int Integer(string text, string field)
    {
        Require(int.TryParse(text.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value),
            "Invalid signed32 combat field: " + field);
        return value;
    }
    private static void Hash(ReadOnlySpan<byte> bytes, string expected, string label) => Require(
        expected is { Length: 64 } && expected.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f')
        && Convert.ToHexStringLower(SHA256.HashData(bytes)) == expected, "SHA256 differs: " + label);
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidDataException(message); }

    // Pure parser/math fixtures only; production always enters through Parse and its source pins.
    internal static RetailNpcStatPveInputs FromXmlForTests(byte[] world, byte[] npc, params int[] maps)
        => new(world, npc, maps);
}
