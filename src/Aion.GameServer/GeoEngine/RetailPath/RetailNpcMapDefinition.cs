using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;

namespace Aion.GameServer.GeoEngine.RetailPath;

/// <summary>Manifest-selected bytes and explicit managed compatibility coverage.
/// Unknown regions are exclusions, never declarations that missing geometry is empty.
/// Counts/byte budgets are support limits, not native defaults or process-memory caps.</summary>
internal sealed record RetailNpcMapAsset(string Path,string Sha256,long ByteLength)
{
    internal byte[] Read(string directory,long maximum)
    {
        string path=System.IO.Path.GetFullPath(System.IO.Path.IsPathRooted(Path)?Path:System.IO.Path.Combine(directory,Path));
        var file=new FileInfo(path);
        if(ByteLength<=0||ByteLength>maximum||ByteLength>int.MaxValue||!file.Exists||file.Length!=ByteLength)
            throw new InvalidDataException("Retail map asset length/limit mismatch: "+path);
        byte[] bytes=File.ReadAllBytes(path);
        if(bytes.LongLength!=ByteLength||Convert.ToHexStringLower(SHA256.HashData(bytes))!=Sha256)
            throw new InvalidDataException("Retail map asset identity mismatch: "+path);
        return bytes;
    }
}
internal sealed record RetailNpcMapPlaceable(int EditorId,bool MatchedNative,RetailSearchPoint Minimum,RetailSearchPoint Maximum,string TransformSha256);
internal sealed record RetailNpcUnknownRegion(int X,int Y,string Reason,ImmutableArray<int> ObjectIds);
internal sealed record RetailNpcMapLimits(long PathMetadataBytes,long CollisionMetadataBytes,long PreparationMetadataBytes,
    int MaxVertices,int MaxTriangles,int MaxBands,int MaxReferences,int MaxFields);
internal sealed record RetailNpcMapDefinition(int MapId,string Devname,int WidthMetres,int HeightMetres,string Directory,
    Guid BuildGuid,ImmutableDictionary<string,RetailNpcMapAsset> Assets,ImmutableArray<RetailNpcMapPlaceable> Placeables,
    ImmutableArray<RetailNpcUnknownRegion> UnknownRegions,RetailNpcMapLimits Limits)
{
    internal RetailNpcMapAsset Asset(string role)=>Assets.TryGetValue(role,out var value)?value:
        throw new InvalidDataException("Missing retail map asset role: "+role);
    internal bool IsKnown(RetailSearchPoint point)=>point.IsFinite&&point.X>=0&&point.Y>=0&&point.X<WidthMetres&&point.Y<HeightMetres&&
        !UnknownRegions.Any(r=>r.X==(int)point.X/16&&r.Y==(int)point.Y/16);
    internal static RetailNpcMapDefinition Parse(JsonElement row)
    {
        Shape(row,"map_id","devname","width_metres","height_metres","directory","build_guid","assets","placeables","unknown_regions","limits");
        int id=Int(row,"map_id",1,int.MaxValue),width=Int(row,"width_metres",64,4096),height=Int(row,"height_metres",64,4096);
        if(width%64!=0||height%64!=0)throw new InvalidDataException("Retail map dimensions must be explicit64m multiples");
        string dev=Text(row,"devname"),directory=System.IO.Path.GetFullPath(Text(row,"directory"));
        if(!Guid.TryParse(Text(row,"build_guid"),out var guid)||guid==Guid.Empty)throw new InvalidDataException("Map GUID required");
        var assets=row.GetProperty("assets");
        Shape(assets,"path","heightmap40","mesh_material","collision","waypoints","makers","world","legacy_geo","legacy_models","mapping_evidence");
        var builder=ImmutableDictionary.CreateBuilder<string,RetailNpcMapAsset>(StringComparer.Ordinal);
        foreach(var p in assets.EnumerateObject())
        {
            Shape(p.Value,"path","sha256","byte_length");string digest=Text(p.Value,"sha256");
            if(digest.Length!=64||digest.Any(c=>!char.IsAsciiHexDigit(c))||digest!=digest.ToLowerInvariant())
                throw new InvalidDataException("Map asset requires lower-case SHA256: "+p.Name);
            long bytes=p.Value.GetProperty("byte_length").GetInt64();
            if(bytes is <=0 or >1_073_741_824)throw new InvalidDataException("Map asset byte bound");
            builder.Add(p.Name,new(Text(p.Value,"path"),digest,bytes));
        }
        var placeables=ImmutableArray.CreateBuilder<RetailNpcMapPlaceable>();var ids=new HashSet<int>();
        var ps=row.GetProperty("placeables");if(ps.ValueKind!=JsonValueKind.Array||ps.GetArrayLength()>4096)throw new InvalidDataException("Placeable catalog bound");
        foreach(var p in ps.EnumerateArray())
        {
            Shape(p,"editor_id","matched_native","minimum","maximum","transform_sha256");int editor=Int(p,"editor_id",1,ushort.MaxValue);
            var lo=Point(p.GetProperty("minimum"));var hi=Point(p.GetProperty("maximum"));
            if(!ids.Add(editor)||lo.X>hi.X||lo.Y>hi.Y||lo.Z>hi.Z)throw new InvalidDataException("Duplicate/invalid placeable bounds");
            string transform=Text(p,"transform_sha256");
            if(transform.Length!=64||transform.Any(c=>!char.IsAsciiHexDigit(c))||transform!=transform.ToLowerInvariant())throw new InvalidDataException("Placeable transform SHA256 required");
            placeables.Add(new(editor,p.GetProperty("matched_native").GetBoolean(),lo,hi,transform));
        }
        var holes=ImmutableArray.CreateBuilder<RetailNpcUnknownRegion>();var cells=new HashSet<(int,int)>();
        var hs=row.GetProperty("unknown_regions");if(hs.ValueKind!=JsonValueKind.Array||hs.GetArrayLength()>width/16*(height/16))throw new InvalidDataException("Unknown region bound");
        foreach(var h in hs.EnumerateArray())
        {
            Shape(h,"x","y","reason","object_ids");int x=Int(h,"x",0,width/16-1),y=Int(h,"y",0,height/16-1);
            if(!cells.Add((x,y)))throw new InvalidDataException("Duplicate unknown region");
            var os=h.GetProperty("object_ids");if(os.ValueKind!=JsonValueKind.Array||os.GetArrayLength() is <1 or >4096)throw new InvalidDataException("Unknown-region object identities required");
            int[] objects=os.EnumerateArray().Select(v=>v.GetInt32()).ToArray();
            if(objects.Any(v=>v<=0||v>ushort.MaxValue)||objects.Distinct().Count()!=objects.Length)throw new InvalidDataException("Unknown-region identities invalid");
            holes.Add(new(x,y,Text(h,"reason"),objects.ToImmutableArray()));
        }
        var l=row.GetProperty("limits");Shape(l,"path_metadata_bytes","collision_metadata_bytes","preparation_metadata_bytes","max_vertices","max_triangles","max_bands","max_references","max_fields");
        long Budget(string n){long v=l.GetProperty(n).GetInt64();if(v is <=0 or >1_073_741_824)throw new InvalidDataException("Map metadata budget");return v;}
        var limits=new RetailNpcMapLimits(Budget("path_metadata_bytes"),Budget("collision_metadata_bytes"),Budget("preparation_metadata_bytes"),
            Int(l,"max_vertices",1,2_000_000),Int(l,"max_triangles",1,4_000_000),Int(l,"max_bands",1,16_000_000),
            Int(l,"max_references",1,64_000_000),Int(l,"max_fields",1,4096));
        return new(id,dev,width,height,directory,guid,builder.ToImmutable(),placeables.OrderBy(p=>p.EditorId).ToImmutableArray(),holes.ToImmutable(),limits);
    }
    private static RetailSearchPoint Point(JsonElement p)
    {
        if(p.ValueKind!=JsonValueKind.Array||p.GetArrayLength()!=3)throw new InvalidDataException("Expected finite XYZ");
        var a=p.EnumerateArray().Select(x=>x.GetSingle()).ToArray();var v=new RetailSearchPoint(a[0],a[1],a[2]);
        if(!v.IsFinite)throw new InvalidDataException("Nonfinite map bounds");return v;
    }
    private static string Text(JsonElement row,string name)
    {string? v=row.GetProperty(name).GetString();if(string.IsNullOrWhiteSpace(v)||v.Length>4096)throw new InvalidDataException("Map text: "+name);return v;}
    private static int Int(JsonElement row,string name,int min,int max)
    {int v=row.GetProperty(name).GetInt32();if(v<min||v>max)throw new InvalidDataException("Map integer: "+name);return v;}
    private static void Shape(JsonElement row,params string[] names)
    {
        if(row.ValueKind!=JsonValueKind.Object)throw new InvalidDataException("Map definition requires objects");
        string[] actual=row.EnumerateObject().Select(p=>p.Name).ToArray();
        if(actual.Length!=names.Length||actual.Distinct(StringComparer.Ordinal).Count()!=actual.Length||actual.Except(names,StringComparer.Ordinal).Any())
            throw new InvalidDataException("Missing/duplicate/unknown map definition field");
    }
}
