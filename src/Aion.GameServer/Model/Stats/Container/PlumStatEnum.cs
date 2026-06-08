namespace Aion.GameServer.Model.Stats.Container;

/// <summary>Java parity: model/stats/container/PlumStatEnum (enum with id/boostValue data).</summary>
public enum PlumStatEnum
{
    PLUM_HP,
    PLUM_BOOST_MAGICAL_SKILL,
    PLUM_PHISICAL_ATTACK,
    PLUM_SPEED
}

public static class PlumStatEnumExtensions
{
    public static int GetId(this PlumStatEnum e) => e switch
    {
        PlumStatEnum.PLUM_HP => 42,
        PlumStatEnum.PLUM_BOOST_MAGICAL_SKILL => 35,
        PlumStatEnum.PLUM_PHISICAL_ATTACK => 30,
        PlumStatEnum.PLUM_SPEED => 40,
        _ => 0
    };

    public static int GetBoostValue(this PlumStatEnum e) => e switch
    {
        PlumStatEnum.PLUM_HP => 150,
        PlumStatEnum.PLUM_BOOST_MAGICAL_SKILL => 20,
        PlumStatEnum.PLUM_PHISICAL_ATTACK => 4,
        PlumStatEnum.PLUM_SPEED => 0,
        _ => 0
    };
}
