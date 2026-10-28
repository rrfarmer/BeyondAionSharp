using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Aion.GameServer.GeoEngine.RetailPath;

namespace Aion.GameServer.Tests;

public sealed class RetailCollisionDataReaderTests
{
    [SkippableFact]
    public void InstalledSkyTemplePairMatchesIndependentPinnedStructureWithoutPreprocessOrRuntimeClaims()
    {
        string folder=Environment.GetEnvironmentVariable("AION_RETAIL_COLLISION_FIXTURE_ROOT")
            ?? @"D:\Aion58ServerTesting\Server\Map\Worlds\IDDF2Flying";
        string meshPath=Path.Combine(folder,"MeshMaterial.Dat"),collisionPath=Path.Combine(folder,"Collision.Dat");
        Skip.IfNot(File.Exists(meshPath)&&File.Exists(collisionPath),"Pinned Sky Temple mesh/collision pair is absent.");
        byte[] mesh=File.ReadAllBytes(meshPath),collision=File.ReadAllBytes(collisionPath);
        Assert.Equal("e964645652e44f064ebb84e0588677d36bbf4be472db88a5bd5af30057355977",Convert.ToHexString(SHA256.HashData(mesh)).ToLowerInvariant());
        Assert.Equal("e5dd2cacc876c03f939fee77610db592e77d8b0322340434f7f961f2d49b7ffc",Convert.ToHexString(SHA256.HashData(collision)).ToLowerInvariant());
        var read=RetailCollisionDataReader.ReadPair(mesh,collision,new(512,512),CollisionFixture.Options);
        Assert.True(read.Success,read.Error?.ToString());var data=read.Data!;
        Assert.Equal(597594,data.MeshParsedBytes);Assert.Equal(2693092,data.CollisionParsedBytes);
        Assert.Equal(25553,data.VertexCount);Assert.Equal(32290,data.TriangleCount);Assert.Equal(3,data.Fields.Count);
        Assert.Equal(64,data.Sectors.Count);Assert.Equal(65536,data.Cells.Count);Assert.Equal(178241,data.Bands.Count);Assert.Equal(462168,data.ReferenceCount);
        Assert.Equal(Guid.Parse("ff47ba3c-ac6d-4fbf-a6b1-a7d0945a7045"),data.MeshGuid);Assert.Equal(data.MeshGuid,data.CollisionGuid);
        var ids=new HashSet<uint>();foreach(var band in data.Bands)for(int r=0;r<band.ReferenceCount;r++)ids.Add(data.GetRawReference(band,r)&0x7fffffffu);
        Assert.Equal(16664,ids.Count);Assert.Equal(19075u,ids.Max());
        Assert.Empty(read.Diagnostics);Assert.False(read.NativeAcceptanceVerified);Assert.False(data.RuntimeVerified);
    }

    [Fact]
    public void RealLayoutOwnsBothCarriersAndPreservesRawBandsDescriptorsAndFieldPayloads()
    {
        byte[] payload = [80,0,0,0,119,0,0,0,80,0,0,0,119,0,0,0];
        var mesh = CollisionFixture.Mesh([CollisionFixture.Face()], fields: [new(15, "PlaceableObject", "earth", payload)]);
        var collision = CollisionFixture.Collision((x,y) => [new(0, 0x80000000u)]);
        var read = RetailCollisionDataReader.ReadPair(mesh, collision, new(64,64), CollisionFixture.Options);
        Assert.True(read.Success, read.Error?.ToString()); var data = read.Data!;
        Assert.Equal(3, data.VertexCount); Assert.Equal(1, data.TriangleCount); Assert.Equal(1024, data.ReferenceCount);
        Assert.Equal(new RetailCollisionTriangleDescriptor(0,3,6,172), data.GetDescriptor(0));
        Assert.Equal(0x80000000u, data.GetRawReference(data.Bands[0],0));
        Assert.Equal(payload, Assert.Single(data.Fields).Payload.ToArray());
        mesh.AsSpan().Fill(0); collision.AsSpan().Fill(0); payload.AsSpan().Fill(0);
        Assert.Equal(0xaa55aa55u, BinaryPrimitives.ReadUInt32LittleEndian(data.MeshBytes.Span));
        Assert.Equal(0x80000000u, data.GetRawReference(data.Bands[0],0));
        Assert.Equal((byte)80, Assert.Single(data.Fields).Payload.Span[0]);
        Assert.False(read.NativeAcceptanceVerified); Assert.False(data.RuntimeVerified);
    }

    [Fact]
    public void SignedRelativeOffsetsAreFloatElementOffsetsRatherThanVertexIndices()
    {
        byte[] mesh = CollisionFixture.Mesh([CollisionFixture.Face()]);
        // Same triangle cyclically reordered: base float6, relative-6,-3.
        BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(172), 6);
        BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(176), -6);
        BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(178), -3);
        var data = CollisionFixture.Read(mesh, CollisionFixture.Collision());
        var face = data.GetTriangle(0);
        Assert.Equal(new RetailSearchPoint(0,2,0), face.A); Assert.Equal(new RetailSearchPoint(0,0,0), face.B);
        Assert.Equal(new RetailSearchPoint(2,0,0), face.C);
    }

    [Theory]
    [InlineData("base",172)] [InlineData("delta1",176)] [InlineData("delta2",178)]
    public void InvalidVertexReferencesIdentifyTheActualDescriptorField(string kind, int offset)
    {
        var mesh = CollisionFixture.Mesh([CollisionFixture.Face()]);
        if (kind == "base") BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(offset), uint.MaxValue);
        else BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(offset), -1);
        Error(mesh, CollisionFixture.Collision(), RetailCollisionReadErrorCode.VertexReference,"MeshMaterial.Dat",offset);
    }

    [Fact]
    public void NativeVertexTripletAlignmentIsNotAnInventedFormatRestriction()
    {
        var mesh = CollisionFixture.Mesh([CollisionFixture.Face()]);
        BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(172), 1);
        BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(176), 0);
        BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(178), 3);
        var data = CollisionFixture.Read(mesh, CollisionFixture.Collision());
        Assert.Equal(1u, data.GetDescriptor(0).BaseFloatOffset);
    }

    [Theory]
    [InlineData(0)] [InlineData(-7)]
    public void SignedLessThanOneSectorBranchRemainsSharedStorageNotClearGeometry(int length)
    {
        var raw = CollisionFixture.Collision(emptySector: true);
        BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(128), length);
        var data = CollisionFixture.Read(CollisionFixture.Mesh([CollisionFixture.Face()]), raw);
        Assert.All(data.Cells, c => Assert.True(c.SharedEmptyStorage)); Assert.Empty(data.Bands);
        Assert.Equal(length, Assert.Single(data.Sectors).DeclaredBlobBytes);
    }

    [Fact]
    public void SignedBandCountCannotBeConvertedIntoAnUnsignedSuccessfulTraversal()
    {
        var collision = CollisionFixture.Collision(); BinaryPrimitives.WriteInt16LittleEndian(collision.AsSpan(132), -1);
        Error(CollisionFixture.Mesh([CollisionFixture.Face()]),collision,RetailCollisionReadErrorCode.UnsupportedSignedCount,"Collision.Dat",132);
    }

    [Fact]
    public void TriangleHighBitIsPreservedWhileTheUnderlyingReferenceMustStillExist()
    {
        var collision = CollisionFixture.Collision((x,y)=>x==0&&y==0 ? [new(0,0x80000001u)] : [new(0)]);
        Error(CollisionFixture.Mesh([CollisionFixture.Face()]), collision, RetailCollisionReadErrorCode.TriangleReference,"Collision.Dat",2184);
    }

    [Theory]
    [InlineData("sector-count-plane",133,132)] [InlineData("sector-blob",2180,2180)]
    public void IncompleteCarriersReportTheStartOfTheRequiredField(string _, int length, int expected)
    {
        var collision = CollisionFixture.Collision(); Array.Resize(ref collision,length);
        Error(CollisionFixture.Mesh([CollisionFixture.Face()]),collision,RetailCollisionReadErrorCode.Truncated,"Collision.Dat",expected);
    }

    [Fact]
    public void ConsumedBlobMustEndExactlyEvenWhenTheOuterFileWouldContainMoreBytes()
    {
        var collision = CollisionFixture.Collision();
        Array.Resize(ref collision,collision.Length+4);
        BinaryPrimitives.WriteInt32LittleEndian(collision.AsSpan(128),4100);
        Error(CollisionFixture.Mesh([CollisionFixture.Face()]),collision,RetailCollisionReadErrorCode.InvalidLength,"Collision.Dat",6276);
    }

    [Fact]
    public void ZeroVerticesSkipTheNativeMeshTailAndRetainItWithoutInventingTriangles()
    {
        byte[] mesh = [.. CollisionFixture.Header(),0,0,0,0,1,2,3];
        var read = RetailCollisionDataReader.ReadPair(mesh,CollisionFixture.Collision(emptySector:true),new(64,64),CollisionFixture.Options with {RequireExactEnd=false});
        Assert.True(read.Success); Assert.True(read.Data!.EmptyVertexBranch); Assert.Equal(new byte[]{1,2,3},read.Data.MeshTrailingBytes.ToArray());
        Assert.Contains(read.Diagnostics,d=>d.Code=="NativeEmptyVertexBranch");
    }

    [Fact]
    public void DuplicatesAndUnknownFieldPayloadsRemainInFileOrderAndNamesAreRawBytes()
    {
        var mesh=CollisionFixture.Mesh([CollisionFixture.Face()],fields:[new(7,"Unknown","first",[255,0,3]),new(7,"Other","second",[8,9])]);
        var read=RetailCollisionDataReader.ReadPair(mesh,CollisionFixture.Collision(),new(64,64),CollisionFixture.Options);
        Assert.True(read.Success); Assert.Equal(new uint[]{7,7},read.Data!.Fields.Select(f=>f.Id));
        Assert.Equal("first",Encoding.ASCII.GetString(read.Data.Fields[0].RawName.Span));
        Assert.Equal(new byte[]{255,0,3},read.Data.Fields[0].Payload.ToArray());
        Assert.Single(read.Diagnostics,d=>d.Code=="DuplicateFieldId");
    }

    [Fact]
    public void GuidWarningAndHeaderlessStructureDoNotBecomeCompatibilityProof()
    {
        var mesh=CollisionFixture.Mesh([CollisionFixture.Face()]); var collision=CollisionFixture.Collision(); collision[4]^=1;
        var read=RetailCollisionDataReader.ReadPair(mesh,collision,new(64,64),CollisionFixture.Options);
        Assert.True(read.Success); Assert.Single(read.Diagnostics,d=>d.Code=="GuidMismatch");
        var bare=CollisionFixture.Read(mesh[128..],collision[128..]); Assert.Null(bare.MeshGuid); Assert.Null(bare.CollisionGuid);
    }

    [Fact]
    public void NonmonotonicBandsAndNonfiniteRawCoordinatesArePreservedAsData()
    {
        var mesh=CollisionFixture.Mesh([CollisionFixture.Face()]); BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(132),0x7fc01234);
        var read=RetailCollisionDataReader.ReadPair(mesh,CollisionFixture.Collision((x,y)=>[new(4),new(2)]),new(64,64),CollisionFixture.Options);
        Assert.True(read.Success); Assert.True(float.IsNaN(read.Data!.GetTriangle(0).A.X));
        Assert.Equal(new short[]{4,2},read.Data.Bands.Take(2).Select(b=>b.Height));
        Assert.Contains(read.Diagnostics,d=>d.Code=="NonmonotonicBandHeight");
    }

    [Theory]
    [InlineData("input")] [InlineData("vertices")] [InlineData("metadata")] [InlineData("bands")] [InlineData("references")] [InlineData("names")]
    public void ExplicitManagedBudgetsFailWithoutNativeAcceptanceClaims(string limit)
    {
        var mesh=CollisionFixture.Mesh([CollisionFixture.Face()],fields:[new(1,"PlaceableObject","name",[1])]);
        var collision=CollisionFixture.Collision((x,y)=>[new(0,0)]);
        var options=limit switch
        {
            "input"=>CollisionFixture.Options with {MaxMeshBytes=4},"vertices"=>CollisionFixture.Options with {MaxVertices=1},
            "metadata"=>CollisionFixture.Options with {MaxMetadataBytes=1},"bands"=>CollisionFixture.Options with {MaxBands=1},
            "references"=>CollisionFixture.Options with {MaxReferences=1},_=>CollisionFixture.Options with {MaxNameBytes=1}
        };
        var read=RetailCollisionDataReader.ReadPair(mesh,collision,new(64,64),options);
        Assert.False(read.Success); Assert.Equal(RetailCollisionReadErrorCode.AllocationLimitExceeded,read.Error!.Code); Assert.False(read.NativeAcceptanceVerified);
    }

    [Fact]
    public void ExactEndAndForeignBandRecordsHaveExplicitBoundaries()
    {
        var mesh=CollisionFixture.Mesh([CollisionFixture.Face()]); byte[] extra=[..mesh,77];
        Error(extra,CollisionFixture.Collision(),RetailCollisionReadErrorCode.TrailingBytes,"MeshMaterial.Dat",mesh.Length);
        var data=CollisionFixture.Read(mesh,CollisionFixture.Collision((x,y)=>[new(0,0)]));
        Assert.Throws<ArgumentException>(()=>data.GetRawReference(data.Bands[0] with {HeaderFileOffset=data.Bands[0].HeaderFileOffset+4},0));
    }

    private static void Error(byte[] mesh,byte[] collision,RetailCollisionReadErrorCode code,string file,int offset)
    {
        var result=RetailCollisionDataReader.ReadPair(mesh,collision,new(64,64),CollisionFixture.Options);
        Assert.False(result.Success); Assert.Null(result.Data); Assert.Equal(code,result.Error!.Code); Assert.Equal(file,result.Error.File); Assert.Equal(offset,result.Error.FileOffset);
    }
}

internal readonly record struct CollisionFace(RetailSearchPoint A,RetailSearchPoint B,RetailSearchPoint C,byte Material);
internal sealed record CollisionBand(short Height,params uint[] References);
internal sealed record CollisionField(uint Id,string Kind,string Name,byte[] Payload);
internal static class CollisionFixture
{
    internal static readonly Guid Guid=new("11111111-2222-3333-4444-555555555555");
    internal static readonly RetailCollisionReadOptions Options=new(16*1024*1024,16*1024*1024,100000,100000,256,1000000,1000000,128L*1024*1024,1000,255,true);
    internal static readonly RetailCollisionPrepareLimits PrepareLimits=new(1000000,1000000,128L*1024*1024);
    internal static readonly RetailCollisionQueryLimits QueryLimits=new(10000,1000000,1000000,1000,100000);
    internal static readonly RetailCollisionCosineReceipt Cosine=new(true,RetailGroundPathSearch.SupportedNpcBinarySha256,
        "0x14039df80",0x3f490fdb,0x3f3504f3,"synthetic-explicit-cosine",new string('a',64),true);
    internal static CollisionFace Face(float z=0,byte material=0,bool reversed=false) => reversed
        ? new(new(0,0,z),new(0,2,z),new(2,0,z),material) : new(new(0,0,z),new(2,0,z),new(0,2,z),material);
    internal static byte[] Header()
    {
        byte[] header=new byte[128]; BinaryPrimitives.WriteUInt32LittleEndian(header,0xaa55aa55); Guid.ToByteArray().CopyTo(header,4); return header;
    }
    internal static byte[] Mesh(IReadOnlyList<CollisionFace> faces,IReadOnlyList<CollisionField>? fields=null)
    {
        using var memory=new MemoryStream(); using var writer=new BinaryWriter(memory);
        writer.Write(Header()); writer.Write(faces.Count*3);
        foreach(var face in faces) foreach(var p in new[]{face.A,face.B,face.C}){writer.Write(p.X);writer.Write(p.Y);writer.Write(p.Z);}
        if(faces.Count==0)return memory.ToArray();
        writer.Write(faces.Count);
        for(int i=0;i<faces.Count;i++){writer.Write((uint)(i*9));writer.Write((short)3);writer.Write((short)6);}
        foreach(var face in faces)writer.Write(face.Material);
        writer.Write(fields?.Count??0);
        foreach(var field in fields??[])
        {
            writer.Write(field.Id);writer.Write(Encoding.UTF8.GetBytes(field.Kind));writer.Write((byte)0);
            writer.Write(Encoding.UTF8.GetBytes(field.Name));writer.Write((byte)0);writer.Write(field.Payload.Length);writer.Write(field.Payload);
        }
        return memory.ToArray();
    }
    internal static byte[] Collision(Func<int,int,IReadOnlyList<CollisionBand>>? cell=null,bool emptySector=false,int width=64,int height=64)
    {
        using var memory=new MemoryStream();using var writer=new BinaryWriter(memory);writer.Write(Header());
        for(int sy=0;sy<height/64;sy++)for(int sx=0;sx<width/64;sx++)
        {
            if(emptySector){writer.Write(0);continue;}
            using var blob=new MemoryStream();using var bandWriter=new BinaryWriter(blob);short[] counts=new short[1024];
            for(int y=0;y<32;y++)for(int x=0;x<32;x++)
            {
                var bands=cell?.Invoke(sx*32+x,sy*32+y)??[new CollisionBand(0)];counts[y*32+x]=(short)bands.Count;
                foreach(var band in bands){bandWriter.Write(band.Height);bandWriter.Write((ushort)band.References.Length);foreach(uint id in band.References)bandWriter.Write(id);}
            }
            writer.Write((int)blob.Length);foreach(short count in counts)writer.Write(count);writer.Write(blob.ToArray());
        }
        return memory.ToArray();
    }
    internal static RetailCollisionData Read(byte[] mesh,byte[] collision,int width=64,int height=64)
    {
        var read=RetailCollisionDataReader.ReadPair(mesh,collision,new(width,height),Options);Assert.True(read.Success,read.Error?.ToString());return read.Data!;
    }
    internal static RetailHeightMap40Data Terrain(Func<int,int,byte>? selector=null,Func<int,int,ushort>? word=null,byte firstPlane=0,int width=64,int height=64)
    {
        using var memory=new MemoryStream();using var writer=new BinaryWriter(memory);writer.Write(Header());writer.Write(height/2);writer.Write(width/2);
        for(int y=0;y<=height/2;y++)for(int x=0;x<=width/2;x++)writer.Write(word?.Invoke(x,y)??(ushort)0);
        for(int y=0;y<=height/2;y++)for(int x=0;x<=width/2;x++)writer.Write(firstPlane);
        for(int y=0;y<=height/2;y++)for(int x=0;x<=width/2;x++)writer.Write(selector?.Invoke(x,y)??(byte)14);
        var read=RetailHeightMap40Reader.Read(memory.ToArray());Assert.True(read.Success,read.Error?.ToString());return read.Data!;
    }
    internal static RetailCollisionPreparedData Prepared(RetailCollisionData raw,RetailCollisionCosineReceipt? cosine=null)
    {
        var result=RetailCollisionMath.Prepare(raw,cosine??Cosine,PrepareLimits);Assert.True(result.Success,result.Detail);return result.Data!;
    }
    internal static RetailCollisionSources Sources(RetailCollisionPreparedData data,RetailHeightMap40Data terrain)
    {
        var raw=data.Raw;int width=raw.Dimensions.WidthMetres,height=raw.Dimensions.HeightMetres;
        return new(new(new("synthetic-mesh",raw.MeshSha256,raw.MeshBytes.Length),raw.MeshGuid,width,height),
            new(new("synthetic-collision",raw.CollisionSha256,raw.CollisionBytes.Length),raw.CollisionGuid,width,height),
            new(new("synthetic40",Convert.ToHexString(SHA256.HashData(terrain.RawFile.Span)).ToLowerInvariant(),terrain.RawFile.Length),terrain.BuildGuid,terrain.WorldWidthMetres,terrain.WorldHeightMetres),
            new("pinned-native-declaration",RetailGroundPathSearch.SupportedNpcBinarySha256,RetailGroundPathSearch.SupportedNpcBinaryBytes));
    }
    internal static IReadOnlyList<RetailCollisionRegionObservation> Regions(int width=64,int height=64)
    {
        var list=new List<RetailCollisionRegionObservation>();
        for(int y=0;y<height/16;y++)for(int x=0;x<width/16;x++)list.Add(new(x,y,true,"scene1",new string('b',64),[]));return list;
    }
    internal static RetailCollisionScene Scene(RetailCollisionPreparedData prepared,RetailHeightMap40Data terrain,
        IReadOnlyList<RetailCollisionRegionObservation>? regions=null,RetailCollisionMaterialPolicy? policy=null,bool missingPolicy=false)
    {
        var result=RetailCollisionScene.Create(prepared,terrain,Sources(prepared,terrain),new(new(0,0,-10240),new(prepared.Raw.Dimensions.WidthMetres,prepared.Raw.Dimensions.HeightMetres,10240)),
            new("synthetic-world","instance1","scene1","asset1",new string('c',64),"synthetic-owned-scene-only",true),
            regions??Regions(prepared.Raw.Dimensions.WidthMetres,prepared.Raw.Dimensions.HeightMetres),missingPolicy?null:policy??RetailCollisionMaterialPolicy.FromPinnedFileImage("synthetic-file-image"),new(10000,10000));
        Assert.True(result.Success,result.Detail);return result.Scene!;
    }
}
