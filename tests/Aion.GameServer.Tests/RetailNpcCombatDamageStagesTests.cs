using Aion.GameServer.Controllers.Attack.RetailNpc;

namespace Aion.GameServer.Tests;

/// <summary>
/// Static instruction-derived vectors, NOT captured retail outcomes or full combat tests.
/// Expected bits are pinned independently by exact-rational SSE reconstruction in
/// Aion-NPC-Discovery/implementation/phase3/normal-calculator/static-vectors.json.
/// No world, live Creature, RNG, packet, observer or HP application is constructed.
/// </summary>
public sealed class RetailNpcCombatDamageStagesTests
{
    private static RetailNpcNormalScope NpcToNpc() => RetailNpcNormalScope.Create(
        RetailNpcNormalScope.MainBinarySha256, RetailNpcNormalActorKind.OrdinaryOwnerlessNpc,
        RetailNpcNormalActorKind.OrdinaryOwnerlessNpc, 212835, 212765);

    private static RetailNpcNormalScope NpcToPlayer() => RetailNpcNormalScope.Create(
        RetailNpcNormalScope.MainBinarySha256, RetailNpcNormalActorKind.OrdinaryOwnerlessNpc,
        RetailNpcNormalActorKind.Player, 212835, 0);

    private static RetailNpcNormalScope PlayerToNpc() => RetailNpcNormalScope.Create(
        RetailNpcNormalScope.MainBinarySha256, RetailNpcNormalActorKind.Player,
        RetailNpcNormalActorKind.OrdinaryOwnerlessNpc, 0, 212835);

    private static void Bits(uint expected, float actual) => Assert.Equal(expected, BitConverter.SingleToUInt32Bits(actual));

    [Fact]
    public void ChosenFireKeyBaseUsesItsRangeInsteadOfGenericAttackScalar()
    {
        var scope = NpcToPlayer();
        var range = RetailNpcNormalRange.Create(scope, 206, 300);
        var result = RetailNpcNormalDamageStages.CalculateNpcBase(scope, range, 253, 1f, 0f, 0, 0, 0);
        Bits(0x3f800000u, result.SummedRate);
        Assert.Equal(0, result.SummedFlat);
        Bits(0x437d0000u, result.BeforeMinimum);
        Bits(0x437d0000u, result.AfterMinimum);
        Bits(0x437d0000u, result.AfterMovement);
    }

    [Fact]
    public void FractionalRatesAndMovementKeepSeparateBinary32Operations()
    {
        var scope = NpcToNpc();
        var range = RetailNpcNormalRange.Create(scope, 200, 220);
        var result = RetailNpcNormalDamageStages.CalculateNpcBase(scope, range, 211, 1.1f, 0.2f, 3, -5, 15);
        Bits(0x3fa66667u, result.SummedRate);
        Assert.Equal(-2, result.SummedFlat);
        Bits(0x43882667u, result.BeforeMinimum);
        Bits(0x439c9290u, result.AfterMovement);
    }

    [Fact]
    public void FlatAdditionWrapsInt32BeforeFloatConversionAndMinimum()
    {
        var scope = NpcToNpc();
        var range = RetailNpcNormalRange.Create(scope, 1, 1);
        var result = RetailNpcNormalDamageStages.CalculateNpcBase(scope, range, null, 1f, 0f, 1, int.MaxValue, 0);
        Assert.Equal(int.MinValue, result.SummedFlat);
        Bits(0xcf000000u, result.BeforeMinimum);
        Bits(0x3f800000u, result.AfterMinimum);
        Bits(0x3f800000u, result.AfterMovement);
    }

    [Fact]
    public void BaseMinimumPrecedesMovementAndDoesNotInventAFinalMinimum()
    {
        var scope = NpcToNpc();
        var range = RetailNpcNormalRange.Create(scope, 0, 0);
        var result = RetailNpcNormalDamageStages.CalculateNpcBase(scope, range, null, 0f, 0f, 0, 0, -150);
        Bits(0u, result.BeforeMinimum);
        Bits(0x3f800000u, result.AfterMinimum);
        Bits(0xbf000000u, result.AfterMovement);
    }

    [Fact]
    public void SingletonBaseAcceptsNoDrawAndUsesTheExplicitMovementEntry()
    {
        var scope = NpcToNpc();
        var range = RetailNpcNormalRange.Create(scope, 600, 600);
        var result = RetailNpcNormalDamageStages.CalculateNpcBase(scope, range, null, 1f, 0f, 0, 0, -50);
        Bits(0x44160000u, result.AfterMinimum);
        Bits(0x43960000u, result.AfterMovement);
    }

    [Theory]
    [InlineData(32, false, 0x43480000u)] // +2: 200
    [InlineData(33, false, 0x43340000u)] // +3: 180
    [InlineData(42, false, 0x00000000u)] // +12: zero factor
    [InlineData(43, false, 0x00000000u)] // +13: negative factor returns zero
    [InlineData(33, true, 0x43480000u)]  // player flag: +3 grace
    [InlineData(34, true, 0x43340000u)]
    public void PlayerToOrdinaryNpcLevelRuleHonorsExactGraceAndZeroBoundary(int defenderLevel, bool flag, uint expected)
    {
        Bits(expected, RetailNpcNormalDamageStages.ApplyLevelRule(PlayerToNpc(), 200f, 30, defenderLevel, flag));
    }

    [Fact]
    public void LevelRuleRunsBeforeArmor()
    {
        var result = RetailNpcNormalDamageStages.CalculatePhysicalDefense(NpcToNpc(), 200f, 30, 33, false, 600, 0, 1000);
        Bits(0x43340000u, result.AfterLevel); // 180
        Assert.Equal(600, result.MovementAdjustedDefense);
        Bits(0x42700000u, result.ArmorTerm); // 60
        Bits(0x42f00000u, result.Damage); // 120; armor-first would be 126
    }

    [Fact]
    public void NpcAttackOnPlayerHasNoNpcTargetLevelReduction()
    {
        var result = RetailNpcNormalDamageStages.CalculatePhysicalDefense(NpcToPlayer(), 200f, 30, 33, false, 600, 0, 1000);
        Bits(0x43480000u, result.AfterLevel);
        Bits(0x430c0000u, result.Damage);
    }

    [Fact]
    public void ExplicitPlayerFlagChangesLevelStageBeforePhysicalArmor()
    {
        var result = RetailNpcNormalDamageStages.CalculatePhysicalDefense(PlayerToNpc(), 200f, 30, 33, true, 600, 0, 1000);
        Bits(0x43480000u, result.AfterLevel);
        Bits(0x430c0000u, result.Damage);
    }

    [Fact]
    public void StatRatioDividesTheWholePostArmorDamage()
    {
        var result = RetailNpcNormalDamageStages.CalculatePhysicalDefense(NpcToNpc(), 200f, 30, 30, false, 600, 0, 1250);
        Bits(0x42e00000u, result.Damage); // (200 - 60)/1.25 = 112
    }

    [Fact]
    public void MovementDefenseTruncatesBeforeDivisionByTen()
    {
        var result = RetailNpcNormalDamageStages.CalculatePhysicalDefense(NpcToNpc(), 300f, 30, 30, false, 601, 33, 1225);
        Assert.Equal(799, result.MovementAdjustedDefense);
        Bits(0x429fcccdu, result.ArmorTerm);
        Bits(0x4333ac69u, result.Damage);
    }

    [Fact]
    public void ArmorCanMakeZeroLevelResultNegativeBeforeItsOwnClamp()
    {
        var result = RetailNpcNormalDamageStages.CalculatePhysicalDefense(NpcToNpc(), 200f, 30, 42, false, 600, 0, 1000);
        Bits(0u, result.AfterLevel);
        Bits(0xc2700000u, result.BeforeClamp);
        Bits(0u, result.Damage);
    }

    [Fact]
    public void DefenseConversionAcceptsLastPositiveRepresentableInt32Float()
    {
        var result = RetailNpcNormalDamageStages.CalculatePhysicalDefense(NpcToNpc(), 0f, 30, 30, false, 2147483520u, 0, 1000);
        Assert.Equal(2147483520, result.MovementAdjustedDefense);
        Bits(0x4d4cccccu, result.ArmorTerm);
        Bits(0u, result.Damage);
    }

    [Fact]
    public void DefenseConversionAcceptsNegativeInt32Boundary()
    {
        var result = RetailNpcNormalDamageStages.CalculatePhysicalDefense(NpcToNpc(), 5f, 30, 30, false, 2147483648u, -200, 1000);
        Assert.Equal(int.MinValue, result.MovementAdjustedDefense);
        Bits(0xcd4ccccdu, result.ArmorTerm);
        Bits(0x4d4ccccdu, result.Damage);
    }

    [Fact]
    public void PhysicalZeroClampPreservesNativeNegativeZero()
    {
        float negativeZero = BitConverter.UInt32BitsToSingle(0x80000000u);
        var result = RetailNpcNormalDamageStages.CalculatePhysicalDefense(NpcToNpc(), negativeZero, 30, 30, false, 0, 0, 1000);
        Bits(0x80000000u, result.AfterLevel);
        Bits(0x80000000u, result.Damage);
    }

    [Theory]
    [InlineData((short)130, 130, 0x3f666666u, 0x43580000u)]
    [InlineData((short)-2000, -1300, 0x40000000u, 0x43f00000u)]
    public void OrdinaryNpcElementUsesExplicitFloor(short element, int expectedClamped, uint multiplierBits, uint damageBits)
    {
        var result = RetailNpcNormalDamageStages.CalculateElementalDefense(NpcToNpc(), 300f, 33, 33, false, 600, 0, element, 0, 1000, -1300);
        Assert.Equal(expectedClamped, result.ClampedElementalDefense);
        Bits(0x43700000u, result.AfterMagicalDefense);
        Bits(0x44a28000u, result.ElementalDenominator);
        Bits(multiplierBits, result.ElementalMultiplier);
        Bits(damageBits, result.Damage);
    }

    [Theory]
    [InlineData(60, (short)140, 0x44af0000u)] // 1400
    [InlineData(50, (short)130, 0x44a28000u)] // one level is 50: 1300
    public void PlayerElementDenominatorUsesMinimumLevelOnlyWhenBothAboveFifty(int defenderLevel, short element, uint expectedDenominator)
    {
        // The supplied floor zero is a chosen static table input, not a loaded-player capture.
        var result = RetailNpcNormalDamageStages.CalculateElementalDefense(NpcToPlayer(), 300f, 65, defenderLevel, false, 600, 0, element, 0, 1000, 0);
        Bits(expectedDenominator, result.ElementalDenominator);
        Bits(0x43580000u, result.Damage);
    }

    [Fact]
    public void ElementNormalizationTruncatesAfterAttackerStatRatioDivision()
    {
        var result = RetailNpcNormalDamageStages.CalculateElementalDefense(NpcToNpc(), 300f, 33, 33, false, 601, 33, 130, 3, 1225, -1300);
        Assert.Equal(799, result.MovementAdjustedMagicalDefense);
        Assert.Equal(108, result.NormalizedElementalDefense);
        Bits(0x435c199au, result.AfterMagicalDefense);
        Bits(0x3f6abb78u, result.ElementalMultiplier);
        Bits(0x4349d095u, result.Damage);
    }

    [Fact]
    public void ElementStageDoesNotApplyLaterFinalDamageMinimum()
    {
        var result = RetailNpcNormalDamageStages.CalculateElementalDefense(NpcToNpc(), 100f, 33, 33, false, 0, 0, 2600, 0, 1000, -1300);
        Bits(0xbf800000u, result.ElementalMultiplier);
        Bits(0xc2c80000u, result.Damage);
    }

    [Fact]
    public void ElementWordIsSignedAndItsMovementAdditionWrapsInt32()
    {
        var result = RetailNpcNormalDamageStages.CalculateElementalDefense(NpcToNpc(), 300f, 33, 33, false, 0, 0, 1, int.MaxValue, 1000, -1300);
        Assert.Equal(int.MinValue, result.NormalizedElementalDefense);
        Assert.Equal(-1300, result.ClampedElementalDefense);
        Bits(0x44160000u, result.Damage);
    }

    [Theory]
    [InlineData(true, (ushort)0, 1000u, 0x3fc00000u)]
    [InlineData(false, (ushort)0, 1000u, 0x3f800000u)]
    [InlineData(true, (ushort)250, 1000u, 0x3fa00000u)]
    [InlineData(true, (ushort)1000, 1000u, 0x3f800000u)]
    [InlineData(true, (ushort)100, 1225u, 0x3fb5a1cbu)]
    public void NpcNormalCriticalHasItsOwnTypeAndNormalization(bool physical, ushort reduction, uint ratio, uint expected)
    {
        Bits(expected, RetailNpcNormalDamageStages.CalculateNpcCriticalMultiplier(NpcToPlayer(), physical, reduction, ratio));
    }

    [Theory]
    [InlineData(0, 0, 0x3f800000u)]
    [InlineData(150, 50, 0x3f8ccccdu)]
    [InlineData(9999, 9998, 0x3f8020c0u)] // separate divisions, then addition, then subtraction
    [InlineData(-10000, 10000, 0x3dcccccdu)]
    [InlineData(10000, -10000, 0x41400000u)]
    [InlineData(int.MaxValue, int.MinValue, 0x41400000u)]
    public void PveHelperPreservesInputClampsOperationOrderAndOutputBounds(int attack, int defense, uint expected)
    {
        Bits(expected, RetailNpcNormalDamageStages.CalculatePveRatio(NpcToPlayer(), attack, defense));
    }

    [Fact]
    public void ConstructorZeroNpcLevelIsAnArithmeticOperandAndNeverAPlayerDefault()
    {
        Assert.Equal(240f,RetailNpcNormalDamageStages.CalculatePhysicalDefense(PlayerToNpc(),300,38,0,false,600,0,1000).Damage);
        Assert.Equal(240f,RetailNpcNormalDamageStages.CalculatePhysicalDefense(NpcToPlayer(),300,0,38,false,600,0,1000).Damage);
        Assert.Equal(0f,RetailNpcNormalDamageStages.ApplyLevelRule(NpcToNpc(),300,0,38,false));
        Assert.Equal(300f,RetailNpcNormalDamageStages.ApplyLevelRule(NpcToNpc(),300,0,0,false));
        Assert.Throws<ArgumentOutOfRangeException>(()=>RetailNpcNormalDamageStages.ApplyLevelRule(PlayerToNpc(),300,0,0,false));
        Assert.Throws<ArgumentOutOfRangeException>(()=>RetailNpcNormalDamageStages.ApplyLevelRule(NpcToPlayer(),300,0,0,false));
    }
}
