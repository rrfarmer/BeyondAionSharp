using Aion.LoginServer.Data;
using Aion.LoginServer.Model;
using Aion.LoginServer.Services;

namespace Aion.LoginServer.Tests;

public sealed class BannedMacHddServiceTests
{
	[Fact]
	public async Task MacService_CleansExpiredBansWithoutLoadingUntilFirstUse()
	{
		var banTime = DateTime.UtcNow.AddDays(1);
		var repository = new FakeBannedMacRepository(new BannedMacEntry("aa-bb", banTime, "seed"));
		var service = new BannedMacService(repository);

		await service.CleanExpiredBansAsync();

		Assert.Equal(1, repository.CleanExpiredCalls);
		Assert.Equal(0, repository.LoadCalls);

		var entries = await service.GetEntriesAsync();

		Assert.Equal(1, repository.LoadCalls);
		var entry = Assert.Single(entries);
		Assert.Equal("aa-bb", entry.Mac);
		Assert.Equal("seed", entry.Details);
	}

	[Fact]
	public async Task MacService_BanLoadsExistingMapBeforeReplacingEntry()
	{
		var initialTime = DateTime.UtcNow.AddDays(1);
		var replacementTime = DateTime.UtcNow.AddDays(2);
		var repository = new FakeBannedMacRepository(new BannedMacEntry("aa-bb", initialTime, "seed"));
		var service = new BannedMacService(repository);

		await service.BanAsync("cc-dd", replacementTime, "added");

		Assert.Equal(1, repository.LoadCalls);
		Assert.Equal(new BannedMacEntry("cc-dd", replacementTime, "added"), repository.UpdatedEntry);
		var entries = await service.GetEntriesAsync();
		Assert.Equal(2, entries.Count);
		Assert.Contains(entries, entry => entry.Mac == "aa-bb" && entry.Details == "seed");
		Assert.Contains(entries, entry => entry.Mac == "cc-dd" && entry.Details == "added");
	}

	[Fact]
	public async Task HddService_CleansExpiredBansWithoutLoadingUntilFirstUse()
	{
		var banTime = DateTime.UtcNow.AddDays(1);
		var repository = new FakeBannedHddRepository(("disk-a", banTime));
		var service = new BannedHddService(repository);

		await service.CleanExpiredBansAsync();

		Assert.Equal(1, repository.CleanExpiredCalls);
		Assert.Equal(0, repository.LoadCalls);

		var entries = await service.GetEntriesAsync();

		Assert.Equal(1, repository.LoadCalls);
		Assert.Equal(banTime, entries["disk-a"]);
	}

	[Fact]
	public async Task HddService_UnbanLoadsExistingMapBeforeRemovingEntry()
	{
		var repository = new FakeBannedHddRepository(("disk-a", DateTime.UtcNow.AddDays(1)));
		var service = new BannedHddService(repository);

		await service.UnbanAsync("disk-a");

		Assert.Equal(1, repository.LoadCalls);
		Assert.Equal("disk-a", repository.RemovedSerial);
		Assert.Empty(await service.GetEntriesAsync());
	}

	private sealed class FakeBannedMacRepository : IBannedMacRepository
	{
		private readonly Dictionary<string, BannedMacEntry> _entries;

		public FakeBannedMacRepository(params BannedMacEntry[] entries)
		{
			_entries = entries.ToDictionary(entry => entry.Mac);
		}

		public int LoadCalls { get; private set; }

		public int CleanExpiredCalls { get; private set; }

		public BannedMacEntry? UpdatedEntry { get; private set; }

		public Task<IReadOnlyDictionary<string, BannedMacEntry>> LoadAsync(CancellationToken cancellationToken = default)
		{
			LoadCalls++;
			return Task.FromResult<IReadOnlyDictionary<string, BannedMacEntry>>(new Dictionary<string, BannedMacEntry>(_entries));
		}

		public Task<bool> UpdateAsync(BannedMacEntry entry, CancellationToken cancellationToken = default)
		{
			UpdatedEntry = entry;
			_entries[entry.Mac] = entry;
			return Task.FromResult(true);
		}

		public Task<bool> RemoveAsync(string address, CancellationToken cancellationToken = default)
		{
			return Task.FromResult(_entries.Remove(address));
		}

		public Task CleanExpiredBansAsync(CancellationToken cancellationToken = default)
		{
			CleanExpiredCalls++;
			return Task.CompletedTask;
		}
	}

	private sealed class FakeBannedHddRepository : IBannedHddRepository
	{
		private readonly Dictionary<string, DateTime> _entries;

		public FakeBannedHddRepository(params (string Serial, DateTime Time)[] entries)
		{
			_entries = entries.ToDictionary(entry => entry.Serial, entry => entry.Time);
		}

		public int LoadCalls { get; private set; }

		public int CleanExpiredCalls { get; private set; }

		public string? RemovedSerial { get; private set; }

		public Task<IReadOnlyDictionary<string, DateTime>> LoadAsync(CancellationToken cancellationToken = default)
		{
			LoadCalls++;
			return Task.FromResult<IReadOnlyDictionary<string, DateTime>>(new Dictionary<string, DateTime>(_entries));
		}

		public Task<bool> UpdateAsync(string serial, DateTime time, CancellationToken cancellationToken = default)
		{
			_entries[serial] = time;
			return Task.FromResult(true);
		}

		public Task<bool> RemoveAsync(string serial, CancellationToken cancellationToken = default)
		{
			RemovedSerial = serial;
			return Task.FromResult(_entries.Remove(serial));
		}

		public Task CleanExpiredBansAsync(CancellationToken cancellationToken = default)
		{
			CleanExpiredCalls++;
			return Task.CompletedTask;
		}
	}
}
