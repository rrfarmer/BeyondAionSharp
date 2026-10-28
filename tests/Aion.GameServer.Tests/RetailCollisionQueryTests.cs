using Aion.GameServer.GeoEngine.RetailPath;

namespace Aion.GameServer.Tests;

public sealed class RetailCollisionQueryTests
{
    [Theory]
    [InlineData(0,true,true)] [InlineData(1,true,true)] [InlineData(2,true,true)] [InlineData(3,true,true)]
    [InlineData(4,true,true)] [InlineData(5,true,true)] [InlineData(6,true,true)] [InlineData(7,true,true)]
    [InlineData(8,true,false)] [InlineData(9,true,false)] [InlineData(10,true,false)] [InlineData(11,true,false)]
    [InlineData(12,false,false)] [InlineData(13,false,false)] [InlineData(14,false,false)] [InlineData(15,false,false)]
    public void AllSixteenProjectionSelectorsRetainConsumerSpecificTerrainFamilies(byte selector,bool prediction,bool ground)
    {
        var scene=EmptyScene(selector,(_,_)=>128);
        var predicted=RetailCollisionQuery.ProjectPrediction(scene,scene.Stamp,new(.5f,.5f,10),CollisionFixture.QueryLimits);
        var grounded=RetailCollisionQuery.ProjectGround(scene,scene.Stamp,new(.5f,.5f,10),CollisionFixture.QueryLimits);
        Computed(predicted,prediction); Computed(grounded,ground);
        Assert.Equal(prediction?new RetailSearchPoint(.5f,.5f,4):(RetailSearchPoint?)null,predicted.Point);
        Assert.Equal(ground?new RetailSearchPoint(.5f,.5f,4):(RetailSearchPoint?)null,grounded.Point);
        Assert.Same(scene.Sources,predicted.Sources);Assert.Same(scene.Prepared.Cosine,predicted.CosineReceipt);Assert.Same(scene.MaterialPolicy,predicted.MaterialPolicy);
        Assert.Equal(RetailCollisionQuery.DispatchFunction(RetailCollisionQueryKind.PredictionProjection,selector),Assert.Single(predicted.Trace,t=>t.Kind==RetailCollisionTraceKind.Cell).NativeFunction);
        Assert.Equal(RetailCollisionQuery.DispatchFunction(RetailCollisionQueryKind.GroundProjection,selector),Assert.Single(grounded.Trace,t=>t.Kind==RetailCollisionTraceKind.Cell).NativeFunction);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    [InlineData(8)] [InlineData(9)] [InlineData(10)] [InlineData(11)] [InlineData(12)] [InlineData(13)] [InlineData(14)] [InlineData(15)]
    public void AllSegmentSelectorsUseTheirOwnTablesAndInclusiveTerrainCrossings(byte selector)
    {
        var scene=EmptyScene(selector); var from=new RetailSearchPoint(.5f,.5f,1); var to=new RetailSearchPoint(.5f,.5f,-1);
        var blocked=RetailCollisionQuery.SegmentBlocked(scene,scene.Stamp,from,to,0,0,0,CollisionFixture.QueryLimits);
        var contact=RetailCollisionQuery.FirstContact(scene,scene.Stamp,from,to,0,0,CollisionFixture.QueryLimits);
        Computed(blocked,selector<12); Computed(contact,selector<12);
        Assert.Equal(selector<12?new RetailSearchPoint(.5f,.5f,0):(RetailSearchPoint?)null,contact.Point); Assert.Null(blocked.Point);
        Assert.Equal(RetailCollisionQuery.DispatchFunction(RetailCollisionQueryKind.BooleanSegment,selector),Assert.Single(blocked.Trace,t=>t.Kind==RetailCollisionTraceKind.Cell).NativeFunction);
        Assert.Equal(RetailCollisionQuery.DispatchFunction(RetailCollisionQueryKind.ContactSegment,selector),Assert.Single(contact.Trace,t=>t.Kind==RetailCollisionTraceKind.Cell).NativeFunction);
    }

    [Fact]
    public void HeightMap40UsesSecondPlaneDirectlyAndBilinearWeightsKeepXy()
    {
        var raw=Raw();var prepared=CollisionFixture.Prepared(raw);
        var terrain=CollisionFixture.Terrain((_,_)=>14,(_,_)=>3,255);
        var direct=CollisionFixture.Scene(prepared,terrain);
        Computed(RetailCollisionQuery.ProjectPrediction(direct,direct.Stamp,new(1,1,10),CollisionFixture.QueryLimits),false);
        var bilinear=EmptyScene(0,(x,y)=>(ushort)(x==0&&y==0?0:x==1&&y==0?64:x==0&&y==1?128:192));
        var result=RetailCollisionQuery.ProjectPrediction(bilinear,bilinear.Stamp,new(1,1,10),CollisionFixture.QueryLimits);
        Computed(result,true); Assert.Equal(new RetailSearchPoint(1,1,3),result.Point);
        var flat=EmptyScene(4,(x,y)=>(ushort)(x==0&&y==0?0:192));
        Assert.Equal(0f,RetailCollisionQuery.ProjectPrediction(flat,flat.Stamp,new(1,1,10),CollisionFixture.QueryLimits).Point!.Value.Z);
    }

    [Fact]
    public void TerrainContactKeepsFirstTriangleEvenWhenSecondHasNearerIntersection()
    {
        var scene=EmptyScene(0,(x,y)=>(ushort)(x==1&&y==1?64:0));
        var result=RetailCollisionQuery.FirstContact(scene,scene.Stamp,new(1.5f,1.5f,.5f),new(.5f,.5f,-.25f),0,0,CollisionFixture.QueryLimits);
        Computed(result,true); Assert.Equal(0f,result.Point!.Value.Z);
        Assert.Equal(BitConverter.SingleToInt32Bits(.8333333f),BitConverter.SingleToInt32Bits(result.Point.Value.X));
        Assert.Single(result.Trace,t=>t.Kind==RetailCollisionTraceKind.TerrainTriangle);
        Assert.Contains(result.Trace,t=>t.NativeFunction=="0x140297e80 first");
    }

    [Theory]
    [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    public void FlatFamiliesPreserveUnscaledCellIndexBoundsInsteadOfSubstitutingTerrainQuad(byte selector)
    {
        var flat=EmptyScene(selector); var from=new RetailSearchPoint(4.5f,4.5f,1);var to=new RetailSearchPoint(4.5f,4.5f,-1);
        Computed(RetailCollisionQuery.SegmentBlocked(flat,flat.Stamp,from,to,0,0,0,CollisionFixture.QueryLimits),false);
        Computed(RetailCollisionQuery.FirstContact(flat,flat.Stamp,from,to,0,0,CollisionFixture.QueryLimits),false);
        var bilinear=EmptyScene(0);
        Computed(RetailCollisionQuery.FirstContact(bilinear,bilinear.Stamp,from,to,0,0,CollisionFixture.QueryLimits),true);
    }

    [Fact]
    public void GroundProjectionStopsAtTaggedPrefixButPredictionIntersectsBothWindings()
    {
        var scene=MeshScene([CollisionFixture.Face(reversed:true)],(x,y)=>x==0&&y==0?[new(0,0)]:[new(0)],14);
        var input=new RetailSearchPoint(.5f,.5f,1);
        var ground=RetailCollisionQuery.ProjectGround(scene,scene.Stamp,input,CollisionFixture.QueryLimits);
        var prediction=RetailCollisionQuery.ProjectPrediction(scene,scene.Stamp,input,CollisionFixture.QueryLimits);
        Computed(ground,false); Assert.Equal(0,ground.Triangles); Computed(prediction,true); Assert.Equal(1,prediction.Triangles);
        Assert.Equal(new RetailSearchPoint(.5f,.5f,0),prediction.Point);
    }

    [Fact]
    public void ProjectionUsesNativeAbovePenaltyAndBandEarlyExitRatherThanNearestAbsoluteHeight()
    {
        var scene=MeshScene([CollisionFixture.Face(1),CollisionFixture.Face(10.2f)],
            (x,y)=>x==0&&y==0?[new(0,0),new(10,1)]:[new(0)],14);
        var result=RetailCollisionQuery.ProjectPrediction(scene,scene.Stamp,new(.5f,.5f,10),CollisionFixture.QueryLimits);
        Computed(result,true);Assert.Equal(1f,result.Point!.Value.Z);Assert.Equal(2,result.Triangles);
        var early=MeshScene([CollisionFixture.Face(1),CollisionFixture.Face(20)],
            (x,y)=>x==0&&y==0?[new(0,0),new(12,1)]:[new(0)],14);
        var stopped=RetailCollisionQuery.ProjectPrediction(early,early.Stamp,new(.5f,.5f,10),CollisionFixture.QueryLimits);
        Assert.Equal(1f,stopped.Point!.Value.Z);Assert.Equal(1,stopped.Triangles);Assert.Equal(2,stopped.Bands);
        var noEarlier=MeshScene([CollisionFixture.Face(20)],(x,y)=>x==0&&y==0?[new(0),new(12,0)]:[new(0)],14);
        Assert.Equal(20f,RetailCollisionQuery.ProjectPrediction(noEarlier,noEarlier.Stamp,new(.5f,.5f,10),CollisionFixture.QueryLimits).Point!.Value.Z);
    }

    [Fact]
    public void ProjectionAbovePenaltyThresholdIsStrictAtTheAdjacentBinary32Values()
    {
        var equality=MeshScene([CollisionFixture.Face(-1),CollisionFixture.Face(.1f)],
            (x,y)=>x==0&&y==0?[new(-2,0),new(0,1)]:[new(0)],14);
        Assert.Equal(.1f,RetailCollisionQuery.ProjectPrediction(equality,equality.Stamp,new(.5f,.5f,0),CollisionFixture.QueryLimits).Point!.Value.Z);
        var penalty=MeshScene([CollisionFixture.Face(-1),CollisionFixture.Face(MathF.BitIncrement(.1f))],
            (x,y)=>x==0&&y==0?[new(-2,0),new(0,1)]:[new(0)],14);
        Assert.Equal(-1f,RetailCollisionQuery.ProjectPrediction(penalty,penalty.Stamp,new(.5f,.5f,0),CollisionFixture.QueryLimits).Point!.Value.Z);
    }

    [Theory]
    [InlineData(0,0,true)] [InlineData(0,5,false)] [InlineData(6,0,false)] [InlineData(6,1,true)]
    public void SegmentMaterialsUseExplicitRawViewWhileContactForcesColumnZero(byte material,int view,bool blocked)
    {
        var scene=MeshScene([CollisionFixture.Face(material:material)],(x,y)=>x==0&&y==0?[new(0,0)]:[new(0)],14);
        Computed(RetailCollisionQuery.SegmentBlocked(scene,scene.Stamp,new(.5f,.5f,1),new(.5f,.5f,-1),view,0,0,CollisionFixture.QueryLimits),blocked);
        Computed(RetailCollisionQuery.FirstContact(scene,scene.Stamp,new(.5f,.5f,1),new(.5f,.5f,-1),0,0,CollisionFixture.QueryLimits),material==0);
    }

    [Theory]
    [InlineData(-1)] [InlineData(6)]
    public void InvalidMaterialViewRejectsWhenConsumedButNoMeshDoesNotInventAPolicyRead(int view)
    {
        var scene=MeshScene([CollisionFixture.Face()],(x,y)=>[new(0,0)],14);
        Rejected(RetailCollisionQuery.SegmentBlocked(scene,scene.Stamp,new(.5f,.5f,1),new(.5f,.5f,-1),view,0,0,CollisionFixture.QueryLimits),RetailCollisionStatus.UnsupportedFormat);
        var unused=EmptyScene(12);
        Computed(RetailCollisionQuery.SegmentBlocked(unused,unused.Stamp,new(.5f,.5f,1),new(.5f,.5f,-1),view,0,0,CollisionFixture.QueryLimits),false);
    }

    [Theory]
    [InlineData("missing")] [InlineData("unknown")] [InlineData("bytes")] [InlineData("pin")] [InlineData("revision")] [InlineData("evidence")]
    public void ConsumedUnknownOrMismatchedMaterialPolicyNeverPublishesClearOrContact(string invalid)
    {
        byte[] bytes=RetailCollisionMaterialPolicy.PinnedFileImageBytes.ToArray();if(invalid=="bytes")bytes[0]^=1;
        var policy=new RetailCollisionMaterialPolicy(bytes,invalid=="pin"?new string('0',64):RetailGroundPathSearch.SupportedNpcBinarySha256,
            invalid=="revision"?"":"policy1",invalid=="evidence"?"unknown":new string('d',64),RetailCollisionPolicySource.RecordedRuntimeDeclaration,invalid!="unknown");
        var scene=MeshScene([CollisionFixture.Face()],(x,y)=>[new(0,0)],14,policy:policy,missingPolicy:invalid=="missing");
        Rejected(RetailCollisionQuery.FirstContact(scene,scene.Stamp,new(.5f,.5f,1),new(.5f,.5f,-1),0,0,CollisionFixture.QueryLimits),RetailCollisionStatus.MissingMaterialPolicy);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void MarchOrderUsesCurrentCellThenOrderedCrossingCells(bool reverse)
    {
        var scene=EmptyScene(12);var a=new RetailSearchPoint(.5f,.5f,1);var b=new RetailSearchPoint(4.5f,4.5f,1);
        var result=RetailCollisionQuery.FirstContact(scene,scene.Stamp,reverse?b:a,reverse?a:b,0,0,CollisionFixture.QueryLimits);
        Computed(result,false);
        var actual=result.Trace.Where(t=>t.Kind==RetailCollisionTraceKind.Cell).Select(t=>(t.CellX,t.CellY)).ToArray();
        (int,int)[] expected=reverse?[(2,2),(1,1),(2,1),(1,2),(0,0),(1,0),(0,1)]:[(0,0),(1,1),(0,1),(1,0),(2,2),(1,2),(2,1)];
        Assert.Equal(expected,actual);
    }

    [Fact]
    public void ContactReturnsFirstSuccessfulCellAndDoesNotGloballyMinimizeAcrossMarch()
    {
        CollisionFace Face(float z)=>new(new(0,0,z),new(8,0,z),new(0,8,z),0);
        var scene=MeshScene([Face(0),Face(2)],(x,y)=>x==0&&y==0?[new(0,0)]:x==1&&y==0?[new(2,1)]:[new(0)],14);
        var result=RetailCollisionQuery.FirstContact(scene,scene.Stamp,new(.25f,.25f,3),new(4.25f,.25f,-1),0,0,CollisionFixture.QueryLimits);
        Computed(result,true);Assert.Equal(new RetailSearchPoint(3.25f,.25f,0),result.Point);Assert.Equal(1,result.VisitedCells);
        Assert.Single(result.Trace,t=>t.Kind==RetailCollisionTraceKind.MeshTriangle&&t.TriangleId==0);
    }

    [Theory]
    [InlineData(1,2)] [InlineData(2,1)]
    public void SegmentFiltersPrimitiveParametersOutsideInclusiveEndpoints(float fromZ,float toZ)
    {
        var scene=MeshScene([CollisionFixture.Face()],(x,y)=>[new(0,0)],14);
        Computed(RetailCollisionQuery.FirstContact(scene,scene.Stamp,new(.5f,.5f,fromZ),new(.5f,.5f,toZ),0,0,CollisionFixture.QueryLimits),false);
        var endpoint=RetailCollisionQuery.FirstContact(scene,scene.Stamp,new(.5f,.5f,1),new(.5f,.5f,0),0,0,CollisionFixture.QueryLimits);
        Computed(endpoint,true);Assert.Equal(0f,endpoint.Point!.Value.Z);
    }

    [Theory]
    [InlineData(.2f)] [InlineData(-.2f)]
    public void ShortSegmentGuardSquaresExplicitToleranceBeforeConsumingGeometry(float tolerance)
    {
        var raw=CollisionFixture.Read(CollisionFixture.Mesh([CollisionFixture.Face()]),CollisionFixture.Collision(emptySector:true));
        var scene=CollisionFixture.Scene(CollisionFixture.Prepared(raw),CollisionFixture.Terrain((_,_)=>255));
        var result=RetailCollisionQuery.FirstContact(scene,scene.Stamp,new(.5f,.5f,1),new(.5f,.5f,1.1f),0,tolerance,CollisionFixture.QueryLimits);
        Computed(result,false);Assert.Equal(0,result.VisitedCells);Assert.Contains(result.Trace,t=>t.NativeFunction=="NativeShortSegment");
    }

    [Fact]
    public void OutsideIntegerBoundsDifferBetweenBooleanAndContactAndProjectionIsHalfOpen()
    {
        var scene=EmptyScene(12);var a=new RetailSearchPoint(-1,.5f,1);var b=new RetailSearchPoint(-1,.5f,-1);
        Computed(RetailCollisionQuery.SegmentBlocked(scene,scene.Stamp,a,b,0,0,0,CollisionFixture.QueryLimits),true);
        Computed(RetailCollisionQuery.FirstContact(scene,scene.Stamp,a,b,0,0,CollisionFixture.QueryLimits),false);
        Computed(RetailCollisionQuery.ProjectGround(scene,scene.Stamp,new(64,.5f,1),CollisionFixture.QueryLimits),false);
    }

    [Theory]
    [InlineData("instance")] [InlineData("scene")] [InlineData("asset")]
    public void RequestEpochMismatchesFailBeforeGeometry(string changed)
    {
        var scene=EmptyScene(0);var stamp=changed switch
        {"instance"=>scene.Stamp with {InstanceIdentity="other"},"scene"=>scene.Stamp with {SceneRevision="other"},_=>scene.Stamp with {AssetRevision="other"}};
        var result=RetailCollisionQuery.ProjectGround(scene,stamp,new(.5f,.5f,1),CollisionFixture.QueryLimits);
        Rejected(result,RetailCollisionStatus.StaleSource);Assert.Empty(result.Trace);
    }

    [Theory]
    [InlineData("missing",RetailCollisionStatus.UnsupportedDynamicScene)] [InlineData("unknown",RetailCollisionStatus.UnsupportedDynamicScene)]
    [InlineData("stale",RetailCollisionStatus.StaleSource)]
    public void OddSelectorsRequireAnExplicitCurrentCompleteOrderedRegion(string invalid,RetailCollisionStatus status)
    {
        IReadOnlyList<RetailCollisionRegionObservation> regions=invalid=="missing"?[]:
            [new(0,0,invalid!="unknown",invalid=="stale"?"old":"scene1",new string('b',64),[])];
        var scene=CollisionFixture.Scene(CollisionFixture.Prepared(Raw()),CollisionFixture.Terrain((_,_)=>1),regions);
        Rejected(RetailCollisionQuery.ProjectGround(scene,scene.Stamp,new(.5f,.5f,1),CollisionFixture.QueryLimits),status);
        var even=CollisionFixture.Scene(scene.Prepared,CollisionFixture.Terrain((_,_)=>0),regions);
        Computed(RetailCollisionQuery.ProjectGround(even,even.Stamp,new(.5f,.5f,1),CollisionFixture.QueryLimits),true);
    }

    [Fact]
    public void UnknownDynamicObjectDiscardsProvisionalProjectionAndContactButBooleanMayReturnEarlierStaticHit()
    {
        RetailCollisionRegionObservation[] regions=[new(0,0,true,"scene1",new string('b',64),[new(42,"unresolved-native-class","object1")])];
        var scene=MeshScene([CollisionFixture.Face()],(x,y)=>[new(0,0)],15,regions:regions);
        Rejected(RetailCollisionQuery.ProjectPrediction(scene,scene.Stamp,new(.5f,.5f,1),CollisionFixture.QueryLimits),RetailCollisionStatus.UnsupportedDynamicScene);
        Rejected(RetailCollisionQuery.ProjectGround(scene,scene.Stamp,new(.5f,.5f,1),CollisionFixture.QueryLimits),RetailCollisionStatus.UnsupportedDynamicScene);
        var contact=RetailCollisionQuery.FirstContact(scene,scene.Stamp,new(.5f,.5f,1),new(.5f,.5f,-1),0,0,CollisionFixture.QueryLimits);
        Rejected(contact,RetailCollisionStatus.UnsupportedDynamicScene);Assert.Equal(1,contact.Triangles);Assert.Equal(1,contact.SceneObjects);
        var boolean=RetailCollisionQuery.SegmentBlocked(scene,scene.Stamp,new(.5f,.5f,1),new(.5f,.5f,-1),0,0,0,CollisionFixture.QueryLimits);
        Computed(boolean,true);Assert.Equal(0,boolean.SceneObjects);
        Computed(RetailCollisionQuery.FirstContact(scene,scene.Stamp,new(.5f,.5f,1),new(.5f,.5f,-1),42,0,CollisionFixture.QueryLimits),true);
    }

    [Fact]
    public void IgnoredObjectZeroMeansNoIgnoreAndOwnedSceneDoesNotBorrowMutableLists()
    {
        var objects=new List<RetailCollisionSceneObject>{new(0,"unresolved","object1")};
        var regions=new List<RetailCollisionRegionObservation>{new(0,0,true,"scene1",new string('b',64),objects)};
        var scene=CollisionFixture.Scene(CollisionFixture.Prepared(Raw()),CollisionFixture.Terrain((_,_)=>13),regions);
        objects.Clear();regions.Clear();
        Rejected(RetailCollisionQuery.FirstContact(scene,scene.Stamp,new(.5f,.5f,1),new(.5f,.5f,-1),0,0,CollisionFixture.QueryLimits),RetailCollisionStatus.UnsupportedDynamicScene);
    }

    [Fact]
    public void FieldCatalogNeverSubstitutesForLiveRegionMembership()
    {
        var raw=CollisionFixture.Read(CollisionFixture.Mesh([CollisionFixture.Face()],fields:[new(15,"PlaceableObject","earth",[0,0,0,0])]),CollisionFixture.Collision());
        var scene=CollisionFixture.Scene(CollisionFixture.Prepared(raw),CollisionFixture.Terrain((_,_)=>13),[]);
        Rejected(RetailCollisionQuery.ProjectGround(scene,scene.Stamp,new(.5f,.5f,1),CollisionFixture.QueryLimits),RetailCollisionStatus.UnsupportedDynamicScene);
        var explicitEmpty=CollisionFixture.Scene(scene.Prepared,scene.Terrain,CollisionFixture.Regions());
        Computed(RetailCollisionQuery.ProjectGround(explicitEmpty,explicitEmpty.Stamp,new(.5f,.5f,1),CollisionFixture.QueryLimits),false);
    }

    [Theory]
    [InlineData("empty",RetailCollisionStatus.UnsupportedEmptySectorStorage)] [InlineData("selector",RetailCollisionStatus.UnsupportedFormat)]
    public void UnresolvedStorageAndUnknownDirectSelectorHaveNoClearSceneFallback(string invalid,RetailCollisionStatus status)
    {
        var raw=CollisionFixture.Read(CollisionFixture.Mesh([CollisionFixture.Face()]),CollisionFixture.Collision(emptySector:invalid=="empty"));
        var scene=CollisionFixture.Scene(CollisionFixture.Prepared(raw),CollisionFixture.Terrain((_,_)=>(byte)(invalid=="selector"?16:0)));
        Rejected(RetailCollisionQuery.ProjectGround(scene,scene.Stamp,new(.5f,.5f,1),CollisionFixture.QueryLimits),status);
    }

    [Fact]
    public void NativeFloorSectorDimensionsDoNotInventStorageForPartialWorldEdge()
    {
        var raw=CollisionFixture.Read(CollisionFixture.Mesh([CollisionFixture.Face()]),CollisionFixture.Collision(width:66),66,64);
        var scene=CollisionFixture.Scene(CollisionFixture.Prepared(raw),CollisionFixture.Terrain((_,_)=>0,width:66));
        Rejected(RetailCollisionQuery.ProjectGround(scene,scene.Stamp,new(64.5f,.5f,1),CollisionFixture.QueryLimits),RetailCollisionStatus.UnsupportedEmptySectorStorage);
        Computed(RetailCollisionQuery.ProjectGround(scene,scene.Stamp,new(62.5f,.5f,1),CollisionFixture.QueryLimits),true);
    }

    [Theory]
    [InlineData(0x7fc01234u)] [InlineData(1u)]
    public void ConsumedUnsupportedCoordinatesAndToleranceAreTypedFailures(uint bits)
    {
        float value=BitConverter.Int32BitsToSingle(unchecked((int)bits));var scene=EmptyScene(0);
        Rejected(RetailCollisionQuery.ProjectGround(scene,scene.Stamp,new(value,.5f,1),CollisionFixture.QueryLimits),RetailCollisionStatus.UnsupportedArithmetic);
        Rejected(RetailCollisionQuery.FirstContact(scene,scene.Stamp,new(.5f,.5f,1),new(.5f,.5f,-1),0,value,CollisionFixture.QueryLimits),RetailCollisionStatus.UnsupportedArithmetic);
    }

    [Theory]
    [InlineData("cells")] [InlineData("bands")] [InlineData("triangles")] [InlineData("trace")] [InlineData("objects")]
    public void ManagedOperationLimitsDiscardAnyProvisionalResult(string kind)
    {
        RetailCollisionRegionObservation[] regions=[new(0,0,true,"scene1",new string('b',64),[new(42,"unresolved","object1")])];
        var scene=MeshScene([CollisionFixture.Face()],(x,y)=>[new(0,0)],15,regions:regions);
        var limits=kind switch
        {
            "cells"=>CollisionFixture.QueryLimits with {MaxVisitedCells=1},"bands"=>CollisionFixture.QueryLimits with {MaxBands=0},
            "triangles"=>CollisionFixture.QueryLimits with {MaxTriangles=0},"trace"=>CollisionFixture.QueryLimits with {MaxTraceEntries=1},
            _=>CollisionFixture.QueryLimits with {MaxSceneObjects=0}
        };
        RetailCollisionQueryResult result;
        if(kind=="cells")
        {
            var empty=EmptyScene(12);
            result=RetailCollisionQuery.FirstContact(empty,empty.Stamp,new(.5f,.5f,1),new(4.5f,.5f,1),0,0,limits);
        }
        else result=RetailCollisionQuery.FirstContact(scene,scene.Stamp,new(.5f,.5f,1),new(.5f,.5f,-1),0,0,limits);
        Rejected(result,RetailCollisionStatus.DefensiveLimit);
    }

    [Theory]
    [InlineData("mesh")] [InlineData("collision")] [InlineData("height40")] [InlineData("native")]
    public void EveryIndividualAssetIdentityIsCheckedAgainstOwnedBytes(string changed)
    {
        var prepared=CollisionFixture.Prepared(Raw());var terrain=CollisionFixture.Terrain();var sources=CollisionFixture.Sources(prepared,terrain);
        sources=changed switch
        {
            "mesh"=>sources with {Mesh=sources.Mesh with {Asset=sources.Mesh.Asset with {Sha256=new string('0',64)}}},
            "collision"=>sources with {Collision=sources.Collision with {Asset=sources.Collision.Asset with {ByteLength=0}}},
            "height40"=>sources with {HeightMap40=sources.HeightMap40 with {BuildGuid=Guid.Empty}},
            _=>sources with {NativeNpcBinary=sources.NativeNpcBinary with {Sha256=new string('0',64)}}
        };
        var result=Create(prepared,terrain,sources,CollisionFixture.Regions());
        Assert.Equal(RetailCollisionStatus.SourceIdentityMismatch,result.Status);Assert.Null(result.Scene);
    }

    [Fact]
    public void IndividuallyBoundDifferentGuidsAndDimensionsStillCannotBeCombined()
    {
        var bytes=CollisionFixture.Collision();bytes[4]^=1;
        var prepared=CollisionFixture.Prepared(CollisionFixture.Read(CollisionFixture.Mesh([CollisionFixture.Face()]),bytes));
        var terrain=CollisionFixture.Terrain();
        Assert.Equal(RetailCollisionStatus.SourceIdentityMismatch,Create(prepared,terrain,CollisionFixture.Sources(prepared,terrain),CollisionFixture.Regions()).Status);
        prepared=CollisionFixture.Prepared(Raw());terrain=CollisionFixture.Terrain(width:128);
        Assert.Equal(RetailCollisionStatus.SourceIdentityMismatch,Create(prepared,terrain,CollisionFixture.Sources(prepared,terrain),CollisionFixture.Regions()).Status);
    }

    [Fact]
    public void HeaderlessPairCanBeParsedButCannotPublishASourceMatchedScene()
    {
        var raw=CollisionFixture.Read(CollisionFixture.Mesh([CollisionFixture.Face()])[128..],CollisionFixture.Collision()[128..]);
        var prepared=CollisionFixture.Prepared(raw);var terrain=CollisionFixture.Terrain();
        var result=Create(prepared,terrain,CollisionFixture.Sources(prepared,terrain),CollisionFixture.Regions());
        Assert.Equal(RetailCollisionStatus.SourceIdentityMismatch,result.Status);Assert.Null(result.Scene);
    }

    [Fact]
    public void MissingSceneOrInvalidRequestBudgetIsTypedAndCannotPublishNativeSuccess()
    {
        var point=new RetailSearchPoint(.5f,.5f,1);var stamp=new RetailCollisionQueryStamp("instance1","scene1","asset1");
        Rejected(RetailCollisionQuery.ProjectGround(null!,stamp,point,CollisionFixture.QueryLimits),RetailCollisionStatus.InvalidInput);
        Rejected(RetailCollisionQuery.ProjectPrediction(null!,stamp,point,CollisionFixture.QueryLimits),RetailCollisionStatus.InvalidInput);
        Rejected(RetailCollisionQuery.FirstContact(null!,stamp,point,point,0,0,CollisionFixture.QueryLimits),RetailCollisionStatus.InvalidInput);
        Rejected(RetailCollisionQuery.SegmentBlocked(null!,stamp,point,point,0,0,0,CollisionFixture.QueryLimits),RetailCollisionStatus.InvalidInput);
        var scene=EmptyScene(0);
        Rejected(RetailCollisionQuery.ProjectGround(scene,scene.Stamp,point,CollisionFixture.QueryLimits with {MaxVisitedCells=0}),RetailCollisionStatus.InvalidInput);
    }

    [Fact]
    public void SceneDuplicateAndRecordBudgetsDoNotSilentlySelectOrTruncateSnapshots()
    {
        var prepared=CollisionFixture.Prepared(Raw());var terrain=CollisionFixture.Terrain();var sources=CollisionFixture.Sources(prepared,terrain);
        RetailCollisionRegionObservation region=new(0,0,true,"scene1",new string('b',64),[new(1,"class","revision")]);
        Assert.Equal(RetailCollisionStatus.InvalidInput,Create(prepared,terrain,sources,[region,region]).Status);
        Assert.Equal(RetailCollisionStatus.DefensiveLimit,Create(prepared,terrain,sources,[region],new(10,0)).Status);
        Assert.Equal(RetailCollisionStatus.DefensiveLimit,Create(prepared,terrain,sources,CollisionFixture.Regions(),new(1,10)).Status);
    }

    private static RetailCollisionSceneResult Create(RetailCollisionPreparedData prepared,RetailHeightMap40Data terrain,
        RetailCollisionSources sources,IReadOnlyList<RetailCollisionRegionObservation> regions,RetailCollisionSceneLimits? limits=null)=>
        RetailCollisionScene.Create(prepared,terrain,sources,new(new(0,0,-10240),new(64,64,10240)),
            new("synthetic-world","instance1","scene1","asset1",new string('c',64),"synthetic-scene",true),regions,
            RetailCollisionMaterialPolicy.FromPinnedFileImage("explicit-file-image"),limits??new(10000,10000));
    private static RetailCollisionData Raw()=>CollisionFixture.Read(CollisionFixture.Mesh([CollisionFixture.Face()]),CollisionFixture.Collision());
    private static RetailCollisionScene EmptyScene(byte selector,Func<int,int,ushort>? word=null)=>
        CollisionFixture.Scene(CollisionFixture.Prepared(Raw()),CollisionFixture.Terrain((_,_)=>selector,word));
    private static RetailCollisionScene MeshScene(IReadOnlyList<CollisionFace> faces,Func<int,int,IReadOnlyList<CollisionBand>> bands,
        byte selector,Func<int,int,ushort>? word=null,IReadOnlyList<RetailCollisionRegionObservation>? regions=null,
        RetailCollisionMaterialPolicy? policy=null,bool missingPolicy=false)=>CollisionFixture.Scene(
            CollisionFixture.Prepared(CollisionFixture.Read(CollisionFixture.Mesh(faces),CollisionFixture.Collision(bands))),
            CollisionFixture.Terrain((_,_)=>selector,word),regions,policy,missingPolicy);
    private static void Computed(RetailCollisionQueryResult result,bool hit)
    {
        Assert.Equal(RetailCollisionStatus.Computed,result.Status);Assert.Equal(hit?1u:0u,result.NativeResult);
        Assert.False(result.RuntimeVerified);Assert.False(result.NativeTargetExecuted);Assert.False(result.ExecutesCommands);Assert.False(result.AppliesActorClearance);
    }
    private static void Rejected(RetailCollisionQueryResult result,RetailCollisionStatus status)
    {Assert.Equal(status,result.Status);Assert.Null(result.NativeResult);Assert.Null(result.Point);Assert.False(result.RuntimeVerified);}
}
