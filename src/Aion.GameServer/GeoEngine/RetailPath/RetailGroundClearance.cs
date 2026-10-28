using static Aion.GameServer.GeoEngine.RetailPath.RetailMovementArithmetic;

namespace Aion.GameServer.GeoEngine.RetailPath;

public sealed record RetailGroundClearanceResult(RetailMovementCalculationStatus Status, uint? NativeResult,
    RetailSearchPoint? Endpoint, IReadOnlyList<RetailCollisionQueryResult> Geometry,
    RetailMovementDomainFailure? Failure)
{ public bool HasNativeOutcome=>NativeResult is not null&&Failure is null; public bool RuntimeVerified=>false; }

/// <summary>
/// Complete admitted ordinary-ground one-step1402f2fe0/direct walk1402f3ab0.
/// Geometry is computed. Active unimplemented moving-crowd owner stops at its reached boundary.
/// Native tiny epsilon, hard0.0316, body extension and ground-step rules are distinct.
/// </summary>
public static class RetailGroundClearance
{
    public static RetailGroundClearanceResult OneStep(RetailGroundMovementScene scene,RetailNpcMovementActor actor,
        RetailSearchPoint source,RetailSearchPoint target,float epsilon,uint? ignoredEditorId,RetailMovementLimits? limits=null)=>
        Run(scene,actor,source,target,epsilon,ignoredEditorId,0,false,limits??new());
    public static RetailGroundClearanceResult Walk(RetailGroundMovementScene scene,RetailNpcMovementActor actor,
        RetailSearchPoint source,RetailSearchPoint target,int mode,uint? ignoredEditorId,RetailMovementLimits? limits=null)=>
        Run(scene,actor,source,target,.001f,ignoredEditorId,mode,true,limits??new());
    private static RetailGroundClearanceResult Run(RetailGroundMovementScene scene,RetailNpcMovementActor actor,
        RetailSearchPoint source,RetailSearchPoint target,float epsilon,uint? ignored,int mode,bool walk,RetailMovementLimits limits)
    {
        List<RetailCollisionQueryResult> queries=[];long traceEntries=0,totalTriangles=0;
        try
        {
            scene.Validate();Point(source);Point(target);F(epsilon);
            if(limits.MaxSteps<1)Stop(RetailMovementCalculationStatus.DefensiveLimit,0x1402f3ab0,"step-budget","Positive step support budget required.");
            if(mode is not 0 and not 1)Stop(RetailMovementCalculationStatus.UnsupportedConsumedDomain,0x1402f3ab0,"walk-mode","Only explicit native modes0/1 are admitted.");
            (bool blocked,RetailSearchPoint end) result=walk?WalkCore():Step(source,target,epsilon);
            return new(RetailMovementCalculationStatus.CalculatedApplicationChain,result.blocked?1u:0u,result.end,queries.AsReadOnly(),null);
        }
        catch(RetailMovementStop ex){return new(ex.Failure.Status,null,null,queries.AsReadOnly(),ex.Failure);}
        catch(RetailCollisionStop ex)
        {return new(RetailMovementCalculationStatus.UnsupportedConsumedDomain,null,null,queries.AsReadOnly(),new(RetailMovementCalculationStatus.UnsupportedConsumedDomain,0,"computed-geometry",ex.Message));}
        (bool,RetailSearchPoint) Step(RetailSearchPoint a,RetailSearchPoint b,float tiny)
        {
            if(Squared(a,b)<Mul(tiny,tiny))return(false,b); // Before actor body/crowd reads.
            if(actor.Vtable!=RetailNpcMovementOwnerPlan.BaseNpcVtable||!string.Equals(actor.VtableSha256,RetailNpcMovementOwnerPlan.BaseNpcVtableSha256,StringComparison.OrdinalIgnoreCase)||!Digest(actor.EvidenceSha256))
                Stop(RetailMovementCalculationStatus.UnsupportedConsumedDomain,0x1402f2fe0,"actor-source","Consumed body fields require the actual supported NPC class and current raw-field receipt.");
            float h=F(actor.UpperC8);if(h<1.5f)h=1.5f;
            float extent=F(actor.FrontC0);if(extent<=F(actor.SideC4))extent=actor.SideC4;
            if(extent<0)Stop(RetailMovementCalculationStatus.UnsupportedConsumedDomain,0x1402f2fe0,"actor-bounds","Negative constructed horizontal extent is unadmitted.");
            var lifted=Point(a with{Z=Add(a.Z,h)});var end=Point(b with{Z=Add(b.Z,h)});var delta=Minus(end,lifted);
            float distance=Length(lifted,end);var unit=distance>0?new RetailSearchPoint(Mul(delta.X,Div(1,distance)),Mul(delta.Y,Div(1,distance)),Mul(delta.Z,Div(1,distance))):delta;
            var extended=Along(end,unit,extent);
            var q=RetailCollisionQuery.SegmentBlocked(scene.Collision,scene.Collision.Stamp,lifted,extended,0,0,
                BitConverter.UInt32BitsToSingle(0x3d016f00),limits.Geometry);
            bool hit=Query(q);
            if(hit&&actor.TemplateIdA8 is 216520 or 216580)
            {
                var contact=RetailCollisionQuery.FirstContact(scene.Collision,scene.Collision.Stamp,lifted,end,0,
                    BitConverter.UInt32BitsToSingle(0x3d016f00),limits.Geometry);
                hit=Query(contact);
                if(hit)Stop(RetailMovementCalculationStatus.IncompleteAtChild,0x1402f2fe0,"special-template-diagnostic","Reached special contact/name/log child requires its actual source-bound diagnostic owner.");
            }
            if(hit)return(true,a);
            if(ignored is not null&&actor.Kind88==2)
            {
                // Exact native early guard, not a caller-supplied no-collision result.
                if(actor.Crowd310!=0||((actor.Pose5a0&0x20)==0&&actor.Crowd314!=0))
                    Stop(RetailMovementCalculationStatus.IncompleteAtChild,0x1402f2970,"moving-crowd","Active crowd/time owner is not admitted by this finite ordinary transaction.");
            }
            return(false,b);
        }
        (bool,RetailSearchPoint) WalkCore()
        {
            float squared=Squared(source,target);if(squared<Mul(epsilon,epsilon))return(false,target);
            float distance=F(System.MathF.Sqrt(squared));float spacing=Mul(F(actor.RadiusBc),2);
            if(mode==0)
            {
                var direct=Step(source,target,.001f);if(!direct.Item1||distance<=spacing)return direct;
            }
            else if(distance<=spacing)
                return target.Z<=Add(source.Z,1.5f)?Step(source,target,.001f):(true,source);
            if(!(spacing>0))Stop(RetailMovementCalculationStatus.UnsupportedConsumedDomain,0x1402f3ab0,"nonprogress-spacing","Nonpositive native spacing would not make safe bounded progress.");
            float fraction=Div(spacing,distance);var full=Minus(target,source);
            float dx=Mul(full.X,fraction),dy=Mul(full.Y,fraction),highest=target.Z<source.Z?source.Z:target.Z;
            var last=source;float x=source.X,y=source.Y,z=source.Z,passed=0;
            for(int count=0;;count++)
            {
                if(count>=limits.MaxSteps)Stop(RetailMovementCalculationStatus.DefensiveLimit,0x1402f3ab0,"step-budget","Native walk support loop exceeded its configured defensive budget.");
                float nextPassed=Add(passed,spacing);if(!(nextPassed>passed))Stop(RetailMovementCalculationStatus.UnsupportedConsumedDomain,0x1402f3ab0,"nonprogress-spacing","Binary32 spacing ceased to advance.");passed=nextPassed;
                if(distance<=passed)return Add(last.Z,1.5f)<target.Z?(true,last):Step(last,target,.001f);
                y=Add(y,dy);x=Add(x,dx);
                if(IsAlternate(actor))Stop(RetailMovementCalculationStatus.UnsupportedConsumedDomain,0x1402f3ab0,"alternate-motion","Alternate/flying actor must use its separately owned native consumer.");
                var projection=RetailCollisionQuery.ProjectGround(scene.Collision,scene.Collision.Stamp,new(x,y,Add(highest,1)),limits.Geometry);
                bool support=Query(projection);if(!support)return(true,last);
                z=projection.Point!.Value.Z;if(Add(last.Z,1.5f)<z)return(true,last);
                var next=new RetailSearchPoint(x,y,z);var stepped=Step(last,next,.001f);if(stepped.Item1)return stepped;last=stepped.Item2;
            }
        }
        bool Query(RetailCollisionQueryResult q)
        {
            traceEntries+=q.Trace.Count;totalTriangles+=q.Triangles;
            if(queries.Count>=limits.MaxOperations||traceEntries>limits.MaxOperations||totalTriangles>limits.Geometry.MaxTriangles)
                Stop(RetailMovementCalculationStatus.DefensiveLimit,0,"clearance-total-budget","Total owned clearance query/trace/triangle budget exhausted.");
            queries.Add(q);
            if(q.Status!=RetailCollisionStatus.Computed||q.NativeResult is null)
                Stop(q.Status==RetailCollisionStatus.DefensiveLimit?RetailMovementCalculationStatus.DefensiveLimit:RetailMovementCalculationStatus.UnsupportedConsumedDomain,0,"computed-geometry",q.Detail??q.Status.ToString());
            return q.NativeResult!=0;
        }
    }
    internal static bool IsAlternate(RetailNpcMovementActor a)=>(a.Pose5a0&15) is 3 or 13 or 14||a.Kind32c==0x10;
}
