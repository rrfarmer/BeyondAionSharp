using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PlayerKiskReviveServiceTests
{
	[Fact]
	public void TryUseKiskReviveMatchesJavaDeadGate()
	{
		var registry = new PlayerKiskRegistry();
		var kisk = new PlayerKiskRuntimeState(objectId: 9001, ownerObjectId: 1001, npcId: 700273);
		registry.RegisterKisk(kisk);
		var alivePlayer = CreatePlayer(boundKiskObjectId: 9001, currentHp: 100);

		var result = PlayerKiskReviveService.TryUseKiskRevive(
			alivePlayer,
			registry,
			_ => new WorldPosition(210010000, 1, 2, 3, 0));

		Assert.Equal(PlayerKiskReviveStatus.PlayerNotDead, result.Status);
		Assert.False(result.UsedKisk);
		Assert.Equal(kisk.MaxResurrects, kisk.RemainingResurrects);
	}

	[Fact]
	public void TryUseKiskReviveRejectsMissingInactiveOrUnpositionedKisksBeforeChargeUse()
	{
		var registry = new PlayerKiskRegistry();
		var missing = CreatePlayer(boundKiskObjectId: 9001);
		var inactiveKisk = new PlayerKiskRuntimeState(objectId: 9002, ownerObjectId: 1001, npcId: 700273, maxResurrects: 1);
		Assert.True(inactiveKisk.UseResurrection());
		registry.RegisterKisk(inactiveKisk);
		var inactive = CreatePlayer(boundKiskObjectId: 9002);
		var unpositionedKisk = new PlayerKiskRuntimeState(objectId: 9003, ownerObjectId: 1003, npcId: 700273);
		registry.RegisterKisk(unpositionedKisk);
		var unpositioned = CreatePlayer(boundKiskObjectId: 9003);

		var missingResult = PlayerKiskReviveService.TryUseKiskRevive(missing, registry, _ => null);
		var inactiveResult = PlayerKiskReviveService.TryUseKiskRevive(inactive, registry, _ => new WorldPosition(210010000, 1, 2, 3, 0));
		var unpositionedResult = PlayerKiskReviveService.TryUseKiskRevive(unpositioned, registry, _ => null);

		Assert.Equal(PlayerKiskReviveStatus.NoBoundKisk, missingResult.Status);
		Assert.Equal(PlayerKiskReviveStatus.KiskInactive, inactiveResult.Status);
		Assert.Equal(PlayerKiskReviveStatus.MissingKiskPosition, unpositionedResult.Status);
		Assert.Equal(0, inactiveKisk.RemainingResurrects);
		Assert.Equal(unpositionedKisk.MaxResurrects, unpositionedKisk.RemainingResurrects);
	}

	[Fact]
	public void TryUseKiskReviveConsumesChargeAndReturnsUpdateIntent()
	{
		var registry = new PlayerKiskRegistry();
		var kisk = new PlayerKiskRuntimeState(objectId: 9001, ownerObjectId: 1001, npcId: 700273, maxResurrects: 2);
		registry.RegisterKisk(kisk);
		var player = CreatePlayer(boundKiskObjectId: 9001);
		var position = new WorldPosition(210010000, 11, 22, 33, 0);

		var result = PlayerKiskReviveService.TryUseKiskRevive(player, registry, _ => position);

		Assert.Equal(PlayerKiskReviveStatus.Used, result.Status);
		Assert.True(result.UsedKisk);
		Assert.False(result.ShouldDeleteKisk);
		Assert.Same(kisk, result.Kisk);
		Assert.Equal(position, result.KiskPosition);
		Assert.Equal(1, result.Resurrection?.RemainingResurrections);
		Assert.Equal(1, kisk.RemainingResurrects);
	}

	[Fact]
	public void TryUseKiskReviveFlagsDeleteWhenLastChargeIsConsumed()
	{
		var registry = new PlayerKiskRegistry();
		var kisk = new PlayerKiskRuntimeState(objectId: 9001, ownerObjectId: 1001, npcId: 700273, maxResurrects: 1);
		registry.RegisterKisk(kisk);
		var player = CreatePlayer(boundKiskObjectId: 9001);

		var result = PlayerKiskReviveService.TryUseKiskRevive(
			player,
			registry,
			_ => new WorldPosition(210010000, 1, 2, 3, 0));

		Assert.Equal(PlayerKiskReviveStatus.Depleted, result.Status);
		Assert.True(result.UsedKisk);
		Assert.True(result.ShouldDeleteKisk);
		Assert.Equal(0, result.Resurrection?.RemainingResurrections);
		Assert.Equal(0, kisk.RemainingResurrects);
	}

	private static Player CreatePlayer(int boundKiskObjectId, int currentHp = 0)
	{
		return new Player
		{
			ObjectId = 1002,
			BoundKiskObjectId = boundKiskObjectId,
			LifeStats = new PlayerLifeStats(currentHp, CurrentMp: 0, CurrentFp: 0),
		};
	}
}
