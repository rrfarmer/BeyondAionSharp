using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Aion.GameServer.Commons.Utils.RetailRandom;

namespace Aion.GameServer.GeoEngine.RetailPath;

/// <summary>Inactive owned input contract. Managed lifetime/serialization is explicit; no native heap or socket emulator.</summary>
internal static class RetailNpcManagedMembershipObservation
{
    internal const string Policy = "NpcRetailGameplayWithManagedInfrastructureV1";
    internal const string Frontier = "0524faf9bc05a9f64320a1eb966524839615ec8daa058f9cbbd5c18e30cc45b7";
    internal const string Review = "4303716187d394d4a9076bc05c87929dd81a8bd21fcd9120fc6c6945a7835223";
    internal const string PackageContext = "frozen-USA-base-managed-membership;loaded-context-unavailable";
    internal const ulong NpcVtable = 0x1412b8470, UserVtable = 0x141161250;
    internal enum Origin { Unknown, ConstructedSourceFixture, DeclaredManagedSnapshot }
    internal sealed record Evidence(string MainSha256,string Policy,string PackageContext,string Operation,string Epoch,Origin Origin,string Receipt);
    internal sealed record Lease(string Owner,string Generation,string Operation,string Epoch,bool Exclusive,bool NoInlineGameWriter);
    internal sealed record Allocation(ulong Address,string Identity,string Generation,ImmutableArray<byte> Bytes,Evidence Evidence);
    internal sealed record ActorKey(ulong Address,uint ObjectId,string Lifetime,bool? Disposed);
    internal sealed record Member(ActorKey Actor,int Cell,RetailMainWrapKind Kind,string LeaseIdentity);
    internal sealed record Matrix(ulong Address,int F98,int F9c,ImmutableArray<int> Words,string Generation,Evidence Evidence);
    internal sealed record SharedRandom(ulong Address,string Generation,RetailRandomState State,Lease Lease,Evidence Evidence);
    internal sealed record Clock(ulong Site,uint Value,uint RecipientId,Evidence Evidence);
    internal sealed record Npc(ActorKey Key,RetailNpcMainActorSnapshot MotionSource,Allocation Image,Allocation Template);
    internal sealed record User(ActorKey Key,Allocation Image,string ConnectionGeneration,bool ConnectionPresent);
    internal sealed record CurrentGlobals(uint PostSee142769c24,uint OwnerDebug1427679a8,uint PacketDebug142766278,
        uint StayDebug14276627c,Evidence Evidence);
    internal sealed record Scene(RetailMainOwnedWorld Geometry,ImmutableArray<Member> Members,ImmutableArray<User> Users,
        ImmutableArray<Allocation> Pointees,Matrix? Relations,SharedRandom? Random,CurrentGlobals Globals,
        Lease Lease,Evidence Evidence);
    internal sealed record Input(ImmutableArray<byte> CanonicalMove,Npc Npc,Scene Scene,ImmutableArray<Clock> Clocks,
        Evidence Evidence,bool NearestEven,bool ExceptionsMasked,bool Sse2,bool FlushToZero,bool DenormalsAreZero);
    internal sealed record Event(int Sequence,string Stage,ulong SourceSite,uint RecipientId=0,long? Before=null,long? After=null);
    internal enum Status { CompleteManagedTransaction, NativeEarlyReturn, IncompleteReachedChild, InvalidInput }
    internal sealed record Failure(string Code,string Detail,ulong? Child);
    internal sealed class Fault(string code,string detail,ulong? child=null):Exception(detail)
    { internal string Code=>code; internal ulong? Child=>child; }
    internal static void Need([DoesNotReturnIf(false)] bool condition,string code,string detail)
    { if(!condition)throw new Fault(code,detail); }
    internal static void Child(ulong child,string detail)=>throw new Fault("reached-child",detail,child);
    internal static void Text(string value)=>Need(!string.IsNullOrWhiteSpace(value),"identity","Missing identity/generation/receipt.");
    internal static void Verify(Evidence actual,Evidence expected)
    {
        Need(actual is not null && expected is not null,"evidence","Missing source/current evidence.");
        Need(actual.MainSha256==RetailNpcMainMotion.MainBinarySha256 && actual.Policy==Policy && actual.PackageContext==PackageContext && actual.Operation==expected.Operation && actual.Epoch==expected.Epoch
            && actual.Origin is Origin.ConstructedSourceFixture or Origin.DeclaredManagedSnapshot,"evidence","Main/policy/operation/epoch/provenance mismatch.");
        Text(actual.Operation);Text(actual.Epoch);Text(actual.Receipt);
    }
    internal static void Validate(Input i)
    {
        Verify(i.Evidence,i.Evidence);Verify(i.Scene.Evidence,i.Evidence);Verify(i.Scene.Globals.Evidence,i.Evidence);
        Need(i.NearestEven && i.ExceptionsMasked && i.Sse2 && !i.FlushToZero && !i.DenormalsAreZero,"floating","Explicit RNE/masked/SSE2/FTZ-DAZ-off contract required.");
        Lease lease=i.Scene.Lease;Text(lease.Owner);Text(lease.Generation);
        Need(lease.Exclusive && lease.NoInlineGameWriter && lease.Operation==i.Evidence.Operation && lease.Epoch==i.Evidence.Epoch,
            "lease","Complete immutable world/actor/recipient generation interval requires one exclusive owner and no inline game writer.");
        Need(!i.Scene.Members.IsDefault && !i.Scene.Users.IsDefault && !i.Scene.Pointees.IsDefault && !i.Clocks.IsDefault,"unknown","Unknown collections are not known empty storage.");
        Need(i.Scene.Members.All(m=>m is not null && m.Actor is not null) && i.Scene.Users.All(u=>u is not null && u.Key is not null && u.Image is not null),
            "unknown","Null owned membership/recipient roots are unsupported.");
        Need(i.Scene.Members.Length<=262144 && i.Scene.Users.Length<=65536 && i.Scene.Pointees.Length<=65536 && i.Clocks.Length<=65539,
            "budget","Defensive fixture count bound exceeded.");
        RetailNpcMainWorldMotion.ValidateDimensions(i.Scene.Geometry,new());
        RetailNpcMainMaterialQuery.ValidateStorage(i.Scene.Geometry,new());
        Npc npc=i.Npc;Need(npc.Key.Address!=0 && npc.Key.ObjectId!=0 && npc.Key.Disposed==false,"actor","Missing/disposed/unknown source actor lifetime.");Text(npc.Key.Lifetime);
        Need(npc.MotionSource.ObjectAddress==npc.Key.Address && npc.MotionSource.ObjectId==npc.Key.ObjectId && npc.Image.Address==npc.Key.Address
            && npc.MotionSource.Vtable==NpcVtable && npc.MotionSource.NativeClass144==2,"actor","Actual ordinary NPC source/motion/image binding mismatch.");
        VerifyAllocation(npc.Image,i.Evidence);VerifyAllocation(npc.Template,i.Evidence);
        Need(npc.Image.Bytes.Length>=0x1c50 && npc.Template.Bytes.Length>=0x500,"storage","Complete consumed NPC/template spans required.");
        Need(U64(npc.Image,0)==NpcVtable && U32(npc.Image,0x70)==npc.Key.ObjectId && I32(npc.Image,0x144)==2
            && U64(npc.Image,0x1848)==npc.Template.Address && npc.MotionSource.Template1848.Address==npc.Template.Address,
            "actor","Actual vtable/class/object/template aliases disagree.");
        Need(I32(npc.Template,0x354)==npc.MotionSource.Template1848.Raw354 && I32(npc.Image,0x188c)==npc.MotionSource.Owner188c
            && I32(npc.Image,0x1a50)==npc.MotionSource.SpecialBroadcast1a50 && I32(npc.Image,0x140)==npc.MotionSource.ObserverRange140
            && U32(npc.Image,0x19f8)==npc.MotionSource.Counter19f8 && I32(npc.Image,0x12c)==npc.MotionSource.Heading12c
            && U8(npc.Image,0x1c35)==npc.MotionSource.Flags1c35,"actor","Raw selected motion/image scalar aliases disagree.");
        Need(U64(npc.Image,0xb8)==npc.MotionSource.WrapB8 && I32(npc.Image,8)==npc.MotionSource.RefCount8
            && I32(npc.Image,0x14)==npc.MotionSource.RefDebug14 && U64(npc.Image,0x28)==npc.MotionSource.RefSource28
            && U32(npc.Image,0x30)==npc.MotionSource.RefLine30 && I32(npc.Image,0x700)==npc.MotionSource.LockBypass700,
            "actor","Historical native infrastructure aliases must also be coherent at input; managed output does not simulate their poststates.");
        Need(Vector(npc.Image,0x120)==npc.MotionSource.Motion.Endpoint120 && Vector(npc.Image,0x19fc)==npc.MotionSource.Motion.ContinuingGoal19fc,
            "actor","Current raw endpoint/goal aliases disagree.");
        Need(Vector(npc.Image,0x1860)==npc.MotionSource.Motion.PriorEndpoint1860 && Vector(npc.Image,0x186c)==npc.MotionSource.Motion.Velocity186c
            && U32(npc.Image,0x1858)==npc.MotionSource.Motion.Due1858 && U32(npc.Image,0x185c)==npc.MotionSource.Motion.Start185c
            && U32(npc.Image,0x7c0)==npc.MotionSource.Motion.CurrentSpeed7c0 && U32(npc.Image,0x7c8)==npc.MotionSource.TurnRate7c8
            && U64(npc.Image,0x1a58)==npc.MotionSource.Suppression1a58.Pointer && U32(npc.Image,0x1ba0)==npc.MotionSource.TraceOwner1ba0,
            "actor","All getter/turn/suppression source fields must share one raw image.");
        Need(U64(npc.Image,0x118)==i.Scene.Geometry.NativeAddress && npc.MotionSource.WorldIdentity==i.Scene.Geometry.Identity,
            "world","Current world binding required; this domain has an actual current world.");
        Need(U32(npc.Image,0x188c)==0 && U32(npc.Image,0x1894)==0 && U64(npc.Image,0x1850)==0,"owner","Only ordinary ownerless NPC motion is admitted.");
        List<Allocation> allocations=[npc.Image,npc.Template];allocations.AddRange(i.Scene.Pointees);
        HashSet<ActorKey> actors=[npc.Key];HashSet<ulong> addresses=[npc.Key.Address];HashSet<uint> ids=[npc.Key.ObjectId];
        foreach(User user in i.Scene.Users)
        {
            Need(user is not null && user.Key.Address!=0 && user.Key.ObjectId!=0 && user.Key.Disposed==false && actors.Add(user.Key) && addresses.Add(user.Key.Address) && ids.Add(user.Key.ObjectId),
                "user","Duplicate/null recipient identity.");Text(user.Key.Lifetime);Text(user.ConnectionGeneration);
            VerifyAllocation(user.Image,i.Evidence);allocations.Add(user.Image);
            Need(user.Image.Address==user.Key.Address && user.Image.Bytes.Length>=0x7360 && U64(user.Image,0)==UserVtable
                && I32(user.Image,0x144)==1 && U32(user.Image,0x70)==user.Key.ObjectId,"user","Actual User storage/vtable/class binding required.");
            Need(U64(user.Image,0x118)==i.Scene.Geometry.NativeAddress,"world","All reached User roots belong to the same current world.");
            Need((U64(user.Image,0x3720)!=0)==user.ConnectionPresent,"connection","Actual connection pointer presence and managed connection lifetime disagree.");
        }
        foreach(Allocation allocation in i.Scene.Pointees)VerifyAllocation(allocation,i.Evidence);
        var suppression=npc.MotionSource.Suppression1a58;
        Need(suppression is not null && (suppression.Pointer==0?suppression.Word is null:
            suppression.Word.HasValue && BinaryPrimitives.ReadUInt32LittleEndian(Resolve(i.Scene,suppression.Pointer,4))==suppression.Word.Value),
            "actor","Suppression pointer/pointee aliases do not bind one actual source image.");
        Need(allocations.Sum(a=>(long)a.Bytes.Length)<=128*1024*1024,"budget","Complete actor/template/pointee graph exceeds the defensive128MiB domain.");
        allocations.Sort((a,b)=>a.Address.CompareTo(b.Address));
        Need(allocations.All(a=>i.Scene.Geometry.NativeAddress<a.Address || i.Scene.Geometry.NativeAddress>=a.Address+(ulong)a.Bytes.Length),
            "alias","World binding aliases a separately declared actor/template/pointee allocation.");
        for(int n=1;n<allocations.Count;n++)Need(allocations[n-1].Address+(ulong)allocations[n-1].Bytes.Length<=allocations[n].Address,
            "alias","Owned actor/template/pointee allocations overlap.");
        Need(i.Scene.Members.Length==actors.Count,"membership","Complete source NPC/User membership required; unknown other actors are not skipped.");
        HashSet<ActorKey> members=[];
        foreach(Member member in i.Scene.Members)
        {
            Need(members.Add(member.Actor) && actors.Contains(member.Actor) && member.Cell>=0 && member.Cell<i.Scene.Geometry.Cells.Length,
                "membership","Duplicate, unowned or out-of-bounds member.");Text(member.LeaseIdentity);
            Need(member.Kind==(member.Actor==npc.Key?RetailMainWrapKind.Object:RetailMainWrapKind.User),"membership","Actual object/User family mismatch.");
            var raw=member.Actor==npc.Key?npc.Image:i.Scene.Users.Single(u=>u.Key==member.Actor).Image;
            Need(RetailNpcMainWorldMotion.InBounds(i.Scene.Geometry,Vector(raw,0x120)) && member.Cell==RetailNpcMainWorldMotion.Cell(i.Scene.Geometry,Vector(raw,0x120)),
                "membership","Current member cell must derive from its actual raw endpoint.");
        }
        // Ordered membership is the managed owner input; native heap/wrapper addresses are not simulated.
        // Every member must also correspond to the source graph, retaining its per-cell User order.
        RetailNpcMainWorldMotion.ValidateGraph(i.Scene.Geometry,npc.MotionSource,new());
        var nodes=i.Scene.Geometry.Wraps.ToDictionary(x=>x.Address);
        for(int cell=0;cell<i.Scene.Geometry.Cells.Length;cell++)
        {
            List<ulong> raw=[];ulong at=i.Scene.Geometry.Cells[cell].UserRoot;
            while(at!=0){raw.Add(nodes[at].ActorAddress);at=nodes[at].Next;}
            Need(raw.SequenceEqual(i.Scene.Members.Where(m=>m.Cell==cell && m.Kind==RetailMainWrapKind.User).Select(m=>m.Actor.Address)),
                "membership","Managed current per-cell recipient order must bind the original complete graph.");
        }
        Need(nodes.Count==i.Scene.Members.Length,"membership","Original roots contain unsupported/unowned object families.");
        Need(i.Clocks.All(t=>t is not null),"clock","Null original clock receipt.");
        foreach(Clock tick in i.Clocks)Verify(tick.Evidence,i.Evidence);
        Need(i.Clocks.Select(c=>(c.Site,c.RecipientId)).Distinct().Count()==i.Clocks.Length,"clock","Duplicate original clock site/recipient receipt.");
        if(i.Scene.Relations is { } matrix)
        {Verify(matrix.Evidence,i.Evidence);Text(matrix.Generation);Need(matrix.Address!=0 && !matrix.Words.IsDefault && matrix.Words.Length<=1048576
            && matrix.Address<=ulong.MaxValue-(ulong)matrix.Words.Length*4,"matrix","Complete bounded current matrix storage required.");
         foreach(Allocation allocation in allocations)Need(matrix.Address+(ulong)matrix.Words.Length*4<=allocation.Address || allocation.Address+(ulong)allocation.Bytes.Length<=matrix.Address,
             "alias","Current matrix storage overlaps another independently declared allocation.");}
        if(i.Scene.Random is { } rng)
        {Verify(rng.Evidence,i.Evidence);Text(rng.Generation);Need(rng.Address==RetailRandomEngine.NativeEngineAddress && rng.State is not null,"random","Actual serialized shared raw engine required.");
         Need(rng.Lease is not null && rng.Lease.Exclusive && rng.Lease.NoInlineGameWriter && rng.Lease.Operation==i.Evidence.Operation && rng.Lease.Epoch==i.Evidence.Epoch,
             "random","The shared engine needs its own exclusive generation interval across all consumers.");Text(rng.Lease.Owner);Text(rng.Lease.Generation);}
    }
    internal static void VerifyAllocation(Allocation a,Evidence evidence)
    {
        Need(a is not null && a.Address!=0 && !a.Bytes.IsDefault && a.Bytes.Length>0 && a.Address<=ulong.MaxValue-(ulong)a.Bytes.Length,"storage","Unknown/empty/overflow allocation.");
        Text(a.Identity);Text(a.Generation);Verify(a.Evidence,evidence);
    }
    internal static ReadOnlySpan<byte> Span(Allocation a,int offset,int length)
    {Need(offset>=0 && length>=0 && offset<=a.Bytes.Length-length,"storage","Read outside complete owned allocation.");return a.Bytes.AsSpan(offset,length);}
    internal static byte U8(Allocation a,int offset)=>Span(a,offset,1)[0];
    internal static ushort U16(Allocation a,int offset)=>BinaryPrimitives.ReadUInt16LittleEndian(Span(a,offset,2));
    internal static int I32(Allocation a,int offset)=>BinaryPrimitives.ReadInt32LittleEndian(Span(a,offset,4));
    internal static uint U32(Allocation a,int offset)=>BinaryPrimitives.ReadUInt32LittleEndian(Span(a,offset,4));
    internal static ulong U64(Allocation a,int offset)=>BinaryPrimitives.ReadUInt64LittleEndian(Span(a,offset,8));
    internal static RetailMainVectorWords Vector(Allocation a,int offset)=>new(U32(a,offset),U32(a,offset+4),U32(a,offset+8));
    internal static ReadOnlySpan<byte> Resolve(Scene scene,ulong address,int count)
    {
        Need(address!=0 && count>=0 && address<=ulong.MaxValue-(ulong)count,"storage","Malformed reached pointer span.");
        foreach(Allocation p in scene.Pointees)
            if(address>=p.Address && address+(ulong)count<=p.Address+(ulong)p.Bytes.Length)return p.Bytes.AsSpan(checked((int)(address-p.Address)),count);
        throw new Fault("storage","Reached pointer is not bound to complete owned storage.");
    }
    internal static ulong Offset(ulong address,uint offset)
    {Need(address!=0 && address<=ulong.MaxValue-offset,"storage","Reached pointer arithmetic exceeds the finite nonwrapping domain.");return address+offset;}
    internal static string Utf16(Allocation image,int offset,int maxUnits)
    {
        List<char> chars=[];
        for(int n=0;n<maxUnits;n++){ushort v=U16(image,checked(offset+n*2));if(v==0)return new string(chars.ToArray());chars.Add((char)v);}
        throw new Fault("string","Unterminated bounded source UTF16 string.");
    }
    internal static Allocation Replace(Allocation image,int offset,ReadOnlySpan<byte> value)
    { _=Span(image,offset,value.Length);byte[] bytes=image.Bytes.ToArray();value.CopyTo(bytes.AsSpan(offset));return image with {Bytes=[..bytes]}; }
    internal static Allocation Put32(Allocation image,int offset,uint value)
    {System.Span<byte> bytes=stackalloc byte[4];BinaryPrimitives.WriteUInt32LittleEndian(bytes,value);return Replace(image,offset,bytes);}
    internal static Allocation PutVector(Allocation image,int offset,RetailMainVectorWords value)
    {image=Put32(image,offset,value.X);image=Put32(image,offset+4,value.Y);return Put32(image,offset+8,value.Z);}
}
