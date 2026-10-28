using Aion.GameServer.Ai;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.Tests.Ai;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class SkyTempleNpcHeartbeatTests
{
    private sealed class RetainedCallbacks() : ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance)
    {
        internal readonly List<Func<CancellationToken, ValueTask>> Callbacks = [];
        public override ScheduledTask Schedule(Func<CancellationToken, ValueTask> action, TimeSpan delay,
            CancellationToken cancellationToken = default)
        {
            Callbacks.Add(action);
            return Deferred(() => action(cancellationToken).GetAwaiter().GetResult());
        }
    }
    private sealed class ThrowOnceOnHp() : ActionObserver(ObserverType.HP_CHANGED)
    {
        internal int Calls;
        public override void HpChanged(int hp)
        {
            if (++Calls == 1) throw new InvalidOperationException("test post-commit observer failure");
        }
    }
    private static byte[] Source()
    {
        string file = Environment.GetEnvironmentVariable("AION_RETAIL_SKYTEMPLE_DATA")
            ?? @"C:\Users\ryanf\Documents\Aion-NPC-Discovery\implementation\pilot25\data\sky-temple-cohort.json";
        Skip.IfNot(File.Exists(file), "Pinned private25-profile artifact is required for live NPC heartbeat tests.");
        return File.ReadAllBytes(file);
    }

    [SkippableFact]
    public void RealNpcUsesSourceHpAndHeartbeatInsteadOfLegacy1700msRestore()
    {
        using var profiles = SkyTempleNpcProfileCatalog.InstallForTests(Source());
        using var h = BossAiHarness.For(320050000).Build();
        var npc = h.SpawnWithAi(212876, "general", 100, 100, 200);
        var life = npc.GetLifeStats();
        Assert.Equal(5954, life.GetMaxHp());
        Assert.Equal(5954, life.GetCurrentHp());
        life.SetCurrentHp(1000);
        life.TriggerRestoreTask();
        life.TriggerRestoreTask();
        h.Clock.Advance(TimeSpan.FromMilliseconds(1700));
        Assert.Equal(1000, life.GetCurrentHp());
        h.Clock.Advance(TimeSpan.FromMilliseconds(500));
        Assert.Equal(3233, life.GetCurrentHp());
        h.Clock.Advance(TimeSpan.FromMilliseconds(5000));
        Assert.Equal(3233, life.GetCurrentHp());
        h.Clock.Advance(TimeSpan.FromMilliseconds(2000));
        Assert.Equal(5466, life.GetCurrentHp());
        h.Clock.Advance(TimeSpan.FromSeconds(7));
        Assert.Equal(5954, life.GetCurrentHp());
        Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void EncounterSuppressesHealingThroughFearThenReturnCanRegenerateWithoutArrivalHeal()
    {
        using var profiles = SkyTempleNpcProfileCatalog.InstallForTests(Source());
        using var h = BossAiHarness.For(320050000).Build();
        var npc = h.SpawnWithAi(212876, "general", 100, 100, 200);
        var life = npc.GetLifeStats();
        life.SetCurrentHp(1000);
        npc.GetGameStats().SetFightStartingTime();
        npc.GetAi().SetStateIfNot(AIState.FEAR);
        h.Clock.Advance(TimeSpan.FromSeconds(9));
        Assert.Equal(1000, life.GetCurrentHp());
        npc.GetGameStats().ResetFightStats();
        npc.GetAi().SetStateIfNot(AIState.RETURNING);
        Assert.Equal(1000, life.GetCurrentHp());
        h.Clock.Advance(TimeSpan.FromSeconds(7));
        Assert.Equal(3233, life.GetCurrentHp());
        Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void DiseaseAndDeathCannotBeHealedByAnAlreadyArmedHeartbeat()
    {
        using var profiles = SkyTempleNpcProfileCatalog.InstallForTests(Source());
        using var h = BossAiHarness.For(320050000).Build();
        var npc = h.SpawnWithAi(212876, "general", 100, 100, 200);
        var life = npc.GetLifeStats();
        life.SetCurrentHp(1000);
        npc.GetEffectController().SetAbnormal(AbnormalState.DISEASE);
        h.Clock.Advance(TimeSpan.FromSeconds(9));
        Assert.Equal(1000, life.GetCurrentHp());
        npc.GetEffectController().UnsetAbnormal(AbnormalState.DISEASE);
        life.StopRetailHeartbeat();
        life.SetCurrentHp(0);
        h.Clock.Advance(TimeSpan.FromSeconds(20));
        Assert.Equal(0, life.GetCurrentHp());
        Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void ExistingHpCommitRejectsRetiredLifetimeAndDoesNotOverflowOrResurrect()
    {
        using var profiles = SkyTempleNpcProfileCatalog.InstallForTests(Source());
        using var h = BossAiHarness.For(320050000).Build();
        var npc = h.SpawnWithAi(212876, "general", 100, 100, 200);
        var life = npc.GetLifeStats();
        life.SetCurrentHp(1000);
        Assert.Equal(1000, life.IncreaseRetailNpcHp(100, () => false));
        Assert.Equal(1000, life.GetCurrentHp());
        Assert.Equal(5954, life.IncreaseRetailNpcHp(int.MaxValue, () => true));
        life.StopRetailHeartbeat();
        life.SetCurrentHp(0);
        Assert.Equal(0, life.IncreaseRetailNpcHp(100, () => true));
        Assert.Equal(0, life.GetCurrentHp());
    }

    [SkippableFact]
    public void OnlyExplicitRestDoublesRegenAndStoppingRetiresThePendingChain()
    {
        using var profiles = SkyTempleNpcProfileCatalog.InstallForTests(Source());
        using var h = BossAiHarness.For(320050000).Build();
        var npc = h.SpawnWithAi(212876, "general", 100, 100, 200);
        var life = npc.GetLifeStats();
        life.SetCurrentHp(1000);
        npc.SetState(CreatureState.RESTING);
        h.Clock.Advance(TimeSpan.FromMilliseconds(2200));
        Assert.Equal(5466, life.GetCurrentHp());
        life.StopRetailHeartbeat();
        life.SetCurrentHp(1000);
        h.Clock.Advance(TimeSpan.FromSeconds(15));
        Assert.Equal(1000, life.GetCurrentHp());
        Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void PostCommitObserverFailureDoesNotReplayHpOrStrandTheHeartbeat()
    {
        using var profiles = SkyTempleNpcProfileCatalog.InstallForTests(Source());
        using var h = BossAiHarness.For(320050000).Build();
        var npc = h.SpawnWithAi(212876, "general", 100, 100, 200);
        var life = npc.GetLifeStats();
        life.SetCurrentHp(1000);
        var observer = new ThrowOnceOnHp();
        npc.GetObserveController().AddObserver(observer);
        h.Clock.Advance(TimeSpan.FromMilliseconds(2200));
        Assert.Equal(3233, life.GetCurrentHp());
        Assert.Single(h.Clock.Faults);
        Assert.Equal(1, observer.Calls);
        h.Clock.Advance(TimeSpan.FromSeconds(7));
        Assert.Equal(5466, life.GetCurrentHp());
        Assert.Single(h.Clock.Faults);
        Assert.Equal(2, observer.Calls);
        life.CancelAllTasks();
    }

    [SkippableFact]
    public async Task RetiredCallbackCannotHealOrReplaceTheNewSameNpcHeartbeat()
    {
        using var profiles = SkyTempleNpcProfileCatalog.InstallForTests(Source());
        using var h = BossAiHarness.For(320050000).Build();
        var npc = h.SpawnWithAi(212876, "general", 100, 100, 200);
        var life = npc.GetLifeStats();
        life.StopRetailHeartbeat();
        await using var pool = new RetainedCallbacks();
        ThreadPoolManager.RegisterInstance(pool);
        life.StartRetailHeartbeat();
        var old = Assert.Single(pool.Callbacks);
        life.StopRetailHeartbeat();
        life.StartRetailHeartbeat();
        Assert.Equal(2, pool.Callbacks.Count);
        life.SetCurrentHp(1000);
        // Simulate a retained callback whose token was already acquired before cancellation.
        await old(CancellationToken.None);
        Assert.Equal(1000, life.GetCurrentHp());
        Assert.Equal(2, pool.Callbacks.Count);
        await pool.Callbacks[1](CancellationToken.None);
        Assert.Equal(3233, life.GetCurrentHp());
        Assert.Equal(3, pool.Callbacks.Count);
        await old(CancellationToken.None);
        Assert.Equal(3233, life.GetCurrentHp());
        Assert.Equal(3, pool.Callbacks.Count);
        life.CancelAllTasks();
    }
}
