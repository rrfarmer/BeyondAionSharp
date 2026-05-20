namespace Aion.GameServer.Model.GameObjects;

public sealed record PlayerMotion(int Id, int ExpireTimeSeconds, bool IsActive)
{
	// Java parity: model/gameobjects/player/motion/Motion.secondsUntilExpiration.
	public int SecondsUntilExpiration(DateTimeOffset now)
	{
		return ExpireTimeSeconds == 0 ? 0 : ExpireTimeSeconds - (int)now.ToUnixTimeSeconds();
	}
}
