using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.Json;

namespace Aion.GameServer.GeoEngine.RetailPath;

public enum RetailMainApplicationOrigin { ConstructedSyntheticWorld, DeclaredHistoricalObservation }
public enum RetailMainApplyStatus { CalculatedFiniteApplication, CalculatedNativeEarlyReturn, IncompleteAtChild, InvalidSource, InvalidOwnership, UnsupportedFloatingEnvironment, UnsupportedConsumedValue, BudgetExceeded }
public enum RetailMainWorldDisposition { NotReached, SameCellApplied, BoundsRejected }
public enum RetailMainWrapKind { User, Object, Housing, House }
public sealed record RetailMainSourceDeclaration(string Name, string Sha256);
public sealed record RetailMainTemplateBinding(ulong Address, int Raw354, RetailMainSourceDeclaration Source);
public sealed record RetailMainSuppressionSlot(ulong Pointer, uint? Word);
public sealed record RetailMainObjectWrap(ulong Address, ulong Next, ulong Previous, ulong ActorAddress, int ParentCell, RetailMainWrapKind Kind);
public sealed record RetailMainMovingCell(ulong UserRoot, ulong ObjectRoot, ulong HousingRoot, ulong HouseRoot);
public sealed record RetailMainGlobals(uint Telemetry14275eb74, int SerializationDebug142767930, uint TlsDepth60, RetailMainSourceDeclaration Source);
public sealed record RetailMainTickReceipt(ulong CallSite, uint Value, string ActorLifetime, string SnapshotRevision,
    string OperationRevision, RetailMainSourceDeclaration Source, RetailMainApplicationOrigin Origin);
public sealed record RetailMainLogicalWrite(string Field, ulong Before, ulong After, int Bytes, ulong Instruction);

/// <summary>Raw selected current fields, never inferred from an NPC template or name.</summary>
public sealed record RetailNpcMainActorSnapshot(ulong ObjectAddress, ulong Vtable, uint ObjectId, int NativeClass144, string? WorldIdentity,
    ulong WrapB8, uint Counter19f8, RetailNpcMainMotionState Motion, int Heading12c, uint TurnRate7c8, byte Flags1c35,
    RetailMainSuppressionSlot Suppression1a58, uint TraceOwner1ba0, RetailMainTemplateBinding Template1848,
    int Owner188c, int SpecialBroadcast1a50, int ObserverRange140, int RefCount8, int RefDebug14,
    ulong RefSource28, uint RefLine30, int LockBypass700, byte Material1426, byte LastMaterial1427,
    ImmutableArray<uint> MaterialTimers, RetailMainSourceDeclaration Source)
{
    public string SnapshotSha256 => RetailMainApplyEvidence.HashObject(new object?[] { ObjectAddress, Vtable, ObjectId, NativeClass144,
        WorldIdentity, WrapB8, Counter19f8, Motion, Heading12c, TurnRate7c8, Flags1c35, Suppression1a58,
        TraceOwner1ba0, Template1848, Owner188c, SpecialBroadcast1a50, ObserverRange140, RefCount8, RefDebug14,
        RefSource28, RefLine30, LockBypass700, Material1426, LastMaterial1427, MaterialTimers, Source });
}

/// <summary>Owned current storage under a declared synthetic mapping; not a native constructor or loaded-world receipt.</summary>
public sealed class RetailMainMaterialStorage
{
    public sealed class CellBlock
    {
        public CellBlock(ulong address, ReadOnlySpan<byte> bytes)
        {if(bytes.Length>16384)throw new ArgumentOutOfRangeException(nameof(bytes));Address=address;Bytes=ImmutableArray.CreateRange(bytes.ToArray());}
        public ulong Address { get; }
        public ImmutableArray<byte> Bytes { get; }
    }
    public RetailMainMaterialStorage(RetailHeightMap40Data heightMap, int mode1e0, ReadOnlySpan<byte> legacyTypes,
        ReadOnlySpan<byte> materialRecords, IEnumerable<CellBlock> cellBlocks, RetailMainSourceDeclaration source)
    {
        ArgumentNullException.ThrowIfNull(heightMap); ArgumentNullException.ThrowIfNull(cellBlocks); ArgumentNullException.ThrowIfNull(source);
        const int constructionLimit=128*1024*1024;
        long owned=(long)heightMap.HeightWordBytes.Length+heightMap.BytePlane1.Length+legacyTypes.Length+materialRecords.Length;
        if(owned>constructionLimit||heightMap.RawFile.Length>constructionLimit)throw new ArgumentOutOfRangeException(nameof(heightMap),"Defensive128MiB construction limit; not a process-wide memory cap.");
        Mode1e0=mode1e0; Stride1c8=heightMap.Stride; WidthMetres=heightMap.WorldWidthMetres; HeightMetres=heightMap.WorldHeightMetres;
        byte[] raw=heightMap.RawFile.ToArray();HeightFileSha256=RetailMainApplyEvidence.Hash(raw);
        HeightWords=ImmutableArray.CreateRange(raw.AsSpan(heightMap.HeightWordsFileOffset,heightMap.HeightWordBytes.Length).ToArray());
        Plane1b0=ImmutableArray.CreateRange(raw.AsSpan(heightMap.HeightWordsFileOffset+heightMap.HeightWordBytes.Length,heightMap.BytePlane1.Length).ToArray());
        LegacyTypes1b8=ImmutableArray.CreateRange(legacyTypes.ToArray()); MaterialRecords=ImmutableArray.CreateRange(materialRecords.ToArray());
        List<CellBlock> blocks=[];
        foreach(var block in cellBlocks)
        {
            if(block is null||blocks.Count>=65536)throw new ArgumentOutOfRangeException(nameof(cellBlocks));
            owned+=block.Bytes.Length;if(owned>constructionLimit)throw new ArgumentOutOfRangeException(nameof(cellBlocks),"Defensive128MiB construction limit.");
            blocks.Add(new CellBlock(block.Address,block.Bytes.AsSpan()));
        }
        CellBlocks=blocks.ToImmutableArray(); Source=source;
        SnapshotSha256=RetailMainApplyEvidence.HashObject(new object[] { Mode1e0, Stride1c8, WidthMetres, HeightMetres, HeightFileSha256,
            RetailMainApplyEvidence.Hash(HeightWords.AsSpan()),RetailMainApplyEvidence.Hash(Plane1b0.AsSpan()),
            RetailMainApplyEvidence.Hash(LegacyTypes1b8.AsSpan()),RetailMainApplyEvidence.Hash(MaterialRecords.AsSpan()),
            CellBlocks.Select(b=>new { b.Address, Length=b.Bytes.Length, Sha256=RetailMainApplyEvidence.Hash(b.Bytes.AsSpan()) }),Source });
    }
    public int Mode1e0 { get; }
    public int Stride1c8 { get; }
    public int WidthMetres { get; }
    public int HeightMetres { get; }
    public string HeightFileSha256 { get; }
    public ImmutableArray<byte> HeightWords { get; }
    public ImmutableArray<byte> Plane1b0 { get; }
    public ImmutableArray<byte> LegacyTypes1b8 { get; }
    public ImmutableArray<byte> MaterialRecords { get; }
    public ImmutableArray<CellBlock> CellBlocks { get; }
    public RetailMainSourceDeclaration Source { get; }
    public string SnapshotSha256 { get; }
    public bool NativeConstructorVerified => false;
}

public sealed class RetailMainOwnedWorld
{
    public RetailMainOwnedWorld(string identity, ulong nativeAddress, uint baseId24, int widthMetres, int heightMetres,
        RetailMainVectorWords minimum38, RetailMainVectorWords maximum44, int movingStride50, int rcuStride5c,
        int policy14ac0, int policy14ac4, IEnumerable<RetailMainMovingCell> cells, IEnumerable<RetailMainObjectWrap> wraps,
        RetailMainMaterialStorage material, RetailMainSourceDeclaration source)
    {
        Identity=identity; NativeAddress=nativeAddress; BaseId24=baseId24; WidthMetres=widthMetres; HeightMetres=heightMetres; Minimum38=minimum38; Maximum44=maximum44;
        MovingStride50=movingStride50; RcuStride5c=rcuStride5c; Policy14ac0=policy14ac0; Policy14ac4=policy14ac4;
        ArgumentNullException.ThrowIfNull(cells);ArgumentNullException.ThrowIfNull(wraps);ArgumentNullException.ThrowIfNull(material);ArgumentNullException.ThrowIfNull(source);
        Cells=cells.Take(1_048_577).ToImmutableArray();Wraps=wraps.Take(1_048_577).ToImmutableArray();
        if(Cells.Length>1_048_576||Wraps.Length>1_048_576)throw new ArgumentOutOfRangeException(nameof(cells),"Defensive construction count cap.");
        Material=material; Source=source;
        SnapshotSha256=RetailMainApplyEvidence.HashObject(new object[] { Identity, NativeAddress, BaseId24, WidthMetres, HeightMetres, Minimum38,
            Maximum44, MovingStride50,RcuStride5c,Policy14ac0,Policy14ac4,Cells,Wraps,Material.SnapshotSha256,Source });
    }
    public string Identity { get; }
    public ulong NativeAddress { get; }
    public uint BaseId24 { get; }
    public int WidthMetres { get; }
    public int HeightMetres { get; }
    public RetailMainVectorWords Minimum38 { get; }
    public RetailMainVectorWords Maximum44 { get; }
    public int MovingStride50 { get; }
    public int RcuStride5c { get; }
    public int Policy14ac0 { get; }
    public int Policy14ac4 { get; }
    public ImmutableArray<RetailMainMovingCell> Cells { get; }
    public ImmutableArray<RetailMainObjectWrap> Wraps { get; }
    public RetailMainMaterialStorage Material { get; }
    public RetailMainSourceDeclaration Source { get; }
    public string SnapshotSha256 { get; }
    public bool CurrentNativeSceneObserved => false;
}

public sealed record RetailMainApplyContext(string MainBinarySha256, string BridgeEvidenceSha256, string ApplicationEvidenceSha256,
    string GuardEvidenceSha256, string StopEvidenceSha256, string ActorLifetime, string SnapshotRevision, string OperationRevision,
    string ActorSnapshotSha256, string WorldSnapshotSha256, string GlobalsSnapshotSha256, string CanonicalRecordSha256,
    RetailMainApplicationOrigin Origin, bool NearestEven, bool ExceptionsMasked, bool Sse2, bool FlushToZero, bool DenormalsAreZero);
public sealed record RetailMainApplyLimits(int MaxCells=65536, int MaxWraps=262144, int MaxMaterialBytes=128*1024*1024);
public sealed record RetailMainApplyResult(RetailMainApplyStatus Status, RetailNpcMainActorSnapshot? FinalActor,
    RetailMainOwnedWorld? FinalWorld, RetailMainGlobals? FinalGlobals, ImmutableArray<byte> ClientPacket,
    RetailMainWorldDisposition WorldDisposition, ImmutableArray<RetailMainLogicalWrite> CalculatedPrefixWrites,
    ImmutableArray<string> EvaluatedSkips, ulong? IncompleteChild, string? Detail)
{
    public bool Calculated => Status is RetailMainApplyStatus.CalculatedFiniteApplication or RetailMainApplyStatus.CalculatedNativeEarlyReturn;
    public bool HostEffectsExecuted => false;
    public bool CanCommitToHost => false;
    public bool NativeTargetExecuted => false;
    public bool CurrentNativeSceneObserved => false;
    public bool ActivationReady => false;
}

internal sealed class RetailMainApplyFault(RetailMainApplyStatus status,string detail,ulong? child=null):Exception(detail)
{ public RetailMainApplyStatus Status { get; }=status; public ulong? Child { get; }=child; }

internal static class RetailMainApplyEvidence
{
    internal const string ApplicationSha="17f232775ad6ff991923a83b39c5f67c621ce333a1d89568cfe40d7baf718fa2";
    internal const string GuardSha="489b36c4d0ca333906f2942cdcbf61c8c87a929ecb0810373052547e3e83933a";
    internal const string StopSha="116c70d6d635c66c10324735fd2d23f090878187d7a146bf042c86f5ff8b8d11";
    internal static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    internal static string HashObject(object value)=>Hash(JsonSerializer.SerializeToUtf8Bytes(value));
    internal static bool Digest(string? value)=>value is { Length:64 }&&value.All(Uri.IsHexDigit);
    internal static void Require([DoesNotReturnIf(false)] bool condition,string detail,RetailMainApplyStatus status=RetailMainApplyStatus.InvalidOwnership)
    { if(!condition)throw new RetailMainApplyFault(status,detail); }
    internal static void Source(RetailMainSourceDeclaration? source)
    { Require(source is not null&&!string.IsNullOrWhiteSpace(source.Name)&&Digest(source.Sha256),"Explicit source declaration required; this is not native observation.",RetailMainApplyStatus.InvalidSource); }
    internal static void Child(ulong address,string detail)=>throw new RetailMainApplyFault(RetailMainApplyStatus.IncompleteAtChild,detail,address);
}

internal static class RetailMainApplyMath
{
    internal static float Float(uint word)
    {
        uint exp=word&0x7f800000;
        if(exp==0x7f800000||(exp==0&&(word&0x7fffff)!=0))throw new RetailMainApplyFault(RetailMainApplyStatus.UnsupportedConsumedValue,"Consumed nonfinite/subnormal word.");
        return BitConverter.UInt32BitsToSingle(word);
    }
    internal static uint Word(float value){uint w=BitConverter.SingleToUInt32Bits(value);_=Float(w);return w;}
    internal static uint Integer(uint value)=>Word((float)value);
    internal static uint SignedInteger(int value)=>Word((float)value);
    internal static uint Add(uint a,uint b)=>Word(RetailNpcNativeHeading.Add32(Float(a),Float(b)));
    internal static uint Sub(uint a,uint b)=>Word(RetailNpcNativeHeading.Sub32(Float(a),Float(b)));
    internal static uint Mul(uint a,uint b)=>Underflow(Word(RetailNpcNativeHeading.Mul32(Float(a),Float(b))),a,b);
    internal static uint Div(uint a,uint b)=>Underflow(Word(RetailNpcNativeHeading.Div32(Float(a),Float(b))),a,b);
    private static uint Underflow(uint r,uint a,uint b)
    {if((r&0x7fffffff)==0&&(a&0x7fffffff)!=0&&(b&0x7fffffff)!=0)throw new RetailMainApplyFault(RetailMainApplyStatus.UnsupportedConsumedValue,"Nonzero arithmetic underflow to zero.");return r;}
    internal static int Truncate32(uint w)
    {double f=Float(w);RetailMainApplyEvidence.Require(f>=int.MinValue&&f<2147483648d,"CVTTSS2SI32 outside admitted range.",RetailMainApplyStatus.UnsupportedConsumedValue);return (int)f;}
    internal static long Truncate64(uint w)
    {double f=Float(w);RetailMainApplyEvidence.Require(f>=-9223372036854775808d&&f<9223372036854775808d,"CVTTSS2SI64 outside admitted range.",RetailMainApplyStatus.UnsupportedConsumedValue);return (long)f;}
    internal static uint[] Components(RetailMainVectorWords v)=>[v.X,v.Y,v.Z];
    internal static RetailMainVectorWords Vector(uint[] v)=>new(v[0],v[1],v[2]);
    internal static bool Equal(RetailMainVectorWords a,RetailMainVectorWords b)=>Float(a.X)==Float(b.X)&&Float(a.Y)==Float(b.Y)&&Float(a.Z)==Float(b.Z);
    internal static bool Near(RetailMainVectorWords a,RetailMainVectorWords b)
    {
        uint[] x=Components(a),y=Components(b);
        for(int i=0;i<3;i++){float d=Float(Sub(x[i],y[i]));if(!(d<Float(0x38d1b717)&&d>Float(0xb8d1b717)))return false;}
        return true;
    }
}
