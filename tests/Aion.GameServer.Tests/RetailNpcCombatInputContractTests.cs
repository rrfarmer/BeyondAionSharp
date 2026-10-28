using Aion.GameServer.Controllers.Attack.RetailNpc;

namespace Aion.GameServer.Tests;

/// <summary>Supported-domain and range contracts only; no native oracle or live routing.</summary>
public sealed class RetailNpcCombatInputContractTests
{
    private static RetailNpcNormalScope NpcScope() => RetailNpcNormalScope.Create(
        RetailNpcNormalScope.MainBinarySha256, RetailNpcNormalActorKind.OrdinaryOwnerlessNpc,
        RetailNpcNormalActorKind.Player, 212835, 0);

    [Theory]
    [InlineData(206, 300, 206, 300, 95u)]
    [InlineData(300, 206, 206, 300, 95u)]
    [InlineData(0, 65535, 0, 65535, 65536u)]
    public void NormalRangeOrdersWordEndpointsAndCountsInclusively(ushort first, ushort second, ushort minimum, ushort maximum, uint count)
    {
        var range = RetailNpcNormalRange.Create(NpcScope(), first, second);
        Assert.Equal(minimum, range.Minimum);
        Assert.Equal(maximum, range.Maximum);
        Assert.Equal(count, range.InclusiveCount);
        Assert.True(range.RequiresRandomDraw);
        Assert.Equal((int)minimum, range.ResolveOriginalSample(minimum));
        Assert.Equal((int)maximum, range.ResolveOriginalSample(maximum));
    }

    [Theory]
    [InlineData((ushort)0)]
    [InlineData((ushort)65535)]
    public void SingletonReturnsEndpointAndForbidsAnExtraDraw(ushort endpoint)
    {
        var range = RetailNpcNormalRange.Create(NpcScope(), endpoint, endpoint);
        Assert.False(range.RequiresRandomDraw);
        Assert.Equal(1u, range.InclusiveCount);
        Assert.Equal((int)endpoint, range.ResolveOriginalSample(null));
        Assert.Throws<ArgumentException>(() => range.ResolveOriginalSample(endpoint));
    }

    [Fact]
    public void NonSingletonRequiresOriginalResultAndRejectsOutsideRange()
    {
        var range = RetailNpcNormalRange.Create(NpcScope(), 206, 300);
        Assert.Throws<ArgumentException>(() => range.ResolveOriginalSample(null));
        Assert.Throws<ArgumentOutOfRangeException>(() => range.ResolveOriginalSample(205));
        Assert.Throws<ArgumentOutOfRangeException>(() => range.ResolveOriginalSample(301));
    }

    [Fact]
    public void ScopeRejectsMismatchedProvenanceAndPlayerVersusPlayer()
    {
        Assert.Throws<ArgumentException>(() => RetailNpcNormalScope.Create("unverified", RetailNpcNormalActorKind.OrdinaryOwnerlessNpc, RetailNpcNormalActorKind.Player, 212835, 0));
        Assert.Throws<ArgumentException>(() => RetailNpcNormalScope.Create(null!, RetailNpcNormalActorKind.OrdinaryOwnerlessNpc, RetailNpcNormalActorKind.Player, 212835, 0));
        Assert.Throws<ArgumentException>(() => RetailNpcNormalScope.Create(RetailNpcNormalScope.MainBinarySha256, RetailNpcNormalActorKind.Player, RetailNpcNormalActorKind.Player, 0, 0));
    }

    [Theory]
    [InlineData((int)RetailNpcNormalActorKind.Unverified)]
    [InlineData((int)RetailNpcNormalActorKind.OwnedNpc)]
    [InlineData((int)RetailNpcNormalActorKind.ScaledNpc)]
    [InlineData(999)]
    public void ScopeRejectsUnsupportedActorOnEitherSide(int rawKind)
    {
        var kind = (RetailNpcNormalActorKind)rawKind;
        Assert.Throws<ArgumentOutOfRangeException>(() => RetailNpcNormalScope.Create(RetailNpcNormalScope.MainBinarySha256, kind, RetailNpcNormalActorKind.OrdinaryOwnerlessNpc, 0, 212835));
        Assert.Throws<ArgumentOutOfRangeException>(() => RetailNpcNormalScope.Create(RetailNpcNormalScope.MainBinarySha256, RetailNpcNormalActorKind.OrdinaryOwnerlessNpc, kind, 212835, 0));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, 0)]
    [InlineData(212835, 1)]
    public void ScopeRequiresNpcIdentityAndZeroPlayerTemplateId(int npcId, int playerTemplateId)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RetailNpcNormalScope.Create(RetailNpcNormalScope.MainBinarySha256,
            RetailNpcNormalActorKind.OrdinaryOwnerlessNpc, RetailNpcNormalActorKind.Player, npcId, playerTemplateId));
    }

    [Fact]
    public void PlayerBaseAndPlayerWeaponCriticalCannotEnterNpcOnlyStages()
    {
        var scope = RetailNpcNormalScope.Create(RetailNpcNormalScope.MainBinarySha256,
            RetailNpcNormalActorKind.Player, RetailNpcNormalActorKind.OrdinaryOwnerlessNpc, 0, 212835);
        Assert.Throws<ArgumentException>(() => RetailNpcNormalRange.Create(scope, 1, 2));
        Assert.Throws<ArgumentException>(() => RetailNpcNormalDamageStages.CalculateNpcCriticalMultiplier(scope, true, 0, 1000));
        var range = RetailNpcNormalRange.Create(NpcScope(), 1, 2);
        Assert.Throws<ArgumentException>(() => RetailNpcNormalDamageStages.CalculateNpcBase(scope, range, 1, 1f, 0f, 0, 0, 0));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(999u)]
    [InlineData(65001u)]
    [InlineData(uint.MaxValue)]
    public void EffectiveRatioOutsideVerifiedConstructionDomainIsRejected(uint ratio)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RetailNpcNormalDamageStages.CalculatePhysicalDefense(NpcScope(), 200f, 30, 30, false, 600, 0, ratio));
        Assert.Throws<ArgumentOutOfRangeException>(() => RetailNpcNormalDamageStages.CalculateElementalDefense(NpcScope(), 200f, 30, 30, false, 600, 0, 0, 0, ratio, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => RetailNpcNormalDamageStages.CalculateNpcCriticalMultiplier(NpcScope(), true, 0, ratio));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void UnsupportedLevelsAreRejectedInsteadOfClamped(int level)
    {
        if(level==0)
            Assert.Equal(200f,RetailNpcNormalDamageStages.ApplyLevelRule(NpcScope(),200f,0,30,false));
        else Assert.Throws<ArgumentOutOfRangeException>(() => RetailNpcNormalDamageStages.ApplyLevelRule(NpcScope(), 200f, level, 30, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => RetailNpcNormalDamageStages.ApplyLevelRule(NpcScope(), 200f, 30, level, false));
    }

    [Fact]
    public void PlayerOnlyFlagCannotBeGuessedForNpcAttacker()
    {
        Assert.Throws<ArgumentException>(() => RetailNpcNormalDamageStages.ApplyLevelRule(NpcScope(), 200f, 30, 30, true));
    }

    [Fact]
    public void OrdinaryNpcFloorMustBeExplicitAndCorrect()
    {
        var scope = RetailNpcNormalScope.Create(RetailNpcNormalScope.MainBinarySha256,
            RetailNpcNormalActorKind.OrdinaryOwnerlessNpc, RetailNpcNormalActorKind.OrdinaryOwnerlessNpc, 212835, 212765);
        Assert.Throws<ArgumentException>(() => RetailNpcNormalDamageStages.CalculateElementalDefense(scope, 200f, 30, 30, false, 600, 0, 0, 0, 1000, 0));
    }

    [Theory]
    [InlineData(2147483648u, 0)]
    [InlineData(2147483904u, -200)]
    [InlineData(uint.MaxValue, 0)]
    public void UnsupportedCvttss2siDomainIsRejectedRatherThanSilentlyNarrowed(uint defense, int movement)
    {
        Assert.Throws<ArithmeticException>(() => RetailNpcNormalDamageStages.CalculatePhysicalDefense(NpcScope(), 200f, 30, 30, false, defense, movement, 1000));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void NonFiniteInputsAreRejected(float value)
    {
        var scope = NpcScope();
        var range = RetailNpcNormalRange.Create(scope, 1, 2);
        Assert.Throws<ArgumentOutOfRangeException>(() => RetailNpcNormalDamageStages.ApplyLevelRule(scope, value, 30, 30, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => RetailNpcNormalDamageStages.CalculateNpcBase(scope, range, 1, value, 0f, 0, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => RetailNpcNormalDamageStages.CalculateNpcBase(scope, range, 1, 0f, value, 0, 0, 0));
    }

    [Fact]
    public void FiniteInputsThatOverflowAnIntermediateAreRejected()
    {
        var scope = NpcScope();
        var range = RetailNpcNormalRange.Create(scope, 2, 2);
        Assert.Throws<ArithmeticException>(() => RetailNpcNormalDamageStages.CalculateNpcBase(scope, range, null, float.MaxValue, float.MaxValue, 0, 0, 0));
        Assert.Throws<ArithmeticException>(() => RetailNpcNormalDamageStages.CalculateNpcBase(scope, range, null, float.MaxValue, 0f, 0, 0, 0));
    }

    [Fact]
    public void NullContextAndRangeAreNotConvertedToDefaults()
    {
        Assert.Throws<ArgumentNullException>(() => RetailNpcNormalRange.Create(null!, 1, 2));
        Assert.Throws<ArgumentNullException>(() => RetailNpcNormalDamageStages.CalculatePveRatio(null!, 0, 0));
        Assert.Throws<ArgumentNullException>(() => RetailNpcNormalDamageStages.CalculatePhysicalDefense(null!, 200f, 30, 30, false, 600, 0, 1000));
        Assert.Throws<ArgumentNullException>(() => RetailNpcNormalDamageStages.CalculateNpcBase(NpcScope(), null!, 1, 1f, 0f, 0, 0, 0));
    }
}
