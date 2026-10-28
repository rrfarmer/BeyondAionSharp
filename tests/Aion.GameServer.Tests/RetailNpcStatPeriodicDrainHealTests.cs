using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;
using Xunit;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcStatPeriodicDrainHealTests
{
    private sealed class SilentLife(Npc npc):NpcLifeStats(npc)
    {
        protected override void OnHpChanged(SmAttackStatus.TYPE type,int previousHp,int newHp,Creature effector) { }
    }

    [Fact]
    public void DrainLifeWitnessRejectsOldGenerationAfterRealDespawnAndRespawn()
    {
        using var h=BossAiHarness.For(220030000).Build();
        var caster=h.SpawnWithAi(210548,"general");
        var life=caster.GetLifeStats();long original=life.CaptureRetailDrainLife();
        long epoch=caster.GetController().RetailLifeEpoch;
        Assert.True(life.IsRetailDrainLifeEligible(original));
        var world=Aion.GameServer.World.World.GetInstance();world.Despawn(caster);
        Assert.False(caster.IsSpawned());Assert.False(life.IsRetailDrainLifeEligible(original));
        Assert.NotEqual(epoch,caster.GetController().RetailLifeEpoch);
        world.Spawn(caster);
        Assert.Same(life,caster.GetLifeStats());Assert.True(caster.IsSpawned());
        long replacement=life.CaptureRetailDrainLife();
        Assert.NotEqual(original,replacement);Assert.False(life.IsRetailDrainLifeEligible(original));
        Assert.True(life.IsRetailDrainLifeEligible(replacement));
    }

    [Fact]
    public void DrainLifeWitnessRetiresAtZeroAndRequiresANewSpawnGeneration()
    {
        using var h=BossAiHarness.For(220030000).Build();
        var caster=h.SpawnWithAi(210548,"general");
        Assert.True(caster.GetLifeStats().CaptureRetailDrainLife()>=2); // Normal spawn called the birth owner.
        var life=new SilentLife(caster);caster.SetLifeStats(life);life.SetCurrentHp(100);
        long original=life.CaptureRetailDrainLife();
        Assert.True(life.IsRetailDrainLifeEligible(original));
        life.SetCurrentHp(0);Assert.False(life.IsRetailDrainLifeEligible(original));
        life.SetCurrentHp(100);Assert.False(life.IsRetailDrainLifeEligible(original));
        life.BeginRetailDrainLife();long replacement=life.CaptureRetailDrainLife();
        Assert.NotEqual(original,replacement);Assert.True(life.IsRetailDrainLifeEligible(replacement));
        Assert.False(life.IsRetailDrainLifeEligible(original));
        life.ReduceHp(SmAttackStatus.TYPE.DAMAGE,50,0,SmAttackStatus.LOG.REGULAR,caster);
        Assert.True(life.IsRetailDrainLifeEligible(replacement));
        life.ReduceHp(SmAttackStatus.TYPE.DAMAGE,100,0,SmAttackStatus.LOG.REGULAR,caster);
        life.SetCurrentHp(100);Assert.False(life.IsRetailDrainLifeEligible(replacement));
        life.BeginRetailDrainLife();long overflowLife=life.CaptureRetailDrainLife();
        typeof(CreatureLifeStats).GetField("currentHp",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!
            .SetValue(life,-1); // Synthetic signed-overflow state before the host zero-HP transition.
        life.SetCurrentHp(0);life.SetCurrentHp(100);
        Assert.False(life.IsRetailDrainLifeEligible(overflowLife));
        Assert.Empty(h.Clock.Faults);
    }
    [Theory]
    [InlineData(5, 48, 0, 150, 7)]
    [InlineData(100, 48, 2, 150, 246)]
    [InlineData(0, 48, 0, 150, 0)]
    [InlineData(21474837, 48, 0, 100, -21474836)]
    public void HealUsesNativeLevelAndWrappedProductBeforeFloatDivision(int damage,int level,int rate,int percent,int expected)
        => Assert.Equal(expected,RetailNpcStatSpellDrain.HealAmount(damage,level,rate,percent));

    [Fact]
    public void PeriodicRangeUsesSameWorldAndThreeDimensionsIncludingBoundary()
    {
        Assert.True(RetailNpcStatSpellDrain.PeriodicHealWithinRange(true,100,0,0,0,60,80,0));
        Assert.False(RetailNpcStatSpellDrain.PeriodicHealWithinRange(true,100,0,0,0,60,80,1));
        Assert.False(RetailNpcStatSpellDrain.PeriodicHealWithinRange(false,100,0,0,0,0,0,0));
        // 50,000 squared wraps negative before conversion in the native path.
        Assert.False(RetailNpcStatSpellDrain.PeriodicHealWithinRange(true,50000,0,0,0,0,0,0));
    }

    [Fact]
    public void NpcHealConsumesCommittedDamageAndChecksRange()
    {
        using var h=BossAiHarness.For(220030000).Build();
        var caster=h.SpawnWithAi(210548,"general");
        var target=h.SpawnWithAi(210548,"general",x:981);
        caster.GetLifeStats().SetCurrentHp(100);
        var effect=new Effect(caster,target,new SkillTemplate {skillId=910070},48);
        var operands=new RetailNpcStatPeriodicCombat.DrainOperands(100,0,150,null,null,0,3600000,250,1,
            "Target_Only","e1",0,100);

        // A lethal hit against five remaining HP returns five, even if its
        // calculated damage was much larger. 150% is seven after truncation.
        Assert.True(RetailNpcStatSpellDrain.ApplyNpcPeriodicHpHeal(effect,caster,target,operands,48,5,true));
        Assert.Equal(107,caster.GetLifeStats().GetCurrentHp());
        Assert.False(RetailNpcStatSpellDrain.ApplyNpcPeriodicHpHeal(effect,caster,target,operands,48,0,true));
        Assert.False(RetailNpcStatSpellDrain.ApplyNpcPeriodicHpHeal(effect,caster,target,operands,48,5,false));
        Assert.False(RetailNpcStatSpellDrain.ApplyNpcPeriodicHpHeal(effect,caster,target,operands with {Radius=0},48,5,true));
        Assert.Equal(107,caster.GetLifeStats().GetCurrentHp());
        bool checkedUnderHpLock=false;
        Assert.False(RetailNpcStatSpellDrain.ApplyNpcPeriodicHpHeal(effect,caster,target,operands,48,5,()=>
        {
            checkedUnderHpLock=Monitor.IsEntered(caster.GetLifeStats());
            return false; // The caster life retired after the earlier damage.
        }));
        Assert.True(checkedUnderHpLock);
        Assert.Equal(107,caster.GetLifeStats().GetCurrentHp());
        Assert.True(RetailNpcStatSpellDrain.ApplyNpcPeriodicHpHeal(effect,caster,target,operands,48,5,()=>
            Monitor.IsEntered(caster.GetLifeStats())));
        Assert.Equal(114,caster.GetLifeStats().GetCurrentHp());
        Assert.Throws<InvalidOperationException>(()=>caster.GetLifeStats().IncreaseRetailNpcDrainHp(int.MaxValue,effect,()=>true));
        Assert.Equal(114,caster.GetLifeStats().GetCurrentHp());
        Assert.Empty(h.Clock.Faults);
    }
}
