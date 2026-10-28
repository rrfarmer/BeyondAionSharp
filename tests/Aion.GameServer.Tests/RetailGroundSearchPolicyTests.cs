using Aion.GameServer.GeoEngine.RetailPath;

namespace Aion.GameServer.Tests;

public sealed class RetailGroundSearchPolicyTests
{
    private static RetailPathSurfaceRef At(MovementOwnerFixture f,float x,float y)=>
        f.Scene.Surfaces.EnumerateSurfaceCandidates(x,y,0).Candidates.First(c=>c.Status==RetailSurfaceCandidateStatus.WithinTolerance).Surface;
    [Fact]
    public void ActualDirectedCardinalStepAndFourBodyRingsGiveExactHalfMetreCost50()
    {
        var f=MovementOwnerFixture.Create();var policy=new RetailGroundSearchPolicy(f.Scene,f.State.Actor,true,0,f.Limits,"p");
        var r=policy.TransitionCost(At(f,31.25f,1.25f),At(f,31.75f,1.25f));
        Assert.True(r.Stamp.Known);Assert.Equal(50,r.Cost);Assert.Null(policy.Failure);
    }
    [Fact]
    public void MissingBodyRingCosts200BeforeAnyActiveProximityChild()
    {
        // Near west world edge, the two-cell west body ring has an actual outside-world link.
        var f=MovementOwnerFixture.Create();var p=new RetailGroundSearchPolicy(f.Scene,f.State.Actor with {Crowd310=1},true,0,f.Limits,"p");
        var r=p.TransitionCost(At(f,.25f,1.25f),At(f,.75f,1.25f));Assert.True(r.Stamp.Known);Assert.Equal(200,r.Cost);Assert.Null(p.Failure);
    }
    [Fact]
    public void UnknownFieldTreeAndActiveProximityWithholdPolicyValues()
    {
        var f=MovementOwnerFixture.Create();var unknown=new RetailGroundMovementScene(f.Scene.Sources,f.Scene.Collision,f.Scene.Ownership,new(),null,true,f.Scene.FloatingEnvironment);
        var p=new RetailGroundSearchPolicy(unknown,f.State.Actor,false,0,f.Limits,"p");
        Assert.False(p.TransitionCost(At(f,31.25f,1.25f),At(f,31.75f,1.25f)).Stamp.Known);Assert.Equal("registered-field-tree",p.Failure!.Boundary);
        p=new(f.Scene,f.State.Actor with {Crowd310=1},false,0,f.Limits,"p");
        Assert.False(p.TransitionCost(At(f,31.25f,1.25f),At(f,31.75f,1.25f)).Stamp.Known);Assert.Equal("proximity-cost",p.Failure!.Boundary);
    }
    [Theory]
    [InlineData(0,false)] [InlineData(1,true)] [InlineData(2,false)]
    public void AreaPartyHomeSkipComesFromCurrentAiStateNotSuppliedClearGeometry(int ai,bool admitted)
    {
        var f=MovementOwnerFixture.Create();var p=new RetailGroundSearchPolicy(f.Scene,f.State.Actor with {AiState=ai},false,0,f.Limits,"p");
        var r=p.CheckSegment(new(31,1,0),new(32,1,0),RetailSearchPolicyPurpose.InitialShortcut);
        Assert.Equal(admitted,r.Stamp.Known);if(admitted){Assert.False(r.Blocked);Assert.Equal(new(32,1,0),r.ClippedEndpoint);}else Assert.Equal(RetailMovementCalculationStatus.IncompleteAtChild,p.Failure!.Status);
    }
    [Fact]
    public void ComputedWallBlocksShortcutWithoutSupplyingAnOpaqueBoolean()
    {
        var f=MovementOwnerFixture.Create([RetailGroundClearanceTests.Wall()]);var p=new RetailGroundSearchPolicy(f.Scene,f.State.Actor,false,0,f.Limits,"p");
        var r=p.CheckSegment(new(31,1,0),new(32,1,0),RetailSearchPolicyPurpose.InitialShortcut);
        Assert.True(r.Stamp.Known);Assert.True(r.Blocked);Assert.Equal(new(31,1,0),r.ClippedEndpoint);Assert.NotEmpty(p.ClearanceCalls);
    }
    [Fact]
    public void NullFieldTreeEarlyReturnPrecedesZeroXyRayArithmetic()
    {
        var f=MovementOwnerFixture.Create();var p=new RetailGroundSearchPolicy(f.Scene,f.State.Actor,false,0,f.Limits,"p");
        var r=p.FieldObjectCollision(new(31,1,0),new(31,1,0),RetailSearchPolicyPurpose.InitialShortcut);Assert.True(r.Stamp.Known);Assert.False(r.Value);
        var nonempty=new RetailGroundMovementScene(f.Scene.Sources,f.Scene.Collision,f.Scene.Ownership,new(),1,true,f.Scene.FloatingEnvironment);
        p=new(nonempty,f.State.Actor,false,0,f.Limits,"p");r=p.FieldObjectCollision(new(31,1,0),new(31,1,0),RetailSearchPolicyPurpose.InitialShortcut);
        Assert.False(r.Stamp.Known);Assert.Equal("zero-xy-field-ray",p.Failure!.Boundary);
    }
    [Theory]
    [InlineData(1,0u,2000f)] [InlineData(0,0u,200f)] [InlineData(1,15u,200f)]
    public void ComputedTypedFieldOverridesMissingBodyRingAndProximityOnlyWhenStateAndIgnoredIdAllow(int state,uint ignored,float expected)
    {
        var f=MovementOwnerFixture.Create();var wall=new CollisionFace(new(.75f,0,-4),new(.75f,0,8),new(.75f,8,-4),0);
        var prepared=CollisionFixture.Prepared(FieldGeometryFixture.Raw([wall]));
        var field=FieldGeometryFixture.Prepare(prepared,FieldGeometryFixture.Observation(state));var collision=FieldGeometryFixture.Scene(prepared,[field]);
        var sources=f.Scene.Sources with {Terrain=collision.Terrain,TerrainIdentity=collision.Sources.HeightMap40,CollisionAsset=collision.Sources.Collision.Asset};
        var scene=new RetailGroundMovementScene(sources,collision,f.State.Ownership,new(),1,true,f.Scene.FloatingEnvironment);
        var p=new RetailGroundSearchPolicy(scene,f.State.Actor with {Crowd310=1},true,ignored,f.Limits,"p");
        var r=p.TransitionCost(At(f,.25f,.25f),At(f,1.25f,.25f));
        Assert.True(r.Stamp.Known,p.Failure?.ToString());Assert.Equal(expected,r.Cost);
        if(ignored==15)Assert.Empty(p.FieldVisits);else Assert.NotEmpty(p.FieldVisits);
    }
}
