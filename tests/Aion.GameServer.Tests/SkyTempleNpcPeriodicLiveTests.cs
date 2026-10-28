using Aion.GameServer.Controllers;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Controllers.Effects;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;
using Aion.GameServer.Utils.IdFactory;
using Aion.GameServer.Utils.Stats;
using Aion.GameServer.World.Knownlist;
using TYPE=Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus.TYPE;
using LOG=Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus.LOG;
namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class SkyTempleNpcPeriodicLiveTests
{
    private static byte[] Source()
    {
        string p=Environment.GetEnvironmentVariable("AION_RETAIL_SKYTEMPLE_TEST_DATA")??
            Environment.GetEnvironmentVariable("AION_RETAIL_SKYTEMPLE_DATA")??
            @"C:\Users\ryanf\Documents\Aion-NPC-Discovery\implementation\pilot25\data\sky-temple-cohort.json";
        Skip.IfNot(File.Exists(p),"Pinned private selected profile artifact required");return File.ReadAllBytes(p);
    }
    private static Player Player(BossAiHarness h)
    {
        var c=new PlayerCommonData(IDFactory.GetInstance().NextId());c.SetPlayerClass(PlayerClass.WARRIOR);
        c.SetRace(Race.ELYOS);c.SetGender(Gender.MALE);c.SetName("Periodic"+c.GetPlayerObjId());c.SetNote("");
        c.SetExp(DataManager.PLAYER_EXPERIENCE_TABLE.GetStartExpForLevel(24));
        var p=new Player(new PlayerAccountData(c,new PlayerAppearance()),new Account(1));
        p.SetKnownlist(new KnownList(p));p.SetEffectController(new PlayerEffectController(p));p.SetFlyController(new FlyController(p));
        p.SetPosition(h.World.CreatePosition(320050000,101,100,200,0,1));h.World.StoreObject(p);h.World.Spawn(p);
        p.GetGameStats().AddEffectOnly(null!,new IStatFunction[]{new StatSetFunction(StatEnum.MAXHP,50000),
            new StatSetFunction(StatEnum.REGEN_HP,0),new StatSetFunction(StatEnum.MAGICAL_RESIST,0),
            new StatSetFunction(StatEnum.MAGICAL_DEFEND,0),new StatSetFunction(StatEnum.FIRE_RESISTANCE,0),
            new StatSetFunction(StatEnum.MAGICAL_CRITICAL_DAMAGE_REDUCE,0)});p.GetLifeStats().SetCurrentHp(50000);return p;
    }
    private static (Npc Caster,Effect Effect) Apply(BossAiHarness h,Player target)
    {
        var n=h.SpawnWithAi(212876,"general",100,100,200);
        var e=new Effect(n,target,DataManager.SKILL_DATA.GetSkillTemplate(16710),24);
        e.Initialize();Assert.True(e.IsInSuccessEffects(2));e.ApplyEffect();Assert.Equal(13000,e.GetDuration());
        return(n,e);
    }
    private sealed class Hp() : ActionObserver(ObserverType.HP_CHANGED)
    {internal int Calls;public override void HpChanged(int value)=>Calls++;}

    [SkippableFact]
    public void MissingCasterStillTicksThroughActualHpOnceWithoutLegacyNullPermission()
    {
        using var enabled=SkyTempleNpcProfileCatalog.InstallForTests(Source());using var h=BossAiHarness.For(320050000).Build();
        var p=Player(h);var(n,e)=Apply(h,p);var hp=new Hp();p.GetObserveController().AddObserver(hp);
        int before=p.GetLifeStats().GetCurrentHp();Assert.True(h.World.RemoveObject(n));Assert.Null(h.World.FindVisibleObject(n.GetObjectId()));
        h.Clock.Advance(TimeSpan.FromMilliseconds(1999));Assert.Equal(before,p.GetLifeStats().GetCurrentHp());
        h.Clock.Advance(TimeSpan.FromMilliseconds(1));Assert.Equal(before-34,p.GetLifeStats().GetCurrentHp());Assert.Equal(1,hp.Calls);
        Assert.Throws<ArgumentNullException>(()=>p.GetLifeStats().ReduceHp(TYPE.DAMAGE,1,16710,LOG.SPELLATK,null!));
        e.EndEffect();h.Clock.Advance(TimeSpan.FromSeconds(5));Assert.Equal(before-34,p.GetLifeStats().GetCurrentHp());
        Assert.Equal(1,hp.Calls);Assert.Empty(h.Clock.Faults);
    }
    [SkippableFact]
    public void MissingCasterCanKillActualPlayerWithoutInventedAttacker()
    {
        using var enabled=SkyTempleNpcProfileCatalog.InstallForTests(Source());using var h=BossAiHarness.For(320050000).Build();
        // The sparse AI harness deliberately omits player-death service data. Supply the
        // actual holder and rank here; keep PlayerController's real reward/death path.
        var bounties=Aion.GameServer.Dataholders.LoadingUtils.JaxbHolderLoader.LoadFromFile<KillBountyData>(
            Path.Combine(BossAiHarness.RepoRoot(),"game-server","data","static_data","bounties","kill_bounties.xml"));
        Assert.NotEmpty(bounties.GetKillBounties());
        typeof(StaticData).GetProperty(nameof(StaticData.KillBountyDataDh))!.SetValue(
            DataManager.GetRegisteredInstance()!.StaticData,bounties);
        var p=Player(h);p.SetAbyssRank(new AbyssRank(0,0,0,1,0,0,0,1,0,0,0,0,0,0,0));
        var(n,e)=Apply(h,p);Assert.True(h.World.RemoveObject(n));p.GetLifeStats().SetCurrentHp(20);
        h.Clock.Advance(TimeSpan.FromSeconds(2));Assert.Equal(0,p.GetLifeStats().GetCurrentHp());Assert.True(p.IsDead());
        Assert.False(p.GetAggroList().GetFinalDamageList().GetMostDamage()?.GetAttacker() is Player);
        Assert.Equal(0,p.GetAbyssRank().GetAp());Assert.Equal(0,p.GetAbyssRank().GetAllKill());
        Assert.Empty(h.Clock.Faults);
    }
    [SkippableFact]
    public void TargetRetirementCancelsTheActualPeriodicLease()
    {
        using var enabled=SkyTempleNpcProfileCatalog.InstallForTests(Source());using var h=BossAiHarness.For(320050000).Build();
        var p=Player(h);var(_,e)=Apply(h,p);int before=p.GetLifeStats().GetCurrentHp();p.GetController().OnDespawn();
        Assert.False(SkyTempleNpcPeriodicEffects.Owns(e));h.Clock.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(before,p.GetLifeStats().GetCurrentHp());Assert.Empty(h.Clock.Faults);
    }
    [SkippableFact]
    public void CurrentCasterLookupUsesReenteredSameIdOwnerInsteadOfOriginalLifetime()
    {
        using var enabled=SkyTempleNpcProfileCatalog.InstallForTests(Source());using var h=BossAiHarness.For(320050000).Build();
        var p=Player(h);var(n,e)=Apply(h,p);long original=n.GetController().RetailLifeEpoch;
        Assert.True(h.World.RemoveObject(n));
        // Reenter the same registry ID with a fresh NPC life and different current PvE.
        // This is a current-registry/lifetime witness, not a claimed native object address.
        n.GetGameStats().AddEffectOnly(null!,new[]{new StatAddFunction(StatEnum.PVE_ATTACK_RATIO_MAGICAL,1000,true)});
        n.SetPosition(h.World.CreatePosition(320050000,100,100,200,0,1));h.World.StoreObject(n);h.World.Spawn(n);
        Assert.NotEqual(original,n.GetController().RetailLifeEpoch);int before=p.GetLifeStats().GetCurrentHp();
        h.Clock.Advance(TimeSpan.FromSeconds(2));Assert.Equal(before-68,p.GetLifeStats().GetCurrentHp());
        e.EndEffect();Assert.Empty(h.Clock.Faults);
    }
}
