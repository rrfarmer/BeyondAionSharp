using Aion.GameServer.Model;

namespace Aion.GameServer.Utils.Extensions;

// Java parity: model/Expirable default methods secondsUntilExpiration()/isExpired() are callable on the
// implementing object. C# default-interface-methods are NOT accessible through the implementing type
// (only through the interface), so Java-faithful calls like `title.SecondsUntilExpiration()` fail to bind.
// These extensions restore that call site on the concrete types; interface-typed receivers still use the
// interface's own default (a more-specific member wins), so there is no ambiguity.
public static class ExpirableExtensions
{
	public static int SecondsUntilExpiration(this IExpirable expirable) =>
		expirable.GetExpireTime() == 0
			? 0
			: expirable.GetExpireTime() - (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000);

	public static bool IsExpired(this IExpirable expirable) => expirable.SecondsUntilExpiration() < 0;
}
