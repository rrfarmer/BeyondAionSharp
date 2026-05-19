using System.Collections.Concurrent;
using Aion.LoginServer.Data;
using Aion.LoginServer.Model;

namespace Aion.LoginServer.Services;

public interface IBannedMacService
{
	Task LoadAsync(CancellationToken cancellationToken = default);

	IReadOnlyCollection<BannedMacEntry> GetEntries();

	Task BanAsync(string address, DateTime time, string details, CancellationToken cancellationToken = default);

	Task UnbanAsync(string address, CancellationToken cancellationToken = default);
}

public sealed class BannedMacService : IBannedMacService
{
	private readonly IBannedMacRepository _repository;
	private readonly ConcurrentDictionary<string, BannedMacEntry> _bannedList = new();

	public BannedMacService(IBannedMacRepository repository)
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

	public IReadOnlyCollection<BannedMacEntry> GetEntries() => _bannedList.Values.ToArray();

	public async Task BanAsync(string address, DateTime time, string details, CancellationToken cancellationToken = default)
	{
		var entry = new BannedMacEntry(address, time, details);
		_bannedList[address] = entry;
		await _repository.UpdateAsync(entry, cancellationToken);
	}

	public async Task UnbanAsync(string address, CancellationToken cancellationToken = default)
	{
		if (_bannedList.TryRemove(address, out _))
			await _repository.RemoveAsync(address, cancellationToken);
	}
}
