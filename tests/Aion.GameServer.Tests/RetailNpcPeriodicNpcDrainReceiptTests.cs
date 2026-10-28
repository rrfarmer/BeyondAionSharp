using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;
using Aion.GameServer.Network.Aion.ServerPackets;
using Xunit;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcPeriodicNpcDrainReceiptTests
{
    private sealed class Owner:IStatOwner;
    private sealed class TestShield:AttackCalcObserver
    {
        public override void CheckShield(List<AttackResult> attacks,Effect effect,Creature attacker)
            => throw new InvalidOperationException("Unreviewed shield must not be consumed");
    }

    [Aion.GameServer.Ai.AIName("retail_drain_protection_race_fixture")]
    public sealed class ShieldOnSpelledAI(Npc owner):Aion.GameServer.Ai.NpcAI(owner)
    {
        internal bool Armed,Added,RetireCaster;
        internal AttackCalcObserver? AppliedShield;
        internal Effect? EndDuringSpelled;
        internal bool ReplaceLifeDuringSpelled;
        public override bool CanThink()=>false;
        protected override void HandleSpawned()=>SetStateIfNot(Aion.GameServer.Ai.AIState.IDLE);
        protected override void HandleSpelled(Creature caster)
        {
            if(ReplaceLifeDuringSpelled)
            {
                ReplaceLifeDuringSpelled=false;
                var replacement=new FixtureLifeStats(GetOwner());replacement.SetCurrentHp(100000);
                GetOwner().SetLifeStats(replacement);return;
            }
            if(EndDuringSpelled is { } ending)
            {
                EndDuringSpelled=null;ending.EndEffect();return;
            }
            if(RetireCaster)
            {
                RetireCaster=false;caster.GetLifeStats().SetCurrentHp(0);caster.GetLifeStats().SetCurrentHp(100);
                return;
            }
            if(!Armed)return;
            Armed=false;Added=true;AppliedShield=new TestShield();GetOwner().GetObserveController().AddAttackCalcObserver(AppliedShield);
        }
    }

    [SkippableTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void PeriodicTickRejectsReplacedTargetLifeOwner(bool replaceDuringCallback)
    {
        string? manifest=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(manifest)||string.IsNullOrEmpty(hash),"Requires pinned selected retail package");
        using var installed=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(manifest!),hash!,Path.GetDirectoryName(manifest!)!);
        using var h=BossAiHarness.For(120010000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI),typeof(ShieldOnSpelledAI)).Build();
        var caster=h.SpawnWithAi(216520,"retail_stat_base_fixture");var target=h.SpawnWithAi(281451,"retail_drain_protection_race_fixture",x:981);
        var original=new FixtureLifeStats(target);target.SetLifeStats(original);
        var casterLife=new FixtureLifeStats(caster);caster.SetLifeStats(casterLife);
        original.SetCurrentHp(500000);casterLife.SetCurrentHp(100);
        var template=DataManager.SKILL_DATA.GetSkillTemplate(18726);var skill=new Skill(template,caster,1,target,null!);
        var effect=new Effect(skill,target);long generation=casterLife.CaptureRetailDrainLife();
        RetailNpcStatNpcDrainInitial.AttachOfflineInitialization(effect,1,n=>ReferenceEquals(n.GetLifeStats(),casterLife)
            &&casterLife.IsRetailDrainLifeEligible(generation));
        effect.Initialize();Assert.True(RetailNpcStatNpcDrainInitial.TryGetOfflineInitialization(effect,out var projection,out string reason),reason);
        Assert.NotNull(projection);effect.ApplyEffect();h.Clock.Advance(TimeSpan.FromMilliseconds(skill.GetHitTime()+1000));
        original.SetCurrentHp(100000);casterLife.SetCurrentHp(100);
        if(replaceDuringCallback)Assert.IsType<ShieldOnSpelledAI>(target.GetAi()).ReplaceLifeDuringSpelled=true;
        else {var replacement=new FixtureLifeStats(target);replacement.SetCurrentHp(100000);target.SetLifeStats(replacement);}
        h.Clock.Advance(TimeSpan.FromMilliseconds(3000));
        Assert.NotSame(original,target.GetLifeStats());Assert.Equal(100000,original.GetCurrentHp());
        Assert.Equal(100000,target.GetLifeStats().GetCurrentHp());Assert.Equal(100,casterLife.GetCurrentHp());Assert.Empty(h.Clock.Faults);
        effect.EndEffect();
    }

    [SkippableFact]
    public void PeriodicTickRetiredByEffectEndDoesNotDamageOrHeal()
    {
        string? manifest=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(manifest)||string.IsNullOrEmpty(hash),"Requires pinned selected retail package");
        using var installed=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(manifest!),hash!,Path.GetDirectoryName(manifest!)!);
        using var h=BossAiHarness.For(120010000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI),typeof(ShieldOnSpelledAI)).Build();
        var caster=h.SpawnWithAi(216520,"retail_stat_base_fixture");
        var target=h.SpawnWithAi(281451,"retail_drain_protection_race_fixture",x:981);
        var life=new FixtureLifeStats(target);target.SetLifeStats(life);
        var casterLife=new FixtureLifeStats(caster);caster.SetLifeStats(casterLife);
        life.SetCurrentHp(500000);casterLife.SetCurrentHp(100);
        var template=DataManager.SKILL_DATA.GetSkillTemplate(18726);
        var skill=new Skill(template,caster,1,target,null!);var effect=new Effect(skill,target);
        long generation=casterLife.CaptureRetailDrainLife();
        RetailNpcStatNpcDrainInitial.AttachOfflineInitialization(effect,1,n=>ReferenceEquals(n.GetLifeStats(),casterLife)
            &&casterLife.IsRetailDrainLifeEligible(generation));
        effect.Initialize();Assert.True(RetailNpcStatNpcDrainInitial.TryGetOfflineInitialization(effect,out var projection,out string reason),reason);
        Assert.NotNull(projection);effect.ApplyEffect();h.Clock.Advance(TimeSpan.FromMilliseconds(skill.GetHitTime()+1000));
        life.SetCurrentHp(100000);casterLife.SetCurrentHp(100);
        Assert.IsType<ShieldOnSpelledAI>(target.GetAi()).EndDuringSpelled=effect;
        h.Clock.Advance(TimeSpan.FromMilliseconds(3000));
        Assert.True(effect.RetailHasEnded);Assert.Equal(100000,life.GetCurrentHp());Assert.Equal(100,casterLife.GetCurrentHp());
        h.Clock.Advance(TimeSpan.FromMilliseconds(3000));
        Assert.Equal(100000,life.GetCurrentHp());Assert.Equal(100,casterLife.GetCurrentHp());Assert.Empty(h.Clock.Faults);
    }
    private sealed class FixtureLifeStats(Npc npc) : NpcLifeStats(npc)
    {
        internal bool HealAfterHit;
        internal int DamageEvents;
        internal bool CheckProtectionLock;
        protected override void OnHpChanged(SmAttackStatus.TYPE type,int previousHp,int newHp,Creature effector)
        {
            if(CheckProtectionLock)
            {
                var gate=typeof(Aion.GameServer.Controllers.ObserveController).GetField("_attackCalcLock",
                    System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(owner.GetObserveController())!;
                Assert.False(Monitor.IsEntered(gate));
            }
            // The HP commit stays real; fixture isolation avoids external death,
            // reward and respawn services during this offline controller test.
            if(newHp<previousHp)DamageEvents++;
            if(newHp>0 && newHp<previousHp && HealAfterHit)
            {
                HealAfterHit=false;
                SetCurrentHp(newHp+7,effector);
            }
        }
    }

    [SkippableFact]
    public void ShieldAddedAfterTickCalculationRejectsHpAndHeal()
    {
        string? manifest=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(manifest)||string.IsNullOrEmpty(hash),"Requires pinned selected retail package");
        using var installed=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(manifest!),hash!,Path.GetDirectoryName(manifest!)!);
        using var h=BossAiHarness.For(120010000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI),typeof(ShieldOnSpelledAI)).Build();
        var caster=h.SpawnWithAi(216520,"retail_stat_base_fixture");
        var target=h.SpawnWithAi(281451,"retail_drain_protection_race_fixture",x:981);
        var ai=Assert.IsType<ShieldOnSpelledAI>(target.GetAi());
        var life=new FixtureLifeStats(target) {CheckProtectionLock=true};target.SetLifeStats(life);
        caster.SetLifeStats(new FixtureLifeStats(caster));
        life.SetCurrentHp(500000);caster.GetLifeStats().SetCurrentHp(100);
        var template=DataManager.SKILL_DATA.GetSkillTemplate(18726);
        var skill=new Skill(template,caster,1,target,null!);var effect=new Effect(skill,target);
        var casterLife=caster.GetLifeStats();long generation=casterLife.CaptureRetailDrainLife();
        RetailNpcStatNpcDrainInitial.AttachOfflineInitialization(effect,1,n=>ReferenceEquals(n.GetLifeStats(),casterLife)
            &&casterLife.IsRetailDrainLifeEligible(generation));
        effect.Initialize();Assert.True(RetailNpcStatNpcDrainInitial.TryGetOfflineInitialization(effect,out var projection,out string reason),reason);
        Assert.NotNull(projection);effect.ApplyEffect();h.Clock.Advance(TimeSpan.FromMilliseconds(skill.GetHitTime()+1000));
        life.SetCurrentHp(100000);casterLife.SetCurrentHp(100);ai.Armed=true;
        h.Clock.Advance(TimeSpan.FromMilliseconds(3000));
        Assert.True(ai.Added);Assert.Equal(100000,life.GetCurrentHp());Assert.Equal(100,casterLife.GetCurrentHp());
        Assert.Empty(h.Clock.Faults);
        // The next tick sees the already-present shield before calculation;
        // it must also skip without consuming the unreviewed shield.
        h.Clock.Advance(TimeSpan.FromMilliseconds(3000));
        Assert.Equal(100000,life.GetCurrentHp());Assert.Equal(100,casterLife.GetCurrentHp());Assert.Empty(h.Clock.Faults);
        Assert.NotNull(ai.AppliedShield);target.GetObserveController().RemoveAttackCalcObserver(ai.AppliedShield);
        h.Clock.Advance(TimeSpan.FromMilliseconds(3000));
        Assert.True(life.GetCurrentHp()<100000);Assert.True(casterLife.GetCurrentHp()>100);Assert.Empty(h.Clock.Faults);
        effect.EndEffect();
    }

    [SkippableTheory]
    [InlineData(false)] // Caster HP can change after the initial hit.
    [InlineData(true)]  // The host caster can be given a different HP owner.
    public void DelayedInitialHealRejectsOverflowOrReplacedLifeOwner(bool replaceLife)
    {
        string? manifest=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(manifest)||string.IsNullOrEmpty(hash),"Requires pinned selected retail package");
        using var installed=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(manifest!),hash!,Path.GetDirectoryName(manifest!)!);
        using var h=BossAiHarness.For(120010000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var caster=h.SpawnWithAi(216520,"retail_stat_base_fixture");var target=h.SpawnWithAi(281451,"retail_stat_base_fixture",x:981);
        var targetLife=new FixtureLifeStats(target);target.SetLifeStats(targetLife);
        var casterLife=new FixtureLifeStats(caster);caster.SetLifeStats(casterLife);
        targetLife.SetCurrentHp(500000);casterLife.SetCurrentHp(100);
        var template=DataManager.SKILL_DATA.GetSkillTemplate(18726);var skill=new Skill(template,caster,1,target,null!);
        var effect=new Effect(skill,target);long generation=casterLife.CaptureRetailDrainLife();
        RetailNpcStatNpcDrainInitial.AttachOfflineInitialization(effect,1,n=>ReferenceEquals(n.GetLifeStats(),casterLife)
            &&casterLife.IsRetailDrainLifeEligible(generation));
        effect.Initialize();Assert.True(RetailNpcStatNpcDrainInitial.TryGetOfflineInitialization(effect,out var projection,out string reason),reason);
        Assert.NotNull(projection);effect.ApplyEffect();Assert.Equal(500000-projection!.Damage,targetLife.GetCurrentHp());
        NpcLifeStats currentLife=casterLife;int expectedHp;
        if(replaceLife)
        {
            currentLife=new FixtureLifeStats(caster);caster.SetLifeStats(currentLife);currentLife.SetCurrentHp(100);expectedHp=100;
        }
        else
        {
            caster.GetGameStats().AddEffectOnly(new Owner(),[new StatSetFunction(StatEnum.MAXHP,2147483520)]);
            casterLife.SetCurrentHp(2147483520);expectedHp=2147483520;
        }
        h.Clock.Advance(TimeSpan.FromMilliseconds(skill.GetHitTime()+1000));
        Assert.Equal(expectedHp,currentLife.GetCurrentHp());
        Assert.Contains(h.Clock.Faults,f=>f.Exception.ToString().Contains(replaceLife?"heal life owner changed":"drain heal HP overflow",StringComparison.Ordinal));
        if(replaceLife)Assert.Equal(100,casterLife.GetCurrentHp());
        effect.EndEffect();
    }

    [SkippableTheory]
    [InlineData(0)] // Shield added during the hit callback.
    [InlineData(1)] // Target despawns and respawns before the hit.
    [InlineData(2)] // Target has a nonpositive HP state not recognized by host IsDead.
    [InlineData(3)] // Caster life retires and becomes positive during the hit callback.
    public void InitialHitRejectsChangedProtectionOrRespawnedTarget(int scenario)
    {
        string? manifest=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(manifest)||string.IsNullOrEmpty(hash),"Requires pinned selected retail package");
        using var installed=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(manifest!),hash!,Path.GetDirectoryName(manifest!)!);
        using var h=BossAiHarness.For(120010000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI),typeof(ShieldOnSpelledAI)).Build();
        var caster=h.SpawnWithAi(216520,"retail_stat_base_fixture");
        var target=h.SpawnWithAi(281451,"retail_drain_protection_race_fixture",x:981);
        var ai=Assert.IsType<ShieldOnSpelledAI>(target.GetAi());
        var life=new FixtureLifeStats(target) {CheckProtectionLock=true};target.SetLifeStats(life);
        caster.SetLifeStats(new FixtureLifeStats(caster));life.SetCurrentHp(500000);caster.GetLifeStats().SetCurrentHp(100);
        var template=DataManager.SKILL_DATA.GetSkillTemplate(18726);
        var skill=new Skill(template,caster,1,target,null!);var effect=new Effect(skill,target);
        var casterLife=caster.GetLifeStats();long generation=casterLife.CaptureRetailDrainLife();
        RetailNpcStatNpcDrainInitial.AttachOfflineInitialization(effect,1,n=>ReferenceEquals(n.GetLifeStats(),casterLife)
            &&casterLife.IsRetailDrainLifeEligible(generation));
        effect.Initialize();Assert.True(RetailNpcStatNpcDrainInitial.TryGetOfflineInitialization(effect,out var projection,out string reason),reason);
        Assert.NotNull(projection);
        var hpField=typeof(CreatureLifeStats).GetField("currentHp",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
        if(scenario==1)
        {
            h.World.Despawn(target);h.World.Spawn(target);life.SetCurrentHp(500000);
        }
        else if(scenario==2) { hpField.SetValue(life,-1);Assert.False(target.IsDead()); }
        else if(scenario==3) ai.RetireCaster=true;
        else ai.Armed=true;
        effect.ApplyEffect();
        Assert.True(effect.PreparedNpcDrainInitialAborted);
        Assert.Null(target.GetEffectController().FindBySkillId(18726));
        if(scenario==2) { Assert.Equal(-1,life.GetCurrentHp());hpField.SetValue(life,500000); }
        Assert.Equal(500000,life.GetCurrentHp());Assert.Equal(100,casterLife.GetCurrentHp());
        Assert.Equal(scenario==0,ai.Added);h.Clock.Advance(TimeSpan.FromMilliseconds(5000));
        Assert.Equal(500000,life.GetCurrentHp());Assert.Equal(100,casterLife.GetCurrentHp());Assert.Empty(h.Clock.Faults);
    }

    [SkippableTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreparedRetailDrainObtainsNpcHpReceiptAndFeedsPeriodicHeal(bool hpBuffed)
    {
        string? manifest=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(manifest)||string.IsNullOrEmpty(hash),"Requires pinned selected retail package");
        using var installed=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(manifest!),hash!,Path.GetDirectoryName(manifest!)!);
        using var h=BossAiHarness.For(120010000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var caster=h.SpawnWithAi(216520,"retail_stat_base_fixture");
        var target=h.SpawnWithAi(281451,"retail_stat_base_fixture",x:981);
        if(hpBuffed)
        {
            caster.GetGameStats().AddEffectOnly(new Owner(),[new StatAddFunction(StatEnum.MAXHP,2000,true)]);
            target.GetGameStats().AddEffectOnly(new Owner(),[new StatAddFunction(StatEnum.MAXHP,3000,true)]);
        }
        Assert.True(RetailNpcStatCatalog.TryGet(caster,out var sourceProfile));
        Assert.True(RetailNpcStatCatalog.TryGet(target,out var targetProfile));
        Assert.Equal(0,targetProfile.MagicalDamageTrim);
        Assert.False(target.GetObserveController().HasDrainProtectionObserver);
        Assert.False(caster.GetObserveController().HasSkillCriticalObserver);
        Assert.True(RetailNpcStatCatalog.TryGetPeriodic(sourceProfile,18726,48,out var source));
        var operands=Assert.IsType<RetailNpcStatPeriodicCombat.DrainOperands>(Assert.Single(source.Parts).Drain);
        var life=new FixtureLifeStats(target) {CheckProtectionLock=true};target.SetLifeStats(life);
        caster.SetLifeStats(new FixtureLifeStats(caster));
        target.GetLifeStats().SetCurrentHp(500000);caster.GetLifeStats().SetCurrentHp(100);
        var template=DataManager.SKILL_DATA.GetSkillTemplate(18726);
        var primary=Assert.IsType<SpellAtkDrainInstantEffect>(template.GetEffectTemplate(1));
        var periodic=Assert.IsType<SpellAtkDrainEffect>(template.GetEffectTemplate(2));
        Assert.Null(OneTimeBoostSkillAttackEffect.GetActiveBoost(caster,primary));
        Assert.Equal(new RetailNpcStatNormalInputs.Modifiers(0,0),
            RetailNpcStatNormalInputs.CurrentModifiers(caster,StatEnum.BOOST_SPELL_ATTACK));
        // The host script requests level 1; the verified retail indexed cast
        // resolves the bound NPC skill-list entry at level 48.
        var skill=new Skill(template,caster,1,target,null!);
        var effect=new Effect(skill,target);
        Assert.False(RetailNpcStatNpcDrainInitial.TryConsumeOfflineInitialization(effect,primary,out _));
        var denied=new Effect(new Skill(template,caster,1,target,null!),target);
        RetailNpcStatNpcDrainInitial.AttachOfflineInitialization(denied,1,_=>false);
        denied.Initialize();
        Assert.True(RetailNpcStatNpcDrainInitial.TryGetOfflineInitialization(denied,out _,out string declined));
        Assert.Equal("fixture-caster-ineligible",declined);
        Assert.Empty(denied.GetSuccessEffects());Assert.Equal(500000,target.GetLifeStats().GetCurrentHp());
        var casterLife=caster.GetLifeStats();long casterGeneration=casterLife.CaptureRetailDrainLife();
        bool Eligible(Npc actor)=>ReferenceEquals(actor.GetLifeStats(),casterLife)
            &&casterLife.IsRetailDrainLifeEligible(casterGeneration);
        RetailNpcStatNpcDrainInitial.AttachOfflineInitialization(effect,1,Eligible);
        effect.Initialize();
        Assert.True(RetailNpcStatNpcDrainInitial.TryGetOfflineInitialization(effect,out var nullableProjection,out string initialReason),initialReason);
        var projected=Assert.IsType<RetailNpcStatNpcDrainInitial.Result>(nullableProjection);
        Assert.Equal("prepared-fixture-not-enabled",initialReason);
        Assert.Same(caster,projected.Selection.Caster);Assert.Same(target,projected.Selection.Target);
        Assert.Equal(48,projected.Selection.SourceLevel);Assert.Equal(source.Sha256,projected.Selection.Source.Sha256);
        Assert.Equal(sourceProfile.MaxHp+(hpBuffed?2000:0),projected.Selection.CasterMaxHp);
        Assert.Equal(targetProfile.MaxHp+(hpBuffed?3000:0),projected.Selection.TargetMaxHp);
        Assert.Equal(0,projected.BoostDelta);Assert.Equal(75000,projected.Base);
        int initialDamage=projected.Damage;
        Assert.Equal(70500,initialDamage);
        Assert.True(effect.IsInSuccessEffects(1));Assert.Equal(initialDamage,effect.GetReserveds(1).GetValue());
        Assert.True(effect.TryGetCalculatedMagicalCritical(out bool critical));Assert.False(critical);
        Assert.Throws<InvalidOperationException>(()=>effect.Initialize());
        Assert.False(RetailNpcStatNpcDrainInitial.TryPrepareOffline(effect,1,_=>true,out _,out string duplicate));
        Assert.Equal("already-calculated-effect",duplicate);
        // The host life witness is bounded; this is not production admission.
        Assert.True(effect.IsInSuccessEffects(2));
        effect.ApplyEffect(); // The periodic effect's controller starts it once.
        Assert.Equal(500000-initialDamage,target.GetLifeStats().GetCurrentHp());
        h.Clock.Advance(TimeSpan.FromMilliseconds(skill.GetHitTime()+1000));
        Assert.Equal(100+2*initialDamage,caster.GetLifeStats().GetCurrentHp());
        // Isolate the following periodic witnesses from the initial hit.
        target.GetLifeStats().SetCurrentHp(100000);caster.GetLifeStats().SetCurrentHp(100);
        int before=target.GetLifeStats().GetCurrentHp();life.HealAfterHit=true;
        h.Clock.Advance(TimeSpan.FromMilliseconds(3000));
        int ordinaryLoss=before-target.GetLifeStats().GetCurrentHp()+7;
        Assert.True(ordinaryLoss>45000); // Retail source base 75,000, not host base 45,000.
        Assert.Equal(100+RetailNpcStatSpellDrain.HealAmount(ordinaryLoss,150),caster.GetLifeStats().GetCurrentHp());

        target.GetLifeStats().SetCurrentHp(5);
        int casterBefore=caster.GetLifeStats().GetCurrentHp();
        h.Clock.Advance(TimeSpan.FromMilliseconds(3000));
        Assert.Equal(0,target.GetLifeStats().GetCurrentHp());
        Assert.Equal(casterBefore+7,caster.GetLifeStats().GetCurrentHp());
        // Retiring the caster life suppresses the old effect's periodic heal,
        // even after HP is set positive and a new life generation begins.
        target.GetLifeStats().SetCurrentHp(100000);
        casterLife.SetCurrentHp(0);casterLife.SetCurrentHp(100);
        Assert.False(Eligible(caster));
        h.Clock.Advance(TimeSpan.FromMilliseconds(3000));
        Assert.True(target.GetLifeStats().GetCurrentHp()<100000);
        Assert.Equal(100,casterLife.GetCurrentHp());
        target.GetLifeStats().SetCurrentHp(100000);
        casterLife.BeginRetailDrainLife();Assert.False(Eligible(caster));
        h.Clock.Advance(TimeSpan.FromMilliseconds(3000));
        Assert.True(target.GetLifeStats().GetCurrentHp()<100000);
        Assert.Equal(100,casterLife.GetCurrentHp());
        effect.EndEffect();
        target.GetLifeStats().SetCurrentHp(100000);
        int eventsAfterEnd=life.DamageEvents;
        h.Clock.Advance(TimeSpan.FromMilliseconds(6100));
        Assert.Equal(eventsAfterEnd,life.DamageEvents); // No extra periodic tick after cancellation.

        var incomplete=new Effect(new Skill(template,caster,1,target,null!),target);
        incomplete.SetReserveds(new EffectReserved(1,initialDamage,EffectReserved.ResourceType.HP,true),false);
        incomplete.AddSuccessEffect(primary);
        RetailNpcStatSpellDrain.Prepare(incomplete,primary,new(200,false,NpcPeriodic:new(48)));
        Assert.Throws<InvalidOperationException>(()=>periodic.Calculate(incomplete));
        Assert.Empty(h.Clock.Faults);
    }
}
