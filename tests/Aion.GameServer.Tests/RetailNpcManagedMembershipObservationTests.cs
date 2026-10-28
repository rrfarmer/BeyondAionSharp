using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using Aion.GameServer.Commons.Utils.RetailRandom;
using Aion.GameServer.GeoEngine.RetailPath;
using O = Aion.GameServer.GeoEngine.RetailPath.RetailNpcManagedMembershipObservation;
using M = Aion.GameServer.GeoEngine.RetailPath.RetailNpcManagedMembershipMove;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcManagedMembershipObservationTests
{
    [Fact]
    public void CompleteSyntheticGraphHasRealNonemptyOrderedUserRootsAndOwnedBytes()
    {
        var i=ManagedMembershipFixture.Make();O.Validate(i);
        Assert.Equal(5,i.Scene.Members.Length);Assert.Equal(4,i.Scene.Users.Length);
        Assert.NotEqual(0ul,i.Scene.Geometry.Cells[0].UserRoot);Assert.NotEqual(0ul,i.Scene.Geometry.Cells[1].UserRoot);
        Assert.Equal("NpcRetailGameplayWithManagedInfrastructureV1",i.Evidence.Policy);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    [InlineData(6)] [InlineData(7)] [InlineData(8)] [InlineData(9)] [InlineData(10)] [InlineData(11)] [InlineData(12)] [InlineData(13)]
    public void WholeOwnerRejectsMissingStaleAliasedOrContradictoryInputs(int mode)
    {
        var i=ManagedMembershipFixture.Make();var scene=i.Scene;var npc=i.Npc;
        i=mode switch
        {
            0=>i with {Evidence=i.Evidence with {Policy="unapproved"}},
            1=>i with {Scene=scene with {Lease=scene.Lease with {Exclusive=false}}},
            2=>i with {Scene=scene with {Members=default}},
            3=>i with {Scene=scene with {Users=scene.Users.Add(scene.Users[0])}},
            4=>i with {Npc=npc with {Image=npc.Image with {Generation=""}}},
            5=>i with {Npc=npc with {Image=O.Put32(npc.Image,0x19f8,99)}},
            6=>i with {Npc=npc with {Image=O.Put32(npc.Image,0x7c8,1)}},
            7=>i with {Scene=scene with {Members=scene.Members.SetItem(0,scene.Members[0] with {Cell=15})}},
            8=>i with {Scene=scene with {Pointees=[new(npc.Key.Address,"overlap","p1",[1],i.Evidence)]}},
            9=>i with {NearestEven=false},
            10=>i with {Scene=scene with {Users=scene.Users.SetItem(0,scene.Users[0] with {ConnectionPresent=false})}},
            11=>i with {Scene=scene with {Lease=scene.Lease with {Epoch="another"}}},
            12=>i with {Npc=npc with {Key=npc.Key with {Disposed=true}}},
            _=>i with {Npc=npc with {Key=npc.Key with {Disposed=null}}}
        };
        var result=M.Calculate(i);Assert.Equal(O.Status.InvalidInput,result.Status);Assert.Null(result.Candidate);Assert.Null(result.Known);
        Assert.False(result.CanApplyToHost);Assert.False(result.NativeOracle);
    }
    [Fact]
    public void NoCallerObjectCanForgeACompletedManagedCandidate()
    {
        Assert.Throws<ArgumentNullException>(()=>M.Candidate.FromCompletedReplay(null!));
        Assert.Throws<ArgumentException>(()=>M.Candidate.FromCompletedReplay(ManagedMembershipFixture.Make()));
        var result=M.Calculate(ManagedMembershipFixture.Make());Assert.NotNull(result.Candidate);
        Assert.Throws<ArgumentException>(()=>M.Candidate.FromCompletedReplay(result.Candidate!));
    }
    [Fact]
    public void CurrentWord1c3cOwnsHiddenBitAndBothCachedTribeBytes()
    {
        var i=ManagedMembershipFixture.Make();var image=O.Put32(i.Npc.Image,0x1c3c,0x0026a400);
        Assert.Equal((byte)0,O.U8(image,0x1c3c));Assert.Equal((byte)0xa4,O.U8(image,0x1c3d));Assert.Equal((byte)0x26,O.U8(image,0x1c3e));
        Assert.Equal(0u,O.U32(image,0x1c3c)&1);
    }
}

/// <summary>Deliberately constructed source fixtures; no observed actor/world/default memory claim.</summary>
internal static class ManagedMembershipFixture
{
    internal static readonly O.Evidence Evidence=new(RetailNpcMainMotion.MainBinarySha256,O.Policy,O.PackageContext,"managed-move-op","source-epoch",O.Origin.ConstructedSourceFixture,"chosen-owned-byte-image;not-native-oracle");
    internal static readonly RetailMainSourceDeclaration Source=new("chosen-source-fixture",new string('a',64));
    internal static uint Bits(float value)=>BitConverter.SingleToUInt32Bits(value);
    internal static RetailMainVectorWords V(float x,float y=1,float z=0)=>new(Bits(x),Bits(y),Bits(z));
    internal static void W32(byte[] bytes,int offset,uint value)=>BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset,4),value);
    internal static void W64(byte[] bytes,int offset,ulong value)=>BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(offset,8),value);
    internal static void WV(byte[] bytes,int offset,RetailMainVectorWords point){W32(bytes,offset,point.X);W32(bytes,offset+4,point.Y);W32(bytes,offset+8,point.Z);}
    internal static void Text(byte[] bytes,int offset,string value)
    {for(int n=0;n<value.Length;n++)BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset+n*2,2),value[n]);}
    internal static RetailMainMaterialStorage Material(int size,byte[]? records=null)
    {
        int width=size/2,samples=(width+1)*(width+1);byte[] raw=new byte[8+samples*4];W32(raw,0,(uint)width);W32(raw,4,(uint)width);
        for(int n=0;n<samples;n++)raw[8+samples*2+n]=1;
        var parsed=RetailHeightMap40Reader.Read(raw,new(){RequireExactEnd=true,ExpectedWorldWidthMetres=size,ExpectedWorldHeightMetres=size});
        Assert.True(parsed.Success,parsed.Error?.ToString());
        var blocks=Enumerable.Range(0,(size/64)*(size/64)).Select(n=>new RetailMainMaterialStorage.CellBlock(0x90000000+(ulong)n*0x10000,new byte[16384]));
        return new(parsed.Data!,1,[],records??new byte[256*0x44],blocks,Source);
    }
    internal static O.Input Make(float oldX=31,float newX=32,int size=128,int radius=10,bool observers=true)
    {
        const ulong npcAddress=0x100000,templateAddress=0x200000,worldAddress=0xa00000;
        var key=new O.ActorKey(npcAddress,7,"npc-life",false);var old=V(oldX);var endpoint=V(newX);
        var motion=new RetailNpcMainMotionState(old,old,default,old,0,1000,Bits(3));
        int Cell(RetailMainVectorWords p)=>((int)BitConverter.UInt32BitsToSingle(p.Y)>>5)*(size/32)+((int)BitConverter.UInt32BitsToSingle(p.X)>>5);
        int ownCell=Cell(old);
        var actor=new RetailNpcMainActorSnapshot(npcAddress,O.NpcVtable,7,2,"world",0x500000,0,motion,0,0,0,new(0,null),0,
            new(templateAddress,0,Source),0,0,radius,5,0,0,0,0,0,0,[0,0,0,0],Source);
        byte[] image=new byte[0x1c50];W64(image,0,O.NpcVtable);W32(image,8,5);W32(image,0x70,7);W64(image,0xb8,0x500000);W64(image,0x118,worldAddress);WV(image,0x120,old);W32(image,0x140,(uint)radius);W32(image,0x144,2);
        W32(image,0x774,100);W32(image,0x778,50);W32(image,0x7c0,Bits(3));W64(image,0x1848,templateAddress);W32(image,0x1858,1000);WV(image,0x1860,old);WV(image,0x19fc,old);
        image[0x1c34]=1;W32(image,0x100,212776);W32(image,0x130,Bits(2));W32(image,0x134,Bits(4));W32(image,0x138,Bits(1));
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(0x1492,2),35);W32(image,0x1940,1);Text(image,0x1440,"Rig");Text(image,0x1c00,"Key");
        W32(image,0x558,111);W32(image,0x55c,222);W32(image,0x560,333);W32(image,0x564,444);
        byte[] template=new byte[0x500];W32(template,0x284,555);
        var npc=new O.Npc(key,actor,new(npcAddress,"npc-image","npc-image-v1",[..image],Evidence),new(templateAddress,"template","template-v1",[..template],Evidence));
        List<O.User> users=[];List<O.Member> members=[new(key,ownCell,RetailMainWrapKind.Object,"npc-membership-v1")];
        int direction=newX>=oldX?1:-1;
        float[] positions=observers?[newX+direction*radius,oldX-direction,oldX-direction*radius,size-1]:[];
        for(int n=0;n<positions.Length;n++)
        {
            ulong address=0x300000+(ulong)n*0x10000;uint id=checked((uint)(100+n));var ukey=new O.ActorKey(address,id,"user-life-"+n,false);byte[] user=new byte[0x7360];
            W64(user,0,O.UserVtable);W32(user,0x70,id);W64(user,0x118,worldAddress);WV(user,0x120,V(positions[n]));W32(user,0x144,1);W64(user,0x3720,0x800000+(ulong)n*0x100);
            users.Add(new(ukey,new(address,"user-image-"+n,"user-v1-"+n,[..user],Evidence),"connection-"+n,true));
            members.Add(new(ukey,Cell(V(positions[n])),RetailMainWrapKind.User,"user-membership-"+n));
        }
        int cellsCount=(size/32)*(size/32);List<RetailMainObjectWrap> wraps=[new(0x500000,0,0,npcAddress,ownCell,RetailMainWrapKind.Object)];
        RetailMainMovingCell[] cells=Enumerable.Range(0,cellsCount).Select(_=>new RetailMainMovingCell(0,0,0,0)).ToArray();cells[ownCell]=cells[ownCell] with {ObjectRoot=0x500000};
        for(int cell=0;cell<cellsCount;cell++)
        {
            var selected=members.Where(m=>m.Cell==cell && m.Kind==RetailMainWrapKind.User).ToArray();
            for(int n=0;n<selected.Length;n++)
            {
                ulong address=0x510000+selected[n].Actor.ObjectId*0x100ul;
                ulong previous=n==0?0:0x510000+selected[n-1].Actor.ObjectId*0x100ul,next=n+1==selected.Length?0:0x510000+selected[n+1].Actor.ObjectId*0x100ul;
                wraps.Add(new(address,next,previous,selected[n].Actor.Address,cell,RetailMainWrapKind.User));
                if(n==0)cells[cell]=cells[cell] with {UserRoot=address};
            }
        }
        var world=new RetailMainOwnedWorld("world",worldAddress,320050000,size,size,V(0,0,-64),V(size,size,64),size/32,size/64,100,10000,cells,wraps,Material(size),Source);
        var scene=new O.Scene(world,[..members],[..users],[],null,null,new(0,0,0,0,Evidence),new("managed-world","world-generation",Evidence.Operation,Evidence.Epoch,true,true),Evidence);
        var frame=RetailNpcInterserverMoveCodec.EncodeCanonical(new(7,endpoint,0,0xe0,0,endpoint,1000)).Bytes;
        List<O.Clock> clocks=[new(RetailNpcMainMotionApplication.GetterTickSite,1000,0,Evidence),new(RetailNpcMainMotionApplication.DueTickSite,1100,0,Evidence),new(RetailNpcMainMotionApplication.StartTickSite,1103,0,Evidence)];
        if(observers)clocks.Add(new(0x140534f80,6000,100,Evidence));
        return new(frame,npc,scene,[..clocks],Evidence,true,true,true,false,false);
    }
    internal static O.Input NpcImage(O.Input i,Func<O.Allocation,O.Allocation> update)=>i with {Npc=i.Npc with {Image=update(i.Npc.Image)}};
    internal static O.Input UserImage(O.Input i,int n,Func<O.Allocation,O.Allocation> update)=>i with {Scene=i.Scene with {Users=i.Scene.Users.SetItem(n,i.Scene.Users[n] with {Image=update(i.Scene.Users[n].Image)})}};
    internal static O.SharedRandom Random()
    {
        byte[] state=new byte[RetailRandomState.ByteLength];W64(state,8,1);W64(state,RetailRandomState.MaskOffset,0x5555555555555555);
        string hash=Convert.ToHexStringLower(SHA256.HashData(state));Assert.True(RetailRandomState.TryCreate(state,hash,out var parsed,out _));
        return new(RetailRandomEngine.NativeEngineAddress,"shared-rng-v1",parsed!,new("shared-engine","rng-generation",Evidence.Operation,Evidence.Epoch,true,true),Evidence);
    }
}
