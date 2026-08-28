using System.Runtime.CompilerServices;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Services.Instance;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class InstanceScalerPortTests
{
    [Theory]
    [InlineData(6, 0.5f, 1f, 1, 0.5f)]
    [InlineData(6, 0.5f, 1f, 3, 0.5f)]
    [InlineData(6, 0.5f, 1f, 4, 0.6666667f)]
    [InlineData(6, 0.5f, 1f, 6, 1f)]
    [InlineData(6, 0.5f, 1f, 8, 1f)]
    [InlineData(6, 0.5f, 0.75f, 1, 0.5f)]
    [InlineData(6, 0.5f, 0.75f, 4, 0.75f)]
    [InlineData(6, 0.5f, 0.75f, 6, 1f)]
    [InlineData(6, 0.75f, 0.5f, 1, 0.75f)]
    [InlineData(6, 0.75f, 0.5f, 4, 0.8333333f)]
    [InlineData(6, 0.75f, 0.5f, 6, 1f)]
    public void MultiplierHonorsFloorScaleFactorAndMaximumPlayerCount(
        int maxPlayers, float floor, float scaleFactor, int playerCount, float expected)
    {
        Assert.Equal(expected, InstanceScaler.CalculateMultiplier(maxPlayers, floor, scaleFactor, playerCount), precision: 5);
    }

    [Fact]
    public void ScalingConfigDefaultsMatchUpstream()
    {
        Assert.Equal(5, InstanceConfig.INSTANCE_SCALING_MAX_LEVEL_DIFF);
        Assert.Equal(Model.Templates.Npc.NpcRating.ELITE, InstanceConfig.INSTANCE_SCALING_NPC_MIN_RATING);
        Assert.Equal(0.75f, InstanceConfig.INSTANCE_SCALING_HP_SCALE_FACTOR);
        Assert.Equal(0.5f, InstanceConfig.INSTANCE_SCALING_HP_FLOOR);
        Assert.Equal(0.5f, InstanceConfig.INSTANCE_SCALING_DMG_SCALE_FACTOR);
        Assert.Equal(0.75f, InstanceConfig.INSTANCE_SCALING_DMG_FLOOR);
    }

    [Fact]
    public void NpcRatingComparisonMatchesJavaOrdinalOrder()
    {
        // The ShouldScale rating gate compares enum values directly; this only matches Java's
        // ordinal() comparison while NpcRating stays implicitly sequential in declaration order.
        Assert.Equal(
            [Model.Templates.Npc.NpcRating.JUNK, Model.Templates.Npc.NpcRating.NORMAL, Model.Templates.Npc.NpcRating.ELITE,
                Model.Templates.Npc.NpcRating.HERO, Model.Templates.Npc.NpcRating.LEGENDARY],
            Enum.GetValues<Model.Templates.Npc.NpcRating>().OrderBy(r => (int)r));
        Assert.Equal([0, 1, 2, 3, 4], Enum.GetValues<Model.Templates.Npc.NpcRating>().Select(r => (int)r));
    }

    [Fact]
    public void ScalingCreatesHpAndAllDamageModifiersAtPriority120()
    {
        float originalHpFloor = InstanceConfig.INSTANCE_SCALING_HP_FLOOR;
        float originalDamageFloor = InstanceConfig.INSTANCE_SCALING_DMG_FLOOR;
        float originalHpScaleFactor = InstanceConfig.INSTANCE_SCALING_HP_SCALE_FACTOR;
        float originalDamageScaleFactor = InstanceConfig.INSTANCE_SCALING_DMG_SCALE_FACTOR;
        try
        {
            InstanceConfig.INSTANCE_SCALING_HP_FLOOR = 0.5f;
            InstanceConfig.INSTANCE_SCALING_DMG_FLOOR = 0.5f;
            InstanceConfig.INSTANCE_SCALING_HP_SCALE_FACTOR = 0.75f;
            InstanceConfig.INSTANCE_SCALING_DMG_SCALE_FACTOR = 0.5f;

            IReadOnlyList<InstanceScaler.InstanceScalerStatFunction> functions =
                InstanceScaler.Scaling.CreateStatFunctions(maxPlayers: 6, playerCount: 1);

            Assert.Equal(
                [StatEnum.MAXHP, StatEnum.PHYSICAL_ATTACK, StatEnum.MAGICAL_ATTACK, StatEnum.BOOST_SPELL_ATTACK],
                functions.Select(function => function.GetName()));
            Assert.All(functions, function => Assert.Equal(120, function.GetPriority()));

            var owner = (Npc)RuntimeHelpers.GetUninitializedObject(typeof(Npc));
            var stat = new AdditionStat(StatEnum.MAXHP, 100, owner);
            stat.SetBonus(40);
            functions[0].Apply(stat);
            Assert.Equal(70, stat.GetCurrent());
        }
        finally
        {
            InstanceConfig.INSTANCE_SCALING_HP_FLOOR = originalHpFloor;
            InstanceConfig.INSTANCE_SCALING_DMG_FLOOR = originalDamageFloor;
            InstanceConfig.INSTANCE_SCALING_HP_SCALE_FACTOR = originalHpScaleFactor;
            InstanceConfig.INSTANCE_SCALING_DMG_SCALE_FACTOR = originalDamageScaleFactor;
        }
    }

    [Fact]
    public void ScalingOnlyUpdatesWhenPlayerCountIncreases()
    {
        float originalHpFloor = InstanceConfig.INSTANCE_SCALING_HP_FLOOR;
        float originalDamageFloor = InstanceConfig.INSTANCE_SCALING_DMG_FLOOR;
        float originalHpScaleFactor = InstanceConfig.INSTANCE_SCALING_HP_SCALE_FACTOR;
        float originalDamageScaleFactor = InstanceConfig.INSTANCE_SCALING_DMG_SCALE_FACTOR;
        try
        {
            InstanceConfig.INSTANCE_SCALING_HP_FLOOR = 0.5f;
            InstanceConfig.INSTANCE_SCALING_DMG_FLOOR = 0.5f;
            InstanceConfig.INSTANCE_SCALING_HP_SCALE_FACTOR = 0.75f;
            InstanceConfig.INSTANCE_SCALING_DMG_SCALE_FACTOR = 0.5f;
            var scaling = new InstanceScaler.Scaling();

            Assert.True(scaling.Update(currentPlayerCount: 1, maxPlayers: 6));
            Assert.False(scaling.Update(currentPlayerCount: 1, maxPlayers: 6));
            Assert.True(scaling.Update(currentPlayerCount: 4, maxPlayers: 6));
            Assert.False(scaling.Update(currentPlayerCount: 2, maxPlayers: 6));

            var hpFunction = Assert.Single(scaling.StatFunctions, function => function.GetName() == StatEnum.MAXHP);
            var owner = (Npc)RuntimeHelpers.GetUninitializedObject(typeof(Npc));
            var stat = new AdditionStat(StatEnum.MAXHP, 120, owner);
            hpFunction.Apply(stat);
            Assert.Equal(90, stat.GetCurrent()); // 4/6 players, hp scale factor 0.75 -> multiplier 0.75
        }
        finally
        {
            InstanceConfig.INSTANCE_SCALING_HP_FLOOR = originalHpFloor;
            InstanceConfig.INSTANCE_SCALING_DMG_FLOOR = originalDamageFloor;
            InstanceConfig.INSTANCE_SCALING_HP_SCALE_FACTOR = originalHpScaleFactor;
            InstanceConfig.INSTANCE_SCALING_DMG_SCALE_FACTOR = originalDamageScaleFactor;
        }
    }
}
