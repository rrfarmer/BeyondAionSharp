using System.Security.Cryptography;
using System.Buffers.Binary;
using Aion.GameServer.Retail;
using Aion.GameServer.Dataholders;
using Aion.GameServer.GeoEngine.Scene;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World.Geo;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.GeoEngine.RetailPath;

/// <summary>Source-pinned retail map packages. The original SkyTemple API is retained.
/// Managed placeable activity is read from the same instance geometry used by the4.8 server.
/// Native heap/TLS/current-memory receipts are deliberately not manufactured.</summary>
internal sealed class SkyTempleNpcNavigationAssets
{
    internal const string RootEnvironment = "AION_SKYTEMPLE_RETAIL_MAP_ROOT";
    internal const string AssetRevision = "e59d0a7df72c55edb4aa6575b38d8a5c9878eefd9a960b6fd25458d21e069be5";
    internal const string Mapping = "SkyTemple-managed-ground-v1";
    private static readonly Lazy<SkyTempleNpcNavigationAssets> configured = new(() => Load(
        Environment.GetEnvironmentVariable(RootEnvironment) ?? @"D:\Aion58ServerTesting\Server\Map\Worlds\IDDF2Flying"));
    private static readonly AsyncLocal<SkyTempleNpcNavigationAssets?> testScope=new();
    private static IReadOnlyDictionary<int,SkyTempleNpcNavigationAssets> registry=new Dictionary<int,SkyTempleNpcNavigationAssets>();
    internal static SkyTempleNpcNavigationAssets Configured => testScope.Value??configured.Value;
    internal static SkyTempleNpcNavigationAssets For(Npc owner)
    {
        if(testScope.Value is {} scoped&&scoped.MapId==owner.GetWorldId())return scoped;
        if(Volatile.Read(ref registry).TryGetValue(owner.GetWorldId(),out var assets))return assets;
        if(owner.GetWorldId()==320050000)return Configured;
        throw new InvalidDataException("No source-admitted retail navigation map: "+owner.GetWorldId());
    }
    internal static void InitializeCohorts(IReadOnlyList<RetailNpcMapDefinition> maps)
    {
        if(maps.Count is <1 or >16||maps.Select(m=>m.MapId).Distinct().Count()!=maps.Count)
            throw new InvalidDataException("Retail navigation map registry must be nonempty, bounded and unique");
        var next=new Dictionary<int,SkyTempleNpcNavigationAssets>();
        foreach(var definition in maps)
        {
            var assets=Load(definition);
            SkyTempleNpcWaypointProjection.ValidateSource(assets,RetailNpcCohortCatalog.GetPreparedProfiles(definition.MapId));
            next.Add(definition.MapId,assets);
        }
        Volatile.Write(ref registry,next);
    }
    internal static void ValidateLoadedMaps()
    {foreach(var assets in Volatile.Read(ref registry).Values){assets.ValidateLoadedMap();assets.ValidateOrigins();}}
    internal static void Initialize()
    {
        _=Configured;
        SkyTempleNpcWaypointProjection.ValidateSource();
    }
    // Tests still require Load's actual source-pinned bytes. This scoped owner never
    // invents geometry, changes the production singleton or publishes the cohort gate.
    internal static IDisposable InstallForTests(SkyTempleNpcNavigationAssets assets)
    {var previous=testScope.Value;testScope.Value=assets;return new TestScope(previous);}
    private sealed class TestScope(SkyTempleNpcNavigationAssets? previous):IDisposable
    {
        private bool disposed;
        public void Dispose(){if(!disposed){testScope.Value=previous;disposed=true;}}
    }
    internal RetailPathData Path { get; }
    internal RetailHeightMap40Data Terrain { get; }
    internal RetailCollisionPreparedData Collision { get; }
    internal RetailPathSurfaceQuery Surfaces { get; }
    internal RetailCollisionSources CollisionSources { get; }
    internal RetailNpcMapDefinition? Definition {get;}
    internal int MapId=>Definition?.MapId??320050000;
    internal string Devname=>Definition?.Devname??"IDDF2Flying";
    internal string Revision=>Definition?.Asset("path").Sha256??AssetRevision;
    internal string MappingIdentity=>Definition is null?Mapping:"Retail-map-partial-coverage-v1";
    internal int WidthMetres=>Definition?.WidthMetres??512;
    internal int HeightMetres=>Definition?.HeightMetres??512;
    internal RetailSearchWorldBounds Bounds =>new(new(0,0,-10240),new(WidthMetres,HeightMetres,10240));
    internal RetailPathSurfaceQueryOptions SurfaceOptions { get; } = new();
    internal static readonly RetailCollisionQueryLimits QueryLimits = new(4096,100_000,1_000_000,1024,1_000_000);
    internal static readonly RetailMovementLimits MovementLimits = new() { MaxSteps=4096, MaxOperations=1_000_000 };
    internal static readonly RetailNpcMovementFpEnvironment Floating = new(RetailGroundPathSearch.SupportedNpcBinarySha256,
        RetailNpcNativeHeading.ProofSha256,true,true,false,false,true,
        "Managed binary32/RNE arithmetic contract; independently proved native scalar algorithm, not observed native memory",false);
    private DespawnableNode[]? loadedPlaceables;
    private readonly Dictionary<int,(string State,RetailCollisionScene Scene)> scenes=[];
    private SkyTempleNpcNavigationAssets(RetailPathData path,RetailHeightMap40Data terrain,
        RetailCollisionPreparedData collision,RetailCollisionSources sources,RetailNpcMapDefinition? definition=null)
    {Path=path;Terrain=terrain;Collision=collision;CollisionSources=sources;Definition=definition;Surfaces=new(path,terrain,SurfaceOptions);}

    internal static SkyTempleNpcNavigationAssets Load(RetailNpcMapDefinition definition)
    {
        var d=definition;var limits=d.Limits;
        byte[] Read(string role,long max)=>d.Asset(role).Read(d.Directory,max);
        byte[] path=Read("path",128L*1024*1024),height=Read("heightmap40",128L*1024*1024),
            mesh=Read("mesh_material",128L*1024*1024),collision=Read("collision",256L*1024*1024);
        // These source files bind the curated geometry comparison/holes to the
        // same4.8 loader inputs. They do not import native placements/content.
        _=Read("legacy_geo",128L*1024*1024);_=Read("legacy_models",128L*1024*1024);_=Read("mapping_evidence",64L*1024*1024);
        var p=RetailPathDataReader.Read(path,new(d.WidthMetres,d.HeightMetres){ExpectedCollisionGuid=d.BuildGuid,
            MaxMetadataBytes=limits.PathMetadataBytes,MaxNodes=2_000_000,MaxSparseRecords=2_000_000,RequireExactEnd=true});
        var h=RetailHeightMap40Reader.Read(height,new(){ExpectedGuid=d.BuildGuid,ExpectedWorldWidthMetres=d.WidthMetres,
            ExpectedWorldHeightMetres=d.HeightMetres,RequireExactEnd=true});
        var c=RetailCollisionDataReader.ReadPair(mesh,collision,new(d.WidthMetres,d.HeightMetres),
            new(mesh.Length,collision.Length,limits.MaxVertices,limits.MaxTriangles,checked(d.WidthMetres/64*(d.HeightMetres/64)),
                limits.MaxBands,limits.MaxReferences,limits.CollisionMetadataBytes,limits.MaxFields,255,true));
        if(!p.Success||!h.Success||!c.Success)throw new InvalidDataException($"Retail map{d.MapId} parse: {p.Error}; {h.Error}; {c.Error}");
        if(p.Data!.BuildGuid!=d.BuildGuid||h.Data!.BuildGuid!=d.BuildGuid||c.Data!.MeshGuid!=d.BuildGuid||c.Data.CollisionGuid!=d.BuildGuid)
            throw new InvalidDataException("Retail map asset GUID disagreement: "+d.MapId);
        var cosine=RetailCollisionCosineSourceProof.Admit(new(RetailCollisionCosineSourceProof.ProofSha256,
            RetailCollisionCosineSourceProof.IndependentReviewSha256,new(true,true,true,true),
            "Approved conditional scalar proof; managed RNE/masked/normal environment"),Mapping);
        var prepared=RetailCollisionMath.Prepare(c.Data,cosine.Receipt,new(limits.MaxReferences,limits.MaxBands,limits.PreparationMetadataBytes));
        if(!prepared.Success)throw new InvalidDataException($"Retail map{d.MapId} preparation: {prepared.Detail}");
        RetailSearchMapAssetIdentity Identity(string role,byte[] bytes)=>new(new(d.Asset(role).Path,d.Asset(role).Sha256,bytes.Length),d.BuildGuid,d.WidthMetres,d.HeightMetres);
        var sources=new RetailCollisionSources(Identity("mesh_material",mesh),Identity("collision",collision),Identity("heightmap40",height),
            new("pinned NPC algorithm",RetailGroundPathSearch.SupportedNpcBinarySha256,RetailGroundPathSearch.SupportedNpcBinaryBytes));
        var result=new SkyTempleNpcNavigationAssets(p.Data,h.Data,prepared.Data!,sources,d);
        result.ValidateUnknownCoverage();return result;
    }

    internal static SkyTempleNpcNavigationAssets Load(string directory)
    {
        static byte[] Read(string root,string name,string hash)
        {
            byte[] bytes=File.ReadAllBytes(System.IO.Path.Combine(root,name));
            if(!Convert.ToHexStringLower(SHA256.HashData(bytes)).Equals(hash,StringComparison.Ordinal))
                throw new InvalidDataException("SkyTemple asset identity mismatch: "+name);
            return bytes;
        }
        byte[] path=Read(directory,"IDDF2Flying-path.dat",AssetRevision);
        byte[] height=Read(directory,"heightmap40.dat","a6bd0f7a3f0d73b869b4e98c66146b1caf523e709bcb158dcc66ab6a9904d674");
        byte[] mesh=Read(directory,"MeshMaterial.Dat","e964645652e44f064ebb84e0588677d36bbf4be472db88a5bd5af30057355977");
        byte[] collision=Read(directory,"Collision.Dat","e5dd2cacc876c03f939fee77610db592e77d8b0322340434f7f961f2d49b7ffc");
        Guid guid=Guid.Parse("ff47ba3c-ac6d-4fbf-a6b1-a7d0945a7045");
        var p=RetailPathDataReader.Read(path,new(512,512){ExpectedCollisionGuid=guid,RequireExactEnd=true});
        var h=RetailHeightMap40Reader.Read(height,new(){ExpectedGuid=guid,ExpectedWorldWidthMetres=512,ExpectedWorldHeightMetres=512,RequireExactEnd=true});
        var c=RetailCollisionDataReader.ReadPair(mesh,collision,new(512,512),
            new(4_000_000,8_000_000,200_000,100_000,64,1_000_000,2_000_000,128L*1024*1024,100,255,true));
        if(!p.Success||!h.Success||!c.Success)throw new InvalidDataException($"SkyTemple asset parse: {p.Error}; {h.Error}; {c.Error}");
        var cosine=RetailCollisionCosineSourceProof.Admit(new(RetailCollisionCosineSourceProof.ProofSha256,
            RetailCollisionCosineSourceProof.IndependentReviewSha256,new(true,true,true,true),
            "Approved conditional native scalar proof under managed RNE/masked/normal arithmetic"),Mapping);
        var prepared=RetailCollisionMath.Prepare(c.Data!,cosine.Receipt,new(1_000_000,1_000_000,128L*1024*1024));
        if(!prepared.Success)throw new InvalidDataException("SkyTemple collision preparation: "+prepared.Detail);
        static RetailSearchMapAssetIdentity Identity(string name,byte[] bytes,Guid? g)=>new(
            new(name,Convert.ToHexStringLower(SHA256.HashData(bytes)),bytes.Length),g,512,512);
        var sources=new RetailCollisionSources(Identity("MeshMaterial.Dat",mesh,c.Data!.MeshGuid),
            Identity("Collision.Dat",collision,c.Data.CollisionGuid),Identity("heightmap40.dat",height,h.Data!.BuildGuid),
            new("pinned NPC algorithm",RetailGroundPathSearch.SupportedNpcBinarySha256,RetailGroundPathSearch.SupportedNpcBinaryBytes));
        return new(p.Data!,h.Data!,prepared.Data!,sources);
    }

    internal RetailSearchSourceReceipts Sources(string actor,string scene)=>new(Path,
        new(new(Definition?.Asset("path").Path??"IDDF2Flying-path.dat",Revision,Path.RawFile.Length),Path.BuildGuid,WidthMetres,HeightMetres),
        Terrain,CollisionSources.HeightMap40,CollisionSources.NativeNpcBinary,CollisionSources.Collision.Asset,
        new(actor,scene,scene,MappingIdentity));

    /// <summary>Complete loaded geometry traversal. Catalog presence alone never means active.
    /// No guessed native state is used: activity1/0 is the explicit managed infrastructure mapping.</summary>
    internal RetailCollisionScene CurrentScene(Npc npc)
    {
        lock(scenes)
        {
        ValidateLoadedMap();
        int instance=npc.GetInstanceId();
        var current=loadedPlaceables!.Select(n=>(Id:n.id,Active:n.IsActive(instance))).ToArray();
        // Entire ordered vector, not a32-bit bitmask that aliases objects32/64.
        string state=string.Concat(current.Select(x=>x.Active?'1':'0'));
        if(scenes.TryGetValue(instance,out var cached)&&cached.State==state)return cached.Scene;
        var scene=Scene(instance,current);scenes[instance]=(state,scene);return scene;
        }
    }

    /// <summary>Run after the existing GeoService.Init and before cohort publication/spawns.
    /// It inspects the already loaded world; it creates no instance and never replaces geometry.</summary>
    internal void ValidateLoadedMap()
    {
        lock(scenes)
        {
            if(loadedPlaceables is not null)return;
            if(Definition is not null&&(DataManager.WORLD_MAPS_DATA.GetTemplate(MapId).GetWorldSize()!=WidthMetres||WidthMetres!=HeightMetres))
                throw new InvalidDataException("Loaded host map bounds differ from source-admitted retail dimensions: "+MapId);
            Node map;
            try{map=GeoService.GetInstance().GetMap(MapId);}
            catch(KeyNotFoundException ex){throw new InvalidDataException($"Retail world{MapId} geometry is not loaded.",ex);}
            var validated=Definition is null?ValidatePlaceables(map):ValidatePlaceables(map,Definition.Placeables.Select(p=>p.EditorId).ToArray());
            if(Definition is {} definition)
            foreach(var node in validated)
            {
                string expected=definition.Placeables.Single(p=>p.EditorId==node.id).TransformSha256;
                int geometries=0;
                void Check(Node owner)
                {
                    foreach(var child in owner.GetChildren())
                    {
                        if(child is Geometry geometry)
                        {
                            float[] values=new float[16];geometry.GetWorldMatrix().Get(values,true);byte[] raw=new byte[64];
                            for(int i=0;i<16;i++)BinaryPrimitives.WriteSingleLittleEndian(raw.AsSpan(i*4),values[i]);
                            if(Convert.ToHexStringLower(SHA256.HashData(raw))!=expected)
                                throw new InvalidDataException($"Loaded map{MapId} editor{node.id} placement transform differs from source join");
                            geometries++;
                        }
                        else if(child is Node nested)Check(nested);
                    }
                }
                Check(node);if(geometries==0)throw new InvalidDataException($"Loaded editor{node.id} has no actual mesh geometry");
            }
            if(Definition is {} declared)
                Aion.Commons.Logging.AionLog.For(nameof(SkyTempleNpcNavigationAssets)).LogInformation(
                    "RETAIL_MAP_SCENE_SOURCE_VALIDATED map={Map} devname={Devname} loadedPlaceables={Count} knownNativeFields={Known} unknownRegions={Unknown} unknownEditorIds={Ids} source={Source}",
                    MapId,Devname,validated.Length,declared.Placeables.Count(p=>p.MatchedNative),declared.UnknownRegions.Length,
                    string.Join(",",declared.UnknownRegions.SelectMany(r=>r.ObjectIds).Distinct().Order()),declared.Asset("mapping_evidence").Sha256);
            loadedPlaceables=validated;
        }
    }

    internal static DespawnableNode[] ValidatePlaceables(Node map)
        =>ValidatePlaceables(map,[15,16,17]);
    private static DespawnableNode[] ValidatePlaceables(Node map,IReadOnlyList<int> expected)
    {
        List<DespawnableNode> found=[];
        void Walk(Node node)
        {
            foreach(var child in node.GetChildren())
            {if(child is DespawnableNode d)found.Add(d);if(child is Node n)Walk(n);}
        }
        Walk(map);
        if(found.Count!=expected.Count||found.Any(n=>n.type!=DespawnableNode.DespawnableType.PLACEABLE)||
            !found.Select(n=>n.id).Order().SequenceEqual(expected.Order()))
            throw new InvalidDataException("Loaded dynamic geometry differs from the complete manifest4.8 catalog");
        return found.OrderBy(n=>n.id).ToArray();
    }

    internal RetailCollisionScene Scene(int instance,IReadOnlyList<(int Id,bool Active)> current)
    {
        int[] expected=Definition?.Placeables.Select(p=>p.EditorId).ToArray()??[15,16,17];
        if(current.Count!=expected.Length||!current.Select(x=>x.Id).SequenceEqual(expected))
            throw new InvalidDataException("An explicit complete ordered current manifest-placeable snapshot is required");
        string instanceIdentity=Definition is null?"SkyTemple/"+instance:MapId+"/"+instance;
        string revision=MappingIdentity+":"+(Definition is null?"":MapId+":")+string.Join(",",current.Select(x=>$"{x.Id}={(x.Active?1:0)}"));
        string evidence=Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(revision)));
        var fields=new List<RetailFieldObjectGeometry>();
        foreach(var item in current)
        {
            if(Definition is {} definition&&!definition.Placeables.Single(p=>p.EditorId==item.Id).MatchedNative)continue;
            var obs=new RetailFieldObjectObservation((uint)item.Id,revision,item.Active?1:0,MapId.ToString(System.Globalization.CultureInfo.InvariantCulture),instanceIdentity,
                revision,Revision,"managed-GeoMap-placeables",revision,evidence,
                RetailFieldObjectEvidenceKind.SourceInitializationDeclaration,
                new(RetailFieldObjectGeometry.SupportedClass,RetailFieldObjectGeometry.SupportedConstructor,
                    RetailFieldObjectGeometry.SupportedVtable,RetailGroundPathSearch.SupportedNpcBinarySha256,
                    "b0063b3706f41f66703614518b7c02a5275c51b8177700c6965fdd24c604dfd7"));
            var f=RetailFieldObjectGeometry.Prepare(Collision,obs,new(100_000,1_000_000));
            if(!f.Success)throw new InvalidDataException("SkyTemple field geometry: "+f.Detail);
            fields.Add(f.Geometry!);
        }
        int regionCount=checked(WidthMetres/16*(HeightMetres/16));
        var unknown=Definition?.UnknownRegions.Select(r=>(r.X,r.Y)).ToHashSet()??[];
        var regions=new List<RetailCollisionRegionObservation>(regionCount);
        for(int y=0;y<HeightMetres/16;y++)for(int x=0;x<WidthMetres/16;x++)
        {
            var objects=fields.Where(f=>f.Minimum.X<=(x+1)*16&&f.Maximum.X>=x*16&&
                f.Minimum.Y<=(y+1)*16&&f.Maximum.Y>=y*16).Select(f=>
                    new RetailCollisionSceneObject(f.Observation.EditorId,RetailFieldObjectGeometry.SupportedClass,revision)).ToArray();
            regions.Add(new(x,y,!unknown.Contains((x,y)),revision,evidence,objects));
        }
        var result=RetailCollisionScene.Create(Collision,Terrain,CollisionSources,Bounds,
            new(MapId.ToString(System.Globalization.CultureInfo.InvariantCulture),instanceIdentity,revision,Revision,evidence,
                "Complete managed4.8 activity snapshot; manifest holes remain UNKNOWN, compatibility mapping not native observation",false),
            regions,RetailCollisionMaterialPolicy.FromPinnedFileImage(MappingIdentity),new(regionCount,checked(regionCount*System.Math.Max(1,expected.Length))),fields);
        if(!result.Success)throw new InvalidDataException("SkyTemple current scene: "+result.Detail);
        return result.Scene!;
    }

    private void ValidateUnknownCoverage()
    {
        if(Definition is not {} d)return;
        var holes=d.UnknownRegions.Select(r=>(r.X,r.Y)).ToHashSet();
        void Require(RetailSearchPoint lo,RetailSearchPoint hi,int editor)
        {
            int minX=System.Math.Max(0,(int)System.Math.Floor((lo.X-.0001)/16)),minY=System.Math.Max(0,(int)System.Math.Floor((lo.Y-.0001)/16));
            int maxX=System.Math.Min(WidthMetres/16-1,(int)System.Math.Floor((hi.X+.0001)/16)),maxY=System.Math.Min(HeightMetres/16-1,(int)System.Math.Floor((hi.Y+.0001)/16));
            for(int y=minY;y<=maxY;y++)for(int x=minX;x<=maxX;x++)
                if(!holes.Contains((x,y))||!d.UnknownRegions.Single(r=>r.X==x&&r.Y==y).ObjectIds.Contains(editor))
                    throw new InvalidDataException($"Unmatched geometry editor{editor} is not wholly covered by explicit UNKNOWN regions");
        }
        foreach(var p in d.Placeables.Where(p=>!p.MatchedNative))Require(p.Minimum,p.Maximum,p.EditorId);
        var matched=d.Placeables.Where(p=>p.MatchedNative).Select(p=>p.EditorId).ToHashSet();
        if(matched.Any(id=>!Collision.Raw.Fields.Any(f=>f.Id==(uint)id)))throw new InvalidDataException("Matched native field missing from selected mesh");
        foreach(var field in Collision.Raw.Fields.Where(f=>!matched.Contains(checked((int)f.Id))))
        {
            if(!field.RawKind.Span.SequenceEqual("PlaceableObject"u8)||field.Payload.Length!=16)
                throw new InvalidDataException("Unclosed dynamic class cannot publish partial map coverage");
            var points=new List<RetailSearchPoint>();
            for(int pair=0;pair<2;pair++)
            {
                uint a=BinaryPrimitives.ReadUInt32LittleEndian(field.Payload.Span[(pair*8)..]),z=BinaryPrimitives.ReadUInt32LittleEndian(field.Payload.Span[(pair*8+4)..]);
                if(z<a||z>=Collision.Raw.TriangleCount||z-a>100_000)throw new InvalidDataException("Unmatched dynamic range is outside bounded coverage proof");
                for(uint i=a;i<=z;i++){var t=Collision.Raw.GetTriangle(i);points.Add(t.A);points.Add(t.B);points.Add(t.C);}
            }
            Require(new(points.Min(p=>p.X),points.Min(p=>p.Y),points.Min(p=>p.Z)),
                new(points.Max(p=>p.X),points.Max(p=>p.Y),points.Max(p=>p.Z)),checked((int)field.Id));
        }
    }
    private void ValidateOrigins()
    {
        if(Definition is not {} d)return;
        var failures=new List<string>();
        foreach(var p in RetailNpcCohortCatalog.GetPreparedProfiles(MapId))
        foreach(var group in DataManager.SPAWNS_DATA.GetSpawnsForNpc(MapId,p.NpcId))
        foreach(var spot in group.GetSpawnTemplates())
        {
            var origin=new RetailSearchPoint(spot.GetX(),spot.GetY(),spot.GetZ());
            int range=spot.GetRandomWalkRange();
            if(!d.IsKnown(origin)||d.UnknownRegions.Any(r=>r.X*16<=origin.X+range&&(r.X+1)*16>=origin.X-range&&
                r.Y*16<=origin.Y+range&&(r.Y+1)*16>=origin.Y-range))failures.Add($"NPC{p.NpcId}@{origin}");
            else
            {
                var cell=Collision.Cell((int)origin.X/2,(int)origin.Y/2);
                int tx=(int)origin.X/2,ty=(int)origin.Y/2;
                if(cell.SharedEmptyStorage||Terrain.BytePlane2.Span[ty*Terrain.Stride+tx]>=16)
                    failures.Add($"NPC{p.NpcId}@{origin}: unsupported empty-storage/direct40-selector");
                else
                {
                    var surfaces=Surfaces.EnumerateSurfaceCandidates(origin.X,origin.Y,origin.Z);
                    if(surfaces.Status!=RetailSurfaceQueryStatus.Complete||!surfaces.Candidates.Any(c=>c.Status==RetailSurfaceCandidateStatus.WithinTolerance))
                        failures.Add($"NPC{p.NpcId}@{origin}: native-surface-missing-at-host-spawn (map-version compatibility not verified)");
                }
            }
        }
        if(failures.Count>0)throw new InvalidDataException($"Retail map{MapId} has{failures.Count} unsupported spawn origins: "+string.Join(";",failures.Take(24)));
    }

    /// <summary>Additional host-content admission veto, not a replacement ground
    /// answer. Native movement must not place a4.8 actor under its actual floor.
    /// The70cm bound is the existing native path-surface admission domain. An
    /// incompatible step is rejected; no snapping, floor substitution or retry
    /// through legacy navigation occurs. The approved pilot mapping is unchanged.</summary>
    internal bool HasHostSupport(Npc owner,RetailSearchPoint point)
    {
        if(Definition is null)return true;
        return float.IsFinite(GeoService.GetInstance().GetZ(MapId,point.X,point.Y,point.Z+.7f,point.Z-.7f,owner.GetInstanceId()));
    }
}
