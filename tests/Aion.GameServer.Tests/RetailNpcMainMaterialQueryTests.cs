using System.Buffers.Binary;
using System.Collections.Immutable;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Aion.GameServer.GeoEngine.RetailPath;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcMainMaterialQueryTests
{
    public static IEnumerable<object[]> MaterialCases()=>MainApplicationFixture.Cases("material_vectors");
    [Theory,MemberData(nameof(MaterialCases))]
    public void IndependentlyDerivedRawMaterialBranches(string name,string json)
    {
        using var doc=JsonDocument.Parse(json);var v=doc.RootElement;
        int mode=v.GetProperty("mode1e0").GetInt32();ushort height=v.GetProperty("height_word").GetUInt16();
        byte plane=v.GetProperty("plane1b0").GetByte();byte legacy=v.GetProperty("separate_legacy_type1b8").ValueKind==JsonValueKind.Null?(byte)0:v.GetProperty("separate_legacy_type1b8").GetByte();
        var world=MainApplicationFixture.World(MainApplicationFixture.Material(height,plane,mode,legacy));
        var result=RetailNpcMainMaterialQuery.Query(world,new(MainApplicationFixture.Bits(12),MainApplicationFixture.Bits(4),Convert.ToUInt32(v.GetProperty("position_z_bits").GetString(),16)));
        if(v.GetProperty("typed_unsupported_selector").GetBoolean())
        {Assert.Equal(RetailMainMaterialStatus.IncompleteAtSelector,result.Status);Assert.Null(result.Material);Assert.Equal(0x1400ad7afUL,result.IncompleteChild);}
        else
        {Assert.True(result.Computed,name+":"+result.Detail);Assert.Equal(v.GetProperty("computed_material_byte").GetByte(),result.Material);Assert.Equal(v.GetProperty("derived_selector").GetInt32(),result.Selector);}
        Assert.False(result.NativeConstructorVerified);Assert.False(result.ActivationReady);
    }

    [Theory]
    [InlineData(0,"skillID0")]
    [InlineData(1,"level0")]
    [InlineData(2,"interval0")]
    [InlineData(3,"ownerlessNPC/targetmask")]
    public void FourEntriesMustEachProveTheirOwnOrderedNoop(int guard,string reason)
    {
        byte[] records=MainApplicationFixture.ActiveRecords();
        for(int i=0;i<4;i++)
        {
            int r=0x44;
            if(guard==0)BinaryPrimitives.WriteUInt32LittleEndian(records.AsSpan(r+4+4*i),0);
            if(guard==1)BinaryPrimitives.WriteUInt16LittleEndian(records.AsSpan(r+0x14+2*i),0);
            if(guard==2)BinaryPrimitives.WriteUInt32LittleEndian(records.AsSpan(r+0x1c+4*i),0);
            if(guard==3)BinaryPrimitives.WriteUInt32LittleEndian(records.AsSpan(r+0x2c+4*i),2);
        }
        var world=MainApplicationFixture.World(MainApplicationFixture.Material(records:records));
        var result=RetailNpcMainMaterialQuery.EvaluateEffects(world,MainApplicationFixture.Actor(),1);
        Assert.True(result.Computed);Assert.Equal(Enumerable.Range(0,4).Select(i=>$"material[{i}]:{reason}"),result.EvaluatedSkips);
    }

    [Theory,InlineData(0),InlineData(1),InlineData(2),InlineData(3)]
    public void LaterActiveEntryNeverPublishesProvisionalNoEffects(int active)
    {
        byte[] all=MainApplicationFixture.ActiveRecords(),records=new byte[256*0x44];int r=0x44;
        all.AsSpan(r+4+4*active,4).CopyTo(records.AsSpan(r+4+4*active));
        all.AsSpan(r+0x14+2*active,2).CopyTo(records.AsSpan(r+0x14+2*active));
        all.AsSpan(r+0x1c+4*active,4).CopyTo(records.AsSpan(r+0x1c+4*active));
        all.AsSpan(r+0x2c+4*active,4).CopyTo(records.AsSpan(r+0x2c+4*active));
        var w=MainApplicationFixture.World(MainApplicationFixture.Material(records:records));
        var result=RetailNpcMainMaterialQuery.EvaluateEffects(w,MainApplicationFixture.Actor(),1);
        Assert.Equal(RetailMainMaterialStatus.IncompleteAtActiveMaterial,result.Status);Assert.Equal(active,result.EvaluatedSkips.Length);
        Assert.Equal(0x14037c1fdUL,result.IncompleteChild);
    }

    [Fact]
    public void ComputedZeroIsARegionBoundaryIncludingConstantReturnSelector12()
    {
        var world=MainApplicationFixture.World(MainApplicationFixture.Material(height:3));
        var query=RetailNpcMainMaterialQuery.Query(world,MainApplicationFixture.V(12,4,1));
        Assert.True(query.Computed);Assert.Equal((byte)0,query.Material);Assert.Equal(12,query.Selector);
        var effects=RetailNpcMainMaterialQuery.EvaluateEffects(world,MainApplicationFixture.Actor(),query.Material!.Value);
        Assert.Equal(RetailMainMaterialStatus.IncompleteAtRegionFallback,effects.Status);Assert.Equal(0x140d76e00UL,effects.IncompleteChild);
    }

    [Theory,InlineData(0),InlineData(1),InlineData(2),InlineData(3)]
    public void RealStorageCannotBeReplacedByMissingOrAliasedBuffers(int invalid)
    {
        var original=MainApplicationFixture.Material();
        var material=MainApplicationFixture.Material(mode:invalid==0?0:1,legacyLength:invalid==0?1:null,
            records:invalid==1?new byte[1]:null,blocks:invalid==2?[]:invalid==3?[new(0,new byte[16384])]:null);
        var result=RetailNpcMainMaterialQuery.Query(MainApplicationFixture.World(material),MainApplicationFixture.V(12,4,1));
        Assert.Equal(RetailMainMaterialStatus.InvalidStorage,result.Status);Assert.Null(result.Material);Assert.NotEqual(original.SnapshotSha256,material.SnapshotSha256);
    }

    [Fact]
    public void MaterialOwnsBytesAndReportsItsActualFileIdentity()
    {
        byte[] records=new byte[256*0x44],bytes=new byte[16384];
        var material=MainApplicationFixture.Material(records:records,blocks:[new(0x100000,bytes)]);
        string digest=material.SnapshotSha256;records[0x48]=7;bytes[0]=7;
        Assert.Equal(digest,material.SnapshotSha256);Assert.Equal((byte)0,material.MaterialRecords[0x48]);Assert.Equal((byte)0,material.CellBlocks[0].Bytes[0]);
        Assert.Equal(64,material.HeightFileSha256.Length);Assert.False(material.NativeConstructorVerified);
    }

    [Fact]
    public void MaterialBudgetFailureNeverReturnsAComputedMaterial()
    {
        var result=RetailNpcMainMaterialQuery.Query(MainApplicationFixture.World(),MainApplicationFixture.V(12,4,1),new(MaxMaterialBytes:1));
        Assert.Equal(RetailMainMaterialStatus.BudgetExceeded,result.Status);Assert.Null(result.Material);
    }
}

internal static class MainApplicationFixture
{
    internal const string Hash="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    internal static RetailMainSourceDeclaration Source(string name)=>new("constructed-synthetic/"+name,Hash);
    internal static uint Bits(float v)=>BitConverter.SingleToUInt32Bits(v);
    internal static RetailMainVectorWords V(float x,float y,float z)=>new(Bits(x),Bits(y),Bits(z));
    internal static RetailNpcMainActorSnapshot Actor()=>new(0x2000,RetailNpcMainMotion.NpcVtable,0x11223344,2,"world1",0x4000,7,
        new(V(12,4,1),V(0,4,1),V(6,0,0),V(30,4,1),1000,3000,Bits(6)),0,Bits(.5f),0,
        new(0,null),0,new(0x6000,0,Source("template")),0,0,0,1,0,0,0,1,0,0,[0,0,0,0],Source("actor"));
    internal static RetailMainGlobals Globals()=>new(0,0,10,Source("globals"));
    internal static RetailMainMaterialStorage Material(ushort height=0,byte plane=1,int mode=1,byte legacy=0,
        int? legacyLength=null,byte[]? records=null,IEnumerable<RetailMainMaterialStorage.CellBlock>? blocks=null,
        Func<int,int,ushort>? sampleHeight=null)
    {
        const int samples=33*33;byte[] raw=new byte[8+4*samples];
        BinaryPrimitives.WriteInt32LittleEndian(raw,32);BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(4),32);
        for(int i=0;i<samples;i++)
        {
            ushort word=sampleHeight?.Invoke(i%33,i/33)??height;
            BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(8+2*i),word);raw[8+2*samples+i]=plane;
        }
        var parsed=RetailHeightMap40Reader.Read(raw,new(){RequireExactEnd=true,ExpectedWorldWidthMetres=64,ExpectedWorldHeightMetres=64});
        Assert.True(parsed.Success,parsed.Error?.ToString());
        byte[] types=Enumerable.Repeat(legacy,legacyLength??samples).ToArray();
        return new(parsed.Data!,mode,types,records??new byte[256*0x44],blocks??[new(0x100000,new byte[16384])],Source("material-mapping"));
    }
    internal static RetailMainOwnedWorld World(RetailMainMaterialStorage? material=null,IEnumerable<RetailMainMovingCell>? cells=null,
        IEnumerable<RetailMainObjectWrap>? wraps=null,int policy0=100,int policy4=10000)=>new("world1",0x9000,320050000,64,64,V(0,0,-64),V(64,64,64),2,1,
            policy0,policy4,cells??[new(0,0x4000,0,0),new(0,0,0,0),new(0,0,0,0),new(0,0,0,0)],
            wraps??[new(0x4000,0,0,0x2000,0,RetailMainWrapKind.Object)],material??Material(),Source("complete-current-world"));
    internal static byte[] ActiveRecords()
    {
        byte[] r=new byte[256*0x44];for(int i=0;i<4;i++)
        {BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(0x44+4+i*4),123);BinaryPrimitives.WriteUInt16LittleEndian(r.AsSpan(0x44+0x14+i*2),1);
         BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(0x44+0x1c+i*4),100);BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(0x44+0x2c+i*4),3);}
        return r;
    }
    internal static byte[] Frame(RetailMainVectorWords? stop=null,RetailMainVectorWords? goal=null,uint duration=2000,byte flags=0xe0,byte heading=60,byte sequence=7)
        =>RetailNpcInterserverMoveCodec.EncodeCanonical(new(0x11223344,stop??V(18,4,1),heading,flags,sequence,goal??V(30,4,1),duration)).Bytes.ToArray();
    internal static RetailMainApplyContext Context(RetailNpcMainActorSnapshot a,RetailMainOwnedWorld? w,RetailMainGlobals g,byte[] frame)=>new(
        RetailNpcMainMotion.MainBinarySha256,RetailNpcMainMotion.GetterEvidenceSha256,RetailNpcMainMotionApplication.ApplicationEvidenceSha256,
        RetailNpcMainMotionApplication.GuardEvidenceSha256,RetailNpcMainMotionApplication.StopEvidenceSha256,"life1","snapshot1","op1",
        a.SnapshotSha256,w?.SnapshotSha256??RetailNpcMainMotionApplication.HashCanonicalRecord([]),RetailNpcMainMotionApplication.HashGlobals(g),
        RetailNpcMainMotionApplication.HashCanonicalRecord(frame),RetailMainApplicationOrigin.ConstructedSyntheticWorld,true,true,true,false,false);
    internal static RetailMainTickReceipt[] Ticks()=>[Tick(RetailNpcMainMotionApplication.GetterTickSite,2000),Tick(RetailNpcMainMotionApplication.DueTickSite,2100),Tick(RetailNpcMainMotionApplication.StartTickSite,2103)];
    internal static RetailMainTickReceipt Tick(ulong site,uint value)=>new(site,value,"life1","snapshot1","op1",Source("individual-original-tick"),RetailMainApplicationOrigin.ConstructedSyntheticWorld);
    internal static RetailMainApplyResult Run(RetailNpcMainActorSnapshot? actor=null,RetailMainOwnedWorld? world=null,byte[]? frame=null,
        RetailMainGlobals? globals=null,IReadOnlyList<RetailMainTickReceipt>? ticks=null,RetailMainApplyContext? context=null,RetailMainApplyLimits? limits=null)
    {
        var a=actor??Actor();var w=world??World();var f=frame??Frame();var g=globals??Globals();
        return RetailNpcMainMotionApplication.Calculate(f,a,w,g,ticks??Ticks(),context??Context(a,w,g,f),limits);
    }
    // Original externally derived14 motion/12 material records; expected values never come from the C# implementation.
    private const string Corpus="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";
    private const string CorpusSha="cbeae04b1100872601a438f52ac0d68c1e263ca66e167f06eb28836124a7be8f";
    internal static JsonDocument CorpusDocument()
    {
        using var gzip=new GZipStream(new MemoryStream(Convert.FromBase64String(Corpus)),CompressionMode.Decompress);
        using var output=new MemoryStream();gzip.CopyTo(output);byte[] bytes=output.ToArray();
        Assert.Equal(CorpusSha,Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());return JsonDocument.Parse(bytes);
    }
    internal static IEnumerable<object[]> Cases(string field)
    {
        using var doc=CorpusDocument();foreach(var row in doc.RootElement.GetProperty(field).EnumerateArray())yield return [row.GetProperty("name").GetString()!,row.GetRawText()];
    }
    internal static RetailMainVectorWords ReadVector(JsonElement e)=>new(Convert.ToUInt32(e[0].GetString(),16),Convert.ToUInt32(e[1].GetString(),16),Convert.ToUInt32(e[2].GetString(),16));
}
