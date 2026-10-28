using System.Globalization;
using System.Xml.Linq;
using Aion.GameServer.GeoEngine.RetailPath;
using Aion.GameServer.Model.Templates.Walker;
using Aion.GameServer.Retail;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class SkyTempleNpcWaypointProjectionTests
{
    private const string Root=@"D:\Aion58ServerTesting\Server\Map\Worlds\IDDF2Flying";
    private const string Profiles=@"C:\Users\ryanf\Documents\Aion-NPC-Discovery\implementation\pilot25\data\sky-temple-cohort.json";
    private static readonly Lazy<(SkyTempleNpcNavigationAssets Assets,IReadOnlyDictionary<string,RetailSearchPoint[]> Source,
        IReadOnlyDictionary<int,SkyTempleNpcProfile> Profiles)> fixture=new(()=>
    {
        string root=Environment.GetEnvironmentVariable(SkyTempleNpcNavigationAssets.RootEnvironment)??Root;
        return(SkyTempleNpcNavigationAssets.Load(root),SkyTempleNpcWaypointProjection.ReadSource(File.ReadAllBytes(
            Path.Combine(root,SkyTempleNpcWaypointProjection.SourceFile))),SkyTempleNpcProfileCatalog.Parse(File.ReadAllBytes(Profiles)));
    });
    public static IEnumerable<object[]> Cases()
    {
        yield return [212761,"D7ABFE03C4A9D9355860974445C0CFDDB94524B4","IDDF2Flying_MPath_Elementalwater1D_30_5",1,13,436.89f,132.17f,221.1f];
        yield return [212781,"BF2444560E4868AA3418B73CE98225421B647519","DF2_C5_Mobpath_G_ShadowstalkerBigD_32_2",5,15,185.81f,254.16f,539.39764f];
        yield return [212775,"AB41A746E0D4068615041B581090CE8C81BED8A2","IDDF2Flying_MPath_ElementalFire2D_31_3",25,31,148.29f,138.55f,568.0241f];
        yield return [212775,"B1235356995D1AC7FD9A9EA9E905F4AB56B0FA3B","NPCPathIDDF2Flying_Mob_Party3",13,14,94.27f,140.98f,578.75415f];
    }
    public static IEnumerable<object[]> SuccessfulCases()=>Cases().Where(r=>(string)r[1]!="B1235356995D1AC7FD9A9EA9E905F4AB56B0FA3B");
    public static IEnumerable<object[]> ChangedHeightCases()=>Cases().Select(r=>new[]{r[0],r[1],r[3],r[7]});
    public static IEnumerable<object[]> LateFailureCases()=>Cases().Select(r=>new[]{r[0],r[1],r[2],r[3]});
    private static string Repo()
    {
        var p=new DirectoryInfo(AppContext.BaseDirectory);
        while(p is not null&&!File.Exists(Path.Combine(p.FullName,"AionServer.slnx")))p=p.Parent;
        return p?.FullName??throw new InvalidDataException("Repository source fixture is unavailable");
    }
    private static WalkerTemplate Legacy(string route)
    {
        var x=XDocument.Load(Path.Combine(Repo(),"game-server","data","static_data","npc_walker","npc_walker.xml"))
            .Root!.Elements("walker_template").Single(x=>(string?)x.Attribute("route_id")==route);
        var t=new WalkerTemplate(route) {pool=int.Parse(x.Attribute("pool")?.Value??"1",CultureInfo.InvariantCulture),
            formation=Enum.Parse<Aion.GameServer.SpawnEngine.WalkerGroupType>(x.Attribute("formation")?.Value??"POINT"),
            rowValues=x.Attribute("rows")?.Value!,loopType=Enum.Parse<WalkerTemplate.LoopType>(x.Attribute("loop_type")?.Value??"NORMAL"),
            routeStepList=x.Elements("routestep").Select(p=>new RouteStep(
            float.Parse(p.Attribute("x")!.Value,CultureInfo.InvariantCulture),float.Parse(p.Attribute("y")!.Value,CultureInfo.InvariantCulture),
            float.Parse(p.Attribute("z")!.Value,CultureInfo.InvariantCulture),int.Parse(p.Attribute("rest_time")?.Value??"0",CultureInfo.InvariantCulture))).ToList()};
        t.AfterUnmarshal();return t;
    }
    private static void RequireAssets()
    {
        string root=Environment.GetEnvironmentVariable(SkyTempleNpcNavigationAssets.RootEnvironment)??Root;
        Skip.IfNot(File.Exists(Path.Combine(root,SkyTempleNpcWaypointProjection.SourceFile))&&File.Exists(Path.Combine(root,SkyTempleNpcWaypointProjection.MakerFile))&&
            File.Exists(Path.Combine(root,SkyTempleNpcWaypointProjection.WorldFile))&&File.Exists(Profiles),
            "The pinned actual SkyTemple assets/profile/source fixture is required; no flat fallback.");
    }
    private static SkyTempleNpcBody Body(int id)
    {
        var p=fixture.Value.Profiles[id];return new(p.Bounds.Front,p.Bounds.Side,p.Bounds.Upper,p.Bounds.HorizontalRadius,p.SourceRecordSha256,id);
    }
    [SkippableTheory]
    [MemberData(nameof(SuccessfulCases))]
    public void ThreeRecordedStaleGoalsPrepareACompleteActorOwnedGroundRoute(int npc,string route,string name,int bad,int count,float x,float y,float z)
    {
        RequireAssets();var f=fixture.Value;var original=Legacy(route);
        var before=original.GetRouteSteps().Select(p=>(p.GetX(),p.GetY(),p.GetZ(),p.GetRestTime(),p.GetStepIndex(),p.IsLastStep())).ToArray();
        Assert.Equal((x,y,z),(before[bad].Item1,before[bad].Item2,before[bad].Item3));
        foreach(bool active in new[]{false,true})
        {
            var scene=f.Assets.Scene(active?28:27,new[]{(15,active),(16,active),(17,active)});
            var result=SkyTempleNpcWaypointProjection.Prepare(f.Assets,scene,Body(npc),"actual-four-patrols",original,f.Source,f.Profiles[npc].Devname);
            Assert.True(result.Success,result.Failure);var prepared=result.Template!;
            Assert.NotSame(original,prepared);Assert.Equal(route,prepared.GetRouteId());Assert.Equal(count,prepared.GetRouteSteps().Count);
            Assert.True(MathF.Abs(prepared.GetRouteStep(bad).GetZ()-z)>.7f,"The recorded unmatched old height must not survive preparation.");
            var planner=new SkyTempleNpcRoutePlanner(f.Assets,scene,Body(npc),"assert-prepared-patrol");
            for(int i=0;i<count;i++)
            {
                var p=prepared.GetRouteStep(i);Assert.NotSame(original.GetRouteStep(i),p);Assert.Equal(i,p.GetStepIndex());
                Assert.Equal(i==count-1,p.IsLastStep());Assert.Equal(0,p.GetRestTime());
                var at=new RetailSearchPoint(p.GetX(),p.GetY(),p.GetZ());Assert.NotNull(planner.Surface(at));
                Assert.Equal(f.Source[name][i].X,p.GetX());Assert.Equal(f.Source[name][i].Y,p.GetY());
            }
            Assert.Equal(before,original.GetRouteSteps().Select(p=>(p.GetX(),p.GetY(),p.GetZ(),p.GetRestTime(),p.GetStepIndex(),p.IsLastStep())).ToArray());
        }
    }
    [SkippableTheory]
    [MemberData(nameof(ChangedHeightCases))]
    public void NonfiniteLegacyZWithholdsTheCloneWithoutMutatingTheSource(int npc,string route,int bad,float z)
    {
        RequireAssets();var f=fixture.Value;var t=Legacy(route);Assert.Equal(z,t.GetRouteStep(bad).GetZ());t.GetRouteStep(bad).SetZ(float.NaN);
        var result=SkyTempleNpcWaypointProjection.Prepare(f.Assets,f.Assets.Scene(29,new[]{(15,false),(16,false),(17,false)}),
            Body(npc),"changed-source",t,f.Source,f.Profiles[npc].Devname);
        Assert.False(result.Success);Assert.Null(result.Template);Assert.Contains("scalar/pause/index",result.Failure);
        Assert.True(float.IsNaN(t.GetRouteStep(bad).GetZ()));
    }
    [SkippableTheory]
    [MemberData(nameof(LateFailureCases))]
    public void AReachedLateProjectionFailureWithholdsTheWholeRoute(int npc,string route,string name,int bad)
    {
        RequireAssets();var f=fixture.Value;var source=f.Source.ToDictionary(k=>k.Key,v=>v.Value.ToArray(),StringComparer.Ordinal);
        source[name][bad]=source[name][bad] with {Z=-11000};
        var result=SkyTempleNpcWaypointProjection.Prepare(f.Assets,f.Assets.Scene(30,new[]{(15,false),(16,false),(17,false)}),
            Body(npc),"late-projection-failure",Legacy(route),source,f.Profiles[npc].Devname);
        Assert.False(result.Success);Assert.Null(result.Template);Assert.Contains($"Waypoint {bad} eligible-ground rejected",result.Failure);
    }
    [SkippableFact]
    public void All57SourceDefinitionsRetainThe75OriginalCasePointsBeforeGroundPreparation()
    {
        RequireAssets();Assert.Equal(57,fixture.Value.Source.Count);
        Assert.Equal(75,Cases().Sum(r=>fixture.Value.Source[(string)r[2]].Length));
        var files=fixture.Value.Source;Assert.Equal(220.100006f,files["IDDF2Flying_MPath_Elementalwater1D_30_5"][1].Z);
        Assert.Equal(553.099976f,files["DF2_C5_Mobpath_G_ShadowstalkerBigD_32_2"][5].Z);
        Assert.Equal(570.429016f,files["IDDF2Flying_MPath_ElementalFire2D_31_3"][25].Z);
        Assert.Equal(580.100525f,files["NPCPathIDDF2Flying_Mob_Party3"][13].Z);
    }
    [SkippableFact]
    public void UnmatchedNpcAndChangedRouteMetadataAreNotSourceCapabilities()
    {
        RequireAssets();var f=fixture.Value;var scene=f.Assets.Scene(31,new[]{(15,false),(16,false),(17,false)});
        var t=Legacy("D7ABFE03C4A9D9355860974445C0CFDDB94524B4");
        var wrongNpc=SkyTempleNpcWaypointProjection.Prepare(f.Assets,scene,Body(212775),"wrong-NPC",t,f.Source,f.Profiles[212775].Devname);
        Assert.False(wrongNpc.Success);Assert.Null(wrongNpc.Template);
        t.loopType=WalkerTemplate.LoopType.NONE;
        var changed=SkyTempleNpcWaypointProjection.Prepare(f.Assets,scene,Body(212761),"changed-metadata",t,f.Source,f.Profiles[212761].Devname);
        Assert.False(changed.Success);Assert.Null(changed.Template);Assert.Contains("metadata",changed.Failure);
    }
    [Fact]
    public void MissingOrChangedSourceBytesCannotBecomeGroundCandidates()
    {
        Assert.Throws<InvalidDataException>(()=>SkyTempleNpcWaypointProjection.ReadSource([]));
        Assert.Throws<InvalidDataException>(()=>SkyTempleNpcWaypointProjection.ReadSource("<way_point_list/>"u8));
        Assert.Throws<InvalidDataException>(()=>SkyTempleNpcWaypointProjection.ReadMakers([]));
        Assert.Throws<InvalidDataException>(()=>SkyTempleNpcWaypointProjection.ReadMakers("<world/>"u8));
    }
    [SkippableFact]
    public void B123LastPointIsARealGroundHitButNativePathNoGroundRejectsTheWholeRoute()
    {
        RequireAssets();var f=fixture.Value;var original=Legacy("B1235356995D1AC7FD9A9EA9E905F4AB56B0FA3B");
        var expected=new RetailSearchPoint(94.266197f,140.984772f,578.75415f);
        Assert.Equal("0x14027d5d0",RetailCollisionQuery.DispatchFunction(RetailCollisionQueryKind.GroundProjection,2));
        foreach(bool active in new[]{false,true})
        {
            var scene=f.Assets.Scene(active?37:36,new[]{(15,active),(16,active),(17,active)});
            var seed=f.Source["NPCPathIDDF2Flying_Mob_Party3"][13];
            Assert.Equal(new RetailSearchPoint(94.266197f,140.984772f,580.100525f),seed);
            var projection=RetailCollisionQuery.ProjectGround(scene,scene.Stamp,seed,SkyTempleNpcNavigationAssets.QueryLimits);
            Assert.Equal(RetailCollisionStatus.Computed,projection.Status);Assert.Equal(1u,projection.NativeResult);
            Assert.Equal(expected,projection.Point);Assert.Empty(projection.ConsumedFieldObjects);
            var result=SkyTempleNpcWaypointProjection.Prepare(f.Assets,scene,Body(212775),"native-invalid-patrol",original,f.Source,f.Profiles[212775].Devname);
            Assert.False(result.Success);Assert.Null(result.Template);Assert.Equal(13,result.FailedIndex);Assert.Equal(expected,result.ProjectedPoint);
            var lookup=Assert.IsType<RetailSurfaceCandidates>(result.Candidates);Assert.Equal(RetailSurfaceQueryStatus.Complete,lookup.Status);
            Assert.Equal(188,lookup.CellX);Assert.Equal(281,lookup.CellY);Assert.Equal(57875,lookup.QueryCentimetres);
            var candidate=Assert.Single(lookup.Candidates);Assert.Equal(0,candidate.Surface.Layer);Assert.Equal(14,candidate.Surface.Node.Type);
            Assert.Equal(339682,candidate.Surface.NodeFileOffset);Assert.Equal(RetailSurfaceCandidateStatus.NoGround,candidate.Status);
        }
    }
    private static List<SkyTempleNpcWaypointProjection.Declaration> Declarations()
    {
        var profiles=fixture.Value.Profiles;
        var xml=XDocument.Load(Path.Combine(Repo(),"game-server","data","static_data","spawns","Instances","320050000_Sky_Temple_Interior.xml"));
        return xml.Root!.Elements("spawn_map").SelectMany(x=>x.Elements("spawn")).Where(x=>profiles.ContainsKey((int)x.Attribute("npc_id")!))
            .SelectMany(group=>group.Elements("spot").Where(p=>p.Attribute("walker_id") is not null).Select(p=>
                new SkyTempleNpcWaypointProjection.Declaration((int)group.Attribute("npc_id")!,profiles[(int)group.Attribute("npc_id")!].Devname,
                    Legacy(p.Attribute("walker_id")!.Value)))).ToList();
    }
    private static IReadOnlyDictionary<string,string[]> Makers()=>SkyTempleNpcWaypointProjection.ReadMakers(File.ReadAllBytes(Path.Combine(
        Environment.GetEnvironmentVariable(SkyTempleNpcNavigationAssets.RootEnvironment)??Root,SkyTempleNpcWaypointProjection.MakerFile)));
    [SkippableFact]
    public void EveryOneOf60SpotsAnd56RoutesHasOneOrderedSourceAndActualNpcOrPartyMemberJoin()
    {
        RequireAssets();SkyTempleNpcWaypointProjection.ValidateWorldSource();
        var declarations=Declarations();var summary=SkyTempleNpcWaypointProjection.ValidateDeclarations(declarations,fixture.Value.Source,Makers());
        Assert.Equal(new SkyTempleNpcWaypointProjection.SourceSummary(60,56,972,2),summary);
        var grouped=declarations.Where(d=>d.Template.GetPool()==2).ToArray();Assert.Equal(4,grouped.Length);
        Assert.All(grouped,d=>Assert.Equal(new[]{2},d.Template.GetRows()));Assert.All(grouped,d=>Assert.Equal(212765,d.NpcId));
        Assert.Equal(56,declarations.Select(d=>d.Template.GetRouteId()).Distinct().Count());
    }
    [SkippableTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void EveryCurrentPatrolIsCompletelyPreparedOrExplicitlyRejectedByItsNativeLayer(bool active)
    {
        RequireAssets();var f=fixture.Value;var scene=f.Assets.Scene(active?40:39,new[]{(15,active),(16,active),(17,active)});
        var rows=Declarations().DistinctBy(d=>d.Template.GetRouteId()).ToArray();
        var rejected=new List<string>();int prepared=0;
        foreach(var row in rows)
        {
            var result=SkyTempleNpcWaypointProjection.Prepare(f.Assets,scene,Body(row.NpcId),
                "all-current-patrols/"+row.Template.GetRouteId(),row.Template,f.Source,row.Devname);
            if(result.Success){prepared++;Assert.NotSame(row.Template,result.Template);continue;}
            rejected.Add(row.Template.GetRouteId()+": "+result.Failure);
        }
        Assert.Equal(55,prepared);
        Assert.StartsWith("B1235356995D1AC7FD9A9EA9E905F4AB56B0FA3B: Waypoint 13 has no native path layer",Assert.Single(rejected));
    }
    [SkippableTheory]
    [InlineData("missing")]
    [InlineData("ambiguous")]
    [InlineData("owner")]
    [InlineData("metadata")]
    [InlineData("pause")]
    [InlineData("order")]
    public void RequiredCurrentPatrolSourceMismatchesCannotSilentlyRetainTheLegacyRoute(string kind)
    {
        RequireAssets();var rows=Declarations();var points=fixture.Value.Source.ToDictionary(p=>p.Key,p=>p.Value.ToArray(),StringComparer.Ordinal);
        var owners=Makers().ToDictionary(p=>p.Key,p=>p.Value.ToArray(),StringComparer.Ordinal);
        var first=rows.Single(r=>r.Template.GetRouteId()=="D7ABFE03C4A9D9355860974445C0CFDDB94524B4");
        const string native="IDDF2Flying_MPath_Elementalwater1D_30_5";
        switch(kind)
        {
            case "missing": points.Remove(native);break;
            case "ambiguous": points.Add("duplicate-source",points[native].ToArray());break;
            case "owner": owners[native]=["OtherNpc"];break;
            case "metadata": first.Template.loopType=WalkerTemplate.LoopType.NONE;break;
            case "pause": first.Template.GetRouteStep(1).restTime=100;break;
            case "order": (first.Template.routeStepList[0],first.Template.routeStepList[1])=(first.Template.routeStepList[1],first.Template.routeStepList[0]);first.Template.AfterUnmarshal();break;
        }
        Assert.Throws<InvalidDataException>(()=>SkyTempleNpcWaypointProjection.ValidateDeclarations(rows,points,owners));
    }

    [SkippableFact]
    public void SourcePrecomputationDoesNotRejectRoutesUsingAnExtraLiveBodyProbe()
    {
        RequireAssets();var f=fixture.Value;var scene=f.Assets.Scene(38,new[]{(15,false),(16,false),(17,false)});
        var template=Legacy("D7ABFE03C4A9D9355860974445C0CFDDB94524B4");var body=Body(212761);
        var ordinary=SkyTempleNpcWaypointProjection.Prepare(f.Assets,scene,body,"same-source",template,f.Source,f.Profiles[212761].Devname);
        var large=SkyTempleNpcWaypointProjection.Prepare(f.Assets,scene,body with {Front=50,Side=50,Upper=50,Radius=50},
            "same-source",template,f.Source,f.Profiles[212761].Devname);
        Assert.True(ordinary.Success,ordinary.Failure);Assert.True(large.Success,large.Failure);
        static (float,float,float)[] Points(WalkerTemplate t)=>t.GetRouteSteps().Select(p=>(p.GetX(),p.GetY(),p.GetZ())).ToArray();
        Assert.Equal(Points(ordinary.Template!),Points(large.Template!));
        var native=SkyTempleNpcWaypointProjection.NativePrecomputeBody;
        Assert.Equal((.5f,.5f,2f,.5f,0),(native.Front,native.Side,native.Upper,native.Radius,native.NpcId));
    }

}
