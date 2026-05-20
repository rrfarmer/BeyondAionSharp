namespace Aion.GameServer.Model.GameObjects;

public sealed record PlayerEmotion(int Id, int ExpireTimeSeconds)
{
	// Java parity: model/gameobjects/player/emotion/Emotion.secondsUntilExpiration.
	public int SecondsUntilExpiration(DateTimeOffset now)
	{
		return ExpireTimeSeconds == 0 ? 0 : ExpireTimeSeconds - (int)now.ToUnixTimeSeconds();
	}
}
