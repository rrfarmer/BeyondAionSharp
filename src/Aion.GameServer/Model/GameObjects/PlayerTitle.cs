namespace Aion.GameServer.Model.GameObjects;

public sealed record PlayerTitle(int Id, int ExpireTimeSeconds)
{
	// Java parity: model/gameobjects/player/title/Title.secondsUntilExpiration.
	public int SecondsUntilExpiration(DateTimeOffset now)
	{
		return ExpireTimeSeconds == 0 ? 0 : ExpireTimeSeconds - (int)now.ToUnixTimeSeconds();
	}
}
