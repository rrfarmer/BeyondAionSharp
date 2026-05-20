namespace Aion.GameServer.Model.GameObjects;

public sealed record PlayerPortalCooldown(int WorldId, long ReuseTimeMillis, int EntryCount)
{
	// Java parity: model/gameobjects/player/PortalCooldown.getWorldId.
	public int GetWorldId()
	{
		return WorldId;
	}

	// Java parity: model/gameobjects/player/PortalCooldown.getReuseTime.
	public long GetReuseTime()
	{
		return ReuseTimeMillis;
	}

	// Java parity: model/gameobjects/player/PortalCooldown.getEnterCount.
	public int GetEnterCount()
	{
		return EntryCount;
	}
}
