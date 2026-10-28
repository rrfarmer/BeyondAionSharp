using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Aion.GameServer.GeoEngine.RetailPath;

/// <summary>
/// Complete inactive admitted Main MoveNpc composition. Constructed serialized class2 worlds only;
/// every reached game child is calculated or field-proved inactive. No host apply, timer, IO or4.8 adapter.
/// </summary>
public static class RetailNpcMainMotionApplication
{
    public const string ApplicationEvidenceSha256=RetailMainApplyEvidence.ApplicationSha;
    public const string GuardEvidenceSha256=RetailMainApplyEvidence.GuardSha;
    public const string StopEvidenceSha256=RetailMainApplyEvidence.StopSha;
    public const ulong GetterTickSite=0x140b1802e,DueTickSite=0x140b1d50a,StartTickSite=0x140b1d54b;
    public const ulong ResetDueTickSite=0x140b182e4,ResetStartTickSite=0x140b182f0;

    public static string HashCanonicalRecord(ReadOnlySpan<byte> bytes)=>RetailMainApplyEvidence.Hash(bytes);
    public static string HashGlobals(RetailMainGlobals globals)=>RetailMainApplyEvidence.HashObject(globals);

    public static RetailMainApplyResult Calculate(ReadOnlySpan<byte> canonicalRecord,RetailNpcMainActorSnapshot actor,
        RetailMainOwnedWorld? world,RetailMainGlobals globals,IReadOnlyList<RetailMainTickReceipt> ticks,
        RetailMainApplyContext context,RetailMainApplyLimits? limits=null)
    {
        ArgumentNullException.ThrowIfNull(actor);ArgumentNullException.ThrowIfNull(globals);ArgumentNullException.ThrowIfNull(ticks);ArgumentNullException.ThrowIfNull(context);
        limits??=new();List<RetailMainLogicalWrite> writes=[];List<string> skips=[];
        var disposition=RetailMainWorldDisposition.NotReached;HashSet<ulong> consumed=[];
        ImmutableArray<RetailMainTickReceipt> receipts=[];
        RetailMainApplyResult Failure(RetailMainApplyStatus status,string detail,ulong? child=null)=>new(status,null,null,null,[],disposition,writes.ToImmutableArray(),skips.ToImmutableArray(),child,detail);
        RetailMainApplyResult Complete(RetailMainApplyStatus status,RetailNpcMainActorSnapshot value,ImmutableArray<byte> packet)
        {
            RetailMainApplyEvidence.Require(consumed.Count==receipts.Length,"Unused tick receipts cannot become evidence for an unreached clock read.");
            return new(status,value,world,globals,packet,disposition,writes.ToImmutableArray(),skips.ToImmutableArray(),null,null);
        }
        uint Tick(ulong site)
        {
            var matches=receipts.Where(r=>r.CallSite==site).ToArray();
            RetailMainApplyEvidence.Require(matches.Length==1&&consumed.Add(site),"Missing, duplicate or repeated original clock receipt.");
            var tick=matches[0];RetailMainApplyEvidence.Source(tick.Source);
            RetailMainApplyEvidence.Require(tick.Origin==context.Origin&&tick.ActorLifetime==context.ActorLifetime&&
                tick.SnapshotRevision==context.SnapshotRevision&&tick.OperationRevision==context.OperationRevision,"Clock actor/snapshot/operation revision mismatch.");
            return tick.Value;
        }
        void Depth(ulong instruction)
        {
            uint before=globals.TlsDepth60;globals=globals with { TlsDepth60=unchecked(before-1) };
            writes.Add(new("TLS+60",before,globals.TlsDepth60,4,instruction));
        }
        try
        {
            RetailMainApplyEvidence.Require(context.MainBinarySha256==RetailNpcMainMotion.MainBinarySha256&&
                context.BridgeEvidenceSha256==RetailNpcMainMotion.GetterEvidenceSha256&&context.ApplicationEvidenceSha256==ApplicationEvidenceSha256&&
                context.GuardEvidenceSha256==GuardEvidenceSha256&&context.StopEvidenceSha256==StopEvidenceSha256,
                "Pinned complete Main source/evidence identities required.",RetailMainApplyStatus.InvalidSource);
            RetailMainApplyEvidence.Require(context.Origin==RetailMainApplicationOrigin.ConstructedSyntheticWorld,
                "Only a constructed synthetic world is admitted; declarations are never observed-current proof.",RetailMainApplyStatus.InvalidSource);
            RetailMainApplyEvidence.Require(!string.IsNullOrWhiteSpace(context.ActorLifetime)&&!string.IsNullOrWhiteSpace(context.SnapshotRevision)&&
                !string.IsNullOrWhiteSpace(context.OperationRevision),"Explicit actor/operation/snapshot generations required.");
            RetailMainApplyEvidence.Require(actor.Motion is not null&&actor.Suppression1a58 is not null&&actor.Template1848 is not null&&
                !actor.MaterialTimers.IsDefault&&actor.MaterialTimers.Length==4&&actor.ObjectAddress!=0,"Complete selected actor storage required.");
            RetailMainApplyEvidence.Source(actor.Source);RetailMainApplyEvidence.Source(globals.Source);
            RetailMainApplyEvidence.Require(actor.SnapshotSha256==context.ActorSnapshotSha256&&HashGlobals(globals)==context.GlobalsSnapshotSha256&&
                (world?.SnapshotSha256??RetailMainApplyEvidence.Hash([]))==context.WorldSnapshotSha256&&
                HashCanonicalRecord(canonicalRecord)==context.CanonicalRecordSha256,"Owned snapshots/record do not match declared input digests.",RetailMainApplyStatus.InvalidSource);
            RetailMainApplyEvidence.Require(ticks.Count<=3,"At most three clocks exist in this admitted domain.");
            receipts=ticks.ToImmutableArray();
            RetailMainApplyEvidence.Require(receipts.All(r=>r is not null)&&receipts.Select(r=>r.CallSite).Distinct().Count()==receipts.Length,
                "Clock receipts must be distinct and nonnull.");
            var decoded=RetailNpcInterserverMoveCodec.DecodeCanonical(canonicalRecord);
            RetailMainApplyEvidence.Require(decoded.Record is not null,"Outside conservative canonical producer layout; this is not a native OnRead rejection.",RetailMainApplyStatus.InvalidSource);
            var move=decoded.Record!;
            RetailMainApplyEvidence.Require(move.ObjectId==actor.ObjectId,"Known-actor binding does not match interserver objectID.");
            if(RetailNpcInterserverMoveCodec.EvaluateKnownActorGuards(move,actor.Counter19f8)==RetailMainMoveGuardResult.HeadingRejected)
                return Complete(RetailMainApplyStatus.CalculatedNativeEarlyReturn,actor,[]);
            if(globals.Telemetry14275eb74!=0)RetailMainApplyEvidence.Child(0x140b1cd91,"MoveNpc telemetry branch needs secondary globals/TLS/guard owners before any null-world check.");
            if(actor.WorldIdentity is null)
            {RetailMainApplyEvidence.Require(world is null,"Null-world actor cannot bind a different provided scene.");return Complete(RetailMainApplyStatus.CalculatedNativeEarlyReturn,actor,[]);}
            RetailMainApplyEvidence.Require(world is not null&&world.Identity==actor.WorldIdentity,"Current world identity missing or mismatched.");
            if(actor.LockBypass700==0)RetailMainApplyEvidence.Child(0x140041530,"Active critical-section owner requires host integration.");
            if(globals.SerializationDebug142767930!=0)RetailMainApplyEvidence.Child(0x140478950,"Active lock-serialization debug state is not calculated by a callback tape.");
            skips.Add("actor+700!=0:critical-section bypass; lock-debug/telemetry globals0; no live-lock claim");
            if((byte)actor.Counter19f8!=move.Sequence)
            {Depth(0x140b1d6a1);return Complete(RetailMainApplyStatus.CalculatedNativeEarlyReturn,actor,[]);}
            RetailMainApplyEvidence.Require(context.NearestEven&&context.ExceptionsMasked&&context.Sse2&&!context.FlushToZero&&!context.DenormalsAreZero,
                "Declared RNE/masked/SSE2/FTZ-DAZ-off domain required.",RetailMainApplyStatus.UnsupportedFloatingEnvironment);
            RetailMainApplyEvidence.Require(actor.NativeClass144==2&&actor.Vtable==RetailNpcMainMotion.NpcVtable&&actor.Heading12c>=0&&actor.Heading12c<=119,
                "Actual class2 and bounded current heading required.");
            RetailNpcMainWorldMotion.ValidateGraph(world!,actor,limits);
            RetailNpcMainMaterialQuery.ValidateStorage(world!,limits);
            uint getterTick=Tick(GetterTickSite);
            var getterContext=new RetailNpcMainMotionContext(context.MainBinarySha256,context.BridgeEvidenceSha256,RetailNpcMainMotion.NpcVtable,
                context.ActorLifetime,context.SnapshotRevision,context.OperationRevision,context.ActorSnapshotSha256,
                context.NearestEven,context.ExceptionsMasked,context.FlushToZero,context.DenormalsAreZero,context.Sse2,true);
            var getterReceipt=new RetailNpcMainGetterTick(getterTick,GetterTickSite,context.ActorLifetime,context.SnapshotRevision,
                context.OperationRevision,receipts.Single(r=>r.CallSite==GetterTickSite).Source.Sha256,true);
            var location=RetailNpcMainMotion.Calculate(actor.Motion,getterReceipt,getterContext);
            RetailMainApplyEvidence.Require(location.Calculated&&location.Location is not null,"Old effective location: "+location.Detail,RetailMainApplyStatus.UnsupportedConsumedValue);
            var oldPose=location.Location!.Value;
            var resolution=RetailNpcInterserverMoveCodec.ResolveContinuingGoal(move,actor.Motion.ContinuingGoal19fc);
            RetailMainApplyEvidence.Require(resolution.Available&&resolution.Goal is not null&&resolution.Duration is not null,"Missing Main goal/duration resolution.");
            var goal=resolution.Goal!.Value;uint duration=resolution.Duration!.Value;
            uint[] delta=new uint[3],velocity=new uint[3];long turn=0;
            bool turnActive=false;
            if(RetailMainApplyMath.Equal(oldPose,move.Position)||duration==0)
            {
                duration=0;goal=move.Position;
                if(location.PastDue!=0)oldPose=move.Position;
            }
            else
            {
                uint[] stop=RetailMainApplyMath.Components(move.Position),current=RetailMainApplyMath.Components(oldPose);
                uint durationFloat=RetailMainApplyMath.Integer(duration);
                delta[0]=RetailMainApplyMath.Sub(stop[0],current[0]);delta[1]=RetailMainApplyMath.Sub(stop[1],current[1]);
                uint seconds=RetailMainApplyMath.Div(durationFloat,0x447a0000);
                delta[2]=RetailMainApplyMath.Sub(stop[2],current[2]);
                if(RetailMainApplyMath.Float(seconds)!=0)for(int i=0;i<3;i++)velocity[i]=RetailMainApplyMath.Div(delta[i],seconds);
                if((move.Flags&2)!=0)
                {
                    float rate=RetailMainApplyMath.Float(actor.TurnRate7c8);
                    turnActive=rate>=RetailMainApplyMath.Float(0x38d1b717)||rate<=RetailMainApplyMath.Float(0xb8d1b717);
                    if(turnActive)
                    {
                        int difference=System.Math.Abs(actor.Heading12c-move.Heading);
                        uint delay=RetailMainApplyMath.Div(RetailMainApplyMath.Mul(RetailMainApplyMath.Mul(RetailMainApplyMath.SignedInteger(difference),actor.TurnRate7c8),0x447a0000),0x42f00000);
                        turn=RetailMainApplyMath.Truncate64(delay);
                        uint remaining=unchecked(duration-(uint)turn);
                        seconds=RetailMainApplyMath.Div(RetailMainApplyMath.Integer(remaining),0x447a0000);
                        if(RetailMainApplyMath.Float(seconds)==0)Array.Clear(velocity);
                        else for(int i=0;i<3;i++)velocity[i]=RetailMainApplyMath.Div(delta[i],seconds);
                    }
                }
            }
            // bd120 uses the optional pointee, never an arbitrary suppression result.
            var suppression=actor.Suppression1a58;
            RetailMainApplyEvidence.Require(suppression.Pointer==0?suppression.Word is null:suppression.Word.HasValue,"Suppression pointer/pointee storage is incomplete.");
            uint suppressed=suppression.Pointer==0?0:suppression.Word!.Value;
            Depth(0x1400bd1b3);
            ImmutableArray<byte> packet=[];
            if(suppressed==0)
            {
                bool duplicate=move.Flags==8||move.Flags==0&&(actor.Flags1c35&8)==0&&RetailMainApplyMath.Near(move.Position,actor.Motion.ContinuingGoal19fc)||
                    move.Flags!=0&&move.Flags!=8&&move.Flags==actor.Flags1c35&&RetailMainApplyMath.Near(goal,actor.Motion.ContinuingGoal19fc);
                if(!duplicate)packet=BuildClientPacket(actor.ObjectId,oldPose,move.Heading,move.Flags,goal);
                else skips.Add("client packet suppressed by calculated native duplicate branch");
            }
            else skips.Add("bd120:owned nonzero suppression word; motion continues");
            if(actor.TraceOwner1ba0!=0)RetailMainApplyEvidence.Child(0x140363af0,"Active trace owner introduces lookup/packet/log/current-location children.");
            skips.Add("trace owner1ba0=0");
            uint due=unchecked(Tick(DueTickSite)+duration);
            var beforeMotion=actor.Motion;
            var motion=beforeMotion with { Velocity186c=RetailMainApplyMath.Vector(velocity),Due1858=due,PriorEndpoint1860=beforeMotion.Endpoint120 };
            // Native logical store order: velocityXY, due, priorXY, velocityZ, priorZ.
            writes.Add(new("actor+186c",beforeMotion.Velocity186c.X,velocity[0],4,0x140b1d51f));
            writes.Add(new("actor+1870",beforeMotion.Velocity186c.Y,velocity[1],4,0x140b1d51f));
            writes.Add(new("actor+1858",beforeMotion.Due1858,due,4,0x140b1d527));
            writes.Add(new("actor+1860",beforeMotion.PriorEndpoint1860.X,beforeMotion.Endpoint120.X,4,0x140b1d534));
            writes.Add(new("actor+1864",beforeMotion.PriorEndpoint1860.Y,beforeMotion.Endpoint120.Y,4,0x140b1d534));
            writes.Add(new("actor+1874",beforeMotion.Velocity186c.Z,velocity[2],4,0x140b1d53c));
            writes.Add(new("actor+1868",beforeMotion.PriorEndpoint1860.Z,beforeMotion.Endpoint120.Z,4,0x140b1d545));
            uint start=Tick(StartTickSite);motion=motion with { Start185c=start };
            writes.Add(new("actor+185c",beforeMotion.Start185c,start,4,0x140b1d573));
            // The late raw read is stable because every admitted intervening child was calculated NOP.
            if((move.Flags&2)!=0)
            {
                float rate=RetailMainApplyMath.Float(actor.TurnRate7c8);
                turnActive=rate>=RetailMainApplyMath.Float(0x38d1b717)||rate<=RetailMainApplyMath.Float(0xb8d1b717);
                if(turnActive)
                {
                    uint previous=start;start=unchecked(start+(uint)turn);uint previousDue=due;due=unchecked(start+duration);
                    writes.Add(new("actor+185c",previous,start,4,0x140b1d5b7));writes.Add(new("actor+1858",previousDue,due,4,0x140b1d5c0));
                    motion=motion with { Start185c=start,Due1858=due };
                }
            }
            RetailNpcMainWorldMotion.VectorWrites(writes,"actor+19fc",beforeMotion.ContinuingGoal19fc,goal,0x140b1d5d2);
            writes.Add(new("actor+1c35",actor.Flags1c35,(byte)(move.Flags&0xed),1,0x140b1d601));
            actor=actor with { Motion=motion with { ContinuingGoal19fc=goal },Flags1c35=(byte)(move.Flags&0xed) };
            actor=RetailNpcMainWorldMotion.Apply(world!,actor,move.Position,move.Heading,writes,skips,ref disposition);
            var (material,selector)=RetailNpcMainMaterialQuery.Calculate(world!,actor.Motion.Endpoint120);
            writes.Add(new("actor+1426",actor.Material1426,material,1,0x140b1d651));actor=actor with { Material1426=material };
            var effects=RetailNpcMainMaterialQuery.EvaluateEffects(world!,actor,material);
            if(!effects.Computed)RetailMainApplyEvidence.Child(effects.IncompleteChild??0x14037bf80,effects.Detail??"Incomplete material owner.");
            skips.Add($"material query selector{selector}:computed byte{material}");skips.AddRange(effects.EvaluatedSkips);
            Depth(0x140b1d6a1);skips.Add("telemetry local inactive:checked187-byte release NOP");
            return Complete(RetailMainApplyStatus.CalculatedFiniteApplication,actor,packet);
        }
        catch(RetailMainApplyFault f){return Failure(f.Status,f.Message,f.Child);}
    }

    /// <summary>Complete admitted NPC slot21 reset218-byte body; distinct original ticks, raw endpoint copied without arithmetic.</summary>
    public static RetailMainApplyResult Reset(RetailNpcMainActorSnapshot actor,RetailMainGlobals globals,
        IReadOnlyList<RetailMainTickReceipt> ticks,RetailMainApplyContext context)
    {
        ArgumentNullException.ThrowIfNull(actor);ArgumentNullException.ThrowIfNull(globals);ArgumentNullException.ThrowIfNull(ticks);ArgumentNullException.ThrowIfNull(context);
        List<RetailMainLogicalWrite> writes=[];
        try
        {
            RetailMainApplyEvidence.Require(context.MainBinarySha256==RetailNpcMainMotion.MainBinarySha256&&context.BridgeEvidenceSha256==RetailNpcMainMotion.GetterEvidenceSha256&&
                context.ApplicationEvidenceSha256==ApplicationEvidenceSha256&&context.GuardEvidenceSha256==GuardEvidenceSha256&&context.StopEvidenceSha256==StopEvidenceSha256,
                "Pinned reset/class evidence required.",RetailMainApplyStatus.InvalidSource);
            RetailMainApplyEvidence.Require(context.Origin==RetailMainApplicationOrigin.ConstructedSyntheticWorld&&actor.NativeClass144==2&&actor.Vtable==RetailNpcMainMotion.NpcVtable&&actor.Motion is not null&&
                !actor.MaterialTimers.IsDefault&&actor.MaterialTimers.Length==4&&
                !string.IsNullOrWhiteSpace(context.ActorLifetime)&&!string.IsNullOrWhiteSpace(context.SnapshotRevision)&&!string.IsNullOrWhiteSpace(context.OperationRevision),"Constructed class2 reset ownership required.");
            RetailMainApplyEvidence.Source(actor.Source);RetailMainApplyEvidence.Source(globals.Source);
            RetailMainApplyEvidence.Require(actor.SnapshotSha256==context.ActorSnapshotSha256&&HashGlobals(globals)==context.GlobalsSnapshotSha256&&
                context.CanonicalRecordSha256==HashCanonicalRecord([])&&context.WorldSnapshotSha256==HashCanonicalRecord([]),"Reset consumes no record/world snapshot; actor/global digests must match.",RetailMainApplyStatus.InvalidSource);
            if(actor.LockBypass700==0)RetailMainApplyEvidence.Child(0x140041530,"Reset active critical-section owner requires integration.");
            if(globals.SerializationDebug142767930!=0)RetailMainApplyEvidence.Child(0x140478950,"Reset active lock-debug owner remains outside domain.");
            RetailMainApplyEvidence.Require(ticks.Count==2,"Reset needs exactly two distinct original ticks.");
            var owned=ticks.ToArray();
            uint Read(ulong site)
            {
                var r=owned.Where(t=>t is not null&&t.CallSite==site).ToArray();RetailMainApplyEvidence.Require(r.Length==1,"Missing/duplicate reset clock.");
                RetailMainApplyEvidence.Source(r[0].Source);
                RetailMainApplyEvidence.Require(r[0].ActorLifetime==context.ActorLifetime&&r[0].SnapshotRevision==context.SnapshotRevision&&
                    r[0].OperationRevision==context.OperationRevision&&r[0].Origin==context.Origin,"Reset clock revision mismatch.");return r[0].Value;
            }
            var before=actor.Motion;
            writes.Add(new("actor+1870",before.Velocity186c.Y,0,4,0x140b182d7));writes.Add(new("actor+1874",before.Velocity186c.Z,0,4,0x140b182d7));
            writes.Add(new("actor+186c",before.Velocity186c.X,0,4,0x140b182de));
            uint due=Read(ResetDueTickSite);writes.Add(new("actor+1858",before.Due1858,due,4,0x140b182ea));uint start=Read(ResetStartTickSite);
            writes.Add(new("actor+1c35",actor.Flags1c35,0,1,0x140b182f6));
            writes.Add(new("actor+1860",before.PriorEndpoint1860.X,before.Endpoint120.X,4,0x140b18305));
            writes.Add(new("actor+1864",before.PriorEndpoint1860.Y,before.Endpoint120.Y,4,0x140b18305));
            writes.Add(new("actor+19fc[0]",before.ContinuingGoal19fc.X,before.Endpoint120.X,4,0x140b1830d));
            writes.Add(new("actor+19fc[1]",before.ContinuingGoal19fc.Y,before.Endpoint120.Y,4,0x140b1830d));
            writes.Add(new("actor+185c",before.Start185c,start,4,0x140b18315));
            writes.Add(new("actor+1868",before.PriorEndpoint1860.Z,before.Endpoint120.Z,4,0x140b18321));
            writes.Add(new("actor+19fc[2]",before.ContinuingGoal19fc.Z,before.Endpoint120.Z,4,0x140b18327));
            writes.Add(new("TLS+60",globals.TlsDepth60,unchecked(globals.TlsDepth60-1),4,0x140b18360));
            actor=actor with { Motion=before with { Velocity186c=default,Due1858=due,Start185c=start,PriorEndpoint1860=before.Endpoint120,ContinuingGoal19fc=before.Endpoint120 },Flags1c35=0 };
            globals=globals with { TlsDepth60=unchecked(globals.TlsDepth60-1) };
            return new(RetailMainApplyStatus.CalculatedFiniteApplication,actor,null,globals,[],RetailMainWorldDisposition.NotReached,writes.ToImmutableArray(),
                ["slot21:computed raw reset; no packet/world/timer/FP arithmetic"],null,null);
        }
        catch(RetailMainApplyFault f){return new(f.Status,null,null,null,[],RetailMainWorldDisposition.NotReached,writes.ToImmutableArray(),[],f.Child,f.Message);}
    }

    private static ImmutableArray<byte> BuildClientPacket(uint id,RetailMainVectorWords pose,byte heading,byte inputFlags,RetailMainVectorWords goal)
    {
        byte flags=(byte)(inputFlags&0xef);bool extended=(flags&0xc0)==0xc0;
        byte[] packet=new byte[(extended?37:25)+((flags&4)!=0?1:0)];
        BinaryPrimitives.WriteUInt16LittleEndian(packet,(ushort)packet.Length);BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2),0x1d9);
        packet[4]=0x56;BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(5),0xfe26);BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(7),id);
        WriteVector(packet.AsSpan(11),pose);packet[23]=heading;packet[24]=flags;
        if(extended)WriteVector(packet.AsSpan(25),(flags&0x20)!=0?goal:default);
        return ImmutableArray.CreateRange(packet);
    }
    private static void WriteVector(Span<byte> target,RetailMainVectorWords v)
    {BinaryPrimitives.WriteUInt32LittleEndian(target,v.X);BinaryPrimitives.WriteUInt32LittleEndian(target[4..],v.Y);BinaryPrimitives.WriteUInt32LittleEndian(target[8..],v.Z);}
}
