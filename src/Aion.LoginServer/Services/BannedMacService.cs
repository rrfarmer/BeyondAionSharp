using System.Collections.Concurrent;
using Aion.LoginServer.Data;
using Aion.LoginServer.Model;

namespace Aion.LoginServer.Services;

public interface IBannedMacService
{
	Task LoadAsync(CancellationToken cancellationToken = default);

	Task CleanExpiredBansAsync(CancellationToken cancellationToken = default);

	Task<IReadOnlyCollection<BannedMacEntry>> GetEntriesAsync(CancellationToken cancellationToken = default);

	Task BanAsync(string address, DateTime time, string details, CancellationToken cancellationToken = default);

	Task UnbanAsync(string address, CancellationToken cancellationToken = default);
}

public sealed class BannedMacService : IBannedMacService
{
	private readonly IBannedMacRepository _repository;
	private readonly ConcurrentDictionary<string, BannedMacEntry> _bannedList = new();
	private readonly SemaphoreSlim _loadLock = new(1, 1);
	private bool _loaded;

	public BannedMacService(IBannedMacRepository repository)
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

	public async Task<IReadOnlyCollection<BannedMacEntry>> GetEntriesAsync(CancellationToken cancellationToken = default)
	{
		await LoadAsync(cancellationToken);
		return _bannedList.Values.ToArray();
	}

	public async Task BanAsync(string address, DateTime time, string details, CancellationToken cancellationToken = default)
	{
		await LoadAsync(cancellationToken);
		var entry = new BannedMacEntry(address, time, details);
		_bannedList[address] = entry;
		await _repository.UpdateAsync(entry, cancellationToken);
	}

	public async Task UnbanAsync(string address, CancellationToken cancellationToken = default)
	{
		await LoadAsync(cancellationToken);
		if (_bannedList.TryRemove(address, out _))
			await _repository.RemoveAsync(address, cancellationToken);
	}
}
