using System.Buffers.Binary;
using System.Collections.Immutable;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using C = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcPhysicalInstantCalculation;
using P = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcPhysicalInstantParameters;

namespace Aion.GameServer.Tests;

/// <summary>Independent source arithmetic plus derived whole-call fixtures; no original runtime oracle.</summary>
public sealed class RetailNpcPhysicalInstantCalculationTests
{
    private static readonly C.Evidence E = new(P.MainSha256,P.PackageContext,"chosen-physical","serialized-numeric",
        C.Origin.MathematicalFixture,"independent-source-arithmetic;not-native-execution");
    private static C.Binding Bind(ulong address,string label)=>new(address,label,E);
    private static readonly Lazy<JsonDocument> Corpus=new(()=>Decode(FrozenNumericEvidence,FrozenNumericSha256));
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
    private static C.Input Make(int skill=16556,int npc=212776,bool target=true)
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
    private static C.Result Pass(C.Input input)
    {
        var outcome=C.Evaluate(input);Assert.True(outcome.Success,outcome.Diagnostic?.Message);var r=outcome.Value;
        Assert.Null(r.HpResult);Assert.Null(r.ApplicationCell);Assert.False(r.ActivationReady);Assert.False(r.NativeOracle);
        return r;
    }
    private static C.Outcome Fail(C.Input input,string code)
    {var result=C.Evaluate(input);Assert.False(result.Success);Assert.Null(result.Value);Assert.Equal(code,result.Diagnostic!.Code);return result;}
    private static C.Input Defender(C.Input i,Func<C.RawActor,C.RawActor> f)=>i with {Defender=i.Defender with {Raw=f(i.Defender.Raw)}};
    private static C.Input Attacker(C.Input i,Func<C.RawActor,C.RawActor> f)=>i with {Attacker=i.Attacker with {Raw=f(i.Attacker.Raw)}};
    private static C.Input Calls(C.Input i,params C.OriginalCall[] calls)=>i with {OriginalCalls=calls.ToImmutableArray()};

    public static IEnumerable<object[]> SourceVectors()=>Corpus.Value.RootElement.EnumerateArray().Select((x,index)=>new object[]{index,x.GetRawText()});
    [Theory]
    [MemberData(nameof(SourceVectors))]
    public void Frozen960IndependentFractionStagesWithDerivedWholeTargetCallOrder(int index,string json)
    {
        Assert.InRange(index,0,959);using var parsed=JsonDocument.Parse(json);var v=parsed.RootElement;
        int skill=v.GetProperty("skill_id").GetInt32(),npc=v.GetProperty("npc_id").GetInt32();
        var i=Make(skill,npc);var context=v.GetProperty("context");var relation=v.GetProperty("relation");
        uint FloatWord(JsonElement x)=>BitConverter.SingleToUInt32Bits(x.GetSingle());
        i=Attacker(i,a=>a with {X120Bits=FloatWord(relation.GetProperty("attacker_xy")[0]),Y124Bits=FloatWord(relation.GetProperty("attacker_xy")[1])});
        i=Defender(i,d=>d with {Defense1494=context.GetProperty("pdef").GetUInt32()});
        i=i with {Skill=i.Skill with {Sharing90=context.GetProperty("sharing").GetUInt16()}};
        var calls=new List<C.OriginalCall>{Draw(C.Stage.Dodge,1000),Draw(C.Stage.BeginChance,100,1,100)};
        foreach(var call in v.GetProperty("original_invocation_fixture_only").EnumerateArray())
        {
            string stage=call.GetProperty("stage").GetString()!;
            if(stage=="critical")calls.Add(Draw(C.Stage.Parry,1000)); // Derived real probability-zero stage, absent from old bypass corpus.
            calls.Add(Draw(stage switch {"base"=>C.Stage.Base,"base_random"=>C.Stage.BaseRandom,"additional"=>C.Stage.Additional,
                "critical"=>C.Stage.Critical,_=>throw new InvalidOperationException()},call.GetProperty("selected_original").GetInt32(),
                call.GetProperty("lo").GetInt32(),call.GetProperty("hi").GetInt32()));
        }
        i=i with {OriginalCalls=calls.ToImmutableArray()};var r=Pass(i);
        Assert.True(r.CheckReached);Assert.True(r.Hit);Assert.True(r.BeginInvoked);Assert.Equal(0,r.BeginFallback);
        Assert.Equal(v.GetProperty("preliminary_return").GetInt32(),r.PreliminaryReturn);
        Assert.Equal(v.GetProperty("final_record_damage").GetInt32(),r.FinalRecordDamage);
        foreach(var word in v.GetProperty("stages").EnumerateObject())Assert.Equal(Convert.ToUInt32(word.Value.GetString(),16),r.FloatBits[word.Name]);
        Assert.Equal(relation.GetProperty("computed_relation").GetBoolean(),r.ConsumedCalls.Any(c=>c.Stage==C.Stage.Additional));
        Assert.Equal(calls,r.ConsumedCalls);Assert.Empty(r.GlobalWrites);Assert.Empty(r.CounterWrites);
        Assert.Equal(v.GetProperty("critical_threshold").GetInt32(),Assert.Single(r.Trace,t=>t.Stage=="CriticalThreshold").Value);
        Assert.Equal((byte)(v.GetProperty("critical").GetBoolean()?12:0),r.RecordAfter[0x31]);
        Assert.Equal((byte)10,r.RecordAfter[0x30]);Assert.Equal(r.FinalRecordDamage,BinaryPrimitives.ReadInt32LittleEndian(r.RecordAfter.AsSpan(0,4)));
    }

    [Theory]
    [InlineData(-1,0ul,0)] [InlineData(0,1ul,1)] [InlineData(3,15ul,1)] [InlineData(63,ulong.MaxValue,1)]
    public void LazyMaskLoopsLocallyAndPublishesOnlyOnceAfterward(int limit,ulong expected,int writes)
    {
        // Back direction fails its early condition, so the mask/limit are actually consumed.
        var i=Attacker(Make(),a=>a with {X120Bits=0x3f800000});
        i=i with {Globals=i.Globals with {RaceMask154827468=0,RaceLimit14f523f78=limit},
            OriginalCalls=i.OriginalCalls.Where(c=>c.Stage!=C.Stage.Additional).ToImmutableArray()};
        var r=Pass(i);Assert.Equal(expected,r.RaceMaskAfter);Assert.Equal(writes,r.GlobalWrites.Length);
        if(writes!=0){var w=Assert.Single(r.GlobalWrites);Assert.Equal(0ul,w.Before);Assert.Equal(expected,w.After);Assert.Equal(0x140370e50ul,w.NativeSite);}
    }
    [Fact]
    public void AlreadyInitializedMaskSkipsUnknownLimitAndEarlyDirectionSkipsBothGlobals()
    {
        var i=Attacker(Make(),a=>a with {X120Bits=0x3f800000});
        i=i with {Globals=i.Globals with {RaceMask154827468=15,RaceLimit14f523f78=null},
            OriginalCalls=i.OriginalCalls.Where(c=>c.Stage!=C.Stage.Additional).ToImmutableArray()};
        Assert.Empty(Pass(i).GlobalWrites);
        var early=Make();early=early with {Globals=early.Globals with {RaceMask154827468=null,RaceLimit14f523f78=null}};
        Assert.Null(Pass(early).RaceMaskAfter);
        Fail(i with {Globals=i.Globals with {RaceMask154827468=0}},"unknown");
        Fail(i with {Globals=i.Globals with {RaceMask154827468=0,RaceLimit14f523f78=64}},"race-limit");
    }
    [Fact]
    public void HitFailureComputesCacheAndCounterThenSkipsAllBeginCalculationCalls()
    {
        var i=Defender(Make(),d=>d with {Evasion14ae=300});
        i=Calls(i,Draw(C.Stage.Dodge,300),Clock(C.Stage.DodgeClock,uint.MaxValue));
        var r=Pass(i);Assert.False(r.Hit);Assert.False(r.BeginInvoked);Assert.Null(r.BeginFallback);
        Assert.Null(r.FinalRecordDamage);Assert.Null(r.PreliminaryReturn);Assert.Empty(r.FloatBits);
        Assert.Equal(4999u,r.CounterDeadlinesAfter[2]);Assert.Equal(i.Storage.RecordBytes.ToArray(),r.RecordAfter.ToArray());
        Assert.Equal(new[]{C.Stage.Dodge,C.Stage.DodgeClock},r.ConsumedCalls.Select(c=>c.Stage));
        Assert.DoesNotContain(r.Trace,t=>t.Stage=="ConditionStackCopyBound");
    }
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void NaturalNpcBlockAndParryAreComputedAndCriticalStillFollows(bool block)
    {
        var i=Defender(Make(),d=>d with {Block14b6=block?(ushort)500:(ushort)0,Parry14b4=400});
        var calls=i.OriginalCalls.Where(c=>c.Stage is not(C.Stage.Parry or C.Stage.Critical)).ToList();
        calls.Add(Draw(block?C.Stage.Block:C.Stage.Parry,1));calls.Add(Clock(block?C.Stage.BlockClock:C.Stage.ParryClock,100));
        calls.Add(Draw(C.Stage.Critical,499));i=i with {OriginalCalls=calls.ToImmutableArray()};
        var r=Pass(i);Assert.Equal((byte)(block?4:2),r.RecordAfter[0x30]);Assert.Equal((byte)12,r.RecordAfter[0x31]);
        Assert.Equal(block?32u:64u,BinaryPrimitives.ReadUInt32LittleEndian(r.HeaderAfter.AsSpan()));
        Assert.Equal(5100u,r.CounterDeadlinesAfter[block?0:1]);Assert.Single(r.CounterWrites);
        Assert.Equal(346,r.PreliminaryReturn);Assert.Equal(467,r.FinalRecordDamage);
    }
    [Fact]
    public void FailedBlockContinuesParryAndEachProbabilityZeroStillConsumesItsDraw()
    {
        var i=Defender(Make(),d=>d with {Block14b6=1,Parry14b4=0});
        var calls=i.OriginalCalls.ToList();calls.Insert(calls.FindIndex(x=>x.Stage==C.Stage.Parry),Draw(C.Stage.Block,1000));
        var r=Pass(i with {OriginalCalls=calls.ToImmutableArray()});
        Assert.Equal((byte)10,r.RecordAfter[0x30]);Assert.Empty(r.CounterWrites);
        Assert.Equal(new[]{C.Stage.Block,C.Stage.Parry,C.Stage.Critical},r.ConsumedCalls.TakeLast(3).Select(c=>c.Stage));
    }
    [Theory]
    [InlineData(499,true)] [InlineData(500,false)] [InlineData(501,false)]
    public void OrdinaryCriticalUsesStrictEqualityBoundary(int draw,bool critical)
    {
        var i=Make();i=i with {OriginalCalls=i.OriginalCalls.Select(c=>c.Stage==C.Stage.Critical?Draw(c.Stage,draw):c).ToImmutableArray()};
        var r=Pass(i);Assert.Equal((byte)(critical?12:0),r.RecordAfter[0x31]);Assert.Equal(critical?519:346,r.FinalRecordDamage);
    }
    [Fact]
    public void OrdinaryTargetAndCalculatorEntryHaveDistinctOriginalCallContracts()
    {
        var target=Pass(Make());var direct=Pass(Make(target:false));
        Assert.False(direct.CheckReached);Assert.False(direct.BeginInvoked);Assert.Null(direct.BeginFallback);
        Assert.Equal(target.FinalRecordDamage,direct.FinalRecordDamage);
        Assert.Equal(target.ConsumedCalls.Skip(2),direct.ConsumedCalls);
        int chance=Assert.Single(target.Trace,t=>t.Stage=="OriginalDraw:BeginChance").Sequence;
        int copy=Assert.Single(target.Trace,t=>t.Stage=="ConditionStackCopyBound").Sequence;
        var defender=Assert.Single(target.Trace,t=>t.Stage=="DefenderPrepareNoOp");
        var attacker=Assert.Single(target.Trace,t=>t.Stage=="AttackerPrepareNoOp");
        int basis=Assert.Single(target.Trace,t=>t.Stage=="OriginalDraw:Base").Sequence;
        Assert.Equal(0x1400b47c0L,defender.Value);Assert.Equal(defender.Value,attacker.Value);
        Assert.True(chance<copy&&copy<defender.Sequence&&defender.Sequence<attacker.Sequence&&attacker.Sequence<basis);
        Assert.DoesNotContain(direct.Trace,t=>t.Stage.EndsWith("PrepareNoOp",StringComparison.Ordinal));
        Fail(Make(target:false) with {OriginalCalls=Make().OriginalCalls},"original-call");
    }

    [Theory]
    [InlineData("main","evidence")] [InlineData("epoch","evidence")] [InlineData("origin","evidence")]
    [InlineData("owned","actor")] [InlineData("scaled","actor")] [InlineData("player","actor")]
    [InlineData("template-unknown","actor")] [InlineData("template-stale","actor")]
    [InlineData("threshold","force")] [InlineData("blind","blind")] [InlineData("fixed","override")]
    [InlineData("dual","source-branch")] [InlineData("boost","source-branch")] [InlineData("diagnostic","infrastructure")]
    [InlineData("word","skill-word")] [InlineData("descriptor","condition")] [InlineData("attenuation","skill")]
    [InlineData("heading","heading")] [InlineData("randomrow","random-table")] [InlineData("PDef","unknown")]
    [InlineData("nonfinite","floating")] [InlineData("subnormal","floating")] [InlineData("conversion","conversion")]
    [InlineData("record58","storage")] [InlineData("initialization","storage")] [InlineData("overlap","alias")]
    [InlineData("extra","original-call")] [InlineData("missing","original-call")] [InlineData("wrongsite","original-call")]
    [InlineData("adapter","original-call")] [InlineData("rounding","floating")]
    public void UnknownUnsupportedAliasAndOriginalCallFailuresPublishNoPrefix(string invalid,string code)
    {
        var i=Make();
        i=invalid switch
        {
            "main"=>i with {Context=E with {MainSha256="wrong"}},
            "epoch"=>i with {Skill=i.Skill with {Current=i.Skill.Current with {Evidence=E with {Epoch="stale"}}}},
            "origin"=>i with {Context=E with {Origin=C.Origin.Unknown}},
            "owned"=>Attacker(i,a=>a with {Master188c=7}),"scaled"=>Attacker(i,a=>a with {Difficulty1bf8=123}),
            "player"=>Defender(i,d=>d with {Type144=1}),"threshold"=>i with {Skill=i.Skill with {RunInfo58=1}},
            "template-unknown"=>Attacker(i,a=>a with {Template1848=null}),
            "template-stale"=>Defender(i,d=>d with {Template1848=0x300000}),
            "blind"=>Attacker(i,a=>a with {Abnormal1250=0x20}),"fixed"=>Defender(i,d=>d with {TemplateFixedPhysical3f4=1}),
            "dual"=>Attacker(i,a=>a with {Current914=1}),"boost"=>Attacker(i,a=>a with {OneTime200=123}),
            "diagnostic"=>i with {DiagnosticsDisabled=false},
            "word"=>i with {Skill=i.Skill with {Words=i.Skill.Words.SetItem("438",1)}},
            "descriptor"=>i with {Skill=i.Skill with {Condition480=i.Skill.Condition480.SetItem(0x2c,2)}},
            "attenuation"=>i with {Skill=i.Skill with {AttenuationFunction=0}},
            "heading"=>i with {Tables=i.Tables with {HeadingABits=i.Tables.HeadingABits.SetItem(0,0x3f800000)}},
            "randomrow"=>i with {Tables=i.Tables with {RandomType3=i.Tables.RandomType3 with {Values=i.Tables.RandomType3.Values.SetItem(0,999)}}},
            "PDef"=>Defender(i,d=>d with {Defense1494=null}),
            "nonfinite"=>Attacker(i,a=>a with {AdditiveRate8dcBits=0x7fc00000}),
            "subnormal"=>Attacker(i,a=>a with {AdditiveRate8dcBits=1}),
            "conversion"=>Attacker(i,a=>a with {Flat14d4=int.MaxValue}),
            "record58"=>i with {Storage=i.Storage with {RecordBytes=new byte[88].ToImmutableArray()}},
            "initialization"=>i with {Storage=i.Storage with {OriginalCallerLayoutAndInitializationBound=false}},
            "overlap"=>i with {Storage=i.Storage with {Spans=i.Storage.Spans.Add(new(C.SpanRole.Timer,0x6000a0,56,"overlapping-owned-timer",E))}},
            "extra"=>i with {OriginalCalls=i.OriginalCalls.Add(Draw(C.Stage.Critical,500))},
            "missing"=>i with {OriginalCalls=i.OriginalCalls.RemoveAt(0)},
            "wrongsite"=>i with {OriginalCalls=i.OriginalCalls.SetItem(0,i.OriginalCalls[0] with {CallSite=0x14036e167})},
            "adapter"=>i with {OriginalCalls=i.OriginalCalls.SetItem(0,i.OriginalCalls[0] with {InclusiveAdapterInvoked=false})},
            "rounding"=>i with {Floating=i.Floating with {RoundToNearestEven=null}},_=>throw new ArgumentOutOfRangeException(nameof(invalid))
        };
        if(invalid=="conversion")i=i with {OriginalCalls=i.OriginalCalls.Select(c=>c.Stage==C.Stage.BaseRandom?
            Draw(C.Stage.BaseRandom,99,0,99):c).ToImmutableArray()};
        byte[] before=i.Storage.RecordBytes.ToArray();var result=Fail(i,code);
        Assert.Equal(before,i.Storage.RecordBytes);Assert.Null(result.Value);
        Assert.Equal(0u,i.DefenderCounterDeadlines[0]-11u);
    }
    [Fact]
    public void CurrentOpaqueR7CannotTurnOnSourceNoResistOrReplaceCurrentWords()
    {
        var i=Make();Assert.Equal((byte)1,i.Selected.OpaqueReserved7At47c);Assert.Equal(0,i.Selected.NoResist438);
        Assert.Equal(0,i.Skill.Words["438"]);
        Fail(i with {Skill=i.Skill with {Words=i.Skill.Words.SetItem("438",i.Selected.OpaqueReserved7At47c)}},"skill-word");
        Fail(i with {Skill=i.Skill with {Words=i.Skill.Words.Remove("43c")}},"skill-word");
        var properties=typeof(C.Input).GetProperties(System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
        Assert.DoesNotContain(properties,p=>p.Name.Contains("PassedHit")||p.Name.Contains("FinalHp")||typeof(Delegate).IsAssignableFrom(p.PropertyType));
    }
    [Fact]
    public void CriticalDamageReductionReadIsRequiredEvenWhenCriticalComparisonFails()
    {
        var i=Defender(Make(),d=>d with {CriticalDamageReduce14ca=null});
        Fail(i,"unknown");
    }
    [Fact]
    public void ActorStateAndManagerCounterBindingsCannotBeGuessedFromSourceDefaults()
    {
        var i=Make();var manager=new C.Manager(Bind(0x900000,"current-defender-manager"),0x900000,0,0,0);
        i=i with {Defender=i.Defender with {Manager=manager},Storage=i.Storage with {
            Spans=i.Storage.Spans.Add(new(C.SpanRole.Manager,0x900000,0x4500,"current-manager-storage",E))}};
        Pass(i);
        Fail(i with {Defender=i.Defender with {Manager=manager with {Force43e0=null}}},"unknown");
        Fail(i with {Defender=i.Defender with {Manager=manager with {Force43e0=1}}},"force");
        Fail(i with {Defender=i.Defender with {Manager=manager with {Force43e8=-1}}},"force");
        Fail(i with {Tables=i.Tables with {DodgeValue=null}},"unknown");
    }
    [Fact]
    public void PoseFiveSkipsDodgeAndNaturalDefensesButStillConsumesBeginAndCritical()
    {
        var i=Defender(Make(),d=>d with {Pose13c=5});
        i=i with {OriginalCalls=i.OriginalCalls.Where(c=>c.Stage is not(C.Stage.Dodge or C.Stage.Parry)).ToImmutableArray()};
        var r=Pass(i);Assert.DoesNotContain(r.ConsumedCalls,c=>c.Stage==C.Stage.Dodge||c.Stage==C.Stage.Parry);
        Assert.Equal(C.Stage.BeginChance,r.ConsumedCalls[0].Stage);Assert.Equal(C.Stage.Critical,r.ConsumedCalls[^1].Stage);
        Assert.Equal(500,Assert.Single(r.Trace,t=>t.Stage=="CriticalThreshold").Value);
    }
    [Fact]
    public void ParryFileRowOneSkipsTheParryDraw()
    {
        var i=Defender(Make(),d=>d with {StateIndex13d0=1});
        i=i with {Tables=i.Tables with {DodgeRow=Bind(0x145a2c634,"current-BSS-row1")},
            OriginalCalls=i.OriginalCalls.Where(c=>c.Stage!=C.Stage.Parry).ToImmutableArray()};
        Assert.DoesNotContain(Pass(i).ConsumedCalls,c=>c.Stage==C.Stage.Parry);
    }
    [Theory]
    [InlineData(181,181,false)] [InlineData(272,181,true)]
    public void BaseWrapperKeepsOriginalEndpointOrderAndEqualEndpointAdapterBypass(ushort first,ushort second,bool adapter)
    {
        var i=Attacker(Make(),a=>a with {Minimum14dc=first,Maximum14e0=second});
        i=i with {OriginalCalls=i.OriginalCalls.Select(c=>c.Stage==C.Stage.Base?Draw(C.Stage.Base,181,first,second):c).ToImmutableArray()};
        var r=Pass(i);var original=Assert.Single(r.ConsumedCalls,c=>c.Stage==C.Stage.Base);
        Assert.Equal(first,original.First);Assert.Equal(second,original.Second);Assert.Equal(adapter,original.InclusiveAdapterInvoked);
        Assert.Equal(346,r.PreliminaryReturn);
    }
    [Fact]
    public void RawUntouchedCellsAndOwnedImmutableOutputRemainDistinctFromCallerState()
    {
        var i=Make();var bytes=i.Storage.RecordBytes.SetItem(53,0x5a).SetItem(55,0xa5);
        i=i with {Storage=i.Storage with {RecordBytes=bytes}};
        var r=Pass(i);Assert.Equal((byte)0x5a,r.RecordAfter[53]);Assert.Equal((byte)0xa5,r.RecordAfter[55]);
        Assert.Equal(0u,BinaryPrimitives.ReadUInt32LittleEndian(r.HeaderAfter.AsSpan()));
        Assert.Equal(0,BinaryPrimitives.ReadInt32LittleEndian(i.Storage.RecordBytes.AsSpan(0,4)));
        Assert.Equal((byte)0,i.Storage.RecordBytes[0x30]);Assert.Equal((byte)10,r.RecordAfter[0x30]);
        var changed=r.RecordAfter.SetItem(0,255);Assert.NotEqual(changed[0],r.RecordAfter[0]);
    }

    [Theory]
    [InlineData(35,35,1000,1000,0,500,0,0,1,0,0,500,346,346,500,"43ad0000,00000000,43ad0000,43ad0000,43ad0000")]
    [InlineData(35,38,1000,1000,0,500,0,0,1,0,0,500,346,311,500,"439bb333,00000000,439bb333,439bb333,439bb333")]
    [InlineData(35,47,1000,1000,0,500,0,0,1,0,0,499,346,1,500,"00000000,00000000,00000000,00000000,00000000")]
    [InlineData(35,35,1000,1000,16777217,500,0,0,1,0,0,500,346,1,500,"43ad0000,49cccccd,c9ccc1fd,00000000,00000000")]
    [InlineData(35,35,1000,1500,57,500,0,0,1,17,17,500,404,265,333,"43ca68f6,40d33333,4384bd71,4384bd71,4384bd71")]
    [InlineData(35,35,2500,1000,0,500,0,1001,1,0,0,499,346,380,500,"43ad0000,00000000,43ad0000,43be4ccd,43be4ccd")]
    [InlineData(35,35,1000,1000,0,500,0,65535,1,0,0,499,346,346,500,"43ad0000,00000000,43ad0000,43ad0000,43ad0000")]
    [InlineData(35,35,1000,1000,0,500,501,0,1,0,0,1,346,346,-1,"43ad0000,00000000,43ad0000,43ad0000,43ad0000")]
    [InlineData(35,35,1000,1000,0,0,0,0,1,0,0,1,346,346,0,"43ad0000,00000000,43ad0000,43ad0000,43ad0000")]
    [InlineData(35,35,1000,1000,0,65535,0,0,1,0,0,499,346,519,500,"43ad0000,00000000,43ad0000,4401c000,4401c000")]
    [InlineData(35,35,1000,1000,0,500,0,123,3,0,0,499,346,158,500,"43ad0000,00000000,43ad0000,43ee3893,431ed062")]
    [InlineData(35,40,2500,1500,123,500,5,11,2,31,-13,329,453,152,330,"439ea419,412b3333,434c6354,4398e1da,4318e1da")]
    public void IndependentExtraFractionVectorsRetainEveryFloatRoundingAndLateConversion(int al,int dl,int ar,int dr,
        int defense,int critical,int resistance,int reduction,int sharing,int attackMovement,int defenseMovement,int draw,
        int preliminary,int final,int threshold,string words)
    {
        // Expected words are the separately authored exact-rational/RNE vectors, not outputs from this C# unit.
        var i=Attacker(Make(),a=>a with {Level1492=(ushort)al,Ratio1514=(uint)ar,Critical14bc=(ushort)critical,Movement189=1});
        i=Defender(i,d=>d with {Level1492=(ushort)dl,Ratio1514=(uint)dr,Defense1494=(uint)defense,
            CriticalReduce14c6=(ushort)resistance,CriticalDamageReduce14ca=(ushort)reduction,Movement189=2});
        i=i with {Tables=i.Tables with {
            AttackerMovement=new(Bind(0x141069eec,"chosen-current-row1"),1,[attackMovement,0,0,0,0,0,0]),
            DefenderMovement=new(Bind(0x141069f08,"chosen-current-row2"),2,[0,defenseMovement,0,0,0,0,0])},
            Skill=i.Skill with {Sharing90=(ushort)sharing},
            OriginalCalls=i.OriginalCalls.Select(c=>c.Stage==C.Stage.Critical?Draw(c.Stage,draw):c).ToImmutableArray()};
        var r=Pass(i);Assert.Equal(preliminary,r.PreliminaryReturn);Assert.Equal(final,r.FinalRecordDamage);
        Assert.Equal(threshold,Assert.Single(r.Trace,t=>t.Stage=="CriticalThreshold").Value);
        string[] stages=["after_level","armor_term","after_pdef_and_ratio","after_critical","after_sharing"];
        uint[] expected=words.Split(',').Select(x=>Convert.ToUInt32(x,16)).ToArray();
        for(int n=0;n<stages.Length;n++)Assert.Equal(expected[n],r.FloatBits[stages[n]]);
    }
    [Theory]
    [InlineData(int.MinValue,1)] [InlineData(-1,100)] [InlineData(0,100)]
    public void RunInfoAndChanceOriginalDrawRemainIndependent(int runInfo,int chanceDraw)
    {
        var i=Make();i=i with {Skill=i.Skill with {RunInfo58=runInfo},
            OriginalCalls=i.OriginalCalls.Select(c=>c.Stage==C.Stage.BeginChance?Draw(c.Stage,chanceDraw,1,100):c).ToImmutableArray()};
        var r=Pass(i);Assert.Equal(0,r.BeginFallback);
        Assert.Equal((uint)chanceDraw,Assert.Single(r.ConsumedCalls,c=>c.Stage==C.Stage.BeginChance).Returned);
    }
    [Fact]
    public void SkippedDodgeRowPreservesOriginalCacheAndSkipsOnlyReachedForceRead()
    {
        var i=Make();var manager=new C.Manager(Bind(0x900000,"current-defender-manager"),0x900000,null,0,0);
        byte[] cache=i.Storage.CacheBytes.ToArray();BinaryPrimitives.WriteInt32LittleEndian(cache.AsSpan(12,4),123);
        i=i with {Defender=i.Defender with {Manager=manager},Tables=i.Tables with {DodgeValue=1},
            Storage=i.Storage with {CacheBytes=cache.ToImmutableArray(),
                Spans=i.Storage.Spans.Add(new(C.SpanRole.Manager,0x900000,0x4500,"manager",E))},
            OriginalCalls=i.OriginalCalls.Where(c=>c.Stage!=C.Stage.Dodge).ToImmutableArray()};
        var r=Pass(i);Assert.Equal(123,BinaryPrimitives.ReadInt32LittleEndian(r.CacheAfter.AsSpan(12,4)));
        Assert.DoesNotContain(r.Writes,w=>w.Role==C.SpanRole.Cache&&w.Offset==12);
        Fail(i with {Defender=i.Defender with {Manager=manager with {Force43f0=null}}},"unknown");
    }
    [Theory]
    [InlineData(152,false)] [InlineData(153,true)]
    public void NpcEvasionNarrowsTheLevelAdjustedValueBeforeInclusiveHitComparison(int draw,bool hit)
    {
        // Binary32(65535*1.1)=72088.5 -> CVTT72088 -> low16=6552;6552-6400=152.
        var i=Attacker(Make(),a=>a with {Accuracy14aa=6400});
        i=Defender(i,d=>d with {Level1492=38,Evasion14ae=65535});
        i=hit?i with {OriginalCalls=i.OriginalCalls.SetItem(0,Draw(C.Stage.Dodge,draw))}:
            Calls(i,Draw(C.Stage.Dodge,draw),Clock(C.Stage.DodgeClock,7));
        var r=Pass(i);Assert.Equal(hit,r.Hit);
        Assert.Equal(152,Assert.Single(r.Trace,t=>t.Stage=="DodgeThreshold").Value);
        Assert.Equal(6400,BinaryPrimitives.ReadInt32LittleEndian(r.CacheAfter.AsSpan(12,4)));
        Assert.Equal(hit?(int?)311:null,r.FinalRecordDamage);
    }
    [Theory]
    [InlineData(true,500)] [InlineData(false,400)]
    public void NaturalDefenseEqualityWritesThenConsumesClockBeforeCritical(bool block,int threshold)
    {
        var i=Defender(Make(),d=>d with {Block14b6=block?(ushort)500:(ushort)0,Parry14b4=400});
        var calls=i.OriginalCalls.Where(c=>c.Stage is not(C.Stage.Parry or C.Stage.Critical)).ToList();
        calls.Add(Draw(block?C.Stage.Block:C.Stage.Parry,threshold));
        calls.Add(Clock(block?C.Stage.BlockClock:C.Stage.ParryClock,uint.MaxValue));calls.Add(Draw(C.Stage.Critical,500));
        var r=Pass(i with {OriginalCalls=calls.ToImmutableArray()});Assert.Equal(311,r.FinalRecordDamage);
        int draw=Assert.Single(r.Trace,t=>t.Stage=="OriginalDraw:"+(block?C.Stage.Block:C.Stage.Parry)).Sequence;
        int clock=Assert.Single(r.Trace,t=>t.Stage=="OriginalCounterClock:"+(block?C.Stage.BlockClock:C.Stage.ParryClock)).Sequence;
        var label=Assert.Single(r.Writes,w=>w.Role==C.SpanRole.Record&&w.Offset==0x30);
        var header=Assert.Single(r.Writes,w=>w.Role==C.SpanRole.Header);var counter=Assert.Single(r.CounterWrites);
        int crit=Assert.Single(r.Trace,t=>t.Stage=="OriginalDraw:Critical").Sequence;
        Assert.True(draw<label.Sequence&&label.Sequence<header.Sequence&&header.Sequence<clock&&clock<counter.Sequence&&counter.Sequence<crit);
        Assert.Equal(4999u,counter.After);
    }
    [Fact]
    public void TemplateDirectionBypassSkipsGeometryAndLazyStateWhileFailedDirectionPublishesBetweenOriginalCalls()
    {
        var i=Defender(Make(),d=>d with {TemplateFlags3f0=4,Heading12c=null,X120Bits=null,Y124Bits=null});
        i=Attacker(i,a=>a with {X120Bits=null,Y124Bits=null});
        i=i with {Globals=i.Globals with {RaceMask154827468=null,RaceLimit14f523f78=null}};
        Assert.Equal(346,Pass(i).FinalRecordDamage);
        var late=Attacker(Make(),a=>a with {X120Bits=0x3f800000});
        late=late with {Globals=late.Globals with {RaceMask154827468=0,RaceLimit14f523f78=3},
            OriginalCalls=late.OriginalCalls.Where(c=>c.Stage!=C.Stage.Additional).ToImmutableArray()};
        var r=Pass(late);var write=Assert.Single(r.GlobalWrites);
        Assert.True(Assert.Single(r.Trace,t=>t.Stage=="OriginalDraw:BaseRandom").Sequence<write.Sequence);
        Assert.True(write.Sequence<Assert.Single(r.Trace,t=>t.Stage=="OriginalDraw:Parry").Sequence);
        Assert.DoesNotContain(r.ConsumedCalls,c=>c.Stage==C.Stage.Additional);
        Fail(late with {Globals=late.Globals with {RaceMask154827468=null}},"unknown");
    }
    [Theory]
    [InlineData("cache-init")] [InlineData("crit-init")] [InlineData("manager-alias")]
    [InlineData("effect-alias")] [InlineData("ref-alias")] [InlineData("rows")]
    [InlineData("header-init")] [InlineData("prefix-init")]
    public void CurrentStorageAndLoadedRowContradictionsCannotImpersonateOneSerializedInvocation(string failure)
    {
        var i=Make();string code="alias";
        switch(failure)
        {
            case "cache-init":i=i with {Storage=i.Storage with {CacheBytes=i.Storage.CacheBytes.SetItem(0,1)}};code="storage";break;
            case "crit-init":i=i with {Storage=i.Storage with {RecordBytes=i.Storage.RecordBytes.SetItem(0x31,12)}};code="storage";break;
            case "header-init":i=i with {Storage=i.Storage with {HeaderBytes=i.Storage.HeaderBytes.SetItem(1,1)}};code="storage";break;
            case "prefix-init":i=i with {Storage=i.Storage with {RecordBytes=i.Storage.RecordBytes.SetItem(20,0x5a)}};code="storage";break;
            case "rows":i=i with {Tables=i.Tables with {DefenderMovement=i.Tables.DefenderMovement with {Values=[0,1,0,0,0,0,0]}}};code="movement";break;
            default:
                var role=failure=="manager-alias"?C.SpanRole.Manager:failure=="effect-alias"?C.SpanRole.Effect:C.SpanRole.Reference;
                i=i with {Storage=i.Storage with {Spans=i.Storage.Spans.Add(new(role,0x6000a0,56,"owned-overlap",E))}};break;
        }
        Fail(i,code);
    }
    [Theory]
    [InlineData("unknown")] [InlineData("source")] [InlineData("source-offset")] [InlineData("destination")]
    [InlineData("frame")] [InlineData("read-site")] [InlineData("write-site")] [InlineData("argument")]
    [InlineData("argument-store")] [InlineData("call")]
    public void ActualCallerConditionCopyCannotBeReplacedByAFlagOrDirectSkillContainment(string failure)
    {
        var i=Make();var p=i.Skill.ConditionProducer;
        p=failure switch
        {
            "unknown"=>p with {Kind=C.ConditionCopyKind.Unknown},
            "source"=>p with {SourceSkillAddress=0x500100},"source-offset"=>p with {SourceDescriptorAddress=0x500490},
            "destination"=>p with {DestinationAddress=0x500480},
            "frame"=>p with {CallerFrameRbp=Bind(0x600010,"wrong-caller-RBP")},
            "read-site"=>p with {FirstReadSite=0},"write-site"=>p with {LastWriteSite=0},
            "argument"=>p with {ArgumentLeaSite=0},"argument-store"=>p with {ArgumentStoreSite=0},
            "call"=>p with {CalculatorCallSite=0},_=>throw new ArgumentOutOfRangeException(nameof(failure))
        };
        Fail(i with {Skill=i.Skill with {ConditionProducer=p}},"condition-producer");
    }
    [Fact]
    public void HeaderCopyAndFirstPhysicalRecordAreDisjointRangesInsideTheirActualCallerFrame()
    {
        var i=Make();var p=i.Skill.ConditionProducer;
        Assert.Equal(i.Skill.Current.Address+0x480,p.SourceDescriptorAddress);
        Assert.Equal(p.CallerFrameRbp.Address,i.Storage.Header.Address);
        Assert.Equal(p.CallerFrameRbp.Address+0x70,i.Skill.ConditionCopy.Address);
        Assert.Equal(p.CallerFrameRbp.Address+0xa0,i.Storage.Record.Address);
        Assert.Equal(48,i.Skill.Condition480.Length);Assert.Equal(4,i.Storage.HeaderBytes.Length);Assert.Equal(56,i.Storage.RecordBytes.Length);
        Assert.Equal(i.Skill.ConditionCopy.Address+48,i.Storage.Record.Address);
        Pass(i);
    }

    // Frozen independent arithmetic/heading payloads are inserted by the external fixture renderer.
    private const string FrozenNumericSha256="e662a623f75da1861c7209e3a2aabd2bb141966558057487672652bebde95d75";
    private const string FrozenHeadingSha256="6d9aa50a9787382295611faf963a40aa578d59d4e62b9f2edf0f4648e34a76e2";
    private const string FrozenNumericEvidence="H4sIAAAAAAAC/+3dTZPbRpqu4f+itR1BgAAJeDcdJ87uzGaWExMKVpFsK1qWHLLcHzMx//2wqlgUASNJghbIysxrdq03VfoqPzfx4M53/vN/3v32tw8fP77/sH73U7Go68UP7z79+vj8P8uiXC53//vzl/Xmy2b9/uHDp/WHT39999P//OHI0/9eb/7+afXL5t1P7/7j59X68z9++7r6+LfNl//zfj57/2+bd89f58On1cfdz/ph/4vuz//7v/3H+//3+e+bv2x+3v0S7//9y7tvv+j2w+bj+rd3P/3nfx7/nB8Gfs5//fB65OPm75uPuzNlcfSDX1Zfn37e7OiHHj///unr7sd2x/7rh3fbD//8+vuXzfu/b7582P2quz/ddvXxt83/7n6zn3//8rh5v/m0/vXzh93P+OndLx8+vfvDj7//++rj77s/T9EUP7z7svq0/vzL09/Zu59mP7z75fP66Yt+efdTu/tfXzYfV18/fP709Ff582b18re6+/H1Zrv7Ypsv7//5r92fePbDbPfbWn39unr82+uPFc8/tv7wZfP49AXebz+udj919+utPn7ZfaF/vf/w6cPXD6uPH/5793f3ZbX73f2y+u1vzyfWn7/ufjtfd3+X7x62zezp/3Z/hofdb/Tx592Pffr86cfD1/3x6Wf99uN/b758/vHLZvfX8unH57+M3U94/PzLr79/ffrqhz/E69/T4+dPXzf//Pr8DfLyL/vp85dfVk//FL/u/mjPf8THL7vf3+Pq4/v1l9U/3v1Uz3Y/9tvPqy/PfwXf/t5eprvZw+q3zfuPn//x9Ff3/Bf7/AO///rr0w+Uy/Lpn+fr6q+b355+1X98WT0N3v/84ev7X37/+PQvNT/6oz79zMfPm+32w+OHzfO/YzWbdaZPv+z7X798Xv/++DyeP9TH4398+Prz01/51/dPfzMDB15+909/+c/DVblZLBbvnv75/775ZfdLvn/519wPm5ef+evub/LD7jtq9eVfuy/++fOX958/bbpHVtuvuz/W01/i+92v8PS9/OHz0Innb6Dd7+Plyxx+k71Tr/8GQ7PDv8XxaPv03+3w7+1/u7//l2+Wdz/Ny/r1p+2+pXb/Mb9fr37Z/TPtJ99+B8/fO0ffFl9//rL57efPH9cD3xufv3z46/OX/PDp758fVy//Aez/q/386ePTfx//8/Ld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";
    private const string FrozenHeadingEvidence="H4sIAAAAAAAC/+1WO47DIBS8i+sUYOA9fJYohcfA/Y8QBbKa2X6rlV2NEHpvfkh+Prfw/bbHllqx0rNN2Czul39g30MK6Zqw5G59n7Dmmnqb8OjJ/ZwQ7ldIE7aQRisT9urR4gcObhsB1nxuG9H8iHnCPUbEviDytWPCVEIevmBP/VoTso0UzgVHBCadUQxpLws2pLbuWo5HOBY8Wo9rsfUx4jr15NW/0LMtbcNP814XvNoRx4L9qGOR9HFaWswqtcmp3pUJMle2kYMwU75UIdpEsfgg7ohn4qT4K65LFpIQc5M0NWNJXvogLZHusFHSM7ZPOgmegnfBCeBccBvIAWQG8gVVgNpAxaAPoDugZ6CToL+g62AWYEJgbmCaYMZg8mAfwJaA3QEbBfYMbB/YyV+nelcmyFzZRg7CTPlShWgTxeKDuCOeiZPir7guWUhCzE3S1IwleemDtES6w0ZJz37a93o87/d9v+/7ff/T9/3Y6v0f9Cf/Qa/XG3ERFCtVCgAA";
}
