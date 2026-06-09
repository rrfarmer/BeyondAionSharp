namespace Aion.GameServer.Services.Mail;

/// <summary>Java parity: services/mail/SiegeResult. Per-instance id matches ordinal → GetId()=(int)t.</summary>
public enum SiegeResult
{
    DEFENCE,
    OCCUPY,
    PROTECT,
    DEFENDER,
    EMPTY,
    FAIL
}

public static class SiegeResultExtensions
{
    public static int GetId(this SiegeResult r) => (int) r;
}
