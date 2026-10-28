using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Walker;
using Aion.GameServer.Retail;
using Aion.GameServer.SpawnEngine;
using static Aion.GameServer.GeoEngine.RetailPath.RetailMovementArithmetic;

namespace Aion.GameServer.GeoEngine.RetailPath;

/// <summary>Source-joined SkyTemple ground patrols. Native212c70 projects authored
/// seeds before strict layer/path validation. Global4.8 templates and spawn placements
/// stay unchanged. Initialization uses the proved temporary-NPC bounds; runtime
/// movement owns live bounds. Retained4.8 formation ownership remains an explicit
/// managed compatibility policy, not native party equivalence.</summary>
internal static class SkyTempleNpcWaypointProjection
{
    internal const string SourceFile = "world_N_WayPoint_1.xml";
    internal const string SourceSha256 = "be4a4bf584b991a421c319354b383d9bbc1ef4417023dfba84504758f3ac927a";
    internal const string MakerFile = "world_N.xml";
    internal const string MakerSha256 = "8acc893380195002b8145814dfdb05048497f91af0c4444552a95a32d2e15a9e";
    internal const string WorldFile = "world.xml";
    internal const string WorldSha256 = "665e57e28365927c45c087bf2280d9802f6dfabbd6f18f64d7488172d9eb8949";
    //103c70->06e870->1b34f0: BC/C0/C4=.5,C8=2,A8=0.
    //Actual raw32c=1/5a0=0x41/1f8=0/8b0=0 produce ordinary-ground,
    //v14/v18/v19=false and selector0. The managed planner consumes those
    //semantic guards and bounds, not an invented raw temporary actor image.
    internal static readonly SkyTempleNpcBody NativePrecomputeBody = new(.5f,.5f,2f,.5f,
        "c51937a5ca410bd182731edfa8beb65f330ca750764798b6fe0d55bfa183a2b9",0);
    private sealed record Binding(string Name,RetailSearchPoint[] Seeds,string LegacyHash,int Pool,
        WalkerGroupType Formation,int[]? Rows,HashSet<int> NpcIds);
    internal sealed record Declaration(int NpcId,string Devname,WalkerTemplate Template);
    internal sealed record SourceSummary(int Spots,int Routes,int RawPoints,int GroupedRoutes);
    private sealed record MapSource(IReadOnlyDictionary<string,RetailSearchPoint[]> Points,
        IReadOnlyDictionary<string,string[]> Owners,IReadOnlyDictionary<string,Binding> Bindings);
    private static readonly Dictionary<int,MapSource> mapSources=[];
    private sealed record Cache(WalkerTemplate Original,WalkerTemplate Prepared,string Scene,string Instance,string Body);
    private static readonly ConditionalWeakTable<Npc,Cache> cache = new();
    private static readonly object sync = new();
    private static readonly Lazy<IReadOnlyDictionary<string,RetailSearchPoint[]>> source = new(() => ReadSource(File.ReadAllBytes(
        Path.Combine(Environment.GetEnvironmentVariable(SkyTempleNpcNavigationAssets.RootEnvironment)
            ?? @"D:\Aion58ServerTesting\Server\Map\Worlds\IDDF2Flying",SourceFile))));

    private static string DirectoryPath => Environment.GetEnvironmentVariable(SkyTempleNpcNavigationAssets.RootEnvironment)
        ?? @"D:\Aion58ServerTesting\Server\Map\Worlds\IDDF2Flying";
    private static readonly Lazy<IReadOnlyDictionary<string,string[]>> makers = new(() => ReadMakers(File.ReadAllBytes(Path.Combine(DirectoryPath,MakerFile))));

    /// <summary>Call after actual DataManager/profile preparation and before activation.
    /// Static identity admission covers every60 spot/56 route, not current geometry success.</summary>
    internal static void ValidateSource()
    {
        ValidateWorldSource();
        var profiles=SkyTempleNpcProfileCatalog.GetPreparedProfiles();
        if(profiles.Count!=25)throw new InvalidDataException("Waypoint source requires the complete25-profile package");
        var declarations=new List<Declaration>();
        foreach(var profile in profiles)
        foreach(var group in DataManager.SPAWNS_DATA.GetSpawnsForNpc(SkyTempleNpcProfileCatalog.MapId,profile.NpcId))
        foreach(var spot in group.GetSpawnTemplates())
        {
            string? id=spot.GetWalkerId();if(id is null)continue;
            var template=DataManager.WALKER_DATA.GetWalkerTemplate(id)
                ?? throw new InvalidDataException($"Required waypoint template missing: NPC{profile.NpcId}/{id}");
            if(spot.GetRandomWalkRange()!=0||spot.IsAerialSpawn())
                throw new InvalidDataException($"Unsupported current random/aerial waypoint owner: NPC{profile.NpcId}/{id}");
            declarations.Add(new(profile.NpcId,profile.Devname,template));
        }
        var joined=Join(declarations,source.Value,makers.Value,out var summary);
        if(summary!=new SourceSummary(60,56,972,2))
            throw new InvalidDataException($"Waypoint cohort source coverage differs: {summary}");
        lock(sync){mapSources[320050000]=new(source.Value,makers.Value,joined);}
    }

    /// <summary>Same strict source/legacy ownership join for each explicitly selected
    /// map. Unselected flying/special routes are not silently converted to ground.
    /// A selected unsupported route or unknown scene region blocks readiness.</summary>
    internal static void ValidateSource(SkyTempleNpcNavigationAssets assets,IReadOnlyCollection<SkyTempleNpcProfile> profiles)
    {
        var d=assets.Definition??throw new InvalidDataException("Generic waypoint admission requires a map definition");
        if(profiles.Count is <1 or >10_000)throw new InvalidDataException("Missing/bounded map cohort profiles");
        byte[] waypoints=d.Asset("waypoints").Read(d.Directory,16_000_000),maker=d.Asset("makers").Read(d.Directory,32_000_000),
            actual=d.Asset("world").Read(d.Directory,32_000_000);
        var points=ReadMapPoints(ReadXml(waypoints,16_000_000,"way_point_list"));
        var ownerRoot=ReadXml(maker,32_000_000,"world");var owners=OwnersOf(ownerRoot);
        var world=ReadXml(actual,32_000_000,"world");var roots=world.Elements("way_point_list").ToArray();
        if(roots.Length!=1)throw new InvalidDataException("Map World.XML must own exactly one waypoint list");
        var fullPoints=ReadMapPoints(roots[0]);var fullOwners=OwnersOf(world);
        if(points.Count!=fullPoints.Count||points.Any(p=>!fullPoints.TryGetValue(p.Key,out var other)||!p.Value.SequenceEqual(other))||
            owners.Count!=fullOwners.Count||owners.Any(p=>!fullOwners.TryGetValue(p.Key,out var other)||!p.Value.SequenceEqual(other)))
            throw new InvalidDataException("Map World.XML differs from exact split waypoint/maker sources: "+d.MapId);
        var declarations=new List<Declaration>();
        foreach(var profile in profiles)
        foreach(var group in DataManager.SPAWNS_DATA.GetSpawnsForNpc(d.MapId,profile.NpcId))
        foreach(var spot in group.GetSpawnTemplates())
        {
            string? id=spot.GetWalkerId();if(id is null)continue;
            var template=DataManager.WALKER_DATA.GetWalkerTemplate(id)??throw new InvalidDataException($"Missing map{d.MapId} waypoint {id}");
            if(spot.GetRandomWalkRange()!=0||spot.IsAerialSpawn())throw new InvalidDataException($"Unsupported random/aerial waypoint NPC{profile.NpcId}/{id}");
            declarations.Add(new(profile.NpcId,profile.Devname,template));
        }
        var bindings=declarations.Count==0?new Dictionary<string,Binding>(StringComparer.Ordinal):Join(declarations,points,owners,out _);
        foreach(var pair in bindings)
        {
            var seeds=pair.Value.Seeds;
            for(int i=0;i<seeds.Length;i++)
            {
                var a=seeds[i];var b=seeds[(i+1)%seeds.Length];
                if(!d.IsKnown(a)||d.UnknownRegions.Any(r=>r.X*16<=System.Math.Max(a.X,b.X)&&
                    (r.X+1)*16>=System.Math.Min(a.X,b.X)&&r.Y*16<=System.Math.Max(a.Y,b.Y)&&(r.Y+1)*16>=System.Math.Min(a.Y,b.Y)))
                    throw new InvalidDataException($"Map{d.MapId} patrol {pair.Key}/{i} intersects explicit UNKNOWN scene coverage");
            }
        }
        lock(sync){mapSources[d.MapId]=new(points,owners,bindings);}
    }

    private static XElement ReadXml(byte[] bytes,int max,string rootName)
    {
        using var input=new MemoryStream(bytes,false);
        using var reader=XmlReader.Create(input,new XmlReaderSettings {DtdProcessing=DtdProcessing.Ignore,XmlResolver=null,
            MaxCharactersInDocument=max,MaxCharactersFromEntities=0});
        var root=XDocument.Load(reader).Root;
        if(root?.Name!=rootName)throw new InvalidDataException("Unexpected source XML root: "+rootName);return root;
    }
    private static IReadOnlyDictionary<string,RetailSearchPoint[]> ReadMapPoints(XElement root)
    {
        var output=new Dictionary<string,RetailSearchPoint[]>(StringComparer.Ordinal);
        var names=new HashSet<string>(StringComparer.Ordinal);
        foreach(var row in root.Elements("way_point"))
        {
            var ns=row.Elements("name").ToArray();
            if(ns.Length!=1||ns[0].HasElements||string.IsNullOrWhiteSpace(ns[0].Value)||!names.Add(ns[0].Value)||row.Elements("points").Count()!=1)
                throw new InvalidDataException("Malformed/duplicate native map waypoint");
            // No policy for an explicit flying/special path is inferred. A selected
            // legacy route cannot match this omitted unsupported source owner.
            if(row.Elements("fly_path").Any())continue;
            var ps=row.Element("points")!.Elements("data").ToArray();
            if(ps.Length is <1 or >1024||output.Count>=10_000)throw new InvalidDataException("Native waypoint count exceeds managed support");
            float Scalar(XElement p,string field)
            {
                var xs=p.Elements(field).ToArray();if(xs.Length!=1||xs[0].HasElements)throw new InvalidDataException("Map waypoint scalar differs");
                float v=float.Parse(xs[0].Value,NumberStyles.Float,CultureInfo.InvariantCulture);
                if(!float.IsFinite(v))throw new InvalidDataException("Nonfinite map waypoint");return v;
            }
            output.Add(ns[0].Value,ps.Select(p=>new RetailSearchPoint(Scalar(p,"x"),Scalar(p,"y"),Scalar(p,"z"))).ToArray());
        }
        return output;
    }

    internal static SourceSummary ValidateDeclarations(IReadOnlyList<Declaration> declarations,
        IReadOnlyDictionary<string,RetailSearchPoint[]> points,IReadOnlyDictionary<string,string[]> owners)
    {Join(declarations,points,owners,out var summary);return summary;}

    private static IReadOnlyDictionary<string,Binding> Join(IReadOnlyList<Declaration> declarations,
        IReadOnlyDictionary<string,RetailSearchPoint[]> points,IReadOnlyDictionary<string,string[]> owners,out SourceSummary summary)
    {
        if(declarations.Count is <=0 or >1000)throw new InvalidDataException("Missing/oversized waypoint declarations");
        var result=new Dictionary<string,Binding>(StringComparer.Ordinal);
        foreach(var row in declarations)
        {
            if(row is null||row.NpcId<=0||string.IsNullOrWhiteSpace(row.Devname)||row.Template is null)
                throw new InvalidDataException("Incomplete current waypoint owner declaration");
            var binding=Match(row.Template,row.NpcId,row.Devname,points,owners);
            string id=row.Template.GetRouteId();
            if(result.TryGetValue(id,out var previous))
            {
                if(previous.Name!=binding.Name||previous.LegacyHash!=binding.LegacyHash)
                    throw new InvalidDataException("Conflicting duplicate current waypoint definition: "+id);
                previous.NpcIds.Add(row.NpcId);
            }
            else result.Add(id,binding);
        }
        summary=new(declarations.Count,result.Count,result.Values.Sum(b=>b.Seeds.Length),result.Values.Count(b=>b.Pool==2));
        return result;
    }

    private static Binding Match(WalkerTemplate template,int npc,string devname,
        IReadOnlyDictionary<string,RetailSearchPoint[]> points,IReadOnlyDictionary<string,string[]> owners)
    {
        ValidateMetadata(template);
        var steps=template.GetRouteSteps();
        var matches=points.Where(pair=>pair.Value.Length==steps.Count&&pair.Value.Select((p,i)=>p.IsFinite&&
            SourceXYMatches(p,steps[i])).All(x=>x)).ToArray();
        if(matches.Length!=1)throw new InvalidDataException($"Required waypoint {template.GetRouteId()} has {matches.Length} ordered source matches");
        var match=matches[0];
        if(!owners.TryGetValue(match.Key,out var names)||!names.Contains(devname,StringComparer.Ordinal))
            throw new InvalidDataException($"Waypoint NPC/member ownership differs: NPC{npc}/{devname}/{match.Key}");
        return new(match.Key,match.Value,LegacyHash(template),template.GetPool(),template.GetType_(),template.GetRows()?.ToArray(),[npc]);
    }

    /// <summary>False/null is outside the enabled cohort. Every enabled cohort route
    /// requires an admitted source; false/nonempty must abort/reconsider with no fallback.</summary>
    internal static bool TryPrepare(Npc owner,WalkerTemplate template,out WalkerTemplate? prepared,out string? failure)
    {
        prepared=null;failure=null;
        if(!SkyTempleNpcProfileCatalog.TryGet(owner,out var profile))return false;
        try
        {
            if(template is null)throw new InvalidDataException("Null required waypoint template");
            lock(sync)
            {
                var assets=SkyTempleNpcNavigationAssets.For(owner);
                if(!mapSources.TryGetValue(owner.GetWorldId(),out var map))
                {
                    if(assets.Definition is null)ValidateSource();
                    else ValidateSource(assets,RetailNpcCohortCatalog.GetPreparedProfiles(owner.GetWorldId()));
                    map=mapSources[owner.GetWorldId()];
                }
                if(!map.Bindings.TryGetValue(template.GetRouteId()??"",out var binding)||!binding.NpcIds.Contains(profile.NpcId))
                    throw new InvalidDataException($"Required cohort waypoint is not source-admitted: NPC{profile.NpcId}/{template.GetRouteId()}");
                var spawn=owner.GetSpawn()??throw new InvalidDataException("Waypoint owner has no spawn");
                if(spawn.GetWalkerId()!=template.GetRouteId())throw new InvalidDataException("Waypoint spawn/route identity differs");
                var scene=assets.CurrentScene(owner);
                var body=new SkyTempleNpcBody(profile.Bounds.Front,profile.Bounds.Side,profile.Bounds.Upper,
                    profile.Bounds.HorizontalRadius,profile.SourceRecordSha256,profile.NpcId);
                var original=template;
                if(cache.TryGetValue(owner,out var prior))
                {
                    if(ReferenceEquals(template,prior.Prepared))original=prior.Original;
                    if(ReferenceEquals(original,prior.Original)&&prior.Scene==scene.Declaration.SceneRevision&&
                        prior.Instance==scene.Declaration.InstanceIdentity&&prior.Body==body.SourceHash)
                    {RequireLegacy(original,binding);prepared=prior.Prepared;return true;}
                }
                RequireLegacy(original,binding);
                var result=PrepareBound(assets,scene,body,owner.GetWorldId()+"/waypoint/"+owner.GetObjectId(),original,binding);
                if(result.Template is null){failure=result.Failure;return false;}
                prepared=result.Template;cache.Remove(owner);cache.Add(owner,new(original,prepared,
                    scene.Declaration.SceneRevision,scene.Declaration.InstanceIdentity,body.SourceHash));return true;
            }
        }
        catch(Exception ex) when(ex is InvalidDataException or IOException or UnauthorizedAccessException or XmlException or
            FormatException or OverflowException or ArgumentException)
        {failure="SkyTemple waypoint preparation: "+ex.Message;return false;}
    }

    internal sealed record Result(WalkerTemplate? Template,string? Failure,int? FailedIndex=null,
        RetailSearchPoint? ProjectedPoint=null,RetailSurfaceCandidates? Candidates=null)
    {internal bool Success=>Template is not null&&Failure is null;}

    internal static Result Prepare(SkyTempleNpcNavigationAssets assets,RetailCollisionScene scene,SkyTempleNpcBody body,
        string actor,WalkerTemplate template,IReadOnlyDictionary<string,RetailSearchPoint[]> points,string devname)
    {
        try
        {
            IReadOnlyDictionary<string,string[]> owners=assets.Definition is {} definition
                ?OwnersOf(ReadXml(definition.Asset("makers").Read(definition.Directory,32_000_000),32_000_000,"world")):makers.Value;
            return PrepareBound(assets,scene,body,actor,template,Match(template,body.NpcId,devname,points,owners));
        }
        catch(Exception ex) when(ex is InvalidDataException or ArgumentException or OverflowException)
        {return new(null,"SkyTemple waypoint preparation: "+ex.Message);}
    }

    private static Result PrepareBound(SkyTempleNpcNavigationAssets assets,RetailCollisionScene scene,SkyTempleNpcBody body,
        string actor,WalkerTemplate template,Binding binding)
    {
        try
        {
            RequireLegacy(template,binding);var seeds=binding.Seeds;
            //Native validates with a fresh default NPC, not the actual template's
            //large body. Runtime Find/Follow still owns each live actor's bounds.
            var planner=new SkyTempleNpcRoutePlanner(assets,scene,NativePrecomputeBody,actor+"/native-precompute");
            var projected=new List<RetailSearchPoint>(seeds.Length);
            for(int i=0;i<seeds.Length;i++)
            {
                var seed=seeds[i];var legacy=template.GetRouteStep(i);
                if(!seed.IsFinite || MathF.Abs(seed.X-legacy.GetX())>=.01f || MathF.Abs(seed.Y-legacy.GetY())>=.01f)
                    throw new InvalidDataException($"Authored waypoint order/XY differs at {i}");
                //212eae passes the authored XYZ directly to eligible-ground2f2600;
                //there is no height inflation, nearest-layer loop or legacy closestCollision.
                var q=RetailCollisionQuery.ProjectGround(scene,scene.Stamp,seed,SkyTempleNpcNavigationAssets.QueryLimits);
                if(q.Status!=RetailCollisionStatus.Computed||q.NativeResult!=1||q.Point is not {} p)
                    return new(null,$"Waypoint {i} eligible-ground rejected: {q.Status}/{q.NativeResult}");
                projected.Add(p);
            }
            //212f0f/13 retain count-1 in RDX;212f62 resizes only for squared XYZ<1.
            if(projected.Count>2&&Squared(projected[0],projected[^1])<1f)projected.RemoveAt(projected.Count-1);
            for(int i=0;i<projected.Count;i++)
            {
                var lookup=assets.Surfaces.EnumerateSurfaceCandidates(projected[i].X,projected[i].Y,projected[i].Z);
                if(lookup.Status!=RetailSurfaceQueryStatus.Complete||!lookup.Candidates.Any(c=>c.Status==RetailSurfaceCandidateStatus.WithinTolerance))
                    return new(null,$"Waypoint {i} has no native path layer: point={projected[i]}; lookup={lookup.Status}; "+
                        string.Join(";",lookup.Candidates.Select(c=>$"layer={c.Surface.Layer},type={c.Surface.Node.Type},status={c.Status},height={c.Height.Metres}")),
                        i,projected[i],lookup);
            }
            for(int i=0;i<projected.Count;i++)
            {
                var a=projected[i];var b=projected[(i+1)%projected.Count];
                if(Squared(a,b)>=9984f)return new(null,$"Waypoint edge {i} exceeds native precompute bound");
                if(MathF.Abs(a.X-b.X)<.0001f&&MathF.Abs(a.Y-b.Y)<.0001f&&MathF.Abs(a.Z-b.Z)<.0001f)continue;
                var path=planner.Find(a,b,actor+"/waypoint-edge/"+i);
                if(!path.Success||path.Route!.Goal!=b)
                    return new(null,$"Waypoint edge {i} failed temporary-NPC adjacent validation: {path.Failure}");
            }
            var clone=new WalkerTemplate(template.GetRouteId()) { pool=template.GetPool(),formation=template.GetType_(),
                loopType=template.GetLoopType(),routeStepList=projected.Select((p,i)=>
                new RouteStep(p.X,p.Y,p.Z,template.GetRouteStep(i).GetRestTime())).ToList() };
            clone.AfterUnmarshal();return new(clone,null);
        }
        catch(Exception ex) when(ex is InvalidDataException or ArgumentException or OverflowException)
        {return new(null,"SkyTemple waypoint preparation: "+ex.Message);}
    }

    private static float Squared(RetailSearchPoint a,RetailSearchPoint b)
    {float x=Sub(a.X,b.X),y=Sub(a.Y,b.Y),z=Sub(a.Z,b.Z);return Add(Add(Mul(y,y),Mul(x,x)),Mul(z,z));}
    private static bool SourceXYMatches(RetailSearchPoint native,RouteStep legacy)
    {double x=(double)native.X-legacy.GetX(),y=(double)native.Y-legacy.GetY();return x*x+y*y<.0001;}

    private static void ValidateMetadata(WalkerTemplate template)
    {
        var steps=template.GetRouteSteps();
        // Explicit managed4.8 formation transport, not native party equivalence.
        // Positive source-joined pools retain their actual row ownership unchanged.
        bool group=template.GetPool() is >=2 and <=64&&template.GetType_()==WalkerGroupType.SQUARE&&
            template.GetRows() is {Length:>=1 and <=64} rows&&rows.All(r=>r>0)&&rows.Sum()==template.GetPool();
        bool point=template.GetPool()==1&&template.GetType_()==WalkerGroupType.POINT&&template.GetRows() is null;
        if(string.IsNullOrWhiteSpace(template.GetRouteId())||steps is null||steps.Count is <3 or >1024||
            (!group&&!point)||template.GetLoopType()!=WalkerTemplate.LoopType.NORMAL)
            throw new InvalidDataException("Unsupported legacy waypoint metadata");
        for(int i=0;i<steps.Count;i++)
        {
            var p=steps[i];
            if(p is null||!float.IsFinite(p.GetX())||!float.IsFinite(p.GetY())||!float.IsFinite(p.GetZ())||
                p.GetRestTime()!=0||p.GetStepIndex()!=i||p.IsLastStep()!=(i==steps.Count-1))
                throw new InvalidDataException("Unsupported waypoint scalar/pause/index metadata");
        }
    }
    private static void RequireLegacy(WalkerTemplate template,Binding binding)
    {
        ValidateMetadata(template);
        if(LegacyHash(template)!=binding.LegacyHash||template.GetPool()!=binding.Pool||template.GetType_()!=binding.Formation||
            !(template.GetRows()??[]).SequenceEqual(binding.Rows??[]))
            throw new InvalidDataException("Legacy waypoint source/order/rest identity changed after source admission");
    }
    private static string LegacyHash(WalkerTemplate template)
    {
        var steps=template.GetRouteSteps();byte[] raw=new byte[checked(steps.Count*16)];
        for(int i=0;i<steps.Count;i++)
        {
            var p=steps[i];
            BinaryPrimitives.WriteSingleLittleEndian(raw.AsSpan(i*16),p.GetX());
            BinaryPrimitives.WriteSingleLittleEndian(raw.AsSpan(i*16+4),p.GetY());
            BinaryPrimitives.WriteSingleLittleEndian(raw.AsSpan(i*16+8),p.GetZ());
            BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(i*16+12),p.GetRestTime());
        }
        return Convert.ToHexStringLower(SHA256.HashData(raw));
    }

    internal static IReadOnlyDictionary<string,RetailSearchPoint[]> ReadSource(ReadOnlySpan<byte> bytes)
    {
        if(bytes.Length is <=0 or >1_000_000||Convert.ToHexStringLower(SHA256.HashData(bytes))!=SourceSha256)
            throw new InvalidDataException("Waypoint XML byte identity differs");
        using var input=new MemoryStream(bytes.ToArray(),false);
        using var reader=XmlReader.Create(input,new XmlReaderSettings {DtdProcessing=DtdProcessing.Ignore,XmlResolver=null,
            MaxCharactersInDocument=1_000_000,MaxCharactersFromEntities=0});
        var root=XDocument.Load(reader).Root;
        if(root?.Name!="way_point_list")throw new InvalidDataException("Unexpected waypoint XML root");
        var output=new Dictionary<string,RetailSearchPoint[]>(StringComparer.Ordinal);
        static float Scalar(XElement p,string name)
        {
            var xs=p.Elements(name).ToArray();
            if(xs.Length!=1||xs[0].HasElements)throw new InvalidDataException("Missing/duplicate waypoint scalar "+name);
            float v=float.Parse(xs[0].Value,NumberStyles.Float,CultureInfo.InvariantCulture);
            if(!float.IsFinite(v))throw new InvalidDataException("Nonfinite waypoint");return v;
        }
        foreach(var row in root.Elements("way_point"))
        {
            var names=row.Elements("name").ToArray();
            if(names.Length!=1||string.IsNullOrWhiteSpace(names[0].Value)||names[0].HasElements||
                row.Elements("fly_path").Any()||row.Elements("points").Count()!=1)
                throw new InvalidDataException("Reviewed named ground waypoint shape differs");
            var ps=row.Element("points")!.Elements("data").ToArray();
            if(ps.Length is <3 or >1024)throw new InvalidDataException("Authored waypoint count differs");
            if(!output.TryAdd(names[0].Value,ps.Select(p=>new RetailSearchPoint(Scalar(p,"x"),Scalar(p,"y"),Scalar(p,"z"))).ToArray()))
                throw new InvalidDataException("Duplicate native waypoint name");
        }
        if(output.Count!=57)throw new InvalidDataException("Complete native waypoint source count differs");
        return output;
    }

    internal static IReadOnlyDictionary<string,string[]> ReadMakers(ReadOnlySpan<byte> bytes)
    {
        if(bytes.Length is <=0 or >2_000_000||Convert.ToHexStringLower(SHA256.HashData(bytes))!=MakerSha256)
            throw new InvalidDataException("Waypoint maker XML byte identity differs");
        using var input=new MemoryStream(bytes.ToArray(),false);
        using var reader=XmlReader.Create(input,new XmlReaderSettings {DtdProcessing=DtdProcessing.Ignore,XmlResolver=null,
            MaxCharactersInDocument=2_000_000,MaxCharactersFromEntities=0});
        var root=XDocument.Load(reader).Root;
        if(root is null||root.Name!="world")throw new InvalidDataException("Unexpected waypoint maker root");
        return OwnersOf(root);
    }

    private static IReadOnlyDictionary<string,string[]> OwnersOf(XElement root)
    {
        var refs=new Dictionary<string,HashSet<string>>(StringComparer.Ordinal);
        foreach(var row in root.Descendants().Where(x=>x.Name=="npc"||x.Name=="party"))
        {
            var paths=row.Elements("way_point_name").ToArray();if(paths.Length==0)continue;
            if(paths.Length!=1||string.IsNullOrWhiteSpace(paths[0].Value))throw new InvalidDataException("Malformed maker waypoint reference");
            if(!refs.TryGetValue(paths[0].Value,out var owners))refs.Add(paths[0].Value,owners=new(StringComparer.Ordinal));
            if(row.Name=="npc")
            {
                var names=row.Elements("name").ToArray();if(names.Length!=1)throw new InvalidDataException("Malformed NPC maker identity");
                owners.Add(names[0].Value);
            }
            else foreach(var member in row.Elements("members"))
            {
                var names=member.Elements("name").ToArray();if(names.Length!=1)throw new InvalidDataException("Malformed party member identity");
                owners.Add(names[0].Value);
            }
        }
        return refs.ToDictionary(x=>x.Key,x=>x.Value.Order(StringComparer.Ordinal).ToArray(),StringComparer.Ordinal);
    }

    /// <summary>World::Load22c740 opens World.XML. The split files are retained exact
    /// source crosschecks, not silently substituted loader authority or spawn imports.</summary>
    internal static void ValidateWorldSource()
    {
        byte[] bytes=File.ReadAllBytes(Path.Combine(DirectoryPath,WorldFile));
        if(bytes.Length is <=0 or >2_000_000||Convert.ToHexStringLower(SHA256.HashData(bytes))!=WorldSha256)
            throw new InvalidDataException("Actual World.XML source byte identity differs");
        using var input=new MemoryStream(bytes,false);
        using var reader=XmlReader.Create(input,new XmlReaderSettings {DtdProcessing=DtdProcessing.Ignore,XmlResolver=null,
            MaxCharactersInDocument=2_000_000,MaxCharactersFromEntities=0});
        var world=XDocument.Load(reader).Root;
        if(world is null||world.Name!="world"||world.Elements("way_point_list").Count()!=1)
            throw new InvalidDataException("Actual World.XML waypoint root differs");
        var rows=world.Element("way_point_list")!.Elements("way_point").ToArray();
        if(rows.Length!=source.Value.Count)throw new InvalidDataException("Actual World.XML source count differs");
        var names=new HashSet<string>(StringComparer.Ordinal);
        foreach(var row in rows)
        {
            var ns=row.Elements("name").ToArray();
            if(ns.Length!=1||!names.Add(ns[0].Value)||!source.Value.TryGetValue(ns[0].Value,out var expected)||
                row.Elements("points").Count()!=1||row.Elements("fly_path").Any())
                throw new InvalidDataException("Actual World.XML named ground path differs");
            var points=row.Element("points")!.Elements("data").ToArray();
            if(points.Length!=expected.Length)throw new InvalidDataException("Actual World.XML point count differs");
            for(int i=0;i<points.Length;i++)
            {
                float Scalar(string field)
                {
                    var xs=points[i].Elements(field).ToArray();
                    if(xs.Length!=1||xs[0].HasElements)throw new InvalidDataException("Actual World.XML scalar differs");
                    return float.Parse(xs[0].Value,NumberStyles.Float,CultureInfo.InvariantCulture);
                }
                if(new RetailSearchPoint(Scalar("x"),Scalar("y"),Scalar("z"))!=expected[i])
                    throw new InvalidDataException("Actual World.XML ordered XYZ differs from split source");
            }
        }
        var owners=OwnersOf(world);
        if(owners.Count!=makers.Value.Count||owners.Any(p=>!makers.Value.TryGetValue(p.Key,out var other)||!p.Value.SequenceEqual(other)))
            throw new InvalidDataException("Actual World.XML NPC/member source ownership differs");
    }
}
