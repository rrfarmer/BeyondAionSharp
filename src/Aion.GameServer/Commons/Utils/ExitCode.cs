namespace Aion.GameServer.Commons.Utils;

/// <summary>Java parity: commons/utils/ExitCode (SoulKeeper). Exit codes for the server process.</summary>
public static class ExitCode
{
    /// <summary>Indicates that server successfully finished its work.</summary>
    public const int NORMAL = 0;

    /// <summary>Indicates that error happened in server and it needs to shutdown.</summary>
    public const int ERROR = 1;

    /// <summary>Indicates that server successfully finished its work and should be restarted.</summary>
    public const int RESTART = 2;
}
