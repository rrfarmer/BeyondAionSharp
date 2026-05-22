using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Tests;

public sealed class HousingWorldServiceTests
{
	[Fact]
	public async Task LoadWorldHousesAsync_StoresPersistentHouseSnapshotsInWorld()
	{
		var templates = new HousingTemplateTable(
			[new HousingAddressSummary(700100, 1, 798000, MapId: 210010000, X: 10, Y: 20, Z: 30)],
			[new HousingBuildingSummary(730001, "HOUSE", 1)]);
		var worldHouse = new WorldHouse(
			5001,
			700100,
			730001,
			1001,
			"Owner",
			0,
			string.Empty,
			0,
			0,
			0,
			0,
			0,
			0,
			false,
			PlayerHouse.DoorOpen,
			true,
			"notice",
			new WorldPosition(210010000, 10, 20, 30, 0));
		var repository = new CapturingHousingRepository([worldHouse]);
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var service = new HousingWorldService(
			repository,
			new GameServerRuntimeContext(),
			world,
			NullLogger<HousingWorldService>.Instance);

		var loaded = await service.LoadWorldHousesAsync(templates);

		Assert.Equal(1, loaded);
		Assert.Equal(1, service.LoadedCount);
		Assert.Same(templates, repository.Templates);
		Assert.Same(worldHouse, Assert.Single(world.GetHouses()));
		Assert.True(world.TryGetObject(worldHouse.ObjectId, out var stored));
		Assert.Same(worldHouse, stored);
	}

	private sealed class CapturingHousingRepository : IHousingRepository
	{
		private readonly IReadOnlyList<WorldHouse> _houses;

		public CapturingHousingRepository(IReadOnlyList<WorldHouse> houses)
		{
			_houses = houses;
		}

		public HousingTemplateTable? Templates { get; private set; }

		public Task<IReadOnlyList<WorldHouse>> LoadWorldHousesAsync(HousingTemplateTable housingTemplates, CancellationToken cancellationToken = default)
		{
			Templates = housingTemplates;
			return Task.FromResult(_houses);
		}
	}
}
