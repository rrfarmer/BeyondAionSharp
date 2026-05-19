using System.Collections.Concurrent;
using Aion.LoginServer.Data;

namespace Aion.LoginServer.Services;

public interface IBannedHddService
{
	Task LoadAsync(CancellationToken cancellationToken = default);

	Task CleanExpiredBansAsync(CancellationToken cancellationToken = default);

	Task<IReadOnlyDictionary<string, DateTime>> GetEntriesAsync(CancellationToken cancellationToken = default);

	Task BanAsync(string serial, DateTime time, CancellationToken cancellationToken = default);

	Task UnbanAsync(string serial, CancellationToken cancellationToken = default);
}

public sealed class BannedHddService : IBannedHddService
{
	private readonly IBannedHddRepository _repository;
	private readonly ConcurrentDictionary<string, DateTime> _bannedList = new();
	private readonly SemaphoreSlim _loadLock = new(1, 1);
	private bool _loaded;

	public BannedHddService(IBannedHddRepository repository)
	{
		_repository = repository;
	}

	public async Task LoadAsync(CancellationToken cancellationToken = default)
	{
		if (_loaded)
			return;

		await _loadLock.WaitAsync(cancellationToken);
		try
		{
			if (_loaded)
				return;

			_bannedList.Clear();
			foreach (var entry in await _repository.LoadAsync(cancellationToken))
				_bannedList[entry.Key] = entry.Value;
			_loaded = true;
		}
		finally
		{
			_loadLock.Release();
		}
	}

	public Task CleanExpiredBansAsync(CancellationToken cancellationToken = default)
	{
		return _repository.CleanExpiredBansAsync(cancellationToken);
	}

	public async Task<IReadOnlyDictionary<string, DateTime>> GetEntriesAsync(CancellationToken cancellationToken = default)
	{
		await LoadAsync(cancellationToken);
		return new Dictionary<string, DateTime>(_bannedList);
	}

	public async Task BanAsync(string serial, DateTime time, CancellationToken cancellationToken = default)
	{
		await LoadAsync(cancellationToken);
		_bannedList[serial] = time;
		await _repository.UpdateAsync(serial, time, cancellationToken);
	}

	public async Task UnbanAsync(string serial, CancellationToken cancellationToken = default)
	{
		await LoadAsync(cancellationToken);
		if (_bannedList.TryRemove(serial, out _))
			await _repository.RemoveAsync(serial, cancellationToken);
	}
}
