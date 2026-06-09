using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model;

/// <summary>
/// Something that expires at an absolute epoch-second time.
/// Java parity: model/Expirable.
/// </summary>
public interface IExpirable
{
    // Java parity: getExpireTime()
    int GetExpireTime();

    // Java parity: default secondsUntilExpiration()
    int SecondsUntilExpiration() =>
        GetExpireTime() == 0 ? 0 : GetExpireTime() - (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000);

    // Java parity: default isExpired()
    bool IsExpired() => SecondsUntilExpiration() < 0;

    // Java parity: default onBeforeExpire(Player, int)
    void OnBeforeExpire(Aion.GameServer.Model.GameObjects.Player.Player player, int remainingMinutes) { }

    // Java parity: onExpire(Player)
    void OnExpire(Aion.GameServer.Model.GameObjects.Player.Player player);

    // Java parity: default canExpireNow()
    bool CanExpireNow() => true;
}
