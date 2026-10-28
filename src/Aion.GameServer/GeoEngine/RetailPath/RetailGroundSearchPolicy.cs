using static Aion.GameServer.GeoEngine.RetailPath.RetailMovementArithmetic;

namespace Aion.GameServer.GeoEngine.RetailPath;

/// <summary>
/// Computed backend for the frozen native search replay. Caller supplies raw context arguments,
/// owned geometry and actor state, never cost/clear-corridor booleans. Failure stamps withhold values.
/// Includes native2000/200/500 precedence, directed body rings and ordered16m field marcher.
/// </summary>
public sealed class RetailGroundSearchPolicy : IRetailSearchPolicyReplay
{
    private readonly RetailGroundMovementScene scene;
    private readonly RetailNpcMovementActor actor;
    private readonly RetailMovementLimits limits;
    private readonly bool bodyRings;
    private readonly uint ignored;
    private bool validated;
    private long calls;private int fieldVisits,triangles;
    private readonly List<RetailGroundClearanceResult> clearance=[];
    private readonly List<(int X,int Y,uint ObjectId)> fields=[];
    public RetailGroundSearchPolicy(RetailGroundMovementScene scene,RetailNpcMovementActor actor,
        bool nativeBodyRingArgument,uint ignoredEditorId,RetailMovementLimits limits,string revision)
    {this.scene=scene;this.actor=actor;bodyRings=nativeBodyRingArgument;ignored=ignoredEditorId;this.limits=limits;Revision=revision;}
    public string Revision {get;}
    public RetailMovementDomainFailure? Failure {get;private set;}
    public IReadOnlyList<RetailGroundClearanceResult> ClearanceCalls=>clearance.AsReadOnly();
    public IReadOnlyList<(int X,int Y,uint ObjectId)> FieldVisits=>fields.AsReadOnly();
    private RetailSearchReplayStamp Stamp=>new(Failure is null,Revision,"Computed pinned native movement policy; source/current declarations are not runtime verification.");
    private T Compute<T>(Func<T> action,T unsupported)
    {
        if(Failure is not null)return unsupported;
        try
        {
            Ensure();
            Charge(1);
            return action();
        }
        catch(RetailMovementStop ex){Failure=ex.Failure;return unsupported;}
        catch(RetailCollisionStop ex){Failure=new(ex.Status==RetailCollisionStatus.DefensiveLimit?RetailMovementCalculationStatus.DefensiveLimit:RetailMovementCalculationStatus.UnsupportedConsumedDomain,0,"field-geometry",ex.Message);return unsupported;}
    }
    private void Charge(long count)
    {calls+=count;if(calls>limits.MaxOperations)Stop(RetailMovementCalculationStatus.DefensiveLimit,0,"policy-budget","Aggregate computed policy/geometry trace budget exhausted.");}
    private void Ensure()
    {
        if(validated)return;scene.Validate();
        if(actor.Vtable!=RetailNpcMovementOwnerPlan.BaseNpcVtable||!string.Equals(actor.VtableSha256,RetailNpcMovementOwnerPlan.BaseNpcVtableSha256,StringComparison.OrdinalIgnoreCase)||!Digest(actor.EvidenceSha256))
            Stop(RetailMovementCalculationStatus.UnsupportedConsumedDomain,0x140224520,"actor-source","Computed NPC policy requires the individually bound actual supported class and current raw-field evidence.");
        validated=true;
    }
    public RetailSearchCostObservation TransitionCost(RetailPathSurfaceRef source,RetailPathSurfaceRef target)
    {
        float value=Compute(()=>Cost(source,target),0f);return new(Stamp,value);
    }
    public RetailSearchBooleanObservation SameNodeShortcutCapability(RetailPathNode node)
    {
        bool value=Compute(()=>node.Type is 0 or 2,false);return new(Stamp,value);
    }
    public RetailSearchSegmentObservation CheckSegment(RetailSearchPoint source,RetailSearchPoint target,RetailSearchPolicyPurpose purpose)
    {
        var result=Compute(()=>Segment(source,target),(Blocked:true,Endpoint:source));return new(Stamp,result.Blocked,result.Endpoint);
    }
    public RetailSearchBooleanObservation FieldObjectCollision(RetailSearchPoint source,RetailSearchPoint target,RetailSearchPolicyPurpose purpose)
    {
        bool value=Compute(()=>Fields(source,target),false);return new(Stamp,value);
    }
    internal float Cost(RetailPathSurfaceRef from,RetailPathSurfaceRef to)
    {
        Ensure();Charge(1);
        if(!ReferenceEquals(from.Owner,scene.Sources.Path)||!ReferenceEquals(to.Owner,scene.Sources.Path))
            Stop(RetailMovementCalculationStatus.InvalidInput,0x140224520,"foreign-surface","Cost inputs must belong to the bound path.");
        var a=scene.Position(from);var b=scene.Position(to);
        if(Fields(a,b))return 2000;
        if(bodyRings)
        {
            float extent=F(actor.FrontC0);if(extent<=F(actor.SideC4))extent=actor.SideC4;
            int count=Int(Mul(Add(extent,.25f),2),"body-ring-count");
            if(count>limits.MaxSteps)Stop(RetailMovementCalculationStatus.DefensiveLimit,0x140224520,"body-rings","Body-ring walk budget exceeded.");
            foreach(int direction in new[]{0,2,1,3})
            {
                var current=from;
                for(int step=0;step<count;step++)
                {
                    var next=scene.Surfaces.GetCardinalNeighbor(current,direction);
                    if(next.Status is RetailSurfaceQueryStatus.NoLink or RetailSurfaceQueryStatus.OutsideWorld)return 200;
                    if(next.Status!=RetailSurfaceQueryStatus.Found||next.Target is null)
                        Stop(RetailMovementCalculationStatus.UnsupportedConsumedDomain,0x140224520,"body-ring-link",next.Detail??next.Status.ToString());
                    current=next.Target!;
                }
            }
        }
        if(actor.Crowd310!=0||((actor.Pose5a0&0x20)==0&&actor.Crowd314!=0))
            Stop(RetailMovementCalculationStatus.IncompleteAtChild,0x140224830,"proximity-cost","Active proximity/crowd policy requires its separately calculated ordered actor owner.");
        return Mul(Length(a,b),100);
    }
    internal (bool Blocked,RetailSearchPoint Endpoint) Segment(RetailSearchPoint source,RetailSearchPoint target)
    {
        // Native state1 skips topology/area/party/home prechecks, not physical clearance.
        if(actor.AiPointer200 is null||actor.AiState!=1)
            Stop(RetailMovementCalculationStatus.IncompleteAtChild,0x1402257e0,"segment-restriction-children","Only a source-bound current AIstate1 skip is admitted; active area/party/home children are not supplied results.");
        if((actor.Pose5a0&15) is 5 or 6)return(true,source);
        if(RetailGroundClearance.IsAlternate(actor))Stop(RetailMovementCalculationStatus.UnsupportedConsumedDomain,0x1402257e0,"alternate-motion","Ordinary-ground policy cannot substitute for flying/wallpass.");
        var result=RetailGroundClearance.Walk(scene,actor,source,target,0,ignored,limits);
        Charge(1+result.Geometry.Sum(q=>(long)q.Trace.Count));clearance.Add(result);
        if(!result.HasNativeOutcome)throw new RetailMovementStop(result.Failure??new(RetailMovementCalculationStatus.UnsupportedConsumedDomain,0x1402257e0,"clearance","Missing complete clearance result."));
        return(result.NativeResult!=0,result.Endpoint!.Value);
    }
    /// <summary>Entire native shortcut topology predicate, reused by225430/122960.</summary>
    internal bool Shortcut(RetailPathSurfaceRef source,RetailPathSurfaceRef target)
    {
        bool Same(RetailPathSurfaceRef a,RetailPathSurfaceRef b)=>ReferenceEquals(a.Node,b.Node)&&a.NativeId==b.NativeId;
        if(Same(source,target))return true;
        int dx=target.CellX-source.CellX,dy=target.CellY-source.CellY;
        int xd=dx<0?2:0,yd=dy<0?3:1;dx=System.Math.Abs(dx);dy=System.Math.Abs(dy);
        int major,minor,count,error,straight,diagonal;
        if(dx<dy){major=yd;minor=xd;count=dy;error=2*dx-dy;straight=2*dx;diagonal=2*(dx-dy);}
        else {major=xd;minor=yd;count=dx;error=2*dy-dx;straight=2*dy;diagonal=2*(dy-dx);}
        if(count>limits.MaxSteps)Stop(RetailMovementCalculationStatus.DefensiveLimit,0x140225530,"shortcut-budget","Native shortcut step domain exceeds defensive support.");
        var current=source;
        for(int i=0;i<count;i++)
        {
            if(ReferenceEquals(current.Node,target.Node)&&current.Node.Type is 0 or 2)return true;
            RetailPathSurfaceRef? next;
            if(error<1)next=Neighbor(current,major);
            else
            {
                var first=Neighbor(current,major);var firstThen=first is null?null:Neighbor(first,minor);
                var second=Neighbor(current,minor);next=firstThen is null||second is null?null:Neighbor(second,major);
            }
            if(next is null||Cost(current,next)>=500)return false;
            current=next;error+=error<1?straight:diagonal;
        }
        return Same(current,target);
    }
    internal bool Smooth(RetailSearchPoint source,RetailSearchPoint target)
    {
        Ensure();
        if(scene.NativePathTablesAvailable)
        {
            var a=scene.Lookup(source);var b=scene.Lookup(target);
            if(a is null||b is null||!Shortcut(a,b))return false;
        }
        return !Segment(source,target).Blocked;
    }
    private RetailPathSurfaceRef? Neighbor(RetailPathSurfaceRef source,int direction)
    {
        var r=scene.Surfaces.GetCardinalNeighbor(source,direction);
        if(r.Status is RetailSurfaceQueryStatus.NoLink or RetailSurfaceQueryStatus.OutsideWorld)return null;
        if(r.Status!=RetailSurfaceQueryStatus.Found||r.Target is null)Stop(RetailMovementCalculationStatus.UnsupportedConsumedDomain,0x140225530,"shortcut-link",r.Detail??r.Status.ToString());
        return r.Target;
    }
    internal bool Fields(RetailSearchPoint source,RetailSearchPoint target)
    {
        Ensure();Charge(1);
        if(scene.FieldTreePointer is null)Stop(RetailMovementCalculationStatus.IncompleteAtChild,0x1402f4d90,"registered-field-tree","Actual current registered field tree pointer is unknown.");
        if(scene.FieldTreePointer==0)return false; // Exact native early branch, not XML inference.
        Point(source);Point(target);
        if(!scene.Collision.Bounds.Contains(source)||!scene.Collision.Bounds.Contains(target))return false;
        var d=Minus(target,source);float ax=System.MathF.Abs(d.X),ay=System.MathF.Abs(d.Y),sx=16,sy=16;
        int count;
        if(ax<=ay)
        {
            if(ay==0)Stop(RetailMovementCalculationStatus.UnsupportedConsumedDomain,0x1402f4d90,"zero-xy-field-ray","Native zero-XY division/stepping is not normalized to an empty query.");
            int first=Int(source.Y,"field-cell")>>4,last=Int(target.Y,"field-cell")>>4;
            sy=last<first?-16:16;count=System.Math.Abs(last-first)+1;sx=Div(Mul(d.X,16),ay);
        }
        else
        {
            int first=Int(source.X,"field-cell")>>4,last=Int(target.X,"field-cell")>>4;
            sx=last<first?-16:16;count=System.Math.Abs(last-first)+1;sy=Div(Mul(d.Y,16),ax);
        }
        if(count>limits.MaxSteps)Stop(RetailMovementCalculationStatus.DefensiveLimit,0x1402f4d90,"field-march-budget","16m field traversal budget exceeded.");
        float x=source.X,y=source.Y;int previousX=-1,previousY=-1;
        for(int i=0;i<count;i++)
        {
            if(x<scene.Collision.Bounds.Minimum.X||x>=scene.Collision.Bounds.Maximum.X||y<scene.Collision.Bounds.Minimum.Y||y>=scene.Collision.Bounds.Maximum.Y)return true;
            int cx=Int(x,"field-cell")>>4,cy=Int(y,"field-cell")>>4;
            if(Cell(cx,cy))return true;
            if(cx!=previousX&&cy!=previousY&&previousX!=-1)
            {if(Cell(previousX,cy)||Cell(cx,previousY))return true;}
            x=Add(x,sx);y=Add(y,sy);previousX=cx;previousY=cy;
        }
        return false;
        bool Cell(int cx,int cy)
        {
            if(++fieldVisits>limits.MaxOperations)Stop(RetailMovementCalculationStatus.DefensiveLimit,0x1402f4d90,"field-list-budget","Ordered field-list visit budget exceeded.");
            var region=scene.Collision.Region(cx*16,cy*16);
            foreach(var item in region.OrderedObjects)
            {
                if(item.ObjectId==ignored)continue;
                var field=scene.Collision.Field(item); // Exact class/vtable/revision/state is independently bound.
                fields.Add((cx,cy,item.ObjectId));
                if(field.Segment(source,d,0,false,scene.Collision.MaterialPolicy,_=>
                    {if(++triangles>limits.Geometry.MaxTriangles)Stop(RetailMovementCalculationStatus.DefensiveLimit,0x1402f4d90,"field-face-budget","Computed field triangle budget exceeded.");},out _))return true;
            }
            return false;
        }
    }
}
