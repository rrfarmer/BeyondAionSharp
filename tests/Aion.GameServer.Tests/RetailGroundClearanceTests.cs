using Aion.GameServer.GeoEngine.RetailPath;

namespace Aion.GameServer.Tests;

public sealed class RetailGroundClearanceTests
{
    internal static CollisionFace Wall(float x=31.5f,float top=8)=>new(new(x,0,0),new(x,0,top),new(x,8,0),0);
    [Fact]
    public void ComputedSegmentBlocksBothWallWindings()
    {
        var wall=Wall();var f=MovementOwnerFixture.Create([wall]);
        var r=RetailGroundClearance.OneStep(f.Scene,f.State.Actor,new(31,1,0),new(32,1,0),.001f,0,f.Limits);
        Assert.True(r.HasNativeOutcome,r.Failure?.ToString());Assert.Equal(1u,r.NativeResult);Assert.Equal(new(31,1,0),r.Endpoint);
        Assert.Contains(r.Geometry,g=>g.NativeResult==1);
        f=MovementOwnerFixture.Create([wall with {B=wall.C,C=wall.B}]);
        r=RetailGroundClearance.OneStep(f.Scene,f.State.Actor,new(31,1,0),new(32,1,0),.001f,0,f.Limits);
        Assert.Equal(1u,r.NativeResult);Assert.Equal(new(31,1,0),r.Endpoint);
        // This is direct-clearance evidence only; a blocked shortcut has no asserted no-route result.
    }
    [Theory]
    [InlineData(2f,1u)] [InlineData(10f,0u)]
    public void ClearanceUsesActualTallHeightWithoutFollowHelpersTwoMetreCap(float upper,uint expected)
    {
        var f=MovementOwnerFixture.Create([Wall()]);
        var r=RetailGroundClearance.OneStep(f.Scene,f.State.Actor with {UpperC8=upper},new(31,1,0),new(32,1,0),.001f,0,f.Limits);
        Assert.Equal(expected,r.NativeResult);
    }
    [Fact]
    public void TinyDistanceReturnsBeforeUnconsumedBodyOrCrowdValues()
    {
        var f=MovementOwnerFixture.Create();var a=f.State.Actor with {UpperC8=float.NaN,FrontC0=float.NaN,Crowd310=1};
        var r=RetailGroundClearance.OneStep(f.Scene,a,new(31,1,0),new(31.0001f,1,0),.001f,0,f.Limits);
        Assert.Equal(0u,r.NativeResult);Assert.Empty(r.Geometry);
    }
    [Theory]
    [InlineData(1.5f,0u)] [InlineData(1.5001f,1u)]
    public void Mode1ShortStepHasExactOneAndHalfMetreRiseBoundary(float rise,uint expected)
    {
        var f=MovementOwnerFixture.Create();var a=f.State.Actor with {RadiusBc=2};
        var r=RetailGroundClearance.Walk(f.Scene,a,new(31,1,0),new(31,1,rise),1,0,f.Limits);
        Assert.Equal(expected,r.NativeResult);if(expected==1)Assert.Empty(r.Geometry);
    }
    [Fact]
    public void PhysicalClearDoesNotSubstituteForActiveCrowdOwner()
    {
        var f=MovementOwnerFixture.Create();
        var r=RetailGroundClearance.OneStep(f.Scene,f.State.Actor with {Crowd310=1},new(31,1,0),new(32,1,0),.001f,0,f.Limits);
        Assert.Equal(RetailMovementCalculationStatus.IncompleteAtChild,r.Status);Assert.Null(r.NativeResult);Assert.Equal("moving-crowd",r.Failure!.Boundary);
    }
    [Fact]
    public void GeometryBudgetDoesNotBecomeAClearCorridor()
    {
        var f=MovementOwnerFixture.Create([Wall()]);
        var r=RetailGroundClearance.OneStep(f.Scene,f.State.Actor,new(31,1,0),new(32,1,0),.001f,0,f.Limits with {Geometry=f.Limits.Geometry with {MaxTriangles=0}});
        Assert.False(r.HasNativeOutcome);Assert.Null(r.NativeResult);
    }
    [Theory]
    [InlineData(0)] [InlineData(-1)]
    public void NonprogressMarchSpacingIsUnsupportedAfterComputedBlockedDirectStep(int radius)
    {
        var f=MovementOwnerFixture.Create([Wall()]);
        var r=RetailGroundClearance.Walk(f.Scene,f.State.Actor with {RadiusBc=radius},new(31,1,0),new(33,1,0),0,0,f.Limits);
        Assert.Equal(RetailMovementCalculationStatus.UnsupportedConsumedDomain,r.Status);Assert.Equal("nonprogress-spacing",r.Failure!.Boundary);
    }
    [Fact]
    public void InvalidAnalyticLabel25IsRejectedRatherThanMaskedIntoADispatchIndex()
    {
        var f=MovementOwnerFixture.Create(selector:25);
        var r=RetailGroundClearance.OneStep(f.Scene,f.State.Actor,new(31,1,0),new(32,1,0),.001f,0,f.Limits);
        Assert.False(r.HasNativeOutcome);Assert.Null(r.NativeResult);Assert.Contains("16",r.Failure!.Detail);
    }
    [Fact]
    public void Explicit14UsesActualEligibleMeshFloorAndPinnedMaterialPolicy()
    {
        var f=MovementOwnerFixture.Create(selector:14);
        var ground=RetailCollisionQuery.ProjectGround(f.Scene.Collision,f.Scene.Collision.Stamp,new(32,1,3),f.Limits.Geometry);
        Assert.Equal(RetailCollisionStatus.Computed,ground.Status);Assert.Equal(1u,ground.NativeResult);Assert.Equal(new(32,1,0),ground.Point);
        var r=RetailGroundClearance.OneStep(f.Scene,f.State.Actor,new(31,1,0),new(32,1,0),.001f,0,f.Limits);
        Assert.Equal(0u,r.NativeResult);Assert.NotEmpty(r.Geometry);Assert.All(r.Geometry,g=>Assert.Equal(RetailCollisionStatus.Computed,g.Status));
    }
    [Theory]
    [InlineData("class")] [InlineData("evidence")]
    public void ConsumedBodyFieldsRequireTheirSupportedActorSource(string fault)
    {
        var f=MovementOwnerFixture.Create();var a=fault=="class"?f.State.Actor with {Vtable=0}:f.State.Actor with {EvidenceSha256="unknown"};
        var r=RetailGroundClearance.OneStep(f.Scene,a,new(31,1,0),new(32,1,0),.001f,0,f.Limits);
        Assert.False(r.HasNativeOutcome);Assert.Equal("actor-source",r.Failure!.Boundary);
    }
}
