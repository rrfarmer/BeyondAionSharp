namespace Aion.LoginServer.Model;

public sealed class AccountTime
{
	public DateTime LastLoginTime { get; set; } = DateTime.UtcNow;

	public DateTime? ExpirationTime { get; set; }

	public DateTime? PenaltyEnd { get; set; }

	public long SessionDuration { get; set; }

	public long AccumulatedOnlineTime { get; set; }

	public long AccumulatedRestTime { get; set; }
}
