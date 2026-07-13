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
    [InlineData(6, 0.5f, 1, 0.5f)]
    [InlineData(6, 0.5f, 3, 0.5f)]
    [InlineData(6, 0.5f, 4, 0.6666667f)]
    [InlineData(6, 0.5f, 6, 1f)]
    [InlineData(6, 0.5f, 8, 1f)]
    public void MultiplierHonorsFloorAndMaximumPlayerCount(int maxPlayers, float floor, int playerCount, float expected)
    {
        Assert.Equal(expected, InstanceScaler.CalculateMultiplier(maxPlayers, floor, playerCount), precision: 5);
    }

    [Fact]
    public void ScalingCreatesHpAndAllDamageModifiersAtPriority120()
    {
        float originalHpFloor = InstanceConfig.INSTANCE_SCALING_HP_FLOOR;
        float originalDamageFloor = InstanceConfig.INSTANCE_SCALING_DMG_FLOOR;
        try
        {
            InstanceConfig.INSTANCE_SCALING_HP_FLOOR = 0.5f;
            InstanceConfig.INSTANCE_SCALING_DMG_FLOOR = 0.5f;

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
        }
    }

    [Fact]
    public void ScalingOnlyUpdatesWhenPlayerCountIncreases()
    {
        float originalHpFloor = InstanceConfig.INSTANCE_SCALING_HP_FLOOR;
        float originalDamageFloor = InstanceConfig.INSTANCE_SCALING_DMG_FLOOR;
        try
        {
            InstanceConfig.INSTANCE_SCALING_HP_FLOOR = 0.5f;
            InstanceConfig.INSTANCE_SCALING_DMG_FLOOR = 0.5f;
            var scaling = new InstanceScaler.Scaling();

            Assert.True(scaling.Update(currentPlayerCount: 1, maxPlayers: 6));
            Assert.False(scaling.Update(currentPlayerCount: 1, maxPlayers: 6));
            Assert.True(scaling.Update(currentPlayerCount: 4, maxPlayers: 6));
            Assert.False(scaling.Update(currentPlayerCount: 2, maxPlayers: 6));

            var hpFunction = Assert.Single(scaling.StatFunctions, function => function.GetName() == StatEnum.MAXHP);
            var owner = (Npc)RuntimeHelpers.GetUninitializedObject(typeof(Npc));
            var stat = new AdditionStat(StatEnum.MAXHP, 120, owner);
            hpFunction.Apply(stat);
            Assert.Equal(80, stat.GetCurrent());
        }
        finally
        {
            InstanceConfig.INSTANCE_SCALING_HP_FLOOR = originalHpFloor;
            InstanceConfig.INSTANCE_SCALING_DMG_FLOOR = originalDamageFloor;
        }
    }
}
