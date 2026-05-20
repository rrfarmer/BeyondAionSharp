namespace Aion.GameServer.Model.GameObjects;

public sealed record PlayerItemCooldown(long ReuseTimeMillis, int UseDelaySeconds)
{
	// Java parity: model/items/ItemCooldown.getReuseTime.
	public long GetReuseTime()
	{
		return ReuseTimeMillis;
	}

	// Java parity: model/items/ItemCooldown.getUseDelay.
	public int GetUseDelay()
	{
		return UseDelaySeconds;
	}
}
