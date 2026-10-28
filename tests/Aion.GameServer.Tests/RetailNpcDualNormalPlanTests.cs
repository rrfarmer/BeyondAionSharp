using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Controllers.Attack.RetailNpc;

namespace Aion.GameServer.Tests;

/// <summary>Independent native-boundary witnesses. No RNG, actor, timing or HP
/// simulation is substituted by this pure construction/status unit.</summary>
public sealed class RetailNpcDualNormalPlanTests
{
    [Theory]
    [InlineData(0, 1, 0, 0)]
    [InlineData(1, 3, 0, 1)]
    [InlineData(225, 339, 112, 169)]
    [InlineData(65534, 65535, 32767, 32767)]
    [InlineData(339, 225, 169, 112)]
    public void LateSplitPreservesOddZeroMaximumAndUnsortedEndpoints(int first, int second, int minimum, int maximum)
    {
        var range = RetailNpcDualNormalPlan.Split(first, second, 1);
        Assert.Equal(minimum, range.Main.Minimum); Assert.Equal(maximum, range.Main.Maximum);
        Assert.Equal(range.Main, range.Offhand); Assert.Equal(1, range.MainHitCount); Assert.Equal(1, range.OffhandHitCount);
        Assert.Equal(2, range.PrimaryRecordCount); Assert.Equal(0, range.AdditionalRecordCount);
    }

    [Fact]
    public void AttackFlatRemainsALaterPerHandOperandInsteadOfChangingSplitEndpoints()
    {
        var ranges = RetailNpcDualNormalPlan.Split(100, 100, 1);
        var scope = RetailNpcNormalScope.Create(RetailNpcNormalScope.MainBinarySha256,
            RetailNpcNormalActorKind.OrdinaryOwnerlessNpc, RetailNpcNormalActorKind.Player, 215784, 0);
        var selected = RetailNpcNormalRange.Create(scope, ranges.Main.Minimum, ranges.Main.Maximum);
        var main = RetailNpcNormalDamageStages.CalculateNpcBase(scope, selected, null, 1, 0, 0, 20, 0);
        var off = RetailNpcNormalDamageStages.CalculateNpcBase(scope, selected, null, 1, 0, 0, 20, 0);
        Assert.Equal(70f, main.AfterMinimum); Assert.Equal(140f, main.AfterMinimum + off.AfterMinimum);
        Assert.Equal(50, ranges.Main.Minimum);
    }

    [Fact]
    public void ArmorIsAppliedToEachHandInsteadOfTheCombinedRawDamage()
    {
        var scope = RetailNpcNormalScope.Create(RetailNpcNormalScope.MainBinarySha256,
            RetailNpcNormalActorKind.OrdinaryOwnerlessNpc, RetailNpcNormalActorKind.Player, 215784, 0);
        float main = RetailNpcNormalDamageStages.CalculatePhysicalDefense(scope, 50, 1, 1, false, 400, 0, 1000).Damage;
        float off = RetailNpcNormalDamageStages.CalculatePhysicalDefense(scope, 50, 1, 1, false, 400, 0, 1000).Damage;
        Assert.Equal(20f, main + off);
    }

    [Theory]
    [InlineData(-1, 10, 1)]
    [InlineData(0, 65536, 1)]
    [InlineData(0, 10, 0)]
    [InlineData(0, 10, 2)]
    public void WidenedOrMultiHitInputsCannotSilentlyEnterTheTwoRecordDomain(int first, int second, int hits)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RetailNpcDualNormalPlan.Split(first, second, hits));
    }

    [Theory]
    [InlineData(AttackStatus.DODGE)]
    [InlineData(AttackStatus.RESIST)]
    [InlineData(AttackStatus.CRITICAL_DODGE)]
    [InlineData(AttackStatus.CRITICAL_RESIST)]
    public void MainTerminalMissHasNoOffhandSampleOrRecord(AttackStatus main)
    {
        var outcome = RetailNpcDualNormalPlan.Share(main);
        Assert.True(outcome.MainTerminal); Assert.False(RetailNpcDualNormalPlan.ShouldCalculateOffhand(outcome));
        Assert.Null(outcome.OffhandStatus); Assert.Null(outcome.OffhandNative);
        Assert.Throws<InvalidOperationException>(() => RetailNpcDualNormalPlan.ApplySharedOffhand(100, outcome, true, 1000, 0, 100, 0));
    }

    [Theory]
    [InlineData(AttackStatus.NORMALHIT, AttackStatus.OFFHAND_NORMALHIT, 10, 0, 11)]
    [InlineData(AttackStatus.PARRY, AttackStatus.OFFHAND_PARRY, 2, 0, 3)]
    [InlineData(AttackStatus.BLOCK, AttackStatus.OFFHAND_BLOCK, 4, 0, 5)]
    [InlineData(AttackStatus.CRITICAL, AttackStatus.OFFHAND_CRITICAL, 10, 12, 219)]
    [InlineData(AttackStatus.CRITICAL_PARRY, AttackStatus.OFFHAND_CRITICAL_PARRY, 2, 12, 211)]
    [InlineData(AttackStatus.CRITICAL_BLOCK, AttackStatus.OFFHAND_CRITICAL_BLOCK, 4, 12, 213)]
    public void OffhandUsesTheSharedOutcomeAndDistinctNativeStatusBytes(AttackStatus main, AttackStatus off, int nativeMainBase,
        int nativeMainCritical, int packedOffhand)
    {
        var outcome = RetailNpcDualNormalPlan.Share(main);
        Assert.False(outcome.MainTerminal); Assert.True(RetailNpcDualNormalPlan.ShouldCalculateOffhand(outcome));
        Assert.Equal(off, outcome.OffhandStatus); Assert.Equal(nativeMainBase, outcome.MainNative.Base);
        Assert.Equal(nativeMainCritical, outcome.MainNative.Critical);
        Assert.Equal((byte)packedOffhand, outcome.OffhandNative!.Value.Packed);
        Assert.Equal(unchecked((byte)off.GetId()), outcome.OffhandNative.Value.Packed);
    }

    [Fact]
    public void AnOffhandStatusCannotBeReinterpretedAsANewMainDecision()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RetailNpcDualNormalPlan.Share(AttackStatus.OFFHAND_CRITICAL));
    }

    [Fact]
    public void EachHandRetainsItsOwnDefenseAndCurrentShieldAbsorption()
    {
        var normal = RetailNpcDualNormalPlan.Share(AttackStatus.NORMALHIT);
        Assert.Equal(12f, RetailNpcDualNormalPlan.ApplySharedOffhand(12, normal, true, 1000, 0, 100, 0));
        var block = RetailNpcDualNormalPlan.Share(AttackStatus.BLOCK);
        Assert.Equal(70f, RetailNpcDualNormalPlan.ApplySharedOffhand(100, block, true, 1000, 0, 40, 30));
        var parryCritical = RetailNpcDualNormalPlan.Share(AttackStatus.CRITICAL_PARRY);
        // Native binary32 .6f then1.5f: the result is one ULP above90.
        Assert.Equal(0x42b40001u, BitConverter.SingleToUInt32Bits(
            RetailNpcDualNormalPlan.ApplySharedOffhand(100, parryCritical, true, 1000, 0, 100, 0)));
    }

    [Fact]
    public void ElementalCriticalUsesTheNpcCoefficientAndRatioNormalizedReduction()
    {
        var critical = RetailNpcDualNormalPlan.Share(AttackStatus.CRITICAL);
        Assert.Equal(100f, RetailNpcDualNormalPlan.ApplySharedOffhand(100, critical, false, 1000, 700, 100, 0));
        Assert.Equal(130f, RetailNpcDualNormalPlan.ApplySharedOffhand(100, critical, true, 2000, 400, 100, 0));
    }
}
