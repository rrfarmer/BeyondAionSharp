using System.Collections.Concurrent;
using Aion.LoginServer.Data;
using Aion.LoginServer.Model;
using Aion.LoginServer.Utils;

namespace Aion.LoginServer.Services;

public interface IBannedIpService
{
	Task LoadAsync(CancellationToken cancellationToken = default);

	IReadOnlyCollection<BannedIp> GetEntries();

	bool IsBanned(string ip);

	Task<bool> BanAsync(string mask, DateTime? expireTime, CancellationToken cancellationToken = default);

	Task<bool> UnbanAsync(string mask, CancellationToken cancellationToken = default);
}

public sealed class BannedIpService : IBannedIpService
{
	private readonly IBannedIpRepository _repository;
	private readonly ConcurrentDictionary<string, BannedIp> _bannedList = new(StringComparer.Ordinal);

	public BannedIpService(IBannedIpRepository repository)
	{
		_repository = repository;
	}

	public async Task LoadAsync(CancellationToken cancellationToken = default)
	{
		_bannedList.Clear();
		await _repository.CleanExpiredBansAsync(cancellationToken);
		foreach (var ban in await _repository.GetAllBansAsync(cancellationToken))
			_bannedList[ban.Mask] = ban;
	}

	public IReadOnlyCollection<BannedIp> GetEntries() => _bannedList.Values.ToArray();

	public bool IsBanned(string ip)
	{
		var now = DateTime.UtcNow;
		foreach (var ban in _bannedList.Values)
		{
			if (ban.IsActive(now) && NetworkMask.Matches(ban.Mask, ip))
				return true;
		}
		return false;
	}

	public async Task<bool> BanAsync(string mask, DateTime? expireTime, CancellationToken cancellationToken = default)
	{
		var ban = new BannedIp
		{
			Mask = mask,
			TimeEnd = expireTime,
		};
		if (!_bannedList.TryAdd(mask, ban))
			return false;

		return await _repository.InsertAsync(mask, expireTime, cancellationToken);
	}

	public async Task<bool> UnbanAsync(string mask, CancellationToken cancellationToken = default)
	{
		if (!_bannedList.TryGetValue(mask, out _))
			return false;

		if (!await _repository.RemoveAsync(mask, cancellationToken))
			return false;

		_bannedList.TryRemove(mask, out _);
		return true;
	}
}
