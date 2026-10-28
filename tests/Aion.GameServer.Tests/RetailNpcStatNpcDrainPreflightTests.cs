using System.Collections.Immutable;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;
using Xunit;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcStatNpcDrainPreflightTests
{
    private sealed class Owner:IStatOwner;
    private sealed class Protection:AttackCalcObserver
    {
        internal int Calls;
        public override void CheckShield(List<AttackResult> attacks,Effect effect,Creature attacker)=>Calls++;
    }

    [Fact]
    public void PreparedHitWaitsForObserverOwnerAndRejectsNewProtectionWithoutConsumingIt()
    {
        using var h=BossAiHarness.For(220030000).Build();
        var caster=h.SpawnWithAi(210548,"general");var target=h.SpawnWithAi(210548,"general",x:981);
        var effect=new Effect(caster,target,new SkillTemplate {skillId=910071},1);
        var observer=target.GetObserveController();var protection=new Protection();
        var gate=typeof(Aion.GameServer.Controllers.ObserveController).GetField("_attackCalcLock",
            System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(observer)!;
        using var started=new ManualResetEventSlim();Task<bool> attempt;
        lock(gate)
        {
            attempt=Task.Run(()=> {started.Set();return observer.TryCalculateUnprotectedNpcDrainForOfflineFixture(effect,target,500,1);});
            Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
            observer.AddAttackCalcObserver(protection);
            Assert.False(attempt.Wait(TimeSpan.FromMilliseconds(100)));
        }
        Assert.False(attempt.GetAwaiter().GetResult());Assert.Equal(0,protection.Calls);
        Assert.Empty(effect.GetSuccessEffects());
        observer.RemoveAttackCalcObserver(protection);
        Assert.True(observer.TryCalculateUnprotectedNpcDrainForOfflineFixture(effect,target,500,1));
        Assert.Equal(500,effect.GetReserveds(1).GetValue());Assert.Equal(0,protection.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreparedTickWaitsForObserverOwnerAndRejectsNewProtection(bool missingCaster)
    {
        using var h=BossAiHarness.For(220030000).Build();
        var caster=h.SpawnWithAi(210548,"general");var target=h.SpawnWithAi(210548,"general",x:981);
        var effect=new Effect(caster,target,new SkillTemplate {skillId=910072},1);
        var attack=new AttackResult(500,AttackStatus.NORMALHIT,HitType.MAHIT);
        var observer=target.GetObserveController();var protection=new Protection();
        var gate=typeof(Aion.GameServer.Controllers.ObserveController).GetField("_attackCalcLock",
            System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(observer)!;
        using var started=new ManualResetEventSlim();Task<bool> attempt;
        lock(gate)
        {
            attempt=Task.Run(()=> {started.Set();return observer.TryCheckUnprotectedNpcDrainTickForOfflineFixture([attack],effect,missingCaster?null:caster);});
            Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
            observer.AddAttackCalcObserver(protection);
            Assert.False(attempt.Wait(TimeSpan.FromMilliseconds(100)));
        }
        Assert.False(attempt.GetAwaiter().GetResult());Assert.Equal(0,protection.Calls);Assert.Equal(500,attack.GetDamage());
        observer.RemoveAttackCalcObserver(protection);
        Assert.True(observer.TryCheckUnprotectedNpcDrainTickForOfflineFixture([attack],effect,missingCaster?null:caster));
        Assert.Equal(500,attack.GetDamage());Assert.Equal(0,protection.Calls);
    }

    [Fact]
    public void DrainTickReceiptRejectsInterveningChangesAndGuardsOnlyHpCommit()
    {
        using var h=BossAiHarness.For(220030000).Build();
        var caster=h.SpawnWithAi(210548,"general");var target=h.SpawnWithAi(210548,"general",x:981);
        var effect=new Effect(caster,target,new SkillTemplate {skillId=910073},1);
        var observer=target.GetObserveController();var protection=new Protection();
        var attack=new AttackResult(500,AttackStatus.NORMALHIT,HitType.MAHIT);
        Assert.True(observer.TryCheckUnprotectedNpcDrainTickForOfflineFixture([attack],effect,caster,out var first));
        Assert.NotNull(first);
        Assert.Throws<InvalidOperationException>(()=>observer.EnterNpcDrainTickHpForOfflineFixture(first,target));
        observer.AddAttackCalcObserver(protection);observer.RemoveAttackCalcObserver(protection);
        lock(target.GetLifeStats())
            Assert.Throws<Aion.GameServer.Controllers.ObserveController.NpcDrainProtectionChangedException>(
                ()=>observer.EnterNpcDrainTickHpForOfflineFixture(first,target));
        Assert.True(observer.TryCheckUnprotectedNpcDrainTickForOfflineFixture([attack],effect,caster,out var current));
        Assert.NotNull(current);
        using var started=new ManualResetEventSlim();Task? writer=null;
        lock(target.GetLifeStats())
        {
            using(observer.EnterNpcDrainTickHpForOfflineFixture(current,target))
            {
                writer=Task.Run(()=> {started.Set();observer.AddAttackCalcObserver(protection);});
                Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
                Assert.False(writer.Wait(TimeSpan.FromMilliseconds(100)));
            }
        }
        Assert.True(writer!.Wait(TimeSpan.FromSeconds(5)));Assert.True(observer.HasDrainProtectionObserver);
        observer.RemoveAttackCalcObserver(protection);Assert.Equal(0,protection.Calls);
    }

    [Fact]
    public void InitialDrainDoesNotWaitForCasterHpLockWhileHoldingTargetHpLock()
    {
        using var h=BossAiHarness.For(220030000).Build();
        var caster=h.SpawnWithAi(210548,"general");var target=h.SpawnWithAi(210548,"general",x:981);
        var effect=new Effect(caster,target,new SkillTemplate {skillId=910074},1);
        var observer=target.GetObserveController();
        Assert.True(observer.TryCalculateUnprotectedNpcDrainForOfflineFixture(effect,target,500,1,out var protection));
        Assert.NotNull(protection);
        var casterLife=caster.GetLifeStats();var targetLife=target.GetLifeStats();
        long generation=casterLife.CaptureRetailDrainLife();
        var program=new RetailNpcStatSpellDrain.NpcPeriodicProgram(1,IsNativeEligible:n=>casterLife.IsRetailDrainLifeEligible(generation),
            CasterLife:caster.GetController().RetailLifeEpoch,TargetLife:target.GetController().RetailLifeEpoch,
            TargetLifeStats:targetLife,InitialProtection:protection,CasterLifeStats:casterLife);
        var dispatch=new RetailNpcStatSpellDrain.NpcInitialDispatch(effect,caster,target,500,program);dispatch.Claim(target);
        using var held=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
        var other=Task.Run(()=> {lock(casterLife) {held.Set();release.Wait(TimeSpan.FromSeconds(5));}});
        try
        {
            Assert.True(held.Wait(TimeSpan.FromSeconds(5)));
            lock(targetLife)
            {
                var error=Assert.ThrowsAny<InvalidOperationException>(()=>dispatch.EnterHp(target));
                Assert.Contains("caster life busy",error.Message);
            }
        }
        finally {release.Set();Assert.True(other.Wait(TimeSpan.FromSeconds(5)));}
    }

    [Theory]
    [InlineData(1000, 500, 150, true)]
    [InlineData(1, 10737418, 200, true)]
    [InlineData(1, 10737419, 200, false)]
    [InlineData(2147482897, 500, 150, true)]
    [InlineData(2147482898, 500, 150, false)]
    [InlineData(int.MaxValue, 1, 100, false)]
    [InlineData(int.MaxValue, 1, 0, true)]
    [InlineData(0, 1, 100, false)]
    [InlineData(1, 0, 100, false)]
    [InlineData(1, 1, -1, false)]
    public void PeriodicHealSnapshotRejectsNativeMultiplicationAndHpAdditionOverflow(
        int casterMaxHp,int targetMaxHp,int percent,bool expected)
        => Assert.Equal(expected,RetailNpcStatNpcDrainPreflight.PeriodicHealFitsSnapshot(casterMaxHp,targetMaxHp,percent));

    [Theory]
    [InlineData(1000,70500,200,true)]
    [InlineData(1,10737418,200,true)]
    [InlineData(1,10737419,200,false)]
    [InlineData(2147342647,70500,200,true)]
    [InlineData(2147342648,70500,200,false)]
    [InlineData(int.MaxValue,0,200,true)]
    [InlineData(1000,-1,200,false)]
    [InlineData(1000,1,-1,false)]
    public void InitialHealSnapshotUsesReservedDamageAndRejectsOverflow(
        int casterMaxHp,int reservedDamage,int percent,bool expected)
        => Assert.Equal(expected,RetailNpcStatNpcDrainPreflight.InitialHealFitsSnapshot(casterMaxHp,reservedDamage,percent));

    [SkippableFact]
    public void PairAndActorsMustMatchWithoutConsumingTheSkillOrChangingHp()
    {
        string? manifest=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(manifest)||string.IsNullOrEmpty(hash),"Requires pinned selected retail package");
        using var installed=Aion.GameServer.Retail.RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(manifest!),hash!,Path.GetDirectoryName(manifest!)!);
        using var h=BossAiHarness.For(120010000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var caster=h.SpawnWithAi(216520,"retail_stat_base_fixture");
        var target=h.SpawnWithAi(281451,"retail_stat_base_fixture",x:981);
        var template=DataManager.SKILL_DATA.GetSkillTemplate(18726);
        var skill=new Skill(template,caster,1,target,null!);
        var effect=new Effect(skill,target);
        int casterHp=caster.GetLifeStats().GetCurrentHp(),targetHp=target.GetLifeStats().GetCurrentHp();
        Assert.True(RetailNpcStatNpcDrainPreflight.TryMatchInputs(effect,out var selected,out string reason),reason);
        Assert.Equal("matched-inputs-not-enabled",reason);
        Assert.Equal(48,selected.SourceLevel);
        Assert.Equal(45000,selected.Initial.Value);
        Assert.Equal(75000,selected.Source.InitialDrain!.Value);
        Assert.Equal(casterHp,caster.GetLifeStats().GetCurrentHp());
        Assert.Equal(targetHp,target.GetLifeStats().GetCurrentHp());
        Assert.Empty(effect.GetSuccessEffects());
        var hpField=typeof(CreatureLifeStats).GetField("currentHp",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
        hpField.SetValue(target.GetLifeStats(),-1);Assert.False(target.IsDead());
        Assert.False(RetailNpcStatNpcDrainPreflight.TryMatchInputs(effect,out _,out string negativeTarget));
        Assert.Equal("actor-context",negativeTarget);hpField.SetValue(target.GetLifeStats(),targetHp);
        hpField.SetValue(caster.GetLifeStats(),-1);Assert.False(caster.IsDead());
        Assert.False(RetailNpcStatNpcDrainPreflight.TryMatchInputs(effect,out _,out string negativeCaster));
        Assert.Equal("actor-context",negativeCaster);hpField.SetValue(caster.GetLifeStats(),casterHp);
        Assert.True(RetailNpcStatNpcDrainInitial.TryCalculate(effect,1,out var initial,out string initialReason),initialReason);
        Assert.Equal("calculated-inputs-not-enabled",initialReason);
        Assert.Equal(75000,initial.Base);Assert.Equal(70500,initial.Damage);
        Assert.True(RetailNpcStatNpcDrainInitial.TryCalculate(effect,1000,out var highRoll,out _));
        Assert.Equal(initial.Damage,highRoll.Damage);
        Assert.False(RetailNpcStatNpcDrainInitial.TryCalculate(effect,0,out _,out string invalidRoll));
        Assert.Equal("critical-roll-outside-native-range",invalidRoll);
        // Supported retail HP scalar modifiers may change either actor's
        // current maximum without making its source identity unreviewed.
        var casterHpBuff=new Owner();var targetHpBuff=new Owner();
        caster.GetGameStats().AddEffectOnly(casterHpBuff,[new StatAddFunction(StatEnum.MAXHP,2000,true)]);
        target.GetGameStats().AddEffectOnly(targetHpBuff,[new StatAddFunction(StatEnum.MAXHP,3000,true)]);
        Assert.True(RetailNpcStatNpcDrainPreflight.TryMatchInputs(effect,out var buffed,out string buffedReason),buffedReason);
        Assert.Equal(selected.CasterMaxHp+2000,buffed.CasterMaxHp);
        Assert.Equal(selected.TargetMaxHp+3000,buffed.TargetMaxHp);
        Assert.True(RetailNpcStatNpcDrainInitial.TryCalculate(effect,1,out var buffedHit,out string buffedHitReason),buffedHitReason);
        Assert.Equal(initial.Damage,buffedHit.Damage);
        Assert.Empty(effect.GetSuccessEffects());
        caster.GetGameStats().EndEffect(casterHpBuff);target.GetGameStats().EndEffect(targetHpBuff);
        var largeTargetBuff=new Owner();
        target.GetGameStats().AddEffectOnly(largeTargetBuff,[new StatAddFunction(StatEnum.MAXHP,20000000,true)]);
        Assert.False(RetailNpcStatNpcDrainPreflight.TryMatchInputs(effect,out _,out string overflowReason));
        Assert.Equal("unreviewed-periodic-drain-heal-overflow",overflowReason);
        target.GetGameStats().EndEffect(largeTargetBuff);
        var modifierOwner=new Owner();
        caster.GetGameStats().AddEffectOnly(modifierOwner,[new StatAddFunction(StatEnum.BOOST_SPELL_ATTACK,200,true)]);
        Assert.True(RetailNpcStatNpcDrainInitial.TryCalculate(effect,1,out var modified,out string modifiedReason),modifiedReason);
        Assert.Equal(200,modified.Modifiers.Flat);Assert.Equal(75200,modified.Modified);Assert.Equal(70688,modified.Damage);
        var unsupportedOwner=new Owner();
        caster.GetGameStats().AddEffectOnly(unsupportedOwner,[new StatSetFunction(StatEnum.BOOST_SPELL_ATTACK,1)]);
        Assert.False(RetailNpcStatNpcDrainInitial.TryCalculate(effect,1,out _,out string unsupportedReason));
        Assert.Equal("unreviewed-initial-npc-spell-modifier",unsupportedReason);
        caster.GetGameStats().EndEffect(unsupportedOwner);caster.GetGameStats().EndEffect(modifierOwner);
        var hugeAttack=new Owner();
        caster.GetGameStats().AddEffectOnly(hugeAttack,[new StatAddFunction(StatEnum.BOOST_SPELL_ATTACK,20000000,true)]);
        Assert.False(RetailNpcStatNpcDrainInitial.TryCalculate(effect,1,out _,out string initialOverflow));
        Assert.Equal("unreviewed-initial-npc-drain-heal-overflow",initialOverflow);
        caster.GetGameStats().EndEffect(hugeAttack);
        Assert.Empty(effect.GetSuccessEffects());
        Assert.Equal(casterHp,caster.GetLifeStats().GetCurrentHp());Assert.Equal(targetHp,target.GetLifeStats().GetCurrentHp());

        var source=selected.Source;var first=source.InitialDrain!;var part=Assert.Single(source.Parts);var later=part.Drain!;
        string? Check(RetailNpcStatPeriodicCombat.Source candidate) => RetailNpcStatNpcDrainPreflight.RecipeMismatch(
            candidate,selected.Initial,selected.Periodic,template.GetProperties());
        Assert.Null(Check(source));
        Assert.Equal("unreviewed-initial-drain-source",Check(source with {InitialDrain=first with {HpRate=1}}));
        Assert.Equal("unreviewed-drain-source-field",Check(source with {InitialDrain=first with {
            OrderedPrimaryFields=first.OrderedPrimaryFields.Add(new("effect1_unreviewed","1"))}}));
        var changedPart=part with {Drain=later with {PreEffectProbability2=99}};
        Assert.Equal("unreviewed-periodic-drain-source",Check(source with {Parts=ImmutableArray.Create(changedPart)}));
        changedPart=part with {Drain=later with {OrderedFields=later.OrderedFields.Add(new("effect2_unreviewed","1"))}};
        Assert.Equal("unreviewed-drain-source-field",Check(source with {Parts=ImmutableArray.Create(changedPart)}));
        changedPart=part with {Drain=later with {EffectId=later.EffectId+1}};
        Assert.Equal("periodic-host-shape-differs",Check(source with {Parts=ImmutableArray.Create(changedPart)}));

        var selfSkill=new Skill(template,caster,1,caster,null!);
        Assert.False(RetailNpcStatNpcDrainPreflight.TryMatchInputs(new Effect(selfSkill,caster),out _,out _));
        var wrongTarget=new Effect(new Skill(template,caster,1,caster,null!),target);
        Assert.False(RetailNpcStatNpcDrainPreflight.TryMatchInputs(wrongTarget,out _,out string targetReason));
        Assert.Equal("host-first-target-differs",targetReason);
        var dirtyEffect=new Effect(new Skill(template,caster,1,target,null!),target);
        dirtyEffect.AddSuccessEffect(selected.Initial);
        Assert.False(RetailNpcStatNpcDrainPreflight.TryMatchInputs(dirtyEffect,out _,out string dirtyReason));
        Assert.Equal("already-calculated-effect",dirtyReason);
        Assert.False(RetailNpcStatNpcDrainInitial.TryCalculate(dirtyEffect,1,out _,out string dirtyInitial));
        Assert.Equal("already-calculated-effect",dirtyInitial);
        var retiredEffect=new Effect(new Skill(template,caster,1,target,null!),target);
        retiredEffect.EndEffect(false);
        Assert.False(RetailNpcStatNpcDrainPreflight.TryMatchInputs(retiredEffect,out _,out string retiredReason));
        Assert.Equal("retired-effect",retiredReason);
        var interrupted=new Effect(new Skill(template,caster,1,target,null!),target);
        Assert.False(RetailNpcStatNpcDrainInitial.TryPrepareOffline(interrupted,1,_=>
        {interrupted.EndEffect(false);return true;},out _,out string interruptedReason));
        Assert.Equal("retired-effect",interruptedReason);Assert.Empty(interrupted.GetSuccessEffects());
        // Equal numeric HP is insufficient if an unsupported setter produced it.
        caster.GetGameStats().AddEffectOnly(new Owner(),[new StatSetFunction(StatEnum.MAXHP,selected.CasterMaxHp)]);
        Assert.False(RetailNpcStatNpcDrainPreflight.TryMatchInputs(effect,out _,out string mismatch));
        Assert.Equal("unreviewed-actor-stats-or-hp-override",mismatch);
        Assert.Empty(h.Clock.Faults);
    }
}
