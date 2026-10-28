using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Aion.GameServer.Ai;
using Aion.GameServer.Controllers;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Controllers.Effects;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Dataholders;
using Aion.GameServer.GeoEngine.Bounding;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.GeoEngine.Models;
using Aion.GameServer.GeoEngine.RetailPath;
using Aion.GameServer.GeoEngine.Scene;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.IdFactory;
using Aion.GameServer.World.Geo;
using Aion.GameServer.World.Knownlist;

namespace Aion.GameServer.Tests;

/// <summary>Actual Skill.UseSkill, cast timer, Begin, primary impact and recovery owners.
/// These headless managed-owner tests complement the direct59 Effect cases. They do
/// not establish client animation, live network timing or a whole battle-cycle playthrough.</summary>
[Collection("GoldenDataManager")]
public sealed class SkyTempleNpcSkillCastLiveTests
{
    private const string ProfilePath=@"C:\Users\ryanf\Documents\Aion-NPC-Discovery\implementation\pilot25\data\sky-temple-cohort.json";
    private const string OverlayPath=@"C:\Users\ryanf\Documents\Aion-NPC-Discovery\implementation\pilot25\skill-motion-binding\skill-motion59.json";
    private const string OverlayHash="1f8ce40ef9f4542e2e7e8ebe07dea93bc33f44bf4065618d06ef05962318ad23";
    private const string DefaultMapRoot=@"D:\Aion58ServerTesting\Server\Map\Worlds\IDDF2Flying";
    private static string Profiles()=>Environment.GetEnvironmentVariable("AION_RETAIL_SKYTEMPLE_TEST_DATA")??ProfilePath;
    private static readonly Lazy<SkyTempleNpcNavigationAssets> Assets=new(()=>SkyTempleNpcNavigationAssets.Load(
        Environment.GetEnvironmentVariable(SkyTempleNpcNavigationAssets.RootEnvironment)??DefaultMapRoot));
    public static IEnumerable<object[]> SourceBindings()=>SkyTempleNpcSkillLiveTests.SourceBindings();
    private static void RequireSources()=>Skip.IfNot(File.Exists(Profiles())&&File.Exists(OverlayPath)&&File.Exists(Path.Combine(
        Environment.GetEnvironmentVariable(SkyTempleNpcNavigationAssets.RootEnvironment)??DefaultMapRoot,"Collision.Dat")),
        "Pinned25profile/59motion and actualSkyTemple geometry sources are required.");

    private sealed class CurrentPlaceable:DespawnableNode
    {
        internal CurrentPlaceable(int value,RetailFieldObjectGeometry field)
        {
            id=value;type=DespawnableType.PLACEABLE;
            worldBound=new BoundingBox(new Vector3f(field.Minimum.X,field.Minimum.Y,field.Minimum.Z),
                new Vector3f(field.Maximum.X,field.Maximum.Y,field.Maximum.Z));
        }
    }
    private sealed class GeoScope:IDisposable
    {
        private readonly Dictionary<int,GeoMap> maps=(Dictionary<int,GeoMap>)typeof(GeoService)
            .GetField("geoMaps",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(GeoService.GetInstance())!;
        private readonly KeyValuePair<int,GeoMap>[] previous;
        internal GeoScope()=>previous=maps.ToArray();
        public void Dispose(){maps.Clear();foreach(var row in previous)maps.Add(row.Key,row.Value);}
    }
    private static SkyTempleNpcNavigationAssets LoadedAssets()
    {
        var assets=Assets.Value;var map=GeoService.GetInstance().GetMap(320050000);Assert.Empty(map.GetChildren());
        var scene=assets.Scene(1,new[]{(15,false),(16,false),(17,false)});
        foreach(int id in new[]{15,16,17})map.AttachChild(new CurrentPlaceable(id,
            scene.Field(new((uint)id,RetailFieldObjectGeometry.SupportedClass,scene.Declaration.SceneRevision))));
        assets.ValidateLoadedMap();return assets;
    }
    private sealed record Expected(int Hit,int Total,int Cast,int Reuse,int Ammo);
    private static Expected SourceExpected(int npc,int ordinal,int skill,int level)
    {
        byte[] raw=File.ReadAllBytes(OverlayPath);
        Assert.Equal(OverlayHash,Convert.ToHexStringLower(SHA256.HashData(raw)));
        using var doc=JsonDocument.Parse(raw);var root=doc.RootElement;
        Assert.Equal("05ee65a433d2764f97af414851983c7c03d5b17d5cde8f866e7fcf15756809a3",root.GetProperty("dataset_id").GetString());
        var row=Assert.Single(root.GetProperty("rows").EnumerateArray(),r=>r.GetProperty("npc_id").GetInt32()==npc&&
            r.GetProperty("source_entry_index").GetInt32()==ordinal);
        Assert.Equal(skill,row.GetProperty("skill_id48").GetInt32());Assert.Equal(level,row.GetProperty("skill_level").GetInt32());
        Assert.False(row.GetProperty("fallback_applied").GetBoolean());Assert.Equal(1,row.GetProperty("animation_count34").GetInt32());
        int cast=skill switch {16556=>500,16590 or 16593=>1500,16522 or 16576 or 16589 or 16592 or 16594 or 16595 or 16677 or 16699 or 16710=>2500,
            16794 or 17039=>3500,_=>3000};
        int ammo=skill is 16576 or 16590 or 16592 or 16593 or 16594 or 16595 or 16724 or 16725 or 17039?25:0;
        return new(row.GetProperty("first_hit_word2c").GetInt32(),row.GetProperty("movement_delay0c_uint32").GetInt32(),cast,skill==16710?20000:0,ammo);
    }
    private static (Npc Npc,Player Player,SkyTempleNpcProfile Profile) Actors(BossAiHarness h,int npcId,int skill)
    {
        // Same real supported ground corridor as the actual Stagger integration test.
        var npc=h.SpawnWithAi(npcId,"general",391.25f,406.25f,374.58f);
        Assert.True(SkyTempleNpcProfileCatalog.TryGet(npc,out var p));
        var common=new PlayerCommonData(IDFactory.GetInstance().NextId());
        common.SetPlayerClass(PlayerClass.WARRIOR);common.SetRace(Race.ELYOS);common.SetGender(Gender.MALE);
        common.SetName("RetailCastFixture");common.SetNote("");
        common.SetExp(DataManager.PLAYER_EXPERIENCE_TABLE.GetStartExpForLevel(p.Level));
        var player=new Player(new PlayerAccountData(common,new PlayerAppearance()),new Account(1));
        player.SetEffectController(new PlayerEffectController(player));player.SetKnownlist(new KnownList(player));
        player.SetFlyController(new FlyController(player));Assert.NotNull(player.GetMoveController());
        PlayerStatFunctions.AddPredefinedStatFunctions(player);
        player.SetPosition(h.World.CreatePosition(320050000,391.25f,404.25f,374.58f,skill==16794?(byte)30:(byte)0,1));
        h.World.StoreObject(player);h.World.Spawn(player);
        player.GetGameStats().AddEffectOnly(null!,new IStatFunction[]{
            new StatSetFunction(StatEnum.MAXHP,50000),new StatSetFunction(StatEnum.REGEN_HP,0),
            new StatSetFunction(StatEnum.PHYSICAL_DEFENSE,0),new StatSetFunction(StatEnum.MAGICAL_DEFEND,0),
            new StatSetFunction(StatEnum.EVASION,0),new StatSetFunction(StatEnum.PARRY,0),new StatSetFunction(StatEnum.BLOCK,0),
            new StatSetFunction(StatEnum.MAGICAL_RESIST,0),new StatSetFunction(StatEnum.ABNORMAL_RESISTANCE_ALL,0),
            new StatSetFunction(StatEnum.STAGGER_RESISTANCE,0),new StatSetFunction(StatEnum.SPIN_RESISTANCE,0)});
        player.GetLifeStats().SetCurrentHp(50000);Assert.Equal(p.Level,(int)player.GetLevel());Assert.False(player.IsInvulnerable());
        BossAiHarness.MakeMutuallyKnown(npc,player);h.Engage(npc,player);return(npc,player,p);
    }
    private sealed class HpTrace(Creature actor):ActionObserver(ObserverType.HP_CHANGED)
    {
        internal readonly List<(long Tick,int Hp)> Values=[];
        public override void HpChanged(int hp)=>Values.Add((SystemClock.CurrentMillis(),actor.GetLifeStats().GetCurrentHp()));
    }
    private static bool Self(int skill)=>skill is 16522 or 16588 or 16836 or 16677 or 16699;
    private static Skill Cast(Npc npc,Creature target,int id,int level)
    {
        var actual=Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(npc,id,level,target);
        Assert.NotNull(actual);Assert.True(actual.UseSkill(),$"Actual selected Skill.UseSkill failed: NPC{npc.GetNpcId()}/skill{id}/level{level}");
        Assert.Same(actual,npc.GetCastingSkill());return actual;
    }

    [SkippableTheory]
    [MemberData(nameof(SourceBindings))]
    public void All59ActualSkillCastsKeepBeginPrimaryImpactAndRecoverySeparate(int npcId,int ordinal,int id,int level)
    {
        Skip.If(npcId==0,"Exact59 source bindings required.");RequireSources();
        using var profiles=SkyTempleNpcProfileCatalog.InstallForTests(File.ReadAllBytes(Profiles()));using var oldGeo=new GeoScope();
        using var h=BossAiHarness.For(320050000).WithWorldSize(512).Build();using var navigation=SkyTempleNpcNavigationAssets.InstallForTests(LoadedAssets());
        var(npc,player,p)=Actors(h,npcId,id);var expected=SourceExpected(npcId,ordinal,id,level);
        var template=DataManager.SKILL_DATA.GetSkillTemplate(id);
        Assert.Equal(expected.Cast,template.GetDuration());Assert.Equal(expected.Reuse,template.GetCooldown()*100);Assert.Equal((float)expected.Ammo,template.GetAmmoSpeed());
        var sourceMotion=SkyTempleNpcSkillTiming.SourceMotion(npcId,id,level);
        Assert.Equal(expected.Hit,(int)sourceMotion.FirstHit);Assert.Equal(expected.Total,(int)sourceMotion.Recovery);
        Creature target=Self(id)?npc:player;bool heal=id is 16588 or 16836;
        if(heal)npc.GetLifeStats().SetCurrentHp(p.MaxHp/2);
        int before=target.GetLifeStats().GetCurrentHp();var hp=new HpTrace(target);target.GetObserveController().AddObserver(hp);
        long start=SystemClock.CurrentMillis();var cast=Cast(npc,target,id,level);
        var used=SkyTempleNpcSkillState.GetRowSnapshot(npc,ordinal);var descriptor=p.Skills.Single(s=>s.Ordinal==ordinal);
        Assert.Equal(id,used.SkillId);Assert.Equal(level,used.Level);
        Assert.Equal(descriptor.Count==0?0:descriptor.Count-1,used.Remaining);
        Assert.Equal(unchecked((uint)(start+expected.Cast)),used.ReuseAnchor);
        // Target-mode projectiles travel2m (80ms at25m/s); source-Me area skills
        // bind their actual first target to self and therefore have zero travel.
        bool sourceMe=id is 16592 or 16593 or 16594 or 16595 or 16836;
        if(sourceMe)Assert.Same(npc,cast.GetFirstTarget());
        int packetWord=expected.Hit+(expected.Ammo==25&&!Self(id)&&!sourceMe?80:0);
        Assert.Equal(packetWord,cast.GetHitTime());
        int recovery=Math.Max(p.AttackDelay,expected.Total);
        Assert.Equal(expected.Cast+recovery,npc.GetGameStats().GetNextAttackInterval());
        Assert.Equal(expected.Cast+expected.Total,SkyTempleNpcSkillState.GetNextMovementInterval(npc));
        h.Clock.Advance(TimeSpan.FromMilliseconds(expected.Cast-1));Assert.True(npc.IsCasting());Assert.Equal(before,target.GetLifeStats().GetCurrentHp());Assert.Empty(hp.Values);
        h.Clock.Advance(TimeSpan.FromMilliseconds(1));Assert.False(npc.IsCasting());
        Assert.Equal(recovery,npc.GetGameStats().GetNextAttackInterval());
        Assert.Equal(expected.Total,SkyTempleNpcSkillState.GetNextMovementInterval(npc));
        if(Self(id))
        {
            if(heal)
            {
                Assert.Equal(Math.Min(p.MaxHp,before+p.MaxHp*(id==16588?25:20)/100),npc.GetLifeStats().GetCurrentHp());
                Assert.Single(hp.Values);Assert.Equal(start+expected.Cast,hp.Values[0].Tick);
            }
            else
            {
                Assert.Equal(before,npc.GetLifeStats().GetCurrentHp());Assert.Empty(hp.Values);
                Assert.Contains(npc.GetEffectController().GetAbnormalEffects(),e=>e.GetSkillId()==id);
                if(id==16677)Assert.True(npc.GetEffectController().IsAbnormalSet(AbnormalState.HIDE));
                if(id==16522)Assert.Equal(170,npc.GetGameStats().GetStat(StatEnum.PHYSICAL_ATTACK,100).GetCurrent());
            }
        }
        else
        {
            Assert.Equal(before,player.GetLifeStats().GetCurrentHp());Assert.Empty(hp.Values);
            // The same real Effect Begin starts secondary state/DOT now, before primary HP.
            if(id is 16576 or 16589 or 16590 or 16592 or 16593 or 16594 or 16595 or 16710)
                Assert.Contains(player.GetEffectController().GetAbnormalEffects(),e=>e.GetSkillId()==id);
            if(id is 16590 or 16593)Assert.True(player.GetEffectController().IsAbnormalSet(AbnormalState.SILENCE));
            if(id==16595)Assert.True(player.GetEffectController().IsAbnormalSet(AbnormalState.STUN));
            int impact=Math.Max(100,packetWord-50);
            h.Clock.Advance(TimeSpan.FromMilliseconds(impact-1));Assert.Equal(before,player.GetLifeStats().GetCurrentHp());Assert.Empty(hp.Values);
            h.Clock.Advance(TimeSpan.FromMilliseconds(1));Assert.True(player.GetLifeStats().GetCurrentHp()<before);
            Assert.Single(hp.Values);Assert.Equal(start+expected.Cast+impact,hp.Values[0].Tick);
            if(id==16794)Assert.True(player.GetEffectController().IsAbnormalSet(AbnormalState.SPIN));
            Assert.Equal(Math.Max(0,recovery-impact),npc.GetGameStats().GetNextAttackInterval());
        }
        Assert.Empty(h.Clock.Faults);
    }

    [SkippableTheory]
    [InlineData(212770,0,21)] [InlineData(212771,0,22)] [InlineData(212772,2,23)]
    public void Actual17039RunsStaggerPrepass300MillisecondsBeforeItsOneHpCommit(int npcId,int ordinal,int level)
    {
        RequireSources();using var profiles=SkyTempleNpcProfileCatalog.InstallForTests(File.ReadAllBytes(Profiles()));using var oldGeo=new GeoScope();
        using var h=BossAiHarness.For(320050000).WithWorldSize(512).Build();using var nav=SkyTempleNpcNavigationAssets.InstallForTests(LoadedAssets());
        var(npc,player,p)=Actors(h,npcId,17039);var expected=SourceExpected(npcId,ordinal,17039,level);
        var hp=new HpTrace(player);player.GetObserveController().AddObserver(hp);int before=player.GetLifeStats().GetCurrentHp();
        var initial=new RetailSearchPoint(player.GetX(),player.GetY(),player.GetZ());
        var body=player.GetObjectTemplate().GetBoundRadius();
        var geometry=SkyTempleNpcForcedMove.Calculate(Assets.Value.CurrentScene(npc),new(npc.GetX(),npc.GetY(),npc.GetZ()),
            initial,new(body.GetFront(),body.GetSide(),body.GetUpper()),false,SkyTempleNpcNavigationAssets.QueryLimits);
        Assert.True(geometry.Supported,geometry.Detail);Assert.NotNull(geometry.Endpoint);
        Cast(npc,player,17039,level);int impact=Math.Max(100,expected.Hit+80-50);
        h.Clock.Advance(TimeSpan.FromMilliseconds(expected.Cast+impact-301));
        Assert.Equal(initial,new RetailSearchPoint(player.GetX(),player.GetY(),player.GetZ()));
        Assert.False(player.GetEffectController().IsAbnormalSet(AbnormalState.STAGGER));Assert.Empty(hp.Values);
        h.Clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(geometry.Endpoint!.Value,new RetailSearchPoint(player.GetX(),player.GetY(),player.GetZ()));
        Assert.False(player.GetEffectController().IsAbnormalSet(AbnormalState.STAGGER));
        Assert.Equal(before,player.GetLifeStats().GetCurrentHp());Assert.Empty(hp.Values);
        h.Clock.Advance(TimeSpan.FromMilliseconds(299));Assert.Empty(hp.Values);
        Assert.False(player.GetEffectController().IsAbnormalSet(AbnormalState.STAGGER));
        h.Clock.Advance(TimeSpan.FromMilliseconds(1));Assert.Single(hp.Values);Assert.True(player.GetLifeStats().GetCurrentHp()<before);
        Assert.True(player.GetEffectController().IsAbnormalSet(AbnormalState.STAGGER));
        Assert.Equal(geometry.Endpoint!.Value,new RetailSearchPoint(player.GetX(),player.GetY(),player.GetZ()));
        Assert.Empty(h.Clock.Faults);
    }

    [SkippableTheory]
    [InlineData(212780,3,23)] [InlineData(212876,3,24)]
    public void Actual16710ReuseBoundaryAndExhaustedSourceCountRemainIndependent(int npcId,int ordinal,int level)
    {
        RequireSources();using var profiles=SkyTempleNpcProfileCatalog.InstallForTests(File.ReadAllBytes(Profiles()));using var oldGeo=new GeoScope();
        using var h=BossAiHarness.For(320050000).WithWorldSize(512).Build();using var nav=SkyTempleNpcNavigationAssets.InstallForTests(LoadedAssets());
        var(npc,player,p)=Actors(h,npcId,16710);var expected=SourceExpected(npcId,ordinal,16710,level);
        var descriptor=p.Skills.Single(s=>s.Ordinal==ordinal);Assert.Equal(1,descriptor.Count);Assert.Equal(0,descriptor.Delay);
        var cast=Cast(npc,player,16710,level);Assert.False(SkyTempleNpcSkillState.CanStartSkill(npc,cast));
        var used=SkyTempleNpcSkillState.GetRowSnapshot(npc,ordinal);Assert.Equal(0,used.Remaining);
        Assert.Equal(unchecked(SystemClock.RetailTick32()+(uint)expected.Cast),used.ReuseAnchor);
        h.Clock.Advance(TimeSpan.FromMilliseconds(expected.Cast));
        Assert.Contains(player.GetEffectController().GetAbnormalEffects(),e=>e.GetSkillId()==16710);
        // Existing follow-up attack work must not obscure this independent reuse boundary.
        // IDLE makes that scheduler's own ordinary FIGHT guard stop it; current effect timers continue.
        npc.GetAi().SetStateIfNot(AIState.IDLE);
        h.Clock.Advance(TimeSpan.FromMilliseconds(expected.Reuse));
        Assert.Equal(unchecked(used.ReuseAnchor+(uint)expected.Reuse),SystemClock.RetailTick32());
        Assert.False(SkyTempleNpcSkillState.ReuseAllows(used.ReuseAnchor,SystemClock.RetailTick32(),(uint)expected.Reuse,0));
        Assert.False(SkyTempleNpcSkillState.CanStartSkill(npc,cast));
        h.Clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.True(SkyTempleNpcSkillState.ReuseAllows(used.ReuseAnchor,SystemClock.RetailTick32(),(uint)expected.Reuse,0));
        // A completed sourceCount1 cast is exhausted. Elapsed reuse must not manufacture
        // another use or bypass the actual finite-count gate.
        Assert.Equal(0,SkyTempleNpcSkillState.GetRowSnapshot(npc,ordinal).Remaining);
        Assert.Equal(0,SkyTempleNpcSkillState.GetNextMovementInterval(npc));
        Assert.False(SkyTempleNpcSkillState.CanStartSkill(npc,cast));
        Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void ActualAbortRetiresOldCastAndUses100MillisecondRecoveryInsteadOfInitialZero()
    {
        RequireSources();using var profiles=SkyTempleNpcProfileCatalog.InstallForTests(File.ReadAllBytes(Profiles()));using var oldGeo=new GeoScope();
        using var h=BossAiHarness.For(320050000).WithWorldSize(512).Build();using var nav=SkyTempleNpcNavigationAssets.InstallForTests(LoadedAssets());
        var(npc,player,p)=Actors(h,212761,16724);var row=p.Skills.Single(s=>s.Id48==16724);
        Assert.Equal(2,row.Count);Assert.Equal(2,SkyTempleNpcSkillState.GetRowSnapshot(npc,row.Ordinal).Remaining);
        var hp=new HpTrace(player);player.GetObserveController().AddObserver(hp);var cast=Cast(npc,player,16724,row.Level);
        Assert.Equal(1,SkyTempleNpcSkillState.GetRowSnapshot(npc,row.Ordinal).Remaining);
        h.Clock.Advance(TimeSpan.FromMilliseconds(100));Assert.Same(cast,npc.GetController().AbortCast());Assert.False(npc.IsCasting());
        Assert.Equal(2,SkyTempleNpcSkillState.GetRowSnapshot(npc,row.Ordinal).Remaining);
        Assert.Equal(100,npc.GetGameStats().GetNextAttackInterval());Assert.Equal(100,SkyTempleNpcSkillState.GetNextMovementInterval(npc));
        h.Clock.Advance(TimeSpan.FromMilliseconds(99));Assert.Equal(1,npc.GetGameStats().GetNextAttackInterval());
        h.Clock.Advance(TimeSpan.FromMilliseconds(1));Assert.Equal(0,npc.GetGameStats().GetNextAttackInterval());
        h.Clock.Advance(TimeSpan.FromMilliseconds(5000));Assert.Empty(hp.Values);
        Assert.DoesNotContain(player.GetEffectController().GetAbnormalEffects(),e=>e.GetSkillId()==16724);Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void ActualPriorNormalThenCastAbortDoesNotReviveTheLegacyNormalCadence()
    {
        RequireSources();using var profiles=SkyTempleNpcProfileCatalog.InstallForTests(File.ReadAllBytes(Profiles()));using var oldGeo=new GeoScope();
        using var h=BossAiHarness.For(320050000).WithWorldSize(512).Build();using var nav=SkyTempleNpcNavigationAssets.InstallForTests(LoadedAssets());
        var(npc,player,p)=Actors(h,212776,16556);var row=p.Skills.Single(s=>s.Id48==16556);
        // Start a real ordinary attack through its existing manager, including the
        // SM_ATTACK launch and actual AttackComplete timestamp/recovery consumers.
        h.World.UpdatePosition(player,391.25f,405.25f,374.58f,0);
        var hp=new HpTrace(player);player.GetObserveController().AddObserver(hp);
        long normalTick=SystemClock.CurrentMillis();
        Aion.GameServer.Ai.Manager.SimpleAttackManager.PerformAttack((NpcAI)npc.GetAi(),0);
        Assert.Equal(p.AttackDelay,npc.GetGameStats().GetNextAttackInterval());
        Assert.Equal(933,SkyTempleNpcSkillState.GetNextMovementInterval(npc));
        // The actual normal's impact can occur during this wait; its recovery is
        // still active when the shorter movement deadline permits the new cast.
        h.Clock.Advance(TimeSpan.FromMilliseconds(934));
        var cast=Cast(npc,player,16556,row.Level);
        h.Clock.Advance(TimeSpan.FromMilliseconds(100));Assert.Same(cast,npc.GetController().AbortCast());
        Assert.True(p.AttackDelay-(SystemClock.CurrentMillis()-normalTick)>100,
            "The regression needs an old normal timestamp whose legacy remainder exceeds Cancel100.");
        int committed=hp.Values.Count;
        Assert.Equal(100,npc.GetGameStats().GetNextAttackInterval());
        h.Clock.Advance(TimeSpan.FromMilliseconds(99));Assert.Equal(1,npc.GetGameStats().GetNextAttackInterval());
        h.Clock.Advance(TimeSpan.FromMilliseconds(1));Assert.Equal(0,npc.GetGameStats().GetNextAttackInterval());
        npc.GetAi().SetStateIfNot(AIState.IDLE); // Stop the unrelated queued ordinary follow-up.
        h.Clock.Advance(TimeSpan.FromMilliseconds(5000));Assert.Equal(committed,hp.Values.Count);
        Assert.DoesNotContain(player.GetEffectController().GetAbnormalEffects(),e=>e.GetSkillId()==16556);Assert.Empty(h.Clock.Faults);
    }

    private sealed class FaultAfterHp:ActionObserver
    {
        internal int Calls;
        internal FaultAfterHp():base(ObserverType.HP_CHANGED){}
        public override void HpChanged(int hp){Calls++;throw new InvalidOperationException("post-HP fixture observer");}
    }
    [SkippableFact]
    public void ActualSpinChildStillStartsOnceWhenTheOneHpCommitObserverFaults()
    {
        RequireSources();using var profiles=SkyTempleNpcProfileCatalog.InstallForTests(File.ReadAllBytes(Profiles()));using var oldGeo=new GeoScope();
        using var h=BossAiHarness.For(320050000).WithWorldSize(512).Build();using var nav=SkyTempleNpcNavigationAssets.InstallForTests(LoadedAssets());
        var(npc,player,p)=Actors(h,212776,16794);var row=p.Skills.Single(s=>s.Id48==16794);
        var expected=SourceExpected(p.NpcId,row.Ordinal,16794,row.Level);int before=player.GetLifeStats().GetCurrentHp();
        var fault=new FaultAfterHp();player.GetObserveController().AddObserver(fault);
        Cast(npc,player,16794,row.Level);
        h.Clock.Advance(TimeSpan.FromMilliseconds(expected.Cast+Math.Max(100,expected.Hit-50)));
        Assert.True(player.GetLifeStats().GetCurrentHp()<before);Assert.Equal(1,fault.Calls);
        Assert.True(player.GetEffectController().IsAbnormalSet(AbnormalState.SPIN));
        Assert.Single(player.GetEffectController().GetAbnormalEffects(),e=>e.GetSkillId()==8223);
        Assert.Single(h.Clock.Faults);int after=player.GetLifeStats().GetCurrentHp();
        // Stay before either the next ordinary attack or the source Spin expiry.
        h.Clock.Advance(TimeSpan.FromMilliseconds(50));
        Assert.Equal(after,player.GetLifeStats().GetCurrentHp());Assert.Equal(1,fault.Calls);
        Assert.Single(player.GetEffectController().GetAbnormalEffects(),e=>e.GetSkillId()==8223);Assert.Single(h.Clock.Faults);
    }
}
