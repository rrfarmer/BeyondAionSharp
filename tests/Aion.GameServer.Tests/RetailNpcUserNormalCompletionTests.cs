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

public sealed class RetailNpcUserNormalCompletionTests
{
    [Theory][InlineData((ushort)0)][InlineData((ushort)49)][InlineData((ushort)50)]
    public void Duration_at_or_below50_completes_immediately(ushort duration)
    {
        var f=new WorldNormalFixture();var r=O.EvaluateWorld(f.Entry(duration));Assert.Equal(O.Status.CalculatedImmediateWorld,r.Status);
        Assert.NotNull(r.Completion);Assert.Null(r.Retained);Assert.Equal(4882,r.Completion!.Hp!.Owner.Calculated!.Receiver.Hp778);
    }
    [Theory][InlineData((ushort)51,1u)][InlineData((ushort)350,300u)][InlineData(ushort.MaxValue,65485u)]
    public void Ordinary_delay_preserves_relative_duration_minus50_and_defers_all_HP(ushort duration,uint delay)
    {
        var f=new WorldNormalFixture();var created=Create(f,duration);Assert.Equal(delay,created.Retained!.RelativeDelay);Assert.Equal(1000u+delay,created.Retained.Due);
        Assert.Null(created.Completion);Assert.Equal(5000,WorldNormalFixture.I32(created.Calculated!,WorldNormalFixture.User+0x778));
        Assert.Equal(0ul,WorldNormalFixture.U64(created.Calculated!,WorldNormalFixture.Npc+0x13d4));Assert.Equal(2,created.RandomCalls.Length);
        var done=F.DispatchImpact(created.Retained,f.FreshDispatch(created,1000+delay));Assert.True(done.Completed,done.Failure?.ToString());
        Assert.Equal(4882,done.Hp!.Owner.Calculated!.Receiver.Hp778);Assert.DoesNotContain(done.Steps,x=>x.Kind=="ComputedRng");
    }
    [Fact] public void Fresh_due_dispatch_uses_the_original_fixed_target_without_rerunning_outer_guards()
    {
        var f=new WorldNormalFixture();var created=Create(f,51);var current=f.FreshDispatch(created,1001,true);
        var c=new U.Cursor(current.Record.Context.Common,current.Record.Memory,100);c.W8(WorldNormalFixture.User+0x731c,1,0,"became protected after launch");
        var hp=current.HpCurrent with{Receiver=current.HpCurrent.Receiver with{Raw=current.HpCurrent.Receiver.Raw with{Protection731c=1}}};
        current=W.Entry.Declare(current.Record with{Memory=c.Snapshot()},hp,current.Gear,current.World,current.Clocks.Take(1).ToImmutableArray(),current.Budget,50,7,current.Receipt);
        // This current protection is consumed by the real User HP owner, rather than
        // being a fresh outer CanAttack rejection or fabricated completed damage.
        var done=F.DispatchImpact(created.Retained!,current);Assert.True(done.Completed,done.Failure?.ToString());
        Assert.Equal(5000,done.Hp!.Owner.Calculated!.Receiver.Hp778);Assert.Equal(100,done.Hp.Owner.ApplicationCell);Assert.Null(done.Hp.OwnedGear);
        Assert.Equal(0x12345678,WorldNormalFixture.I32(done.Calculated!,WorldNormalFixture.Npc+0x13d4));
        Assert.DoesNotContain(done.Steps,x=>x.Operand=="Npc.state13d0"||x.Operand=="Npc.position.start");
    }
    [Fact] public void Retarget_alone_does_not_cancel_a_retained_ordinary_hit()
    {
        var f=new WorldNormalFixture();var c=Create(f,51);var d=F.DispatchImpact(c.Retained!,f.FreshDispatch(c,1001,true));
        Assert.True(d.Completed,d.Failure?.ToString());Assert.Equal(4882,d.Hp!.Owner.Calculated!.Receiver.Hp778);
        Assert.Equal(0x12345678,WorldNormalFixture.I32(d.Calculated!,WorldNormalFixture.Npc+0x13d4));
    }
    [Fact] public void Retained_lethal_payload_keeps_produced_pending_and_first_kind_lineage()
    {
        var f=new WorldNormalFixture("whole150_lethal_preview");var c=Create(f,51);
        Assert.Equal(1,WorldNormalFixture.I32(c.Calculated!,WorldNormalFixture.User+0x728));Assert.Equal(100,WorldNormalFixture.I32(c.Calculated!,WorldNormalFixture.User+0x778));
        var d=F.DispatchImpact(c.Retained!,f.FreshDispatch(c,1001));Assert.True(d.Completed,d.Failure?.ToString());
        Assert.Equal(100,d.Hp!.Owner.ApplicationCell);Assert.Equal(77,d.Hp.Owner.Calculated!.User.FirstDeathKind5b24);Assert.Equal(0,d.Hp.Owner.Calculated.Receiver.Hp778);
    }
    [Fact] public void Not_due_is_an_adapter_rejection_and_does_not_consume_the_impact()
    {
        var f=new WorldNormalFixture();var c=Create(f,350);var early=F.DispatchImpact(c.Retained!,f.FreshDispatch(c,1299));
        Assert.False(early.Completed);Assert.Equal(F.Status.AdapterNotDue,early.Status);Assert.Equal("adapter-not-due",early.Failure!.Code);Assert.Null(early.Calculated);Assert.Empty(early.Outbox);
        Assert.True(F.DispatchImpact(c.Retained!,f.FreshDispatch(c,1300)).Completed);
    }
    [Fact] public void Successful_completion_consumes_once_and_duplicate_has_no_HP_or_RNG_output()
    {
        var f=new WorldNormalFixture();var c=Create(f,51);var current=f.FreshDispatch(c);Assert.True(F.DispatchImpact(c.Retained!,current).Completed);
        var duplicate=F.DispatchImpact(c.Retained!,current);Assert.Equal(F.Status.AlreadyConsumed,duplicate.Status);Assert.Null(duplicate.Calculated);Assert.Null(duplicate.Hp);Assert.Empty(duplicate.Outbox);Assert.Empty(duplicate.Clocks);
        Assert.Equal(F.Status.AlreadyConsumed,F.CancelImpact(c.Retained!,Cancel()).Status);
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)]
    public void Explicit_lifecycle_cancellation_consumes_exact_owned_lifetimes(int reason)
    {
        var f=new WorldNormalFixture();var c=Create(f,51);var cancelled=F.CancelImpact(c.Retained!,Cancel((F.CancelReason)reason));Assert.Equal(F.Status.Cancelled,cancelled.Status);
        Assert.Null(cancelled.Calculated);Assert.Empty(cancelled.Outbox);Assert.Equal(F.Status.AlreadyConsumed,F.DispatchImpact(c.Retained!,f.FreshDispatch(c)).Status);
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)]
    public void Wrong_cancel_owner_generation_or_undefined_reason_cannot_release_another_impact(int mismatch)
    {
        var f=new WorldNormalFixture();var c=Create(f,51);var v=Cancel();v=mismatch switch{0=>v with{Owner="another"},1=>v with{UserLife="respawned"},_=>v with{Reason=(F.CancelReason)99}};
        Assert.Equal(F.Status.AdapterInvalidated,F.CancelImpact(c.Retained!,v).Status);Assert.True(F.DispatchImpact(c.Retained!,f.FreshDispatch(c)).Completed);
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)]
    public void Stale_dispatch_context_and_stream_do_not_publish_or_consume_HP(int mismatch)
    {
        var f=new WorldNormalFixture();var c=Create(f,51);var e=f.FreshDispatch(c);var record=e.Record;
        if(mismatch==0)record=record with{Boundary=record.Boundary with{UserLife="respawned"}};
        if(mismatch==1)record=record with{Random=record.Random with{Ownership=record.Random.Ownership! with{StreamIdentity="another global stream"}}};
        if(mismatch==2)record=record with{Random=record.Random with{InitialGeneration=7,Ownership=record.Random.Ownership! with{InitialGeneration=7}}};
        e=W.Entry.Declare(record,e.HpCurrent,e.Gear,e.World,e.Clocks,e.Budget,50,7,e.Receipt);
        var bad=F.DispatchImpact(c.Retained!,e);Assert.False(bad.Completed);Assert.Null(bad.Calculated);Assert.Empty(bad.Outbox);
        Assert.True(F.DispatchImpact(c.Retained!,f.FreshDispatch(c)).Completed);
    }
    [Fact] public void Active_late_proc_fails_the_whole_completion_and_releases_only_the_local_claim()
    {
        var f=new WorldNormalFixture();var created=Create(f,51);var e=f.FreshDispatch(created);var cursor=new U.Cursor(e.Record.Context.Common,e.Record.Memory,100);
        cursor.W32(WorldNormalFixture.Npc+0x6c8,1,0,"new active item proc");
        e=W.Entry.Declare(e.Record with{Memory=cursor.Snapshot()},e.HpCurrent,e.Gear,e.World,e.Clocks,e.Budget,50,7,e.Receipt);
        var fail=F.DispatchImpact(created.Retained!,e);Assert.False(fail.Completed);Assert.Null(fail.Calculated);Assert.Null(fail.Hp);Assert.Empty(fail.Outbox);
        Assert.True(F.DispatchImpact(created.Retained!,f.FreshDispatch(created)).Completed);
    }

    [Theory][InlineData(0)][InlineData(1)]
    public void Retained_lethal_dispatch_joins_fresh_SourceWorld_and_log_then_releases_only_failed_local_claim(int mismatch)
    {
        var f=new WorldNormalFixture("whole150_lethal_preview");var c=Create(f,51);var good=f.FreshDispatch(c);var hp=good.HpCurrent;
        hp=mismatch==0?hp with{SourceWorld=hp.SourceWorld with{MapId20=320050001}}:
            hp with{Receiver=hp.Receiver with{Log=hp.Receiver.Log with{Log7254=777}}};
        var bad=W.Entry.Declare(good.Record,hp,good.Gear,good.World,good.Clocks,good.Budget,50,7,good.Receipt);
        var failed=F.DispatchImpact(c.Retained!,bad);Assert.False(failed.Completed);Assert.Null(failed.Hp);Assert.Null(failed.Calculated);Assert.Empty(failed.Outbox);
        var done=F.DispatchImpact(c.Retained!,f.FreshDispatch(c));Assert.True(done.Completed,done.Failure?.ToString());
        Assert.All(done.Hp!.Owner.Events.OfType<H.UserDeathTelemetry>(),x=>{Assert.Equal(0u,x.User.Log7254);Assert.Equal("",x.User.Label70da);Assert.Equal("",x.User.Account70bc);});
        Assert.Equal(77,done.Hp.Owner.Calculated!.User.FirstDeathKind5b24);
    }
    [Fact] public void Retained_creation_skips_lethal_tail_fields_but_due_completion_requires_their_current_values()
    {
        var f=new WorldNormalFixture("whole150_lethal_preview");var correct=f.Hp;
        f.Hp=f.Hp with{SourceWorld=f.Hp.SourceWorld with{Presence=H.Presence.Unknown},Receiver=f.Hp.Receiver with{Log=f.Hp.Receiver.Log with{Label70da="not current"}}};
        var c=Create(f,51);Assert.DoesNotContain(c.Steps,x=>x.Operand.StartsWith("telemetry.",StringComparison.Ordinal));
        var failed=F.DispatchImpact(c.Retained!,f.FreshDispatch(c));Assert.False(failed.Completed);Assert.Null(failed.Calculated);
        f.Hp=correct;Assert.True(F.DispatchImpact(c.Retained!,f.FreshDispatch(c)).Completed);
    }
    [Fact] public void Delayed_dispatch_never_repeats_launch_cleanup_on_a_new_retarget()
    {
        var f=new WorldNormalFixture();var c=Create(f,51);var done=F.DispatchImpact(c.Retained!,f.FreshDispatch(c,1001,true));Assert.True(done.Completed);
        Assert.DoesNotContain(done.Steps,x=>x.Operand.EndsWith(".attack_pair.cleanup",StringComparison.Ordinal));
        Assert.Equal(0x12345678,WorldNormalFixture.I32(done.Calculated!,WorldNormalFixture.Npc+0x13d4));
    }

    private static F.Cancellation Cancel(F.CancelReason reason=F.CancelReason.Death)=>new("serialized-world","world-generation","source-life","user-life",reason);
    private static O.Result Create(WorldNormalFixture f,ushort duration){var c=O.EvaluateWorld(f.Entry(duration));Assert.Equal(O.Status.CalculatedRetainedImpact,c.Status);Assert.NotNull(c.Retained);return c;}
}
