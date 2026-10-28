using System.Collections.Frozen;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Retail;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Source-derived Player exclusive responses and checked item coverage.
/// Native WearEffect builds target map1798 from item exclusive_attribute1..5.
/// Positive responses require retained actor/item event history; unknown IDs
/// and missing history remain explicit boundaries.
/// Evidence: physical-conditional-force-review/exclusive-review/REPORT.md.</summary>
internal sealed partial class RetailNpcStatEquipmentResponses
{
    internal const string ReviewedContextSha256="8e058a8caed8346c3abea56fe0f2358ff3d822301c83c5a9f9de1e58153bb2b9";
    private readonly FrozenSet<int> known,responders;
    internal int KnownCount=>known.Count;
    internal int ResponderCount=>responders.Count;
    private readonly FrozenDictionary<int,Definition[]> itemResponses;
    private readonly FrozenDictionary<string,Definition> definitions;
    private RetailNpcStatEquipmentResponses(IEnumerable<int> known,Dictionary<int,Dictionary<string,string>> responders,Dictionary<string,Definition> definitions)
    {
        this.known=known.ToFrozenSet();this.responders=responders.Keys.ToFrozenSet();
        this.definitions=definitions.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        itemResponses=responders.ToFrozenDictionary(x=>x.Key,x=>x.Value.OrderBy(p=>p.Key,StringComparer.Ordinal)
            .Select(p=>definitions.TryGetValue(p.Value,out var d)?d:throw new InvalidDataException("Unknown response definition: "+p.Value)).ToArray());
    }

    internal bool TryEmpty(Player target,out string reason)
        =>CheckItems(target.GetEquipment().GetEquippedItemIds(),out reason);
    internal static bool Required(SkyTempleNpcProfile profile) => SkyTempleNpcCombat.RequiresEquipmentResponse(profile);
    internal static bool TryEmptyTarget(Player target,out string reason)
    {
        if(RetailNpcStatCatalog.EquipmentResponses is not { } context)
        {reason="source-offensive-exclusive-attribute-unverified";return false;}
        return context.TryEmpty(target,out reason);
    }
    internal bool CheckItems(IEnumerable<int> actualItemIds,out string reason)
    {
        foreach(int id in actualItemIds)
        {
            if(!known.Contains(id)){reason="source-exclusive-unknown-equipped-item-"+id;return false;}
            if(responders.Contains(id)){reason="source-exclusive-nonzero-response-equipment-"+id;return false;}
        }
        reason="source-equipment-response-empty";return true;
    }

    internal static RetailNpcStatEquipmentResponses Parse(byte[] bytes,string hash)
    {
        Need(hash==ReviewedContextSha256&&Convert.ToHexStringLower(SHA256.HashData(bytes))==hash,"Unreviewed equipment response context");
        using var doc=JsonDocument.Parse(bytes);var root=doc.RootElement;
        Need(root.GetProperty("schema_version").GetInt32()==1
            &&root.GetProperty("main").GetProperty("sha256").GetString()==RetailNpcNormalScope.MainBinarySha256,"Equipment native identity differs");
        using(var main=OpenPinned(root.GetProperty("main"))) { }
        var config=root.GetProperty("config");
        Need(config.GetProperty("country").GetInt32()==1&&config.GetProperty("region").GetString()=="USA"
            &&config.GetProperty("aion_event").GetBoolean(),"Equipment country/event source differs");
        using(var file=OpenPinned(config)) { }
        foreach(var candidate in root.GetProperty("event_override").GetProperty("path_candidates").EnumerateArray())
            Absent(candidate.GetProperty("path").GetString()!);
        var definition=root.GetProperty("exclusive_definitions");Absent(definition.GetProperty("USA_candidate").GetString()!);
        Dictionary<string,Definition> definitions;
        using(var file=OpenPinned(definition))
        using(var reader=XmlReader.Create(file,new XmlReaderSettings {DtdProcessing=DtdProcessing.Ignore,XmlResolver=null}))
        {
            var rows=XDocument.Load(reader).Root??throw new InvalidDataException("Missing response definitions");
            Need(rows.Name=="exclusive_attributes","Response definition root differs");
            var expected=definition.GetProperty("definitions").EnumerateArray().ToArray();Need(rows.Elements().Count()==expected.Length,"Incomplete response definitions");
            definitions=new(StringComparer.OrdinalIgnoreCase);int ordinal=0;
            foreach(var row in rows.Elements())
            {
                var fields=row.Elements().ToDictionary(x=>x.Name.LocalName,x=>x.Value.Trim(),StringComparer.Ordinal);
                var pinned=expected[ordinal++].EnumerateObject().ToArray();
                Need(fields.Count==pinned.Length&&pinned.All(x=>fields.TryGetValue(x.Name,out var value)&&value==x.Value.GetString()),"Response definition fields differ");
                var d=Definition.From(fields);Need(definitions.TryAdd(d.Name,d),"Duplicate response definition");
            }
        }
        string[] names=["item_armors.xml","item_accessories.xml","item_etc.xml","item_quest.xml","item_weapons.xml"];
        var files=root.GetProperty("selected_files").EnumerateArray().ToArray();Need(files.Length==names.Length,"Incomplete divided item inputs");
        var ids=new HashSet<int>();var response=new Dictionary<int,Dictionary<string,string>>();
        for(int ordinal=0;ordinal<files.Length;ordinal++)
        {
            var file=files[ordinal];Need(file.GetProperty("ordinal").GetInt32()==ordinal&&file.GetProperty("name").GetString()==names[ordinal],"Item loader order differs");
            Absent(file.GetProperty("USA_candidate").GetString()!);
            using var stream=OpenPinned(file);
            // Retail files carry an external DOCTYPE header. Skip that header;
            // never resolve a DTD or substitute externally supplied entities.
            using var reader=XmlReader.Create(stream,new XmlReaderSettings {DtdProcessing=DtdProcessing.Ignore,XmlResolver=null,MaxCharactersInDocument=512L*1024*1024});
            reader.MoveToContent();Need(reader.Name=="items"&&reader.Depth==0&&reader.NamespaceURI.Length==0,"Item source root differs");
            int count=0,responseCount=0;
            while(reader.Read())
            {
                if(reader.NodeType!=XmlNodeType.Element||reader.Depth!=1)continue;
                Need(reader.Name=="item","Unreviewed item root child");
                using var item=reader.ReadSubtree();item.Read();int? id=null;var fields=new Dictionary<string,string>(StringComparer.Ordinal);
                while(!item.EOF)
                {
                    if(item.NodeType==XmlNodeType.Element&&item.Depth==1)
                    {
                        string name=item.Name;
                        if(name=="id"||name.Length==20&&name.StartsWith("exclusive_attribute",StringComparison.Ordinal)&&name[^1] is >= '1' and <= '5')
                        {
                            Need(!item.HasAttributes,"Item response leaf has attributes");string text=item.ReadElementContentAsString().Trim();
                            if(name=="id")
                            {
                                Need(id is null&&int.TryParse(text,NumberStyles.None,CultureInfo.InvariantCulture,out _),"Invalid/duplicate item ID");
                                id=int.Parse(text,CultureInfo.InvariantCulture);
                            }
                            else Need(fields.TryAdd(name,text),"Duplicate response operand");
                            continue;
                        }
                        item.Skip();continue;
                    }
                    item.Read();
                }
                Need(id is >0&&ids.Add(id.Value),"Duplicate/missing selected item ID");
                if(fields.Count>0){response.Add(id!.Value,fields);responseCount++;}count++;
            }
            Need(count==file.GetProperty("records").GetInt32()&&responseCount==file.GetProperty("response_records").GetInt32(),"Incomplete item response scan");
        }
        var expectedIds=root.GetProperty("item_ids").EnumerateArray().Select(x=>x.GetInt32()).ToArray();
        Need(expectedIds.Length==ids.Count&&ids.SetEquals(expectedIds),"Selected item index differs");
        var expectedResponses=root.GetProperty("responders").EnumerateArray().ToArray();Need(response.Count==expectedResponses.Length,"Response index differs");
        foreach(var r in expectedResponses)
        {
            Need(response.TryGetValue(r.GetProperty("item_id").GetInt32(),out var actual),"Missing response item");
            var fields=r.GetProperty("fields").EnumerateObject().ToArray();
            Need(fields.Length==actual!.Count&&fields.All(p=>actual.TryGetValue(p.Name,out var v)&&v==p.Value.GetString()),"Response fields differ");
        }
        Need(response.Values.All(f=>f.ContainsKey("exclusive_attribute1")),"Unproved missing first response slot");
        return new(ids,response,definitions);
    }
    private static FileStream OpenPinned(JsonElement file)
    {
        var stream=new FileStream(file.GetProperty("path").GetString()!,FileMode.Open,FileAccess.Read,FileShare.Read);
        try
        {
            Need(stream.Length is >0 and <=512L*1024*1024&&stream.Length==file.GetProperty("bytes").GetInt64(),"Equipment source length differs");
            Need(Convert.ToHexStringLower(SHA256.HashData(stream))==file.GetProperty("sha256").GetString(),"Equipment source hash differs");
            stream.Position=0;return stream;
        }
        catch {stream.Dispose();throw;}
    }
    private static void Absent(string path)=>Need(!File.Exists(path),"Equipment regional/event override appeared: "+path);
    private static void Need(bool condition,string message){if(!condition)throw new InvalidDataException(message);}
}
