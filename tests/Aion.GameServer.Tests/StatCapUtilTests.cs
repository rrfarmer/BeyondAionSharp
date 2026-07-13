using System.Reflection;
using System.Runtime.CompilerServices;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Templates.Npc;

namespace Aion.GameServer.Tests;

[Xunit.Collection("GoldenDataManager")]
public sealed class StatCapUtilTests
{
    private readonly Creature nonPlayer = new TestCreature();

    static StatCapUtilTests()
    {
        EnsureDataManagerBridge();
    }

    [Theory]
    [InlineData(StatEnum.HEAL_BOOST, -1000, 1000)]
    [InlineData(StatEnum.PHYSICAL_CRITICAL_RESIST, 0, 700)]
    [InlineData(StatEnum.MAGICAL_CRITICAL_RESIST, 0, 700)]
    [InlineData(StatEnum.PHYSICAL_CRITICAL_DAMAGE_REDUCE, 0, 700)]
    [InlineData(StatEnum.MAGICAL_CRITICAL_DAMAGE_REDUCE, 0, 700)]
    [InlineData(StatEnum.POWER, 80, 999)]
    [InlineData(StatEnum.AGILITY, 80, 999)]
    [InlineData(StatEnum.ACCURACY, 80, 999)]
    [InlineData(StatEnum.HEALTH, 80, 999)]
    [InlineData(StatEnum.KNOWLEDGE, 80, 999)]
    [InlineData(StatEnum.WILL, 80, 999)]
    [InlineData(StatEnum.MAIN_HAND_POWER, 0, int.MaxValue)]
    [InlineData(StatEnum.OFF_HAND_CRITICAL, 0, int.MaxValue)]
    [InlineData(StatEnum.PHYSICAL_DEFENSE, 0, int.MaxValue)]
    [InlineData(StatEnum.MAGICAL_ACCURACY, 0, int.MaxValue)]
    public void FixedRulesMatchUpstream(StatEnum stat, int expectedLower, int expectedUpper)
    {
        Assert.Equal(expectedLower, StatCapUtil.GetLowerCap(stat, nonPlayer));
        Assert.Equal(expectedUpper, StatCapUtil.GetUpperCap(stat, nonPlayer));
    }

    [Fact]
    public void HitPointAndManaMinimumsDependOnCreatureType()
    {
        Player player = NewPlayer(level: 65);

        Assert.Equal(100, StatCapUtil.GetLowerCap(StatEnum.MAXHP, player));
        Assert.Equal(1, StatCapUtil.GetLowerCap(StatEnum.MAXMP, player));
        Assert.Equal(1, StatCapUtil.GetLowerCap(StatEnum.MAXHP, nonPlayer));
        Assert.Equal(0, StatCapUtil.GetLowerCap(StatEnum.MAXMP, nonPlayer));
    }

    [Fact]
    public void MovementCapsApplyOnlyToNonStaffPlayers()
    {
        Player player = NewPlayer(level: 65);
        Player staff = NewPlayer(level: 65, accessLevel: 1);

        Assert.Equal(12000, StatCapUtil.GetUpperCap(StatEnum.SPEED, player));
        Assert.Equal(16000, StatCapUtil.GetUpperCap(StatEnum.FLY_SPEED, player));
        Assert.Equal(int.MaxValue, StatCapUtil.GetUpperCap(StatEnum.SPEED, staff));
        Assert.Equal(int.MaxValue, StatCapUtil.GetUpperCap(StatEnum.FLY_SPEED, staff));
        Assert.Equal(int.MaxValue, StatCapUtil.GetUpperCap(StatEnum.SPEED, nonPlayer));
        Assert.Equal(int.MaxValue, StatCapUtil.GetUpperCap(StatEnum.FLY_SPEED, nonPlayer));
    }

    [Theory]
    [InlineData(45, 1000)]
    [InlineData(50, 1000)]
    [InlineData(65, 1150)]
    public void ElementalDefenseCapsScaleWithPlayerLevel(int level, int expectedCap)
    {
        Player player = NewPlayer(level);

        Assert.Equal(-expectedCap, StatCapUtil.GetLowerCap(StatEnum.FIRE_RESISTANCE, player));
        Assert.Equal(expectedCap, StatCapUtil.GetUpperCap(StatEnum.FIRE_RESISTANCE, player));
    }

    [Fact]
    public void ElementalDefenseUsesBaseCapForNonPlayers()
    {
        Assert.Equal(-1300, StatCapUtil.GetLowerCap(StatEnum.WATER_RESISTANCE, nonPlayer));
        Assert.Equal(1300, StatCapUtil.GetUpperCap(StatEnum.WATER_RESISTANCE, nonPlayer));
    }

    [Fact]
    public void UnregisteredStatsRemainUnlimited()
    {
        Assert.Equal(int.MinValue, StatCapUtil.GetLowerCap(StatEnum.ATTACK_SPEED, nonPlayer));
        Assert.Equal(int.MaxValue, StatCapUtil.GetUpperCap(StatEnum.ATTACK_SPEED, nonPlayer));
        Assert.Equal(int.MaxValue, StatCapUtil.GetDifferenceLimit(StatEnum.ATTACK_SPEED));
    }

    [Fact]
    public void ClampUsesDynamicPlayerRule()
    {
        Player player = NewPlayer(level: 65);

        Assert.Equal(100, StatCapUtil.ClampStatValue(StatEnum.MAXHP, player, 1));
        Assert.Equal(12000, StatCapUtil.ClampStatValue(StatEnum.SPEED, player, 15000));
    }

    private static Player NewPlayer(int level, sbyte accessLevel = 0)
    {
        return new TestPlayer(level, accessLevel);
    }

    private static void EnsureDataManagerBridge()
    {
        try
        {
            _ = DataManager.ABSOLUTE_STATS_DATA;
            return;
        }
        catch (InvalidOperationException)
        {
        }

        var staticData = (StaticData)RuntimeHelpers.GetUninitializedObject(typeof(StaticData));
        SetAutoProperty(staticData, nameof(StaticData.AbsoluteStatsDataDh), new AbsoluteStatsData());

        var constructor = typeof(DataManager).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            new[] { typeof(StaticData) },
            modifiers: null)!;
        DataManager.RegisterInstance((DataManager)constructor.Invoke(new object[] { staticData }));
    }

    private static void SetAutoProperty(object target, string propertyName, object value)
    {
        var field = target.GetType().GetField(
            $"<{propertyName}>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(target.GetType().FullName, propertyName);
        field.SetValue(target, value);
    }

    private sealed class TestPlayer : Player
    {
        private readonly sbyte level;

        public TestPlayer(int level, sbyte accessLevel)
            : base(NewAccountData(), NewAccount(accessLevel))
        {
            this.level = (sbyte)level;
        }

        public override sbyte GetLevel() => level;

        private static PlayerAccountData NewAccountData()
        {
            var common = new PlayerCommonData(1);
            common.SetPlayerClass(PlayerClass.WARRIOR);
            common.SetRace(Race.ELYOS);
            common.SetGender(Gender.MALE);
            common.SetName("StatCapHarness");
            common.SetNote("");
            return new PlayerAccountData(common, new PlayerAppearance());
        }

        private static Account NewAccount(sbyte accessLevel)
        {
            var account = new Account(1);
            account.SetAccessLevel(accessLevel);
            return account;
        }
    }

    private sealed class TestCreature : Creature
    {
        public TestCreature() : base(0, null!, null!, new NpcTemplate(), null!, false)
        {
        }

        public override sbyte GetLevel() => 1;

        public override Race GetRace() => Race.ELYOS;

        public override CreatureGameStats GetGameStats() => null!;

        public override bool IsPvpTarget(Creature creature) => false;
    }
}
