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

/// <summary>Independent signed products, truncation, exact squared geometry and computed RNG.
/// These test the real raw-manager helper; a helper result alone never admits whole World HP.</summary>
public sealed class RetailNpcUserNormalProtectionTests
{
    [Theory]
    [InlineData(100,60,0,1000,11,40,940,2)]
    [InlineData(99,50,1,1000,11,50,951,2)]
    [InlineData(100,60,0,1000,10,100,1000,0)]
    [InlineData(2,int.MaxValue,1,1000,11,2,1000,2)]
    public void Shield_uses_signed_wrapping_product_RNE_CVTT_and_inclusive_chance(int hp,int amount,int mode,int budget,int chance,int expectedHp,int remaining,int flags)
    {
        var f=new WorldNormalFixture();f.Raw.W32(WorldNormalFixture.Manager+0x2b18,0x40);ulong row=WorldNormalFixture.Manager+(1ul+0x3b5)*12;
        f.Raw.W32(row,(uint)chance);f.Raw.W32(row+4,unchecked((uint)amount));f.Raw.W32(row+8,(uint)mode);f.Raw.W32(WorldNormalFixture.Manager+0x2cac,(uint)budget);
        var w=Work(f);var r=P.Calculate(w,hp,1);Assert.Equal(expectedHp,r.Hp);Assert.Equal((byte)flags,r.Flags);Assert.Equal(remaining,w.Cursor.I32(WorldNormalFixture.Manager+0x2cac,0,"remaining"));
        Assert.Equal(11,Assert.Single(w.RandomCalls).ComputedReturn);Assert.Equal(8ul,w.RandomGeneration);
    }
    [Theory][InlineData(1)][InlineData(2)]
    public void MP_shield_computes_actual_User_percent_and_metadata_for_each_classification(int classification)
    {
        var f=new WorldNormalFixture();f.Raw.W32(WorldNormalFixture.Manager+0x2b18,0x800);ulong row=WorldNormalFixture.Manager+((ulong)classification+0x23c)*20;
        f.Raw.W32(row,11);f.Raw.W32(row+4,60);f.Raw.W32(row+8,0);f.Raw.W32(row+12,1234);f.Raw.W32(row+16,50);
        f.Raw.W32(WorldNormalFixture.Manager+0x2d00,1000);f.Raw.W32(WorldNormalFixture.Manager+0x2d04,25);
        var w=Work(f);var r=P.Calculate(w,100,classification);Assert.Equal(70,r.Hp);Assert.Equal(15,r.Mp);Assert.Equal((byte)0x10,r.Flags);
        Assert.Equal(940,w.Cursor.I32(WorldNormalFixture.Manager+0x2d00,0,"remaining"));Assert.Equal(15,w.Cursor.I32(w.Record+0x1c,0,"metadata"));Assert.Equal(1234,w.Cursor.I32(w.Record+0x20,0,"skill"));
    }
    [Theory][InlineData(0)][InlineData(1)]
    public void Exact_shield_exhaustion_and_current_MP_boundary_are_unclosed_End_children(int kind)
    {
        var f=new WorldNormalFixture();ulong row;
        if(kind==0){f.Raw.W32(WorldNormalFixture.Manager+0x2b18,0x40);row=WorldNormalFixture.Manager+(1ul+0x3b5)*12;f.Raw.W32(row,11);f.Raw.W32(row+4,60);f.Raw.W32(WorldNormalFixture.Manager+0x2cac,60);}
        else{f.Raw.W32(WorldNormalFixture.Manager+0x2b18,0x800);row=WorldNormalFixture.Manager+(1ul+0x23c)*20;f.Raw.W32(row,11);f.Raw.W32(row+4,1000);f.Raw.W32(row+16,100);f.Raw.W32(WorldNormalFixture.Manager+0x2d00,2000);f.Raw.W32(WorldNormalFixture.Manager+0x2d04,100);}
        var w=Work(f);var ex=Assert.Throws<U.Domain>(()=>P.Calculate(w,kind==0?100:1000,1));Assert.True(ex.Failure.Unsupported);Assert.Contains("End",ex.Failure.Operand);
        Assert.Equal(5000,f.Hp.Receiver.State.Hp778); // The private diagnostic prefix was never published or applied.
    }
    [Fact] public void Both_protectors_share_one_engine_successor_and_apply_in_native_order()
    {
        var f=new WorldNormalFixture();f.Raw.W32(WorldNormalFixture.Manager+0x2b18,0x840);ulong shield=WorldNormalFixture.Manager+(1ul+0x3b5)*12,mp=WorldNormalFixture.Manager+(1ul+0x23c)*20;
        f.Raw.W32(shield,1000);f.Raw.W32(shield+4,60);f.Raw.W32(WorldNormalFixture.Manager+0x2cac,1000);
        f.Raw.W32(mp,1000);f.Raw.W32(mp+4,20);f.Raw.W32(mp+16,100);f.Raw.W32(WorldNormalFixture.Manager+0x2d00,1000);f.Raw.W32(WorldNormalFixture.Manager+0x2d04,100);
        var w=Work(f);var r=P.Calculate(w,100,1);Assert.Equal(20,r.Hp);Assert.Equal(20,r.Mp);Assert.Equal((byte)0x12,r.Flags);
        Assert.Equal(new[]{"Shield","MpShield"},w.RandomCalls.Select(x=>x.Stage));Assert.Equal(9ul,w.RandomGeneration);
        Assert.Equal(w.RandomCalls[0].NextGeneration+1,w.RandomCalls[1].NextGeneration);
        Assert.Equal(w.RandomCalls[0].RawDraws[^1].IndexAfter,w.RandomCalls[1].RawDraws[0].IndexBefore);
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)]
    public void Reflection_exact_max_min_and_positive_child_boundaries(int branch)
    {
        var f=new WorldNormalFixture();f.Raw.W32(WorldNormalFixture.Manager+0x2b18,0x10);
        // Endpoints (10,20,30) and (31,1,2): 441+361+784=1586 exactly.
        f.Raw.W32(WorldNormalFixture.Manager+0x2c44,WorldNormalFixture.Bits(branch==0?1585:1586));
        f.Raw.W32(WorldNormalFixture.Manager+0x2c40,WorldNormalFixture.Bits(branch==1?1586:1585));
        f.Raw.W32(WorldNormalFixture.Manager+0x2c48,1000);f.Raw.W32(WorldNormalFixture.Manager+0x2c38,50);f.Raw.W32(WorldNormalFixture.Manager+0x2c3c,1);
        var w=Work(f);
        if(branch==2){var ex=Assert.Throws<U.Domain>(()=>P.Calculate(w,100,1));Assert.Equal("reflection.actual_User_to_Npc",ex.Failure.Operand);Assert.Single(w.RandomCalls);}
        else{var r=P.Calculate(w,100,1);Assert.Equal(0,r.Reflected);Assert.Empty(w.RandomCalls);}
    }
    [Theory][InlineData(1u,1)][InlineData(2u,2)][InlineData(4u,1)][InlineData(8u,2)]
    public void Redirect_and_conversion_chance_miss_is_computed_but_success_stops_at_actual_child(uint mask,int classification)
    {
        var f=new WorldNormalFixture();f.Raw.W32(WorldNormalFixture.Manager+0x2b18,mask);bool redirect=mask<=2;
        ulong chance=redirect?WorldNormalFixture.Manager+0x2b24+(ulong)classification*0x1c:WorldNormalFixture.Manager+0x2ba8+(ulong)classification*0x24;
        if(redirect)f.Raw.W32(chance-4,77);f.Raw.W32(chance,10);var w=Work(f);Assert.Equal(100,P.Calculate(w,100,classification).Hp);Assert.Single(w.RandomCalls);
        f.Raw.W32(chance,11);var ex=Assert.Throws<U.Domain>(()=>P.Calculate(Work(f),100,classification));Assert.True(ex.Failure.Unsupported);
    }
    [Fact] public void Missing_current_geometry_is_not_a_zero_reflection_answer()
    {
        var f=new WorldNormalFixture();f.Raw.W32(WorldNormalFixture.Manager+0x2b18,0x10);f.Raw.W32(WorldNormalFixture.Manager+0x2c44,WorldNormalFixture.Bits(2000));f.Raw.Forget(WorldNormalFixture.Npc+0x128,4);
        Assert.Throws<U.Domain>(()=>P.Calculate(Work(f),100,1));
    }
    [Fact] public void Zero_incoming_damage_skips_unreached_manager_and_RNG_state()
    {
        var f=new WorldNormalFixture();f.Raw.Forget(WorldNormalFixture.Manager+0x2b18,4);var w=Work(f);var r=P.Calculate(w,0,1);Assert.Equal(0,r.Hp);Assert.Empty(w.RandomCalls);
    }

    [Theory][InlineData(0)][InlineData(1)]
    public void MP_mode_is_consumed_before_the_selected_flat_or_percent_amount(int mode)
    {
        var f=new WorldNormalFixture();f.MpShield(mode:mode);var w=Work(f);_ = P.Calculate(w,100,1);
        var modeRead=Assert.Single(w.Steps,x=>x.Operand=="mp_shield.mode");var amount=Assert.Single(w.Steps,x=>x.Operand=="mp_shield.amount");
        Assert.Equal(0x14034ccc0ul,modeRead.Site);Assert.Equal(mode==0?0x14034cce9ul:0x14034ccc8ul,amount.Site);Assert.True(modeRead.Sequence<amount.Sequence);
    }
    [Fact] public void Unknown_MP_mode_rejects_before_an_also_unknown_amount()
    {
        var f=new WorldNormalFixture();f.MpShield();ulong row=WorldNormalFixture.Manager+(1ul+0x23c)*20;f.Raw.Forget(row+8,4);f.Raw.Forget(row+4,4);var w=Work(f);
        var ex=Assert.Throws<U.Domain>(()=>P.Calculate(w,100,1));Assert.Equal("mp_shield.mode",ex.Failure.Operand);
        Assert.DoesNotContain(w.Steps,x=>x.Operand=="mp_shield.amount");
    }
    [Theory][InlineData(0)][InlineData(1)]
    public void Nondepleting_MP_budget_write_precedes_each_late_percentage_read_and_survives_only_privately_on_failure(int unknown)
    {
        var f=new WorldNormalFixture();f.MpShield();ulong row=WorldNormalFixture.Manager+(1ul+0x23c)*20;
        f.Raw.Forget(unknown==0?row+16:WorldNormalFixture.Manager+0x2d04,4);var w=Work(f);
        Assert.Throws<U.Domain>(()=>P.Calculate(w,100,1));Assert.Equal(940,w.Cursor.I32(WorldNormalFixture.Manager+0x2d00,0,"private diagnostic budget"));
        var store=Assert.Single(w.Steps,x=>x.Kind=="Write"&&x.Operand=="mp_shield.remaining_budget");Assert.Equal(0x14034cd12ul,store.Site);
        Assert.Equal(1000,WorldNormalFixture.I32(f.Record.Memory,WorldNormalFixture.Manager+0x2d00));
    }
    [Fact] public void Successful_nondepleting_MP_helper_has_exact_budget_percent_End_and_metadata_order()
    {
        var f=new WorldNormalFixture();f.MpShield();var w=Work(f);_ = P.Calculate(w,100,1);
        string[] operands={"mp_shield.mode","mp_shield.amount","mp_shield.budget","mp_shield.remaining_budget",
            "User.mp_shield.hp_percent","mp_shield.mp_percent","mp_shield.remaining_budget_for_End",
            "record.mp_shield_metadata_amount","mp_shield.skill","record.mp_shield_metadata_skill"};
        ulong[] sites={0x14034ccc0,0x14034cce9,0x14034ccff,0x14034cd12,0x14034cd22,0x14034cd4a,0x14034cde6,0x14034ce07,0x14034ce0a,0x14034ce0f};
        var steps=operands.Select(name=>Assert.Single(w.Steps,x=>x.Operand==name)).ToArray();
        Assert.Equal(sites,steps.Select(x=>x.Site));Assert.True(steps.Zip(steps.Skip(1)).All(x=>x.First.Sequence<x.Second.Sequence));
    }
    [Theory][InlineData(0)][InlineData(1)]
    public void Depleting_MP_branch_keeps_old_budget_until_both_percentages_are_calculated(int unknown)
    {
        var f=new WorldNormalFixture();f.MpShield(budget:60);ulong row=WorldNormalFixture.Manager+(1ul+0x23c)*20;
        f.Raw.Forget(unknown==0?row+16:WorldNormalFixture.Manager+0x2d04,4);var w=Work(f);Assert.Throws<U.Domain>(()=>P.Calculate(w,100,1));
        Assert.Equal(60,w.Cursor.I32(WorldNormalFixture.Manager+0x2d00,0,"private old budget"));
        Assert.DoesNotContain(w.Steps,x=>x.Kind=="Write"&&x.Operand=="mp_shield.remaining_budget");
    }
    [Fact] public void Depleting_MP_branch_reads_old_budget_then_zeros_it_before_unsupported_End()
    {
        var f=new WorldNormalFixture();f.MpShield(budget:60);var w=Work(f);var ex=Assert.Throws<U.Domain>(()=>P.Calculate(w,100,1));
        Assert.Equal("mp_shield.End",ex.Failure.Operand);Assert.Equal(0,w.Cursor.I32(WorldNormalFixture.Manager+0x2d00,0,"private zero budget"));
        var ordered=new[]{"User.mp_shield.hp_percent","mp_shield.mp_percent","mp_shield.depleted_budget","mp_shield.remaining_budget","mp_shield.remaining_budget_for_End"}
            .Select(name=>Assert.Single(w.Steps,x=>x.Operand==name)).ToArray();
        Assert.Equal(new ulong[]{0x14034cd72,0x14034cd9a,0x14034cda0,0x14034cdbc,0x14034cde6},ordered.Select(x=>x.Site));
        Assert.True(ordered.Zip(ordered.Skip(1)).All(x=>x.First.Sequence<x.Second.Sequence));
        Assert.DoesNotContain(w.Steps,x=>x.Operand=="record.mp_shield_metadata_amount");
    }

    private static W.Work Work(WorldNormalFixture f){var w=new W.Work(f.Entry());w.Cursor.W8(w.Record+0x32,0,0,"constructed caller output zero");return w;}
}
