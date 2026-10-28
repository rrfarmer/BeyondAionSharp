using System.Reflection;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Handler;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Controllers.Effects;
using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.Dataholders;
using Aion.GameServer.GeoEngine.Bounding;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.GeoEngine.Models;
using Aion.GameServer.GeoEngine.RetailPath;
using Aion.GameServer.GeoEngine.Scene;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Geo;

namespace Aion.GameServer.Tests;

/// <summary>Actual accepted spawn/lifecycle/AI/route owners. Geometry scenarios use
/// the installed retail assets and explicit current host placeables, not native
/// runtime observations. Source11e9d0 boundaries are independently specified.</summary>
[Collection("GoldenDataManager")]
public sealed class SkyTempleNpcReturnTests
{
    private const string Profiles=@"C:\Users\ryanf\Documents\Aion-NPC-Discovery\implementation\pilot25\data\sky-temple-cohort.json";
    private const string Root=@"D:\Aion58ServerTesting\Server\Map\Worlds\IDDF2Flying";
    private static void Require()=>Skip.IfNot(File.Exists(Profiles),"Pinned actual25 profile fixture required");
    private sealed class GeoScope:IDisposable
    {
        private readonly Dictionary<int,GeoMap> maps=(Dictionary<int,GeoMap>)typeof(GeoService)
            .GetField("geoMaps",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(GeoService.GetInstance())!;
        private readonly KeyValuePair<int,GeoMap>[] before;
        internal GeoScope()=>before=maps.ToArray();
        public void Dispose(){maps.Clear();foreach(var p in before)maps.Add(p.Key,p.Value);}
    }
    private sealed class CurrentPlaceable:DespawnableNode
    {
        internal CurrentPlaceable(int editor,RetailSearchPoint min,RetailSearchPoint max)
        {id=editor;type=DespawnableType.PLACEABLE;worldBound=new BoundingBox(new Vector3f(min.X,min.Y,min.Z),new Vector3f(max.X,max.Y,max.Z));}
    }
    private sealed class Fixture:IDisposable
    {
        private readonly IDisposable profiles;
        private readonly GeoScope maps=new();
        private readonly IDisposable? scene;
        internal readonly BossAiHarness H;
        internal readonly Npc Npc;
        internal readonly Player Target;
        internal readonly RetailSearchPoint Birth;
        internal readonly SkyTempleNpcNavigationAssets? Assets;
        internal uint Now=1000;
        internal Fixture(bool geometry=false,int id=212769,float z=374.58f,float birthY=406.25f)
        {
            profiles=SkyTempleNpcProfileCatalog.InstallForTests(File.ReadAllBytes(Profiles));
            var builder=BossAiHarness.For(320050000).WithWorldSize(512);
            if(geometry)builder.WithWalkerRoutes();
            H=builder.Build();SystemClock.UseSource(()=>Now);
            if(geometry)
            {
                Assets=SkyTempleNpcNavigationAssets.Load(Environment.GetEnvironmentVariable(SkyTempleNpcNavigationAssets.RootEnvironment)??Root);
                var declared=Assets.Scene(1,new[]{(15,false),(16,false),(17,false)});
                var map=GeoService.GetInstance().GetMap(320050000);
                foreach(int editor in new[]{15,16,17})
                {var f=declared.Field(new((uint)editor,RetailFieldObjectGeometry.SupportedClass,declared.Declaration.SceneRevision));map.AttachChild(new CurrentPlaceable(editor,f.Minimum,f.Maximum));}
                Assets.ValidateLoadedMap();scene=SkyTempleNpcNavigationAssets.InstallForTests(Assets);
            }
            Npc=H.SpawnWithAi(id,"general",391.25f,birthY,z);Birth=SkyTempleNpcNavigation.Position(Npc);
            Target=H.SpawnPlayer(391.25f,404.25f,374.58f);H.Engage(Npc,Target);
        }
        internal void Move(RetailSearchPoint p)=>H.World.UpdatePosition(Npc,p.X,p.Y,p.Z,0,false);
        internal SkyTempleNpcReturnState.Frame Begin(bool path=false)
        {
            Assert.True(path?SkyTempleNpcReturnState.PreparePathFailure(Npc):SkyTempleNpcReturnState.PrepareChaseTimeout(Npc));
            Npc.GetController().LoseAggro(false);Npc.GetAi().SetStateIfNot(AIState.RETURNING);
            Assert.True(SkyTempleNpcReturnState.TryRead(Npc,out var frame));Assert.True(frame.Enter(Npc));return frame;
        }
        public void Dispose(){H.Dispose();scene?.Dispose();maps.Dispose();profiles.Dispose();SystemClock.UseSystemClock();}
    }
    [SkippableFact]
    public void AcceptedBirthIsNeitherMutableSpawnTemplateNorPreFightPatrolPose()
    {
        Require();using var f=new Fixture();var birth=f.Birth;
        var spawn=Assert.IsType<SpawnTemplate>(f.Npc.GetSpawn());spawn.SetX(10);spawn.SetY(20);spawn.SetZ(30);
        f.Move(birth with{X=birth.X+8});var frame=f.Begin();
        Assert.Equal(birth,frame.Birth);Assert.Equal(birth,frame.Goal);Assert.False(frame.UsesPatrol);
    }
    [Theory]
    [InlineData(1000u,61000u,false)]
    [InlineData(1000u,61001u,true)]
    [InlineData(0u,70000u,false)]
    [InlineData(4294966795u,59499u,false)]
    [InlineData(4294966795u,59500u,true)]
    public void NumericReturnUsesStrictUnsignedSixtySecondBoundaryAndZeroSentinel(uint start,uint now,bool expected)
        =>Assert.Equal(expected,SkyTempleNpcReturnState.Expired(start,now));
    [Theory]
    [InlineData(Aion.GameServer.SkillEngine.Model.ActivationAttribute.ACTIVE,Aion.GameServer.SkillEngine.Model.SkillTargetSlot.BUFF,true)]
    [InlineData(Aion.GameServer.SkillEngine.Model.ActivationAttribute.PROVOKED,Aion.GameServer.SkillEngine.Model.SkillTargetSlot.DEBUFF,true)]
    [InlineData(Aion.GameServer.SkillEngine.Model.ActivationAttribute.MAINTAIN,Aion.GameServer.SkillEngine.Model.SkillTargetSlot.NONE,true)]
    [InlineData(Aion.GameServer.SkillEngine.Model.ActivationAttribute.TOGGLE,Aion.GameServer.SkillEngine.Model.SkillTargetSlot.NOSHOW,true)]
    [InlineData(Aion.GameServer.SkillEngine.Model.ActivationAttribute.CHARGE,Aion.GameServer.SkillEngine.Model.SkillTargetSlot.SPEC,true)]
    [InlineData(Aion.GameServer.SkillEngine.Model.ActivationAttribute.ACTIVE,Aion.GameServer.SkillEngine.Model.SkillTargetSlot.BOOST,false)]
    [InlineData(Aion.GameServer.SkillEngine.Model.ActivationAttribute.PASSIVE,Aion.GameServer.SkillEngine.Model.SkillTargetSlot.BUFF,false)]
    [InlineData(Aion.GameServer.SkillEngine.Model.ActivationAttribute.NONE,Aion.GameServer.SkillEngine.Model.SkillTargetSlot.DEBUFF,false)]
    public void SourceReturnMaskUsesNamedActivationsAndNamedBoostExclusion(Aion.GameServer.SkillEngine.Model.ActivationAttribute a,
        Aion.GameServer.SkillEngine.Model.SkillTargetSlot slot,bool expected)=>Assert.Equal(expected,SkyTempleNpcReturnState.RemovesEffect(a,slot));
    [SkippableFact]
    public void FirstReturnTickSurvivesRouteCancellationAndTickZeroRemainsUnstarted()
    {
        Require();using var f=new Fixture();f.Move(f.Birth with{X=f.Birth.X+2});var frame=f.Begin();
        Assert.Equal(SkyTempleNpcReturnState.Control.Move,frame.Tick(f.Npc,0));Assert.Equal(0u,frame.StartTick);
        frame.Tick(f.Npc,100);f.Npc.GetMoveController().RetailNavigation.Cancel();Assert.Equal(100u,frame.StartTick);
        Assert.Equal(SkyTempleNpcReturnState.Control.Move,frame.Tick(f.Npc,60100));
        Assert.Equal(SkyTempleNpcReturnState.Control.RemoveReason16,frame.Tick(f.Npc,60101));
    }
    [SkippableFact]
    public void ActualGoalEqualityWinsBeforeExpiryWithoutGivingHp()
    {
        Require();using var f=new Fixture();f.Move(f.Birth with{X=f.Birth.X+2});var frame=f.Begin();frame.Tick(f.Npc,1000);
        Assert.Equal(SkyTempleNpcReturnState.Control.RemoveReason16,frame.Tick(f.Npc,61001));
        int hp=f.Npc.GetLifeStats().GetMaxHp()-100;f.Npc.GetLifeStats().SetCurrentHp(hp);f.Move(f.Birth);
        Assert.Equal(SkyTempleNpcReturnState.Control.Complete,frame.Tick(f.Npc,61001));
        Assert.False(frame.Remove(f.Npc));
        ReturningEventHandler.OnBackHome((NpcAI)f.Npc.GetAi());
        Assert.Equal(hp,f.Npc.GetLifeStats().GetCurrentHp());Assert.NotEqual(AIState.RETURNING,f.Npc.GetAi().GetState());
        Assert.False(SkyTempleNpcReturnState.TryRead(f.Npc,out _));
    }
    [SkippableFact]
    public void JavaOneMetreBirthFastPathDoesNotSkipAnOwnedReturn()
    {
        Require();using var f=new Fixture();f.Move(f.Birth with{X=f.Birth.X+.5f});
        TargetEventHandler.OnTargetGiveup((NpcAI)f.Npc.GetAi());
        Assert.Equal(AIState.RETURNING,f.Npc.GetAi().GetState());Assert.True(SkyTempleNpcReturnState.TryRead(f.Npc,out var frame));
        Assert.Equal(SkyTempleNpcReturnState.Control.Move,frame.Tick(f.Npc,1000));Assert.Equal(f.Birth,frame.Goal);
    }
    [SkippableFact]
    public void Reason16UsesActualHostDeletionAndRetiresTheLife()
    {
        Require();using var f=new Fixture();f.Move(f.Birth with{X=f.Birth.X+2});var frame=f.Begin();frame.Tick(f.Npc,1000);
        long life=f.Npc.GetController().RetailLifeEpoch;
        Assert.Equal(SkyTempleNpcReturnState.Control.RemoveReason16,frame.Tick(f.Npc,61001));Assert.True(frame.Remove(f.Npc));
        Assert.False(f.Npc.IsSpawned());Assert.NotEqual(life,f.Npc.GetController().RetailLifeEpoch);Assert.False(frame.IsCurrent(f.Npc));
    }
    [SkippableFact]
    public void DespawnRespawnRejectsTheOldGoalAndCapturesTheNewAcceptedBirth()
    {
        Require();using var f=new Fixture();f.Move(f.Birth with{X=f.Birth.X+2});var old=f.Begin();
        f.H.World.Despawn(f.Npc);var accepted=f.Birth with{X=f.Birth.X+3};
        Assert.True(f.H.World.SetPosition(f.Npc,320050000,1,accepted.X,accepted.Y,accepted.Z,0));
        f.H.World.Spawn(f.Npc);f.H.Engage(f.Npc,f.Target);
        Assert.False(old.IsCurrent(f.Npc));var next=f.Begin();Assert.Equal(accepted,next.Birth);Assert.Equal(accepted,next.Goal);
        Assert.Equal(0u,next.StartTick);Assert.False(old.Remove(f.Npc));
    }
    [SkippableFact]
    public void ActualRetargetAndNewEncounterInvalidateAnOldReturnCapability()
    {
        Require();using var f=new Fixture();var old=f.Begin();var other=f.H.SpawnPlayer(392.25f,404.25f,374.58f);
        f.Npc.SetTarget(other);Assert.False(old.IsCurrent(f.Npc));Assert.False(old.Complete(f.Npc));
        SkyTempleNpcReturnState.EncounterStarted(f.Npc);Assert.False(SkyTempleNpcReturnState.TryRead(f.Npc,out _));
    }
    private static Effect PutRegistration(Fixture f,string stack,SkillTargetSlot slot)
    {
        // Declared registration-only fixture: the actual protected Put owner runs
        // its writer/stamp path. This does not pretend an entire cast succeeded.
        var template=new SkillTemplate{skillId=1000+stack[0],name=stack,stack=stack,targetSlot=slot,activationAttribute=ActivationAttribute.ACTIVE};
        var effect=new Effect(f.Npc,f.Npc,template,1);
        typeof(EffectController).GetMethod("Put",BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(Effect)},null)!.Invoke(f.Npc.GetEffectController(),new object[]{effect});
        return effect;
    }
    [SkippableFact]
    public void ActualRegistryStampTracksStableEraseReplacementAndBankFirstOrder()
    {
        Require();using var f=new Fixture();var ec=f.Npc.GetEffectController();
        var a=PutRegistration(f,"A",SkillTargetSlot.BUFF);var b=PutRegistration(f,"B",SkillTargetSlot.BUFF);
        var c=PutRegistration(f,"C",SkillTargetSlot.BUFF);var d=PutRegistration(f,"D",SkillTargetSlot.BUFF);
        ec.ClearEffect(b,false);Assert.True(SkyTempleNpcReturnState.TryCleanupOrder(f.Npc,out var order));Assert.Equal(new[]{d,c,a},order);
        ec.ClearEffect(c,false);var c2=PutRegistration(f,"C",SkillTargetSlot.BUFF);
        Assert.True(SkyTempleNpcReturnState.TryCleanupOrder(f.Npc,out order));Assert.Equal(new[]{c2,d,a},order);
        ec.ClearEffect(d,false);ec.ClearEffect(c2,false);var debuff=PutRegistration(f,"B2",SkillTargetSlot.DEBUFF);
        var buff=PutRegistration(f,"C3",SkillTargetSlot.BUFF);
        Assert.True(SkyTempleNpcReturnState.TryCleanupOrder(f.Npc,out order));Assert.Equal(new[]{buff,a,debuff},order);
    }
    [SkippableFact]
    public void UnknownActualRegistrationCannotAcquireCleanupProvenanceFromEnumeration()
    {
        Require();using var f=new Fixture();
        var template=new SkillTemplate{skillId=1234,name="unknown",stack="unknown",targetSlot=SkillTargetSlot.BUFF,activationAttribute=ActivationAttribute.ACTIVE};
        var effect=new Effect(f.Npc,f.Npc,template,1);
        var map=(Dictionary<string,Effect>)typeof(EffectController).GetField("abnormalEffectMap",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(f.Npc.GetEffectController())!;
        map["unknown"]=effect;
        try{Assert.False(SkyTempleNpcReturnState.TryCleanupOrder(f.Npc,out var order));Assert.Empty(order);}
        finally{map.Remove("unknown");}
    }
    [SkippableFact]
    public void CommittedExpiryIdentitySurvivesIntentionalAbnormalClearButNotReplacementTarget()
    {
        Require();using var f=new Fixture();f.Now=16000;
        Assert.True(SkyTempleNpcSkillState.TryEvaluateChase(f.Npc,out var decision));
        lock(f.Npc.GetMoveController())Assert.True(decision.TryClaimExpiry(f.Npc));
        f.Npc.GetEffectController().UnsetAbnormal(Aion.GameServer.SkillEngine.Effects.AbnormalState.STUN);
        Assert.Equal(31000u,SkyTempleNpcSkillState.GetChaseSnapshot(f.Npc).Deadline);
        lock(f.Npc.GetMoveController())Assert.True(decision.ClaimedIdentityIsCurrent(f.Npc));
        f.Npc.SetTarget(f.H.SpawnPlayer(392.25f,404.25f,374.58f));
        lock(f.Npc.GetMoveController())Assert.False(decision.ClaimedIdentityIsCurrent(f.Npc));
    }
    [SkippableFact]
    public void ActualMissingGoalSearchDoesNotBecomeAnUnprovedReturnWarp()
    {
        Require();Skip.IfNot(File.Exists(Path.Combine(Root,"IDDF2Flying-path.dat")),"Actual geometry required");
        using var f=new Fixture(true,z:380);f.Move(f.Birth with{Z=374.58f});var frame=f.Begin(true);
        var before=SkyTempleNpcNavigation.Position(f.Npc);
        var result=f.Npc.GetMoveController().RetailNavigation.Tick(false,frame.Goal,1000);
        Assert.Equal(SkyTempleMoveKind.Blocked,result.Kind);Assert.Contains("MissingGoal",result.Detail!);
        Assert.Equal(before,SkyTempleNpcNavigation.Position(f.Npc));Assert.True(frame.IsCurrent(f.Npc));
    }
    [SkippableFact]
    public void ActualComputedFollowFourRequestsOwnedWarpThenASeparatePositionCheckedCompletionWithoutHpGrant()
    {
        Require();Skip.IfNot(File.Exists(Path.Combine(Root,"IDDF2Flying-path.dat")),"Actual geometry required");
        using var f=new Fixture(true,birthY:398.25f);f.Move(new(391.25f,406.25f,374.58f));var frame=f.Begin(true);
        int hp=f.Npc.GetLifeStats().GetMaxHp()-100;f.Npc.GetLifeStats().SetCurrentHp(hp);
        var move=f.Npc.GetMoveController();move.ReturnToLastStepOrSpawn();move.MoveToDestination();
        Assert.False(SkyTempleNpcReturnState.SamePoint(SkyTempleNpcNavigation.Position(f.Npc),frame.Goal));
        // Deliberate1ms owner invocation exercises native budget<.1/raw4. The
        // production owner remains its existing200ms task; no second scheduler.
        f.Now=1001;move.MoveToDestination();
        Assert.True(SkyTempleNpcReturnState.SamePoint(SkyTempleNpcNavigation.Position(f.Npc),frame.Goal),move.RetailNavigation.LastFailure);
        Assert.Equal(AIState.RETURNING,f.Npc.GetAi().GetState());Assert.Equal(hp,f.Npc.GetLifeStats().GetCurrentHp());
        f.Now=1200;move.MoveToDestination();Assert.NotEqual(AIState.RETURNING,f.Npc.GetAi().GetState());
        Assert.Equal(hp,f.Npc.GetLifeStats().GetCurrentHp());
    }
    [SkippableFact]
    public void SourceAdmittedSelectedPatrolGoalDiffersFromPathFailureBirthAndListReplacementRetiresIt()
    {
        Require();Skip.IfNot(File.Exists(Path.Combine(Root,"IDDF2Flying-path.dat")),"Actual geometry/source required");
        using var f=new Fixture(true,212761);
        var spawns=Aion.GameServer.Dataholders.LoadingUtils.JaxbHolderLoader.DeserializeFile<SpawnsData>(Path.Combine(BossAiHarness.RepoRoot(),"game-server","data","static_data","spawns","Instances","320050000_Sky_Temple_Interior.xml"));
        spawns.Initialize();typeof(StaticData).GetProperty(nameof(StaticData.SpawnsDh))!.SetValue(f.H.Data.StaticData,spawns);
        const string route="D7ABFE03C4A9D9355860974445C0CFDDB94524B4";
        Assert.IsType<SpawnTemplate>(f.Npc.GetSpawn()).SetWalkerId(route);var move=f.Npc.GetMoveController();
        Assert.True(move.SetWalkerTemplate(DataManager.WALKER_DATA.GetWalkerTemplate(route),0));
        var template=move.GetWalkerTemplate();var selected=template.GetRouteStep(1);move.SetRouteStep(selected);
        Assert.True(SkyTempleNpcReturnState.PrepareChaseTimeout(f.Npc));Assert.True(SkyTempleNpcReturnState.TryRead(f.Npc,out var patrol));
        Assert.True(patrol.UsesPatrol);Assert.Equal(new RetailSearchPoint(selected.GetX(),selected.GetY(),selected.GetZ()),patrol.Goal);Assert.NotEqual(f.Birth,patrol.Goal);
        Assert.True(SkyTempleNpcReturnState.PreparePathFailure(f.Npc));Assert.True(SkyTempleNpcReturnState.TryRead(f.Npc,out var birth));
        Assert.False(birth.UsesPatrol);Assert.Equal(f.Birth,birth.Goal);Assert.False(patrol.IsCurrent(f.Npc));
        Assert.True(SkyTempleNpcReturnState.PrepareChaseTimeout(f.Npc));Assert.True(SkyTempleNpcReturnState.TryRead(f.Npc,out patrol));
        template.SetRouteSteps(template.GetRouteSteps().ToList());Assert.False(patrol.IsCurrent(f.Npc));Assert.False(SkyTempleNpcReturnState.TryRead(f.Npc,out _));
    }
}
