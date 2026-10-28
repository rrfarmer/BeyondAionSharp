using System.Buffers.Binary;
using System.Collections.Immutable;
using Aion.GameServer.Commons.Utils.RetailRandom;
using O = Aion.GameServer.GeoEngine.RetailPath.RetailNpcManagedMembershipObservation;

namespace Aion.GameServer.GeoEngine.RetailPath;

/// <summary>Calculated source semantics for a future4.8 adapter. No native/client packet transport runs here.</summary>
internal static class RetailNpcManagedVisibility
{
    internal enum Kind { Spawn, Move, Delete }
    internal abstract record Meaning(Kind Kind,uint RecipientId,uint ObjectId);
    internal sealed record Move(uint RecipientId,uint ObjectId,RetailMainVectorWords EffectiveOld,byte Heading,byte Flags,RetailMainVectorWords Goal,byte NativeGlideTail)
        :Meaning(RetailNpcManagedVisibility.Kind.Move,RecipientId,ObjectId);
    internal sealed record Delete(uint RecipientId,uint ObjectId,byte Mode):Meaning(RetailNpcManagedVisibility.Kind.Delete,RecipientId,ObjectId);
    internal enum ArgumentKind { Unsigned, Signed, FloatBits, Vector, Utf16, Equipment }
    internal sealed record Argument(int NativeIndex,ArgumentKind Kind,ulong Scalar,string? Text=null,
        RetailMainVectorWords? Vector=null,ImmutableArray<byte> Bytes=default);
    internal sealed record Spawn(uint RecipientId,uint ObjectId,ImmutableArray<Argument> Arguments,int NativeSourceSize)
        :Meaning(RetailNpcManagedVisibility.Kind.Spawn,RecipientId,ObjectId);
    internal sealed record Intent(Meaning Meaning,string RecipientLifetime,string ConnectionGeneration,bool HasConnection)
    { internal bool HostEffectsExecuted=>false; internal bool EncodedFor48=>false; }
    internal sealed record Result(ImmutableArray<Intent> Intents,ImmutableArray<O.User> Users,O.SharedRandom? Random);
    private static readonly byte[] A=Convert.FromHexString("0008002426362200000000000028f112");
    private static readonly byte[] B=Convert.FromHexString("00cbcbffffffeb0000db0f4940a02526");
    internal sealed class Run(O.Input input,List<O.Event> events,HashSet<(ulong,uint)> consumed)
    {
        private readonly List<Intent> intents=[];
        private O.SharedRandom? random=input.Scene.Random;
        private readonly Dictionary<O.ActorKey,O.User> users=input.Scene.Users.ToDictionary(u=>u.Key);
        internal ImmutableArray<O.User> CurrentUsers=>input.Scene.Users.Select(u=>users[u.Key]).ToImmutableArray();
        internal O.SharedRandom? CurrentRandom=>random;
        internal ImmutableArray<Intent> CurrentIntents=>[..intents];
        private void Emit(string stage,ulong site,uint id=0,long? before=null,long? after=null)=>events.Add(new(events.Count+1,stage,site,id,before,after));
        private uint Tick(ulong site,uint id)
        {
            var matches=input.Clocks.Where(t=>t.Site==site && t.RecipientId==id).ToArray();
            O.Need(matches.Length==1 && consumed.Add((site,id)),"clock","Missing/repeated reached original clock.");
            Emit("OriginalClock",site,id,after:matches[0].Value);return matches[0].Value;
        }
        private void Send(O.User user,Meaning meaning)
        {
            Emit("CalculatedTransportIntent",0x140036fa0,user.Key.ObjectId,after:user.ConnectionPresent?1:0);
            intents.Add(new(meaning,user.Key.Lifetime,user.ConnectionGeneration,user.ConnectionPresent));
        }
        internal Result Calculate(O.Npc npc,O.Allocation image,RetailMainVectorWords oldEndpoint,RetailMainVectorWords endpoint,Move? move,
            ImmutableArray<O.Member> members)
        {
            var w=input.Scene.Geometry;int radius=O.I32(image,0x140);
            O.Need(radius is >=0 and <=46340,"range","Finite nonnegative signed32 radius-square domain0..46340 required.");
            int square=checked(radius*radius);
            int ox=RetailMainApplyMath.Truncate32(oldEndpoint.X),oy=RetailMainApplyMath.Truncate32(oldEndpoint.Y);
            int nx=RetailMainApplyMath.Truncate32(endpoint.X),ny=RetailMainApplyMath.Truncate32(endpoint.Y);
            int Low(int value,int extent)=>System.Math.Clamp(checked(value-radius),0,extent);
            int High(int value,int extent)=>System.Math.Clamp(checked(value+1+radius),0,extent);
            int lowX=System.Math.Min(Low(ox,w.WidthMetres),Low(nx,w.WidthMetres));
            int lowY=System.Math.Min(Low(oy,w.HeightMetres),Low(ny,w.HeightMetres));
            int highX=System.Math.Max(High(ox,w.WidthMetres),High(nx,w.WidthMetres));
            int highY=System.Math.Max(High(oy,w.HeightMetres),High(ny,w.HeightMetres));
            Emit("Class2HousingNotifySkipped",0x140681830);
            for(int y=lowY>>5;y<(highY+31)>>5;y++)for(int x=lowX>>5;x<(highX+31)>>5;x++)
            {
                int cell=checked(y*w.MovingStride50+x);Emit("OrderedRecipientCell",0x140681de1,after:cell);
                foreach(O.Member member in members.Where(m=>m.Cell==cell && m.Kind==RetailMainWrapKind.User))
                {
                    O.User user=users[member.Actor];var point=O.Vector(user.Image,0x120);
                    int oldDistance=Distance(point,oldEndpoint),newDistance=Distance(point,endpoint);
                    bool oldInside=oldDistance<=square,newInside=newDistance<=square;
                    Emit("OriginalRangeOldThenNew",0x140681e79,user.Key.ObjectId,oldDistance,newDistance);
                    if(!oldInside && newInside)See(npc,image,user,endpoint);
                    else if(oldInside && !newInside)DontSee(npc,image,user,endpoint);
                    else if(oldInside && newInside)
                    {
                        Emit("NpcSlot28ConstantOne",0x1400b1940,user.Key.ObjectId,after:1);
                        Emit("NpcSlot39RawWord",0x1400bbfb0,user.Key.ObjectId,after:O.U32(image,0x1c3c)&1);
                        if((O.U32(image,0x1c3c)&1)==0 && move is not null)
                            Send(user,move with {RecipientId=user.Key.ObjectId});
                        if((O.U8(user.Image,0x1ca4)&1)!=0 && O.U32(user.Image,0x130c)==npc.Key.ObjectId && input.Scene.Globals.StayDebug14276627c!=0)
                            O.Child(0x140681830,"Active stay debug message needs its complete gameplay policy.");
                    }
                }
            }
            return new([..intents],input.Scene.Users.Select(u=>users[u.Key]).ToImmutableArray(),random);
        }
        private void DontSee(O.Npc npc,O.Allocation image,O.User user,RetailMainVectorWords endpoint)
        {
            Emit("DontSeeClass2",0x140199060,user.Key.ObjectId);
            if(O.I32(image,0x1a50)!=0)return;
            int radius=O.I32(image,0x140);
            if(radius>100)
            {
                // The leave helper uses separate scalar y*x/z additions and a strict float comparison, not DPPS/CVTT.
                var p=O.Vector(user.Image,0x120);
                uint dx=RetailMainApplyMath.Sub(p.X,endpoint.X),dy=RetailMainApplyMath.Sub(p.Y,endpoint.Y),dz=RetailMainApplyMath.Sub(p.Z,endpoint.Z);
                uint sum=RetailMainApplyMath.Add(RetailMainApplyMath.Add(RetailMainApplyMath.Mul(dy,dy),RetailMainApplyMath.Mul(dx,dx)),RetailMainApplyMath.Mul(dz,dz));
                if(RetailMainApplyMath.Float(sum)<RetailMainApplyMath.Float(RetailMainApplyMath.SignedInteger(checked(radius*radius))))return;
            }
            Send(user,new Delete(user.Key.ObjectId,npc.Key.ObjectId,0));
        }
        private void See(O.Npc npc,O.Allocation image,O.User user,RetailMainVectorWords endpoint)
        {
            Emit("UserSeeEntry",0x140533c80,user.Key.ObjectId);
            // These reads are intentionally lazy, as in the actual parent.
            if(O.U8(user.Image,0x731d)!=0)return;
            if(O.U8(user.Image,0x731c)!=0)return;
            if(O.U32(user.Image,0x43a4)!=O.U32(user.Image,0x43a0))return;
            Emit("NpcSlot39RawWord",0x1400bbfb0,user.Key.ObjectId,after:O.U32(image,0x1c3c)&1);
            if((O.U32(image,0x1c3c)&1)!=0)return;
            if(input.Scene.Globals.OwnerDebug1427679a8!=0)O.Child(0x140533c80,"Active owner/party debug-filter branch is outside this domain.");
            Emit("NpcShowDebugMsgToUserConstantZero",0x1400bcaf0,user.Key.ObjectId,after:0);
            if(O.I32(image,0x1a50)!=0)return; // actual dispatcher caller mode0, distinct from special mode14.
            Emit("SnapshotContinuingGoalAndFlags",0x1400b32b0,user.Key.ObjectId);
            if(!Alive(image,user.Key.ObjectId))O.Child(0x140b37020,"Actual alternate display selection must be calculated before spawn.");
            if(!Alive(image,user.Key.ObjectId))O.Child(0x140b37330,"Actual second alternate selection must be calculated.");
            if((O.U32(image,0x1940)&0x8000)!=0)O.Child(0x140a017d0,"Alternative spawn record is not the ordinary source codec.");
            Spawn spawn=BuildSpawn(npc,image,user,endpoint);
            Send(user,spawn);
            Effect(image,user);
            if(O.U8(npc.Template,0x400)!=0)O.Child(0x140533c80,"Placeable bind-stone gameplay owner is not calculated.");
            Emit("NpcShowDebugMsgToUserConstantZero",0x1400bcaf0,user.Key.ObjectId,after:0);
            if(O.I32(image,0x1b9c)!=0)O.Child(0x14019ab00,"Active NPC related state notification requires its child.");
            PostSee(image,user);
            if(O.I32(image,0x1bb0)!=0 && (O.U32(npc.Template,0x43c)&0x200)!=0)
                O.Child(0x140533c80,"Active house script must be calculated before a final transaction.");
        }
        private bool Alive(O.Allocation image,uint id)
        {
            byte visible=O.U8(image,0x1c34);Emit("Slot38Read1c34",0x1400bc3b0,id,after:visible);if(visible==0)return false;
            int hp=O.I32(image,0x778);Emit("Slot38Read778",0x1400bc3b0,id,after:hp);if(hp<=0)return false;
            int pending=O.I32(image,0x728);Emit("Slot38Read728",0x1400bc3b0,id,after:pending);return pending==0;
        }
        private byte Relation(O.Allocation image,O.User user)
        {
            int recipient=O.I32(user.Image,0x1698);Emit("RecipientSlot41Raw1698",0x1400b2fe0,user.Key.ObjectId,after:recipient);
            if(recipient==0)return O.U8(image,0x1c3d);
            if(recipient==1)return O.U8(image,0x1c3e);
            int source=O.I32(image,0x1698);Emit("NpcSlot41Raw1698",0x1400b2fe0,user.Key.ObjectId,after:source);
            O.Need(input.Scene.Relations is { },"matrix","Reached current matrix base/counts/storage are unknown.");
            O.Matrix matrix=input.Scene.Relations!;
            Emit("MatrixBaseCapturedOnce",0x140d6851f,user.Key.ObjectId,after:unchecked((long)matrix.Address));
            Emit("MatrixF9cCapturedOnce",0x140d68526,user.Key.ObjectId,after:matrix.F9c);
            Emit("MatrixF98CapturedOnce",0x140d6852d,user.Key.ObjectId,after:matrix.F98);
            int Read(int first,int second,ulong site)
            {
                if(second<0 || second>=matrix.F9c || first<0 || first>=matrix.F98)
                {Emit("MatrixBoundsDefaultMinus1",site,user.Key.ObjectId,after:-1);return -1;}
                int index=unchecked(matrix.F98*first+second);
                O.Need(index>=0 && index<matrix.Words.Length,"matrix","Wrapped signed32 matrix address lies outside owned storage.");
                int result=matrix.Words[index];Emit("MatrixRead",site,user.Key.ObjectId,index,result);return result;
            }
            int forward=Read(recipient,source,0x140d68558);
            int reverse=Read(source,recipient,0x140d6857c);
            int reverseAgain=Read(source,recipient,0x140d685a1);
            O.Need(reverse is >=-1 and <=14 && reverseAgain is >=-1 and <=14,"table","Reached relation literal byte outside the pinned -1..14 domain.");
            return (byte)(B[reverseAgain+1]&(A[reverse+1]|O.U8(image,0x1c3c)|(forward==0?8:0)));
        }
        private void Effect(O.Allocation image,O.User user)
        {
            Emit("EffectSendConnectionGuard",0x140363540,user.Key.ObjectId);
            if(!user.ConnectionPresent)return;
            ulong manager=O.U64(image,0x158);if(manager==0)return;
            // Managed exclusive generation ownership replaces the lock/TLS diagnostic mechanics.
            Emit("EffectManagerLockAndDiagnosticNormalReturn",0x140478950,user.Key.ObjectId);
            _=O.Resolve(input.Scene,O.Offset(manager,0x30),4);_=O.Resolve(input.Scene,O.Offset(manager,0x2b10),4);
            ulong fresh=O.U64(image,0x158);O.Need(fresh==manager,"manager","Manager generation changed in a serialized interval.");
            uint size=BinaryPrimitives.ReadUInt32LittleEndian(O.Resolve(input.Scene,O.Offset(fresh,0x446c),4));
            Emit("EffectPayloadUnsignedSize",0x1403635ad,user.Key.ObjectId,after:size);
            if(size>27)O.Child(0x1403635b8,"Active effect payload/tick/lifetime and4.8 abnormal mapping remain required.");
            O.Need(O.U64(image,0x158)==manager,"manager","Fresh unlock manager does not bind the owned manager.");
            Emit("EffectManagerFreshUnlock",0x1403635e3,user.Key.ObjectId);
        }
        private void PostSee(O.Allocation image,O.User user)
        {
            if((input.Scene.Globals.PostSee142769c24&2)!=0)
            {
                O.Need(random is not null,"random","Reached shared raw RNG state is unknown.");
                var draw=RetailRandomEngine.Draw(random.State);random=random with {State=draw.State};
                Emit("SharedRawRngBeforeAny7350Read",0x14053a3c0,user.Key.ObjectId,draw.Step.IndexBefore,unchecked((long)draw.Step.Value));
            }
            uint first=O.U32(user.Image,0x7350);Emit("PostSeeRead7350",0x14053a3c0,user.Key.ObjectId,after:first);if(first!=0)return;
            byte relation=Relation(image,user);if((relation&2)!=0)return;
            uint master=O.U32(image,0x188c);if(master!=0 && (master&0x80000000)==0)return;
            uint state=O.U32(user.Image,0x7358);Emit("PostSeeCAS7358Expected1Desired2",0x14053a3c0,user.Key.ObjectId,state,state==1?2:state);
            if(state==1)
            {users[user.Key]=user with {Image=O.Put32(user.Image,0x7358,2)};O.Child(0x14053a3c0,"CAS succeeded: optional full packet/state owner remains unclosed.");}
        }
        private Spawn BuildSpawn(O.Npc npc,O.Allocation image,O.User user,RetailMainVectorWords endpoint)
        {
            uint tick=Tick(0x140534f80,user.Key.ObjectId);
            uint diameter=O.U32(image,0x130),other=O.U32(image,0x134);
            if(RetailMainApplyMath.Float(diameter)<RetailMainApplyMath.Float(other))diameter=other;
            uint half=RetailMainApplyMath.Mul(diameter,0x3f000000);
            ulong namePointer=O.U64(image,0x110);uint nameCount=0;
            if(namePointer!=0 && BinaryPrimitives.ReadUInt16LittleEndian(O.Resolve(input.Scene,namePointer,2))!=0)
                nameCount=unchecked(BinaryPrimitives.ReadUInt32LittleEndian(O.Resolve(input.Scene,O.Offset(namePointer,2),4))-1)>>1;
            uint variant=O.U8(image,0x16c9)==0?O.U32(image,0x168c):O.U32(image,0x16cc);
            // Exact getter/pointee reads; they are not caller-provided return values.
            ulong suppression=O.U64(image,0x1a58);
            uint stamp=suppression==0?0:BinaryPrimitives.ReadUInt32LittleEndian(O.Resolve(input.Scene,O.Offset(suppression,4),4));
            uint suppressed=suppression==0?0:BinaryPrimitives.ReadUInt32LittleEndian(O.Resolve(input.Scene,suppression,4));
            int hp=O.I32(image,0x778),maxHp=O.I32(image,0x774);byte percent=maxHp>0 && hp>0?unchecked((byte)((long)hp*99/maxHp+1)):(byte)0;
            string firstName=O.Utf16(image,0x1440,100),secondName=O.Utf16(image,0x1c00,24);
            var equipment=O.Span(image,0x558,23*16).ToArray().ToImmutableArray();
            int equipmentCount=0;for(int n=0;n<23;n++)if(BinaryPrimitives.ReadUInt32LittleEndian(equipment.AsSpan(n*16,4))!=0)equipmentCount++;
            byte relation=Relation(image,user);
            List<Argument> args=[];
            void U(int n,ulong value)=>args.Add(new(n,ArgumentKind.Unsigned,value));
            void F(int n,uint word){_=RetailMainApplyMath.Float(word);args.Add(new(n,ArgumentKind.FloatBits,word));}
            args.Add(new(2,ArgumentKind.Vector,0,Vector:endpoint));U(3,npc.Key.ObjectId);U(4,O.U32(image,0x100));U(5,variant);U(6,relation);
            U(7,unchecked((ushort)O.I32(image,0x13c)));U(8,unchecked((byte)O.U32(image,0x12c)));U(9,nameCount);U(10,O.U32(npc.Template,0x284));
            U(11,unchecked((byte)O.U32(image,0x13d0)));U(12,O.U32(image,0x1304));args.Add(new(13,ArgumentKind.Utf16,0,firstName));
            U(14,O.U32(image,0x188c));args.Add(new(15,ArgumentKind.Utf16,0,secondName));U(16,percent);U(17,unchecked((uint)maxHp));U(18,unchecked((byte)O.U16(image,0x1492)));
            args.Add(new(19,ArgumentKind.Equipment,0,Bytes:equipment));F(20,half);F(21,O.U32(image,0x138));F(22,O.U32(image,0x7c0));
            U(23,O.U16(image,0x14a8));U(24,O.U16(image,0x14a6));U(25,0);args.Add(new(26,ArgumentKind.Vector,0,Vector:O.Vector(image,0x19fc)));
            U(27,O.U8(image,0x1c35));U(28,O.U32(image,0x1a3c));U(29,suppressed);U(30,unchecked(tick-stamp));
            U(31,unchecked((byte)O.U32(image,0x1a50)));U(32,unchecked((byte)O.U32(image,0x1a4c)));U(33,unchecked((byte)O.U32(image,0x141c)));
            U(34,unchecked((ushort)O.U32(image,0x1940)));U(35,O.U32(image,0x1a48));U(36,0);U(37,O.U32(image,0x130c));U(38,O.U32(image,0x1bbc));U(39,0);
            int size=checked(52+(firstName.Length+1)*2+(secondName.Length+1)*2+10+equipmentCount*16+(suppressed==0?38:42)+(O.U32(image,0x1a48)==0?5:9)+13);
            O.Need(size<=0x1fff,"spawn-size","Source spawn exceeds the admitted normal-return size domain.");
            Emit("CalculatedOrdinarySpawnArguments",0x140a01330,user.Key.ObjectId,after:size);
            return new(user.Key.ObjectId,npc.Key.ObjectId,[..args],size);
        }
    }
    internal static int Distance(RetailMainVectorWords a,RetailMainVectorWords b)
    {
        uint x=RetailMainApplyMath.Sub(a.X,b.X),y=RetailMainApplyMath.Sub(a.Y,b.Y),z=RetailMainApplyMath.Sub(a.Z,b.Z);
        uint left=RetailMainApplyMath.Add(RetailMainApplyMath.Mul(x,x),RetailMainApplyMath.Mul(y,y));
        uint right=RetailMainApplyMath.Add(RetailMainApplyMath.Mul(z,z),0);
        return RetailMainApplyMath.Truncate32(RetailMainApplyMath.Add(left,right));
    }
}
