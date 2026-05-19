using System.Collections.Concurrent;
using Aion.LoginServer.Data;

namespace Aion.LoginServer.Services;

public interface IBannedHddService
{
	Task LoadAsync(CancellationToken cancellationToken = default);

	IReadOnlyDictionary<string, DateTime> GetEntries();

	Task BanAsync(string serial, DateTime time, CancellationToken cancellationToken = default);

	Task UnbanAsync(string serial, CancellationToken cancellationToken = default);
}

public sealed class BannedHddService : IBannedHddService
{
	private readonly IBannedHddRepository _repository;
	private readonly ConcurrentDictionary<string, DateTime> _bannedList = new();

	public BannedHddService(IBannedHddRepository repository)
	{
		_repository = repository;
	}

	public async Task LoadAsync(CancellationToken cancellationToken = default)
	{
		_bannedList.Clear();
		await _repository.CleanExpiredBansAsync(cancellationToken);
		foreach (var entry in await _repository.LoadAsync(cancellationToken))
			_bannedList[entry.Key] = entry.Value;
	}

	public IReadOnlyDictionary<string, DateTime> GetEntries() => new Dictionary<string, DateTime>(_bannedList);

	public async Task BanAsync(string serial, DateTime time, CancellationToken cancellationToken = default)
	{
		_bannedList[serial] = time;
		await _repository.UpdateAsync(serial, time, cancellationToken);
	}

	public async Task UnbanAsync(string serial, CancellationToken cancellationToken = default)
	{
		if (_bannedList.TryRemove(serial, out _))
			await _repository.RemoveAsync(serial, cancellationToken);
	}
}
