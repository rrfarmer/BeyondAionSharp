using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class WorldNpcLootServiceTests
{
	[Fact]
	public void RequestDropList_OpensVisibleDropsAndMarksPlayerLooting()
	{
		var dropRegistration = new WorldNpcDropRegistrationService();
		var visibleDrop = new WorldNpcDropItem(1, 182400001, 25, new HashSet<int> { 1001 });
		var hiddenDrop = new WorldNpcDropItem(2, 166020000, 1, new HashSet<int> { 1002 });
		dropRegistration.RegisterDrop(5001, looterObjectId: 1001, drops: [visibleDrop, hiddenDrop]);
		var service = new WorldNpcLootService(dropRegistration);
		var player = CreatePlayer(1001);

		var result = service.RequestDropList(player, 5001);

		Assert.Equal(WorldNpcLootStatus.Opened, result.Status);
		Assert.False(player.IsInState(PlayerCreatureState.Active));
		Assert.True(player.IsLooting);
		Assert.Equal(5001, player.LootingNpcObjectId);
		Assert.True(dropRegistration.TryGetRegistration(5001, out var registration));
		Assert.Equal(1001, registration!.LootingPlayerObjectId);
		var itemList = Assert.IsType<SmLootItemList>(Assert.Single(result.PlayerPackets.OfType<SmLootItemList>()));
		Assert.Equal([visibleDrop], itemList.DropItems);
		var status = Assert.IsType<SmLootStatus>(Assert.Single(result.PlayerPackets.OfType<SmLootStatus>()));
		Assert.Equal(SmLootStatusType.OpenDropList, status.Status);
		var emotion = Assert.IsType<SmEmotion>(Assert.Single(result.VisiblePlayerPackets));
		Assert.Equal(37, emotion.OpCode);
	}

	[Fact]
	public void RequestDropList_RejectsPlayerWithoutLootRights()
	{
		var dropRegistration = new WorldNpcDropRegistrationService();
		dropRegistration.RegisterDrop(5001, looterObjectId: 1001, drops: [new WorldNpcDropItem(1, 182400001, 1)]);
		var service = new WorldNpcLootService(dropRegistration);
		var player = CreatePlayer(1002);

		var result = service.RequestDropList(player, 5001);

		Assert.Equal(WorldNpcLootStatus.NoRight, result.Status);
		Assert.False(player.IsLooting);
		var message = Assert.IsType<SmSystemMessage>(Assert.Single(result.PlayerPackets));
		Assert.Equal(901338, message.MessageId);
	}

	[Fact]
	public void RequestDropList_RejectsAlreadyLootedCorpse()
	{
		var dropRegistration = new WorldNpcDropRegistrationService();
		dropRegistration.RegisterDrop(5001, looterObjectId: 1001, drops: [new WorldNpcDropItem(1, 182400001, 1)]);
		dropRegistration.TryGetRegistration(5001, out var registration);
		Assert.True(registration!.TryBeginLooting(1002, out _));
		var service = new WorldNpcLootService(dropRegistration);
		var player = CreatePlayer(1001);

		var result = service.RequestDropList(player, 5001);

		Assert.Equal(WorldNpcLootStatus.AlreadyLooted, result.Status);
		Assert.False(player.IsLooting);
		var message = Assert.IsType<SmSystemMessage>(Assert.Single(result.PlayerPackets));
		Assert.Equal(1300829, message.MessageId);
		Assert.Equal(1002, registration.LootingPlayerObjectId);
	}

	[Fact]
	public void CloseDropList_ClearsLootingStateAndRegistration()
	{
		var dropRegistration = new WorldNpcDropRegistrationService();
		dropRegistration.RegisterDrop(5001, looterObjectId: 1001, drops: [new WorldNpcDropItem(1, 182400001, 1)]);
		var service = new WorldNpcLootService(dropRegistration);
		var player = CreatePlayer(1001);
		Assert.Equal(WorldNpcLootStatus.Opened, service.RequestDropList(player, 5001).Status);

		var result = service.CloseDropList(player, 5001);

		Assert.Equal(WorldNpcLootStatus.Closed, result.Status);
		Assert.True(player.IsInState(PlayerCreatureState.Active));
		Assert.False(player.IsLooting);
		Assert.Equal(0, player.LootingNpcObjectId);
		Assert.True(dropRegistration.TryGetRegistration(5001, out var registration));
		Assert.Null(registration!.LootingPlayerObjectId);
		var emotion = Assert.IsType<SmEmotion>(Assert.Single(result.VisiblePlayerPackets));
		Assert.Equal(37, emotion.OpCode);
	}

	[Fact]
	public void CreateLootEnableStatus_UsesFirstDropLootEffect()
	{
		var dropRegistration = new WorldNpcDropRegistrationService();
		dropRegistration.RegisterDrop(
			5001,
			looterObjectId: 1001,
			drops:
			[
				new WorldNpcDropItem(1, 182400001, 1),
				new WorldNpcDropItem(2, 188053547, 1),
				new WorldNpcDropItem(3, 166020000, 1),
			]);
		var service = new WorldNpcLootService(dropRegistration);

		var status = service.CreateLootEnableStatus(5001);

		Assert.Equal(SmLootStatusType.LootEnable, status.Status);
		Assert.Equal(1002, status.LootEffectId);
	}

	private static Player CreatePlayer(int objectId)
	{
		return new Player
		{
			ObjectId = objectId,
			CreatureState = PlayerCreatureState.Active,
		};
	}
}
