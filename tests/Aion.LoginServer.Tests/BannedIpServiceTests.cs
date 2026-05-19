using Aion.LoginServer.Data;
using Aion.LoginServer.Model;
using Aion.LoginServer.Services;

namespace Aion.LoginServer.Tests;

public sealed class BannedIpServiceTests
{
	[Fact]
	public async Task LoadAsync_CleansExpiredBansAndUsesLoadedMaskCache()
	{
		var repository = new FakeBannedIpRepository(
			new BannedIp { Mask = "10.0.0.*" },
			new BannedIp { Mask = "192.168.0.1", TimeEnd = DateTime.UtcNow.AddMinutes(-1) });
		var service = new BannedIpService(repository);

		await service.LoadAsync();

		Assert.Equal(1, repository.CleanCalls);
		Assert.Equal(1, repository.GetAllCalls);
		Assert.True(service.IsBanned("10.0.0.25"));
		Assert.False(service.IsBanned("192.168.0.1"));
	}

	[Fact]
	public async Task BanAndUnban_UpdateRepositoryAndCacheWithJavaDuplicateBehavior()
	{
		var repository = new FakeBannedIpRepository();
		var service = new BannedIpService(repository);
		await service.LoadAsync();

		Assert.True(await service.BanAsync("10.0.0.1", DateTime.UtcNow.AddMinutes(5)));
		Assert.True(service.IsBanned("10.0.0.1"));
		Assert.Equal(new[] { "10.0.0.1" }, repository.InsertedMasks);

		Assert.False(await service.BanAsync("10.0.0.1", DateTime.UtcNow.AddMinutes(10)));
		Assert.Equal(new[] { "10.0.0.1" }, repository.InsertedMasks);

		Assert.True(await service.UnbanAsync("10.0.0.1"));
		Assert.False(service.IsBanned("10.0.0.1"));
		Assert.Equal(new[] { "10.0.0.1" }, repository.RemovedMasks);
		Assert.False(await service.UnbanAsync("10.0.0.1"));
	}

	[Fact]
	public async Task ExpiredLoadedMaskStillPreventsDuplicateBanLikeJavaHashSet()
	{
		var ban = new BannedIp { Mask = "10.0.0.2", TimeEnd = DateTime.UtcNow.AddMinutes(1) };
		var repository = new FakeBannedIpRepository(ban);
		var service = new BannedIpService(repository);
		await service.LoadAsync();
		ban.TimeEnd = DateTime.UtcNow.AddMinutes(-1);

		Assert.False(service.IsBanned("10.0.0.2"));
		Assert.False(await service.BanAsync("10.0.0.2", DateTime.UtcNow.AddMinutes(10)));
		Assert.Empty(repository.InsertedMasks);
	}

	private sealed class FakeBannedIpRepository : IBannedIpRepository
	{
		private readonly List<BannedIp> _bans;

		public FakeBannedIpRepository(params BannedIp[] bans)
		{
			_bans = bans.ToList();
		}

		public int CleanCalls { get; private set; }

		public int GetAllCalls { get; private set; }

		public List<string> InsertedMasks { get; } = new();

		public List<string> RemovedMasks { get; } = new();

		public Task CleanExpiredBansAsync(CancellationToken cancellationToken = default)
		{
			CleanCalls++;
			_bans.RemoveAll(ban => !ban.IsActive(DateTime.UtcNow));
			return Task.CompletedTask;
		}

		public Task<IReadOnlyCollection<BannedIp>> GetAllBansAsync(CancellationToken cancellationToken = default)
		{
			GetAllCalls++;
			return Task.FromResult<IReadOnlyCollection<BannedIp>>(_bans.ToArray());
		}

		public Task<bool> InsertAsync(string mask, DateTime? expireTime, CancellationToken cancellationToken = default)
		{
			InsertedMasks.Add(mask);
			return Task.FromResult(true);
		}

		public Task<bool> RemoveAsync(string mask, CancellationToken cancellationToken = default)
		{
			RemovedMasks.Add(mask);
			return Task.FromResult(true);
		}
	}
}
