using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcCurseStatMutationTests
{
    private sealed class CallbackStat : StatSetFunction
    {
        internal Action? Callback;
        internal CallbackStat() : base(StatEnum.MAXHP,1000) { }
        public override int GetValue()
        {
            var action=Callback;Callback=null;action?.Invoke();
            return base.GetValue();
        }
    }

    [Fact]
    public void CurseStatObservationRejectsAnAddRemoveDuringTheRead()
    {
        using var h=BossAiHarness.For(220030000).Build();
        var player=h.SpawnPlayer();var stats=player.GetGameStats();
        var curse=new Effect(player,player,new SkillTemplate {skillId=910101},1);
        var transient=new Effect(player,player,new SkillTemplate {skillId=910102},1);
        var trigger=new CallbackStat();stats.AddEffectOnly(null!,[trigger]);
        var checks=RetailNpcCurseStatWitness.Create(player,curse,_=>true,0,0);
        Assert.True(checks.Before(player));
        stats.AddEffectOnly(curse,[]);
        Assert.False(checks.Before(player)); // Arbitrary same-owner writes are not the prepared pair.
        checks=RetailNpcCurseStatWitness.Create(player,curse,_=>true,0,0);
        Assert.True(checks.Before(player));
        stats.AddEffectOnly(curse,[new StatAddFunction(StatEnum.MAXMP,1,false)]);
        stats.EndEffect(curse);
        Assert.False(checks.Before(player)); // Same owner cannot hide an add/remove between checks.
        checks=RetailNpcCurseStatWitness.Create(player,curse,_=>true,0,0);
        Assert.True(checks.Before(player));
        trigger.Callback=()=>
        {
            stats.AddEffectOnly(transient,[new StatAddFunction(StatEnum.MAXMP,1,false)]);
            stats.EndEffect(transient);
        };
        Assert.False(checks.Before(player));
        Assert.False(checks.Before(player)); // Restoring the list does not erase the other owner's writes.
        var fresh=RetailNpcCurseStatWitness.Create(player,curse,_=>true,0,0);
        Assert.True(fresh.Before(player));
        stats.AddEffectOnly(transient,[new StatAddFunction(StatEnum.MAXMP,1,false)]);
        stats.EndEffect(transient);
        Assert.False(fresh.Before(player)); // Also catches a complete change between observations.
    }

    [Fact]
    public void MutationStampRejectsAnActiveWriterAndReleasesOnFailure()
    {
        using var h=BossAiHarness.For(220030000).Build();
        var stats=h.SpawnPlayer().GetGameStats();
        using var entered=new ManualResetEventSlim();
        using var release=new ManualResetEventSlim();
        using var finished=new ManualResetEventSlim();
        Exception? failure=null;
        IEnumerable<IStatFunction> Blocking()
        {
            entered.Set();release.Wait();
            throw new InvalidOperationException("fixture failure");
#pragma warning disable CS0162
            yield break;
#pragma warning restore CS0162
        }
        var writer=new Thread(()=>
        {
            try { stats.AddEffectOnly(null!,Blocking()); }
            catch(Exception ex) { failure=ex; }
            finally { finished.Set(); }
        });
        Assert.True(stats.TryCaptureRetailStatMutation(out long before));
        writer.Start();
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            Assert.False(stats.TryCaptureRetailStatMutation(out _));
            Assert.False(stats.RetailStatMutationUnchanged(before));
        }
        finally { release.Set(); }
        Assert.True(finished.Wait(TimeSpan.FromSeconds(5)));
        Assert.IsType<InvalidOperationException>(failure);
        Assert.True(stats.TryCaptureRetailStatMutation(out long after));
        Assert.NotEqual(before,after);
        Assert.True(stats.RetailStatMutationUnchanged(after));
    }

    [Fact]
    public void ResourceReservationExcludesWritersAndRejectsOldStamps()
    {
        using var h=BossAiHarness.For(220030000).Build();
        var stats=h.SpawnPlayer().GetGameStats();
        using var started=new ManualResetEventSlim();
        using var finished=new ManualResetEventSlim();
        Exception? failure=null;
        var writer=new Thread(()=>
        {
            started.Set();
            try { stats.AddEffectOnly(null!,[]); }
            catch(Exception ex) { failure=ex; }
            finally { finished.Set(); }
        });
        Assert.True(stats.TryCaptureRetailStatMutation(out long stamp));
        Assert.True(stats.TryReserveRetailStatCommit(stamp));
        try
        {
            Assert.False(stats.TryCaptureRetailStatMutation(out _));
            Assert.False(stats.TryReserveRetailStatCommit(stamp));
            writer.Start();Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
            Assert.False(finished.Wait(TimeSpan.FromMilliseconds(150)));
        }
        finally { stats.ReleaseRetailStatCommit(stamp); }
        Assert.True(finished.Wait(TimeSpan.FromSeconds(5)));Assert.Null(failure);
        Assert.False(stats.TryReserveRetailStatCommit(stamp));
        Assert.True(stats.TryCaptureRetailStatMutation(out long fresh));
        Assert.True(stats.TryReserveRetailStatCommit(fresh));stats.ReleaseRetailStatCommit(fresh);
    }
}
