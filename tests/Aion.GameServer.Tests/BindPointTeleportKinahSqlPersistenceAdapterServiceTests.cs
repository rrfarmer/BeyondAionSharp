using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.Data;

namespace Aion.GameServer.Tests;

public sealed class BindPointTeleportKinahSqlPersistenceAdapterServiceTests
{
	[Fact]
	public async Task ExecuteAsync_NoSqlOperationReturnsSatisfiedResultWithoutRepositoryCall()
	{
		var repository = new RecordingRepository(affectedRows: 1);
		var service = new BindPointTeleportKinahSqlPersistenceAdapterService(repository, enabled: true);
		var operationPlan = BindPointTeleportKinahPersistenceOperationPlanService.CreatePlan(
			CreateMutationPlan(currentKinah: 500, requiredPrice: 0));

		var adapterPlan = await service.ExecuteAsync(operationPlan);

		Assert.Equal(BindPointTeleportKinahSqlPersistenceAdapterStatus.NoSqlRequired, adapterPlan.Status);
		Assert.NotNull(adapterPlan.PersistenceResult);
		Assert.Equal(BindPointTeleportKinahPersistenceStatus.Saved, adapterPlan.PersistenceResult.Status);
		Assert.False(adapterPlan.WouldExecuteSql);
		Assert.False(adapterPlan.DidExecuteSql);
		Assert.Empty(repository.ExecutedOperations);
		Assert.False(adapterPlan.IsLive);
	}

	[Fact]
	public async Task ExecuteAsync_DisabledAdapterDoesNotCallRepository()
	{
		var repository = new RecordingRepository(affectedRows: 1);
		var service = new BindPointTeleportKinahSqlPersistenceAdapterService(repository, enabled: false);
		var operationPlan = CreateUpdateOperationPlan();

		var adapterPlan = await service.ExecuteAsync(operationPlan);

		Assert.Equal(BindPointTeleportKinahSqlPersistenceAdapterStatus.Disabled, adapterPlan.Status);
		Assert.True(adapterPlan.WouldExecuteSql);
		Assert.False(adapterPlan.DidExecuteSql);
		Assert.Null(adapterPlan.PersistenceResult);
		Assert.Empty(repository.ExecutedOperations);
		Assert.False(adapterPlan.IsLive);
	}

	[Fact]
	public async Task ExecuteAsync_EnabledAdapterMapsSingleAffectedRowToSaved()
	{
		var repository = new RecordingRepository(affectedRows: 1);
		var service = new BindPointTeleportKinahSqlPersistenceAdapterService(repository, enabled: true);
		var operationPlan = CreateUpdateOperationPlan();

		var adapterPlan = await service.ExecuteAsync(operationPlan);

		Assert.Equal(BindPointTeleportKinahSqlPersistenceAdapterStatus.Saved, adapterPlan.Status);
		Assert.Equal(BindPointTeleportKinahPersistenceStatus.Saved, adapterPlan.PersistenceResult?.Status);
		Assert.True(adapterPlan.DidExecuteSql);
		Assert.Single(repository.ExecutedOperations);
		Assert.Same(operationPlan, repository.ExecutedOperations.Single());
		Assert.Equal(BindPointTeleportKinahPersistenceOperationPlanService.OwnerCheckedCountUpdateSql, repository.ExecutedSql.Single());
		Assert.Equal(
			["item_count", "item_unique_id", "item_owner"],
			repository.ExecutedParameterNames.Single());
		Assert.True(adapterPlan.IsLive);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(2)]
	public async Task ExecuteAsync_EnabledAdapterMapsNonSingleAffectedRowsToMissingRow(int affectedRows)
	{
		var repository = new RecordingRepository(affectedRows);
		var service = new BindPointTeleportKinahSqlPersistenceAdapterService(repository, enabled: true);

		var adapterPlan = await service.ExecuteAsync(CreateUpdateOperationPlan());

		Assert.Equal(BindPointTeleportKinahSqlPersistenceAdapterStatus.MissingRow, adapterPlan.Status);
		Assert.Equal(BindPointTeleportKinahPersistenceStatus.MissingRow, adapterPlan.PersistenceResult?.Status);
		Assert.True(adapterPlan.PersistenceResult?.ShouldRollbackInMemoryMutation);
	}

	[Fact]
	public async Task ExecuteAsync_EnabledAdapterMapsExceptionToFailed()
	{
		var repository = new RecordingRepository(
			affectedRows: 0,
			exception: new InvalidOperationException("simulated sql failure"));
		var service = new BindPointTeleportKinahSqlPersistenceAdapterService(repository, enabled: true);

		var adapterPlan = await service.ExecuteAsync(CreateUpdateOperationPlan());

		Assert.Equal(BindPointTeleportKinahSqlPersistenceAdapterStatus.Failed, adapterPlan.Status);
		Assert.Equal(BindPointTeleportKinahPersistenceStatus.Failed, adapterPlan.PersistenceResult?.Status);
		Assert.True(adapterPlan.PersistenceResult?.ShouldRollbackInMemoryMutation);
		Assert.True(adapterPlan.DidExecuteSql);
	}

	[Fact]
	public async Task ExecuteAsync_PropagatesCancellationTokenToRepository()
	{
		var repository = new RecordingRepository(affectedRows: 1);
		var service = new BindPointTeleportKinahSqlPersistenceAdapterService(repository, enabled: true);
		using var cts = new CancellationTokenSource();

		await service.ExecuteAsync(CreateUpdateOperationPlan(), cts.Token);

		Assert.Equal(cts.Token, repository.CancellationTokens.Single());
	}

	private const int PlayerObjectId = 7001;
	private const int KinahObjectId = 1824;

	private static BindPointTeleportKinahPersistenceOperationPlan CreateUpdateOperationPlan()
	{
		return BindPointTeleportKinahPersistenceOperationPlanService.CreatePlan(
			CreateMutationPlan(currentKinah: 1_500, requiredPrice: 1_000));
	}

	private static BindPointTeleportScheduledKinahMutationPlan CreateMutationPlan(
		long currentKinah,
		long requiredPrice)
	{
		return BindPointTeleportScheduledKinahMutationPlanService.CreatePlan(
			new Player
			{
				ObjectId = PlayerObjectId,
				InventoryItems =
				[
					new InventoryItem
					{
						ObjectId = KinahObjectId,
						OwnerId = PlayerObjectId,
						ItemId = BindPointTeleportScheduledKinahMutationPlanService.KinahItemId,
						Count = currentKinah,
						Location = BindPointTeleportScheduledKinahMutationPlanService.CubeStorageId,
					},
				],
			},
			requiredPrice);
	}

	private sealed class RecordingRepository : IBindPointTeleportKinahPersistenceRepository
	{
		private readonly int _affectedRows;
		private readonly Exception? _exception;

		public RecordingRepository(int affectedRows, Exception? exception = null)
		{
			_affectedRows = affectedRows;
			_exception = exception;
		}

		public List<BindPointTeleportKinahPersistenceOperationPlan> ExecutedOperations { get; } = [];

		public List<string?> ExecutedSql { get; } = [];

		public List<IReadOnlyList<string>> ExecutedParameterNames { get; } = [];

		public List<CancellationToken> CancellationTokens { get; } = [];

		public Task<int> ExecuteKinahCountUpdateAsync(
			BindPointTeleportKinahPersistenceOperationPlan operationPlan,
			CancellationToken cancellationToken = default)
		{
			ExecutedOperations.Add(operationPlan);
			ExecutedSql.Add(operationPlan.Sql);
			ExecutedParameterNames.Add(operationPlan.Parameters.Select(parameter => parameter.Name).ToArray());
			CancellationTokens.Add(cancellationToken);
			return _exception == null
				? Task.FromResult(_affectedRows)
				: Task.FromException<int>(_exception);
		}
	}
}
