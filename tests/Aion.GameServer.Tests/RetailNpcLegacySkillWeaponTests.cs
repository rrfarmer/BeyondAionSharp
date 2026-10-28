using Aion.GameServer.Controllers.Attack.RetailNpc;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcLegacySkillWeaponTests
{
    [Theory]
    [InlineData(false, 101, 205)]
    [InlineData(true, 50, 102)]
    public void SourceRightHandRangeIsSelectedBeforeOneDrawAndOneStatApplication(bool dual, int low, int high)
    {
        int draws = 0, applies = 0;
        var result = RetailNpcLegacySkillWeapon.Create(101, 205, dual, false,
            (a,b) => { draws++; Assert.Equal(low,a); Assert.Equal(high,b); return b; },
            raw => { applies++; Assert.Equal(high,raw); return raw * 1.5f + 20; });
        Assert.Equal(1,draws); Assert.Equal(1,applies);
        Assert.Equal(high,result.RawBase); Assert.Equal(high * 1.5f + 20,result.WeaponDamage);
    }

    [Fact]
    public void PercentAndConditionalBonusReuseTheRawBaseRatherThanItsBuffedValue()
    {
        var result = RetailNpcLegacySkillWeapon.Create(100,100,false,false,
            (a,b) => a, raw => raw * 1.5f + 20);
        // Native base100 * (1 + attack-rate.5 + skill-percent1) + flat20 =270.
        Assert.Equal(270f,result.WeaponDamage + result.RawBase * 100 / 100f);
        Assert.Equal(295f,result.WeaponDamage + result.RawBase * 100 / 100f + result.RawBase * 25 / 100f);
    }

    [Theory]
    [InlineData(false,100,240f)]
    [InlineData(true,50,130f)]
    public void IndependentEffectFlatIsNotMultipliedByEffectPercentOrSkillPercent(bool dual,int expectedRaw,float expectedTotal)
    {
        var result=RetailNpcLegacySkillWeapon.Create(100,100,dual,false,(a,b)=>a,
            raw=>RetailNpcLegacySkillWeapon.WeaponContribution(raw,new(20,20)));
        Assert.Equal(expectedRaw,result.RawBase);
        Assert.Equal(expectedTotal,result.WeaponDamage + result.RawBase * 100 / 100f);
    }

    [Fact]
    public void DodgeOrResistDoesNotDrawOrApplyAttackFunctions()
    {
        var result = RetailNpcLegacySkillWeapon.Create(100,200,true,true,
            (_,_) => throw new InvalidOperationException("unexpected draw"),
            _ => throw new InvalidOperationException("unexpected stat application"));
        Assert.Equal(default,result);
    }

    [Fact]
    public void AZeroSourceBaseIsNotReplacedByRawAttackOrAGenericMinimum()
    {
        var result = RetailNpcLegacySkillWeapon.Create(0,0,false,false,
            (a,b) => { Assert.Equal(0,a); Assert.Equal(0,b); return 0; }, raw => raw + 7);
        Assert.Equal(0,result.RawBase); Assert.Equal(7f,result.WeaponDamage);
    }

    [Theory]
    [InlineData(-1,100)]
    [InlineData(100,99)]
    [InlineData(0,65536)]
    public void InvalidSourceEndpointsAreVisibleFailures(int low,int high) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => RetailNpcLegacySkillWeapon.Create(low,high,false,false,(a,b)=>a,x=>x));

    [Fact]
    public void ADrawCannotEscapeTheSelectedRightHandRange() =>
        Assert.Throws<InvalidOperationException>(() => RetailNpcLegacySkillWeapon.Create(101,205,true,false,(a,b)=>103,x=>x));
}
