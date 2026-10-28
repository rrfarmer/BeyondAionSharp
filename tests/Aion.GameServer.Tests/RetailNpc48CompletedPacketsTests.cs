using System.Buffers.Binary;
using System.Collections.Immutable;
using Aion.GameServer.GeoEngine.RetailPath;
using Aion.GameServer.Network.Aion.ServerPackets;
using P=Aion.GameServer.GeoEngine.RetailPath.RetailNpc48PresentationInputs;
using Q=Aion.GameServer.GeoEngine.RetailPath.RetailNpc48CompletedPackets;
using O=Aion.GameServer.GeoEngine.RetailPath.RetailNpcManagedMembershipObservation;
using M=Aion.GameServer.GeoEngine.RetailPath.RetailNpcManagedMembershipMove;

namespace Aion.GameServer.Tests;

public sealed class RetailNpc48CompletedPacketsTests
{
    public static IEnumerable<object[]> Original25BothRaces()=>Spawn48Fixture.Cases.RootElement.GetProperty("rows").EnumerateArray()
        .Select(x=>new object[]{x.GetProperty("npc").GetUInt32(),x.GetProperty("race").GetString()!,x.GetProperty("body").GetString()!,x.GetProperty("original_npc_base64").GetString()!});
    [Theory][MemberData(nameof(Original25BothRaces))]
    public void WholeCrossCellOwnerDerivesAll25RetainedSourceBodiesForBothPlayerTribes(uint npc,string race,string expected,string originalNpc)
    {
        var f=new Spawn48Fixture(npc,race);Assert.Equal(Convert.FromBase64String(originalNpc),f.Input.Npc.Image.Bytes.ToArray());
        var raw=Spawn48Fixture.Cases.RootElement.GetProperty("raw_fixture");
        Assert.Equal(Convert.FromBase64String(raw.GetProperty("template_base64").GetString()!),f.Input.Npc.Template.Bytes.ToArray());
        foreach(var user in raw.GetProperty("original_users").EnumerateArray())
            Assert.Equal(Convert.FromBase64String(user.GetProperty("bytes").GetString()!),Assert.Single(f.Input.Scene.Users,x=>x.Key.ObjectId==user.GetProperty("object_id").GetUInt32()).Image.Bytes.ToArray());
        Assert.Equal(Convert.FromHexString(raw.GetProperty("canonical_move_hex").GetString()!),f.Input.CanonicalMove.ToArray());
        var candidate=f.Candidate();var owner=M.Owner.FromDeclaredInput(f.Input);
        var result=Q.Calculate(candidate,f.Input,f.Capability(),f.Limits);Good(result);
        Assert.Equal(3,result.Deliveries.Length);var spawn=Assert.Single(result.Deliveries,x=>x.Packet.GetOpCode()==14);
        Assert.Equal(100u,spawn.RecipientId);Assert.Equal("user-life-0",spawn.RecipientLifetime);Assert.Equal("connection-0",spawn.ConnectionGeneration);
        Assert.Equal(Convert.FromHexString(expected),Spawn48Fixture.Capture(spawn.Packet));
        Assert.Contains(result.Deliveries,x=>x.Packet.GetOpCode()==55);Assert.Contains(result.Deliveries,x=>x.Packet.GetOpCode()==22);
        Assert.False(owner.Consumed);Assert.Equal(candidate.Value.SourceSnapshotSha256,result.SourceSnapshotSha256);
        Assert.Equal(M.OnceStatus.ComputedFirstApplication,owner.CalculateOnce(candidate).Status);
        Assert.False(result.HostEffectsExecuted);Assert.False(result.OnceLedgerConsumed);Assert.False(result.CurrentHostObserved);Assert.False(result.ActivationReady);Assert.False(result.CanApplyToHost);
    }
    [Theory][InlineData(31f,32f)][InlineData(63f,64f)]
    public void FullMotionIsCalculatedThrough32And64BeforeAnyPresentationAdapter(float oldX,float newX)
    {
        var f=new Spawn48Fixture(oldX:oldX,newX:newX);var candidate=f.Candidate();var r=Q.Calculate(candidate,f.Input,f.Capability(),f.Limits);Good(r);
        Assert.Equal(ManagedMembershipFixture.V(newX),candidate.Value.State.Motion.State.Endpoint120);
        Assert.Equal((int)newX>>5,Assert.Single(candidate.Value.State.Membership,x=>x.Actor.ObjectId==7).Cell);
        var raw=Spawn48Fixture.Capture(Assert.Single(r.Deliveries,x=>x.Packet.GetOpCode()==14).Packet);
        Assert.Equal(ManagedMembershipFixture.Bits(newX),BinaryPrimitives.ReadUInt32LittleEndian(raw));
        Assert.Contains(candidate.Value.State.Events,x=>x.Stage=="EndpointWorldHeadingPublished");
    }
    [Fact] public void OldConnectedSpawnGateRemainsClosedWhileNewWholeAdapterIsSeparate()
    {
        var f=new Spawn48Fixture();var candidate=f.Candidate();
        Assert.Empty(RetailNpc48MoveDeletePackets.CalculateCompleted(candidate).Deliveries);
        Assert.False(RetailNpc48MoveDeletePackets.CalculateCompleted(candidate).HostEffectsExecuted);
        Good(Q.Calculate(candidate,f.Input,f.Capability(),f.Limits));
    }
    [Fact] public void EntireNoConnectionBatchSkipsUnavailablePresentationAndConsumesNothing()
    {
        var f=new Spawn48Fixture();f.Input=f.Input with{Scene=f.Input.Scene with{Users=[..f.Input.Scene.Users.Select(x=>x with{ConnectionPresent=false,Image=O.Replace(x.Image,0x3720,BitConverter.GetBytes(0ul))})]}};
        var c=f.Candidate();var r=Q.Calculate(c,f.Input,null,f.Limits);Good(r);Assert.Empty(r.Deliveries);Assert.Null(r.PresentationSnapshotSha256);
        Assert.DoesNotContain(r.KnownReads,x=>x.Stage=="PresentationOrigin"||x.Stage=="OriginalBirthClock");
    }
    [Fact] public void MissingConnectedPresentationFailsAfterEarlierMoveDeleteWithoutPublishingThem()
    {
        var f=new Spawn48Fixture();var r=Q.Calculate(f.Candidate(),f.Input,null,f.Limits);
        Assert.Equal(Q.Status.UnsupportedReachedMeaning,r.Status);Assert.True(r.FailureIndex>0);Assert.Empty(r.Deliveries);Assert.Equal("presentation",r.Failure!.Code);
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)]
    public void SourceHashOperationEpochAndLifeJoinUseTheOriginalCompleteInput(int changed)
    {
        var f=new Spawn48Fixture();var c=f.Candidate();var old=f.Input;
        var different=changed switch{0=>ManagedMembershipFixture.UserImage(old,0,u=>O.Put32(u,0x7358,2)),1=>old with{Evidence=old.Evidence with{Operation="other"}},
            2=>old with{Evidence=old.Evidence with{Epoch="other"}},_=>old with{Npc=old.Npc with{Key=old.Npc.Key with{Lifetime="respawned"}}}};
        var r=Q.Calculate(c,different,f.Capability(),f.Limits);Assert.Empty(r.Deliveries);Assert.NotNull(r.Failure);Assert.False(r.OnceLedgerConsumed);
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)]
    public void ReachedHostClassSourceWorldGenerationAndPlayerFieldsCannotBeChosenAnswers(int missing)
    {
        var f=new Spawn48Fixture();f.Raw=missing switch{0=>f.Raw with{Npc=f.Raw.Npc with{RuntimeType="custom-subclass"}},
            1=>f.Raw with{Npc=f.Raw.Npc with{Binding=f.Raw.Npc.Binding with{Lifetime="stale"}}},
            2=>f.Raw with{World=f.Raw.World with{Generation="stale"}},3=>f.Raw with{Players=[]},
            4=>f.Raw with{Players=f.Raw.Players.SetItem(0,f.Raw.Players[0] with{OriginalPanesterraFaction=P.Reference.Unknown})},
            _=>f.Raw with{Npc=f.Raw.Npc with{OriginalStateField=null}}};
        Failed(f.Calculate());
    }
    [Theory][InlineData(-1L,1)][InlineData(0L,1)][InlineData(1499L,1)][InlineData(1500L,0)][InlineData(1501L,0)]
    public void OriginalBirthClockUsesStrictSigned64ElapsedBoundary(long elapsed,int expected)
    {
        var f=new Spawn48Fixture();f.Raw=f.Raw with{Npc=f.Raw.Npc with{OriginalLastSpawnMillis=100000-elapsed}};
        var r=f.Calculate();Good(r);var raw=Spawn48Fixture.Capture(Assert.Single(r.Deliveries,x=>x.Packet.GetOpCode()==14).Packet);
        Assert.Equal((byte)expected,raw[75]);
        if(elapsed==1499)Assert.Equal(Convert.FromHexString(Spawn48Fixture.Extra("birth1499")),raw);
    }
    [Fact] public void BirthClockOverflowUsesJavaWrapAndNoGeneratedClock()
    {
        var f=new Spawn48Fixture();f.Raw=f.Raw with{Npc=f.Raw.Npc with{OriginalLastSpawnMillis=long.MaxValue},OriginalBirthClocks=[f.Raw.OriginalBirthClocks[0] with{UnixMillis=long.MinValue}]};
        var r=f.Calculate();Good(r);var clock=Assert.Single(r.KnownReads,x=>x.Stage=="OriginalBirthClock");Assert.Equal(100u,clock.RecipientId);Assert.Equal<long?>(long.MinValue,clock.Value);
        Assert.Equal((byte)1,Spawn48Fixture.Capture(Assert.Single(r.Deliveries,x=>x.Packet.GetOpCode()==14).Packet)[75]);
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)]
    public void MissingWrongActorNullAndUnusedBirthClocksWithholdWholeBatch(int gap)
    {
        var f=new Spawn48Fixture();var a=f.Raw.OriginalBirthClocks;
        f.Raw=f.Raw with{OriginalBirthClocks=gap switch{0=>[],1=>a.SetItem(0,a[0] with{ObjectLifetime="stale"}),2=>[null!],_=>a.Add(a[0])}};
        Failed(f.Calculate());
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)]
    public void TownProofRequiresCompleteCurrentConstructorAndInheritedFieldHistory(int gap)
    {
        var f=new Spawn48Fixture();var z=f.Raw.World.OriginalCompleteRegistrations;
        z=gap switch{0=>default,1=>z.RemoveAt(0),2=>z.SetItem(0,z[0] with{Producer=P.ZoneProducer.Unknown}),3=>z.SetItem(0,z[0] with{OriginalTownIdField=1}),_=>z.SetItem(0,z[0] with{CurrentTemplateGeneration="later-unknown-writer"})};
        f.Raw=f.Raw with{World=f.Raw.World with{OriginalCompleteRegistrations=z}};Failed(f.Calculate());
    }
    [Fact] public void MaterialConstructorHasInheritedZeroButUnknownProducerNeverBecomesTownZero()
    {
        var f=new Spawn48Fixture();var material=new P.ZoneRegistration("material1","material-generation",P.ZoneProducer.MaterialZoneTemplate,Q.MaterialZoneRuntime,"ordinary SPHERE",P.MapId,
            Spawn48Fixture.Sources.SnapshotSha256,"material-generation",0,"source-geometry-owner","source-geometry-generation");
        f.Raw=f.Raw with{World=f.Raw.World with{OriginalCompleteRegistrations=f.Raw.World.OriginalCompleteRegistrations.Add(material)}};
        Good(f.Calculate());f.Raw=f.Raw with{World=f.Raw.World with{OriginalCompleteRegistrations=f.Raw.World.OriginalCompleteRegistrations.SetItem(6,material with{RuntimeTemplateType="custom"})}};
        Failed(f.Calculate());
    }
    [Fact] public void UnspawnedPositionFieldSkipsUnreachedCurrentZoneRegistry()
    {
        var f=new Spawn48Fixture();f.Raw=f.Raw with{Npc=f.Raw.Npc with{OriginalPositionSpawnedField=false},World=f.Raw.World with{OriginalCompleteRegistrations=default}};
        var r=f.Calculate();Good(r);Assert.DoesNotContain(r.KnownReads,x=>x.Stage=="VerifiedCurrentZoneConstructor");
    }
    [Theory][InlineData("ATTACKABLE",0)][InlineData("PEACE",2)][InlineData("AGGRESSIVE",8)][InlineData("INVULNERABLE",10)][InlineData("FRIEND",38)][InlineData("SUPPORT",54)]
    public void ExactOriginalOverrideEnumIsCalculatedAsPresentationOnly(string token,int expected)
    {
        var f=new Spawn48Fixture();f.Raw=f.Raw with{Npc=f.Raw.Npc with{OriginalOverriddenType=new(P.Reference.Present,token)}};
        var c=f.Candidate();var r=Q.Calculate(c,f.Input,f.Capability(),f.Limits);Good(r);
        Assert.Equal((byte)expected,Spawn48Fixture.Capture(Assert.Single(r.Deliveries,x=>x.Packet.GetOpCode()==14).Packet)[24]);
        Assert.Equal(c.Value.State.Image.Bytes.ToArray(),M.Calculate(f.Input).Candidate!.Value.State.Image.Bytes.ToArray());
    }
    [Theory][InlineData(64,0)][InlineData(256,2)][InlineData(320,0)]
    public void EnemyAndNeutralRawStateRetainJavaPriority(int flags,int expected)
    {
        var f=new Spawn48Fixture();f.Raw=f.Raw with{Npc=f.Raw.Npc with{OriginalOverriddenType=new(P.Reference.Present,"PEACE")},
            Players=f.Raw.Players.SetItem(0,f.Raw.Players[0] with{OriginalCustomStateField=flags})};
        var r=f.Calculate();Good(r);Assert.Equal((byte)expected,Spawn48Fixture.Capture(Assert.Single(r.Deliveries,x=>x.Packet.GetOpCode()==14).Packet)[24]);
    }
    [Fact] public void MasterUtf16StaticIdAndRetailHpPercentageComeFromTheirActualPrograms()
    {
        var f=new Spawn48Fixture();f.Raw=f.Raw with{Npc=f.Raw.Npc with{OriginalMasterNameField=new(true,"A\ud800B")}};
        var r=f.Calculate();Good(r);Assert.Equal(Convert.FromHexString(Spawn48Fixture.Extra("master_utf16")),Spawn48Fixture.Capture(Assert.Single(r.Deliveries,x=>x.Packet.GetOpCode()==14).Packet));
        f=new();f.Raw=f.Raw with{Npc=f.Raw.Npc with{OriginalSpawn=f.Raw.Npc.OriginalSpawn! with{OriginalStaticIdField=-1}}};
        r=f.Calculate();Good(r);Assert.Equal(Convert.FromHexString(Spawn48Fixture.Extra("static_wrap")),Spawn48Fixture.Capture(Assert.Single(r.Deliveries,x=>x.Packet.GetOpCode()==14).Packet));
        f=new();f.Npc32(0x778,1);f.Npc32(0x774,201);r=f.Calculate();Good(r);
        Assert.Equal(Convert.FromHexString(Spawn48Fixture.Extra("retail_percent_differs")),Spawn48Fixture.Capture(Assert.Single(r.Deliveries,x=>x.Packet.GetOpCode()==14).Packet));
    }
    [Fact] public void NonzeroTargetNeedsActualRetainedOwnedGraphAndLifetime()
    {
        var f=new Spawn48Fixture();f.Npc32(0x130c,101);
        f.Raw=f.Raw with{OriginalTargetObjects=[new(101,"user-life-1",Q.PlayerRuntime,"world","world-generation",P.MapId,1,false)]};
        var r=f.Calculate();Good(r);Assert.Equal(Convert.FromHexString(Spawn48Fixture.Extra("target_User")),Spawn48Fixture.Capture(Assert.Single(r.Deliveries,x=>x.Packet.GetOpCode()==14).Packet));
        f.Raw=f.Raw with{OriginalTargetObjects=[f.Raw.OriginalTargetObjects[0] with{Lifetime="dead-replaced"}]};Failed(f.Calculate());
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)]
    public void ActiveUnknownCreatorTransformCustomNameAndTargetHaveNoZeroFallback(int branch)
    {
        var f=new Spawn48Fixture();switch(branch)
        {
            case 0: f.Raw=f.Raw with{Npc=f.Raw.Npc with{OriginalCreatorIdField=123}};break;
            case 1: f.Raw=f.Raw with{Npc=f.Raw.Npc with{OriginalTransformModel=P.Reference.Unknown}};break;
            case 2: f.Raw=f.Raw with{Npc=f.Raw.Npc with{OriginalOverriddenType=new(P.Reference.Present,"UNKNOWN")}};break;
            case 3: f.Input=ManagedMembershipFixture.NpcImage(f.Input,x=>O.Replace(x,0x1440,[65,0,0,0]));break;
            default:f.Npc32(0x130c,999);break;
        }
        Failed(f.Calculate());
    }
    [Fact] public void ResolvedOrderedItemSourceCalculatesFirstFreeSlotsAndNative23SlotsAreIgnored()
    {
        var f=new Spawn48Fixture();var records=Spawn48Fixture.Gear();
        f.Raw=f.Raw with{Npc=f.Raw.Npc with{OriginalOverrideEquipment=new(P.EquipmentKind.OriginalOrderedOverride,"original-equipped-gear","gear-generation","NpcEquippedGear(NpcEquipmentList)",[..records])}};
        var r=f.Calculate();Good(r);var body=Spawn48Fixture.Capture(Assert.Single(r.Deliveries,x=>x.Packet.GetOpCode()==14).Packet);
        Assert.Equal(Convert.FromHexString(Spawn48Fixture.Extra("first_free_equipment")),body);Assert.Equal(147,body.Length);Assert.Equal(3u,BinaryPrimitives.ReadUInt32LittleEndian(body.AsSpan(55,4)));
        Assert.Equal(900000001u,BinaryPrimitives.ReadUInt32LittleEndian(body.AsSpan(59,4)));Assert.Equal(900000002u,BinaryPrimitives.ReadUInt32LittleEndian(body.AsSpan(75,4)));
        Assert.DoesNotContain(r.KnownReads,x=>x.Detail?.StartsWith("111#",StringComparison.Ordinal)==true);
        Assert.Equal(2,r.KnownReads.Count(x=>x.Stage=="CalculatedFirstFreeSlot"));
    }
    [Fact] public void UnknownItemGroupIsAnExplicitWholeFailure()
    {
        var f=new Spawn48Fixture();f.Raw=f.Raw with{Npc=f.Raw.Npc with{OriginalOverrideEquipment=new(P.EquipmentKind.OriginalOrderedOverride,"gear","generation","NpcEquippedGear(NpcEquipmentList)",[..Spawn48Fixture.Gear("SWORD_typo")])}};
        var r=f.Calculate();Failed(r);Assert.Equal("enum",r.Failure!.Code);
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)]
    public void LateOutputBudgetsWithholdEveryEarlierPacketAndLeaveOnceUnconsumed(int budget)
    {
        var f=new Spawn48Fixture();f.Limits=budget switch{0=>f.Limits with{MaxDeliveries=1},1=>f.Limits with{MaxEncodedBytes=20},_=>f.Limits with{MaxSteps=2}};
        var c=f.Candidate();var owner=M.Owner.FromDeclaredInput(f.Input);var r=Q.Calculate(c,f.Input,f.Capability(),f.Limits);Failed(r);Assert.False(owner.Consumed);
        Assert.Equal(M.OnceStatus.ComputedFirstApplication,owner.CalculateOnce(c).Status);
    }
    [Fact] public void RepeatedEncodingDoesNotConsumeCandidateAndPacketSnapshotsOwnTheirBytes()
    {
        var f=new Spawn48Fixture();var c=f.Candidate();var a=Q.Calculate(c,f.Input,f.Capability(),f.Limits);var b=Q.Calculate(c,f.Input,f.Capability(),f.Limits);Good(a);Good(b);
        foreach(var pair in a.Deliveries.Zip(b.Deliveries))Assert.Equal(Spawn48Fixture.Capture(pair.First.Packet),Spawn48Fixture.Capture(pair.Second.Packet));
        var packet=Assert.Single(a.Deliveries,x=>x.Packet.GetOpCode()==14).Packet;var raw=Spawn48Fixture.Capture(packet);raw[0]=255;
        Assert.Equal(Convert.FromHexString(Spawn48Fixture.Extra("empty")),Spawn48Fixture.Capture(packet));
    }
    private static void Good(Q.Result r){Assert.Equal(Q.Status.EncodedWholeBatch,r.Status);Assert.Null(r.Failure);}
    private static void Failed(Q.Result r){Assert.NotEqual(Q.Status.EncodedWholeBatch,r.Status);Assert.Empty(r.Deliveries);Assert.NotNull(r.Failure);Assert.False(r.OnceLedgerConsumed);}
}
