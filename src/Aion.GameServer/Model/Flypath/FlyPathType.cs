namespace Aion.GameServer.Model.Flypath;

/// <summary>Java parity: model/flypath/FlyPathType (xTz). Ids 0/1/2 match ordinals.</summary>
public enum FlyPathType
{
    GEYSER,
    ONE_WAY,
    TWO_WAY
}

public static class FlyPathTypeExtensions
{
    public static int GetId(this FlyPathType t) => (int) t;
}
