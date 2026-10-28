using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Model;

namespace Aion.GameServer.Tests;

public sealed class SkyTempleCombatTests
{
    [Theory]
    [InlineData("physical",SkillElement.NONE)]
    [InlineData("magical_water",SkillElement.WATER)]
    [InlineData("magical_Fire",SkillElement.FIRE)]
    [InlineData("magical_Earth",SkillElement.EARTH)]
    [InlineData("magical_Air",SkillElement.WIND)]
    public void SelectedNormalTypeIsReadFromRetailRecord(string source,SkillElement expected)
        => Assert.Equal(expected,SkyTempleCombatInputs.Element(source));

    [Fact] public void UnknownNormalTypeDoesNotFallBackToPhysical()
        => Assert.Throws<InvalidDataException>(()=>SkyTempleCombatInputs.Element("magical_typo"));

    [Theory]
    [InlineData(1,-1000)] [InlineData(50,-1000)] [InlineData(51,-1010)] [InlineData(100,-1500)]
    public void PlayerElementFloorUsesProvedInitializer(int level,int floor)
        => Assert.Equal(floor,SkyTempleCombatInputs.ElementalFloor(level));

    [Theory] [InlineData(0)] [InlineData(101)]
    public void UnsupportedLevelDoesNotReadAChosenFloor(int level)
        => Assert.Throws<ArgumentOutOfRangeException>(()=>SkyTempleCombatInputs.ElementalFloor(level));

    [Fact]
    public void ManagedRetailEngineUsesKnownMt64SequenceAndSingletonConsumesNoWord()
    {
        // Published MT19937-64 seed5489 first three words:
        //14514284786278117030,4620546740167642908,13109570281517897720.
        var random = new SkyTempleNpcCombat.LiveRandom(5489);
        Assert.Equal(31,random.Range(1,1000));
        Assert.Equal(250,random.Range(250,250));
        Assert.Equal(909,random.Range(1000,1));
        Assert.Equal(721,random.Range(1,1000));
    }

    [Fact]
    public void FireGuardianRawBaseIsNotGenericAttack411()
    {
        var scope = SkyTempleCombatInputs.Scope(212835);
        // Chosen matched hypothetical player: MDef100, earth0, level33.
        // Source sample250 is inside actual206..300. Armor subtracts10.
        float amount = RetailNpcNormalDamageStages.CalculateElementalDefense(scope,250,33,33,false,
            100,0,0,0,1000,-1000).Damage;
        Assert.Equal(240f,amount);
        Assert.Equal(1f,RetailNpcNormalDamageStages.CalculateNpcCriticalMultiplier(scope,false,0,1000));
    }

    [Fact]
    public void SourceRatioNormalizesDefenseRatherThanScalingHpAgain()
    {
        var scope = SkyTempleCombatInputs.Scope(212776);
        Assert.Equal(120f,RetailNpcNormalDamageStages.CalculatePhysicalDefense(scope,250,33,33,false,
            100,0,2000).Damage);
        Assert.Throws<InvalidOperationException>(()=>SkyTempleCombatInputs.Word(65536,"critical"));
    }

    [Theory]
    [InlineData(200,60,1000,0x42f00001u)]
    [InlineData(200,60,20,0x43340000u)]
    [InlineData(200,100,1000,0x43480000u)]
    [InlineData(200,0,1000,0u)]
    public void ShieldUsesRetainedReverseStatAsRemainingFraction(float amount,int remaining,int cap,uint expectedBits)
        =>Assert.Equal(expectedBits,BitConverter.SingleToUInt32Bits(SkyTempleNpcCombat.BlockDamage(amount,remaining,cap)));

    [Theory]
    [InlineData(999,587)] [InlineData(1000,1175)] [InlineData(1999,1175)] [InlineData(2000,1762)]
    public void NativeSpellMinmaxBoostUsesIntegerQuotient(int boost,int expected)
        =>Assert.Equal(expected,SkyTempleNpcSkillCombat.MagicBase(250,135,boost,1000));

    [Fact]
    public void SourceLabel17039Remains23WhileReal16725Has135()
    {
        Assert.Equal(23,SkyTempleNpcSkillCombat.SelectedSpell(17039).Percent);
        Assert.Equal(135,SkyTempleNpcSkillCombat.SelectedSpell(16725).Percent);
    }
    [Fact]
    public void CountZeroAndCancelledFiniteUseKeepSeparateSemantics()
    {
        var rows=new SkyTempleNpcSkillState.Counts([0,1]);
        Assert.True(rows.Allows(0,100,0,0));Assert.True(rows.Allows(1,100,0,0));
        rows.Use(0,100);rows.Use(1,100);
        Assert.Equal(0,rows.Remaining[0]);Assert.False(rows.Allows(1,101,0,0));
        rows.Cancel(1);Assert.True(rows.Allows(1,101,0,0));
        Assert.False(rows.Allows(0,100,0,0)); // Strict same-tick reuse even for Count0.
    }
    [Theory]
    [InlineData(100u,200u,100u,0u,false)]
    [InlineData(100u,201u,100u,0u,true)]
    [InlineData(100u,151u,999u,50u,true)]
    [InlineData(4294967290u,5u,10u,0u,true)]
    public void SkillReusePreservesStrictWrappedAbsoluteComparison(uint last,uint now,uint reuse,uint delay,bool expected)
        =>Assert.Equal(expected,SkyTempleNpcSkillState.ReuseAllows(last,now,reuse,delay));

    [Fact]
    public void PilotReflectionIsPercentPlusFlatAndZeroRadiusExcludesCoincidence()
    {
        Assert.Equal(85,RetailNpcPilotProtectionTransition.ReflectionAmount(100,25,60));
        Assert.Equal(73,RetailNpcPilotProtectionTransition.ShieldAmount(100,73,1));
        Assert.Equal(64f,SkyTempleNpcProtection.DistanceSquared(0,0,0,8,0,0));
        Assert.Equal(0f,SkyTempleNpcProtection.DistanceSquared(1,2,3,1,2,3));
    }

    [Theory]
    [InlineData(34,607.5f)] [InlineData(35,675f)] [InlineData(64,675f)] [InlineData(65,742.5f)]
    public void PhysicalSkillAddsCurrent70PercentToSkill100Percent(int random,float expected)
        =>Assert.Equal(expected,SkyTempleNpcPhysicalSkills.Base(250,100,new(.7f,0),random));

    [Theory]
    [InlineData(16556,0,1,0,true)] [InlineData(16556,0,-1,0,false)]
    [InlineData(16794,0,-1,0,true)] [InlineData(16794,0,0,1,true)]
    [InlineData(16556,30,0,1,true)] [InlineData(16794,30,0,-1,true)]
    public void SkillDirectionUsesExactNativeHeadingTables(int id,int heading,float x,float y,bool expected)
        =>Assert.Equal(expected,SkyTempleNpcPhysicalSkills.Direction(id,heading,0,0,x,y));

    [Fact]
    public void ArbitraryObjectCannotProducePeriodicHpOrBypassLegacyNullGuard()
        =>Assert.Throws<InvalidOperationException>(()=>SkyTempleNpcPeriodicEffects.TickDispatch.Produce(new object()));

    [Fact] public void NativeReuseUsesMillisecondsWhileHostCooldownUsesHundredMsUnits()
    {
        Assert.Equal(20000u,SkyTempleNpcSkillState.ReuseMilliseconds(200));
        Assert.False(SkyTempleNpcSkillState.ReuseAllows(100,20100,20000,0));
        Assert.True(SkyTempleNpcSkillState.ReuseAllows(100,20101,20000,0));
        Assert.Throws<OverflowException>(()=>SkyTempleNpcSkillState.ReuseMilliseconds(-1));
    }
    [Theory]
    [InlineData(0,100)] [InlineData(49,100)] [InlineData(50,100)]
    [InlineData(150,100)] [InlineData(151,101)] [InlineData(361,311)] [InlineData(1466,1416)]
    public void SkillPrimaryDelayNarrowsSourceWordThenSubtractsFiftyAndFloorsHundred(ushort word,int expected)
        =>Assert.Equal(expected,SkyTempleNpcSkillTiming.PrimaryDelay(word));

    [Fact]
    public void ArbitraryObjectCannotProduceSelectedPrimaryHp()
        =>Assert.Throws<InvalidOperationException>(()=>SkyTempleNpcSkillTiming.ImpactDispatch.Produce(new object()));

    [Fact]
    public void SelectedSourceMotionRetainsCastReuseAndProjectileChannels()
    {
        var longFire=SkyTempleNpcSkillTiming.SourceMotion(212876,16710,24);
        Assert.Equal(2500u,longFire.Cast);Assert.Equal(20000u,longFire.Reuse);
        Assert.Equal(0f,longFire.Ammo);Assert.True(longFire.Recovery>0);
        Assert.Equal(25f,SkyTempleNpcSkillTiming.SourceMotion(212762,16590,21).Ammo);
        Assert.Throws<InvalidOperationException>(()=>SkyTempleNpcSkillTiming.SourceMotion(212876,16710,1));
    }

}
