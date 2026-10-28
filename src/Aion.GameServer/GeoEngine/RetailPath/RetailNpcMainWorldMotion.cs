using System.Collections.Immutable;

namespace Aion.GameServer.GeoEngine.RetailPath;

public sealed record RetailMainWorldMotionResult(RetailMainApplyStatus Status,RetailNpcMainActorSnapshot? FinalActor,
    RetailMainWorldDisposition Disposition,ImmutableArray<RetailMainLogicalWrite> CalculatedPrefixWrites,
    ImmutableArray<string> EvaluatedSkips,ulong? IncompleteChild,string? Detail)
{ public bool CanCommitToHost=>false; public bool ActivationReady=>false; }

/// <summary>Inactive Main World::Move/_Move same32m-cell model. Crossings never use a generic dictionary replacement.</summary>
public static class RetailNpcMainWorldMotion
{
    internal static void ValidateDimensions(RetailMainOwnedWorld world,RetailMainApplyLimits limits)
    {
        RetailMainApplyEvidence.Require(limits.MaxCells>0&&limits.MaxWraps>0&&limits.MaxMaterialBytes>0,"Invalid defensive budgets.",RetailMainApplyStatus.BudgetExceeded);
        RetailMainApplyEvidence.Source(world.Source);
        RetailMainApplyEvidence.Require(!string.IsNullOrWhiteSpace(world.Identity)&&world.NativeAddress!=0&&world.WidthMetres>0&&world.HeightMetres>0&&
            world.WidthMetres<=65536&&world.HeightMetres<=65536&&(world.WidthMetres&1)==0&&(world.HeightMetres&1)==0,
            "Finite positive even world dimensions at most65536 required.");
        RetailMainApplyEvidence.Require(world.MovingStride50==(world.WidthMetres+31)/32&&world.RcuStride5c==(world.WidthMetres+63)/64,
            "Explicit native32/64m strides disagree with admitted dimensions.");
        uint[] lo=RetailMainApplyMath.Components(world.Minimum38),hi=RetailMainApplyMath.Components(world.Maximum44);
        for(int i=0;i<3;i++)RetailMainApplyEvidence.Require(RetailMainApplyMath.Float(lo[i])<RetailMainApplyMath.Float(hi[i]),"Invalid half-open XYZ bounds.");
        RetailMainApplyEvidence.Require(RetailMainApplyMath.Float(lo[0])==0&&RetailMainApplyMath.Float(lo[1])==0&&
            RetailMainApplyMath.Float(hi[0])==world.WidthMetres&&RetailMainApplyMath.Float(hi[1])==world.HeightMetres,
            "This finite scene admits zero-origin XY with explicitly matching extents.");
        long count=(long)world.MovingStride50*((world.HeightMetres+31)/32);
        RetailMainApplyEvidence.Require(count<=limits.MaxCells&&world.Wraps.Length<=limits.MaxWraps,"World graph exceeds defensive budget.",RetailMainApplyStatus.BudgetExceeded);
        RetailMainApplyEvidence.Require(count==world.Cells.Length,"Every current moving-cell header must be supplied.");
        RetailMainApplyEvidence.Require(world.Cells.All(c=>c is not null)&&world.Wraps.All(w=>w is not null),"Null current headers/wraps cannot certify ownership.");
    }

    internal static bool InBounds(RetailMainOwnedWorld world,RetailMainVectorWords p)
    {
        uint[] point=RetailMainApplyMath.Components(p),lo=RetailMainApplyMath.Components(world.Minimum38),hi=RetailMainApplyMath.Components(world.Maximum44);
        for(int i=0;i<3;i++){float v=RetailMainApplyMath.Float(point[i]);if(v<RetailMainApplyMath.Float(lo[i])||v>=RetailMainApplyMath.Float(hi[i]))return false;}
        return true;
    }
    internal static int Cell(RetailMainOwnedWorld w,RetailMainVectorWords p)=>checked((RetailMainApplyMath.Truncate32(p.Y)>>5)*w.MovingStride50+(RetailMainApplyMath.Truncate32(p.X)>>5));

    internal static void ValidateGraph(RetailMainOwnedWorld world,RetailNpcMainActorSnapshot actor,RetailMainApplyLimits limits)
    {
        ValidateDimensions(world,limits);
        RetailMainApplyEvidence.Source(actor.Source);
        RetailMainApplyEvidence.Require(actor.Motion is not null&&actor.Template1848 is not null,"Complete actor motion/template descriptor required.");
        RetailMainApplyEvidence.Require(actor.Vtable==RetailNpcMainMotion.NpcVtable&&actor.NativeClass144==2,"Actual admitted NPC vtable/class pair required.",RetailMainApplyStatus.InvalidSource);
        RetailMainApplyEvidence.Require(actor.WorldIdentity==world.Identity&&InBounds(world,actor.Motion.Endpoint120),"Actor current world/endpoint must bind the owned scene.");
        Dictionary<ulong,RetailMainObjectWrap> nodes=[];
        foreach(var node in world.Wraps)
            RetailMainApplyEvidence.Require(node.Address!=0&&node.ActorAddress!=0&&Enum.IsDefined(node.Kind)&&nodes.TryAdd(node.Address,node),"Invalid or duplicate current wrap.");
        HashSet<ulong> seen=[];
        for(int cell=0;cell<world.Cells.Length;cell++)
        {
            var header=world.Cells[cell];ulong[] roots=[header.UserRoot,header.ObjectRoot,header.HousingRoot,header.HouseRoot];
            for(int kind=0;kind<4;kind++)
            {
                ulong at=roots[kind],previous=0;
                while(at!=0)
                {
                    RetailMainApplyEvidence.Require(seen.Add(at)&&nodes.TryGetValue(at,out _),"Missing, cyclic or multiply-owned list node.");
                    var node=nodes[at];
                    RetailMainApplyEvidence.Require(node.Previous==previous&&node.ParentCell==cell&&(int)node.Kind==kind,"Current wrap parent/previous/header mismatch.");
                    previous=at;at=node.Next;
                }
            }
        }
        RetailMainApplyEvidence.Require(seen.Count==nodes.Count,"Unreachable wrap cannot stand in for complete current roots.");
        RetailMainApplyEvidence.Require(nodes.TryGetValue(actor.WrapB8,out var own)&&own.ActorAddress==actor.ObjectAddress&&
            own.Kind==RetailMainWrapKind.Object&&own.ParentCell==Cell(world,actor.Motion.Endpoint120),"Own NPC must remain in its actual object list.");
        RetailMainApplyEvidence.Require(nodes.Values.Count(n=>n.ActorAddress==actor.ObjectAddress)==1,"Own actor has duplicate current membership.");
    }

    public static RetailMainWorldMotionResult Calculate(RetailMainOwnedWorld world,RetailNpcMainActorSnapshot actor,
        RetailMainVectorWords endpoint,int heading,RetailMainApplyLimits? limits=null)
    {
        ArgumentNullException.ThrowIfNull(world);ArgumentNullException.ThrowIfNull(actor);limits??=new();
        List<RetailMainLogicalWrite> writes=[];List<string> skips=[];var disposition=RetailMainWorldDisposition.NotReached;
        try
        {
            ValidateGraph(world,actor,limits);
            var result=Apply(world,actor,endpoint,heading,writes,skips,ref disposition);
            return new(RetailMainApplyStatus.CalculatedFiniteApplication,result,disposition,writes.ToImmutableArray(),skips.ToImmutableArray(),null,null);
        }
        catch(RetailMainApplyFault f){return new(f.Status,null,disposition,writes.ToImmutableArray(),skips.ToImmutableArray(),f.Child,f.Message);}
    }

    internal static RetailNpcMainActorSnapshot Apply(RetailMainOwnedWorld world,RetailNpcMainActorSnapshot actor,
        RetailMainVectorWords endpoint,int heading,List<RetailMainLogicalWrite> writes,List<string> skips,ref RetailMainWorldDisposition disposition)
    {
        if(!InBounds(world,actor.Motion.Endpoint120)||!InBounds(world,endpoint))
        {disposition=RetailMainWorldDisposition.BoundsRejected;skips.Add("World::Move:half-open-bounds-rejected; no rollback of prior MoveNpc stores");return actor;}
        // Actual slots3/4. Admit positive nondebug count with no destroy/diagnostic children.
        if(actor.RefDebug14!=0)RetailMainApplyEvidence.Child(0x140d01f80,"Active reference debug callback is outside this domain.");
        if(actor.RefCount8<=0||actor.RefCount8==int.MaxValue)RetailMainApplyEvidence.Child(0x140d02cc0,"Reference increment could enter diagnostic/destruction/overflow domain.");
        writes.Add(new("actor+8",unchecked((uint)actor.RefCount8),unchecked((uint)(actor.RefCount8+1)),4,0x140d02ce6));
        if(actor.RefSource28==0)
        {
            writes.Add(new("actor+28",0,0x1412712c8,8,0x140d02cf4));
            writes.Add(new("actor+30",actor.RefLine30,0xebd,4,0x140d02cf8));
            actor=actor with { RefSource28=0x1412712c8,RefLine30=0xebd };
        }
        if(Cell(world,actor.Motion.Endpoint120)!=Cell(world,endpoint))
            RetailMainApplyEvidence.Child(0x140d796c0,"Cross32m membership requires actual Remove/PutMovingObject and allocation/reclamation owners.");
        var old=actor.Motion.Endpoint120;
        writes.Add(new("actor+118/world",world.NativeAddress,world.NativeAddress,8,0x140680cb7));
        VectorWrites(writes,"actor+120",old,endpoint,0x140680cc3);
        writes.Add(new("actor+12c",unchecked((uint)actor.Heading12c),unchecked((uint)heading),4,0x140680cdc));
        actor=actor with { Motion=actor.Motion with { Endpoint120=endpoint },Heading12c=heading };
        disposition=RetailMainWorldDisposition.SameCellApplied;
        writes.Add(new("actor+8",unchecked((uint)(actor.RefCount8+1)),unchecked((uint)actor.RefCount8),4,0x140d02e5c));
        skips.Add("_Move:same32m-cell; existing object-list membership retained");
        // Post-move region child reads template before testing the owner.
        RetailMainApplyEvidence.Source(actor.Template1848.Source);
        RetailMainApplyEvidence.Require(actor.Template1848.Address!=0,"Reached template descriptor is not bound.");
        if(actor.Template1848.Raw354==4&&actor.Owner188c>0)
            RetailMainApplyEvidence.Child(0x140d7cf70,"Active region/system-skill processing is unclosed.");
        skips.Add(actor.Template1848.Raw354!=4?"680f30:template+354!=4":"680f30:owner188c<=0");
        if(actor.SpecialBroadcast1a50==1)RetailMainApplyEvidence.Child(0x140681640,"Special world-wide broadcast policy remains outside this domain.");
        if(world.Policy14ac0!=100||world.Policy14ac4!=10000)
            RetailMainApplyEvidence.Child(0x140682020,"Alternate notification policy reaches an unclosed pattern helper.");
        RetailMainApplyEvidence.Require(actor.NativeClass144==2,"Only actual class2 admitted for complete housing no-op.");
        skips.Add("NotifyMoveInHousing:class2 fails class1 active guard");
        RetailMainApplyEvidence.Require(actor.ObserverRange140>=0&&actor.ObserverRange140<=65536,"Observer range outside bounded signed32 rectangle domain.");
        int range=actor.ObserverRange140;
        int ox=RetailMainApplyMath.Truncate32(old.X),oy=RetailMainApplyMath.Truncate32(old.Y);
        int nx=RetailMainApplyMath.Truncate32(endpoint.X),ny=RetailMainApplyMath.Truncate32(endpoint.Y);
        int lowX=System.Math.Min(System.Math.Clamp(ox-range,0,world.WidthMetres),System.Math.Clamp(nx-range,0,world.WidthMetres));
        int lowY=System.Math.Min(System.Math.Clamp(oy-range,0,world.HeightMetres),System.Math.Clamp(ny-range,0,world.HeightMetres));
        int highX=System.Math.Max(System.Math.Clamp(ox+1+range,0,world.WidthMetres),System.Math.Clamp(nx+1+range,0,world.WidthMetres));
        int highY=System.Math.Max(System.Math.Clamp(oy+1+range,0,world.HeightMetres),System.Math.Clamp(ny+1+range,0,world.HeightMetres));
        int visited=0;
        for(int y=lowY>>5;y<(highY+31)>>5;y++)for(int x=lowX>>5;x<(highX+31)>>5;x++)
        {
            int cell=y*world.MovingStride50+x;visited++;
            if(world.Cells[cell].UserRoot!=0)RetailMainApplyEvidence.Child(0x140681de1,"Reached current observer actor/visibility/send owners require expansion; no arbitrary return admitted.");
        }
        skips.Add($"681830:computed{visited}complete empty user roots; own NPC object root retained");
        return actor;
    }

    internal static void VectorWrites(List<RetailMainLogicalWrite> writes,string field,RetailMainVectorWords before,RetailMainVectorWords after,ulong instruction)
    {
        uint[] a=RetailMainApplyMath.Components(before),b=RetailMainApplyMath.Components(after);
        for(int i=0;i<3;i++)writes.Add(new($"{field}[{i}]",a[i],b[i],4,instruction));
    }
}
