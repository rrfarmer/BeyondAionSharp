using System.Buffers.Binary;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Aion.GameServer.GeoEngine.RetailPath;

/// <summary>Raw words also preserve signed zero and unconsumed exceptional payloads.</summary>
public readonly record struct RetailMainVectorWords(uint X, uint Y, uint Z);
public sealed record RetailNpcMainMotionState(RetailMainVectorWords Endpoint120,
    RetailMainVectorWords PriorEndpoint1860, RetailMainVectorWords Velocity186c,
    RetailMainVectorWords ContinuingGoal19fc, uint Start185c, uint Due1858, uint CurrentSpeed7c0);
/// <summary>Caller declaration, never a current-process observation or host FP measurement.</summary>
public sealed record RetailNpcMainMotionContext(string MainBinarySha256, string GetterEvidenceSha256,
    ulong NpcVtable, string ActorLifetime, string SnapshotRevision, string OperationRevision, string InputEvidenceSha256,
    bool NearestEven, bool ExceptionsMasked, bool FlushToZero, bool DenormalsAreZero, bool Sse2,
    bool ConstructedSyntheticInput);
public sealed record RetailNpcMainGetterTick(uint Value,ulong NativeCallSite,string ActorLifetime,
    string SnapshotRevision,string OperationRevision,string EvidenceSha256,bool ConstructedSyntheticInput);
public enum RetailMainLocationStatus { CalculatedConditionalLocation, SourceIdentityMismatch, UnsupportedFloatingEnvironment, UnsupportedConsumedValue, InvalidOwnership }
public sealed record RetailMainLocationResult(RetailMainLocationStatus Status, RetailMainVectorWords? Location,
    uint? PastDue, string? Branch, FrozenDictionary<string,uint> Stages, string? Detail)
{
    public bool Calculated => Status == RetailMainLocationStatus.CalculatedConditionalLocation;
    public bool NativeTargetExecuted => false;
    public bool HostFloatingStateMeasured => false;
    public bool WholeMainApplicationCovered => false;
    public bool ActivationReady => false;
}

/// <summary>
/// Inactive getter140b18010 through actual NPC slot20. Java NpcMoveController and
/// SM_MOVE were read first. Main's endpoint and effective location are distinct.
/// Ordered scalar binary32, explicit RNE/masked/SSE2/FTZ-DAZ-off declaration;
/// consumed nonfinite, subnormal and under/overflow arithmetic is outside admission.
/// No native locks, clocks, world membership, MoveNpc or 4.8 packet adaptation run here.
/// </summary>
public static class RetailNpcMainMotion
{
    public const string MainBinarySha256="da1ee5633d65a7ebb436b29d15ca24e46ab4f267449384d177bf20d2d26eb8f1";
    public const string GetterEvidenceSha256="e35f067682016fd4d357c16cfc3b8052e26559372af736356159bb3a2efa168b";
    public const ulong NpcVtable=0x1412b8470, NativeGetter=0x140b18010;
    private const uint MillisecondsToSeconds=0x3a83126f;
    private sealed class ConsumedValueException(string message):Exception(message);

    public static RetailMainLocationResult Calculate(RetailNpcMainMotionState state,RetailNpcMainGetterTick tick,
        RetailNpcMainMotionContext context)
    {
        ArgumentNullException.ThrowIfNull(state);ArgumentNullException.ThrowIfNull(tick);ArgumentNullException.ThrowIfNull(context);
        Dictionary<string,uint> stages=[];
        RetailMainLocationResult Result(RetailMainLocationStatus status,RetailMainVectorWords? location=null,
            uint? pastDue=null,string? branch=null,string? detail=null)=>new(status,location,pastDue,branch,stages.ToFrozenDictionary(StringComparer.Ordinal),detail);
        if(context.MainBinarySha256!=MainBinarySha256||context.GetterEvidenceSha256!=GetterEvidenceSha256||context.NpcVtable!=NpcVtable)
            return Result(RetailMainLocationStatus.SourceIdentityMismatch,detail:"Actual supported binary/getter/class declaration required.");
        static bool Digest(string? value)=>value is {Length:64}&&value.All(Uri.IsHexDigit);
        if(string.IsNullOrWhiteSpace(context.ActorLifetime)||string.IsNullOrWhiteSpace(context.SnapshotRevision)||string.IsNullOrWhiteSpace(context.OperationRevision)||
            !Digest(context.InputEvidenceSha256)||!Digest(tick.EvidenceSha256)||tick.NativeCallSite!=0x140b1802e||
            tick.ActorLifetime!=context.ActorLifetime||tick.SnapshotRevision!=context.SnapshotRevision||tick.OperationRevision!=context.OperationRevision||
            tick.ConstructedSyntheticInput!=context.ConstructedSyntheticInput)
            return Result(RetailMainLocationStatus.InvalidOwnership,detail:"Explicit actor lifetime, snapshot revision and input evidence required; no current-state proof is implied.");
        if(!context.NearestEven||!context.ExceptionsMasked||context.FlushToZero||context.DenormalsAreZero||!context.Sse2)
            return Result(RetailMainLocationStatus.UnsupportedFloatingEnvironment,detail:"Only declared RNE/masked/SSE2 with FTZ and DAZ off is admitted.");
        try
        {
            uint originalGetterTick=tick.Value;
            // Unsigned comparisons are intentional, including across tick wrap.
            if(state.Start185c>=originalGetterTick)
                return Result(RetailMainLocationStatus.CalculatedConditionalLocation,state.PriorEndpoint1860,0,"before-or-at-start");
            if(state.Due1858>=originalGetterTick)
            {
                uint seconds=Mul(Integer(unchecked(state.Due1858-originalGetterTick)),MillisecondsToSeconds);
                stages.Add("remaining_seconds_bits",seconds);
                var position=Sub(state.Endpoint120,Mul(state.Velocity186c,seconds));
                return Result(RetailMainLocationStatus.CalculatedConditionalLocation,position,0,"through-due");
            }
            var direction=Sub(state.ContinuingGoal19fc,state.Endpoint120);
            uint length=Sqrt(DotYxz(direction));stages.Add("sqrt_bits",length);
            if(Float(length)>0)direction=Mul(direction,Div(state.CurrentSpeed7c0,length));
            uint elapsed=Mul(Integer(unchecked(originalGetterTick-state.Due1858)),MillisecondsToSeconds);
            stages.Add("seconds_bits",elapsed);
            var delta=Mul(direction,elapsed);
            var negative=Sub(default(RetailMainVectorWords),delta);
            uint travel=Add(Add(Mul(negative.X,negative.X),Mul(negative.Y,negative.Y)),Mul(negative.Z,negative.Z));
            uint remaining=DotYxz(Sub(state.Endpoint120,state.ContinuingGoal19fc));
            stages.Add("travel_squared_bits",travel);stages.Add("remaining_squared_bits",remaining);
            bool cap=Float(travel)>Float(remaining);
            return Result(RetailMainLocationStatus.CalculatedConditionalLocation,
                cap?state.ContinuingGoal19fc:Add(delta,state.Endpoint120),1,cap?"after-due-goal-cap":"after-due-advance");
        }
        catch(ConsumedValueException error){return Result(RetailMainLocationStatus.UnsupportedConsumedValue,detail:error.Message);}
    }
    private static float Float(uint word)
    {
        uint exponent=word&0x7f800000;
        if(exponent==0x7f800000||(exponent==0&&(word&0x7fffff)!=0))
            throw new ConsumedValueException("Consumed arithmetic word is nonfinite or subnormal.");
        return BitConverter.UInt32BitsToSingle(word);
    }
    private static uint Word(float value){uint word=BitConverter.SingleToUInt32Bits(value);_=Float(word);return word;}
    [MethodImpl(MethodImplOptions.NoInlining)] private static float UInt32ToFloat(uint value)=>(float)value;
    private static uint Integer(uint value)=>Word(UInt32ToFloat(value));
    private static uint Add(uint a,uint b)=>Word(RetailNpcNativeHeading.Add32(Float(a),Float(b)));
    private static uint Sub(uint a,uint b)=>Word(RetailNpcNativeHeading.Sub32(Float(a),Float(b)));
    private static uint Mul(uint a,uint b)=>NonzeroUnderflow(Word(RetailNpcNativeHeading.Mul32(Float(a),Float(b))),a,b);
    private static uint Div(uint a,uint b)=>NonzeroUnderflow(Word(RetailNpcNativeHeading.Div32(Float(a),Float(b))),a,b);
    private static uint NonzeroUnderflow(uint result,uint a,uint b)
    {if((result&0x7fffffff)==0&&(a&0x7fffffff)!=0&&(b&0x7fffffff)!=0)throw new ConsumedValueException("Nonzero arithmetic underflow to zero is outside admission.");return result;}
    private static uint Sqrt(uint a)=>Word(System.MathF.Sqrt(Float(a)));
    private static uint DotYxz(RetailMainVectorWords v)=>Add(Add(Mul(v.Y,v.Y),Mul(v.X,v.X)),Mul(v.Z,v.Z));
    private static RetailMainVectorWords Add(RetailMainVectorWords a,RetailMainVectorWords b)=>new(Add(a.X,b.X),Add(a.Y,b.Y),Add(a.Z,b.Z));
    private static RetailMainVectorWords Sub(RetailMainVectorWords a,RetailMainVectorWords b)=>new(Sub(a.X,b.X),Sub(a.Y,b.Y),Sub(a.Z,b.Z));
    private static RetailMainVectorWords Mul(RetailMainVectorWords a,uint b)=>new(Mul(a.X,b),Mul(a.Y,b),Mul(a.Z,b));
}

public sealed record RetailNpcInterserverMoveWords(uint ObjectId,RetailMainVectorWords Position,byte Heading,
    byte Flags,byte Sequence,RetailMainVectorWords? ExtendedGoal,uint Duration);
public enum RetailCanonicalMoveStatus { CanonicalProducerLayout, OutsideCanonicalProducerLayout }
public enum RetailMainMoveGuardResult { HeadingRejected, SequenceRejected, HeadingAndSequencePassed }
public sealed record RetailCanonicalMoveResult(RetailCanonicalMoveStatus Status,RetailNpcInterserverMoveWords? Record,string? Detail)
{
    /// <summary>OnRead logs some noncanonical headers and continues; conservative rejection here is not native rejection.</summary>
    public bool ClaimsNativeFramerRejection => false;
    public bool NativeTargetExecuted => false;
    public bool ActivationReady => false;
}
public sealed record RetailCanonicalMoveEncoding(RetailCanonicalMoveStatus Status,ImmutableArray<byte> Bytes,string? Detail);
public sealed record RetailMainGoalResolution(bool Available,RetailMainVectorWords? Goal,uint? Duration,string? Detail);

/// <summary>
/// Exact canonical NPC14019c2c0 producer layout, not the entire tolerant Main OnRead
/// parser or a 4.8 client packet. Raw float words are never numerically interpreted.
/// Queue hash/key ownership, delivery, lookup/class, locking and world effects are absent.
/// </summary>
public static class RetailNpcInterserverMoveCodec
{
    public static RetailCanonicalMoveEncoding EncodeCanonical(RetailNpcInterserverMoveWords move)
    {
        ArgumentNullException.ThrowIfNull(move);
        bool extended=(move.Flags&0xc0)==0xc0;
        if(extended&&move.ExtendedGoal is null)
            return new(RetailCanonicalMoveStatus.OutsideCanonicalProducerLayout,ImmutableArray<byte>.Empty,"Extended record requires its goal; no prior-goal fallback.");
        byte[] bytes=new byte[extended?44:28];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes,(ushort)bytes.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2),4);bytes[4]=0xac;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(5),0xfffb);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(7),unchecked((ushort)move.ObjectId));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(9),move.ObjectId);WriteVector(bytes.AsSpan(13),move.Position);
        bytes[25]=move.Heading;bytes[26]=move.Flags;bytes[27]=move.Sequence;
        if(extended){WriteVector(bytes.AsSpan(28),move.ExtendedGoal!.Value);BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(40),move.Duration);}
        return new(RetailCanonicalMoveStatus.CanonicalProducerLayout,ImmutableArray.CreateRange(bytes),null);
    }
    public static RetailCanonicalMoveResult DecodeCanonical(ReadOnlySpan<byte> bytes)
    {
        static RetailCanonicalMoveResult Reject(string reason)=>new(RetailCanonicalMoveStatus.OutsideCanonicalProducerLayout,null,reason);
        if(bytes.Length is not 28 and not 44)return Reject("Canonical record length must be28 or44.");
        if(BinaryPrimitives.ReadUInt16LittleEndian(bytes)!=bytes.Length||BinaryPrimitives.ReadUInt16LittleEndian(bytes[2..])!=4||
            bytes[4]!=0xac||BinaryPrimitives.ReadUInt16LittleEndian(bytes[5..])!=0xfffb)
            return Reject("Outside the pinned producer header; no native OnRead rejection is asserted.");
        uint id=BinaryPrimitives.ReadUInt32LittleEndian(bytes[9..]);
        if(BinaryPrimitives.ReadUInt16LittleEndian(bytes[7..])!=unchecked((ushort)id))
            return Reject("Producer key must equal low16objectID; native handler does not itself check equality.");
        bool extended=(bytes[26]&0xc0)==0xc0;
        if(extended!=(bytes.Length==44))return Reject("Canonical extension length disagrees with flags c0.");
        return new(RetailCanonicalMoveStatus.CanonicalProducerLayout,new(id,ReadVector(bytes[13..]),bytes[25],bytes[26],bytes[27],
            extended?ReadVector(bytes[28..]):null,extended?BinaryPrimitives.ReadUInt32LittleEndian(bytes[40..]):0),null);
    }
    /// <summary>Only these two guards for an already selected actor; this is neither lookup/delivery nor full MoveNpc success.</summary>
    public static RetailMainMoveGuardResult EvaluateKnownActorGuards(RetailNpcInterserverMoveWords move,uint currentCounter19f8)
    {
        ArgumentNullException.ThrowIfNull(move);
        if(move.Heading>=120)return RetailMainMoveGuardResult.HeadingRejected; // MOVSX then unsigned JA119.
        return move.Sequence==unchecked((byte)currentCounter19f8)?RetailMainMoveGuardResult.HeadingAndSequencePassed:RetailMainMoveGuardResult.SequenceRejected;
    }
    public static RetailMainGoalResolution ResolveContinuingGoal(RetailNpcInterserverMoveWords move,RetailMainVectorWords priorGoal)
    {
        ArgumentNullException.ThrowIfNull(move);
        if((move.Flags&0xc0)!=0xc0)return new(true,priorGoal,0,null);
        return move.ExtendedGoal is null?new(false,null,null,"Extended goal is missing; no previous-goal fallback."):new(true,move.ExtendedGoal,move.Duration,null);
    }
    private static void WriteVector(Span<byte> bytes,RetailMainVectorWords v)
    {BinaryPrimitives.WriteUInt32LittleEndian(bytes,v.X);BinaryPrimitives.WriteUInt32LittleEndian(bytes[4..],v.Y);BinaryPrimitives.WriteUInt32LittleEndian(bytes[8..],v.Z);}
    private static RetailMainVectorWords ReadVector(ReadOnlySpan<byte> bytes)=>new(BinaryPrimitives.ReadUInt32LittleEndian(bytes),BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]),BinaryPrimitives.ReadUInt32LittleEndian(bytes[8..]));
}
