using System.Buffers.Binary;
using System.Collections.Immutable;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using C = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcPhysicalInstantCalculation;
using F = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcPhysicalSkillRecordCompletion;
using N = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcImmediateDamageOwner;
using P = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcPhysicalInstantParameters;

namespace Aion.GameServer.Tests;

/// <summary>Chosen full owned-input fixtures, instruction-derived expected values; no native actor/clock oracle.</summary>
public sealed class RetailNpcPhysicalSkillRecordCompletionTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void FactoriesRejectPublicObjectsAndPreparedDataWithoutPrivateReplay(int mode)
    {
        F.Input input=Make();object foreign=mode switch {0=>new object(),1=>input,
            _=>new F.ContinuationPreparation(input,[],Complete(input).Known!.Storage!,input.Allocation.Scheduler,1,2,3,4)};
        Assert.Throws<ArgumentException>(()=>F.OwnerCall.CreateFromReplay(foreign));
        Assert.Throws<ArgumentException>(()=>F.Continuation.CreateFromReplay(foreign));
    }
    [Fact]
    public void FactoriesDoNotAcceptAnAlreadyProducedContinuationAsThePrivateCarrier()
    {
        var result=Complete(Make(retain:true),F.Completion.CompleteInitialEnqueueHpPending);
        object existing=result.Retained!;
        Assert.Throws<ArgumentException>(()=>F.OwnerCall.CreateFromReplay(existing));
        Assert.Throws<ArgumentException>(()=>F.Continuation.CreateFromReplay(existing));
        Assert.DoesNotContain(typeof(F.Continuation).GetConstructors(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance),x=>!x.IsPrivate);
    }
    [Fact]
    public void FactoryNullIsRejectedWithoutProducingAProof()
    {
        Assert.Throws<ArgumentNullException>(()=>F.OwnerCall.CreateFromReplay(null!));
        Assert.Throws<ArgumentNullException>(()=>F.Continuation.CreateFromReplay(null!));
    }
    private static readonly C.Evidence E = new(P.MainSha256,P.PackageContext,"chosen-physical","serialized-numeric",
        C.Origin.MathematicalFixture,"independent-source-arithmetic;not-native-execution");
    private static C.Binding Bind(ulong address,string label)=>new(address,label,E);
    private static readonly Lazy<JsonDocument> Headings=new(()=>Decode(FrozenHeadingEvidence,FrozenHeadingSha256));
    private static JsonDocument Decode(string text,string expected)
    {
        using var stream=new MemoryStream(Convert.FromBase64String(text));
        using var gzip=new GZipStream(stream,CompressionMode.Decompress);using var output=new MemoryStream();
        gzip.CopyTo(output);byte[] raw=output.ToArray();
        Assert.Equal(expected,Convert.ToHexStringLower(SHA256.HashData(raw)));
        return JsonDocument.Parse(raw);
    }
    private static ImmutableArray<uint> Heading(int index)=>Headings.Value.RootElement[index].EnumerateArray()
        .Select(x=>Convert.ToUInt32(x.GetString(),16)).ToImmutableArray();
    private static C.OriginalCall Draw(C.Stage stage,int returned,int first=1,int second=1000)=>new(C.CallKind.Draw,stage,stage switch
    {
        C.Stage.Dodge=>0x14036d690,C.Stage.BeginChance=>0x140bbd1fe,C.Stage.Base=>0x14036ddc9,
        C.Stage.BaseRandom=>0x14036de03,C.Stage.Additional=>0x14036de81,C.Stage.Block=>0x14036efd2,
        C.Stage.Parry=>0x14036f12d,C.Stage.Critical=>0x14036e167,_=>throw new ArgumentOutOfRangeException(nameof(stage))
    },first,second,checked((uint)returned),first!=second,E);
    private static C.OriginalCall Clock(C.Stage stage,uint value)=>new(C.CallKind.CounterClock,stage,0x1400b4650,0,0,value,false,E);
    private static C.RawActor Raw()=>new()
    {
        Type144=2,Vtable=C.NpcVtable,Kind1940=1,Master188c=0,Related1894=0,VirtualProfile1850=0,Difficulty1bf8=0,Pose13c=0,Abnormal1250=0,
        Current914=0,TemplateFlags434=0,TemplateFlags3f0=0,TemplateExclusive450=0,TemplateFixedPhysical3f4=0,
        Level1492=35,Ratio1514=1000,Minimum14dc=181,Maximum14e0=272,AdditiveRate8dcBits=0,Flat14d4=0,
        Accuracy14aa=0,Evasion14ae=0,Parry14b4=0,Block14b6=0,Defense1494=0,Critical14bc=500,
        CriticalReduce14c6=0,CriticalDamageReduce14ca=0,OneTime200=0,OneTime208=0,Movement189=0,
        StateIndex13d0=0,Heading12c=0,X120Bits=0xbf800000,Y124Bits=0
    };
    internal static C.Input Numeric(int skill=16556,int npc=212776,bool target=true)
    {
        var selected=RetailNpcPhysicalInstantParametersTests.Select(skill,npc);
        ushort lower=npc==212776?(ushort)181:(ushort)189,upper=npc==212776?(ushort)272:(ushort)284;
        var a=new C.Actor(Bind(0x100000,"NPC-source"),Bind(0x300000,"NPC-source-template"),npc,"source-life",
            Raw() with {Template1848=0x300000,Minimum14dc=lower,Maximum14e0=upper},new(Bind(0x100158,"null-manager-pointer-cell"),0,null,null,null));
        var d=new C.Actor(Bind(0x200000,"NPC-defender"),Bind(0x400000,"NPC-defender-template"),212833,"defender-life",
            Raw() with {Template1848=0x400000,X120Bits=0},new(Bind(0x200158,"null-manager-pointer-cell"),0,null,null,null));
        var current=new C.CurrentSkill(Bind(0x500000,"selected-Skill"),selected.Constructed.ToImmutableDictionary(x=>x.Key,x=>(long?)x.Value),
            Bind(0x600070,"current-caller-stack-descriptor-copy"),selected.Condition480,C.NoneAttenuation,0,0,1,true,
            new(C.ConditionCopyKind.SkillPhyAttackEffect0StackCopy,Bind(0x600000,"current-SkillPhyAttack-RBP"),0x500000,0x500480,0x600070,
                0x140bb83eb,0x140bb8429,0x140bb84d1,0x140bb84d5,0x140bb84fa));
        var tables=new C.LoadedTables(new(Bind(0x141069ed0,"actual-attacker-row0"),0,[0,0,0,0,0,0,0]),
            new(Bind(0x141069ed0,"actual-defender-row0"),0,[0,0,0,0,0,0,0]),
            new(Bind(0x1428b7410+240,"loaded-type3"),3,Enumerable.Range(0,20).Select(i=>i<7?90:i<13?100:110).ToImmutableArray()),
            Bind(0x14111ca60,"pinned-heading-A"),Bind(0x14111cc40,"pinned-heading-B"),Heading(0),Heading(1),
            Bind(0x141122d00,"pinned-parry-table"),Enumerable.Range(0,32).Select(i=>i==1?1:0).ToImmutableArray(),
            Bind(0x145a2c630,"declared-current-BSS-row-not-file-zero"),0);
        C.Span Span(C.SpanRole role,ulong address,int bytes)=>new(role,address,bytes,role.ToString(),E);
        var storage=new C.RawStorage(Bind(0x600000,"aggregate-header"),Bind(0x800000,"original-cache"),Bind(0x6000a0,"physical-record"),
            ImmutableArray.CreateRange(new byte[4]),ImmutableArray.CreateRange(new byte[16]),ImmutableArray.CreateRange(new byte[56]),
            [Span(C.SpanRole.Header,0x600000,4),Span(C.SpanRole.Cache,0x800000,16),Span(C.SpanRole.Record,0x6000a0,56),
             Span(C.SpanRole.Condition,0x600070,48),Span(C.SpanRole.Attacker,0x100000,0x2000),Span(C.SpanRole.Defender,0x200000,0x2000),
             Span(C.SpanRole.AttackerTemplate,0x300000,0x500),Span(C.SpanRole.DefenderTemplate,0x400000,0x500),Span(C.SpanRole.Skill,0x500000,0x1000)],true,true);
        // Back at attackerX=-1 has positive dot and adds21. Front at the same XY adds nothing.
        var calls=new List<C.OriginalCall>();
        if(target){calls.Add(Draw(C.Stage.Dodge,1000));calls.Add(Draw(C.Stage.BeginChance,100,1,100));}
        calls.Add(Draw(C.Stage.Base,lower,lower,upper));calls.Add(Draw(C.Stage.BaseRandom,0,0,99));
        if(skill==16556)calls.Add(Draw(C.Stage.Additional,100,1,100));
        calls.Add(Draw(C.Stage.Parry,1000));calls.Add(Draw(C.Stage.Critical,500));
        return new(target?C.Entry.TargetCheckThenBeginAndCalculate:C.Entry.CalculatorCall,E,selected,a,d,current,tables,
            new(Bind(0x154827468,"current-race-mask-not-source-default"),Bind(0x14f523f78,"current-race-limit"),1,null),
            storage,new(true,true,true),[11,22,33,44],calls.ToImmutableArray(),true,true);
    }

    private static readonly N.Evidence NE=new(N.FactKind.SourceBoundDeclaration,"chosen-source-fixture;not-runtime");
    private const uint SourceId=0x80001002,TargetId=0x80001001;
    private static byte[] Opaque(int length,int seed)=>Enumerable.Range(0,length).Select(x=>unchecked((byte)(x*17+seed))).ToArray();
    private static N.Tick OwnerTick(N.TickRole role,uint value,string epoch="serialized-numeric")=>new(role,role switch
    {N.TickRole.AttributionOuter=>0x1402a740c,N.TickRole.AttributionDecay=>0x1402aa365,
     N.TickRole.TimerEntry=>0x140d01b02,N.TickRole.TimerDue=>0x140d01c29,N.TickRole.TimerExit=>0x140d01da2,
     _=>throw new ArgumentOutOfRangeException(nameof(role))},value,"chosen-physical",epoch,NE);
    private static ImmutableArray<N.Tick> OwnerTicks(int hp,int incoming,string epoch="serialized-numeric")
    {
        var list=new List<N.Tick>();
        if(incoming>0)list.Add(OwnerTick(N.TickRole.AttributionOuter,1_000_000,epoch));
        if(incoming>=hp && incoming>0){list.Add(OwnerTick(N.TickRole.TimerEntry,1_000_010,epoch));list.Add(OwnerTick(N.TickRole.TimerDue,1_000_011,epoch));list.Add(OwnerTick(N.TickRole.TimerExit,1_000_012,epoch));}
        return [..list];
    }
    private static F.OriginalCall OwnDraw(F.CallStage stage,uint value)=>new(stage,stage switch
    {F.CallStage.ShieldDraw=>0x14034cbdf,F.CallStage.ReflectorDraw=>0x14034cedc,F.CallStage.SpinDraw=>0x140bbbfda,
     _=>throw new ArgumentOutOfRangeException(nameof(stage))},value,1,1000,E);
    private static F.OriginalCall OwnTick(F.CallStage stage,uint value)=>new(stage,stage switch
    {F.CallStage.ConstructionTick=>0x14002aa0a,F.CallStage.TimerEntry=>0x140d01b02,F.CallStage.TimerDue=>0x140d01c29,
     F.CallStage.TimerExit=>0x140d01da2,_=>throw new ArgumentOutOfRangeException(nameof(stage))},value,0,0,E);
    internal static F.Input Make(int skill=16556,int npc=212776,int targetHp=1000,int sourceHp=1000,bool shield=false,bool reflect=false,bool retain=false)
    {
        C.Input calc=Numeric(skill,npc);int baseDamage=skill==16556?(npc==212776?346:361):(npc==212776?197:205);
        uint flags=(shield?0x40u:0)|(reflect?0x10u:0);ulong manager=flags==0?0ul:0x900000;
        if(manager!=0)calc=calc with
        {
            Defender=calc.Defender with {Manager=calc.Defender.Manager with
                {Binding=Bind(manager,"actual-defender-protection-manager"),CurrentPointer158=manager,Force43e0=0,Force43f0=0,Force43e8=0}},
            Storage=calc.Storage with {Spans=calc.Storage.Spans.Add(new(C.SpanRole.Manager,manager,0x5000,"actual-defender-protection-manager",E))}
        };
        N.Actor Actor(C.Actor raw,uint id,int hp,string generation)=>new(
            new(id,raw.NpcId,2,N.NpcVtable,raw.LifeGeneration,generation,E.Operation,E.Epoch,NE),
            new(1000,hp,0,0,0,1,9,0,0,null,0,0,0),new(0,0,1,0,0,0,0,0,0,55),
            new(3,0,0,17),new(123,29,2,35,new(raw.Raw.X120Bits!.Value,raw.Raw.Y124Bits!.Value,0)));
        F.RawActor RawOwner(C.Actor raw,uint id,int hp,string generation)=>new(Actor(raw,id,hp,generation),raw.Current.Address,raw.Current.Identity,
            0x910000,0,0,0,0,null,null,0,0,0,0,0,0,0,0x3f800000);
        F.RawActor a=RawOwner(calc.Attacker,SourceId,sourceHp,"manager-a"),d=RawOwner(calc.Defender,TargetId,targetHp,"manager-d");
        N.Identity identity=new(P.MainSha256,N.Dataset,N.Region,E.Operation,E.Epoch,NE);
        F.OwnerEnvironment Environment(F.RawActor receiver,bool primary,int incoming)
        {
            N.BoundSkill bound=new(0,7,NE);
            var effect=new N.Manager(primary&&manager!=0?N.ManagerPresence.Present:N.ManagerPresence.VerifiedNull,
                primary?manager:0,primary?flags:0,receiver.Owner.Binding.ManagerGeneration,[],NE);
            var guard=new F.CoreGuards(0,0,0,0,effect,bound,bound,bound,NE);
            return new(identity,new(0x141246950,320050000,320050000,0,"world-current",E.Operation,E.Epoch,NE),guard,
                new(1_000_000,0,[],NE),new(100,NE),new(0,false,0,1,uint.MaxValue,0,0,0,[new(0,uint.MaxValue,[])],NE),
                new(Enumerable.Range(0,8).Select(x=>new N.NpcSocket(x,N.SocketPresence.Present,0,3,0,N.OpenSendOutcome.NormalQueuedOrSent,NE)).ToArray(),NE),
                new(true,true,true,NE),new(true,true,true,true,true,true,true,NE),OwnerTicks(receiver.Owner.State.Hp778,incoming),receiver.World118);
        }
        int reflected=reflect?baseDamage*25/100+60:0;
        int protectedDamage=shield?baseDamage-baseDamage*73/100:baseDamage;
        var protection=new F.ManagerProgram(manager==0?N.ManagerPresence.VerifiedNull:N.ManagerPresence.Present,manager,"manager-d",
            flags,1000,73,1,10000,16699,25,60,0,0x42800000,1000,E);
        var storage=new F.Storage(Bind(0x600000,"SkillPhyAttack-RBP"),Bind(0x600000,"owned-header40"),Bind(0x6000a0,"owned-records896"),
            Bind(0x600420,"owned-info948"),Bind(0x6007e0,"owned-copy948"),Bind(0xa00000,"bb5370-RBP"),
            [..Opaque(40,11)],[..Opaque(896,21)],[..Opaque(948,31)],[..Opaque(948,41)],0,1,[..Opaque(948,51)],-1,int.MaxValue,[],true);
        var scheduler=new F.DelayedScheduler(0,false,0,1,uint.MaxValue,0,0,0,[new(0,uint.MaxValue,[])],E);
        var allocation=new F.AllocationInput(retain?F.Allocation.OriginalFreshDisjoint:F.Allocation.OriginalNull,Bind(0xb00000,"fresh-delayed110"),
            [..Opaque(0x110,61)],97,101,1,1,1,0,scheduler);
        var calls=new List<F.OriginalCall>();if(shield)calls.Add(OwnDraw(F.CallStage.ShieldDraw,1000));if(reflect)calls.Add(OwnDraw(F.CallStage.ReflectorDraw,1000));
        if(retain){calls.Add(OwnTick(F.CallStage.ConstructionTick,10));calls.Add(OwnTick(F.CallStage.TimerEntry,20));calls.Add(OwnTick(F.CallStage.TimerDue,30));calls.Add(OwnTick(F.CallStage.TimerExit,40));}
        return new(calc,a,d,Environment(d,true,protectedDamage),Environment(a,false,reflected),protection,
            new(0,null,0x920000,0x1412d3aa8,E),storage,allocation,250,0,0,1,0x140b67a10,0,[..calls],true,true);
    }
    private static F.Result Complete(F.Input input,F.Completion completion=F.Completion.CompleteImmediateInvocation)
    {
        F.Result result=F.EvaluatePhysicalBegin(input);Assert.True(result.Status==completion,result.Unsupported ?? $"Expected {completion}, got {result.Status}");Assert.Null(result.Unsupported);
        Assert.NotNull(result.Known);Assert.False(result.ActivationReady);Assert.False(result.NativeOracle);Assert.Null(result.FinalHpAfterDeferredLife);
        Assert.Equal(Enumerable.Range(1,result.Known.Events.Length),result.Known.Events.Select(e=>e.Sequence));return result;
    }
    private static F.Result Incomplete(F.Input input,string contains)
    {var r=F.EvaluatePhysicalBegin(input);Assert.Equal(F.Completion.IncompleteReachedChild,r.Status);Assert.Contains(contains,r.Unsupported);Assert.NotNull(r.Known);Assert.Null(r.Primary);Assert.Null(r.Reflection);Assert.Null(r.FinalHpAfterDeferredLife);return r;}
    private static int I(ImmutableArray<byte> bytes,int at)=>BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(at,4));
    private static uint U(ImmutableArray<byte> bytes,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at,4));
    private static ulong Q(ImmutableArray<byte> bytes,int at)=>BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(at,8));
    private static ImmutableArray<byte> Put(ImmutableArray<byte> bytes,int at,int value)
    {byte[] copy=bytes.ToArray();BinaryPrimitives.WriteInt32LittleEndian(copy.AsSpan(at,4),value);return [..copy];}

    [Theory]
    [InlineData(16556,212776,346)] [InlineData(16556,212781,361)]
    [InlineData(16794,212776,197)] [InlineData(16794,212781,205)]
    public void FullOwnedSourceCalculationThroughNonzeroSkillOwner(int skill,int npc,int expected)
    {
        F.Input input=Make(skill,npc);var r=Complete(input);var k=r.Known!;
        Assert.Equal(expected,k.Calculation!.FinalRecordDamage);Assert.Equal(expected,I(k.Storage!.Records896,0));
        Assert.Equal(1000-expected,k.Defender.Owner.State.Hp778);Assert.Equal(expected,r.Primary!.ActualHpLoss);
        Assert.Equal(expected,r.Primary.ApplicationCell);Assert.Equal(0u,Assert.Single(r.Primary.Events.OfType<N.WorldHp>()).Skill);
        Assert.Equal(expected*10+2,I(r.Packet!.Bytes,15));Assert.Equal(33,r.Packet.Bytes.Length);
        Assert.Equal(SourceId,U(r.Packet.Bytes,7));Assert.Equal(TargetId,U(r.Packet.Bytes,11));
        Assert.Equal(expected,I(r.Packet.Bytes,23));Assert.Equal(skill,BinaryPrimitives.ReadUInt16LittleEndian(r.Packet.Bytes.AsSpan(31,2)));
        Assert.Equal(2,r.Packet.Transport.Slot);Assert.Equal(0,r.Primary.Calculated!.Receiver.Pending728);
        Assert.Null(r.Reflection);Assert.Null(r.Retained);Assert.Empty(k.ConsumedCalls);
    }
    [Theory]
    [InlineData(16556,346,94,146)] [InlineData(16794,197,54,109)]
    public void ShieldReflectionHoPAndActualLossRemainSeparate(int skill,int before,int after,int reflected)
    {
        var r=Complete(Make(skill,targetHp:1000,sourceHp:100,shield:true,reflect:true));var k=r.Known!;
        Assert.Equal(after,I(k.Storage!.Records896,0));Assert.Equal(reflected,I(k.Storage.Records896,0x14));
        Assert.Equal(16699,I(k.Storage.Records896,0x18));Assert.Equal(3,k.Storage.Records896[0x32]);
        Assert.Equal(1000-after,k.Defender.Owner.State.Hp778);Assert.Equal(0,k.Attacker.Owner.State.Hp778);
        Assert.Equal(before*10+2,I(r.Packet!.Bytes,15));Assert.Equal(after,I(r.Packet.Bytes,23));
        Assert.Equal(100,r.Reflection!.ActualHpLoss);Assert.Equal(100,r.Reflection.ApplicationCell);
        Assert.Equal(1,k.Attacker.Owner.State.Pending728);Assert.Equal(0,k.Storage.Info948[0x38a]);
        Assert.Equal(10000-(before-after),k.BeginProtectionManager!.Budget2cac);Assert.Equal(2,k.ConsumedCalls.Length);
        Assert.DoesNotContain(k.Events,x=>x.Stage=="ExplicitSourceHpSync");
        Assert.True(Array.FindIndex(k.Events.ToArray(),x=>x.Stage=="SourceAttackSyncAfterPrimaryCellRewrite")<Array.FindIndex(k.Events.ToArray(),x=>x.Stage=="ReflectedActualHpThenExplicitSendHp"));
    }
    [Fact]
    public void Preview728FeedsRealSkillOwnerWithoutResetAndCellRewriteLeavesRecord()
    {
        var r=Complete(Make(16794,targetHp:100));var k=r.Known!;
        Assert.Equal(197,I(k.Storage!.Records896,0));Assert.Equal(0,k.Storage.Info948[0x389]);
        Assert.Equal(100,r.Primary!.ApplicationCell);Assert.Equal(100,r.Primary.ActualHpLoss);
        Assert.Equal(0,k.Defender.Owner.State.Hp778);Assert.Equal(1,k.Defender.Owner.State.Pending728);
        Assert.Equal(100,I(r.Packet!.Bytes,23));Assert.Equal(1972,I(r.Packet.Bytes,15));
        Assert.Contains(k.Events,x=>x.Stage=="TargetPending728Cas");
        Assert.Equal(1,r.Primary.Calculated!.ReceiverReferences.PendingTimers10);
        Assert.Single(r.Primary.Events.OfType<N.TimerRequest>());
        Assert.Equal(16794u,Assert.Single(r.Primary.Events.OfType<N.Telemetry>()).Skill);
    }
    [Theory]
    [InlineData(0,1)] [InlineData(1,1)] [InlineData(0,0)]
    public void AllOpaqueRecordHeaderAndCopyBytesUseTheirOwnSources(int count,int capacity)
    {
        F.Input i=Make();i=i with {Storage=i.Storage with {ContainerCount=count,ContainerCapacity=capacity,ContainerBytes=[..Opaque(capacity*948,51)]}};
        var s=Complete(i).Known!.Storage!;
        for(int n=0;n<16;n++)for(int b=53;b<56;b++)Assert.Equal(i.Storage.RecordsBefore896[n*56+b],s.Records896[n*56+b]);
        foreach(int b in new[]{5,6,7,20,21,22,23})Assert.Equal(i.Storage.HeaderBefore40[b],s.Header40[b]);
        Assert.Equal(s.Records896.ToArray(),s.Info948.AsSpan(8,896).ToArray());
        Assert.Equal(i.Storage.InfoBefore948[0x38b],s.Info948[0x38b]);
        Assert.Equal(i.Storage.CopyBefore948[0x38b],s.Copy948[0x38b]);
        foreach(int b in new[]{5,6,7})Assert.Equal(i.Storage.CopyBefore948[0x38c+b],s.Copy948[0x38c+b]);
        foreach(int b in new[]{20,21,22,23})Assert.Equal(i.Storage.HeaderBefore40[b],s.Copy948[0x38c+b]);
        Assert.Equal(count<capacity?count+1:count,s.ContainerCount);
        if(count<capacity)Assert.Equal(s.Copy948.ToArray(),s.Container.AsSpan(count*948,948).ToArray());
        else Assert.Equal(i.Storage.ContainerBytes.ToArray(),s.Container.ToArray());
        Assert.Equal(346,s.Maximum);Assert.Equal(346,s.Minimum);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    public void RawGeometryDeterminesReflectorRangeAndDrawConsumption(int mode)
    {
        F.Input i=Make(reflect:true);var a=i.Attacker;var calc=i.Calculation;
        uint x=mode switch{0=>0u,1=>0x41000000u,2=>0x41000001u,3=>0x3f800000u,4=>0u,5=>0u,_=>0xbf800000u};
        uint z=mode==3?0x41000000u:mode==4?0x3f800000u:0;
        ulong world=mode==5?0ul:0x910000;
        a=a with {World118=world,Z128Bits=z,Owner=a.Owner with {Telemetry=a.Owner.Telemetry with {Position120=new(x,0,z)}}};
        calc=calc with {Attacker=calc.Attacker with {Raw=calc.Attacker.Raw with {X120Bits=x}}};
        // Pinned template bypass keeps the physical additional branch unchanged by these geometry fixtures.
        calc=calc with {Defender=calc.Defender with {Raw=calc.Defender.Raw with {TemplateFlags3f0=4}}};
        // World mismatch skips the otherwise unknown raw geometry field; HP-owner telemetry remains independently bound.
        if(mode==6)a=a with {World118=0,Z128Bits=null};
        bool reached=mode is 1 or 4 or 7;
        i=i with {Attacker=a,Calculation=calc,OriginalCalls=reached?[OwnDraw(F.CallStage.ReflectorDraw,1000)]:[],
            Reflection=i.Reflection with {OriginalTicks=reached?OwnerTicks(1000,146):[]}};
        var r=Complete(i);Assert.Equal(reached, r.Reflection is not null);
        Assert.Equal(reached?1:0,r.Known!.ConsumedCalls.Length);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void ReachedUnsupportedChildRetainsHonestPrefixAndNoFinalOwner(int mode)
    {
        F.Input i=Make(shield:true);string reason;
        if(mode==0){i=i with {Protection=i.Protection with {Budget2cac=1}};reason="exhausted";}
        else if(mode==1){i=i with {Attacker=i.Attacker with {ItemMask6c8=1}};reason="item-proc";}
        else if(mode==2){i=i with {Primary=i.Primary with {Guards=i.Primary.Guards with {Bound168=new(null,7,NE)}}};reason="bound168";}
        else if(mode==3){i=i with {Primary=i.Primary with {Guards=i.Primary.Guards with {Systems12cc=1}}};reason="system";}
        else if(mode==4){i=i with {Primary=i.Primary with {World=i.Primary.World with {Vtable=1}}};reason="world";}
        else{i=i with {OriginalCalls=[]};reason="original draw";}
        var r=Incomplete(i,reason);Assert.False(r.InvocationComplete);
        if(mode==0){Assert.Equal(0,r.Known!.BeginProtectionManager!.Budget2cac);Assert.Null(r.Known.Storage);}
        if(mode==1)Assert.Equal(906,r.Known!.Defender.Owner.State.Hp778);
        if(mode==4)Assert.Equal(906,r.Known!.IncompleteOwnerState!.Receiver.Hp778);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    public void StorageIdentityAndActorDomainFailuresRejectBeforeAnyReplay(int mode)
    {
        F.Input i=Make();
        i=mode switch{
            0=>i with {Storage=i.Storage with {Header=Bind(0x600001,"wrong")}},
            1=>i with {Storage=i.Storage with {CompleteSourceAndDestinationAliases=null}},
            2=>i with {Storage=i.Storage with {HeaderBefore40=default}},
            3=>i with {Storage=i.Storage with {CompletionFrameRbp=Bind(0x100100,"actor-overlap")}},
            4=>i with {Attacker=i.Attacker with {Owner=i.Attacker.Owner with {State=i.Attacker.Owner.State with {Master188c=1}}}},
            5=>i with {Defender=i.Defender with {Owner=i.Defender.Owner with {Binding=i.Defender.Owner.Binding with {LifeGeneration="wrong"}}}},
            6=>i with {CurrentSkill4d4=1},_=>i with {DiagnosticsDisabled=null}};
        var r=F.EvaluatePhysicalBegin(i);Assert.Equal(F.Completion.RejectedBeforeEvaluation,r.Status);Assert.Null(r.Known);Assert.Null(r.Primary);Assert.Null(r.FinalHpAfterDeferredLife);
    }
    [Fact]
    public void DodgeFailureDoesNotInitializeWrapperOrConsumeBeginProtectorOrAllocation()
    {
        F.Input i=Make();var c=i.Calculation;
        c=c with {Defender=c.Defender with {Raw=c.Defender.Raw with {Evasion14ae=9999}},OriginalCalls=[Draw(C.Stage.Dodge,1),Clock(C.Stage.DodgeClock,123)]};
        var r=Complete(i with {Calculation=c,OriginalCalls=[]},F.Completion.TargetCheckFailed);
        Assert.Null(r.Known!.Storage);Assert.Null(r.Retained);Assert.Null(r.Packet);
        Assert.DoesNotContain(r.Known.Events,x=>x.Stage=="WrapperHeaderAnd16RecordsInitialized");
        Assert.False(r.Known.Calculation!.BeginInvoked);Assert.Empty(r.Known.ConsumedCalls);
    }

    private static F.TimerInput Timer(F.Result enqueue,int currentTargetHp=1000,int phase=0)
    {
        F.Continuation held=enqueue.Retained!;var k=enqueue.Known!;const string epoch="fresh-timer-original";
        F.RawActor Current(F.RawActor actor,int hp)=>actor with {Owner=actor.Owner with
        {Binding=actor.Owner.Binding with {Epoch=epoch},State=actor.Owner.State with {Hp778=hp}}};
        F.OwnerEnvironment Fresh(F.OwnerEnvironment env,int hp,int incoming)=>env with
        {Identity=env.Identity with {Epoch=epoch},World=env.World with {Epoch=epoch},OriginalTicks=OwnerTicks(hp,incoming,epoch)};
        var original=Put(Put(Put(held.ObjectBytes,0x108,phase),8,1),0x10,0);
        return new(held,Current(k.Attacker,k.Attacker.Owner.State.Hp778),Current(k.Defender,currentTargetHp),
            Fresh(held.Origin.Primary,currentTargetHp,held.HpDamage),Fresh(held.Origin.Reflection,k.Attacker.Owner.State.Hp778,held.Reflected),
            new(0xc00000,"fresh-completion-RBP",E with {Epoch=epoch}),original,phase,1,0,E with {Epoch=epoch},true,true,true,[],true);
    }
    [Theory]
    [InlineData(16556,346)] [InlineData(16794,197)]
    public void CompleteRetainedPayloadAndFreshTimerOwnerAreDifferentInvocations(int skill,int expected)
    {
        F.Input input=Make(skill,retain:true);var r=Complete(input,F.Completion.CompleteInitialEnqueueHpPending);
        var held=r.Retained!;var k=r.Known!;
        Assert.Null(r.Primary);Assert.Null(r.Packet);Assert.Equal(1000,k.Defender.Owner.State.Hp778);
        Assert.Equal(4,k.Attacker.Owner.References.Count8);Assert.Equal(4,k.Defender.Owner.References.Count8);
        Assert.Equal(0x1412eddb8ul,Q(held.ObjectBytes,0));Assert.Equal(1,I(held.ObjectBytes,8));Assert.Equal(1,I(held.ObjectBytes,0x10));
        Assert.Equal(10u,U(held.ObjectBytes,0x68));Assert.Equal(230u,held.Due); // original due tick30 + max(100,250-50).
        Assert.Equal(input.Attacker.Address,Q(held.ObjectBytes,0x70));Assert.Equal(input.Defender.Address,Q(held.ObjectBytes,0x78));
        Assert.Equal(k.Storage!.Header40.ToArray(),held.ObjectBytes.AsSpan(0x88,40).ToArray());
        Assert.Equal(expected,I(held.ObjectBytes,0xb4));Assert.Equal(expected*10+2,I(held.ObjectBytes,0xbc));
        Assert.Equal(skill,I(held.ObjectBytes,0xc0));Assert.Equal(0,I(held.ObjectBytes,0x108));
        Assert.Equal(4,k.ConsumedCalls.Length);Assert.True(k.ConstructionReferenceReleased);
        Assert.Equal(held.ObjectBytes.ToArray(),k.RetainedObjectBytes.ToArray());
        var queued=Assert.Single(Assert.Single(held.Scheduler.Queues).Heap);Assert.Equal(0xb00000ul,queued.ObjectAddress);Assert.Equal(0,queued.TimerId);
        Assert.Equal(10u,queued.Generation);Assert.Equal(200ul,held.Scheduler.AccumulatedDelay);Assert.Equal(1ul,held.Scheduler.EnqueueCount);
        var timerInput=Timer(r,currentTargetHp:50);
        timerInput=timerInput with {Primary=timerInput.Primary with {Scheduler=timerInput.Primary.Scheduler with {AccumulatedDelay=777,EnqueueCount=13}}};
        var timer=F.EvaluateTimerExpired(timerInput);Assert.Equal(F.Completion.CompleteTimerHandler,timer.Status);Assert.Null(timer.Unsupported);
        Assert.Equal(50,timer.Primary!.ApplicationCell);Assert.Equal(50,timer.Primary.ActualHpLoss);Assert.Equal(0,timer.Known!.Defender.Owner.State.Hp778);
        Assert.Equal(expected,I(timer.Known.Storage!.Records896,0));Assert.Equal(50,I(timer.Packet!.Bytes,23));Assert.Equal(50,I(timer.Packet.Bytes,19));
        Assert.Empty(timer.Known.ConsumedCalls);Assert.Equal(0,timer.Known.Phase108After);Assert.Equal(0,I(timer.Known.RetainedObjectBytes,0x108));
        Assert.Null(timer.Known.CurrentDelayedScheduler); // No current delayed-service queue read by this phase0 handler.
        Assert.Equal(2777ul,timer.Primary.Calculated!.Scheduler.AccumulatedDelay);Assert.Equal(14ul,timer.Primary.Calculated.Scheduler.EnqueueCount);
        Assert.Equal(200ul,held.Scheduler.AccumulatedDelay);Assert.Equal(1ul,held.Scheduler.EnqueueCount); // Explicitly historical.
        Assert.Null(timer.FinalHpAfterDeferredLife);Assert.Null(timer.Retained);Assert.Equal(1,r.Retained!.PendingTimers);
    }
    [Fact]
    public void Complete110ImagePreservesEveryConstructorUnwrittenByte()
    {
        F.Input i=Make(retain:true);var r=Complete(i,F.Completion.CompleteInitialEnqueueHpPending);
        byte[] expected=i.Allocation.OriginalBytes110.ToArray();
        void D(int at,int value)=>BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(at,4),value);
        void Qw(int at,ulong value)=>BinaryPrimitives.WriteUInt64LittleEndian(expected.AsSpan(at,8),value);
        void W(int at,ushort value)=>BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(at,2),value);
        // Declarative independent complete constructor/write set from02a9f0,bb5870,d01ae0 and wrapper release.
        Qw(0,0x1412eddb8);D(8,1);Qw(0xc,0);D(0x14,0);Qw(0x18,0);D(0x20,0);Qw(0x28,0);
        D(0x30,0);Qw(0x38,0);D(0x40,0);Qw(0x48,0x140d02fc0);Qw(0x50,0);Qw(0x58,0);
        W(0x60,0);W(0x62,(ushort)((0xb00000u>>5)%97));W(0x64,(ushort)((0xb00000u>>5)%101));D(0x68,10);
        Qw(0x70,i.Attacker.Address);Qw(0x78,i.Defender.Address);Qw(0x80,0);r.Known!.Storage!.Header40.AsSpan().CopyTo(expected.AsSpan(0x88));
        D(0xb0,1);D(0xb4,346);D(0xb8,0);D(0xbc,3462);D(0xc0,16556);
        foreach(int at in new[]{0xc4,0xc8,0xcc,0xd0,0xd4,0xd8,0xdc,0xe0})D(at,0);
        Qw(0xe8,0);W(0xf0,0);D(0xf4,0);Qw(0xf8,0);Qw(0x100,0);D(0x108,0);D(0x10,1);
        Assert.Equal(expected,r.Retained!.ObjectBytes.ToArray());
        foreach(int at in new[]{0x24,0x25,0x26,0x27,0x34,0x35,0x36,0x37,0x44,0x45,0x46,0x47,0x66,0x67,0x6c,0x6d,0x6e,0x6f,0xe4,0xe5,0xe6,0xe7,0xf2,0xf3,0x10c,0x10d,0x10e,0x10f})
            Assert.Equal(i.Allocation.OriginalBytes110[at],r.Retained.ObjectBytes[at]);
    }
    [Theory]
    [InlineData(0,100,130)] [InlineData(149,100,130)] [InlineData(150,100,130)] [InlineData(151,101,131)] [InlineData(65535,65485,65515)]
    public void DelayUsesUnsigned16RunInfoAndIndependentSecondTick(int raw,int delay,uint due)
    {
        F.Input i=Make(retain:true);var r=Complete(i with {RunInfo78=checked((ushort)raw)},F.Completion.CompleteInitialEnqueueHpPending);
        Assert.Equal(due,r.Retained!.Due);Assert.Equal((ulong)delay,r.Retained.Scheduler.AccumulatedDelay);
        Assert.Equal(10u,U(r.Retained.ObjectBytes,0x68));
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void TimerHeapStrictSignedWrappedComparisonPreservesTiesAndRotation(int mode)
    {
        F.Input i=Make(retain:true);F.DelayedScheduler q=i.Allocation.Scheduler;
        F.TimerTuple old=new(mode==0?230u:mode==1?231u:mode==2?229u:0xfffffff0u,0xd00000,7,99,80);
        q=q with {Rotation=mode==3,TlsIndex=uint.MaxValue,QueueCount=mode==3?2u:1u,
            Queues=mode==3?[new(0,uint.MaxValue,[old]),new(1,uint.MaxValue,[])]:[new(0,uint.MaxValue,[old])]};
        var r=Complete(i with {Allocation=i.Allocation with {Scheduler=q}},F.Completion.CompleteInitialEnqueueHpPending);
        var selected=r.Retained!.Scheduler.Queues.Single(x=>x.Index==0);Assert.Equal(0u,r.Retained.Scheduler.TlsIndex);
        Assert.Equal(mode==1?0xb00000ul:0xd00000ul,selected.Heap[0].ObjectAddress);
        Assert.Equal(2,selected.Heap.Length);if(mode==0)Assert.Equal(old,selected.Heap[0]);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public void LateRetainedFailuresKeepOnlyActuallyCalculatedConstructorOrQueuePrefix(int mode)
    {
        F.Input i=Make(retain:true);var x=i.Allocation;string reason;
        if(mode==0){x=x with {Modulus142726bd0=0};reason="division";}
        else if(mode==1){x=x with {TypeNameInitialized15486eff0=0};reason="type-name";}
        else if(mode==2){x=x with {CallerInitialized15486eff8=0};reason="caller registration";}
        else if(mode==3){x=x with {Scheduler=x.Scheduler with {Closing=1}};reason="closed timer";}
        else if(mode==4){x=x with {Scheduler=x.Scheduler with {SleepMilliseconds=1}};reason="Sleep";}
        else if(mode==5){i=i with {OriginalCalls=i.OriginalCalls.RemoveAt(3)};reason="original tick";}
        else{x=x with {AllocationBinding=Bind(0x600100,"overlaps-wrapper")};reason="Overlapping";}
        var r=Incomplete(i with {Allocation=x},reason);Assert.NotNull(r.Known!.Storage);Assert.Null(r.Retained);Assert.Null(r.Packet);
        if(mode is 1 or 2 or 3 or 4 or 5){Assert.Equal(4,r.Known.Attacker.Owner.References.Count8);Assert.Equal(4,r.Known.Defender.Owner.References.Count8);}
        if(mode is 4 or 5)Assert.Equal(2,I(r.Known.RetainedObjectBytes,8));
        if(mode==5)Assert.Single(Assert.Single(r.Known.CurrentDelayedScheduler!.Queues).Heap);
        Assert.False(r.Known.ConstructionReferenceReleased==true);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void FreshTimerOwnershipAndPayloadAreRequiredNotEnqueueHpOrCallerChosenDamage(int mode)
    {
        var r=Complete(Make(retain:true),F.Completion.CompleteInitialEnqueueHpPending);var t=Timer(r);
        t=mode switch{
            0=>t with {OriginalObjectBytes=Put(t.OriginalObjectBytes,0xb4,1)},
            1=>t with {CurrentDefender=t.CurrentDefender with {Owner=t.CurrentDefender.Owner with {Binding=t.CurrentDefender.Owner.Binding with {LifeGeneration="other"}}}},
            2=>t with {DispatchOwnsActualRetainedObject=null},
            3=>t with {Primary=t.Primary with {Identity=t.Primary.Identity with {Epoch="stale"}}},
            4=>t with {OriginalObjectBytes=Put(t.OriginalObjectBytes,0x108,1)},_=>t with {References8=0}};
        var bad=F.EvaluateTimerExpired(t);Assert.Equal(F.Completion.RejectedBeforeEvaluation,bad.Status);Assert.Null(bad.Known);Assert.Null(bad.Primary);Assert.Null(bad.Packet);
        Assert.Null(bad.FinalHpAfterDeferredLife);
    }
    [Fact]
    public void NonzeroTimerPhaseDoesNotPretendHpAndPrepassRequeueHappenedTogether()
    {
        var r=Complete(Make(retain:true),F.Completion.CompleteInitialEnqueueHpPending);
        var t=Timer(r,phase:1);var outc=F.EvaluateTimerExpired(t);
        Assert.Equal(F.Completion.IncompleteReachedChild,outc.Status);Assert.Contains("prepass/requeue300",outc.Unsupported);
        Assert.Null(outc.Primary);Assert.Null(outc.Packet);Assert.Equal(1000,outc.Known!.Defender.Owner.State.Hp778);
        Assert.Equal(0,outc.Known.Phase108After);Assert.Equal(0,I(outc.Known.RetainedObjectBytes,0x108));
        Assert.Null(outc.Known.CurrentDelayedScheduler); // Requeue child was not executed; enqueue-era state is not current.
    }
    [Theory]
    [InlineData(0u,false)] [InlineData(0x100u,false)] [InlineData(1u,true)]
    public void PostCompletion1248ClearsBeforeReachedAbnormalBroadcast(uint old,bool broadcast)
    {
        var r=Complete(Make(shield:true,retain:true),F.Completion.CompleteInitialEnqueueHpPending);var t=Timer(r);
        t=t with {CurrentDefender=t.CurrentDefender with {Post1248=old}};
        var outcome=F.EvaluateTimerExpired(t);Assert.Equal(broadcast?F.Completion.IncompleteReachedChild:F.Completion.CompleteTimerHandler,outcome.Status);
        Assert.Equal(0u,outcome.Known!.Defender.Post1248);Assert.Equal(906,outcome.Known.Defender.Owner.State.Hp778);
        Assert.NotNull(outcome.Packet);Assert.Null(outcome.FinalHpAfterDeferredLife);
        if(broadcast){Assert.Null(outcome.Primary);Assert.Contains("broadcast",outcome.Unsupported);}
    }
    private static F.Input FrontSpin()
    {
        F.Input i=Make(16794);C.Input c=i.Calculation;
        c=c with {Attacker=c.Attacker with {Raw=c.Attacker.Raw with {X120Bits=0x3f800000}},OriginalCalls=c.OriginalCalls.Insert(4,Draw(C.Stage.Additional,100,1,100))};
        return i with {Calculation=c,Attacker=i.Attacker with {Owner=i.Attacker.Owner with {Telemetry=i.Attacker.Owner.Telemetry with {Position120=new(0x3f800000,0,0)}}}};
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    public void SpinEligibilityAndFailedComparisonAreCalculatedFromCurrentRawFields(int mode)
    {
        F.Input i=FrontSpin();bool draw=mode>=6;
        if(mode==0)i=i with {Defender=i.Defender with {Field1a3c=1,Pointer1a58=null}};
        if(mode==1)i=i with {Defender=i.Defender with {Pointer1a58=0x930000,First1a58=1,Field198At1a58=null}};
        if(mode==2)i=i with {Stagger=i.Stagger with {Global14276a0cc=1,Global142766278=4}};
        if(mode==3)i=i with {Calculation=i.Calculation with {Defender=i.Calculation.Defender with {Raw=i.Calculation.Defender.Raw with {TemplateFlags3f0=8}}}};
        if(mode==4)i=i with {Defender=i.Defender with {Template278=1u<<19}};
        if(mode==5)i=i with {Calculation=i.Calculation with {Defender=i.Calculation.Defender with {Raw=i.Calculation.Defender.Raw with {Abnormal1250=0x40}}},Primary=i.Primary with {Guards=i.Primary.Guards with {Abnormal1250=0x40}}};
        if(mode==6)i=i with {Defender=i.Defender with {Defense824=1000},OriginalCalls=[OwnDraw(F.CallStage.SpinDraw,1000)]};
        if(mode==7)i=i with {Defender=i.Defender with {Defense824=2000},Calculation=i.Calculation with {Defender=i.Calculation.Defender with {Raw=i.Calculation.Defender.Raw with {Pose13c=5}}},OriginalCalls=[OwnDraw(F.CallStage.SpinDraw,600)]};
        // Pose5 bypasses natural parry; preserve the actual original child call sequence.
        if(mode==7)i=i with {Calculation=i.Calculation with {OriginalCalls=i.Calculation.OriginalCalls.Where(x=>x.Stage is not(C.Stage.Parry or C.Stage.Dodge)).ToImmutableArray()}};
        var r=Complete(i);Assert.Equal(draw?1:0,r.Known!.ConsumedCalls.Length);Assert.Equal(0,r.Known.Defender.Owner.State.Field72c);
        Assert.Equal(0u,U(r.Known.Storage!.Header40,0));Assert.False(r.Known.Events.Any(x=>x.Stage=="SpinNewHeader10"));
        if(mode==6)Assert.Contains(r.Known.Events,x=>x.Stage=="SpinClass19Comparison" && x.Before==1000 && x.After==1000);
        if(mode==7)Assert.Contains(r.Known.Events,x=>x.Stage=="SpinClass19Comparison" && x.Before==600 && x.After==600);
    }
    [Fact]
    public void SuccessfulComparisonWithAlreadySet72cDoesNotSetNewHeaderOrOverwritePreview728()
    {
        F.Input i=FrontSpin();i=i with {Defender=i.Defender with {Owner=i.Defender.Owner with {State=i.Defender.Owner.State with {Field72c=7}}},OriginalCalls=[OwnDraw(F.CallStage.SpinDraw,1)]};
        var r=Complete(i);Assert.Equal(1,r.Known!.Defender.Owner.State.Field72c);Assert.Equal(0,r.Known.Defender.Owner.State.Pending728);
        Assert.Equal(0u,U(r.Known.Storage!.Header40,0));Assert.Equal(257,r.Primary!.ActualHpLoss);
        Assert.Contains(r.Known.Events,x=>x.Stage=="Spin72cExchangeDistinctFromPreview728" && x.Before==7 && x.After==1);
    }
    [Fact]
    public void NewlyActivatedSpinReachesRealUnclosedMovementOwnerWithKnownHeaderAnd72c()
    {
        F.Input i=FrontSpin();i=i with {OriginalCalls=[OwnDraw(F.CallStage.SpinDraw,1)]};
        var r=Incomplete(i,"movement/system-effect");Assert.Equal(1,r.Known!.Defender.Owner.State.Field72c);
        Assert.Equal(0,r.Known.Defender.Owner.State.Pending728);Assert.Equal(0x10u,U(r.Known.Storage!.Header40,0));
        Assert.Equal(1,I(r.Known.Storage.Header40,24));Assert.Equal(3,I(r.Known.Storage.Header40,28));
        Assert.Equal(0,I(r.Known.Storage.Header40,20));Assert.Equal(1000,r.Known.Defender.Owner.State.Hp778);
        Assert.Contains(r.Known.Events,x=>x.Stage=="PreActionActualSlot161");Assert.Null(r.Known.IncompleteOwnerState);
    }
    [Fact]
    public void NewlyActivatedSpinCannotPublishAnAdmittedContinuationEvenAfterKnownEnqueue()
    {
        F.Input i=FrontSpin(),held=Make(16794,retain:true);
        i=i with {Allocation=held.Allocation,OriginalCalls=[OwnDraw(F.CallStage.SpinDraw,1),..held.OriginalCalls]};
        var r=Incomplete(i,"future movement/system");Assert.Null(r.Retained);Assert.Null(r.Primary);Assert.Null(r.Packet);
        Assert.Equal(0x10u,U(r.Known!.Storage!.Header40,0));Assert.Equal(0,r.Known.Phase108After);
        Assert.Equal(4,r.Known.Attacker.Owner.References.Count8);Assert.Equal(4,r.Known.Defender.Owner.References.Count8);
        Assert.True(r.Known.ConstructionReferenceReleased);Assert.Single(Assert.Single(r.Known.CurrentDelayedScheduler!.Queues).Heap);
        Assert.Equal(1000,r.Known.Defender.Owner.State.Hp778);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void OrderedOriginalProtectionDrawsRequireActualStageRangeSiteAndEpoch(int mode)
    {
        F.Input i=Make(shield:true,reflect:true);var call=i.OriginalCalls[0];
        call=mode switch{0=>call with {Value=0},1=>call with {Value=1001},2=>call with {Stage=F.CallStage.ReflectorDraw},
            3=>call with {Site=0x14034ccb1},4=>call with {Minimum=0},_=>call with {Evidence=E with {Epoch="stale"}}};
        i=i with {OriginalCalls=i.OriginalCalls.SetItem(0,call)};var r=Incomplete(i,mode==5?"provenance":"draw");Assert.Empty(r.Known!.ConsumedCalls);
        Assert.Equal(10000,r.Known.BeginProtectionManager!.Budget2cac);Assert.Null(r.Known.Storage);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void SourceSyncUsesActualSourceSocketAndPostPrimaryGetterEvenBaseWire(int mode)
    {
        F.Input i=Make();var list=i.Primary.Transport.Sockets.ToArray();
        list[2]=list[2] with {Presence=mode==0?N.SocketPresence.VerifiedNull:N.SocketPresence.Present,
            State414=mode==1?2:0,OpenOutcome=mode==2?N.OpenSendOutcome.NormalSendError:N.OpenSendOutcome.NormalQueuedOrSent};
        i=i with {Primary=i.Primary with {Transport=new(list,NE)},Defender=i.Defender with {State19f8=unchecked((int)0xabcdef12)}};
        var r=Complete(i);Assert.Equal(33,r.Packet!.Bytes.Length);Assert.Equal(unchecked((int)0xabcdef12),r.Packet.Getter19f8);
        Assert.Equal(2,r.Packet.Transport.Slot);Assert.Equal(mode switch{0=>N.TransportDisposition.NoSocketReleased,1=>N.TransportDisposition.ClosedReleased,2=>N.TransportDisposition.SendError,_=>N.TransportDisposition.QueuedOrSent},r.Packet.Transport.Disposition);
    }
    [Theory]
    [InlineData(0u,33)] [InlineData(0x20u,33)] [InlineData(0x10u,38)] [InlineData(0x10000u,49)] [InlineData(1u,49)] [InlineData(0x10011u,49)]
    public void NativeFrameCodecIsRawPrimitiveEncodingNotAnHpOwnerAnswer(uint flags,int size)
    {
        ImmutableArray<byte> header=[..Opaque(40,71)];header=Put(header,0,unchecked((int)flags));
        var disposition=new N.TransportEffects(2,0,N.TransportDisposition.NoSocketReleased,true,0,0,null);
        var frame=F.EncodeNativeSync(SourceId,TargetId,-17,123,57,header,unchecked((int)0x89abcdef),0xff01,disposition);
        Assert.Equal(size,frame.Bytes.Length);Assert.Equal(size,BinaryPrimitives.ReadUInt16LittleEndian(frame.Bytes.AsSpan(0,2)));
        Assert.Equal(0x12,BinaryPrimitives.ReadUInt16LittleEndian(frame.Bytes.AsSpan(2,2)));Assert.Equal(0xad,frame.Bytes[4]);
        Assert.Equal(0xffed,BinaryPrimitives.ReadUInt16LittleEndian(frame.Bytes.AsSpan(5,2)));
        Assert.Equal(SourceId,U(frame.Bytes,7));Assert.Equal(TargetId,U(frame.Bytes,11));Assert.Equal(-17,I(frame.Bytes,15));
        Assert.Equal(123,I(frame.Bytes,19));Assert.Equal(57,I(frame.Bytes,23));Assert.Equal(flags,U(frame.Bytes,27));
        if(size==38){Assert.Equal(header[20],frame.Bytes[31]);Assert.Equal(unchecked((int)0x89abcdef),I(frame.Bytes,32));}
        if(size==49){Assert.Equal(header.AsSpan(8,12).ToArray(),frame.Bytes.AsSpan(31,12).ToArray());Assert.Equal(unchecked((int)0x89abcdef),I(frame.Bytes,43));}
        Assert.Equal(0xff01,BinaryPrimitives.ReadUInt16LittleEndian(frame.Bytes.AsSpan(size-2,2)));
    }
    [Theory]
    [InlineData(0)] [InlineData(39)] [InlineData(41)]
    public void NativePrimitiveCodecRejectsMissingOrMalformedHeader(int bytes)
    {
        var transport=new N.TransportEffects(0,0,N.TransportDisposition.NoSocketReleased,true,0,0,null);
        Assert.Throws<ArgumentException>(()=>F.EncodeNativeSync(0,0,0,0,0,bytes==0?default:[..new byte[bytes]],0,0,transport));
    }
    [Theory]
    [InlineData(0x00000000u,0)] [InlineData(0x80000000u,0)] [InlineData(0x3f000000u,1731)]
    [InlineData(0x3f800000u,3462)] [InlineData(0x40000000u,6924)] [InlineData(0xbf000000u,-1731)] [InlineData(0x3f800001u,3462)]
    public void ThreatScalingRetainsRawIntFloatRneOrderAndSignedZero(uint rate,int expected)
    {
        F.Input i=Make(shield:true);i=i with {Attacker=i.Attacker with {ThreatRateFf8Bits=rate}};
        var r=Complete(i);Assert.Equal(expected,I(r.Packet!.Bytes,15));Assert.Equal(94,r.Primary!.ActualHpLoss);
        var mul=Assert.Single(r.Known!.Events,x=>x.Stage=="ThreatBinary32Product");
        if(rate is 0 or 0x80000000)Assert.Equal((long)rate,mul.After);
        Assert.Contains(r.Known.Events,x=>x.Stage=="LatchedDamageHoPBeforeProtection" && x.Before==346 && x.After==3462);
    }
    [Theory]
    [InlineData(0x7f800000u)] [InlineData(0x7fc00000u)] [InlineData(1u)] [InlineData(0x4f800000u)]
    public void UnsupportedThreatFloatOrCvttRetainsReachedShieldPrefix(uint rate)
    {
        F.Input i=Make(shield:true);i=i with {Attacker=i.Attacker with {ThreatRateFf8Bits=rate}};
        var r=F.EvaluatePhysicalBegin(i);Assert.Equal(F.Completion.IncompleteReachedChild,r.Status);
        Assert.Null(r.Primary);Assert.Null(r.Known!.Storage);Assert.Equal(94,r.Known.CurrentDamageCell);
        Assert.Equal(9748,r.Known.BeginProtectionManager!.Budget2cac);Assert.Single(r.Known.ConsumedCalls);
        Assert.Null(r.FinalHpAfterDeferredLife);
    }
    [Theory]
    [InlineData(false,0x20u)] [InlineData(true,0x40u)]
    public void NaturalDefenseHeaderIsProjectedIntoActualOwnerContextRatherThanSuppliedAsStatus(bool parry,uint flag)
    {
        F.Input i=Make();C.Input c=i.Calculation;
        c=c with {Defender=c.Defender with {Raw=c.Defender.Raw with {Block14b6=parry?(ushort)0:(ushort)9999,Parry14b4=parry?(ushort)9999:(ushort)0}},
            OriginalCalls=c.OriginalCalls.Take(5).Concat(new[]{Draw(parry?C.Stage.Parry:C.Stage.Block,1),Clock(parry?C.Stage.ParryClock:C.Stage.BlockClock,50),Draw(C.Stage.Critical,500)}).ToImmutableArray()};
        var r=Complete(i with {Calculation=c});Assert.Equal(311,r.Primary!.ActualHpLoss);Assert.Equal(311,I(r.Known!.Storage!.Records896,0));
        Assert.Equal(flag,U(r.Known.Storage.Header40,0));Assert.Equal(flag,U(r.Packet!.Bytes,27));
        Assert.Equal(346,r.Known.Storage.Maximum);Assert.Equal(346,r.Known.Storage.Minimum);
        Assert.Contains(r.Primary.Events,x=>x is N.Trace t&&t.Kind==N.TraceKind.ActionGuard&&t.Before==flag);
        Assert.Equal(3112,I(r.Packet.Bytes,15));Assert.Single(r.Known.Calculation!.CounterWrites);
    }
    [Fact]
    public void CriticalRecordAndHeader4DoNotInventDifferentCompletionContextCritical21()
    {
        F.Input i=Make();var calls=i.Calculation.OriginalCalls.SetItem(i.Calculation.OriginalCalls.Length-1,Draw(C.Stage.Critical,1));
        var r=Complete(i with {Calculation=i.Calculation with {OriginalCalls=calls}});
        Assert.Equal(519,r.Primary!.ActualHpLoss);Assert.Equal(12,r.Known!.Storage!.Records896[0x31]);Assert.Equal(1,r.Known.Storage.Header40[4]);
        Assert.Equal(0,Assert.Single(r.Primary.Events.OfType<N.WorldHp>()).Critical);Assert.Equal(5192,I(r.Packet!.Bytes,15));
        Assert.Contains(r.Known.Events,x=>x.Stage=="NaturalStaggerOtherWeaponSkip");
    }

    private const string FrozenHeadingSha256="6d9aa50a9787382295611faf963a40aa578d59d4e62b9f2edf0f4648e34a76e2";
    private const string FrozenHeadingEvidence="H4sIAAAAAAAC/+1WO47DIBS8i+sUYOA9fJYohcfA/Y8QBbKa2X6rlV2NEHpvfkh+Prfw/bbHllqx0rNN2Czul39g30MK6Zqw5G59n7Dmmnqb8OjJ/ZwQ7ldIE7aQRisT9urR4gcObhsB1nxuG9H8iHnCPUbEviDytWPCVEIevmBP/VoTso0UzgVHBCadUQxpLws2pLbuWo5HOBY8Wo9rsfUx4jr15NW/0LMtbcNP814XvNoRx4L9qGOR9HFaWswqtcmp3pUJMle2kYMwU75UIdpEsfgg7ohn4qT4K65LFpIQc5M0NWNJXvogLZHusFHSM7ZPOgmegnfBCeBccBvIAWQG8gVVgNpAxaAPoDugZ6CToL+g62AWYEJgbmCaYMZg8mAfwJaA3QEbBfYMbB/YyV+nelcmyFzZRg7CTPlShWgTxeKDuCOeiZPir7guWUhCzE3S1IwleemDtES6w0ZJz37a93o87/d9v+/7ff/T9/3Y6v0f9Cf/Qa/XG3ERFCtVCgAA";
}
