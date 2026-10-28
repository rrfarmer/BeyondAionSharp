using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aion.GameServer.Controllers;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;
using Aion.GameServer.Utils.IdFactory;
using Aion.GameServer.World.Knownlist;
using Family=Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcPeriodicSpellParameters.Family;
using TYPE=Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus.TYPE;
using LOG=Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus.LOG;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcStatPeriodicTests
{
    [Theory]
    [InlineData(Family.Poison)]
    [InlineData(Family.Bleed)]
    internal void IdentityChildrenIgnoreBoostMagicDefenseAndElement(Family family)
    {
        Assert.Equal(54,RetailNpcPeriodicDamageStages.SkillBoost(family,9000,0,1000,0,54).Damage);
        Assert.Equal(54,RetailNpcPeriodicDamageStages.MagicalDefense(family,5000,54));
        Assert.Equal(54,RetailNpcPeriodicDamageStages.ElementalDefense(family,SkyTempleCombatInputs.Scope(210506),1300,54,14,38).Damage);
        Assert.Equal(54,RetailNpcPeriodicDamageStages.NpcCritical(54,5,0,0,1000));
        Assert.Throws<InvalidOperationException>(()=>RetailNpcStatPeriodicCombat.TickDispatch.Produce(new object(),null,54));
    }

    [SkippableFact]
    public void CompleteSelectedPoisonBleedCensusKeepsTheOneNonperiodicProgramVisible()
    {
        string? manifest=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        Skip.If(string.IsNullOrEmpty(manifest),"Requires pinned selected full09 sources");
        using var doc=JsonDocument.Parse(File.ReadAllBytes(manifest!));int poison=0,bleed=0,unsupported=0;
        foreach(var asset in doc.RootElement.GetProperty("skill_sources").EnumerateArray())
        {
            var source=RetailNpcStatPeriodicCombat.Parse(File.ReadAllBytes(asset.GetProperty("path").GetString()!),asset.GetProperty("sha256").GetString()!);
            foreach(var part in source.Parts)
            {
                if(part.Family is not (Family.Poison or Family.Bleed))continue;
                if(!part.Supported){unsupported++;Assert.Equal(17915,source.Id);Assert.Equal("nonpositive-periodic-interval",part.UnsupportedReason);continue;}
                if(part.Family==Family.Poison)poison++;else bleed++;
            }
        }
        Assert.Equal((80,56,1),(poison,bleed,unsupported));
    }

    private sealed class Owner:IStatOwner { }
    private sealed class HpCounter():ActionObserver(ObserverType.HP_CHANGED)
    {internal int Calls;public override void HpChanged(int value)=>Calls++;}

    internal static Player Player(BossAiHarness h,int map)
    {
        // BossAiHarness.SpawnPlayer deliberately returns an invulnerable actor.
        // Use the real Player for these controller/HP integration witnesses.
        var common=new PlayerCommonData(IDFactory.GetInstance().NextId());
        common.SetPlayerClass(PlayerClass.WARRIOR);common.SetRace(Race.ASMODIANS);common.SetGender(Gender.MALE);
        common.SetName("PeriodicStats"+common.GetPlayerObjId());common.SetNote("");
        common.SetExp(DataManager.PLAYER_EXPERIENCE_TABLE.GetStartExpForLevel(38));
        var player=new Player(new PlayerAccountData(common,new PlayerAppearance()),new Account(1));
        player.SetKnownlist(new KnownList(player));player.SetEffectController(new Aion.GameServer.Controllers.Effects.PlayerEffectController(player));
        player.SetFlyController(new FlyController(player));player.SetPosition(h.World.CreatePosition(map,985,135,242,0,1));
        h.World.StoreObject(player);h.World.Spawn(player);Assert.False(player.IsInvulnerable());return player;
    }

    [SkippableFact]
    public void RealPoisonAndBleedUseCurrentMagicalPveAndOneExistingTimerHpCommit()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_PERIODIC_UNIT_CASES");
        Skip.If(string.IsNullOrEmpty(path),"Requires external source-bound periodic fixture");
        using var doc=JsonDocument.Parse(File.ReadAllBytes(path!));var root=doc.RootElement;
        string manifest=root.GetProperty("manifest").GetString()!,hash=root.GetProperty("sha256").GetString()!;
        using var installed=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(manifest),hash,Path.GetDirectoryName(manifest)!);
        foreach(var c in root.GetProperty("cases").EnumerateArray())
        {
            using var h=BossAiHarness.For(c.GetProperty("map").GetInt32()).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
            var n=h.SpawnWithAi(c.GetProperty("npc").GetInt32(),"retail_stat_base_fixture");
            var p=Player(h,c.GetProperty("map").GetInt32());
            p.GetGameStats().AddEffectOnly(new Owner(),[new StatSetFunction(StatEnum.MAXHP,50000),new StatSetFunction(StatEnum.REGEN_HP,0)]);
            p.GetLifeStats().SetCurrentHp(50000);
            int id=c.GetProperty("skill").GetInt32(),level=c.GetProperty("level").GetInt32(),position=c.GetProperty("position").GetInt32(),basis=c.GetProperty("base").GetInt32();
            var template=DataManager.SKILL_DATA.GetSkillTemplate(id);
            var part=Assert.IsAssignableFrom<AbstractOverTimeEffect>(template.GetEffectTemplate(position));
            var e=new Effect(n,p,template,level);
            // Deliberately preselect a magical critical to exercise the native NPC
            // base1 and current-fortitude branch independently of a random outcome.
            e.SetCalculatedMagicalCritical(position,true);
            Assert.True(RetailNpcStatPeriodicCombat.TryResolveCritical(e,part));e.AddSuccessEffect(part);
            part.StartEffect(e);var hp=new HpCounter();p.GetObserveController().AddObserver(hp);
            Assert.True(e.IsPeriodic());Assert.False(e.RetailHasEnded);Assert.True(p.IsSpawned());Assert.False(p.IsDead());
            Assert.Equal((0,50),SkyTempleCombatInputs.Pve(n,p,false));
            int before=p.GetLifeStats().GetCurrentHp(),first=(int)(basis*.95f);
            h.Clock.Advance(TimeSpan.FromMilliseconds(part.checktime));
            Assert.Empty(h.Clock.Faults);
            Assert.Equal(before-first,p.GetLifeStats().GetCurrentHp());Assert.Equal(1,hp.Calls);
            p.GetGameStats().AddEffectOnly(new Owner(),[new StatSetFunction(StatEnum.MAGICAL_DEFEND,5000),
                new StatSetFunction(StatEnum.FIRE_RESISTANCE,1300),new StatSetFunction(StatEnum.WIND_RESISTANCE,1300),
                new StatSetFunction(StatEnum.MAGICAL_CRITICAL_DAMAGE_REDUCE,1000),new StatSetFunction(StatEnum.PVE_DEFEND_RATIO_PHYSICAL,5000)]);
            h.Clock.Advance(TimeSpan.FromMilliseconds(part.checktime));
            Assert.Equal(before-2*first,p.GetLifeStats().GetCurrentHp());Assert.Equal(2,hp.Calls);
            p.GetGameStats().AddEffectOnly(new Owner(),[new StatSetFunction(StatEnum.PVE_DEFEND_RATIO_MAGICAL,450)]);
            Assert.Equal((0,500),SkyTempleCombatInputs.Pve(n,p,false));
            h.Clock.Advance(TimeSpan.FromMilliseconds(part.checktime));
            Assert.Equal(before-2*first-(int)(basis*.5f),p.GetLifeStats().GetCurrentHp());Assert.Equal(3,hp.Calls);
            Assert.True(h.World.RemoveObject(n));
            h.Clock.Advance(TimeSpan.FromMilliseconds(part.checktime));
            Assert.Equal(before-2*first-(int)(basis*.5f)-basis,p.GetLifeStats().GetCurrentHp());Assert.Equal(4,hp.Calls);
            Assert.Throws<ArgumentNullException>(()=>p.GetLifeStats().ReduceHp(TYPE.DAMAGE,1,id,LOG.POISON,null!));
            e.EndEffect();int ended=p.GetLifeStats().GetCurrentHp();
            h.Clock.Advance(TimeSpan.FromMilliseconds(2*part.checktime));Assert.Equal(ended,p.GetLifeStats().GetCurrentHp());
            Assert.Equal(4,hp.Calls);Assert.Empty(h.Clock.Faults);
        }
    }
}
