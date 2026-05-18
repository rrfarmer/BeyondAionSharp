namespace Aion.LoginServer.Model;

public sealed class AccountTime
{
	public long AccumulatedOnlineTime { get; init; }

	public long AccumulatedRestTime { get; init; }
}
