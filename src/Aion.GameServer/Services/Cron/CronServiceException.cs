using System;

namespace Aion.GameServer.Services.Cron;

/// <summary>Java parity: services/cron/CronServiceException (RuntimeException→Exception).</summary>
public class CronServiceException : Exception
{
    public CronServiceException()
    {
    }

    public CronServiceException(string message)
        : base(message)
    {
    }

    public CronServiceException(string message, Exception cause)
        : base(message, cause)
    {
    }

    // Java parity: RuntimeException(Throwable cause) → base(null, cause).
    public CronServiceException(Exception cause)
        : base(null, cause)
    {
    }
}
