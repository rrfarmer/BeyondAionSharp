using System.Collections.Frozen;
using System.Text.Json;
using System.Xml.Linq;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Dataholders.RetailNpc;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Retail;

internal static partial class RetailNpcStatCatalog
{
    private sealed record BuffIdentity(string SourceName, string SourceSha256, string HostStack, int HostNameId)
    {
        internal bool Matches(RetailNpcStatBuffs.Source source, SkillTemplate host) =>
            source.Name == SourceName && source.Sha256 == SourceSha256
            && host.GetStack() == HostStack && host.GetL10nId() == HostNameId;
    }

    private static FrozenDictionary<int, BuffIdentity> ReadBuffIdentities(JsonElement root, string directory,
        Dictionary<int, RetailNpcStatBuffs.Source> sources)
    {
        if (!root.TryGetProperty("buff_identity_review", out var asset)) return FrozenDictionary<int, BuffIdentity>.Empty;
        using var doc = JsonDocument.Parse(ReadAsset(asset, directory));
        RetailNpcDatasetValidation.UniqueJson(doc.RootElement);
        var review = doc.RootElement;
        if (review.GetProperty("version").GetInt32() != 1)
            throw new InvalidDataException("Unsupported buff identity review");
        using var namesStream = new MemoryStream(ReadAsset(review.GetProperty("host_native_names"), directory));
        var names = XDocument.Load(namesStream).Root?.Elements("skill").ToDictionary(
            x => (int)x.Attribute("id")!, x => (string)x.Attribute("name")!)
            ?? throw new InvalidDataException("Missing host skill identity catalog");
        var result = new Dictionary<int, BuffIdentity>();
        foreach (var row in review.GetProperty("entries").EnumerateArray())
        {
            int id = row.GetProperty("id").GetInt32();
            var identity = new BuffIdentity(row.GetProperty("source_name").GetString()!,
                row.GetProperty("source_sha256").GetString()!, row.GetProperty("host_stack").GetString()!,
                row.GetProperty("host_name_id").GetInt32());
            if (!sources.TryGetValue(id, out var source) || source.Name != identity.SourceName
                || source.Sha256 != identity.SourceSha256 || string.IsNullOrEmpty(identity.HostStack)
                || !names.TryGetValue(id, out var hostName) || !SameNativeSkillName(source.Name, hostName)
                || !result.TryAdd(id, identity))
                throw new InvalidDataException("Buff identity review does not join the pinned source and host names: " + id);
        }
        return result.ToFrozenDictionary();
    }
}
