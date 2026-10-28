using System.Buffers.Binary;
using System.Collections.Immutable;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Commons.Utils.RetailRandom;
using W=Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserNormalWorldInputs;
using O=Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserNormalWorldOwner;
using F=Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserNormalCompletion;
using P=Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserNormalProtection;
using H=Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcToUserImmediateDamageOwner;
using N=Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcImmediateDamageOwner;
using U=Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserPhysicalInputs;
using C=Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcToUserNormalRecordCalculation;
using GI=Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserGearStatLifeInputs;
namespace Aion.GameServer.Tests;

public sealed class RetailNpcUserNormalWorldOwnerTests
{
    public static IEnumerable<object[]> CompleteBanks()=>Enumerable.Range(0,WorldNormalFixture.Corpus.RootElement.GetProperty("cases").GetArrayLength()).Select(x=>new object[]{x});
    [Theory][MemberData(nameof(CompleteBanks))]
    public void All25_D_profiles_and_charge_lethal_recipes_compute_whole_raw_outputs(int index)
    {
        var f=new WorldNormalFixture(index);var e=f.Entry();var r=O.EvaluateWorld(e);var x=f.Row.GetProperty("expected");
        Assert.Equal(O.Status.CalculatedImmediateWorld,r.Status);Assert.Null(r.Failure);Assert.NotNull(r.Calculated);
        Assert.NotNull(r.Completion);Assert.True(r.Completion!.Completed,r.Completion.Failure?.ToString());Assert.Null(r.Retained);
        Assert.Equal(x.GetProperty("normal_packet_hex").GetString(),Convert.ToHexStringLower(r.Emission!.Bytes.AsSpan()));
        Assert.Single(r.Emission.Recipients);Assert.Equal(0x80001001u,r.Emission.Recipients[0].ObjectId);
        Assert.Equal(x.GetProperty("record").GetInt32(),r.Totals!.Hp);Assert.Equal(x.GetProperty("threat").GetInt32(),r.Totals.Threat);
        var hp=r.Completion.Hp!;Assert.Equal(x.GetProperty("application_cell").GetInt32(),hp.Owner.ApplicationCell);
        Assert.Equal(x.GetProperty("core_loss").GetInt32(),hp.CoreActualHpLoss);Assert.Equal(x.GetProperty("post_gear_hp").GetInt32(),hp.Owner.Calculated!.Receiver.Hp778);
        Assert.Equal(x.GetProperty("total_hp_loss").GetInt32(),hp.TotalImmediateHpLoss);
        Assert.Equal(x.GetProperty("post_gear_max_hp").GetInt32(),hp.EffectiveMaxima!.CurrentHp);
        Assert.Equal(x.GetProperty("post_gear_mp").GetInt32(),hp.Owner.Calculated.Receiver.Mp790);
        Assert.Equal(x.GetProperty("post_gear_max_mp").GetInt32(),hp.Owner.Calculated.Receiver.MaxMp78c);
        Assert.Equal(x.GetProperty("pending728").GetInt32(),hp.Owner.Calculated.Receiver.Pending728);
        Assert.Equal(77,hp.Owner.Calculated.User.FirstDeathKind5b24);
        Assert.Equal(x.GetProperty("raw_killer918").GetUInt64(),WorldNormalFixture.U64(r.Calculated!,WorldNormalFixture.User+0x918));
        Assert.Equal(x.GetProperty("managed_killer_id918").ValueKind==JsonValueKind.Null?(uint?)null:x.GetProperty("managed_killer_id918").GetUInt32(),hp.Owner.Calculated.Receiver.Killer918);
        Assert.Equal(x.GetProperty("record_union_base64").GetString(),Convert.ToBase64String(new U.Cursor(e.Record.Context.Common,r.Calculated!,100).Read(WorldNormalFixture.Frame+0x80,53,0,"visible union")));
        Assert.Equal(x.GetProperty("source_sync_hex").GetString(),Convert.ToHexStringLower(r.Completion.Outbox[0].NativePacket.AsSpan()));
        Assert.Equal(new[]{"SourceNpcSync","UserSelfHP","UserHPStatSync"},r.Completion.Outbox.Select(a=>a.Kind));
        Assert.Equal(hp.Owner.Calculated.Receiver.Hp778,r.Completion.Outbox[1].Current);Assert.Equal(hp.EffectiveMaxima.CurrentHp,r.Completion.Outbox[1].Maximum);
        Assert.Equal(x.GetProperty("rng_after").GetProperty("sha256").GetString(),r.RandomStateAfter!.Sha256);
        Assert.Equal(Convert.FromBase64String(x.GetProperty("rng_after").GetProperty("base64").GetString()!),r.RandomStateAfter.Bytes.ToArray());
        Assert.Equal(9ul,r.RandomGenerationAfter);Assert.Equal(new[]{"Base","Critical"},r.RandomCalls.Select(a=>a.Stage));
        Assert.Equal(0ul,WorldNormalFixture.U64(r.Calculated!,WorldNormalFixture.Npc+0x13d4));
        Assert.Equal(0ul,WorldNormalFixture.U64(r.Calculated!,WorldNormalFixture.User+0x13d4));
        Assert.Equal(0,WorldNormalFixture.I32(e.Record.Memory,WorldNormalFixture.User+0x728));
        Assert.False(r.CurrentActorObserved);Assert.False(r.HostEffectsExecuted);Assert.False(r.ActivationReady);Assert.Null(r.FinalHpAfterDeferredLife);
        Assert.Equal(e.Record.Context.DatasetId,hp.Owner.Source.DatasetSha256);Assert.NotEqual(N.Dataset,hp.Owner.Source.DatasetSha256);
    }
    [Fact] public void Charge_refresh_changes_maximum_after_core_and_before_unconditional_self_HP()
    {
        var f=new WorldNormalFixture();var r=Good(f);var hp=r.Completion!.Hp!;
        Assert.Equal(100,hp.Owner.ApplicationCell);Assert.Equal(100,hp.CoreActualHpLoss);Assert.Equal(-18,hp.GearHpDelta);Assert.Equal(118,hp.TotalImmediateHpLoss);
        Assert.Equal(7238,hp.EffectiveMaxima!.AuditInitialHp);Assert.Equal(7211,hp.EffectiveMaxima.CurrentHp);Assert.Equal(4882,r.Completion.Outbox[1].Current);
        var before=Assert.Single(hp.Owner.Events.OfType<H.SelfPacket>());Assert.Equal(4900,before.Current);Assert.Equal(7238,before.Maximum);
        var broadcast=Assert.Single(hp.Owner.Events.OfType<N.WorldHp>());Assert.Equal(0,broadcast.Delta);Assert.Equal(5,broadcast.ChangeType);
    }
    [Fact] public void Lethal_preview_produces_pending_before_User_entry_and_preserves_prior_first_kind()
    {
        var r=Good(new("whole150_lethal_preview"));var hp=r.Completion!.Hp!;
        Assert.Equal(150,r.Totals!.Hp);Assert.Equal(100,hp.Owner.ApplicationCell);Assert.Equal(0,hp.Owner.Calculated!.Receiver.Hp778);
        Assert.Equal(1,hp.Owner.Calculated.Receiver.Pending728);Assert.Equal(77,hp.Owner.Calculated.User.FirstDeathKind5b24);
        Assert.Equal(0x80001002u,hp.Owner.Calculated.Receiver.Killer918);
        Assert.Equal(0x10000000ul,WorldNormalFixture.U64(r.Calculated!,WorldNormalFixture.User+0x918));
        Assert.Single(hp.Owner.Events.OfType<H.UserDeathTimer>());
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)]
    public void Exact_World_target_predicates_return_native_prepared_rejection(int guard)
    {
        var f=new WorldNormalFixture();ulong a=WorldNormalFixture.User;
        switch(guard){case 0:f.Raw.W8(a+0x731c,1);f.Hp=f.Hp with{Receiver=f.Hp.Receiver with{Raw=f.Hp.Receiver.Raw with{Protection731c=1}}};break;
        case 1:f.Raw.W32(a+0x43a4,7);f.Hp=f.Hp with{Receiver=f.Hp.Receiver with{Raw=f.Hp.Receiver.Raw with{World43a4=7}}};break;
        case 2:f.Raw.W32(a+0x13c,2);f.Hp=f.Hp with{Receiver=f.Hp.Receiver with{Raw=f.Hp.Receiver.Raw with{Pose13c=2}}};break;
        case 3:f.Raw.W32(a+0x141c,1);f.Hp=f.Hp with{Receiver=f.Hp.Receiver with{Raw=f.Hp.Receiver.Raw with{Invincibility141c=1}}};break;
        case 4:f.Raw.W32(WorldNormalFixture.Npc+0x1250,4);break;
        case 5:f.Raw.W32(0x145a2d2c0,1);break;}
        var e=f.Entry();e=W.Entry.Declare(e.Record,e.HpCurrent,e.Gear,e.World,e.Clocks.Take(2).ToImmutableArray(),e.Budget,50,7,e.Receipt);
        var r=O.EvaluateWorld(e);Assert.Equal(O.Status.NativeTargetRejectedPrepared,r.Status);Assert.NotNull(r.Calculated);Assert.Empty(r.RandomCalls);Assert.Null(r.Emission);
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)]
    public void Late_active_gameplay_and_unknown_fields_withhold_whole_outputs(int child)
    {
        var f=new WorldNormalFixture();switch(child)
        {case 0:f.Raw.W32(WorldNormalFixture.Npc+0x6c8,1);break;
        case 1:f.Raw.W32(WorldNormalFixture.Manager+0x43c4,1);break;
        case 2:f.Raw.W32(WorldNormalFixture.User+0x7bf0,1);f.Hp=f.Hp with{Receiver=f.Hp.Receiver with{Raw=f.Hp.Receiver.Raw with{Spectator7bf0=1}}};break;
        case 3:f.Raw.W32(WorldNormalFixture.User+0x595c,1);f.Hp=f.Hp with{Receiver=f.Hp.Receiver with{Raw=f.Hp.Receiver.Raw with{Recall595c=1}}};break;
        case 4:f.Raw.Forget(WorldNormalFixture.User+0x4238,4);break;
        case 5:f.Raw.W64(WorldNormalFixture.User+0x168,1);f.Hp=f.Hp with{Guards=f.Hp.Guards with{Bound168=f.Hp.Guards.Bound168 with{Pointer=1}}};break;}
        var r=O.EvaluateWorld(f.Entry());Assert.Null(r.Calculated);Assert.Null(r.Completion);Assert.Null(r.Emission);Assert.Null(r.Retained);Assert.NotNull(r.Failure);Assert.Empty(r.RandomCalls);
        Assert.Equal(5000,f.Hp.Receiver.State.Hp778);
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)]
    public void Bounded_late_failure_never_exposes_partial_HP_or_packet(int budget)
    {
        var f=new WorldNormalFixture();f.Limits=budget switch{0=>f.Limits with{MaxSteps=20},1=>f.Limits with{MaxRandomOperations=1},2=>f.Limits with{MaxRawDraws=0},_=>f.Limits with{MaxRandomTraceBytes=1}};
        var r=O.EvaluateWorld(f.Entry());Assert.Null(r.Calculated);Assert.Null(r.Emission);Assert.NotNull(r.Failure);
    }
    [Fact] public void Original_clock_tail_and_role_mismatch_are_failures_not_reordered_samples()
    {
        var f=new WorldNormalFixture();var e=f.Entry();var tail=e.Clocks.Add(f.Clock(W.Phase.Hp,"PostHit",0,0x140541fda,WorldNormalFixture.User));
        var bad=W.Entry.Declare(e.Record,e.HpCurrent,e.Gear,e.World,tail,e.Budget,50,7,e.Receipt);Assert.Null(O.EvaluateWorld(bad).Calculated);
        bad=W.Entry.Declare(e.Record,e.HpCurrent,e.Gear,e.World,e.Clocks.SetItem(0,e.Clocks[1]),e.Budget,50,7,e.Receipt);Assert.Null(O.EvaluateWorld(bad).Calculated);
    }
    [Fact] public void Sphere_integer_inclusion_precedes_connection_and_preserves_cell_list_order()
    {
        var f=new WorldNormalFixture();var near=Observer(f,0xa0000000,700,WorldNormalFixture.Bits(110.00000762939453f),0);
        var outside=Observer(f,0xb0000000,701,WorldNormalFixture.Bits(110.01f),1);
        f.Raw.Forget(outside.Address+0x3720,8);f.Roots=[outside,near,f.Root()];
        var r=Good(f);Assert.Equal(new uint[]{0x80001001u,700},r.Emission!.Recipients.Select(x=>x.ObjectId));
        Assert.DoesNotContain(r.Steps,x=>x.Address==outside.Address+0x3720);
        Assert.Contains(r.Steps,x=>x.Address==near.Address+0x3720);
    }
    [Fact] public void Disconnected_in_sphere_User_is_skipped_after_pointer_read()
    {
        var f=new WorldNormalFixture();var observer=Observer(f,0xa0000000,700,WorldNormalFixture.Bits(100),0) with{ConnectionAddress=0};
        f.Raw.W64(observer.Address+0x3720,0);f.Roots=[observer,f.Root()];var r=Good(f);
        Assert.Single(r.Emission!.Recipients);Assert.Contains(r.Steps,x=>x.Address==observer.Address+0x3720);
    }
    private static W.UserRoot Observer(WorldNormalFixture f,ulong address,uint id,uint x,int ordinal)
    {
        f.Raw.Add(address,0x4000,"observer-"+id,"observer-life-"+id);f.Raw.W64(address,U.UserVtable);f.Raw.W32(address+0x144,1);
        f.Raw.W32(address+0x70,id);f.Raw.W64(address+0x118,WorldNormalFixture.World);f.Raw.W32(address+0x120,x);
        f.Raw.W32(address+0x124,WorldNormalFixture.Bits(20));f.Raw.W32(address+0x128,WorldNormalFixture.Bits(30));f.Raw.W64(address+0x3720,WorldNormalFixture.Connection);
        return new(address,"observer-life-"+id,3,0,ordinal,WorldNormalFixture.Connection,"connection-generation","observer current connection");
    }
    [Fact] public void Whole_zero_record_preserves_threat2_and_unconditional_SendHP_without_burning_gear()
    {
        var f=new WorldNormalFixture();f.Raw.W32(WorldNormalFixture.User+0x13d0,0);f.Raw.W16(WorldNormalFixture.User+0x14ae,1000);f.Raw.W16(WorldNormalFixture.Npc+0x14aa,0);
        f.Raw.Clocks=f.Raw.Clocks.Add(f.Raw.Clock(C.ClockRole.DodgeCounter,0x1400b4640,0x1400b4650,1000));
        var e=f.Entry();e=W.Entry.Declare(e.Record,e.HpCurrent,e.Gear,e.World,e.Clocks.Where(x=>x.Phase!=W.Phase.Hp).ToImmutableArray(),e.Budget,50,7,e.Receipt);
        var r=O.EvaluateWorld(e);Assert.Equal(O.Status.CalculatedImmediateWorld,r.Status);Assert.Equal(0,r.Totals!.Hp);Assert.Equal(2,r.Totals.Threat);
        Assert.Equal(0,r.Completion!.Hp!.Owner.ApplicationCell);Assert.False(r.Completion.Hp.Owner.CoreReturn);Assert.Null(r.Completion.Hp.OwnedGear);
        Assert.Equal(5000,r.Completion.Outbox[1].Current);Assert.Equal(7238,r.Completion.Outbox[1].Maximum);
        Assert.Equal(new[]{"Base","Dodge"},r.RandomCalls.Select(x=>x.Stage));Assert.Equal(11,r.RandomCalls[1].ComputedReturn);Assert.Equal(6000,WorldNormalFixture.I32(r.Calculated!,WorldNormalFixture.User+0x13e4));
    }
    [Fact] public void Whole_true_rejection_retains_rejected_then_accepted_engine_draw_order()
    {
        var f=new WorldNormalFixture("whole100_true_rare_rejection");var r=Good(f);var critical=r.RandomCalls[1];
        Assert.Equal(2,critical.RawDraws.Length);Assert.Equal(RetailRandomDrawDisposition.Rejected,critical.RawDraws[0].Disposition);
        Assert.Equal(RetailRandomDrawDisposition.Accepted,critical.RawDraws[1].Disposition);Assert.Equal(ulong.MaxValue,critical.RawDraws[0].Value);
        Assert.Equal(11,critical.ComputedReturn);Assert.Equal(4882,r.Completion!.Hp!.Owner.Calculated!.Receiver.Hp778);
    }
    [Theory][InlineData("roll312_on_first_base",RetailRandomRegeneration.WriteSecond312)][InlineData("roll624_on_first_base",RetailRandomRegeneration.WriteFirst312AndReset)]
    public void Whole_RNG_rollover_is_computed_from_the_same_current_engine(string name,RetailRandomRegeneration stage)
    {
        var f=new WorldNormalFixture(name);var r=Good(f);Assert.Equal(stage,r.RandomCalls[0].RawDraws[0].Regeneration);
        Assert.Equal(f.Row.GetProperty("expected").GetProperty("rng_after").GetProperty("sha256").GetString(),r.RandomStateAfter!.Sha256);
    }
    [Fact] public void Whole_shield_uses_shared_next_draw_preserves_raw_threat_and_then_owned_HP()
    {
        var f=new WorldNormalFixture();ulong row=WorldNormalFixture.Manager+(1ul+0x3b5)*12;
        f.Raw.W32(WorldNormalFixture.Manager+0x2b18,0x40);f.Raw.W32(row,1000);f.Raw.W32(row+4,60);f.Raw.W32(WorldNormalFixture.Manager+0x2cac,1000);
        f.Hp=f.Hp with{Guards=f.Hp.Guards with{Manager=f.Hp.Guards.Manager with{Flags2b18=0x40}}};
        var r=Good(f);Assert.Equal(40,r.Totals!.Hp);Assert.Equal(1002,r.Totals.Threat);Assert.Equal(40,r.Completion!.Hp!.Owner.ApplicationCell);
        Assert.Equal(40,r.Completion.Hp.CoreActualHpLoss);Assert.Equal(4941,r.Completion.Hp.Owner.Calculated!.Receiver.Hp778);Assert.Equal(59,r.Completion.Hp.TotalImmediateHpLoss);
        Assert.Equal(new[]{"Base","Critical","Shield"},r.RandomCalls.Select(x=>x.Stage));Assert.Equal(500,r.RandomCalls[2].ComputedReturn);Assert.Equal(10ul,r.RandomGenerationAfter);
        Assert.Equal(940,WorldNormalFixture.I32(r.Calculated!,WorldNormalFixture.Manager+0x2cac));
        Assert.Equal((byte)2,r.Emission!.Bytes[^2]);Assert.Equal(7211,r.Completion.Outbox[1].Maximum);
    }
    [Fact] public void Whole_User_HP_transport_must_join_the_same_owned_current_connection()
    {
        var f=new WorldNormalFixture();f.Roots=[f.Root() with{ConnectionReceipt="another current connection"}];var r=O.EvaluateWorld(f.Entry());
        Assert.Null(r.Calculated);Assert.Null(r.Emission);Assert.Equal("HP current connection",r.Failure!.Operand);
    }

    [Fact] public void Whole_MP_shield_composes_shared_third_draw_exact_cells_gear_and_final_transport()
    {
        // Independently derived full-state receipt56a4a2ed...: dyadic percentages,
        // both independent RNG models, and the original source-bound gear graph.
        var f=new WorldNormalFixture();f.MpShield();var e=f.Entry();var r=O.EvaluateWorld(e);
        Assert.Equal(O.Status.CalculatedImmediateWorld,r.Status);Assert.Null(r.Failure);Assert.NotNull(r.Calculated);
        Assert.Equal(70,r.Totals!.Hp);Assert.Equal(15,r.Totals.Mp);Assert.Equal(1002,r.Totals.Threat);
        var hp=r.Completion!.Hp!;Assert.Equal(70,hp.Owner.ApplicationCell);Assert.Equal(70,hp.CoreActualHpLoss);Assert.Equal(15,hp.CoreActualMpLoss);
        Assert.Equal(4912,hp.Owner.Calculated!.Receiver.Hp778);Assert.Equal(985,hp.Owner.Calculated.Receiver.Mp790);Assert.Equal(88,hp.TotalImmediateHpLoss);
        Assert.Equal(7211,hp.Owner.Calculated.Receiver.MaxHp774);Assert.Equal(3965,hp.Owner.Calculated.Receiver.MaxMp78c);
        Assert.Equal(940,WorldNormalFixture.I32(r.Calculated!,WorldNormalFixture.Manager+0x2d00));
        Assert.Equal(new[]{"Base","Critical","MpShield"},r.RandomCalls.Select(x=>x.Stage));Assert.Equal(500,r.RandomCalls[2].ComputedReturn);Assert.Equal(10ul,r.RandomGenerationAfter);
        Assert.Equal("c1ccbafcfae4630cada372b26086526749c1f3e5f1bc1e27b9b03e30e5ab7e6a",r.RandomStateAfter!.Sha256);
        Assert.Equal(Convert.FromBase64String(MpExpectedStateBase64),r.RandomStateAfter.Bytes.ToArray());
        Assert.Equal(4912,r.Completion.Outbox[1].Current);Assert.Equal(7211,r.Completion.Outbox[1].Maximum);
        Assert.Equal(62,r.Emission!.Bytes.Length);Assert.Equal((byte)0x10,r.Emission.Bytes[32]);
        Assert.Equal((byte)0,r.Emission.Bytes[^2]); // Protection union tail, not the flag.
        Assert.Equal(Convert.FromHexString("3e00de015621fe0210008000320000070110008044640000000001460000000a1000000000000000000000000000000000000000000f000000d204000000"),r.Emission.Bytes.ToArray());
        Assert.Equal(1000,WorldNormalFixture.I32(e.Record.Memory,WorldNormalFixture.Manager+0x2d00));
    }
    [Theory][InlineData(0)][InlineData(1)]
    public void Whole_MP_unknown_late_percentages_withhold_every_candidate_despite_private_budget_prefix(int unknown)
    {
        var f=new WorldNormalFixture();f.MpShield();ulong row=WorldNormalFixture.Manager+(1ul+0x23c)*20;
        f.Raw.Forget(unknown==0?row+16:WorldNormalFixture.Manager+0x2d04,4);var r=O.EvaluateWorld(f.Entry());
        Assert.Null(r.Calculated);Assert.Null(r.Completion);Assert.Null(r.Emission);Assert.Empty(r.Steps);Assert.Empty(r.RandomCalls);
        Assert.Equal(1000,WorldNormalFixture.I32(f.Record.Memory,WorldNormalFixture.Manager+0x2d00));
    }
    [Theory][InlineData(60,60)][InlineData(1000,2000)]
    public void Whole_MP_exhaustion_or_current_MP_boundary_does_not_publish_an_End_result(int amount,int budget)
    {
        var f=new WorldNormalFixture();f.MpShield(amount,budget);
        if(amount==1000)
        {
            f.Raw.W32(WorldNormalFixture.User+0x790,100);
            f.Hp=f.Hp with{Receiver=f.Hp.Receiver with{State=f.Hp.Receiver.State with{Mp790=100}}};
            f.Gear=f.Gear with{Life=f.Gear.Life with{Mp=100}};
        }
        var r=O.EvaluateWorld(f.Entry());
        Assert.Null(r.Calculated);Assert.Null(r.Completion);Assert.Null(r.Emission);Assert.Equal("mp_shield.End",r.Failure!.Operand);
        Assert.Equal(budget,WorldNormalFixture.I32(f.Record.Memory,WorldNormalFixture.Manager+0x2d00));
    }
    [Theory][InlineData((ushort)50)][InlineData((ushort)51)]
    public void Immediate_and_retained_creation_clear_User_pair_before_Source_pair(ushort duration)
    {
        var f=new WorldNormalFixture();var r=O.EvaluateWorld(f.Entry(duration));Assert.Null(r.Failure);Assert.NotNull(r.Calculated);
        var steps=r.Steps.Where(x=>x.Kind=="Write"&&x.Operand.EndsWith(".attack_pair.cleanup",StringComparison.Ordinal)).ToArray();
        Assert.Equal(new[]{"User.attack_pair.cleanup","Npc.attack_pair.cleanup"},steps.Select(x=>x.Operand));
        Assert.Equal(new ulong[]{0x140690166,0x140690171},steps.Select(x=>x.Site));
        Assert.True(steps[0].Sequence<steps[1].Sequence);Assert.Equal(WorldNormalFixture.User+0x13d4,steps[0].Address);Assert.Equal(WorldNormalFixture.Npc+0x13d4,steps[1].Address);
    }
    [Fact] public void Original_lethal_raw_bank_overlay_changes_only_telemetry_arguments()
    {
        var f=new WorldNormalFixture("whole150_lethal_preview");var r=Good(f);var events=r.Completion!.Hp!.Owner.Events.OfType<H.UserDeathTelemetry>().ToArray();
        Assert.Equal(new uint[]{0x8b,0x8a},events.Select(x=>x.EventId));
        Assert.All(events,x=>{Assert.Equal(0u,x.User.Log7254);Assert.Equal(0,x.User.Character3a70);Assert.Equal("",x.User.Label70da);
            Assert.Equal("",x.User.Account70bc);Assert.Equal(0,x.User.Field584c);Assert.Equal(0,x.User.Field79c);});
        Assert.Equal(320050000u,events[1].NpcMapId20);Assert.Equal(0,events[0].NativeTail[2]);Assert.Equal(0,events[0].NativeTail[3]);
        Assert.Equal(100,r.Completion.Hp.Owner.ApplicationCell);Assert.Equal(77,r.Completion.Hp.Owner.Calculated!.User.FirstDeathKind5b24);
        Assert.Equal(f.Row.GetProperty("expected").GetProperty("normal_packet_hex").GetString(),Convert.ToHexStringLower(r.Emission!.Bytes.AsSpan()));
        Assert.Equal(f.Row.GetProperty("expected").GetProperty("rng_after").GetProperty("sha256").GetString(),r.RandomStateAfter!.Sha256);
    }

    private const string MpExpectedStateBase64="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";

    private static O.Result Good(WorldNormalFixture f){var r=O.EvaluateWorld(f.Entry());Assert.Equal(O.Status.CalculatedImmediateWorld,r.Status);Assert.Null(r.Failure);return r;}
}
