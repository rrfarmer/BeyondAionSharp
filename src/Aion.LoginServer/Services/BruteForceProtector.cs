using System.Collections.Concurrent;

namespace Aion.LoginServer.Services;

public interface IBruteForceProtector
{
	bool AddFailedConnect(string ip, int loginTryBeforeBan, int banWindowMinutes);
}

public sealed class BruteForceProtector : IBruteForceProtector
{
	private readonly ConcurrentDictionary<string, FailedLoginInfo> _failedConnections = new();

	public bool AddFailedConnect(string ip, int loginTryBeforeBan, int banWindowMinutes)
	{
		var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		var banWindowMillis = banWindowMinutes * 60_000L;
		if (!_failedConnections.TryGetValue(ip, out var failed) || failed.Time + banWindowMillis < now)
		{
			_failedConnections[ip] = new FailedLoginInfo(1, now);
			return false;
		}

		if (failed.Count >= loginTryBeforeBan)
		{
			_failedConnections.TryRemove(ip, out _);
			return true;
		}

		failed.IncreaseCount();
		return false;
	}

	private sealed class FailedLoginInfo
	{
		private int _count;

		public FailedLoginInfo(int count, long time)
		{
			_count = count;
			Time = time;
		}

		public int Count => Volatile.Read(ref _count);

		public long Time { get; }

		public void IncreaseCount()
		{
			Interlocked.Increment(ref _count);
		}
	}
}
