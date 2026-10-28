using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Aion.GameServer.GeoEngine.RetailPath;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcMainMotionTests
{
    private const string Hash="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static RetailNpcMainMotionContext Context=>new(RetailNpcMainMotion.MainBinarySha256,RetailNpcMainMotion.GetterEvidenceSha256,
        RetailNpcMainMotion.NpcVtable,"life1","snapshot1","operation1",Hash,true,true,false,false,true,true);
    private static RetailNpcMainGetterTick Tick(uint value)=>new(value,0x140b1802e,"life1","snapshot1","operation1",Hash,true);
    private static readonly RetailMainVectorWords Zero=default;
    private static readonly RetailMainVectorWords OneX=new(0x3f800000,0,0);
    private static RetailNpcMainMotionState Ordinary=>new(Zero,Zero,Zero,OneX,0,1000,0x3f800000);
    private const string PrimaryCorpus="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";
    private const string AdditionalCorpus="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";

    private static JsonDocument ReadCorpus(string encoded,string expectedHash)
    {
        using var source=new MemoryStream(Convert.FromBase64String(encoded));
        using var gzip=new GZipStream(source,CompressionMode.Decompress);using var output=new MemoryStream();gzip.CopyTo(output);
        byte[] bytes=output.ToArray();
        if(bytes.Length>1024*1024||Convert.ToHexStringLower(SHA256.HashData(bytes))!=expectedHash)throw new InvalidDataException("Frozen independent corpus changed");
        return JsonDocument.Parse(bytes);
    }
    public static IEnumerable<object[]> LocationVectors()
    {
        using var primary=ReadCorpus(PrimaryCorpus,"f08f9be1cd340a20d80d3ea5587199bee0d20625b0c82ba376a09b3d25701a83");
        int index=0;foreach(var item in primary.RootElement.GetProperty("location_vectors").EnumerateArray())yield return [index++,item.GetRawText()];
        using var additional=ReadCorpus(AdditionalCorpus,"43b64249d178253b58e89cbeaeae0341d97e474255935f9b263e15def61b1949");
        foreach(var item in additional.RootElement.GetProperty("extra_fixture_designs").EnumerateArray())yield return [index++,item.GetRawText()];
        if(index!=73)throw new InvalidDataException("Expected68 original and5 independent additional vectors");
    }
    private static uint Word(string s)=>Convert.ToUInt32(s.StartsWith("0x",StringComparison.Ordinal)?s[2..]:s,16);
    private static RetailMainVectorWords Vector(JsonElement e)=>new(Word(e[0].GetString()!),Word(e[1].GetString()!),Word(e[2].GetString()!));
    [Theory]
    [MemberData(nameof(LocationVectors))]
    public void FrozenSourceModelsMatchAllRawCoordinatesFlagsBranchesAndArithmeticWords(int index,string json)
    {
        using var document=JsonDocument.Parse(json);var expected=document.RootElement;
        var input=expected.TryGetProperty("input",out var child)?child:expected;
        var state=new RetailNpcMainMotionState(Vector(input.GetProperty("endpoint_bits")),Vector(input.GetProperty("origin_bits")),
            Vector(input.GetProperty("velocity_bits")),Vector(input.GetProperty("goal_bits")),input.GetProperty("start_raw_u32").GetUInt32(),
            input.GetProperty("due_raw_u32").GetUInt32(),Word(input.GetProperty("speed_bits").GetString()!));
        var result=RetailNpcMainMotion.Calculate(state,Tick(input.GetProperty("tick_raw_u32").GetUInt32()),Context);
        Assert.True(result.Calculated,$"Vector{index}: {result.Detail}");Assert.Equal(Vector(expected.GetProperty("output_bits")),result.Location);
        Assert.Equal(expected.GetProperty("past_due_flag").GetUInt32(),result.PastDue);Assert.Equal(expected.GetProperty("branch").GetString(),result.Branch);
        var stages=expected.GetProperty("stages").EnumerateObject().ToArray();Assert.Equal(stages.Length,result.Stages.Count);
        foreach(var stage in stages)Assert.Equal(Word(stage.Value.GetString()!),result.Stages[stage.Name]);
        Assert.False(result.NativeTargetExecuted);Assert.False(result.HostFloatingStateMeasured);Assert.False(result.ActivationReady);Assert.False(result.WholeMainApplicationCovered);
    }
    [Fact]
    public void All4608CanonicalWireRecordsAnd13824GuardContextsMatchIndependentDigest()
    {
        using var digest=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);int accepted=0,rejected=0,packets=0;
        foreach(int flags in Enumerable.Range(0,256))foreach(byte heading in new byte[]{0,119,120,127,128,255})foreach(byte sequence in new byte[]{0,1,255})
        {
            bool extended=(flags&0xc0)==0xc0;
            var move=new RetailNpcInterserverMoveWords(0x234567,new(0x41f80000,0x3f800000,0x40000000),heading,(byte)flags,sequence,
                extended?new RetailMainVectorWords(0x42000000,0x3f800000,0x40000000):null,extended?1234u:0);
            var encoded=RetailNpcInterserverMoveCodec.EncodeCanonical(move);Assert.Equal(RetailCanonicalMoveStatus.CanonicalProducerLayout,encoded.Status);
            Assert.Equal(extended?44:28,encoded.Bytes.Length);var decoded=RetailNpcInterserverMoveCodec.DecodeCanonical(encoded.Bytes.AsSpan());
            Assert.Equal(move,decoded.Record);Assert.False(decoded.ClaimsNativeFramerRejection);
            foreach(uint current in new uint[]{sequence,(uint)sequence+256,unchecked((byte)(sequence+1))})
            {
                bool invoke=RetailNpcInterserverMoveCodec.EvaluateKnownActorGuards(decoded.Record!,current)==RetailMainMoveGuardResult.HeadingAndSequencePassed;
                if(invoke)accepted++;else rejected++;
                digest.AppendData(encoded.Bytes.AsSpan());byte[] context=new byte[5];BinaryPrimitives.WriteUInt32LittleEndian(context,current);context[4]=invoke?(byte)1:(byte)0;digest.AppendData(context);
            }
            packets++;
        }
        Assert.Equal(4608,packets);Assert.Equal(3072,accepted);Assert.Equal(10752,rejected);
        Assert.Equal("ac6f51f5f1b0bd2079bd5fef346af72b08c3360f1ebe3822a1c8649149c88888",Convert.ToHexStringLower(digest.GetHashAndReset()));
    }
    private static RetailNpcInterserverMoveWords Move=>new(0x234567,new(0x7fa12345,0x80000000,1),119,0xc0,255,new(0xffc12345,0,0x7f800000),uint.MaxValue);
    [Fact]
    public void RawPayloadsAndDecodedSnapshotsSurviveCallerBufferMutation()
    {
        var encoded=RetailNpcInterserverMoveCodec.EncodeCanonical(Move);byte[] bytes=encoded.Bytes.ToArray();
        var decoded=RetailNpcInterserverMoveCodec.DecodeCanonical(bytes);Array.Fill(bytes,(byte)0);
        Assert.Equal(Move,decoded.Record);Assert.Equal(44,encoded.Bytes.Length);Assert.NotEqual((byte)0,encoded.Bytes[4]);
    }
    [Theory]
    [InlineData(0)] [InlineData(2)] [InlineData(4)] [InlineData(5)] [InlineData(7)] [InlineData(26)] [InlineData(-1)] [InlineData(-2)]
    public void MalformedCanonicalShapesHaveNoClaimAboutNativeFramerRejection(int fault)
    {
        byte[] bytes=RetailNpcInterserverMoveCodec.EncodeCanonical(Move).Bytes.ToArray();
        if(fault==-1)bytes=bytes[..^1];else if(fault==-2)bytes=bytes.Append((byte)0).ToArray();else bytes[fault]^=0x40;
        var result=RetailNpcInterserverMoveCodec.DecodeCanonical(bytes);
        Assert.Equal(RetailCanonicalMoveStatus.OutsideCanonicalProducerLayout,result.Status);Assert.Null(result.Record);Assert.False(result.ClaimsNativeFramerRejection);
    }
    [Fact]
    public void NonextendedArgumentsAreUnconsumedAndMissingExtendedGoalNeverFallsBack()
    {
        var nonextended=Move with {Flags=0};var omitted=nonextended with {ExtendedGoal=null,Duration=0};
        Assert.Equal(RetailNpcInterserverMoveCodec.EncodeCanonical(omitted).Bytes.ToArray(),RetailNpcInterserverMoveCodec.EncodeCanonical(nonextended).Bytes.ToArray());
        var resolved=RetailNpcInterserverMoveCodec.ResolveContinuingGoal(nonextended,OneX);
        Assert.True(resolved.Available);Assert.Equal(OneX,resolved.Goal);Assert.Equal(0u,resolved.Duration);
        var missing=Move with {ExtendedGoal=null};
        Assert.Equal(RetailCanonicalMoveStatus.OutsideCanonicalProducerLayout,RetailNpcInterserverMoveCodec.EncodeCanonical(missing).Status);
        Assert.False(RetailNpcInterserverMoveCodec.ResolveContinuingGoal(missing,OneX).Available);
    }
    [Theory]
    [InlineData("main")] [InlineData("proof")] [InlineData("class")] [InlineData("owner")]
    [InlineData("tick-site")] [InlineData("tick-life")] [InlineData("tick-snapshot")] [InlineData("tick-operation")] [InlineData("tick-evidence")] [InlineData("tick-origin")]
    public void WrongSourceOrStaleOriginalTickCannotProduceAnAdmittedLocation(string fault)
    {
        var c=Context;var tick=Tick(1500);
        c=fault switch {"main"=>c with {MainBinarySha256=Hash},"proof"=>c with {GetterEvidenceSha256=Hash},"class"=>c with {NpcVtable=0},"owner"=>c with {ActorLifetime=""},_=>c};
        tick=fault switch {"tick-site"=>tick with {NativeCallSite=0},"tick-life"=>tick with {ActorLifetime="old"},"tick-snapshot"=>tick with {SnapshotRevision="old"},"tick-operation"=>tick with {OperationRevision="old"},"tick-evidence"=>tick with {EvidenceSha256=""},"tick-origin"=>tick with {ConstructedSyntheticInput=false},_=>tick};
        var result=RetailNpcMainMotion.Calculate(Ordinary,tick,c);Assert.False(result.Calculated);Assert.Null(result.Location);
    }
    [Theory]
    [InlineData("rounding")] [InlineData("exceptions")] [InlineData("ftz")] [InlineData("daz")] [InlineData("sse2")]
    public void UnsupportedDeclaredFloatingEnvironmentIsVisible(string fault)
    {
        var c=Context;c=fault switch {"rounding"=>c with {NearestEven=false},"exceptions"=>c with {ExceptionsMasked=false},"ftz"=>c with {FlushToZero=true},"daz"=>c with {DenormalsAreZero=true},_=>c with {Sse2=false}};
        Assert.Equal(RetailMainLocationStatus.UnsupportedFloatingEnvironment,RetailNpcMainMotion.Calculate(Ordinary,Tick(1500),c).Status);
    }
    [Theory]
    [InlineData(0x7fc00000u)] [InlineData(0x7f800000u)] [InlineData(0xff800000u)] [InlineData(1u)] [InlineData(0x00800000u)] [InlineData(0x1f800000u)] [InlineData(0x7f7fffffu)]
    public void ReachedExceptionalSubnormalUnderflowOrOverflowCannotInventCoordinates(uint word)
    {
        var state=Ordinary with {ContinuingGoal19fc=new(word,0,0)};
        var result=RetailNpcMainMotion.Calculate(state,Tick(1500),Context);
        Assert.Equal(RetailMainLocationStatus.UnsupportedConsumedValue,result.Status);Assert.Null(result.Location);
    }
    [Fact]
    public void DivisionUnderflowDoesNotBecomeAZeroMovementSpeed()
    {
        // Independent exact case: length2^63 is finite, but2^-126 /2^63 rounds to zero.
        var state=Ordinary with {ContinuingGoal19fc=new(0x5f000000,0,0),CurrentSpeed7c0=0x00800000};
        var result=RetailNpcMainMotion.Calculate(state,Tick(1500),Context);
        Assert.Equal(RetailMainLocationStatus.UnsupportedConsumedValue,result.Status);Assert.Null(result.Location);
        Assert.Equal(0x5f000000u,result.Stages["sqrt_bits"]);Assert.Contains("underflow to zero",result.Detail);
    }
    [Fact]
    public void UnconsumedWordsAndRawSignedZeroCopiesArePreserved()
    {
        var exceptional=new RetailMainVectorWords(0x80000000,0x7fa12345,1);
        var state=Ordinary with {Start185c=1000,PriorEndpoint1860=exceptional,CurrentSpeed7c0=0x7fc00000,ContinuingGoal19fc=exceptional};
        var before=RetailNpcMainMotion.Calculate(state,Tick(1000),Context);Assert.True(before.Calculated);Assert.Equal(exceptional,before.Location);Assert.Empty(before.Stages);
        state=state with {Start185c=0,Due1858=1000,Endpoint120=new(0x80000000,0,0),Velocity186c=Zero};
        var due=RetailNpcMainMotion.Calculate(state,Tick(1000),Context);Assert.True(due.Calculated);Assert.Equal(0x80000000u,due.Location!.Value.X);
        state=state with {ContinuingGoal19fc=state.Endpoint120,CurrentSpeed7c0=0x7fc00000};
        Assert.True(RetailNpcMainMotion.Calculate(state,Tick(1001),Context).Calculated); // Zero length never consumes speed.
    }
}
