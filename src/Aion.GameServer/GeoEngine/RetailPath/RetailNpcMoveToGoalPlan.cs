using static Aion.GameServer.GeoEngine.RetailPath.RetailMovementArithmetic;

namespace Aion.GameServer.GeoEngine.RetailPath;

/// <summary>Raw native request arguments. Supplied path is copied, never accepted as a completed search observation.</summary>
public sealed record RetailNpcMoveToGoalRequest(RetailSearchPoint Goal,uint IgnoredEditorId,float SquaredTolerance,
    RetailNpcOwnedMovementPath? SuppliedPath,uint MotionFlags,bool AllowReuse,int NativeSelector,
    RetailGroundQueueKind Queue,int? ObservedHeapRankCapacity);

/// <summary>Inactive complete admitted ground MoveToGoal producer/search/reuse/follow/stage calculation.</summary>
public static class RetailNpcMoveToGoalPlan
{
    public static RetailNpcMovementCalculation Plan(RetailNpcMovementSnapshot input,RetailGroundMovementScene scene,
        RetailNpcMoveToGoalRequest request,RetailMovementLimits limits) =>
        RetailNpcMovementOwnerPlan.Evaluate(input,scene,limits,r=>Goal(r,request),0x140123190);

    internal static uint Goal(RetailNpcMovementReduction r,RetailNpcMoveToGoalRequest request)
    {
        const ulong e=0x140123190;
        if(r.Actor.WorldPointerIdentity is null)return 1;
        if(!RetailNpcMotionPlan.Admit(r))return 4;
        var goal=Point(request.Goal);F(request.SquaredTolerance);
        if(Squared(r.Actor.Position,goal)<Add(request.SquaredTolerance,.0001f))
        {
            if(request.MotionFlags==0){r.Write(e,"motion+34",r.Motion.Waypoint34,0);r.Motion=r.Motion with {Waypoint34=0};}
            return 2;
        }
        bool available;
        if(request.SuppliedPath is null)
        {
            uint start=r.Tick("FindBegin",0x140123491);
            if(Squared(goal,r.Actor.LastFailedGoalA1c)<=2.25f)
            {
                if((r.Globals.DebugFlags1417817c4&0x80)!=0)r.Effect(new(0,RetailNpcMovementEffectKind.DiagnosticLog,e,"Skip repeat failed goal within squared2.25."));
                return 1;
            }
            available=Has(r,ref goal);
            if(!available)
            {
                if(ReuseEnabled(r,request,goal)&&r.Path is {Count:>0})
                {int reuse=Reuse(r,request,goal,r.Path);if(reuse==0)return Follow(r,request,goal);if(reuse==2)return 1;}
                bool found=false;
                if(r.Actor.Master55c==0)
                {found=Find(r,request,goal,true);if(!found)found=Find(r,request,goal,false);}
                else found=Find(r,request,goal,false);
                if(!found)
                {
                    uint end=r.Tick("FindFailureEnd",0x140123923),elapsed=unchecked(end-start);
                    if(elapsed>100)r.Effect(new(0,RetailNpcMovementEffectKind.DiagnosticLog,e,"Native failed path search elapsed>100ms."));
                    uint raw=elapsed>100&&elapsed>r.Globals.MaxPathProcessing14176cb54&&unchecked(r.World.WorldId+0x80000000u)<0x40000000u&&r.Globals.NoRetry14176cb44!=0?5u:1u;
                    r.Write(e,"actor+a1c/a20/a24",r.Actor.LastFailedGoalA1c,goal);r.Actor=r.Actor with {LastFailedGoalA1c=goal};return raw;
                }
                uint finish=r.Tick("FindSuccessEnd",0x1401236c5);
                if(unchecked(finish-start)>100&&r.World.WorldId!=0x17d7ab10)r.Effect(new(0,RetailNpcMovementEffectKind.DiagnosticLog,e,"Native successful path search elapsed>100ms."));
                ResetProgress(r);
            }
        }
        else if(!Has(r,ref goal))
        {
            var supplied=CopyInput(r,request.SuppliedPath);
            if(ReuseEnabled(r,request,goal))
            {int reuse=Reuse(r,request,goal,supplied);if(reuse==0)return Follow(r,request,goal);if(reuse==2)return 1;}
            r.PathAcquire(e);
            if(supplied.Count<1){r.FreePath(e);r.PathRelease(e);return 1;}
            Clone(r,supplied.Points,supplied.Count,0,supplied.Destination,supplied.Extra30c,0x140123190);r.PathRelease(e);
            ResetProgress(r);
        }
        return Follow(r,request,goal);
    }
    private static bool ReuseEnabled(RetailNpcMovementReduction r,RetailNpcMoveToGoalRequest q,RetailSearchPoint goal)=>
        q.AllowReuse&&r.Globals.Reuse1417819f4!=0&&Squared(r.Actor.Position,goal)>=225;
    private static void ResetProgress(RetailNpcMovementReduction r)
    {r.Write(0x140123190,"actor+82c.exchange",r.Actor.Progress82c,0);r.Actor=r.Actor with {Progress82c=0};}
    private static bool Has(RetailNpcMovementReduction r,ref RetailSearchPoint goal)
    {
        r.PathAcquire(0x140124290);var p=r.Path;
        bool yes=p is not null&&p.Count>p.Index&&Near(p.Destination,goal)&&!Near(r.Actor.Position,p.Destination);
        r.PathRelease(0x140124290);if(!yes)return false;
        goal=p!.Destination;r.Child(0x140124290,"HasPathToMove(goal)","nonnull; count>index; metadataNearGoal; source notNearMetadata","goal copies exact stored metadata words");return true;
    }
    internal static RetailNpcOwnedMovementPath CopyInput(RetailNpcMovementReduction r,RetailNpcOwnedMovementPath p)
    {
        if(p.Points.Count>r.Limits.MaxPathPoints)Stop(RetailMovementCalculationStatus.DefensiveLimit,0,"path-copy","Supplied owned point limit.");
        Require(p.Count>=0&&p.Count<=p.Points.Count&&p.Index>=0&&Text(p.StorageIdentity),"path-header","Malformed owned path header.");
        foreach(var x in p.Points)Point(x);Point(p.Destination);
        return p with {Points=Array.AsReadOnly(p.Points.ToArray())};
    }
    private static RetailGroundSearchResult Search(RetailNpcMovementReduction r,RetailNpcMoveToGoalRequest q,
        RetailSearchPoint source,RetailSearchPoint goal,bool rings)
    {
        RetailNpcMotionPlan.Ordinary(r,0x140223f00);
        var policy=new RetailGroundSearchPolicy(r.Scene,r.Actor,rings,q.IgnoredEditorId,r.Limits,r.Value.Ownership.RequestRevision);
        bool usesTables=r.Scene.NativePathTablesAvailable&&Squared(source,goal)<10000;
        var s=usesTables?r.Scene.Lookup(source):null;var g=usesTables?r.Scene.Lookup(goal):null;
        var observation=new RetailGroundSearchObservation(source,goal,r.Scene.Collision.Bounds,
            new(true,false,RetailNpcMotionPlan.V19(r.Actor),RetailNpcMotionPlan.V18(r.Actor),r.Actor.UpperC8),
            q.NativeSelector,q.Queue,q.ObservedHeapRankCapacity,r.Scene.NativePathTablesAvailable,
            new(s is null?RetailSearchSurfaceState.Missing:RetailSearchSurfaceState.Resolved,s),
            new(g is null?RetailSearchSurfaceState.Missing:RetailSearchSurfaceState.Resolved,g),policy.Revision,
            r.Scene.Sources,r.Scene.SurfaceOptions,r.Value.Ownership.Origin==RetailMovementInputOrigin.ConstructedSyntheticWorld);
        var result=RetailGroundPathSearch.Search(observation,policy,r.Limits.Search);
        if(policy.Failure is not null)throw new RetailMovementStop(policy.Failure);
        if(!result.Success&&result.Status!=RetailGroundSearchStatus.NoRoute)
            Stop(result.Status==RetailGroundSearchStatus.DefensiveResourceLimit?RetailMovementCalculationStatus.DefensiveLimit:RetailMovementCalculationStatus.UnsupportedConsumedDomain,
                0x140223f00,"search-consumer",$"{result.Status}/{result.Termination}: {result.Detail}");
        r.Child(0x140223f00,"FindPath computed geometry/policy/search",$"{result.Status}/{result.Termination}; rings={rings}; selector={q.NativeSelector}",
            $"actual hashed path/height/collision; {result.PolicyCalls} computed policy calls; {result.PoppedNodes} pops; {result.Points.Count} points");
        return result;
    }
    private static bool Find(RetailNpcMovementReduction r,RetailNpcMoveToGoalRequest q,RetailSearchPoint goal,bool rings)
    {
        var result=Search(r,q,r.Actor.Position,goal,rings);
        // Native FindPath clears old actor path on both native true and false; managed rejection is not a native false.
        r.PathAcquire(0x140123ca0);r.FreePath(0x140123ca0);
        if(!result.Success)
        {r.PathRelease(0x140123ca0);if(r.Actor.DebugMode8b0 is 1 or 2)r.Incomplete(0x140123ca0,"failed-path-debug-log","Reached v82 name/FileLog diagnostic owner.");return false;}
        Clone(r,result.Points,result.Points.Count,0,goal,null,0x140123ca0,true);r.PathRelease(0x140123ca0);return true;
    }
    private static bool Smooth(RetailNpcMovementReduction r,RetailNpcMoveToGoalRequest q,RetailSearchPoint from,RetailSearchPoint to)
    {
        var p=new RetailGroundSearchPolicy(r.Scene,r.Actor,false,q.IgnoredEditorId,r.Limits,r.Value.Ownership.RequestRevision);
        bool value=p.Smooth(from,to);if(p.Failure is not null)throw new RetailMovementStop(p.Failure);
        r.Child(0x140225430,"computed smoothing",$"returns{value}","directed topology shortcut plus complete computed segment policy");return value;
    }
    private static int Reuse(RetailNpcMovementReduction r,RetailNpcMoveToGoalRequest q,RetailSearchPoint goal,RetailNpcOwnedMovementPath original)
    {
        r.PathAcquire(0x140122b60);var old=CopyInput(r,original);if(old.Count<1){r.PathRelease(0x140122b60);return 1;}
        var last=old.Points[old.Count-1];if(Near(last,goal)){r.PathRelease(0x140122b60);return 0;}
        if(Smooth(r,q,last,goal))
        {
            var points=old.Points.Take(old.Count).Append(goal).ToList();
            Clone(r,points,points.Count,old.Index,goal,old.Extra30c,0x140122b60);
            int remaining=points.Count-old.Index;
            if(remaining<0)Stop(RetailMovementCalculationStatus.UnsupportedConsumedDomain,0x140122960,"reuse-remaining-count","Negative subpath would address before native storage.");
            int first=old.Index,third=first+2,logicalEnd=points.Count,steps=0;
            while(third<logicalEnd)
            {
                if(++steps>r.Limits.MaxSteps)Stop(RetailMovementCalculationStatus.DefensiveLimit,0x140122960,"reuse-simplify-budget","Subpath smoothing budget.");
                if(Smooth(r,q,points[first],points[third]))
                {
                    points.RemoveAt(third-1);logicalEnd--;remaining--;
                    if(third>=logicalEnd)break;
                    // Native narrows through signed16 coords for this late lookup. Admit only the nonwrapping integer domain.
                    var late=points[third];
                    if(late.X<short.MinValue||late.X>short.MaxValue||late.Y<short.MinValue||late.Y>short.MaxValue)
                        Stop(RetailMovementCalculationStatus.UnsupportedConsumedDomain,0x140122960,"reuse-int16-lookup","Late native coordinate narrowing would wrap.");
                    if(r.Scene.Lookup(new((short)late.X,(short)late.Y,late.Z)) is null)break;
                }
                else {first++;third++;}
            }
            var p=r.Path!;var retained=p.Points.ToArray();for(int i=old.Index;i<points.Count;i++)retained[i]=points[i];
            r.Write(0x140122960,"path.count ODD remaining count",p.Count,remaining);
            r.Path=p with {Points=Array.AsReadOnly(retained),Count=remaining,Index=old.Index};
            r.Child(0x140122b60,"reuse direct append/simplify","index preserved; count=remaining rather than index+remaining","native odd transition intentionally visible; consumed tail bytes retained");r.PathRelease(0x140122b60);return 0;
        }
        var tail=Search(r,q,last,goal,false);if(!tail.Success){r.PathRelease(0x140122b60);return 1;}
        var joined=old.Points.Take(old.Count).Concat(tail.Points).ToArray();
        Clone(r,joined,joined.Length,old.Index,goal,old.Extra30c,0x140122b60);
        r.Child(0x140122b60,"reuse searched tail","all old points + all tail points","duplicate tail start and old index preserved; no remaining simplifier on this branch");r.PathRelease(0x140122b60);return 0;
    }
    internal static void Clone(RetailNpcMovementReduction r,IReadOnlyList<RetailSearchPoint> points,int count,int index,
        RetailSearchPoint destination,uint? extra,ulong e,bool nativeFindTransfer=false)
    {
        Require(count>=0&&count<=points.Count,"clone-count","Native positive copy count must fit owned storage.");
        if(count>r.Limits.MaxPathPoints||count>r.Storage.AvailablePathPointSlots)
            Stop(RetailMovementCalculationStatus.DefensiveLimit,e,"path-reservation","Owned path point capacity exhausted; no native allocation false result is fabricated.");
        // All arguments are already owned; release happens after evaluation, avoiding alias loss during reuse.
        var copy=points.Take(count).ToArray();r.FreePath(e);
        string storage=$"{r.Storage.ReservationIdentity}/path/{r.Writes.Count}";
        r.Storage=r.Storage with {UsedPathPointSlots=copy.Length};
        // Clone does not copy +1c. Native FindPath bulk-transfers an adjacent unconsumed word whose bytes are unproved.
        uint? adjacentWord=nativeFindTransfer?null:r.Value.UnconsumedPathWord30c;
        r.Value=r.Value with {UnconsumedPathWord30c=adjacentWord};
        r.Path=new(Array.AsReadOnly(copy),count,0,destination,adjacentWord,storage);
        r.Write(e,"path2f0/header","null",$"{storage}/{count}/0; +30c adjacent word {(adjacentWord is null?"unknown":adjacentWord.ToString())}");
        if(index!=0){r.Write(e,"path.index.restore",0,index);r.Path=r.Path with {Index=index};}
        _=extra; // Retained in raw input only; the actual Clone never copies this adjacent word.
        r.Effect(new(0,RetailNpcMovementEffectKind.ModelAllocation,e,"Calculated reserved path copy; native allocator not executed."));
    }
    private static uint Follow(RetailNpcMovementReduction r,RetailNpcMoveToGoalRequest q,RetailSearchPoint goal)
    {
        var p=r.Path;if(p is null)return 1;
        var o=r.Value.Ownership;
        var epoch=new RetailNpcPathFollowEpoch(o.ActorIdentity,o.RequestRevision,o.ActorLifetime,o.PathRevision,o.WorldIdentity,o.InstanceIdentity,o.SceneRevision,o.AssetRevision);
        string digest=RetailNpcPathFollowObservation.ComputeSnapshotSha256(p.Points,p.Count,p.Index,p.Destination,r.Limits.MaxPathPoints);
        var input=RetailNpcPathFollowObservation.Create(p.Points,p.Count,p.Index,p.Destination,r.Actor.Position,goal,r.Actor.DistanceBudget59c,q.SquaredTolerance,q.IgnoredEditorId,
            new(0x14007dfa0,r.Actor.Pose5a0,r.Actor.Kind32c,r.Actor.UpperC8,r.Actor.Progress82c,true,true,r.Actor.EvidenceSha256),
            epoch,new(p.StorageIdentity,digest,r.Actor.EvidenceSha256,"Calculated owned producer; not runtime path observation"),o.Origin==RetailMovementInputOrigin.ConstructedSyntheticWorld,r.Limits.MaxPathPoints);
        r.PathAcquire(0x140123190);
        var result=RetailNpcMoveByPathPlan.Plan(input,r.Scene.Collision,new(r.Limits.MaxPathPoints,r.Limits.MaxSteps,r.Limits.MaxSteps,r.Limits.MaxOperations,r.Limits.Geometry));
        if(!result.HasNativeOutcome)Stop(result.Status==RetailNpcPathFollowStatus.DefensiveLimit?RetailMovementCalculationStatus.DefensiveLimit:RetailMovementCalculationStatus.UnsupportedConsumedDomain,
            0x140225d80,"follow-consumer",$"{result.Status}: {result.Detail}");
        if(result.FinalProgress is int progress)
        {r.Write(0x140225d80,"actor+82c.consume/set",r.Actor.Progress82c,progress);r.Actor=r.Actor with {Progress82c=progress};}
        r.Child(0x140225d80,"computed WorldMoveByPath",$"raw{result.NativeValue}; localIndex{result.FinalPathIndex}",$"{result.Queries.Count} computed geometry queries; actor header transferred only on raw0");
        uint raw=result.NativeValue!.Value;
        if(raw==0)
        {
            int index=result.FinalPathIndex!.Value;r.Write(0x140123190,"path.index.transfer",p.Index,index);r.Path=p with {Index=index};
            var next=result.NextPosition!.Value;var desired=result.DesiredWaypoint!.Value;
            var angle=RetailNpcNativeHeading.Atan2(BitConverter.SingleToUInt32Bits(Sub(next.Y,r.Actor.Position.Y)),BitConverter.SingleToUInt32Bits(Sub(next.X,r.Actor.Position.X)),r.Scene.FloatingEnvironment);
            if(!angle.Success)Stop(RetailMovementCalculationStatus.UnsupportedConsumedDomain,0x14039d7e0,"native-heading",angle.Status.ToString());
            uint heading=unchecked((uint)RetailNpcNativeHeading.QuantizeMoveToGoal(angle.ReturnWord!.Value));
            // 123b12 TEST AL,2 /123b16 MOV EAX,ESI(0): the set-bit branch stages zero, not the caller word.
            uint flags=(q.MotionFlags&2)!=0?0:!Near(goal,desired)?1:q.MotionFlags;
            r.PathRelease(0x140123190);RetailNpcMotionPlan.Stage(r,desired,next,heading,flags);return 0;
        }
        r.PathRelease(0x140123190);if(raw==2)r.InvalidatePath();
        return raw; // raw1 first-attempt path is retained; raw4 likewise. No invented progress or implicit cancel.
    }
}
