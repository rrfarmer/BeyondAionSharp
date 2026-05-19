namespace Aion.LoginServer.Network.GameServer;

public sealed class GameServerPingTracker
{
	private int _unrespondedPingCount;

	public bool ShouldCloseOnPingTick()
	{
		var previousUnrespondedCount = Interlocked.Increment(ref _unrespondedPingCount) - 1;
		return previousUnrespondedCount > 2;
	}

	public void OnReceivePong()
	{
		Interlocked.Exchange(ref _unrespondedPingCount, 0);
	}
}
