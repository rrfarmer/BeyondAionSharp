using System.Reflection;
using System.Runtime.CompilerServices;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests;

public sealed class EnemyCommandPortTests
{
    [Theory]
    [InlineData(CreatureType.FRIEND, CreatureType.ATTACKABLE)]
    [InlineData(CreatureType.PEACE, CreatureType.ATTACKABLE)]
    [InlineData(CreatureType.ATTACKABLE, CreatureType.ATTACKABLE)]
    [InlineData(CreatureType.AGGRESSIVE, CreatureType.AGGRESSIVE)]
    public void EnemyOfAllNpcsMakesNpcAttackable(CreatureType baseType, CreatureType expected)
    {
        Npc npc = NpcWithOverriddenType(baseType);
        Player player = UninitializedPlayer();
        player.SetCustomState(CustomPlayerState.ENEMY_OF_ALL_NPCS);

        Assert.Equal(expected, npc.GetTypeValue(player));
        Assert.Equal(
            expected is CreatureType.ATTACKABLE or CreatureType.AGGRESSIVE,
            player.IsEnemyFrom(npc));
    }

    [Theory]
    [InlineData(CreatureType.ATTACKABLE, CreatureType.PEACE)]
    [InlineData(CreatureType.AGGRESSIVE, CreatureType.PEACE)]
    [InlineData(CreatureType.FRIEND, CreatureType.FRIEND)]
    [InlineData(CreatureType.PEACE, CreatureType.PEACE)]
    public void NeutralToAllNpcsMakesNpcUnattackable(CreatureType baseType, CreatureType expected)
    {
        Npc npc = NpcWithOverriddenType(baseType);
        Player player = UninitializedPlayer();
        player.SetCustomState(CustomPlayerState.NEUTRAL_TO_ALL_NPCS);

        Assert.Equal(expected, npc.GetTypeValue(player));
        Assert.False(player.IsEnemyFrom(npc));
    }

    private static Npc NpcWithOverriddenType(CreatureType type)
    {
        var npc = (Npc)RuntimeHelpers.GetUninitializedObject(typeof(Npc));
        FieldInfo field = typeof(Npc).GetField("overriddenType", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(Npc).FullName, "overriddenType");
        field.SetValue(npc, type);
        return npc;
    }

    private static Player UninitializedPlayer() =>
        (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
}
