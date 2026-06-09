namespace Aion.GameServer.Services.Ban;

/// <summary>Java parity: services/ban/BanAction. Per-instance id matches ordinal → GetId()=(int)t.</summary>
public enum BanAction
{
    UNBAN,
    BAN
}

public static class BanActionExtensions
{
    public static int GetId(this BanAction a) => (int) a;
}
