using Aion.GameServer.GeoEngine.RetailPath;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcMainWorldMotionTests
{
    [Fact]
    public void SameCellPreservesRealOwnMembershipAndBalancesReferenceStores()
    {
        var actor=MainApplicationFixture.Actor();var world=MainApplicationFixture.World();
        var result=RetailNpcMainWorldMotion.Calculate(world,actor,MainApplicationFixture.V(18,4,1),60);
        Assert.Equal(RetailMainApplyStatus.CalculatedFiniteApplication,result.Status);Assert.Equal(RetailMainWorldDisposition.SameCellApplied,result.Disposition);
        var final=Assert.IsType<RetailNpcMainActorSnapshot>(result.FinalActor);
        Assert.Equal(MainApplicationFixture.V(18,4,1),final.Motion.Endpoint120);Assert.Equal(60,final.Heading12c);
        Assert.Equal(actor.RefCount8,final.RefCount8);Assert.Equal(0x1412712c8UL,final.RefSource28);Assert.Equal(0xebdU,final.RefLine30);
        Assert.Equal(new ulong[]{2,1},result.CalculatedPrefixWrites.Where(w=>w.Field=="actor+8").Select(w=>w.After).ToArray());
        Assert.Equal(0x4000UL,world.Cells[0].ObjectRoot);Assert.Equal(actor.WrapB8,Assert.Single(world.Wraps).Address);
        Assert.Contains(result.EvaluatedSkips,s=>s.Contains("own NPC object root retained"));Assert.False(result.CanCommitToHost);
    }

    [Fact]
    public void ExistingReferenceSourceIsCopiedUntouched()
    {
        var a=MainApplicationFixture.Actor() with { RefSource28=0x7777,RefLine30=99 };
        var r=RetailNpcMainWorldMotion.Calculate(MainApplicationFixture.World(),a,MainApplicationFixture.V(18,4,1),60);
        Assert.Equal(0x7777UL,r.FinalActor!.RefSource28);Assert.Equal(99U,r.FinalActor.RefLine30);
        Assert.DoesNotContain(r.CalculatedPrefixWrites,w=>w.Field is "actor+28" or "actor+30");
    }

    [Theory]
    [InlineData(64f,4f,1f)] [InlineData(-1f,4f,1f)] [InlineData(18f,64f,1f)]
    [InlineData(18f,-1f,1f)] [InlineData(18f,4f,64f)] [InlineData(18f,4f,-65f)]
    public void KnownHalfOpenBoundsFailureKeepsEndpointAndMembership(float x,float y,float z)
    {
        var a=MainApplicationFixture.Actor();var r=RetailNpcMainWorldMotion.Calculate(MainApplicationFixture.World(),a,MainApplicationFixture.V(x,y,z),60);
        Assert.Equal(RetailMainApplyStatus.CalculatedFiniteApplication,r.Status);Assert.Equal(RetailMainWorldDisposition.BoundsRejected,r.Disposition);
        Assert.Equal(a,r.FinalActor);Assert.Empty(r.CalculatedPrefixWrites);
    }

    [Theory,InlineData(32f,4f),InlineData(4f,32f),InlineData(63f,63f)]
    public void ActualCrossingsStopAtRemoveInsteadOfClaimingMembership(float x,float y)
    {
        var r=RetailNpcMainWorldMotion.Calculate(MainApplicationFixture.World(),MainApplicationFixture.Actor(),MainApplicationFixture.V(x,y,1),60);
        Assert.Equal(RetailMainApplyStatus.IncompleteAtChild,r.Status);Assert.Equal(0x140d796c0UL,r.IncompleteChild);Assert.Null(r.FinalActor);
        Assert.Contains(r.CalculatedPrefixWrites,w=>w.Field=="actor+8"&&w.After==2);
        Assert.DoesNotContain(r.CalculatedPrefixWrites,w=>w.Field=="actor+12c");
    }

    [Theory,InlineData(0),InlineData(1),InlineData(2),InlineData(3),InlineData(4),InlineData(5),InlineData(6),InlineData(7)]
    public void CompleteRootValidationRejectsCorruptAndHiddenOwnership(int invalid)
    {
        var a=MainApplicationFixture.Actor();RetailMainMovingCell[] cells=[new(0,0x4000,0,0),new(0,0,0,0),new(0,0,0,0),new(0,0,0,0)];
        List<RetailMainObjectWrap> wraps=[new(0x4000,0,0,0x2000,0,RetailMainWrapKind.Object)];
        switch(invalid)
        {
            case 0:cells[0]=cells[0] with { ObjectRoot=0x9999 };break;
            case 1:wraps[0]=wraps[0] with { Next=0x4000 };break;
            case 2:wraps[0]=wraps[0] with { Previous=0x4000 };break;
            case 3:wraps[0]=wraps[0] with { ParentCell=1 };break;
            case 4:wraps[0]=wraps[0] with { Kind=RetailMainWrapKind.User };break;
            case 5:wraps.Add(new(0x5000,0,0,0x3000,1,RetailMainWrapKind.Object));break;
            case 6:cells[1]=cells[1] with { ObjectRoot=0x5000 };wraps.Add(new(0x5000,0,0,0x2000,1,RetailMainWrapKind.Object));break;
            case 7:a=a with { WrapB8=0 };break;
        }
        var r=RetailNpcMainWorldMotion.Calculate(MainApplicationFixture.World(cells:cells,wraps:wraps),a,MainApplicationFixture.V(18,4,1),60);
        Assert.Equal(RetailMainApplyStatus.InvalidOwnership,r.Status);Assert.Null(r.FinalActor);Assert.Empty(r.CalculatedPrefixWrites);
    }

    [Theory,InlineData(0,true),InlineData(3,false)]
    public void ClosedUserRootsDistinguishReachedObserversFromUnconsumedDistantOnes(int cell,bool reached)
    {
        RetailMainMovingCell[] cells=[new(0,0x4000,0,0),new(0,0,0,0),new(0,0,0,0),new(0,0,0,0)];cells[cell]=cells[cell] with { UserRoot=0x5000 };
        RetailMainObjectWrap[] wraps=[new(0x4000,0,0,0x2000,0,RetailMainWrapKind.Object),new(0x5000,0,0,0x3000,cell,RetailMainWrapKind.User)];
        var r=RetailNpcMainWorldMotion.Calculate(MainApplicationFixture.World(cells:cells,wraps:wraps),MainApplicationFixture.Actor(),MainApplicationFixture.V(18,4,1),60);
        Assert.Equal(reached?RetailMainApplyStatus.IncompleteAtChild:RetailMainApplyStatus.CalculatedFiniteApplication,r.Status);
        if(reached){Assert.Null(r.FinalActor);Assert.Equal(0x140681de1UL,r.IncompleteChild);Assert.Contains(r.CalculatedPrefixWrites,w=>w.Field=="actor+12c");}
        else Assert.NotNull(r.FinalActor);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void ActiveSourceChildrenCannotBeSatisfiedByFinalStateInputs(int branch)
    {
        var a=MainApplicationFixture.Actor();var world=MainApplicationFixture.World();ulong child=0;
        switch(branch)
        {
            case 0:a=a with { RefDebug14=1 };child=0x140d01f80;break;
            case 1:a=a with { RefCount8=0 };child=0x140d02cc0;break;
            case 2:a=a with { RefCount8=int.MaxValue };child=0x140d02cc0;break;
            case 3:a=a with { Template1848=a.Template1848 with { Raw354=4 },Owner188c=1 };child=0x140d7cf70;break;
            case 4:a=a with { SpecialBroadcast1a50=1 };child=0x140681640;break;
            case 5:world=MainApplicationFixture.World(policy0:99);child=0x140682020;break;
        }
        var r=RetailNpcMainWorldMotion.Calculate(world,a,MainApplicationFixture.V(18,4,1),60);
        Assert.Equal(RetailMainApplyStatus.IncompleteAtChild,r.Status);Assert.Equal(child,r.IncompleteChild);Assert.Null(r.FinalActor);
    }

    [Fact]
    public void TemplateReadMustBeBoundBeforeOwnerlessSkip()
    {
        var a=MainApplicationFixture.Actor();a=a with { Template1848=a.Template1848 with { Address=0 },Owner188c=0 };
        var r=RetailNpcMainWorldMotion.Calculate(MainApplicationFixture.World(),a,MainApplicationFixture.V(18,4,1),60);
        Assert.Equal(RetailMainApplyStatus.InvalidOwnership,r.Status);Assert.Null(r.FinalActor);
        Assert.Contains(r.CalculatedPrefixWrites,w=>w.Field=="actor+12c");
    }

    [Theory,InlineData(0),InlineData(-1)]
    public void Template4OwnerNonpositiveProducesCalculatedRegionNoop(int owner)
    {
        var a=MainApplicationFixture.Actor();a=a with { Template1848=a.Template1848 with { Raw354=4 },Owner188c=owner };
        var r=RetailNpcMainWorldMotion.Calculate(MainApplicationFixture.World(),a,MainApplicationFixture.V(18,4,1),60);
        Assert.Equal(RetailMainApplyStatus.CalculatedFiniteApplication,r.Status);Assert.Contains("680f30:owner188c<=0",r.EvaluatedSkips);
    }

    [Fact]
    public void DefensiveGraphBudgetIsNotAClaimOfNativeCapacity()
    {
        var r=RetailNpcMainWorldMotion.Calculate(MainApplicationFixture.World(),MainApplicationFixture.Actor(),MainApplicationFixture.V(18,4,1),60,new(MaxCells:1));
        Assert.Equal(RetailMainApplyStatus.BudgetExceeded,r.Status);Assert.Null(r.FinalActor);Assert.Empty(r.CalculatedPrefixWrites);
    }
}
